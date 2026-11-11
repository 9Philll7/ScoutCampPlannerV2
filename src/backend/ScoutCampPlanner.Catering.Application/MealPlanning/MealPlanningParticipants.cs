using System.Text.Json;
using ScoutCampPlanner.Camp.Contracts;
using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Application.MealPlanning;

// Legacy transport/migration DTOs. The current write API rejects direct assignments.
public sealed record ParticipantMealOverride(Guid CampMealId, IReadOnlyList<Guid> ParticipantIds);
public sealed record CookingUnitParticipantAssignments(Guid CookingUnitId, IReadOnlyList<Guid> DefaultParticipantIds,
    IReadOnlyList<ParticipantMealOverride> MealOverrides);
public sealed record SaveParticipantPlanningRequest(int ExpectedVersion, MealPlanningDemandMode DemandMode,
    IReadOnlyList<CookingUnitParticipantAssignments>? Assignments = null);
public sealed record ParticipantPlanningConfigurationDocument(int Version, MealPlanningDemandMode DemandMode,
    bool RequiresStructureMigration);
public sealed record ParticipantPlanningProblem(string ReasonCode, IReadOnlyList<Guid> ParticipantReferences);
public sealed record OperationalParticipantDemand(EffectiveDemandBasis Basis, decimal? Demand, bool Complete,
    IReadOnlyList<RequirementGroup> RequirementGroups, IReadOnlyList<Guid> UnassignedParticipantIds,
    string RelevantSignature, IReadOnlyList<ParticipantPlanningProblem>? Problems = null);

public sealed partial class MealPlanningService
{
    public async Task<bool> UsesActualParticipantsAsync(Guid campId, CancellationToken ct = default) =>
        RequiresParticipantAccess(await store.LoadAsync(campId, ct));

    private static bool RequiresParticipantAccess(MealPlanningData data) =>
        data.ParticipantConfiguration?.DemandMode == MealPlanningDemandMode.UseActualParticipants ||
        data.MealStates.Any(RequiresParticipantAccess);

    public static bool RequiresParticipantAccess(CookingUnitMealState value) => value.DemandBasis == EffectiveDemandBasis.ActualParticipants ||
            value.VerificationSnapshotJson is not null && JsonSerializer.Deserialize<MealVerificationSnapshot>(value.VerificationSnapshotJson, JsonOptions)?.DemandBasis == EffectiveDemandBasis.ActualParticipants || ReadManualSupply(value).Any(solution =>
                solution.Allocations.Any(allocation => allocation.RequirementGroupKey != new RequirementSignature(null, [], []).Key));

    public async Task<ParticipantPlanningConfigurationDocument> GetParticipantConfigurationAsync(Guid campId, CancellationToken ct = default)
    {
        var data = await store.LoadAsync(campId, ct);
        return new(data.ParticipantConfiguration?.Version ?? 0,
            data.ParticipantConfiguration?.DemandMode ?? MealPlanningDemandMode.EstimatedPlanning,
            HasLegacyAssignments(data));
    }

    public async Task<MealPlanningMutationResult> SaveParticipantConfigurationAsync(Guid campId,
        SaveParticipantPlanningRequest request, CancellationToken ct = default)
    {
        if (await campPlanning.GetPlanningDataAsync(campId, ct) is null) return NotFound();
        if (!Enum.IsDefined(request.DemandMode) || request.ExpectedVersion < 0 || request.Assignments?.Count > 0)
            return Invalid("direct_participant_assignment_removed", "Teilnehmer werden im Camp einem Strukturknoten zugeordnet.");
        var data = await store.LoadAsync(campId, ct);
        if (HasLegacyAssignments(data))
            return Invalid("participant_structure_migration_required", "Alte direkte Zuordnungen müssen zuerst verlustfrei migriert werden.");
        return await store.SaveParticipantConfigurationAsync(campId, request.ExpectedVersion, request.DemandMode, "[]", ct);
    }

    public static bool HasLegacyAssignments(MealPlanningData data) => ReadAssignments(data)
        .Any(value => value.DefaultParticipantIds.Count > 0 || value.MealOverrides.Count > 0);

    public static void ValidateAssignments(IReadOnlyList<CookingUnitParticipantAssignments> assignments,
        IReadOnlySet<Guid> unitIds, IReadOnlySet<Guid> mealIds, IReadOnlySet<Guid> participantIds)
    {
        if (assignments.Any(value => value is null || !unitIds.Contains(value.CookingUnitId) ||
                value.DefaultParticipantIds is null || value.MealOverrides is null) ||
            assignments.Select(value => value.CookingUnitId).Distinct().Count() != assignments.Count)
            throw new ArgumentException("Die Kocheinheiten der Zuordnung sind ungültig.");
        foreach (var value in assignments)
        {
            if (value.MealOverrides.Any(item => item is null || !mealIds.Contains(item.CampMealId) || item.ParticipantIds is null) ||
                value.MealOverrides.Select(item => item.CampMealId).Distinct().Count() != value.MealOverrides.Count)
                throw new ArgumentException("Die Mahlzeitenabweichungen sind ungültig.");
            if (value.DefaultParticipantIds.Concat(value.MealOverrides.SelectMany(item => item.ParticipantIds)).Any(id => !participantIds.Contains(id)))
                throw new ArgumentException("Eine zugeordnete Person gehört nicht zu diesem Lager.");
        }
        EnsureDistinct(assignments.SelectMany(value => value.DefaultParticipantIds));
        foreach (Guid mealId in mealIds)
            EnsureDistinct(assignments.SelectMany(value => EffectiveIds(value, mealId)));
    }

    private static void EnsureDistinct(IEnumerable<Guid> values)
    {
        var ids = values.ToArray();
        if (ids.Length != ids.Distinct().Count()) throw new ArgumentException("Ein Teilnehmer darf pro Mahlzeit nur einer Kocheinheit zugeordnet sein.");
    }

    public static IReadOnlyList<CookingUnitParticipantAssignments> ReadAssignments(MealPlanningData data) =>
        data.ParticipantConfiguration is null ? [] :
            JsonSerializer.Deserialize<CookingUnitParticipantAssignments[]>(data.ParticipantConfiguration.AssignmentsJson, JsonOptions) ?? [];
    private static IReadOnlyList<Guid> EffectiveIds(CookingUnitParticipantAssignments value, Guid mealId) =>
        value.MealOverrides.SingleOrDefault(item => item.CampMealId == mealId)?.ParticipantIds ?? value.DefaultParticipantIds;

    private async Task<IReadOnlyList<CateringParticipantData>> ReadMealParticipantsAsync(Guid campId, CampMeal meal, CancellationToken ct) =>
        participants is not null ? await participants.GetMealParticipantsAsync(campId, meal.Date, meal.Id, ct) :
        throw new InvalidOperationException("The minimized Camp catering projection is required.");

    private static IReadOnlyList<CateringParticipantData> PeopleRequiringSupply(MealPlanningData data, CampPlanningData camp,
        Guid mealId, IReadOnlyList<CateringParticipantData> people, MealPlanningData? exclusionBasis = null)
    {
        var basis = exclusionBasis ?? data;
        bool IsExcluded(MealPlanningData source, Guid unitId) => source.MealStates.Any(state => state.CookingUnitId == unitId &&
            state.CampMealId == mealId && state.SubscriptionState == MealPlanSubscriptionState.NoSupplyRequired);
        var excludedUnits = data.CookingUnits.Where(unit => IsExcluded(data, unit.Id) && IsExcluded(basis, unit.Id)).ToArray();
        if (excludedUnits.Length == 0) return people;
        var activeUnits = data.CookingUnits.Where(unit => !IsExcluded(data, unit.Id)).ToArray();
        bool Matches(CookingUnit unit, CateringParticipantData person, MealPlanningData source) => person.StructureNodeId is Guid node &&
            IsCovered(node, EffectiveStructureNodes(unit.Id, mealId, source, includeInactive: true), camp.Nodes) &&
            StructureParticipantProjection.Matches(EffectiveParticipantFilter(unit, mealId, source), person.RequiresSpecialCatering);
        return people.Where(person => !person.IsPresent || !excludedUnits.Any(unit => Matches(unit, person, basis)) ||
            activeUnits.Any(unit => Matches(unit, person, data))).ToArray();
    }

    private static OperationalParticipantDemand ParticipantDemand(MealPlanningData data, CampPlanningData camp, CookingUnit unit, CampMeal meal,
        IReadOnlyList<CateringParticipantData> people, decimal? estimate)
    {
        if (data.ParticipantConfiguration?.DemandMode != MealPlanningDemandMode.UseActualParticipants)
            return new(EffectiveDemandBasis.Estimated, estimate, estimate.HasValue && !HasLegacyAssignments(data), [], [],
                HasLegacyAssignments(data) ? "estimated-migration-required" : "estimated",
                HasLegacyAssignments(data) ? [new ParticipantPlanningProblem("ParticipantStructureMigrationRequired", [])] : []);
        people = PeopleRequiringSupply(data, camp, meal.Id, people);
        var selections = data.CookingUnits.Select(value => new StructureParticipantSelection(value.Id,
            EffectiveStructureNodes(value.Id, meal.Id, data), EffectiveParticipantFilter(value, meal.Id, data))).ToArray();
        var projection = StructureParticipantProjection.Derive(camp.Nodes, people, selections);
        var selected = projection.ParticipantsByUnit.GetValueOrDefault(unit.Id) ?? [];
        var groups = selected.Select(value => new RequirementSignature(value.DietTypeId, value.AllergenIds,
                value.Intolerances.Select(item => new IntoleranceRequirement(item.SubstanceId, item.ThresholdGramsPerPortion))))
            .GroupBy(value => value.Key).OrderBy(value => value.Key)
            .Select(value => new RequirementGroup(value.First(), value.Count())).ToArray();
        // Include absent structural members when deciding whether real context data exist.
        var allPresent = StructureParticipantProjection.Derive(camp.Nodes,
            people.Select(value => value with { IsPresent = true }).ToArray(),
            [new(unit.Id, EffectiveStructureNodes(unit.Id, meal.Id, data), CookingUnitParticipantFilter.All)]);
        var localMembers = StructureParticipantProjection.Derive(camp.Nodes, people,
            [new(unit.Id, EffectiveStructureNodes(unit.Id, meal.Id, data), EffectiveParticipantFilter(unit, meal.Id, data))])
            .ParticipantsByUnit.GetValueOrDefault(unit.Id) ?? [];
        var localIds = localMembers.Select(value => value.ParticipantReference).ToHashSet();
        var overlapping = projection.Overlapping.Where(localIds.Contains).ToArray();
        int contextCount = allPresent.ParticipantsByUnit.GetValueOrDefault(unit.Id)?.Count ?? 0;
        var basis = MealDemandBasisPolicy.Resolve(MealPlanningDemandMode.UseActualParticipants, contextCount,
            selected.Count, overlapping.Length, estimate);
        bool migration = HasLegacyAssignments(data);
        return new(basis.Basis, basis.Demand, basis.Complete && !migration, groups, [],
            JsonSerializer.Serialize(new { basis.Basis, migration, participantFilter = EffectiveParticipantFilter(unit, meal.Id, data),
                selected = selected.OrderBy(value => value.ParticipantReference).Select(value => new {
                    value.ParticipantReference, Signature = new RequirementSignature(value.DietTypeId, value.AllergenIds,
                        value.Intolerances.Select(item => new IntoleranceRequirement(item.SubstanceId, item.ThresholdGramsPerPortion))).Key }),
                Unassigned = Array.Empty<Guid>(), Overlapping = overlapping }, JsonOptions),
            (overlapping.Length > 0 ? new[] { new ParticipantPlanningProblem("OverlappingCookingUnits", overlapping) } : [])
                .Concat(migration ? [new ParticipantPlanningProblem("ParticipantStructureMigrationRequired", [])] : []).ToArray());
    }
}
