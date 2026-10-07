using System.Security.Claims;

namespace ScoutCampPlanner.Api.Camps;

public static class CampExplicitPermissionEndpoints
{
    public static void MapCampExplicitPermissionEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/camps/{campId:guid}/explicit-permissions").RequireAuthorization();
        group.MapGet("", async (Guid campId, ClaimsPrincipal principal, CampExplicitPermissionService service, CancellationToken ct) =>
        {
            var result = await service.ListAsync(Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), campId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        });
        group.MapPut("/{membershipId:guid}", async (Guid campId, Guid membershipId, SetExplicitPermissionRequest request,
            ClaimsPrincipal principal, CampExplicitPermissionService service, CancellationToken ct) =>
        {
            var result = await service.SetCloudAsync(Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!),
                campId, membershipId, request.Permission, request.Granted, ct);
            return result switch
            {
                ExplicitPermissionResult.Success => Results.NoContent(),
                ExplicitPermissionResult.Invalid => Results.BadRequest(new { code = "invalid_explicit_permission" }),
                ExplicitPermissionResult.Conflict => Results.Conflict(new { code = "permission_context_changed" }),
                _ => Results.NotFound()
            };
        });
    }
}
