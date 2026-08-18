using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
