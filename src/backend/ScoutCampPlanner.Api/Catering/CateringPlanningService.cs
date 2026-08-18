using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ScoutCampPlanner.Api.Camps;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Platform.Application.Auditing;
using ScoutCampPlanner.Platform.Application.Authorization;
using ScoutCampPlanner.Platform.Domain;
using ScoutCampPlanner.Platform.Infrastructure;
using ScoutCampPlanner.Platform.Infrastructure.Auditing;

namespace ScoutCampPlanner.Api.Catering;

public sealed record TenantStageFoodFactorSummary(string StageName, decimal Factor);
public sealed record UpdateTenantStageFoodFactorsRequest(IReadOnlyList<TenantStageFoodFactorSummary>? Factors);
public enum UpdateFoodFactorsFailure { None, Forbidden, InvalidFactors }
public sealed record CampStageReference(Guid Id, string Name);
public sealed record CampStageFoodFactorSummary(Guid CampStageId, string StageName, decimal Factor);
public sealed record UpdateCampStageFoodFactorsRequest(IReadOnlyList<CampStageFoodFactorSummary>? Factors);
public sealed record WeightedStageTotal(Guid CampStageId, string StageName, long ChildYouthCount,
    long LeaderCount, decimal Factor, decimal FoodUnits);

public sealed class CateringPlanningService(
    PlatformDbContext platform, CateringDbContext catering, IAuditedOperationExecutor auditedOperation,
    AuditRuntimeState auditRuntime, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<TenantStageFoodFactorSummary>?> GetTenantFactorsAsync(
        Guid actorUserId, Guid tenantId, IReadOnlyList<string> stageNames, CancellationToken cancellationToken = default)
    {
        if (!await HasPermissionAsync(actorUserId, tenantId, Permissions.Tenant.View, cancellationToken)) return null;
        var stored = await catering.TenantStageFoodFactors.Where(value => value.TenantId == tenantId)
            .ToDictionaryAsync(value => value.NormalizedStageName, cancellationToken);
        return stageNames.Select(name => stored.TryGetValue(name.Trim().ToUpperInvariant(), out var factor)
            ? new TenantStageFoodFactorSummary(name, factor.Factor)
            : new TenantStageFoodFactorSummary(name, 1m)).ToList();
    }

    public async Task<UpdateFoodFactorsFailure> UpdateTenantFactorsAsync(
        Guid actorUserId, Guid tenantId, IReadOnlyList<string> stageNames,
        UpdateTenantStageFoodFactorsRequest request, CancellationToken cancellationToken = default)
    {
        if (!await HasPermissionAsync(actorUserId, tenantId, Permissions.Tenant.ManageSettings, cancellationToken))
            return UpdateFoodFactorsFailure.Forbidden;
        var factors = request.Factors?.ToArray() ?? [];
        var expected = stageNames.Select(value => value.Trim().ToUpperInvariant()).ToHashSet();
        if (factors.Length != expected.Count || factors.Select(value => value.StageName.Trim().ToUpperInvariant()).Distinct().Count() != factors.Length ||
            factors.Any(value => !expected.Contains(value.StageName.Trim().ToUpperInvariant()) || value.Factor < 0.1m ||
                value.Factor > 3m || decimal.Round(value.Factor, 2) != value.Factor))
            return UpdateFoodFactorsFailure.InvalidFactors;
        var entities = factors.Select(value => new TenantStageFoodFactor(
            Guid.NewGuid(), tenantId, value.StageName, value.Factor)).ToArray();
        var auditEvent = new AuditEventDraft(Guid.NewGuid(), timeProvider.GetUtcNow(),
            "tenant.catering-stage-factors.updated", "success", actorUserId, tenantId, null,
            "tenant-catering-stage-factors", tenantId, "server", auditRuntime.InstanceId,
            Guid.NewGuid(), null, null, new Dictionary<string, string> { ["stageCount"] = entities.Length.ToString() });
        await auditedOperation.ExecuteAsync(auditEvent, async operationCancellationToken =>
        {
            var transaction = platform.Database.CurrentTransaction!;
            await catering.Database.UseTransactionAsync(transaction.GetDbTransaction(), operationCancellationToken);
            try
            {
                await catering.TenantStageFoodFactors.Where(value => value.TenantId == tenantId)
                    .ExecuteDeleteAsync(operationCancellationToken);
                catering.TenantStageFoodFactors.AddRange(entities); await catering.SaveChangesAsync(operationCancellationToken);
            }
            finally { await catering.Database.UseTransactionAsync(null, CancellationToken.None); }
        }, cancellationToken);
        return UpdateFoodFactorsFailure.None;
    }

    public async Task<IReadOnlyList<CampStageFoodFactorSummary>> GetCampFactorsAsync(
        Guid tenantId, Guid campId, IReadOnlyList<CampStageReference> stages, CancellationToken cancellationToken = default)
    {
        var stored = await catering.CampStageFoodFactors.Where(value => value.CampId == campId)
            .ToDictionaryAsync(value => value.CampStageId, cancellationToken);
        if (stored.Count > 0) return stages.Select(stage => stored.TryGetValue(stage.Id, out var factor)
            ? new CampStageFoodFactorSummary(stage.Id, stage.Name, factor.Factor)
            : new CampStageFoodFactorSummary(stage.Id, stage.Name, 1m)).ToList();
        var tenantFactors = await catering.TenantStageFoodFactors.Where(value => value.TenantId == tenantId)
            .ToDictionaryAsync(value => value.NormalizedStageName, cancellationToken);
        return stages.Select(stage => tenantFactors.TryGetValue(stage.Name.Trim().ToUpperInvariant(), out var factor)
            ? new CampStageFoodFactorSummary(stage.Id, stage.Name, factor.Factor)
            : new CampStageFoodFactorSummary(stage.Id, stage.Name, 1m)).ToList();
    }

    public async Task<UpdateFoodFactorsFailure> UpdateCampFactorsAsync(
        Guid actorUserId, Guid tenantId, Guid campId, IReadOnlyList<CampStageReference> stages,
        UpdateCampStageFoodFactorsRequest request, CancellationToken cancellationToken = default)
    {
        var factors = request.Factors?.ToArray() ?? [];
        var stageIds = stages.Select(value => value.Id).ToHashSet();
        if (factors.Length != stageIds.Count || factors.Select(value => value.CampStageId).Distinct().Count() != factors.Length ||
            factors.Any(value => !stageIds.Contains(value.CampStageId) || value.Factor < 0.1m || value.Factor > 3m ||
                decimal.Round(value.Factor, 2) != value.Factor)) return UpdateFoodFactorsFailure.InvalidFactors;
        var entities = factors.Select(value => new CampStageFoodFactor(Guid.NewGuid(), campId,
            value.CampStageId, value.StageName, value.Factor)).ToArray();
        var auditEvent = new AuditEventDraft(Guid.NewGuid(), timeProvider.GetUtcNow(),
            "camp.catering-stage-factors.updated", "success", actorUserId, tenantId, campId,
            "camp-catering-stage-factors", campId, "server", auditRuntime.InstanceId,
            Guid.NewGuid(), null, null, new Dictionary<string, string> { ["stageCount"] = entities.Length.ToString() });
        await auditedOperation.ExecuteAsync(auditEvent, async operationCancellationToken =>
        {
            var transaction = platform.Database.CurrentTransaction!;
            await catering.Database.UseTransactionAsync(transaction.GetDbTransaction(), operationCancellationToken);
            try
            {
                await catering.CampStageFoodFactors.Where(value => value.CampId == campId)
                    .ExecuteDeleteAsync(operationCancellationToken);
                catering.CampStageFoodFactors.AddRange(entities); await catering.SaveChangesAsync(operationCancellationToken);
            }
            finally { await catering.Database.UseTransactionAsync(null, CancellationToken.None); }
        }, cancellationToken);
        return UpdateFoodFactorsFailure.None;
    }

    public static IReadOnlyList<WeightedStageTotal> CalculateWeightedTotals(
        CampPlanningSummary summary, IReadOnlyList<CampStageFoodFactorSummary> factors)
    {
        var byStage = factors.ToDictionary(value => value.CampStageId);
        return summary.StageTotals.Select(total =>
        {
            decimal factor = byStage.GetValueOrDefault(total.CampStageId)?.Factor ?? 1m;
            return new WeightedStageTotal(total.CampStageId, total.StageName, total.ChildYouthCount,
                total.LeaderCount, factor, total.ChildYouthCount * factor + total.LeaderCount);
        }).ToList();
    }

    private async Task<bool> HasPermissionAsync(Guid userId, Guid tenantId, string permission, CancellationToken cancellationToken)
    {
        string[] roles = await platform.TenantMemberships.Where(value => value.UserId == userId &&
                value.TenantId == tenantId && value.State == TenantMembershipState.Active)
            .Join(platform.TenantRoleAssignments, membership => membership.Id, role => role.MembershipId,
                (_, role) => role.RoleIdentifier).ToArrayAsync(cancellationToken);
        return TenantRoleSetValidator.Validate(roles).IsValid &&
            AuthorizationCatalogue.ResolvePermissions(AuthorizationScope.Tenant, roles).Contains(permission);
    }
}
