using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Application.MealPlanning;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure.Ingredients;

namespace ScoutCampPlanner.Catering.Infrastructure;

public sealed class DietaryCatalogStore(CateringDbContext database) : IDietaryCatalogStore
{
    public async Task<DietaryMutationResult> SetThresholdDefaultAsync(Guid substanceId, SubstanceThresholdDefaultRequest request, CancellationToken ct)
    {
        if (request.GramsPerPortion is decimal amount && (amount < 0 || amount > 999999999999.999999m || decimal.Round(amount, 6) != amount) ||
            request.Source?.Length > 500 || request.GramsPerPortion.HasValue && string.IsNullOrWhiteSpace(request.Source))
            return new(DietaryMutationStatus.Invalid);
        var substance = await database.Set<IngredientIntoleranceDefinitionRecord>().SingleOrDefaultAsync(value => value.Id == substanceId && value.Status == 0, ct);
        if (substance is null) return new(DietaryMutationStatus.NotFound);
        if (!substance.IsQuantityDependent) return new(DietaryMutationStatus.Invalid);
        if (substance.DefaultThresholdVersion != request.ExpectedVersion) return new(DietaryMutationStatus.Conflict);
        substance.DefaultThresholdGramsPerPortion = request.GramsPerPortion;
        substance.DefaultThresholdSource = request.GramsPerPortion.HasValue ? request.Source!.Trim() : null;
        substance.DefaultThresholdVersion++;
        try { await database.SaveChangesAsync(ct); return new(DietaryMutationStatus.Success, substanceId); }
        catch (DbUpdateConcurrencyException) { database.ChangeTracker.Clear(); return new(DietaryMutationStatus.Conflict); }
    }

    public static readonly IReadOnlySet<string> MainOriginCodes = new HashSet<string>(StringComparer.Ordinal)
    { "PLANT", "FUNGI", "MINERAL", "SYNTHETIC", "MICROBIAL", "MEAT", "POULTRY", "FISH", "CRUSTACEAN", "MOLLUSC", "DAIRY", "EGG", "HONEY", "INSECT" };

    public async Task<IReadOnlyList<DietaryTypeDocument>> ListAsync(Guid? tenantId, CancellationToken ct) =>
        (await database.DietaryRequirements.AsNoTracking().Where(value => value.TenantId == null || value.TenantId == tenantId)
            .OrderBy(value => value.SortOrder).ThenBy(value => value.Name).ToArrayAsync(ct)).Select(Document).ToArray();

    public async Task<DietaryMutationResult> SaveAsync(Guid id, Guid? tenantId, SaveDietaryTypeRequest request, CancellationToken ct)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty || request is null || request.Rules is null || request.Rules.Any(value => value is null))
            return new(DietaryMutationStatus.Invalid);
        await using var transaction = await database.Database.BeginTransactionAsync(ct);
        try
        {
            var existing = await database.DietaryRequirements.SingleOrDefaultAsync(value => value.Id == id, ct);
            if (existing is not null && existing.TenantId != tenantId) return new(DietaryMutationStatus.NotFound);
            if ((existing?.Version ?? 0) != request.ExpectedVersion) return new(DietaryMutationStatus.Conflict);
            var origins = await database.Set<IngredientOriginPropertyRecord>().AsNoTracking()
                .Where(value => value.Status == 0).ToArrayAsync(ct);
            if (request.Rules.Any(rule => !origins.Any(origin => origin.Id == rule.OriginId && MainOriginCodes.Contains(origin.Code))))
                return new(DietaryMutationStatus.Invalid);
            var desired = existing ?? new DietaryRequirement(id, request.Name, tenantId);
            desired.Revise(request.Name, request.Description, request.SortOrder,
                request.Rules.Select(value => new DietaryOriginRule(value.OriginId, value.Decision)));
            if (existing is null) database.DietaryRequirements.Add(desired);
            database.Set<DietaryRequirementRevisionRecord>().Add(new() { DietaryRequirementId = id, Version = desired.Version,
                SnapshotJson = JsonSerializer.Serialize(Document(desired)) });
            await database.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new(DietaryMutationStatus.Success, id);
        }
        catch (ArgumentException) { await transaction.RollbackAsync(CancellationToken.None); database.ChangeTracker.Clear(); return new(DietaryMutationStatus.Invalid); }
        catch (DbUpdateException) { await transaction.RollbackAsync(CancellationToken.None); database.ChangeTracker.Clear(); return new(DietaryMutationStatus.Conflict); }
    }

    public async Task<DietaryMutationResult> SubmitAsync(Guid id, Guid tenantId, int version, Guid actor, DateTimeOffset now, CancellationToken ct)
    {
        var diet = await database.DietaryRequirements.AsNoTracking().SingleOrDefaultAsync(value => value.Id == id && value.TenantId == tenantId, ct);
        if (diet is null) return new(DietaryMutationStatus.NotFound);
        if (diet.Version != version || !await database.Set<DietaryRequirementRevisionRecord>().AnyAsync(value => value.DietaryRequirementId == id && value.Version == version, ct))
            return new(DietaryMutationStatus.Conflict);
        var contribution = new DietaryRequirementContributionRecord { Id = Guid.NewGuid(), DietaryRequirementId = id, Version = version, SubmittedBy = actor, SubmittedAtUtc = now };
        database.Add(contribution);
        try { await database.SaveChangesAsync(ct); return new(DietaryMutationStatus.Success, contribution.Id); }
        catch (DbUpdateException) { database.ChangeTracker.Clear(); return new(DietaryMutationStatus.Conflict); }
    }

    public async Task<IReadOnlyList<DietaryContributionDocument>> ContributionsAsync(CancellationToken ct)
    {
        var rows = await (from contribution in database.Set<DietaryRequirementContributionRecord>().AsNoTracking()
            join revision in database.Set<DietaryRequirementRevisionRecord>().AsNoTracking()
                on new { contribution.DietaryRequirementId, contribution.Version } equals new { revision.DietaryRequirementId, revision.Version }
            where contribution.Status == 0
            select new { contribution, revision.SnapshotJson }).ToArrayAsync(ct);
        return rows.Select(value => new DietaryContributionDocument(value.contribution.Id,
            JsonSerializer.Deserialize<DietaryTypeDocument>(value.SnapshotJson)!, value.contribution.SubmittedBy,
            value.contribution.SubmittedAtUtc, value.contribution.Status, value.contribution.CentralDietaryRequirementId)).ToArray();
    }

    public async Task<DietaryMutationResult> ReviewAsync(Guid id, bool accept, Guid? targetCentralId, Guid actor, DateTimeOffset now, CancellationToken ct)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(ct);
        try
        {
            var contribution = await database.Set<DietaryRequirementContributionRecord>().SingleOrDefaultAsync(value => value.Id == id, ct);
            if (contribution is null) return new(DietaryMutationStatus.NotFound);
            if (contribution.Status != 0) return new(DietaryMutationStatus.Conflict);
            if (accept)
            {
                if (targetCentralId.HasValue)
                {
                    if (!await database.DietaryRequirements.AnyAsync(value => value.Id == targetCentralId && value.TenantId == null, ct))
                        return new(DietaryMutationStatus.Invalid);
                    contribution.CentralDietaryRequirementId = targetCentralId;
                }
                else
                {
                    string snapshot = await database.Set<DietaryRequirementRevisionRecord>()
                        .Where(value => value.DietaryRequirementId == contribution.DietaryRequirementId && value.Version == contribution.Version)
                        .Select(value => value.SnapshotJson).SingleAsync(ct);
                    var submitted = JsonSerializer.Deserialize<DietaryTypeDocument>(snapshot)!;
                    var central = new DietaryRequirement(Guid.NewGuid(), submitted.Name);
                    central.Revise(submitted.Name, submitted.Description, submitted.SortOrder,
                        submitted.Rules.Select(value => new DietaryOriginRule(value.OriginId, value.Decision)));
                    database.DietaryRequirements.Add(central);
                    database.Add(new DietaryRequirementRevisionRecord { DietaryRequirementId = central.Id, Version = central.Version, SnapshotJson = JsonSerializer.Serialize(Document(central)) });
                    contribution.CentralDietaryRequirementId = central.Id;
                }
            }
            contribution.Status = accept ? 1 : 2; contribution.ReviewedBy = actor; contribution.ReviewedAtUtc = now;
            await database.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
            return new(DietaryMutationStatus.Success, contribution.CentralDietaryRequirementId ?? id);
        }
        catch (DbUpdateException) { await transaction.RollbackAsync(CancellationToken.None); database.ChangeTracker.Clear(); return new(DietaryMutationStatus.Conflict); }
    }

    public static DietaryTypeDocument Document(DietaryRequirement value) => new(value.Id, value.TenantId, value.Name,
        value.Description, value.SortOrder, value.Version, value.OriginRules.OrderBy(rule => rule.OriginId)
            .Select(rule => new DietaryOriginRuleData(rule.OriginId, rule.Decision)).ToArray());
}
