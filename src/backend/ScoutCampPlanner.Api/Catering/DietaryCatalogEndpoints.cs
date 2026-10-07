using System.Security.Claims;
using ScoutCampPlanner.Catering.Application.Ingredients;
using ScoutCampPlanner.Catering.Application.MealPlanning;
using ScoutCampPlanner.Catering.Infrastructure;

namespace ScoutCampPlanner.Api.Catering;

public sealed record SubmitDietaryContributionRequest(int Version);
public sealed record ReviewDietaryContributionRequest(bool Accept, Guid? TargetCentralId);

public static class DietaryCatalogEndpoints
{
    public static void MapDietaryCatalogEndpoints(this WebApplication app)
    {
        app.MapGet("/api/central/diet-types", (ClaimsPrincipal principal, DietaryCatalogService service,
            IIngredientEditorReferenceDataStore references, ParticipantRequirementCatalogStore requirements, CancellationToken ct) =>
            ListAsync(null, principal, service, references, requirements, ct)).RequireAuthorization();
        app.MapGet("/api/tenants/{tenantId:guid}/diet-types", (Guid tenantId, ClaimsPrincipal principal, DietaryCatalogService service,
            IIngredientEditorReferenceDataStore references, ParticipantRequirementCatalogStore requirements, CancellationToken ct) =>
            ListAsync(tenantId, principal, service, references, requirements, ct)).RequireAuthorization();
        app.MapPut("/api/central/diet-types/{id:guid}", async (Guid id, SaveDietaryTypeRequest request,
            ClaimsPrincipal principal, DietaryCatalogService service, CancellationToken ct) =>
            Result(await service.SaveAsync(Actor(principal), null, id, request, ct))).RequireAuthorization();
        app.MapPut("/api/tenants/{tenantId:guid}/diet-types/{id:guid}", async (Guid tenantId, Guid id, SaveDietaryTypeRequest request,
            ClaimsPrincipal principal, DietaryCatalogService service, CancellationToken ct) =>
            Result(await service.SaveAsync(Actor(principal), tenantId, id, request, ct))).RequireAuthorization();
        app.MapPost("/api/tenants/{tenantId:guid}/diet-types/{id:guid}/contributions", async (Guid tenantId, Guid id,
            SubmitDietaryContributionRequest request, ClaimsPrincipal principal, DietaryCatalogService service, CancellationToken ct) =>
            Result(await service.SubmitAsync(Actor(principal), tenantId, id, request.Version, ct))).RequireAuthorization();
        app.MapGet("/api/central/diet-type-contributions", async (ClaimsPrincipal principal, DietaryCatalogService service, CancellationToken ct) =>
        {
            var result = await service.ContributionsAsync(Actor(principal), ct);
            return result is null ? Results.NotFound() : Results.Ok(result);
        }).RequireAuthorization();
        app.MapPost("/api/central/diet-type-contributions/{id:guid}/review", async (Guid id, ReviewDietaryContributionRequest request,
            ClaimsPrincipal principal, DietaryCatalogService service, CancellationToken ct) =>
            Result(await service.ReviewAsync(Actor(principal), id, request.Accept, request.TargetCentralId, ct))).RequireAuthorization();
        app.MapPut("/api/central/substances/{id:guid}/threshold-default", async (Guid id, SubstanceThresholdDefaultRequest request,
            ClaimsPrincipal principal, DietaryCatalogService service, CancellationToken ct) =>
            Result(await service.SetThresholdDefaultAsync(Actor(principal), id, request, ct))).RequireAuthorization();
    }

    private static async Task<IResult> ListAsync(Guid? tenantId, ClaimsPrincipal principal, DietaryCatalogService service,
        IIngredientEditorReferenceDataStore references, ParticipantRequirementCatalogStore requirements, CancellationToken ct)
    {
        var types = await service.ListAsync(Actor(principal), tenantId, ct);
        if (types is null) return Results.NotFound();
        var catalogue = await references.GetAsync(ct);
        return Results.Ok(new { Types = types, Origins = catalogue.Origins.Where(value => DietaryCatalogStore.MainOriginCodes.Contains(value.Code)),
            Substances = (await requirements.ReadAsync(ct, tenantId)).Substances });
    }
    private static Guid Actor(ClaimsPrincipal principal) => Guid.Parse(principal.FindFirstValue(ClaimTypes.NameIdentifier)!);
    private static IResult Result(DietaryMutationResult result) => result.Status switch
    {
        DietaryMutationStatus.Success => Results.Ok(new { result.Id }),
        DietaryMutationStatus.Invalid => Results.BadRequest(new { code = "invalid_diet_type" }),
        DietaryMutationStatus.Conflict => Results.Conflict(new { code = "diet_type_changed_or_duplicate" }),
        _ => Results.NotFound()
    };
}
