using System.Security.Claims;
using ScoutCampPlanner.Api.Camps;
using ScoutCampPlanner.Catering.Application.MealPlanning;

namespace ScoutCampPlanner.Api.Catering;

public static class RecipePlanningHintsEndpoints
{
    public static void MapRecipePlanningHintsEndpoints(this WebApplication app)
    {
        app.MapGet("/api/camps/{campId:guid}/meal-planning/recipes/{revisionId:guid}/hints", async (
            Guid campId, Guid revisionId, ClaimsPrincipal principal, CampManagementService camps,
            IRecipePlanningHintsReader reader, HttpResponse response, CancellationToken ct) =>
        {
            response.Headers.CacheControl = "no-store";
            Guid actor = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var context = await camps.GetCampMealContextAsync(actor, campId, ct);
            if (context is null) return Results.NotFound();
            var result = await reader.ReadAsync(campId, context.TenantId, revisionId, ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).RequireAuthorization();
    }
}
