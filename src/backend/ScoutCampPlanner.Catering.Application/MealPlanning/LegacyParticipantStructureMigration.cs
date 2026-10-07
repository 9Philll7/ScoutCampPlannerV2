using ScoutCampPlanner.Camp.Contracts;

namespace ScoutCampPlanner.Catering.Application.MealPlanning;

/// <summary>One-time conversion of development data, never an operational assignment model.</summary>
public static class LegacyParticipantStructureMigration
{
    public static IReadOnlyDictionary<Guid, Guid> Resolve(IReadOnlyList<CampPlanningNode> nodes,
        IReadOnlyList<CookingUnitParticipantAssignments> legacy,
        IReadOnlyDictionary<Guid, IReadOnlyList<Guid>> unitNodes)
    {
        if (legacy.Any(value => value.MealOverrides.Count > 0))
            throw new InvalidOperationException("Mahlzeitenspezifische direkte Altzuordnungen benötigen eine explizite Strukturentscheidung; Daten bleiben erhalten.");
        var parents = nodes.ToDictionary(value => value.Id, value => value.ParentId);
        var leaves = nodes.Where(value => !nodes.Any(child => child.ParentId == value.Id)).ToArray();
        bool Covers(Guid node, IReadOnlyList<Guid> roots)
        {
            var seen = new HashSet<Guid>(); Guid? current = node;
            while (current is Guid id && seen.Add(id)) { if (roots.Contains(id)) return true; current = parents.GetValueOrDefault(id); }
            return false;
        }
        var result = new Dictionary<Guid, Guid>();
        foreach (var assignment in legacy.Where(value => value.DefaultParticipantIds.Count > 0))
        {
            var candidates = leaves.Where(value => Covers(value.Id, unitNodes.GetValueOrDefault(assignment.CookingUnitId) ?? [])).ToArray();
            if (candidates.Length != 1 || unitNodes.Count(value => Covers(candidates[0].Id, value.Value)) != 1)
                throw new InvalidOperationException("Direkte Altzuordnung ist keinem eindeutigen Blattknoten zuordenbar; Daten bleiben erhalten.");
            foreach (Guid person in assignment.DefaultParticipantIds)
                if (!result.TryAdd(person, candidates[0].Id)) throw new InvalidOperationException("Doppelte direkte Altzuordnung; Daten bleiben erhalten.");
        }
        return result;
    }
}
