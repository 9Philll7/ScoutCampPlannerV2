using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Infrastructure.Offline;

public sealed record MealPlanningPackageData(
    int SchemaVersion,
    Guid CampId,
    IReadOnlyList<MealPlanPackageRecord> MealPlans,
    IReadOnlyList<MealPlanSnapshotPackageRecord> Snapshots,
    IReadOnlyList<MealPlanOfferGroupPackageRecord> OfferGroups,
    IReadOnlyList<MealPlanEntryPackageRecord> Entries,
    IReadOnlyList<CookingUnitGroupPackageRecord> CookingUnitGroups,
    IReadOnlyList<CookingUnitPackageRecord> CookingUnits,
    IReadOnlyList<StructureAssignmentPackageRecord> StructureAssignments,
    IReadOnlyList<CookingUnitMealStatePackageRecord> MealStates,
    IReadOnlyList<OfferTargetPackageRecord> OfferTargets,
    IReadOnlyList<RecipeChoicePackageRecord> RecipeChoices);

public sealed record MealPlanPackageRecord(Guid Id, Guid CampId, string Name, int SortOrder, int Version);
public sealed record MealPlanSnapshotPackageRecord(
    Guid Id, Guid MealPlanId, Guid CampId, int Version, string ContentJson, DateTimeOffset SavedAtUtc);
public sealed record MealPlanOfferGroupPackageRecord(
    Guid Id, Guid MealPlanId, Guid CampMealId, string? Name, int SortOrder);
public sealed record MealPlanEntryPackageRecord(
    Guid Id, Guid OfferGroupId, Guid RecipeRevisionId, bool IsStandard, string? DisplayName,
    int? Role, string? Note, int SortOrder);
public sealed record CookingUnitGroupPackageRecord(Guid Id, Guid CampId, string Name, int SortOrder);
public sealed record CookingUnitPackageRecord(
    Guid Id, Guid CampId, string Name, int SortOrder, Guid? GroupId, Guid? StandardMealPlanId);
public sealed record StructureAssignmentPackageRecord(
    Guid Id, Guid CampId, Guid CookingUnitId, Guid? CampMealId, Guid StructureNodeId);
public sealed record CookingUnitMealStatePackageRecord(
    Guid Id, Guid CampId, Guid CookingUnitId, Guid CampMealId, int SubscriptionState,
    decimal? DemandOverride, decimal? CalculatedDemand, decimal? EffectiveDemand, int Status,
    Guid? MealPlanId, int? MealPlanVersion, Guid? MealPlanSnapshotId,
    string? CalculationSnapshotJson, string? SourceFingerprint, string? WarningsJson,
    DateTimeOffset? CalculatedAtUtc);
public sealed record OfferTargetPackageRecord(
    Guid Id, Guid CookingUnitMealStateId, Guid OfferGroupId, decimal? TargetOverride);
public sealed record RecipeChoicePackageRecord(
    Guid Id, Guid CookingUnitMealStateId, Guid RecipeRevisionId, Guid? OfferGroupId,
    Guid? MealPlanEntryId, int SortOrder);

public sealed class CampMealPlanningPackageStore(CateringDbContext database)
{
    public const int CurrentSchemaVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static JsonElement CreateEmptyPackageData(Guid campId) => JsonSerializer.SerializeToElement(
        new MealPlanningPackageData(CurrentSchemaVersion, campId, [], [], [], [], [], [], [], [], [], []),
        JsonOptions);

    public static MealPlanningPackageData ReadPackageData(JsonElement json, Guid campId) =>
        DeserializeAndValidate(json, campId);

    public static JsonElement CreatePackageData(MealPlanningPackageData data)
    {
        JsonElement json = JsonSerializer.SerializeToElement(data, JsonOptions);
        DeserializeAndValidate(json, data.CampId);
        return json;
    }

    public async Task<JsonElement> ExportAsync(Guid campId, CancellationToken cancellationToken = default)
    {
        MealPlanPackageRecord[] plans = await database.MealPlans.AsNoTracking()
            .Where(value => value.CampId == campId)
            .Select(value => new MealPlanPackageRecord(
                value.Id, value.CampId, value.Name, value.SortOrder, value.Version))
            .ToArrayAsync(cancellationToken);
        Guid[] planIds = plans.Select(value => value.Id).ToArray();
        MealPlanSnapshotPackageRecord[] snapshots = await database.MealPlanSnapshots.AsNoTracking()
            .Where(value => value.CampId == campId)
            .Select(value => new MealPlanSnapshotPackageRecord(
                value.Id, value.MealPlanId, value.CampId, value.Version, value.ContentJson, value.SavedAtUtc))
            .ToArrayAsync(cancellationToken);
        MealPlanOfferGroupPackageRecord[] offerGroups = await database.MealPlanOfferGroups.AsNoTracking()
            .Where(value => planIds.Contains(value.MealPlanId))
            .Select(value => new MealPlanOfferGroupPackageRecord(
                value.Id, value.MealPlanId, value.CampMealId, value.Name, value.SortOrder))
            .ToArrayAsync(cancellationToken);
        Guid[] offerGroupIds = offerGroups.Select(value => value.Id).ToArray();
        MealPlanEntryPackageRecord[] entries = await database.MealPlanEntries.AsNoTracking()
            .Where(value => offerGroupIds.Contains(value.OfferGroupId))
            .Select(value => new MealPlanEntryPackageRecord(
                value.Id, value.OfferGroupId, value.RecipeRevisionId, value.IsStandard,
                value.DisplayName, value.Role.HasValue ? (int?)value.Role.Value : null, value.Note, value.SortOrder))
            .ToArrayAsync(cancellationToken);
        CookingUnitGroupPackageRecord[] unitGroups = await database.CookingUnitGroups.AsNoTracking()
            .Where(value => value.CampId == campId)
            .Select(value => new CookingUnitGroupPackageRecord(value.Id, value.CampId, value.Name, value.SortOrder))
            .ToArrayAsync(cancellationToken);
        CookingUnitPackageRecord[] units = await database.CookingUnits.AsNoTracking()
            .Where(value => value.CampId == campId)
            .Select(value => new CookingUnitPackageRecord(
                value.Id, value.CampId, value.Name, value.SortOrder, value.GroupId, value.StandardMealPlanId))
            .ToArrayAsync(cancellationToken);
        StructureAssignmentPackageRecord[] assignments = await database.CookingUnitStructureAssignments.AsNoTracking()
            .Where(value => value.CampId == campId)
            .Select(value => new StructureAssignmentPackageRecord(
                value.Id, value.CampId, value.CookingUnitId, value.CampMealId, value.StructureNodeId))
            .ToArrayAsync(cancellationToken);
        CookingUnitMealStatePackageRecord[] states = await database.CookingUnitMealStates.AsNoTracking()
            .Where(value => value.CampId == campId)
            .Select(value => new CookingUnitMealStatePackageRecord(
                value.Id, value.CampId, value.CookingUnitId, value.CampMealId, (int)value.SubscriptionState,
                value.DemandOverride, value.CalculatedDemand, value.EffectiveDemand, (int)value.Status,
                value.MealPlanId, value.MealPlanVersion, value.MealPlanSnapshotId,
                value.CalculationSnapshotJson, value.SourceFingerprint, value.WarningsJson, value.CalculatedAtUtc))
            .ToArrayAsync(cancellationToken);
        Guid[] stateIds = states.Select(value => value.Id).ToArray();
        OfferTargetPackageRecord[] targets = await database.CookingUnitMealOfferTargets.AsNoTracking()
            .Where(value => stateIds.Contains(value.CookingUnitMealStateId))
            .Select(value => new OfferTargetPackageRecord(
                value.Id, value.CookingUnitMealStateId, value.OfferGroupId, value.TargetOverride))
            .ToArrayAsync(cancellationToken);
        RecipeChoicePackageRecord[] choices = await database.CookingUnitMealRecipeChoices.AsNoTracking()
            .Where(value => stateIds.Contains(value.CookingUnitMealStateId))
            .Select(value => new RecipeChoicePackageRecord(
                value.Id, value.CookingUnitMealStateId, value.RecipeRevisionId, value.OfferGroupId,
                value.MealPlanEntryId, value.SortOrder))
            .ToArrayAsync(cancellationToken);
        var payload = new MealPlanningPackageData(CurrentSchemaVersion, campId, plans, snapshots, offerGroups,
            entries, unitGroups, units, assignments, states, targets, choices);
        return JsonSerializer.SerializeToElement(payload, JsonOptions);
    }

    public async Task ImportAsync(JsonElement json, Guid campId, CancellationToken cancellationToken = default)
    {
        MealPlanningPackageData data = DeserializeAndValidate(json, campId);
        database.MealPlans.AddRange(data.MealPlans.Select(value =>
            new MealPlan(value.Id, value.CampId, value.Name, value.SortOrder, value.Version)));
        database.MealPlanSnapshots.AddRange(data.Snapshots.Select(value =>
            new MealPlanSnapshot(value.Id, value.MealPlanId, value.CampId, value.Version,
                value.ContentJson, value.SavedAtUtc)));
        database.MealPlanOfferGroups.AddRange(data.OfferGroups.Select(value =>
            new MealPlanOfferGroup(value.Id, value.MealPlanId, value.CampMealId, value.Name, value.SortOrder)));
        database.MealPlanEntries.AddRange(data.Entries.Select(value =>
            new MealPlanEntry(value.Id, value.OfferGroupId, value.RecipeRevisionId, value.IsStandard,
                value.DisplayName, value.Role.HasValue ? (MealPlanEntryRole?)value.Role.Value : null,
                value.Note, value.SortOrder)));
        database.CookingUnitGroups.AddRange(data.CookingUnitGroups.Select(value =>
            new CookingUnitGroup(value.Id, value.CampId, value.Name, value.SortOrder)));
        database.CookingUnits.AddRange(data.CookingUnits.Select(value =>
            new CookingUnit(value.Id, value.CampId, value.Name, value.SortOrder,
                value.GroupId, value.StandardMealPlanId)));
        database.CookingUnitStructureAssignments.AddRange(data.StructureAssignments.Select(value =>
            new CookingUnitStructureAssignment(value.Id, value.CampId, value.CookingUnitId,
                value.CampMealId, value.StructureNodeId)));
        foreach (CookingUnitMealStatePackageRecord value in data.MealStates)
        {
            var state = new CookingUnitMealState(value.Id, value.CampId, value.CookingUnitId, value.CampMealId);
            state.Configure((MealPlanSubscriptionState)value.SubscriptionState, value.DemandOverride);
            if (value.CalculatedAtUtc.HasValue && value.SourceFingerprint is not null && value.CalculationSnapshotJson is not null)
                state.ApplyCalculation(value.CalculatedDemand, value.MealPlanId, value.MealPlanVersion,
                    value.MealPlanSnapshotId, value.CalculationSnapshotJson, value.SourceFingerprint,
                    value.WarningsJson ?? "[]", value.CalculatedAtUtc.Value,
                    value.Status != (int)OperationalMealPlanStatus.Incomplete);
            if (value.Status == (int)OperationalMealPlanStatus.Stale) state.MarkStale();
            database.CookingUnitMealStates.Add(state);
        }
        database.CookingUnitMealOfferTargets.AddRange(data.OfferTargets.Select(value =>
            new CookingUnitMealOfferTarget(value.Id, value.CookingUnitMealStateId,
                value.OfferGroupId, value.TargetOverride)));
        database.CookingUnitMealRecipeChoices.AddRange(data.RecipeChoices.Select(value =>
            new CookingUnitMealRecipeChoice(value.Id, value.CookingUnitMealStateId,
                value.RecipeRevisionId, value.OfferGroupId, value.MealPlanEntryId, value.SortOrder)));
        await database.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteCampDataAsync(Guid campId, CancellationToken cancellationToken = default)
    {
        Guid[] planIds = await database.MealPlans.Where(value => value.CampId == campId)
            .Select(value => value.Id).ToArrayAsync(cancellationToken);
        Guid[] offerGroupIds = await database.MealPlanOfferGroups.Where(value => planIds.Contains(value.MealPlanId))
            .Select(value => value.Id).ToArrayAsync(cancellationToken);
        Guid[] stateIds = await database.CookingUnitMealStates.Where(value => value.CampId == campId)
            .Select(value => value.Id).ToArrayAsync(cancellationToken);
        await database.CookingUnitMealRecipeChoices.Where(value => stateIds.Contains(value.CookingUnitMealStateId))
            .ExecuteDeleteAsync(cancellationToken);
        await database.CookingUnitMealOfferTargets.Where(value => stateIds.Contains(value.CookingUnitMealStateId))
            .ExecuteDeleteAsync(cancellationToken);
        await database.CookingUnitMealStates.Where(value => value.CampId == campId).ExecuteDeleteAsync(cancellationToken);
        await database.CookingUnitStructureAssignments.Where(value => value.CampId == campId).ExecuteDeleteAsync(cancellationToken);
        await database.CookingUnits.Where(value => value.CampId == campId).ExecuteDeleteAsync(cancellationToken);
        await database.CookingUnitGroups.Where(value => value.CampId == campId).ExecuteDeleteAsync(cancellationToken);
        await database.MealPlanEntries.Where(value => offerGroupIds.Contains(value.OfferGroupId)).ExecuteDeleteAsync(cancellationToken);
        await database.MealPlanOfferGroups.Where(value => planIds.Contains(value.MealPlanId)).ExecuteDeleteAsync(cancellationToken);
        await database.MealPlanSnapshots.Where(value => value.CampId == campId).ExecuteDeleteAsync(cancellationToken);
        await database.MealPlans.Where(value => value.CampId == campId).ExecuteDeleteAsync(cancellationToken);
    }

    public static void Validate(JsonElement json, Guid campId) => DeserializeAndValidate(json, campId);

    private static MealPlanningPackageData DeserializeAndValidate(JsonElement json, Guid campId)
    {
        if (json.ValueKind != JsonValueKind.Object)
            throw new InvalidOperationException("Catering meal-planning package data is missing.");
        MealPlanningPackageData data = json.Deserialize<MealPlanningPackageData>(JsonOptions)
            ?? throw new InvalidOperationException("Catering meal-planning package data is empty.");
        if (data.SchemaVersion != CurrentSchemaVersion || data.CampId != campId)
            throw new InvalidOperationException("Catering meal-planning package identity or schema is invalid.");
        if (data.MealPlans.Any(value => value.CampId != campId) ||
            data.Snapshots.Any(value => value.CampId != campId) ||
            data.CookingUnitGroups.Any(value => value.CampId != campId) ||
            data.CookingUnits.Any(value => value.CampId != campId) ||
            data.StructureAssignments.Any(value => value.CampId != campId) ||
            data.MealStates.Any(value => value.CampId != campId))
            throw new InvalidOperationException("Catering meal-planning package contains another camp.");
        EnsureUnique(data.MealPlans.Select(value => value.Id), "meal plan");
        EnsureUnique(data.Snapshots.Select(value => value.Id), "meal-plan snapshot");
        EnsureUnique(data.OfferGroups.Select(value => value.Id), "offer group");
        EnsureUnique(data.Entries.Select(value => value.Id), "meal-plan entry");
        EnsureUnique(data.CookingUnits.Select(value => value.Id), "cooking unit");
        EnsureUnique(data.MealStates.Select(value => value.Id), "operational meal state");
        Guid[] planIds = data.MealPlans.Select(value => value.Id).ToArray();
        Guid[] groupIds = data.OfferGroups.Select(value => value.Id).ToArray();
        Guid[] entryIds = data.Entries.Select(value => value.Id).ToArray();
        Guid[] unitGroupIds = data.CookingUnitGroups.Select(value => value.Id).ToArray();
        Guid[] unitIds = data.CookingUnits.Select(value => value.Id).ToArray();
        Guid[] stateIds = data.MealStates.Select(value => value.Id).ToArray();
        if (data.Snapshots.Any(value => !planIds.Contains(value.MealPlanId)) ||
            data.OfferGroups.Any(value => !planIds.Contains(value.MealPlanId)) ||
            data.Entries.Any(value => !groupIds.Contains(value.OfferGroupId)) ||
            data.CookingUnits.Any(value => value.GroupId.HasValue && !unitGroupIds.Contains(value.GroupId.Value) ||
                value.StandardMealPlanId.HasValue && !planIds.Contains(value.StandardMealPlanId.Value)) ||
            data.StructureAssignments.Any(value => !unitIds.Contains(value.CookingUnitId)) ||
            data.MealStates.Any(value => !unitIds.Contains(value.CookingUnitId) ||
                value.MealPlanId.HasValue && !planIds.Contains(value.MealPlanId.Value)) ||
            data.OfferTargets.Any(value => !stateIds.Contains(value.CookingUnitMealStateId) ||
                !groupIds.Contains(value.OfferGroupId)) ||
            data.RecipeChoices.Any(value => !stateIds.Contains(value.CookingUnitMealStateId) ||
                value.OfferGroupId.HasValue && !groupIds.Contains(value.OfferGroupId.Value) ||
                value.MealPlanEntryId.HasValue && !entryIds.Contains(value.MealPlanEntryId.Value)))
            throw new InvalidOperationException("Catering meal-planning package references are incomplete.");
        return data;
    }

    private static void EnsureUnique(IEnumerable<Guid> values, string label)
    {
        Guid[] ids = values.ToArray();
        if (ids.Any(value => value == Guid.Empty) || ids.Distinct().Count() != ids.Length)
            throw new InvalidOperationException($"Catering {label} identities are invalid.");
    }
}
