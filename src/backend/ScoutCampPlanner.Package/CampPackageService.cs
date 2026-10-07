using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System.Text.Json;
using ScoutCampPlanner.Camp.Domain;
using ScoutCampPlanner.Camp.Infrastructure;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Catering.Infrastructure.Offline;
using ScoutCampPlanner.Platform.Domain;
using ScoutCampPlanner.Platform.Infrastructure;

namespace ScoutCampPlanner.Package;

public sealed class CampPackageService(
    PlatformDbContext platform,
    CampDbContext camp,
    CateringDbContext catering,
    TimeProvider timeProvider,
    ICampPackageParticipantAccess? participantAccess = null)
{
    private static readonly string[] IncludedModules = ["Camp", "Catering"];
    private bool externalTransaction;

    public async Task<bool> RemoveLocalCampAsync(Guid deviceId, Guid campId, Guid transferId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        try
        {
            await camp.Camps.Where(value => value.Id == campId).ExecuteUpdateAsync(
                setters => setters.SetProperty(value => value.BaselineVersion, value => value.BaselineVersion), cancellationToken);
            var local = await camp.Camps.AsNoTracking().SingleOrDefaultAsync(x => x.Id == campId &&
                !x.IsFrozen && x.ActiveTransferId == transferId, cancellationToken);
            if (local is null || transferId == Guid.Empty || !await platform.LocalCampAccessGrants.AnyAsync(
                x => x.DeviceIdentityId == deviceId && x.CampId == campId && x.TenantId == local.TenantId &&
                     x.TransferId == transferId, cancellationToken)) return false;
            if (await ContainsParticipantPlanningAsync(campId, cancellationToken))
                await DemandParticipantEditAsync(campId, cancellationToken);
            await new LocalCampRemovalStore(catering).DeleteAsync(campId, cancellationToken);
            await camp.Participants.Where(x => x.CampId == campId).ExecuteDeleteAsync(cancellationToken);
            await camp.ParticipantEstimates.Where(x => x.CampId == campId).ExecuteDeleteAsync(cancellationToken);
            // Remove children before parents because the tree uses restrictive parent foreign keys.
            while (await camp.StructureNodes.AnyAsync(x => x.CampId == campId, cancellationToken))
            {
                int removed = await camp.StructureNodes.Where(x => x.CampId == campId &&
                    !camp.StructureNodes.Any(child => child.ParentId == x.Id)).ExecuteDeleteAsync(cancellationToken);
                if (removed == 0) throw new InvalidOperationException("Invalid local structure.");
            }
            await camp.CampStages.Where(x => x.CampId == campId).ExecuteDeleteAsync(cancellationToken);
            await platform.LocalCampAccessGrants.Where(x => x.CampId == campId).ExecuteDeleteAsync(cancellationToken);
            await camp.Camps.Where(x => x.Id == campId).ExecuteDeleteAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            camp.ChangeTracker.Clear(); catering.ChangeTracker.Clear();
            if (!externalTransaction) platform.ChangeTracker.Clear();
            return true;
        }
        catch { await transaction.RollbackAsync(CancellationToken.None); throw; }
        finally { await DetachEnlistedTransactionsAsync(cancellationToken); }
    }

    public async Task<byte[]> StartOfflineTransferAsync(Guid campId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        try
        {
        int available = await camp.Camps.Where(value => value.Id == campId && !value.IsFrozen && value.ActiveTransferId == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.BaselineVersion, value => value.BaselineVersion), cancellationToken);
        if (available != 1) throw new InvalidOperationException("Camp is unavailable or already has an active transfer.");
        var entity = await camp.Camps.SingleOrDefaultAsync(x => x.Id == campId, cancellationToken)
            ?? throw new KeyNotFoundException("Camp was not found.");
        await camp.Entry(entity).ReloadAsync(cancellationToken);
        var tenant = await platform.Tenants.SingleOrDefaultAsync(x => x.Id == entity.TenantId, cancellationToken)
            ?? throw new InvalidOperationException("Camp tenant was not found.");

        var transferId = Guid.NewGuid();
        entity.Freeze(transferId);
        await camp.SaveChangesAsync(cancellationToken);

        byte[] bytes = await BuildAsync(entity, tenant, CampPackageDirection.CloudToLocal, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return bytes;
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            camp.ChangeTracker.Clear();
            throw;
        }
        finally { await DetachEnlistedTransactionsAsync(CancellationToken.None); }
    }

    public async Task<byte[]> CreateReturnPackageAsync(Guid campId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await BeginTransactionAsync(cancellationToken);
        try
        {
        // Serialize with participant edits and transfer state changes on both providers.
        await camp.Camps.Where(value => value.Id == campId).ExecuteUpdateAsync(
            setters => setters.SetProperty(value => value.BaselineVersion, value => value.BaselineVersion), cancellationToken);
        var entity = await camp.Camps.SingleOrDefaultAsync(x => x.Id == campId, cancellationToken)
            ?? throw new KeyNotFoundException("Camp was not found.");
        if (entity.IsFrozen || entity.ActiveTransferId is null)
            throw new InvalidOperationException("Camp has no active offline transfer.");
        var tenant = await platform.Tenants.SingleAsync(x => x.Id == entity.TenantId, cancellationToken);
        byte[] bytes = await BuildAsync(entity, tenant, CampPackageDirection.LocalToCloud, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return bytes;
        }
        catch { await transaction.RollbackAsync(CancellationToken.None); throw; }
        finally { await DetachEnlistedTransactionsAsync(CancellationToken.None); }
    }

    public Task ImportInitialPackageAsync(byte[] bytes, CancellationToken cancellationToken = default) =>
        ImportInitialPackageCoreAsync(bytes, null, cancellationToken);

    public Task ImportInitialPackageForDeviceAsync(byte[] bytes, Guid deviceIdentityId,
        CancellationToken cancellationToken = default)
    {
        if (deviceIdentityId == Guid.Empty) throw new ArgumentException("A local device identity is required.");
        return ImportInitialPackageCoreAsync(bytes, deviceIdentityId, cancellationToken);
    }

    private async Task ImportInitialPackageCoreAsync(byte[] bytes, Guid? deviceIdentityId,
        CancellationToken cancellationToken)
    {
        var package = CampPackageSerializer.Deserialize(bytes);
        if (package.Manifest.Direction != CampPackageDirection.CloudToLocal)
            throw new CampPackageValidationException("Expected a cloud-to-local package.");

        await using var transaction = await BeginTransactionAsync(cancellationToken);
        try
        {
            if (deviceIdentityId.HasValue && !await platform.LocalDeviceIdentities.AnyAsync(
                    value => value.Id == deviceIdentityId.Value, cancellationToken))
                throw new CampPackageValidationException("Local device identity was not found.");
            if (await camp.Camps.AnyAsync(x => x.Id == package.Camp.Id, cancellationToken))
                throw new CampPackageValidationException("Camp already exists locally.");
            if (!await platform.Tenants.AnyAsync(x => x.Id == package.Tenant.Id, cancellationToken))
                platform.Tenants.Add(new Tenant(package.Tenant.Id, package.Tenant.Name));

            var importedCamp = new Camp.Domain.Camp(
                package.Camp.Id, package.Camp.TenantId, package.Camp.Name,
                package.Camp.StartDate, package.Camp.EndDate);
            importedCamp.ConfigureStructure(package.Camp.StructureMode == CampStructureMode.Fixed.ToString()
                ? package.Camp.StructureLevelNames : []);
            importedCamp.BeginLocalTransfer(package.Manifest.TransferId, package.Manifest.BaselineVersion);
            camp.Camps.Add(importedCamp);
            if (deviceIdentityId.HasValue)
                platform.LocalCampAccessGrants.Add(new LocalCampAccess(deviceIdentityId.Value,
                    package.Camp.TenantId, package.Camp.Id, package.Manifest.TransferId));
            camp.CampStages.AddRange(package.CampStages.Select(x => new CampStage(x.Id, x.CampId, x.Name, x.SortOrder)));
            camp.StructureNodes.AddRange(OrderStructureNodes(package.StructureNodes)
                .Select(x => new StructureNode(x.Id, x.CampId, x.ParentId, x.Name)));
            camp.ParticipantEstimates.AddRange(package.ParticipantEstimates.Select(x => new ParticipantEstimate(
                x.Id, x.CampId, x.StructureNodeId, x.CampStageId, x.ChildYouthCount, x.LeaderCount)));
            catering.CampStageFoodFactors.AddRange(package.CampStageFoodFactors.Select(x => new CampStageFoodFactor(
                x.Id, x.CampId, x.CampStageId, x.StageName, x.Factor)));
            catering.CampMealTypes.AddRange((package.CampMealTypes ?? []).Select(x => new CampMealType(x.Id, x.CampId, x.Name, x.SortOrder)));
            catering.CampMeals.AddRange((package.CampMeals ?? []).Select(x => new CampMeal(
                x.Id, x.CampId, x.MealTypeId, x.Date, x.IsActive, x.ChangeVersion)));
            await new CampOfflineReferenceStore(catering).ImportAsync(
                package.CateringReferenceData, package.Camp.Id, cancellationToken,
                CampMealPlanningPackageStore.ReadRecipeRevisionIds(package.CateringMealPlanningData, package.Camp.Id));
            await new CampMealPlanningPackageStore(catering).ImportAsync(
                package.CateringMealPlanningData, package.Camp.Id, cancellationToken);
            // Persist catalogue closure before resolving participant references in this transaction.
            await catering.SaveChangesAsync(cancellationToken);
            await ImportParticipantsAsync(package, initial: true, cancellationToken);
            await SaveAllAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            camp.ChangeTracker.Clear(); catering.ChangeTracker.Clear(); platform.ChangeTracker.Clear();
            throw;
        }
        finally
        {
            await DetachEnlistedTransactionsAsync(cancellationToken);
        }
    }

    public async Task ImportReturnPackageAsync(byte[] bytes, CancellationToken cancellationToken = default)
    {
        var package = CampPackageSerializer.Deserialize(bytes);
        if (package.Manifest.Direction != CampPackageDirection.LocalToCloud)
            throw new CampPackageValidationException("Expected a local-to-cloud package.");

        await using var transaction = await BeginTransactionAsync(cancellationToken);
        try
        {
            // Serialize return import against explicit cancellation on both providers.
            int active = await camp.Camps.Where(value => value.Id == package.Camp.Id && value.IsFrozen &&
                    value.TenantId == package.Manifest.TenantId && value.ActiveTransferId == package.Manifest.TransferId &&
                    value.BaselineVersion == package.Manifest.BaselineVersion)
                .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.BaselineVersion,
                    package.Manifest.BaselineVersion), cancellationToken);
            if (active != 1)
                throw new CampPackageValidationException("Return package does not match the active transfer baseline.");
            var existing = await camp.Camps.SingleOrDefaultAsync(x => x.Id == package.Camp.Id, cancellationToken)
                ?? throw new CampPackageValidationException("Target camp does not exist.");
            if (existing.TenantId != package.Manifest.TenantId ||
                existing.ActiveTransferId != package.Manifest.TransferId ||
                existing.BaselineVersion != package.Manifest.BaselineVersion ||
                !existing.IsFrozen)
                throw new CampPackageValidationException("Return package does not match the active transfer baseline.");

            bool hasParticipants = await camp.Participants.AnyAsync(value => value.CampId == existing.Id, cancellationToken);
            if (await catering.Set<MealPlanningParticipantConfiguration>().AnyAsync(value => value.CampId == existing.Id, cancellationToken) &&
                CampMealPlanningPackageStore.ReadPackageData(package.CateringMealPlanningData, package.Camp.Id).ParticipantConfiguration is null)
                throw new CampPackageValidationException("Return package is missing participant planning configuration; replacement was cancelled.");
            if (hasParticipants && package.Participants is null)
                throw new CampPackageValidationException("Return package is missing participant data; replacement was cancelled.");
            if (await ContainsParticipantPlanningAsync(existing.Id, cancellationToken) || package.Participants?.Items.Count > 0 ||
                CampMealPlanningPackageStore.ReadPackageData(package.CateringMealPlanningData, package.Camp.Id).ParticipantConfiguration is not null)
                await DemandParticipantEditAsync(existing.Id, cancellationToken);
            if (package.Participants is not null)
            {
                // Tracked owned entities must also be discarded before replacing stable participant IDs.
                foreach (var participant in await camp.Participants.Where(value => value.CampId == existing.Id).ToArrayAsync(cancellationToken))
                    camp.Participants.Remove(participant);
                await camp.SaveChangesAsync(cancellationToken);
                await ImportParticipantsAsync(package, initial: false, cancellationToken);
            }

            await camp.ParticipantEstimates.Where(x => x.CampId == existing.Id).ExecuteDeleteAsync(cancellationToken);
            await camp.StructureNodes.Where(x => x.CampId == existing.Id).ExecuteDeleteAsync(cancellationToken);
            await camp.CampStages.Where(x => x.CampId == existing.Id).ExecuteDeleteAsync(cancellationToken);
            await new CampMealPlanningPackageStore(catering).DeleteCampDataAsync(existing.Id, cancellationToken);
            await catering.CampMeals.Where(x => x.CampId == existing.Id).ExecuteDeleteAsync(cancellationToken);
            await catering.CampMealTypes.Where(x => x.CampId == existing.Id).ExecuteDeleteAsync(cancellationToken);
            await catering.CampStageFoodFactors.Where(x => x.CampId == existing.Id).ExecuteDeleteAsync(cancellationToken);
            // ExecuteDelete bypasses the change tracker. The complete Catering camp slice is replaced below,
            // so no previously tracked instance may remain authoritative in this scoped context.
            catering.ChangeTracker.Clear();
            foreach (var entry in camp.ChangeTracker.Entries<StructureNode>().Where(x => x.Entity.CampId == existing.Id))
                entry.State = EntityState.Detached;
            foreach (var entry in camp.ChangeTracker.Entries<CampStage>().Where(x => x.Entity.CampId == existing.Id))
                entry.State = EntityState.Detached;
            foreach (var entry in camp.ChangeTracker.Entries<ParticipantEstimate>().Where(x => x.Entity.CampId == existing.Id))
                entry.State = EntityState.Detached;
            camp.StructureNodes.AddRange(OrderStructureNodes(package.StructureNodes)
                .Select(x => new StructureNode(x.Id, x.CampId, x.ParentId, x.Name)));
            camp.CampStages.AddRange(package.CampStages.Select(x => new CampStage(x.Id, x.CampId, x.Name, x.SortOrder)));
            camp.ParticipantEstimates.AddRange(package.ParticipantEstimates.Select(x => new ParticipantEstimate(
                x.Id, x.CampId, x.StructureNodeId, x.CampStageId, x.ChildYouthCount, x.LeaderCount)));
            catering.CampMealTypes.AddRange((package.CampMealTypes ?? []).Select(x => new CampMealType(x.Id, x.CampId, x.Name, x.SortOrder)));
            catering.CampMeals.AddRange((package.CampMeals ?? []).Select(x => new CampMeal(
                x.Id, x.CampId, x.MealTypeId, x.Date, x.IsActive, x.ChangeVersion)));
            catering.CampStageFoodFactors.AddRange(package.CampStageFoodFactors.Select(x => new CampStageFoodFactor(
                x.Id, x.CampId, x.CampStageId, x.StageName, x.Factor)));
            await new CampMealPlanningPackageStore(catering).ImportAsync(
                package.CateringMealPlanningData, package.Camp.Id, cancellationToken);
            existing.CompleteTransfer(package.Manifest.TransferId, package.Manifest.BaselineVersion);
            existing.UpdateDetails(package.Camp.Name, package.Camp.StartDate, package.Camp.EndDate);
            existing.ConfigureStructure(package.Camp.StructureMode == CampStructureMode.Fixed.ToString()
                ? package.Camp.StructureLevelNames : []);
            await SaveAllAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            camp.ChangeTracker.Clear(); catering.ChangeTracker.Clear(); platform.ChangeTracker.Clear();
            throw;
        }
        finally
        {
            await DetachEnlistedTransactionsAsync(cancellationToken);
        }
    }

    private async Task<byte[]> BuildAsync(Camp.Domain.Camp entity, Tenant tenant, CampPackageDirection direction, CancellationToken cancellationToken)
    {
        if (await ContainsParticipantPlanningAsync(entity.Id, cancellationToken))
        {
            if (participantAccess is null) throw new CampPackageParticipantAccessException();
            await participantAccess.DemandReadAsync(entity.Id, cancellationToken);
        }
        var participants = await camp.GetParticipantsAsync(entity.Id, cancellationToken);
        Guid[] dietIds = participants.Where(value => value.DietTypeId.HasValue).Select(value => value.DietTypeId!.Value).Distinct().ToArray();
        var dietRows = await catering.DietaryRequirements.AsNoTracking().Where(value => dietIds.Contains(value.Id) &&
            (value.TenantId == null || value.TenantId == entity.TenantId)).ToArrayAsync(cancellationToken);
        var diets = dietRows.Select(value => new ParticipantDietReference(value.Id, value.Name, value.TenantId,
            value.Description, value.SortOrder, value.Version, DietaryCatalogStore.Document(value).Rules)).ToArray();
        var structureNodes = await camp.StructureNodes.Where(x => x.CampId == entity.Id)
            .Select(x => new StructureNodeData(x.Id, x.CampId, x.ParentId, x.Name)).ToListAsync(cancellationToken);
        var stages = await camp.CampStages.Where(x => x.CampId == entity.Id).OrderBy(x => x.SortOrder)
            .Select(x => new CampStageData(x.Id, x.CampId, x.Name, x.SortOrder)).ToListAsync(cancellationToken);
        var estimates = await camp.ParticipantEstimates.Where(x => x.CampId == entity.Id)
            .Select(x => new ParticipantEstimateData(x.Id, x.CampId, x.StructureNodeId, x.CampStageId,
                x.ChildYouthCount, x.LeaderCount)).ToListAsync(cancellationToken);
        var foodFactors = await catering.CampStageFoodFactors.Where(x => x.CampId == entity.Id)
            .Select(x => new CampStageFoodFactorData(x.Id, x.CampId, x.CampStageId, x.StageName, x.Factor))
            .ToListAsync(cancellationToken);
        if (foodFactors.Count == 0)
        {
            var tenantFoodFactors = await catering.TenantStageFoodFactors.Where(x => x.TenantId == entity.TenantId)
                .ToDictionaryAsync(x => x.NormalizedStageName, cancellationToken);
            foodFactors.AddRange(stages.Select(stage => new CampStageFoodFactorData(Guid.NewGuid(), entity.Id,
                stage.Id, stage.Name, tenantFoodFactors.TryGetValue(stage.Name.Trim().ToUpperInvariant(), out var factor)
                    ? factor.Factor : 1m)));
        }
        var mealTypes = await catering.CampMealTypes.Where(x => x.CampId == entity.Id).OrderBy(x => x.SortOrder)
            .Select(x => new CampMealTypeData(x.Id, x.CampId, x.Name, x.SortOrder)).ToListAsync(cancellationToken);
        var campMeals = await catering.CampMeals.Where(x => x.CampId == entity.Id)
            .Select(x => new CampMealData(
                x.Id, x.CampId, x.MealTypeId, x.Date, x.IsActive, x.ChangeVersion)).ToListAsync(cancellationToken);
        JsonElement cateringMealPlanningData = await new CampMealPlanningPackageStore(catering)
            .ExportAsync(entity.Id, cancellationToken);
        JsonElement cateringReferenceData = await new CampOfflineReferenceStore(catering)
            .ExportAsync(entity.Id, cancellationToken,
                CampMealPlanningPackageStore.ReadRecipeRevisionIds(cateringMealPlanningData, entity.Id));
        var manifest = new CampPackageManifest(CampPackageVersions.Current, tenant.Id, entity.Id,
            entity.ActiveTransferId!.Value, entity.BaselineVersion, direction, IncludedModules,
            timeProvider.GetUtcNow());
        return CampPackageSerializer.Serialize(new CampPackagePayload(manifest,
            new TenantData(tenant.Id, tenant.Name), new CampData(
                entity.Id, entity.TenantId, entity.Name,
                entity.StartDate ?? throw new InvalidOperationException("Legacy camps without a period cannot be exported."),
                entity.EndDate ?? throw new InvalidOperationException("Legacy camps without a period cannot be exported."),
                entity.StructureMode.ToString(), entity.GetStructureLevelNames()), stages, estimates, foodFactors,
            structureNodes, mealTypes, campMeals, cateringReferenceData, cateringMealPlanningData,
            new CampParticipantPackageData(2, true, participants, diets)));
    }

    private async Task DemandParticipantEditAsync(Guid campId, CancellationToken ct)
    {
        if (participantAccess is null) throw new CampPackageParticipantAccessException();
        await participantAccess.DemandEditAsync(campId, ct);
    }

    private async Task<bool> ContainsParticipantPlanningAsync(Guid campId, CancellationToken ct) =>
        await camp.Participants.AnyAsync(value => value.CampId == campId, ct) ||
        await catering.Set<MealPlanningParticipantConfiguration>().AnyAsync(value => value.CampId == campId, ct) ||
        await catering.CookingUnitMealStates.AnyAsync(value => value.CampId == campId && value.DemandBasis == EffectiveDemandBasis.ActualParticipants, ct);

    private async Task ImportParticipantsAsync(CampPackagePayload package, bool initial, CancellationToken ct)
    {
        if (package.Participants is not { } data) return;
        if (initial)
        {
            foreach (var diet in data.DietTypes)
            {
                var imported = DietaryRequirement.Restore(diet.Id, diet.Name, diet.TenantId,
                    diet.Description, diet.SortOrder, diet.Version,
                    (diet.Rules ?? []).Select(rule => new DietaryOriginRule(rule.OriginId, rule.Decision)));
                var existing = await catering.DietaryRequirements.SingleOrDefaultAsync(value => value.Id == diet.Id, ct);
                if (existing is null) catering.DietaryRequirements.Add(imported);
                else if (JsonSerializer.Serialize(DietaryCatalogStore.Document(existing)) !=
                         JsonSerializer.Serialize(DietaryCatalogStore.Document(imported)))
                    throw new CampPackageValidationException("The local dietary catalogue conflicts with the package reference.");
            }
            await catering.SaveChangesAsync(ct);
        }
        var catalog = new ParticipantRequirementCatalogStore(catering);
        foreach (var item in data.Items)
        {
            if (!await catalog.ContainsAsync(item.DietTypeId, item.AllergenIds,
                    item.Intolerances.Select(value => value.SubstanceId).ToArray(), ct, package.Camp.TenantId))
                throw new CampPackageValidationException("Participant catalogue reference is unavailable.");
            camp.Participants.Add(CampParticipantPackageValidation.Restore(item));
        }
    }

    private async Task<PackageTransaction> BeginTransactionAsync(CancellationToken cancellationToken)
    {
        externalTransaction = platform.Database.CurrentTransaction is not null;
        IDbContextTransaction transaction;
        if (externalTransaction)
            transaction = (await camp.Database.UseTransactionAsync(
                platform.Database.CurrentTransaction!.GetDbTransaction(), cancellationToken))!;
        else
        {
            transaction = await camp.Database.BeginTransactionAsync(cancellationToken);
            await platform.Database.UseTransactionAsync(transaction.GetDbTransaction(), cancellationToken);
        }
        await catering.Database.UseTransactionAsync(transaction.GetDbTransaction(), cancellationToken);
        return new PackageTransaction(transaction, !externalTransaction);
    }

    private async Task DetachEnlistedTransactionsAsync(CancellationToken cancellationToken)
    {
        if (!externalTransaction) await platform.Database.UseTransactionAsync(null, cancellationToken);
        await catering.Database.UseTransactionAsync(null, cancellationToken);
        if (externalTransaction) await camp.Database.UseTransactionAsync(null, cancellationToken);
    }

    // An outer audited operation owns its commit: package writes must not commit before the audit.
    private sealed class PackageTransaction(IDbContextTransaction transaction, bool ownsCommit) : IAsyncDisposable
    {
        public Task CommitAsync(CancellationToken ct) => ownsCommit ? transaction.CommitAsync(ct) : Task.CompletedTask;
        public Task RollbackAsync(CancellationToken ct) => ownsCommit ? transaction.RollbackAsync(ct) : Task.CompletedTask;
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }

    private async Task SaveAllAsync(CancellationToken cancellationToken)
    {
        await platform.SaveChangesAsync(cancellationToken);
        await camp.SaveChangesAsync(cancellationToken);
        await catering.SaveChangesAsync(cancellationToken);
    }

    private static IReadOnlyList<StructureNodeData> OrderStructureNodes(
        IReadOnlyList<StructureNodeData> nodes)
    {
        var remaining = nodes.ToDictionary(node => node.Id);
        var ordered = new List<StructureNodeData>(nodes.Count);
        var added = new HashSet<Guid>();
        while (remaining.Count > 0)
        {
            StructureNodeData[] next = remaining.Values
                .Where(node => node.ParentId is null || added.Contains(node.ParentId.Value)).ToArray();
            if (next.Length == 0)
                throw new CampPackageValidationException("Camp structure contains a cycle or missing parent.");
            foreach (StructureNodeData node in next)
            {
                ordered.Add(node);
                added.Add(node.Id);
                remaining.Remove(node.Id);
            }
        }
        return ordered;
    }
}
