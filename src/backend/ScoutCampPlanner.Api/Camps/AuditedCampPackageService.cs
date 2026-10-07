using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Camp.Infrastructure;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Package;
using ScoutCampPlanner.Platform.Application.Auditing;
using ScoutCampPlanner.Platform.Application.Authorization;
using ScoutCampPlanner.Platform.Infrastructure.Auditing;

namespace ScoutCampPlanner.Api.Camps;

/// <summary>Package writes and metadata-only audit share one commit, including dummy participant transport.</summary>
public sealed class AuditedCampPackageService(CampPackageService packages, IAuditedOperationExecutor audit,
    AuditRuntimeState runtime, TimeProvider time, IHttpContextAccessor context, SingleDeviceRuntime device,
    CampDbContext camps, CateringDbContext catering)
{
    public Task<byte[]> StartOfflineTransferAsync(Guid campId, CancellationToken ct) =>
        ExecuteAsync(campId, "camp.package.exported", token => packages.StartOfflineTransferAsync(campId, token), ct);
    public Task<byte[]> CreateReturnPackageAsync(Guid campId, CancellationToken ct) =>
        ExecuteAsync(campId, "camp.package.return-exported", token => packages.CreateReturnPackageAsync(campId, token), ct);
    public async Task<bool> RemoveLocalCampAsync(Guid deviceId, Guid campId, Guid transferId, CancellationToken ct)
    {
        try
        {
            return await ExecuteAsync(campId, "camp.package.local-removed", async token =>
            {
                if (!await packages.RemoveLocalCampAsync(deviceId, campId, transferId, token)) throw new MissingLocalCampException();
                return true;
            }, ct);
        }
        catch (MissingLocalCampException) { return false; }
    }
    public Task ImportInitialPackageForDeviceAsync(byte[] bytes, Guid deviceId, CancellationToken ct)
    {
        Guid campId = CampPackageSerializer.Deserialize(bytes).Camp.Id;
        return ExecuteAsync(campId, "camp.package.imported", async token =>
        {
            await packages.ImportInitialPackageForDeviceAsync(bytes, deviceId, token);
            return true;
        }, ct);
    }
    public Task ImportReturnPackageAsync(byte[] bytes, CancellationToken ct)
    {
        Guid campId = CampPackageSerializer.Deserialize(bytes).Camp.Id;
        return ExecuteAsync(campId, "camp.package.return-imported", async token =>
        {
            await packages.ImportReturnPackageAsync(bytes, token);
            return true;
        }, ct);
    }

    private async Task<T> ExecuteAsync<T>(Guid campId, string action, Func<CancellationToken, Task<T>> operation, CancellationToken ct)
    {
        if (!Guid.TryParse(context.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out Guid actor))
            throw new CampPackageParticipantAccessException();
        Guid? tenant = await camps.Camps.AsNoTracking().Where(value => value.Id == campId)
            .Select(value => (Guid?)value.TenantId).SingleOrDefaultAsync(ct);
        AuditEventDraft Event(bool success) => new(Guid.NewGuid(), time.GetUtcNow(), success ? action : "camp.package.denied",
            success ? "success" : "denial", actor, tenant, campId, "camp", campId,
            device.Enabled ? "single-device" : "server", runtime.InstanceId, Guid.NewGuid(), null,
            AuthorizationCatalogue.DefinitionVersion, new Dictionary<string, string>());
        T result = default!;
        try
        {
            await audit.ExecuteAsync(Event(true), async token => result = await operation(token), ct);
            return result;
        }
        catch (CampPackageParticipantAccessException)
        {
            camps.ChangeTracker.Clear(); catering.ChangeTracker.Clear();
            await audit.ExecuteAsync(Event(false), _ => Task.CompletedTask, ct);
            throw;
        }
        catch
        {
            camps.ChangeTracker.Clear(); catering.ChangeTracker.Clear();
            throw;
        }
    }

    private sealed class MissingLocalCampException : Exception { }
}
