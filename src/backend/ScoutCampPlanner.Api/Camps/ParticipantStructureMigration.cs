using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ScoutCampPlanner.Camp.Infrastructure;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Application.MealPlanning;

namespace ScoutCampPlanner.Api.Camps;

public static class ParticipantStructureMigration
{
    public static async Task RunAsync(CampDbContext camp, CateringDbContext catering, ILogger logger, CancellationToken ct = default)
    {
        var campIds = await catering.Set<MealPlanningParticipantConfiguration>().Select(value => value.CampId).ToArrayAsync(ct);
        foreach (Guid campId in campIds)
        {
            var owner = await camp.Camps.AsNoTracking().SingleOrDefaultAsync(value => value.Id == campId, ct);
            if (owner is null || owner.IsFrozen) continue;
            var data = await new MealPlanningStore(catering).LoadAsync(campId, ct);
            if (!MealPlanningService.HasLegacyAssignments(data)) continue;
            var planning = await camp.GetPlanningDataAsync(campId, ct);
            if (planning is null) continue;
            try
            {
                var mapping = LegacyParticipantStructureMigration.Resolve(planning.Nodes, MealPlanningService.ReadAssignments(data),
                    data.CookingUnits.ToDictionary(value => value.Id, value => (IReadOnlyList<Guid>)data.StructureAssignments
                        .Where(item => item.CookingUnitId == value.Id && item.CampMealId is null).Select(item => item.StructureNodeId).ToArray()));
                if (owner.StructureMode == ScoutCampPlanner.Camp.Domain.CampStructureMode.Fixed)
                    foreach (var nodeId in mapping.Values.Distinct())
                    {
                        int depth = 1; Guid? parent = planning.Nodes.Single(value => value.Id == nodeId).ParentId;
                        while (parent is Guid id) { depth++; parent = planning.Nodes.Single(value => value.Id == id).ParentId; }
                        if (depth != owner.GetStructureLevelNames().Count) throw new InvalidOperationException("Altzuordnung liegt nicht auf der festgelegten Teilnehmerebene.");
                    }
                await using var transaction = await camp.Database.BeginTransactionAsync(ct);
                await catering.Database.UseTransactionAsync(transaction.GetDbTransaction(), ct);
                try
                {
                    var people = await camp.Participants.Where(value => value.CampId == campId).ToDictionaryAsync(value => value.Id, ct);
                    foreach (var pair in mapping)
                    {
                        if (!people.TryGetValue(pair.Key, out var person) || person.StructureNodeId is Guid existing && existing != pair.Value)
                            throw new InvalidOperationException("Bestehende Teilnehmerstruktur widerspricht der Altzuordnung; Daten bleiben erhalten.");
                        person.AssignStructureNode(pair.Value);
                    }
                    var configuration = await catering.Set<MealPlanningParticipantConfiguration>().SingleAsync(value => value.CampId == campId, ct);
                    configuration.Update(configuration.DemandMode, "[]");
                    foreach (var state in await catering.CookingUnitMealStates.Where(value => value.CampId == campId).ToArrayAsync(ct)) state.MarkStale();
                    await camp.SaveChangesAsync(ct); await catering.SaveChangesAsync(ct); await transaction.CommitAsync(ct);
                }
                finally { await catering.Database.UseTransactionAsync(null, CancellationToken.None); }
            }
            catch (InvalidOperationException exception)
            {
                camp.ChangeTracker.Clear(); catering.ChangeTracker.Clear();
                logger.LogWarning("Participant structure migration requires review for camp {CampId}: {Reason}", campId, exception.Message);
            }
        }
    }
}
