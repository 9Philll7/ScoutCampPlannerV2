using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Camp.Infrastructure;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Platform.Application.Authorization;

namespace ScoutCampPlanner.Api.Camps;

public sealed record UpdateParticipantRequest(string ExpectedStateToken, ParticipantEditRequest Data);

public static class ParticipantEndpoints
{
    public static void MapParticipantEndpoints(this WebApplication app)
    {
        app.MapGet("/api/camps/{campId:guid}/participant-counts", async (Guid campId, ClaimsPrincipal principal,
            CampManagementService permissions, CampDbContext camps, HttpResponse response, CancellationToken ct) =>
        {
            response.Headers.CacheControl = "no-store";
            if (!await permissions.HasCampPermissionAsync(Actor(principal), campId, Permissions.Camp.View, ct)) return Results.NotFound();
            // Only organizational counts; no names, dietary data or health filters.
            var counts = await camps.Participants.AsNoTracking().Where(value => value.CampId == campId)
                .GroupBy(value => new { value.StructureNodeId, value.CampStageId, value.Category })
                .Select(group => new { group.Key.StructureNodeId, group.Key.CampStageId, group.Key.Category, Count = group.Count() })
                .ToArrayAsync(ct);
            var stages = await camps.CampStages.AsNoTracking().Where(value => value.CampId == campId)
                .OrderBy(value => value.SortOrder).Select(value => new { value.Id, value.Name }).ToArrayAsync(ct);
            return Results.Ok(new { Counts = counts, Stages = stages });
        }).RequireAuthorization();
        var group = app.MapGroup("/api/camps/{campId:guid}/participants").RequireAuthorization();
        group.MapGet("", async (Guid campId, ClaimsPrincipal principal, ParticipantManagementService participants,
            CampManagementService permissions, CampDbContext camps, CateringDbContext catering, HttpResponse response, CancellationToken ct) =>
        {
            response.Headers.CacheControl = "no-store";
            Guid actor = Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var documents = await participants.ListAsync(actor, campId, ct);
            if (documents is null) return Results.NotFound();
            var camp = await camps.Camps.AsNoTracking().SingleAsync(value => value.Id == campId, ct);
            var meals = await (from meal in catering.CampMeals.AsNoTracking()
                join type in catering.CampMealTypes.AsNoTracking() on meal.MealTypeId equals type.Id
                where meal.CampId == campId && meal.Date >= camp.StartDate && meal.Date <= camp.EndDate
                orderby meal.Date, type.SortOrder
                select new { meal.Id, meal.Date, type.Name, meal.IsActive }).ToArrayAsync(ct);
            return Results.Ok(new
            {
                DummyDataOnly = true, camp.StartDate, camp.EndDate, camp.IsFrozen,
                CanEdit = !camp.IsFrozen && await permissions.HasCampPermissionAsync(actor, campId, Permissions.Health.EditParticipantRequirements, ct),
                Participants = documents, Meals = meals,
                Stages = await camps.CampStages.AsNoTracking().Where(value => value.CampId == campId)
                    .OrderBy(value => value.SortOrder).Select(value => new { value.Id, value.Name }).ToArrayAsync(ct),
                ParticipantStructureDepth = camp.StructureMode == ScoutCampPlanner.Camp.Domain.CampStructureMode.Fixed
                    ? (int?)camp.GetStructureLevelNames().Count : null,
                StructureNodes = await camps.StructureNodes.AsNoTracking().Where(value => value.CampId == campId)
                    .Select(value => new { value.Id, value.ParentId, value.Name }).ToArrayAsync(ct),
                Catalog = await new ParticipantRequirementCatalogStore(catering).ReadAsync(ct, camp.TenantId)
            });
        });
        group.MapPost("", async (Guid campId, ParticipantEditRequest request, ClaimsPrincipal principal,
            ParticipantManagementService participants, CancellationToken ct) => Result(await participants.SaveAsync(
                Actor(principal), campId, null, null, request, ct)));
        group.MapPut("/{participantId:guid}", async (Guid campId, Guid participantId, UpdateParticipantRequest request,
            ClaimsPrincipal principal, ParticipantManagementService participants, CancellationToken ct) => Result(await participants.SaveAsync(
                Actor(principal), campId, participantId, request.ExpectedStateToken, request.Data, ct)));
        group.MapDelete("/{participantId:guid}", async (Guid campId, Guid participantId, HttpRequest request,
            ClaimsPrincipal principal, ParticipantManagementService participants, CancellationToken ct) => Result(await participants.DeleteAsync(
                Actor(principal), campId, participantId, request.Headers.IfMatch.ToString().Trim('"'), ct)));
    }

    private static Guid Actor(ClaimsPrincipal principal) => Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static IResult Result(ParticipantMutationResult result) => result.Status switch
    {
        ParticipantMutationStatus.Success => Results.Ok(new { result.ParticipantId }),
        ParticipantMutationStatus.Invalid => Results.BadRequest(new { code = "invalid_participant" }),
        ParticipantMutationStatus.Conflict => Results.Conflict(new { code = "participant_changed" }),
        ParticipantMutationStatus.Frozen => Results.Conflict(new { code = "camp_frozen" }),
        _ => Results.NotFound()
    };
}
