using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ScoutCampPlanner.Camp.Infrastructure;
using ScoutCampPlanner.Platform.Application.Auditing;
using ScoutCampPlanner.Platform.Application.Authorization;
using ScoutCampPlanner.Platform.Domain;
using ScoutCampPlanner.Platform.Infrastructure;
using ScoutCampPlanner.Platform.Infrastructure.Auditing;

namespace ScoutCampPlanner.Api.Camps;

public sealed record CampExplicitPermissionMember(Guid MembershipId, Guid UserId,
    string DisplayName, IReadOnlyList<string> Permissions);
public enum ExplicitPermissionResult { Success, NotFound, Invalid, Conflict }
public sealed record SetExplicitPermissionRequest(string Permission, bool Granted);

public sealed class CampExplicitPermissionService(PlatformDbContext platform, CampDbContext camps,
    CampManagementService authorization, IAuditedOperationExecutor audit, AuditRuntimeState runtime,
    TimeProvider timeProvider, SingleDeviceRuntime deviceRuntime)
{
    public async Task<IReadOnlyList<CampExplicitPermissionMember>?> ListAsync(Guid actorId, Guid campId,
        CancellationToken ct = default)
    {
        if (deviceRuntime.Enabled || !await authorization.HasCampPermissionAsync(actorId, campId, Permissions.Camp.ManageMembers, ct))
            return null;
        Guid tenantId = await camps.Camps.Where(value => value.Id == campId).Select(value => value.TenantId).SingleAsync(ct);
        var members = await (from membership in platform.CampMemberships.AsNoTracking()
            join tenant in platform.TenantMemberships.AsNoTracking() on membership.TenantMembershipId equals tenant.Id
            join user in platform.UserAccounts.AsNoTracking() on tenant.UserId equals user.Id
            where membership.CampId == campId && tenant.TenantId == tenantId &&
                membership.State == CampMembershipState.Active && tenant.State == TenantMembershipState.Active
            select new { MembershipId = membership.Id, UserId = user.Id, user.Email }).ToArrayAsync(ct);
        Guid[] ids = members.Select(value => value.MembershipId).ToArray();
        var grants = await platform.CampPermissionGrants.AsNoTracking().Where(value => ids.Contains(value.MembershipId)).ToArrayAsync(ct);
        return members.Select(value => new CampExplicitPermissionMember(value.MembershipId, value.UserId, value.Email,
            grants.Where(grant => grant.MembershipId == value.MembershipId && LocalCampAccessPolicy.IsExplicitPermission(grant.Permission))
                .Select(grant => grant.Permission).Order(StringComparer.Ordinal).ToArray())).ToArray();
    }

    public async Task<ExplicitPermissionResult> SetCloudAsync(Guid actorId, Guid campId, Guid membershipId,
        string permission, bool granted, CancellationToken ct = default)
    {
        if (!LocalCampAccessPolicy.IsExplicitPermission(permission)) return ExplicitPermissionResult.Invalid;
        if (deviceRuntime.Enabled || !await authorization.HasCampPermissionAsync(actorId, campId, Permissions.Camp.ManageMembers, ct))
        {
            await audit.ExecuteAsync(Event(actorId, campId, null, membershipId, permission, granted, false, "server"),
                _ => Task.CompletedTask, ct);
            return ExplicitPermissionResult.NotFound;
        }
        Guid tenantId = await camps.Camps.Where(value => value.Id == campId).Select(value => value.TenantId).SingleAsync(ct);
        try
        {
            await audit.ExecuteAsync(Event(actorId, campId, tenantId, membershipId, permission, granted, true, "server"),
                async (CancellationToken token) =>
                {
                    await camps.Database.UseTransactionAsync(platform.Database.CurrentTransaction!.GetDbTransaction(), token);
                    try
                    {
                        if (!await authorization.HasCampPermissionAsync(actorId, campId, Permissions.Camp.ManageMembers, token))
                            throw new GrantRejectedException();
                        bool active = await (from membership in platform.CampMemberships
                            join tenant in platform.TenantMemberships on membership.TenantMembershipId equals tenant.Id
                            where membership.Id == membershipId && membership.CampId == campId && tenant.TenantId == tenantId &&
                                membership.State == CampMembershipState.Active && tenant.State == TenantMembershipState.Active
                            select membership.Id).AnyAsync(token);
                        if (!active) throw new GrantRejectedException();
                        var existing = await platform.CampPermissionGrants.SingleOrDefaultAsync(value =>
                            value.MembershipId == membershipId && value.Permission == permission, token);
                        if (granted && existing is null) platform.CampPermissionGrants.Add(new(membershipId, permission));
                        if (!granted && existing is not null) platform.CampPermissionGrants.Remove(existing);
                    }
                    finally { await camps.Database.UseTransactionAsync(null, CancellationToken.None); }
                }, ct);
            return ExplicitPermissionResult.Success;
        }
        catch (GrantRejectedException)
        {
            await audit.ExecuteAsync(Event(actorId, campId, tenantId, membershipId, permission, granted, false, "server"),
                _ => Task.CompletedTask, ct);
            return ExplicitPermissionResult.NotFound;
        }
    }

    // Deliberately not mapped to HTTP. Only the explicit local OS-operator command calls this path.
    public async Task<ExplicitPermissionResult> SetLocalFromCommandAsync(Guid campId, Guid transferId,
        string permission, bool granted, CancellationToken ct = default)
    {
        if (!camps.Database.IsSqlite() || transferId == Guid.Empty || !LocalCampAccessPolicy.IsExplicitPermission(permission))
            return ExplicitPermissionResult.Invalid;
        var device = await platform.LocalDeviceIdentities.AsNoTracking().SingleOrDefaultAsync(ct);
        var camp = await camps.Camps.AsNoTracking().SingleOrDefaultAsync(value => value.Id == campId, ct);
        if (device is null || camp is null) return ExplicitPermissionResult.NotFound;
        try
        {
            await audit.ExecuteAsync(Event(device.Id, campId, camp.TenantId, device.Id, permission, granted, true, "single-device"),
                async (CancellationToken token) =>
                {
                    await camps.Database.UseTransactionAsync(platform.Database.CurrentTransaction!.GetDbTransaction(), token);
                    try
                    {
                        if (!await camps.Camps.AnyAsync(value => value.Id == campId && !value.IsFrozen &&
                                value.ActiveTransferId == transferId, token) ||
                            !await platform.LocalCampAccessGrants.AnyAsync(value => value.CampId == campId &&
                                value.DeviceIdentityId == device.Id && value.TenantId == camp.TenantId && value.TransferId == transferId, token))
                            throw new GrantRejectedException();
                        var existing = await platform.LocalCampPermissionGrants.SingleOrDefaultAsync(value =>
                            value.DeviceIdentityId == device.Id && value.CampId == campId && value.TransferId == transferId &&
                            value.Permission == permission, token);
                        if (granted && existing is null) platform.LocalCampPermissionGrants.Add(new(device.Id, camp.TenantId, campId, transferId, permission));
                        if (!granted && existing is not null) platform.LocalCampPermissionGrants.Remove(existing);
                    }
                    finally { await camps.Database.UseTransactionAsync(null, CancellationToken.None); }
                }, ct);
            return ExplicitPermissionResult.Success;
        }
        catch (GrantRejectedException)
        {
            await audit.ExecuteAsync(Event(device.Id, campId, camp.TenantId, device.Id, permission, granted, false, "single-device"),
                _ => Task.CompletedTask, ct);
            return ExplicitPermissionResult.Conflict;
        }
    }

    private AuditEventDraft Event(Guid actor, Guid camp, Guid? tenant, Guid target, string permission, bool granted, bool allowed, string origin) =>
        new(Guid.NewGuid(), timeProvider.GetUtcNow(), allowed ? (granted ? "camp.permission.granted" : "camp.permission.revoked") : "camp.permission.denied",
            allowed ? "success" : "denial", actor, tenant, camp, "permission-grant", target, origin, runtime.InstanceId,
            Guid.NewGuid(), null, AuthorizationCatalogue.DefinitionVersion,
            new Dictionary<string, string> { ["permission"] = permission });

    private sealed class GrantRejectedException : Exception { }
}
