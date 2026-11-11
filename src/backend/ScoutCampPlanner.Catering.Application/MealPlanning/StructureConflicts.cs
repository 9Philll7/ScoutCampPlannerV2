using System.Text.Json;
using ScoutCampPlanner.Camp.Contracts;
using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Application.MealPlanning;

public sealed record StructureConflict(string Id, Guid NodeId, string NodeName, string Kind,
    IReadOnlyList<Guid> CookingUnitIds, IReadOnlyList<Guid> MealIds, int? Persons);
public sealed record StructureConflictDocument(string Version, IReadOnlyList<StructureConflict> Conflicts, bool ActualParticipants)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlyDictionary<string, string> IntegrityCases { get; init; } = new Dictionary<string, string>();
}
public sealed record ResolveStructureConflictRequest(string ExpectedVersion, string ConflictId, Guid KeepCookingUnitId,
    bool UseDefaults, IReadOnlyList<Guid> MealIds, IReadOnlyList<Guid>? CookingUnitIds = null);
public sealed record StructureAssignmentChange(Guid CookingUnitId, Guid? MealId, IReadOnlyList<Guid> NodeIds, bool NoSupply = false, bool ResumeSupply = false);
public sealed record StructureResolutionPreview(string Version, int Resolved, int Remaining, int NewConflicts,
    IReadOnlyList<StructureAssignmentChange> Changes, IReadOnlyList<StructureConflict> Conflicts, int OverlappingMeals = 0, bool OverlappingFilters = false);

public sealed partial class MealPlanningService
{
    public static string StructureDataVersion(MealPlanningData data) => Fingerprint(JsonSerializer.Serialize(new {
        data.ScheduleVersion,
        units = data.CookingUnits.OrderBy(value => value.Id).Select(value => new { value.Id, value.Name, value.GroupId, value.ParticipantFilter }),
        assignments = data.StructureAssignments.OrderBy(value => value.Id),
        states = data.MealStates.OrderBy(value => value.Id).Select(value => new { value.Id, value.SubscriptionState, value.SupplyVersion }),
        meals = data.Meals.OrderBy(value => value.Id), data.ParticipantConfiguration
    }, JsonOptions));

    private async Task<Dictionary<Guid, IReadOnlyList<CateringParticipantData>>> StructurePeopleAsync(Guid campId, MealPlanningData data, CampPlanningData camp, CancellationToken ct)
    {
        var result = new Dictionary<Guid, IReadOnlyList<CateringParticipantData>>();
        if (data.ParticipantConfiguration?.DemandMode == MealPlanningDemandMode.UseActualParticipants && participants is not null)
            foreach (var meal in data.Meals.Where(value => value.IsActive && IsInsideCamp(value.Date, camp)))
                result[meal.Id] = await ReadMealParticipantsAsync(campId, meal, ct);
        return result;
    }

    private static StructureConflictDocument StructureConflicts(MealPlanningData data, CampPlanningData camp,
        Dictionary<Guid, IReadOnlyList<CateringParticipantData>> people, MealPlanningData? exclusionBasis = null)
    {
        people = people.Where(value => data.Meals.Any(meal => meal.Id == value.Key && meal.IsActive && IsInsideCamp(meal.Date, camp)))
            .ToDictionary(value => value.Key, value => value.Value);
        var integrityCases = new Dictionary<string, string>();
        var occurrences = new List<(Guid Node, string Kind, Guid[] Units, Guid Meal, int? Persons)>();
        foreach (var meal in data.Meals.Where(value => value.IsActive && IsInsideCamp(value.Date, camp)))
        {
            if (people.TryGetValue(meal.Id, out var present) && present.Count > 0)
            {
                present = PeopleRequiringSupply(data, camp, meal.Id, present, exclusionBasis);
                var memberships = data.CookingUnits.ToDictionary(unit => unit.Id, unit =>
                    StructureParticipantProjection.Derive(camp.Nodes, present, [new(unit.Id,
                        EffectiveStructureNodes(unit.Id, meal.Id, data), EffectiveParticipantFilter(unit, meal.Id, data))])
                        .ParticipantsByUnit[unit.Id].Select(value => value.ParticipantReference).ToHashSet());
                foreach (var group in present.Where(value => value.IsPresent && value.StructureNodeId.HasValue)
                    .Select(person => new { Person = person, Units = memberships.Where(value => value.Value.Contains(person.ParticipantReference)).Select(value => value.Key).Order().ToArray() })
                    .Where(value => value.Units.Length != 1).GroupBy(value => new { Node = value.Person.StructureNodeId!.Value, Units = string.Join(',', value.Units) }))
                {
                    var units = group.First().Units;
                    string kind = units.Length == 0 ? "missing" : "overlap";
                    string caseKey = $"{group.Key.Node}:{kind}:{meal.Id}";
                    foreach (var person in group) integrityCases[$"{caseKey}:{person.Person.ParticipantReference}"] = caseKey;
                    occurrences.Add((group.Key.Node, kind, units, meal.Id, group.Count()));
                }
                var unknown = present.Count(value => value.IsPresent && (!value.StructureNodeId.HasValue || !camp.Nodes.Any(node => node.Id == value.StructureNodeId)));
                if (unknown > 0)
                {
                    string caseKey = $"{Guid.Empty}:missing:{meal.Id}";
                    foreach (var person in present.Where(value => value.IsPresent && (!value.StructureNodeId.HasValue || !camp.Nodes.Any(node => node.Id == value.StructureNodeId))))
                        integrityCases[$"{caseKey}:{person.ParticipantReference}"] = caseKey;
                    occurrences.Add((Guid.Empty, "missing", [], meal.Id, unknown));
                }
            }
            else
                foreach (var node in camp.Estimates.Where(value => value.ChildYouthCount + value.LeaderCount > 0).Select(value => value.StructureNodeId).Distinct())
                {
                    var covering = data.CookingUnits.Where(unit => IsCovered(node, EffectiveStructureNodes(unit.Id, meal.Id, data), camp.Nodes)).ToArray();
                    var excluded = data.CookingUnits.Where(unit => data.MealStates.Any(state => state.CookingUnitId == unit.Id &&
                        state.CampMealId == meal.Id && state.SubscriptionState == MealPlanSubscriptionState.NoSupplyRequired) &&
                        (exclusionBasis ?? data).MealStates.Any(state => state.CookingUnitId == unit.Id && state.CampMealId == meal.Id && state.SubscriptionState == MealPlanSubscriptionState.NoSupplyRequired) &&
                        IsCovered(node, EffectiveStructureNodes(unit.Id, meal.Id, data, includeInactive: true), camp.Nodes)).ToArray();
                    foreach (bool special in new[] { false, true })
                    {
                        var units = covering.Where(unit => StructureParticipantProjection.Matches(EffectiveParticipantFilter(unit, meal.Id, data), special)).Select(value => value.Id).Order().ToArray();
                        bool intentionallyUnserved = units.Length == 0 && excluded.Any(unit => StructureParticipantProjection.Matches(EffectiveParticipantFilter(unit, meal.Id, data), special));
                        if (units.Length != 1 && !intentionallyUnserved)
                        {
                            string kind = units.Length == 0 ? "missing" : "overlap";
                            string caseKey = $"{node}:{kind}:{meal.Id}";
                            integrityCases[$"{caseKey}:{special}"] = caseKey;
                            occurrences.Add((node, kind, units, meal.Id, null));
                        }
                    }
                }
        }
        var conflicts = occurrences.GroupBy(value => new { value.Node, value.Kind, Units = string.Join(',', value.Units) })
            .Select(group => new StructureConflict(Fingerprint(JsonSerializer.Serialize(group.Key)), group.Key.Node,
                camp.Nodes.SingleOrDefault(value => value.Id == group.Key.Node)?.Name ?? "Personen ohne Strukturzuordnung", group.Key.Kind, group.First().Units,
                group.Select(value => value.Meal).Distinct().Order().ToArray(), group.Any(value => value.Persons is null) ? null : group.Max(value => value.Persons)))
            .OrderBy(value => value.NodeName).ThenBy(value => value.Id).ToArray();
        var version = Fingerprint(JsonSerializer.Serialize(new { data = StructureDataVersion(data), camp.Nodes, camp.Estimates,
            people = people.OrderBy(value => value.Key).Select(value => new { value.Key, Persons = value.Value.OrderBy(person => person.ParticipantReference)
                .Select(person => new { person.ParticipantReference, person.StructureNodeId, person.IsPresent, person.RequiresSpecialCatering }) }) }, JsonOptions));
        return new(version, conflicts, people.Values.Any(value => value.Count > 0)) { IntegrityCases = integrityCases };
    }

    public async Task<StructureConflictDocument?> GetStructureConflictsAsync(Guid campId, CancellationToken ct = default)
    {
        var camp = await campPlanning.GetPlanningDataAsync(campId, ct);
        if (camp is null) return null;
        var data = await store.LoadAsync(campId, ct);
        return StructureConflicts(data, camp, await StructurePeopleAsync(campId, data, camp, ct));
    }

    private static Guid[] RemoveStructureBranch(IReadOnlyList<Guid> roots, Guid excluded, IReadOnlyList<CampPlanningNode> nodes)
    {
        var result = new List<Guid>();
        void Visit(Guid root)
        {
            if (IsCovered(root, [excluded], nodes)) return;
            if (IsCovered(excluded, [root], nodes))
                foreach (var child in nodes.Where(value => value.ParentId == root)) Visit(child.Id);
            else result.Add(root);
        }
        foreach (var root in roots) Visit(root);
        return result.Distinct().ToArray();
    }

    private static Guid[] AddStructureBranch(Guid[] roots, Guid nodeId, IReadOnlyList<CampPlanningNode> nodes)
    {
        if (IsCovered(nodeId, roots, nodes)) return roots;
        return roots.Where(root => !IsCovered(root, [nodeId], nodes)).Append(nodeId).ToArray();
    }

    private static MealPlanningData WithStructureChanges(MealPlanningData data, Guid campId, IReadOnlyList<StructureAssignmentChange> changes)
    {
        var assignments = data.StructureAssignments.Where(value => !changes.Any(change => change.CookingUnitId == value.CookingUnitId && change.MealId == value.CampMealId))
            .Concat(changes.SelectMany(change => change.NodeIds.Select(node => new CookingUnitStructureAssignment(Guid.NewGuid(), campId, change.CookingUnitId, change.MealId, node)))).ToArray();
        // Clone states before simulating so preview cannot mutate a tracked or test-store instance.
        var replaced = data.MealStates.Where(value => !changes.Any(change => change.MealId == value.CampMealId && change.CookingUnitId == value.CookingUnitId && (change.NoSupply || change.ResumeSupply))).ToList();
        foreach (var change in changes.Where(value => (value.NoSupply || value.ResumeSupply) && value.MealId.HasValue))
        {
            var previous = data.MealStates.SingleOrDefault(value => value.CookingUnitId == change.CookingUnitId && value.CampMealId == change.MealId);
            var state = new CookingUnitMealState(previous?.Id ?? Guid.NewGuid(), campId, change.CookingUnitId, change.MealId!.Value);
            if (previous is not null)
                state.RestoreSupply(previous.ManualSupplySolutionsJson, previous.SupplyVersion, previous.VerificationSnapshotJson, previous.VerificationFingerprint);
            state.Configure(change.NoSupply ? MealPlanSubscriptionState.NoSupplyRequired : MealPlanSubscriptionState.FollowStandard, previous?.DemandOverride);
            replaced.Add(state);
        }
        return data with { StructureAssignments = assignments, MealStates = replaced };
    }

    private async Task<(StructureResolutionPreview Preview, MealPlanningData Data)?> BuildStructureResolutionAsync(Guid campId, ResolveStructureConflictRequest request, CancellationToken ct)
    {
        var camp = await campPlanning.GetPlanningDataAsync(campId, ct);
        if (camp is null) return null;
        var data = await store.LoadAsync(campId, ct);
        var people = await StructurePeopleAsync(campId, data, camp, ct);
        var before = StructureConflicts(data, camp, people);
        if (request.ExpectedVersion != before.Version) return null;
        var conflict = before.Conflicts.SingleOrDefault(value => value.Id == request.ConflictId);
        var selectedUnitIds = request.CookingUnitIds ?? new[] { request.KeepCookingUnitId };
        if (conflict is null || !camp.Nodes.Any(node => node.Id == conflict.NodeId) || request.MealIds is null ||
            selectedUnitIds.Count == 0 || selectedUnitIds.Distinct().Count() != selectedUnitIds.Count ||
            selectedUnitIds.Any(id => !data.CookingUnits.Any(unit => unit.Id == id)) ||
            !request.UseDefaults && (request.MealIds.Count == 0 || request.MealIds.Any(id => !conflict.MealIds.Contains(id)))) return null;
        var changes = new List<StructureAssignmentChange>();
        var unitIds = conflict.CookingUnitIds.Union(selectedUnitIds).ToArray();
        foreach (var unitId in unitIds)
        {
            var meals = request.UseDefaults ? new Guid?[] { null } : request.MealIds.Distinct().Select(id => (Guid?)id).ToArray();
            foreach (var meal in meals)
            {
                var roots = meal.HasValue ? EffectiveStructureNodes(unitId, meal.Value, data, includeInactive: true) :
                    data.StructureAssignments.Where(value => value.CookingUnitId == unitId && value.CampMealId is null).Select(value => value.StructureNodeId).ToArray();
                bool selected = selectedUnitIds.Contains(unitId);
                var nodes = selected ? AddStructureBranch(roots, conflict.NodeId, camp.Nodes) : RemoveStructureBranch(roots, conflict.NodeId, camp.Nodes);
                bool resume = selected && meal.HasValue && data.MealStates.Any(state => state.CookingUnitId == unitId &&
                    state.CampMealId == meal && state.SubscriptionState == MealPlanSubscriptionState.NoSupplyRequired);
                if (resume || !roots.Order().SequenceEqual(nodes.Order()))
                    changes.Add(new(unitId, meal, nodes, !selected && meal.HasValue && nodes.Length == 0, resume));
            }
        }
        var proposed = WithStructureChanges(data, campId, changes);
        var after = StructureConflicts(proposed, camp, people, exclusionBasis: data);
        var targetCases = conflict.MealIds.Select(meal => $"{conflict.NodeId}:{conflict.Kind}:{meal}").ToHashSet();
        var afterCases = after.IntegrityCases.Values.ToHashSet();
        int newConflicts = after.IntegrityCases.Where(value => !before.IntegrityCases.ContainsKey(value.Key)).Select(value => value.Value).Distinct().Count();
        var changedMealIds = data.Meals.Where(meal => meal.IsActive && IsInsideCamp(meal.Date, camp) && changes.Any(change =>
            change.MealId == meal.Id || change.MealId is null && !data.StructureAssignments.Any(assignment =>
                assignment.CookingUnitId == change.CookingUnitId && assignment.CampMealId == meal.Id)))
            .Select(meal => meal.Id).ToHashSet();
        int overlappingMeals = after.Conflicts.Where(value => value.NodeId == conflict.NodeId && value.Kind == "overlap")
            .SelectMany(value => value.MealIds).Where(changedMealIds.Contains).Distinct().Count();
        // Check both possible catering categories, including categories without current participants.
        bool overlappingFilters = request.UseDefaults && new[] { false, true }.Any(special =>
            data.CookingUnits.Count(unit => selectedUnitIds.Contains(unit.Id) &&
                StructureParticipantProjection.Matches(unit.ParticipantFilter, special)) > 1);
        overlappingFilters |= data.Meals.Where(meal => changedMealIds.Contains(meal.Id)).Any(meal =>
            new[] { false, true }.Any(special => data.CookingUnits.Count(unit =>
                IsCovered(conflict.NodeId, EffectiveStructureNodes(unit.Id, meal.Id, proposed), camp.Nodes) &&
                StructureParticipantProjection.Matches(EffectiveParticipantFilter(unit, meal.Id, proposed), special)) > 1));
        return (new(before.Version, targetCases.Except(afterCases).Count(), targetCases.Intersect(afterCases).Count(), newConflicts, changes, after.Conflicts, overlappingMeals, overlappingFilters), data);
    }

    public async Task<StructureResolutionPreview?> PreviewStructureResolutionAsync(Guid campId, ResolveStructureConflictRequest request, CancellationToken ct = default) =>
        (await BuildStructureResolutionAsync(campId, request, ct))?.Preview;

    public async Task<MealPlanningMutationResult> ApplyStructureResolutionAsync(Guid campId, ResolveStructureConflictRequest request, CancellationToken ct = default)
    {
        var result = await BuildStructureResolutionAsync(campId, request, ct);
        if (result is null) return new(MealPlanningMutationStatus.Conflict, "structure_conflict_changed", "Der Zuordnungsstand hat sich geändert. Bitte neu laden.");
        if (result.Value.Preview.OverlappingFilters || result.Value.Preview.NewConflicts > 0 || result.Value.Preview.OverlappingMeals > 0 || result.Value.Preview.Changes.Count == 0 || result.Value.Preview.Resolved == 0)
            return Invalid("structure_resolution_incomplete", "Diese Auswahl behebt keine fehlende oder mehrfache Zuordnung oder erzeugt neue Konflikte.");
        return await store.ApplyStructureResolutionAsync(campId, StructureDataVersion(result.Value.Data), result.Value.Preview.Changes, ct);
    }
}
