using ScoutCampPlanner.Camp.Contracts;
using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Application.MealPlanning;

public sealed record StructureParticipantSelection(Guid CookingUnitId, IReadOnlyList<Guid> StructureNodeIds,
    CookingUnitParticipantFilter Filter);
public sealed record StructureParticipantProjectionResult(
    IReadOnlyDictionary<Guid, IReadOnlyList<CateringParticipantData>> ParticipantsByUnit,
    IReadOnlyList<Guid> Unassigned, IReadOnlyList<Guid> Overlapping);

public static class StructureParticipantProjection
{
    public static StructureParticipantProjectionResult Derive(IReadOnlyList<CampPlanningNode> nodes,
        IReadOnlyList<CateringParticipantData> participants, IReadOnlyList<StructureParticipantSelection> units)
    {
        var parents = nodes.ToDictionary(value => value.Id, value => value.ParentId);
        bool Covers(Guid? node, IReadOnlyList<Guid> roots)
        {
            var visited = new HashSet<Guid>();
            while (node is Guid id && visited.Add(id))
            {
                if (roots.Contains(id)) return true;
                node = parents.GetValueOrDefault(id);
            }
            return false;
        }
        var matches = units.ToDictionary(unit => unit.CookingUnitId, unit =>
            (IReadOnlyList<CateringParticipantData>)participants.Where(person => person.IsPresent &&
                Covers(person.StructureNodeId, unit.StructureNodeIds) && Matches(unit.Filter, person.RequiresSpecialCatering)).ToArray());
        var counts = matches.Values.SelectMany(value => value).GroupBy(value => value.ParticipantReference)
            .ToDictionary(value => value.Key, value => value.Count());
        var overlapping = counts.Where(value => value.Value > 1).Select(value => value.Key).Order().ToArray();
        // Never choose a winner for inconsistent imports or later participant/profile changes.
        var unique = matches.ToDictionary(value => value.Key, value =>
            (IReadOnlyList<CateringParticipantData>)value.Value.Where(person => !overlapping.Contains(person.ParticipantReference)).ToArray());
        return new(unique, participants.Where(value => value.IsPresent && !counts.ContainsKey(value.ParticipantReference))
            .Select(value => value.ParticipantReference).Order().ToArray(), overlapping);
    }

    public static bool Matches(CookingUnitParticipantFilter filter, bool special) => filter switch
    {
        CookingUnitParticipantFilter.All => true,
        CookingUnitParticipantFilter.SpecialCateringOnly => special,
        CookingUnitParticipantFilter.WithoutSpecialCatering => !special,
        _ => throw new ArgumentOutOfRangeException(nameof(filter))
    };
}
