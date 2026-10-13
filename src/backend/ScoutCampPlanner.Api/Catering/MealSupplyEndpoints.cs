using System.Security.Claims;
using ScoutCampPlanner.Api.Camps;
using ScoutCampPlanner.Catering.Application.MealPlanning;
using ScoutCampPlanner.Platform.Application.Authorization;

namespace ScoutCampPlanner.Api.Catering;

public static class MealSupplyEndpoints
{
    public static void MapMealSupplyEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPut("/api/camps/{campId:guid}/cooking-units/{cookingUnitId:guid}/meals/{mealId:guid}/supply", async (
            Guid campId, Guid cookingUnitId, Guid mealId, SaveSupplyRequest request, ClaimsPrincipal principal,
            CampManagementService camps, MealPlanningService planning, CancellationToken cancellationToken) =>
        {
            Guid userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            IResult? denied = await RequireMealPlanningEditAsync(campId, userId, camps, cancellationToken);
            if (denied is not null) return denied;
            if (await planning.UsesActualParticipantsAsync(campId, cancellationToken) &&
                !await camps.HasCampPermissionAsync(userId, campId, Permissions.Health.ReadParticipantRequirements, cancellationToken)) return Results.NotFound();
            return ToMealPlanningResult(await planning.SaveSupplyAsync(campId, cookingUnitId, mealId, request, cancellationToken));
        }).RequireAuthorization();
        app.MapGet("/api/camps/{campId:guid}/meal-planning/supply-positions/{revisionId:guid}", async (
            Guid campId, Guid revisionId, ClaimsPrincipal principal, CampManagementService camps,
            MealPlanningService planning, CancellationToken cancellationToken) =>
        {
            Guid userId = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (await camps.GetCampMealContextAsync(userId, campId, cancellationToken) is null) return Results.NotFound();
            var positions = await planning.GetSupplyPositionsAsync(campId, revisionId, cancellationToken);
            return positions is null ? Results.NotFound() : Results.Ok(positions);
        }).RequireAuthorization();
        app.MapPost("/api/camps/{campId:guid}/cooking-units/{cookingUnitId:guid}/meals/{mealId:guid}/verify", async (
            Guid campId, Guid cookingUnitId, Guid mealId, VerifyMealRequest request, ClaimsPrincipal principal,
            MealVerificationService verification, CancellationToken cancellationToken) =>
            ToMealPlanningResult(await verification.VerifyAsync(campId, cookingUnitId, mealId,
                Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!), request, cancellationToken)))
            .RequireAuthorization();
    }

    private static async Task<IResult?> RequireMealPlanningEditAsync(Guid campId, Guid userId,
        CampManagementService camps, CancellationToken ct)
    {
        var context = await camps.GetCampMealContextAsync(userId, campId, ct);
        if (context is null) return Results.NotFound();
        if (!await camps.HasCampPermissionAsync(userId, campId, Permissions.Catering.EditMealPlanning, ct)) return Results.Forbid();
        return context.IsFrozen ? Results.Conflict(new { code = "camp_frozen" }) : null;
    }

    private static IResult ToMealPlanningResult(MealPlanningMutationResult result) => result.Status switch {
        MealPlanningMutationStatus.Success => Results.Ok(new { result.Id, result.Version }),
        MealPlanningMutationStatus.NotFound => Results.NotFound(),
        MealPlanningMutationStatus.Conflict => Results.Conflict(new { result.Code, result.Message, result.Version }),
        MealPlanningMutationStatus.Blocked => Results.Conflict(new { result.Code, result.Message, result.References }),
        _ => Results.UnprocessableEntity(new { result.Code, result.Message })
    };
}
