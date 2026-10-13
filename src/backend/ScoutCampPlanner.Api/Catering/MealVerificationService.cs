using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ScoutCampPlanner.Api.Camps;
using ScoutCampPlanner.Camp.Infrastructure;
using ScoutCampPlanner.Catering.Application.MealPlanning;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Platform.Application.Auditing;
using ScoutCampPlanner.Platform.Application.Authorization;
using ScoutCampPlanner.Platform.Infrastructure;
using ScoutCampPlanner.Platform.Infrastructure.Auditing;

namespace ScoutCampPlanner.Api.Catering;

public sealed class MealVerificationService(CampManagementService camps, MealPlanningService planning,
    PlatformDbContext platform, CateringDbContext catering, CampDbContext campDatabase, IAuditedOperationExecutor audit,
    AuditRuntimeState runtime, TimeProvider time, SingleDeviceRuntime device)
{
    public async Task<MealPlanningMutationResult> VerifyAsync(Guid campId, Guid unitId, Guid mealId,
        Guid actorId, VerifyMealRequest request, CancellationToken ct)
    {
        var context = await camps.GetCampMealContextAsync(actorId, campId, ct);
        if (context is null || !await camps.HasCampPermissionAsync(actorId, campId, Permissions.Catering.VerifyMealPlanning, ct) ||
            await planning.UsesActualParticipantsAsync(campId, ct) &&
            !await camps.HasCampPermissionAsync(actorId, campId, Permissions.Health.ReadParticipantRequirements, ct))
            return new(MealPlanningMutationStatus.NotFound);
        if (context.IsFrozen) return new(MealPlanningMutationStatus.Blocked, "camp_frozen");
        var entry = new AuditEventDraft(Guid.NewGuid(), time.GetUtcNow(), "catering.meal-planning.verified",
            "success", actorId, context.TenantId, campId, "cooking-unit", unitId,
            device.Enabled ? "single-device" : "server", runtime.InstanceId, Guid.NewGuid(), null,
            AuthorizationCatalogue.DefinitionVersion, new Dictionary<string, string> { ["mealId"] = mealId.ToString() });
        MealPlanningMutationResult result = new(MealPlanningMutationStatus.Invalid);
        try
        {
            await audit.ExecuteAsync(entry, async token =>
            {
                await catering.Database.UseTransactionAsync(platform.Database.CurrentTransaction!.GetDbTransaction(), token);
                await campDatabase.Database.UseTransactionAsync(platform.Database.CurrentTransaction!.GetDbTransaction(), token);
                try
                {
                    result = await planning.VerifySupplyAsync(campId, unitId, mealId, actorId, request, token);
                    if (!result.IsSuccess) throw new VerificationRejected();
                }
                finally
                {
                    await catering.Database.UseTransactionAsync(null, CancellationToken.None);
                    await campDatabase.Database.UseTransactionAsync(null, CancellationToken.None);
                }
            }, ct);
        }
        catch (VerificationRejected) { catering.ChangeTracker.Clear(); return result; }
        catch { catering.ChangeTracker.Clear(); throw; }
        return result;
    }
    private sealed class VerificationRejected : Exception;
}
