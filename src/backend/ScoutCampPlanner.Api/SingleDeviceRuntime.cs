using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Camp.Infrastructure;
using ScoutCampPlanner.Platform.Application.Authorization;
using ScoutCampPlanner.Platform.Domain;
using ScoutCampPlanner.Platform.Infrastructure;

namespace ScoutCampPlanner.Api;

public sealed record RemoveLocalCampRequest(Guid TransferId, bool ConfirmLoss);

public sealed class SingleDeviceRuntime
{
    public const string HeaderName = "X-ScoutCampPlanner-Device";
    private readonly byte[] tokenHash;
    public bool Enabled { get; }
    public Guid IdentityId { get; private set; }

    public SingleDeviceRuntime(IConfiguration configuration)
    {
        Enabled = configuration.GetValue<bool>("SingleDevice:Enabled");
        string token = configuration["SingleDevice:AccessToken"] ?? "";
        if (Enabled && token.Length < 32)
            throw new InvalidOperationException("Single-device startup requires a private launch token.");
        tokenHash = SHA256.HashData(Encoding.UTF8.GetBytes(token));
    }

    public bool Accepts(IPAddress? remoteAddress, string token) => Enabled &&
        remoteAddress is not null && IPAddress.IsLoopback(remoteAddress) &&
        CryptographicOperations.FixedTimeEquals(tokenHash, SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public async Task InitializeAsync(PlatformDbContext database)
    {
        if (!Enabled) return;
        var identity = await database.LocalDeviceIdentities.SingleOrDefaultAsync();
        if (identity is null)
        {
            identity = new LocalDeviceIdentity(Guid.NewGuid());
            database.LocalDeviceIdentities.Add(identity);
            await database.SaveChangesAsync();
        }
        IdentityId = identity.Id;
    }

    public bool IsOperator(Guid actorId) => Enabled && IdentityId != Guid.Empty && actorId == IdentityId;
}

public sealed class LocalDeviceAccess(
    SingleDeviceRuntime runtime, PlatformDbContext platform, CampDbContext camps)
{
    public bool IsOperator(Guid actorId) => runtime.IsOperator(actorId);

    public async Task<Guid[]> CampIdsAsync(Guid actorId, Guid tenantId, string permission,
        CancellationToken cancellationToken)
    {
        if (!IsOperator(actorId)) return [];
        var grants = await platform.LocalCampAccessGrants.AsNoTracking()
            .Where(value => value.DeviceIdentityId == actorId && value.TenantId == tenantId)
            .ToArrayAsync(cancellationToken);
        var current = await camps.Camps.AsNoTracking().Where(value => value.TenantId == tenantId &&
                !value.IsFrozen && value.ActiveTransferId != null)
            .Select(value => new { value.Id, value.ActiveTransferId }).ToArrayAsync(cancellationToken);
        return current.Where(camp => LocalCampAccessPolicy.Allows(true,
                grants.SingleOrDefault(grant => grant.CampId == camp.Id), actorId, tenantId,
                camp.Id, camp.ActiveTransferId!.Value, AuthorizationScope.Camp, permission))
            .Select(value => value.Id).ToArray();
    }

    public async Task<bool> AllowsAsync(Guid actorId, Guid campId, string permission,
        CancellationToken cancellationToken)
    {
        if (!IsOperator(actorId)) return false;
        Guid? tenant = await camps.Camps.Where(value => value.Id == campId)
            .Select(value => (Guid?)value.TenantId).SingleOrDefaultAsync(cancellationToken);
        return tenant.HasValue && (await CampIdsAsync(actorId, tenant.Value, permission, cancellationToken)).Contains(campId);
    }

    public async Task<Guid[]> TenantIdsAsync(Guid actorId, CancellationToken cancellationToken)
    {
        if (!IsOperator(actorId)) return [];
        Guid[] candidates = await platform.LocalCampAccessGrants.Where(value => value.DeviceIdentityId == actorId)
            .Select(value => value.TenantId).Distinct().ToArrayAsync(cancellationToken);
        var visible = new List<Guid>();
        foreach (Guid tenant in candidates)
            if ((await CampIdsAsync(actorId, tenant, Permissions.Camp.View, cancellationToken)).Length > 0)
                visible.Add(tenant);
        return visible.ToArray();
    }
}
