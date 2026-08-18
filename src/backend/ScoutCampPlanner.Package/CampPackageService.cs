using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ScoutCampPlanner.Camp.Domain;
using ScoutCampPlanner.Camp.Infrastructure;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Platform.Domain;
using ScoutCampPlanner.Platform.Infrastructure;

namespace ScoutCampPlanner.Package;

public sealed class CampPackageService(
    PlatformDbContext platform,
    CampDbContext camp,
    CateringDbContext catering,
    TimeProvider timeProvider)
{
    private static readonly string[] IncludedModules = ["Camp", "Catering"];

    public async Task<byte[]> StartOfflineTransferAsync(Guid campId, CancellationToken cancellationToken = default)
    {
        var entity = await camp.Camps.SingleOrDefaultAsync(x => x.Id == campId, cancellationToken)
            ?? throw new KeyNotFoundException("Camp was not found.");
        var tenant = await platform.Tenants.SingleOrDefaultAsync(x => x.Id == entity.TenantId, cancellationToken)
            ?? throw new InvalidOperationException("Camp tenant was not found.");

        var transferId = Guid.NewGuid();
        entity.Freeze(transferId);
        await camp.SaveChangesAsync(cancellationToken);

        return await BuildAsync(entity, tenant, CampPackageDirection.CloudToLocal, cancellationToken);
    }

    public async Task<byte[]> CreateReturnPackageAsync(Guid campId, CancellationToken cancellationToken = default)
    {
        var entity = await camp.Camps.SingleOrDefaultAsync(x => x.Id == campId, cancellationToken)
            ?? throw new KeyNotFoundException("Camp was not found.");
        if (!entity.IsFrozen || entity.ActiveTransferId is null)
            throw new InvalidOperationException("Camp has no active offline transfer.");
        var tenant = await platform.Tenants.SingleAsync(x => x.Id == entity.TenantId, cancellationToken);
        return await BuildAsync(entity, tenant, CampPackageDirection.LocalToCloud, cancellationToken);
    }

    public async Task ImportInitialPackageAsync(byte[] bytes, CancellationToken cancellationToken = default)
    {
        var package = CampPackageSerializer.Deserialize(bytes);
        if (package.Manifest.Direction != CampPackageDirection.CloudToLocal)
            throw new CampPackageValidationException("Expected a cloud-to-local package.");

        await using var transaction = await camp.Database.BeginTransactionAsync(cancellationToken);
        await EnlistAsync(transaction, cancellationToken);
        try
        {
            if (await camp.Camps.AnyAsync(x => x.Id == package.Camp.Id, cancellationToken))
                throw new CampPackageValidationException("Camp already exists locally.");
            if (!await platform.Tenants.AnyAsync(x => x.Id == package.Tenant.Id, cancellationToken))
                platform.Tenants.Add(new Tenant(package.Tenant.Id, package.Tenant.Name));

            var importedCamp = new Camp.Domain.Camp(
                package.Camp.Id, package.Camp.TenantId, package.Camp.Name,
                package.Camp.StartDate, package.Camp.EndDate);
            importedCamp.ConfigureStructure(package.Camp.StructureMode == CampStructureMode.Fixed.ToString()
                ? package.Camp.StructureLevelNames : []);
            importedCamp.Freeze(package.Manifest.TransferId);
            camp.Camps.Add(importedCamp);
            camp.CampStages.AddRange(package.CampStages.Select(x => new CampStage(x.Id, x.CampId, x.Name, x.SortOrder)));
            camp.StructureNodes.AddRange(OrderStructureNodes(package.StructureNodes)
                .Select(x => new StructureNode(x.Id, x.CampId, x.ParentId, x.Name)));
            camp.ParticipantEstimates.AddRange(package.ParticipantEstimates.Select(x => new ParticipantEstimate(
                x.Id, x.CampId, x.StructureNodeId, x.CampStageId, x.ChildYouthCount, x.LeaderCount)));
            catering.MealPlans.AddRange(package.MealPlans.Select(x => new MealPlan(x.Id, x.CampId, x.Name)));
            await SaveAllAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
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

        await using var transaction = await camp.Database.BeginTransactionAsync(cancellationToken);
        await EnlistAsync(transaction, cancellationToken);
        try
        {
            var existing = await camp.Camps.SingleOrDefaultAsync(x => x.Id == package.Camp.Id, cancellationToken)
                ?? throw new CampPackageValidationException("Target camp does not exist.");
            if (existing.TenantId != package.Manifest.TenantId ||
                existing.ActiveTransferId != package.Manifest.TransferId ||
                existing.BaselineVersion != package.Manifest.BaselineVersion ||
                !existing.IsFrozen)
                throw new CampPackageValidationException("Return package does not match the active transfer baseline.");

            await camp.ParticipantEstimates.Where(x => x.CampId == existing.Id).ExecuteDeleteAsync(cancellationToken);
            await camp.StructureNodes.Where(x => x.CampId == existing.Id).ExecuteDeleteAsync(cancellationToken);
            await camp.CampStages.Where(x => x.CampId == existing.Id).ExecuteDeleteAsync(cancellationToken);
            await catering.MealPlans.Where(x => x.CampId == existing.Id).ExecuteDeleteAsync(cancellationToken);
            foreach (var entry in camp.ChangeTracker.Entries<StructureNode>().Where(x => x.Entity.CampId == existing.Id))
                entry.State = EntityState.Detached;
            foreach (var entry in camp.ChangeTracker.Entries<CampStage>().Where(x => x.Entity.CampId == existing.Id))
                entry.State = EntityState.Detached;
            foreach (var entry in camp.ChangeTracker.Entries<ParticipantEstimate>().Where(x => x.Entity.CampId == existing.Id))
                entry.State = EntityState.Detached;
            foreach (var entry in catering.ChangeTracker.Entries<MealPlan>().Where(x => x.Entity.CampId == existing.Id))
                entry.State = EntityState.Detached;
            camp.StructureNodes.AddRange(OrderStructureNodes(package.StructureNodes)
                .Select(x => new StructureNode(x.Id, x.CampId, x.ParentId, x.Name)));
            camp.CampStages.AddRange(package.CampStages.Select(x => new CampStage(x.Id, x.CampId, x.Name, x.SortOrder)));
            camp.ParticipantEstimates.AddRange(package.ParticipantEstimates.Select(x => new ParticipantEstimate(
                x.Id, x.CampId, x.StructureNodeId, x.CampStageId, x.ChildYouthCount, x.LeaderCount)));
            catering.MealPlans.AddRange(package.MealPlans.Select(x => new MealPlan(x.Id, x.CampId, x.Name)));
            existing.CompleteTransfer(package.Manifest.TransferId, package.Manifest.BaselineVersion);
            existing.ConfigureStructure(package.Camp.StructureMode == CampStructureMode.Fixed.ToString()
                ? package.Camp.StructureLevelNames : []);
            await SaveAllAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
        finally
        {
            await DetachEnlistedTransactionsAsync(cancellationToken);
        }
    }

    private async Task<byte[]> BuildAsync(Camp.Domain.Camp entity, Tenant tenant, CampPackageDirection direction, CancellationToken cancellationToken)
    {
        var structureNodes = await camp.StructureNodes.Where(x => x.CampId == entity.Id)
            .Select(x => new StructureNodeData(x.Id, x.CampId, x.ParentId, x.Name)).ToListAsync(cancellationToken);
        var stages = await camp.CampStages.Where(x => x.CampId == entity.Id).OrderBy(x => x.SortOrder)
            .Select(x => new CampStageData(x.Id, x.CampId, x.Name, x.SortOrder)).ToListAsync(cancellationToken);
        var estimates = await camp.ParticipantEstimates.Where(x => x.CampId == entity.Id)
            .Select(x => new ParticipantEstimateData(x.Id, x.CampId, x.StructureNodeId, x.CampStageId,
                x.ChildYouthCount, x.LeaderCount)).ToListAsync(cancellationToken);
        var meals = await catering.MealPlans.Where(x => x.CampId == entity.Id)
            .Select(x => new MealPlanData(x.Id, x.CampId, x.Name)).ToListAsync(cancellationToken);
        var manifest = new CampPackageManifest(CampPackageVersions.Current, tenant.Id, entity.Id,
            entity.ActiveTransferId!.Value, entity.BaselineVersion, direction, IncludedModules,
            timeProvider.GetUtcNow());
        return CampPackageSerializer.Serialize(new CampPackagePayload(manifest,
            new TenantData(tenant.Id, tenant.Name), new CampData(
                entity.Id, entity.TenantId, entity.Name,
                entity.StartDate ?? throw new InvalidOperationException("Legacy camps without a period cannot be exported."),
                entity.EndDate ?? throw new InvalidOperationException("Legacy camps without a period cannot be exported."),
                entity.StructureMode.ToString(), entity.GetStructureLevelNames()), stages, estimates, structureNodes, meals));
    }

    private async Task EnlistAsync(IDbContextTransaction transaction, CancellationToken cancellationToken)
    {
        await platform.Database.UseTransactionAsync(transaction.GetDbTransaction(), cancellationToken);
        await catering.Database.UseTransactionAsync(transaction.GetDbTransaction(), cancellationToken);
    }

    private async Task DetachEnlistedTransactionsAsync(CancellationToken cancellationToken)
    {
        await platform.Database.UseTransactionAsync(null, cancellationToken);
        await catering.Database.UseTransactionAsync(null, cancellationToken);
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
