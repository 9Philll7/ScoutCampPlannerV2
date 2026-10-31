using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ScoutCampPlanner.Api.Camps;
using ScoutCampPlanner.Camp.Infrastructure;
using ScoutCampPlanner.Catering.Application.MealPlanning;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Platform.Application.Auditing;
using ScoutCampPlanner.Platform.Application.Authorization;
using ScoutCampPlanner.Platform.Infrastructure;
using ScoutCampPlanner.Platform.Infrastructure.Auditing;

namespace ScoutCampPlanner.Api.Catering;

public sealed record SaveParticipantFilterRequest(int ExpectedVersion, string Name, ParticipantFilterDefinition Definition);

public static class ParticipantFilterEndpoints
{
    public static void MapParticipantFilterEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/camps/{campId:guid}/participant-filters").RequireAuthorization();
        group.MapGet("", async (Guid campId, ClaimsPrincipal user, CampManagementService permissions,
            CampDbContext camps, CateringDbContext catering, HttpResponse response, CancellationToken ct) =>
        {
            response.Headers.CacheControl = "no-store";
            var actor = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var context = await permissions.GetCampMealContextAsync(actor, campId, ct);
            if (context is null || !await permissions.HasCampPermissionAsync(actor, campId,
                    Permissions.Health.ReadParticipantRequirements, ct)) return Results.NotFound();
            var camp = await camps.Camps.AsNoTracking().SingleAsync(value => value.Id == campId, ct);
            var filters = await catering.Set<CampParticipantFilter>().AsNoTracking().Where(value => value.CampId == campId)
                .OrderBy(value => value.Name).ToArrayAsync(ct);
            return Results.Ok(new
            {
                CanEdit = !camp.IsFrozen && await permissions.HasCampPermissionAsync(actor, campId, Permissions.Catering.EditMealPlanning, ct),
                Filters = filters.Select(value => new { value.Id, value.Name, value.Version, Definition = value.ReadDefinition() }),
                Stages = await camps.CampStages.AsNoTracking().Where(value => value.CampId == campId)
                    .OrderBy(value => value.SortOrder).Select(value => new { value.Id, value.Name }).ToArrayAsync(ct),
                Catalog = await new ParticipantRequirementCatalogStore(catering).ReadAsync(ct, camp.TenantId)
            });
        });
        group.MapPut("/{id:guid}", (Guid campId, Guid id, SaveParticipantFilterRequest request, ClaimsPrincipal user,
            ParticipantFilterConfigurationService service, CancellationToken ct) =>
            service.SaveAsync(Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!), campId, id, request, ct));
    }
}

// Composition service: joins module-owned persistence only at the API boundary, in the audited transaction.
public sealed class ParticipantFilterConfigurationService(CampManagementService permissions, CampDbContext camps,
    CateringDbContext catering, PlatformDbContext platform, IAuditedOperationExecutor audit, AuditRuntimeState runtime,
    TimeProvider clock, LocalDeviceAccess? localAccess = null)
{
    public async Task<IResult> SaveAsync(Guid actor, Guid campId, Guid id, SaveParticipantFilterRequest request, CancellationToken ct)
    {
        async Task<bool> Allowed(CancellationToken token) =>
            await permissions.HasCampPermissionAsync(actor, campId, Permissions.Health.ReadParticipantRequirements, token) &&
            await permissions.HasCampPermissionAsync(actor, campId, Permissions.Catering.EditMealPlanning, token);
        if (!await Allowed(ct)) return Results.NotFound();
        var camp = await camps.Camps.AsNoTracking().SingleOrDefaultAsync(value => value.Id == campId, ct);
        if (camp is null) return Results.NotFound();
        if (camp.IsFrozen) return Results.Conflict(new { message = "Das Lager ist eingefroren." });
        try
        {
            _ = new CampParticipantFilter(id, campId, request.Name, request.Definition);
            await audit.ExecuteAsync(new(Guid.NewGuid(), clock.GetUtcNow(), "catering.participant-filter.saved", "success",
                actor, camp.TenantId, campId, "participant-filter", id,
                localAccess?.IsOperator(actor) == true ? "single-device" : "server", runtime.InstanceId, Guid.NewGuid(), null,
                AuthorizationCatalogue.DefinitionVersion, new Dictionary<string, string>()), async (CancellationToken token) =>
            {
                await camps.Database.UseTransactionAsync(platform.Database.CurrentTransaction!.GetDbTransaction(), token);
                await catering.Database.UseTransactionAsync(platform.Database.CurrentTransaction!.GetDbTransaction(), token);
                try
                {
                    if (await camps.Camps.Where(value => value.Id == campId && !value.IsFrozen)
                        .ExecuteUpdateAsync(update => update.SetProperty(value => value.BaselineVersion, value => value.BaselineVersion), token) != 1 ||
                        !await Allowed(token)) throw new InvalidOperationException("Lagerstatus oder Berechtigung geändert.");
                    var catalog = await new ParticipantRequirementCatalogStore(catering).ReadAsync(token, camp.TenantId);
                    new ParticipantFilterReferences((await camps.CampStages.Where(value => value.CampId == campId)
                        .Select(value => value.Id).ToArrayAsync(token)).ToHashSet(), catalog.DietTypes.Select(value => value.Id).ToHashSet(),
                        catalog.Allergens.Select(value => value.Id).ToHashSet(), catalog.Substances.Select(value => value.Id).ToHashSet())
                        .Validate(request.Definition);
                    var current = await catering.Set<CampParticipantFilter>().SingleOrDefaultAsync(value => value.Id == id, token);
                    if (current is null)
                    {
                        if (request.ExpectedVersion != 0) throw new InvalidOperationException("Filter wurde geändert oder entfernt.");
                        catering.Add(new CampParticipantFilter(id, campId, request.Name, request.Definition));
                    }
                    else
                    {
                        if (current.CampId != campId) throw new InvalidOperationException("Filter nicht verfügbar.");
                        current.Update(request.ExpectedVersion, request.Name, request.Definition);
                    }
                    await catering.SaveChangesAsync(token);
                }
                finally
                {
                    await catering.Database.UseTransactionAsync(null, CancellationToken.None);
                    await camps.Database.UseTransactionAsync(null, CancellationToken.None);
                }
            }, ct);
            return Results.Ok();
        }
        catch (ArgumentException) { catering.ChangeTracker.Clear(); return Results.BadRequest(new { message = "Name, Bedingungen oder Referenzen sind ungültig." }); }
        catch (InvalidOperationException) { catering.ChangeTracker.Clear(); return Results.Conflict(new { message = "Nicht gespeichert. Lagerstatus, Berechtigung oder Filterversion geändert. Bitte neu laden." }); }
        catch (DbUpdateException) { catering.ChangeTracker.Clear(); return Results.Conflict(new { message = "Filter wurde gleichzeitig geändert. Bitte neu laden." }); }
    }
}
