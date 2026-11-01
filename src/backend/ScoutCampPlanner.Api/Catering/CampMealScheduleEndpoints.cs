using System.Security.Claims;
using ScoutCampPlanner.Api.Camps;
using ScoutCampPlanner.Platform.Application.Authorization;

namespace ScoutCampPlanner.Api.Catering;

public static class CampMealScheduleEndpoints
{
    public static void MapMealScheduleEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/camps/{campId:guid}").RequireAuthorization();
        group.MapPost("/meals/additional", async (Guid campId, AddAdditionalMealRequest request,
            ClaimsPrincipal principal, CampManagementService camps, CateringPlanningService service, CancellationToken ct) =>
        {
            Guid actor = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (!await camps.HasCampPermissionAsync(actor, campId, Permissions.Camp.Edit, ct)) return Results.NotFound();
            var context = await camps.GetCampMealContextAsync(actor, campId, ct);
            if (context is null) return Results.NotFound();
            var result = await service.AddAdditionalMealAsync(actor, context.TenantId, campId, context.StartDate,
                context.EndDate, context.IsFrozen, request, ct);
            return result.Failure == UpdateCampMealsFailure.None
                ? Results.Created($"/api/camps/{campId}/meals/{result.Meal!.Id}", result) : Failure(result.Failure);
        });
        group.MapPut("/meals/days/{date}/order", async (Guid campId, DateOnly date, OrderDayMealsRequest request,
            ClaimsPrincipal principal, CampManagementService camps, CateringPlanningService service, CancellationToken ct) =>
        {
            Guid actor = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (!await camps.HasCampPermissionAsync(actor, campId, Permissions.Camp.Edit, ct)) return Results.NotFound();
            var context = await camps.GetCampMealContextAsync(actor, campId, ct);
            if (context is null) return Results.NotFound();
            var result = await service.OrderDayMealsAsync(actor, context.TenantId, campId, date, context.StartDate,
                context.EndDate, context.IsFrozen, request, ct);
            return result.Failure == UpdateCampMealsFailure.None ? Results.Ok(result) : Failure(result.Failure);
        });
        group.MapDelete("/meals/{mealId:guid}", async (Guid campId, Guid mealId, HttpRequest request,
            ClaimsPrincipal principal, CampManagementService camps, CateringPlanningService service, CancellationToken ct) =>
        {
            Guid actor = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            if (!await camps.HasCampPermissionAsync(actor, campId, Permissions.Camp.Edit, ct)) return Results.NotFound();
            var context = await camps.GetCampMealContextAsync(actor, campId, ct);
            if (context is null) return Results.NotFound();
            if (!long.TryParse(request.Headers.IfMatch.ToString().Trim('"'), out long version) || version < 0)
                return Results.BadRequest(new { code = "expected_schedule_version_required" });
            var result = await service.RemoveAdditionalMealAsync(actor, context.TenantId, campId, mealId, context.IsFrozen, version, ct);
            return result.Failure == UpdateCampMealsFailure.None ? Results.Ok(result) : Failure(result.Failure);
        });
    }

    private static IResult Failure(UpdateCampMealsFailure failure) => failure switch
    {
        UpdateCampMealsFailure.NotFound => Results.NotFound(),
        UpdateCampMealsFailure.Frozen => Results.Conflict(new { code = "camp_frozen" }),
        UpdateCampMealsFailure.Changed => Results.Conflict(new { code = "meal_schedule_changed" }),
        UpdateCampMealsFailure.NameConflict => Results.Conflict(new { code = "meal_name_conflict" }),
        UpdateCampMealsFailure.AlreadyExists => Results.Conflict(new { code = "meal_already_exists" }),
        UpdateCampMealsFailure.InUse => Results.Conflict(new { code = "meal_in_use" }),
        _ => Results.BadRequest(new { code = "invalid_meal_schedule" })
    };
}
