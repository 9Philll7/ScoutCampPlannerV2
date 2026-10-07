using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ScoutCampPlanner.Camp.Contracts;
using ScoutCampPlanner.Camp.Domain;
using ScoutCampPlanner.Camp.Infrastructure;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Platform.Application.Auditing;
using ScoutCampPlanner.Platform.Application.Authorization;
using ScoutCampPlanner.Platform.Infrastructure;
using ScoutCampPlanner.Platform.Infrastructure.Auditing;

namespace ScoutCampPlanner.Api.Camps;

public sealed record ParticipantEditRequest(string DisplayName, Guid? DietTypeId,
    IReadOnlyList<DateOnly> AbsentDays, IReadOnlyList<Guid> AbsentMealIds,
    IReadOnlyList<Guid> AllergenIds, IReadOnlyList<ParticipantIntoleranceData> Intolerances, Guid? StructureNodeId = null);
public sealed record ParticipantDocument(ParticipantPlanningData Data, string StateToken);
public enum ParticipantMutationStatus { Success, NotFound, Frozen, Invalid, Conflict }
public sealed record ParticipantMutationResult(ParticipantMutationStatus Status, Guid? ParticipantId = null);

/// <summary>
/// Composition-level dummy participant use cases. No permission is granted by this service.
/// </summary>
public sealed class ParticipantManagementService(
    CampManagementService authorization, CampDbContext camps, CateringDbContext catering,
    PlatformDbContext platform, IAuditedOperationExecutor audit, AuditRuntimeState runtime,
    TimeProvider timeProvider, LocalDeviceAccess? localAccess = null)
{
    public async Task<IReadOnlyList<ParticipantDocument>?> ListAsync(Guid actorId, Guid campId,
        CancellationToken cancellationToken = default)
    {
        if (!await authorization.HasCampPermissionAsync(actorId, campId,
                Permissions.Health.ReadParticipantRequirements, cancellationToken))
        {
            await DeniedAsync(actorId, campId, cancellationToken);
            return null;
        }
        var camp = await camps.Camps.AsNoTracking().SingleAsync(value => value.Id == campId, cancellationToken);
        IReadOnlyList<ParticipantDocument> result = [];
        try
        {
            await audit.ExecuteAsync(Event("health.participants.read", "success", actorId, campId,
                camp.TenantId, "camp", campId), async (CancellationToken ct) =>
            {
                await camps.Database.UseTransactionAsync(platform.Database.CurrentTransaction!.GetDbTransaction(), ct);
                try
                {
                    if (!await authorization.HasCampPermissionAsync(actorId, campId,
                            Permissions.Health.ReadParticipantRequirements, ct)) throw new AccessChangedException();
                    result = (await camps.GetParticipantsAsync(campId, ct))
                        .Select(value => new ParticipantDocument(value, Token(value))).ToArray();
                }
                finally { await camps.Database.UseTransactionAsync(null, CancellationToken.None); }
            }, cancellationToken);
        }
        catch (AccessChangedException)
        {
            await DeniedAsync(actorId, campId, cancellationToken);
            return null;
        }
        return result;
    }

    public Task<ParticipantMutationResult> SaveAsync(Guid actorId, Guid campId,
        Guid? participantId, string? expectedStateToken, ParticipantEditRequest request,
        CancellationToken cancellationToken = default) =>
        MutateAsync(actorId, campId, participantId, expectedStateToken, request, false, cancellationToken);

    public Task<ParticipantMutationResult> DeleteAsync(Guid actorId, Guid campId,
        Guid participantId, string expectedStateToken, CancellationToken cancellationToken = default) =>
        MutateAsync(actorId, campId, participantId, expectedStateToken, null, true, cancellationToken);

    private async Task<ParticipantMutationResult> MutateAsync(Guid actorId, Guid campId,
        Guid? participantId, string? expectedStateToken, ParticipantEditRequest? request, bool delete,
        CancellationToken cancellationToken)
    {
        if (!await authorization.HasCampPermissionAsync(actorId, campId,
                Permissions.Health.EditParticipantRequirements, cancellationToken))
        {
            await DeniedAsync(actorId, campId, cancellationToken);
            return new(ParticipantMutationStatus.NotFound);
        }
        if (participantId == Guid.Empty || (participantId.HasValue
                ? string.IsNullOrWhiteSpace(expectedStateToken) : expectedStateToken is not null))
            return new(ParticipantMutationStatus.Invalid);
        var camp = await camps.Camps.AsNoTracking().SingleAsync(value => value.Id == campId, cancellationToken);
        if (camp.IsFrozen) return new(ParticipantMutationStatus.Frozen);
        Guid id = participantId ?? Guid.NewGuid();
        Participant? desired = null;
        if (!delete)
        {
            try { desired = Build(id, campId, request!); }
            catch (ArgumentException) { return new(ParticipantMutationStatus.Invalid); }
        }
        string action = delete ? "health.participant.deleted" : participantId.HasValue
            ? "health.participant.updated" : "health.participant.created";
        try
        {
            await audit.ExecuteAsync(Event(action, "success", actorId, campId, camp.TenantId, "participant", id), async (CancellationToken ct) =>
            {
                await camps.Database.UseTransactionAsync(platform.Database.CurrentTransaction!.GetDbTransaction(), ct);
                await catering.Database.UseTransactionAsync(platform.Database.CurrentTransaction!.GetDbTransaction(), ct);
                try
                {
                    // Serialize participant writes with camp freeze/replace; recheck inside the transaction.
                    int available = await camps.Camps.Where(value => value.Id == campId && !value.IsFrozen)
                        .ExecuteUpdateAsync(update => update.SetProperty(value => value.BaselineVersion,
                            value => value.BaselineVersion), ct);
                    if (available != 1) throw new RejectedMutationException(ParticipantMutationStatus.Frozen);
                    if (!await authorization.HasCampPermissionAsync(actorId, campId,
                            Permissions.Health.EditParticipantRequirements, ct)) throw new AccessChangedException();
                    var currentCamp = await camps.Camps.AsNoTracking().SingleAsync(value => value.Id == campId, ct);
                    if (!delete && !await ReferencesValidAsync(desired!, currentCamp.StartDate, currentCamp.EndDate, currentCamp.TenantId, ct))
                        throw new RejectedMutationException(ParticipantMutationStatus.Invalid);
                    Participant? current = null;
                    if (participantId.HasValue)
                    {
                        var data = (await camps.GetParticipantsAsync(campId, ct)).SingleOrDefault(value => value.Id == id);
                        if (data is null) throw new RejectedMutationException(ParticipantMutationStatus.NotFound);
                        if (Token(data) != expectedStateToken) throw new RejectedMutationException(ParticipantMutationStatus.Conflict);
                        current = await camps.Participants.SingleAsync(value => value.CampId == campId && value.Id == id, ct);
                    }
                    if (delete) camps.Participants.Remove(current!);
                    else if (current is null) camps.Participants.Add(desired!);
                    else
                    {
                        current.Rename(desired!.DisplayName);
                        current.AssignStructureNode(desired.StructureNodeId);
                        current.SetRequirements(desired.DietTypeId, desired.Allergens.Select(value => value.AllergenId), desired.Intolerances);
                        foreach (var day in current.AbsentDays.ToArray()) current.SetDayAbsent(day.Date, false);
                        foreach (var meal in current.AbsentMeals.ToArray()) current.SetMealAbsent(meal.MealId, false);
                        foreach (var day in desired.AbsentDays) current.SetDayAbsent(day.Date, true);
                        foreach (var meal in desired.AbsentMeals) current.SetMealAbsent(meal.MealId, true);
                    }
                    await camps.SaveChangesAsync(ct);
                }
                finally
                {
                    await catering.Database.UseTransactionAsync(null, CancellationToken.None);
                    await camps.Database.UseTransactionAsync(null, CancellationToken.None);
                }
            }, cancellationToken);
            return new(ParticipantMutationStatus.Success, id);
        }
        catch (RejectedMutationException exception)
        {
            camps.ChangeTracker.Clear();
            return new(exception.Status);
        }
        catch (AccessChangedException)
        {
            camps.ChangeTracker.Clear();
            await DeniedAsync(actorId, campId, cancellationToken);
            return new(ParticipantMutationStatus.NotFound);
        }
        catch
        {
            camps.ChangeTracker.Clear();
            throw;
        }
    }

    private async Task<bool> ReferencesValidAsync(Participant desired, DateOnly? start, DateOnly? end, Guid tenantId, CancellationToken ct)
    {
        if (desired.StructureNodeId is Guid nodeId)
        {
            var nodes = await camps.StructureNodes.Where(value => value.CampId == desired.CampId).ToDictionaryAsync(value => value.Id, ct);
            if (!nodes.TryGetValue(nodeId, out var node) || nodes.Values.Any(value => value.ParentId == nodeId)) return false;
            var owner = await camps.Camps.AsNoTracking().SingleAsync(value => value.Id == desired.CampId, ct);
            int depth = 1;
            while (node.ParentId is Guid parent && nodes.TryGetValue(parent, out var ancestor)) { depth++; node = ancestor; }
            if (owner.StructureMode == CampStructureMode.Fixed && depth != owner.GetStructureLevelNames().Count) return false;
        }
        if ((desired.AbsentDays.Count > 0 || desired.AbsentMeals.Count > 0) && (start is null || end is null)) return false;
        if (desired.AbsentDays.Any(value => value.Date < start || value.Date > end)) return false;
        Guid[] meals = desired.AbsentMeals.Select(value => value.MealId).ToArray();
        if (await catering.CampMeals.CountAsync(value => meals.Contains(value.Id) && value.CampId == desired.CampId &&
                value.Date >= start && value.Date <= end, ct) != meals.Length) return false;
        Guid[] allergens = desired.Allergens.Select(value => value.AllergenId).ToArray();
        Guid[] substances = desired.Intolerances.Select(value => value.SubstanceId).ToArray();
        return await new ParticipantRequirementCatalogStore(catering).ContainsAsync(desired.DietTypeId, allergens, substances, ct, tenantId);
    }

    private static Participant Build(Guid id, Guid campId, ParticipantEditRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.AbsentDays);
        ArgumentNullException.ThrowIfNull(request.AbsentMealIds);
        ArgumentNullException.ThrowIfNull(request.AllergenIds);
        ArgumentNullException.ThrowIfNull(request.Intolerances);
        if (request.Intolerances.Any(value => value is null)) throw new ArgumentException("Invalid requirement.");
        var participant = new Participant(id, campId, request.DisplayName);
        participant.AssignStructureNode(request.StructureNodeId);
        participant.SetRequirements(request.DietTypeId, request.AllergenIds, request.Intolerances.Select(value =>
            new ParticipantIntolerance(value.SubstanceId, value.ThresholdGramsPerPortion, value.ThresholdSource)));
        foreach (DateOnly date in request.AbsentDays) participant.SetDayAbsent(date, true);
        foreach (Guid mealId in request.AbsentMealIds) participant.SetMealAbsent(mealId, true);
        return participant;
    }

    private static string Token(ParticipantPlanningData data) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(data)));

    private Task DeniedAsync(Guid actorId, Guid campId, CancellationToken ct) =>
        audit.ExecuteAsync(Event("health.participants.denied", "denial", actorId, campId,
            null, "camp", campId), _ => Task.CompletedTask, ct);

    private AuditEventDraft Event(string action, string result, Guid actorId, Guid campId,
        Guid? tenantId, string targetType, Guid targetId) => new(Guid.NewGuid(), timeProvider.GetUtcNow(),
        action, result, actorId, tenantId, campId, targetType, targetId,
        localAccess?.IsOperator(actorId) == true ? "single-device" : "server", runtime.InstanceId,
        Guid.NewGuid(), null, AuthorizationCatalogue.DefinitionVersion, new Dictionary<string, string>());

    private sealed class RejectedMutationException(ParticipantMutationStatus status) : Exception
    {
        public ParticipantMutationStatus Status { get; } = status;
    }
    private sealed class AccessChangedException : Exception { }
}
