using System.Security.Claims;
using ScoutCampPlanner.Package;
using ScoutCampPlanner.Platform.Application.Authorization;

namespace ScoutCampPlanner.Api.Camps;

public sealed class ParticipantPackageAccess(IHttpContextAccessor context, CampManagementService authorization)
    : ICampPackageParticipantAccess
{
    public Task DemandReadAsync(Guid campId, CancellationToken ct) => DemandAsync(campId, Permissions.Health.ReadParticipantRequirements, ct);
    public Task DemandEditAsync(Guid campId, CancellationToken ct) => DemandAsync(campId, Permissions.Health.EditParticipantRequirements, ct);

    private async Task DemandAsync(Guid campId, string permission, CancellationToken ct)
    {
        if (!Guid.TryParse(context.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier), out Guid actor) ||
            !await authorization.HasCampPermissionAsync(actor, campId, permission, ct))
            throw new CampPackageParticipantAccessException();
    }
}
