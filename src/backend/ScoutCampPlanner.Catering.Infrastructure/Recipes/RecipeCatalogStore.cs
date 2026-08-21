using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Infrastructure.Recipes;

public sealed class RecipeCatalogStore(CateringDbContext database) : IRecipeCatalogStore
{
    public async Task<IReadOnlyList<RecipeCatalogEntry>> ListCentralAsync(
        CancellationToken cancellationToken = default)
    {
        var rows = await (from recipe in database.Set<RecipeRecord>().AsNoTracking()
            join revision in database.Set<RecipeRevisionRecord>().AsNoTracking()
                on recipe.Id equals revision.RecipeId
            where recipe.ScopeType == (int)RecipeScopeType.Central &&
                  revision.RevisionNumber == database.Set<RecipeRevisionRecord>()
                      .Where(candidate => candidate.RecipeId == recipe.Id)
                      .Max(candidate => candidate.RevisionNumber)
            select new { Recipe = recipe, Revision = revision }).ToArrayAsync(cancellationToken);
        return rows.Select(value => MapRevision(
                null, value.Recipe, value.Revision, isLocal: false, value.Revision.PublishedAtUtc))
            .OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase).ThenBy(value => value.RecipeId)
            .ToArray();
    }

    public async Task<IReadOnlyList<RecipeCatalogEntry>> ListTenantAsync(
        Guid tenantId, CancellationToken cancellationToken = default)
    {
        TenantRecipeEntryRecord[] entries = await database.Set<TenantRecipeEntryRecord>().AsNoTracking()
            .Where(value => value.TenantId == tenantId).ToArrayAsync(cancellationToken);
        return await ResolveTenantAsync(entries, cancellationToken);
    }

    public async Task<IReadOnlyList<RecipeCatalogEntry>> ListCampAsync(
        Guid campId, CancellationToken cancellationToken = default)
    {
        CampRecipeEntryRecord[] entries = await database.Set<CampRecipeEntryRecord>().AsNoTracking()
            .Where(value => value.CampId == campId).ToArrayAsync(cancellationToken);
        Guid[] revisionIds = entries.Where(value => value.UpstreamRecipeRevisionId.HasValue)
            .Select(value => value.UpstreamRecipeRevisionId!.Value).ToArray();
        Guid[] recipeIds = entries.Where(value => value.CampRecipeId.HasValue)
            .Select(value => value.CampRecipeId!.Value).ToArray();
        Dictionary<Guid, RecipeRevisionRecord> revisions = await LoadRevisionsAsync(revisionIds, cancellationToken);
        Dictionary<Guid, RecipeRecord> recipes = await LoadRecipesAsync(
            recipeIds.Concat(revisions.Values.Select(value => value.RecipeId)).ToArray(), cancellationToken);
        Dictionary<Guid, RecipeRevisionRecord> latestLocal = await LoadLatestRevisionsAsync(recipeIds, cancellationToken);
        return entries.Select(entry => entry.UpstreamRecipeRevisionId.HasValue
                ? MapRevision(entry.Id, recipes[revisions[entry.UpstreamRecipeRevisionId.Value].RecipeId],
                    revisions[entry.UpstreamRecipeRevisionId.Value], false, entry.UpdatedAtUtc)
                : MapLocal(entry.Id, recipes[entry.CampRecipeId!.Value],
                    latestLocal.GetValueOrDefault(entry.CampRecipeId.Value), entry.UpdatedAtUtc))
            .OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase).ThenBy(value => value.LibraryEntryId)
            .ToArray();
    }

    private async Task<IReadOnlyList<RecipeCatalogEntry>> ResolveTenantAsync(
        TenantRecipeEntryRecord[] entries, CancellationToken cancellationToken)
    {
        Guid[] revisionIds = entries.Where(value => value.CentralRecipeRevisionId.HasValue)
            .Select(value => value.CentralRecipeRevisionId!.Value).ToArray();
        Guid[] localIds = entries.Where(value => value.TenantRecipeId.HasValue)
            .Select(value => value.TenantRecipeId!.Value).ToArray();
        Dictionary<Guid, RecipeRevisionRecord> revisions = await LoadRevisionsAsync(revisionIds, cancellationToken);
        Dictionary<Guid, RecipeRecord> recipes = await LoadRecipesAsync(
            localIds.Concat(revisions.Values.Select(value => value.RecipeId)).ToArray(), cancellationToken);
        Dictionary<Guid, RecipeRevisionRecord> latestLocal = await LoadLatestRevisionsAsync(localIds, cancellationToken);
        return entries.Select(entry => entry.CentralRecipeRevisionId.HasValue
                ? MapRevision(entry.Id, recipes[revisions[entry.CentralRecipeRevisionId.Value].RecipeId],
                    revisions[entry.CentralRecipeRevisionId.Value], false, entry.UpdatedAtUtc)
                : MapLocal(entry.Id, recipes[entry.TenantRecipeId!.Value],
                    latestLocal.GetValueOrDefault(entry.TenantRecipeId.Value), entry.UpdatedAtUtc))
            .OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase).ThenBy(value => value.LibraryEntryId)
            .ToArray();
    }

    private async Task<Dictionary<Guid, RecipeRevisionRecord>> LoadRevisionsAsync(
        Guid[] ids, CancellationToken cancellationToken) =>
        (await database.Set<RecipeRevisionRecord>().AsNoTracking()
            .Where(value => ids.Contains(value.Id)).ToArrayAsync(cancellationToken))
        .ToDictionary(value => value.Id);

    private async Task<Dictionary<Guid, RecipeRecord>> LoadRecipesAsync(
        Guid[] ids, CancellationToken cancellationToken) =>
        (await database.Set<RecipeRecord>().AsNoTracking()
            .Where(value => ids.Contains(value.Id)).ToArrayAsync(cancellationToken))
        .ToDictionary(value => value.Id);

    private async Task<Dictionary<Guid, RecipeRevisionRecord>> LoadLatestRevisionsAsync(
        Guid[] recipeIds, CancellationToken cancellationToken) =>
        (await database.Set<RecipeRevisionRecord>().AsNoTracking()
            .Where(value => recipeIds.Contains(value.RecipeId)).ToArrayAsync(cancellationToken))
        .GroupBy(value => value.RecipeId)
        .ToDictionary(group => group.Key, group => group.MaxBy(value => value.RevisionNumber)!);

    private static RecipeCatalogEntry MapRevision(
        Guid? entryId, RecipeRecord recipe, RecipeRevisionRecord revision,
        bool isLocal, DateTimeOffset updatedAtUtc) => new(
        entryId, recipe.Id, revision.Id, revision.RevisionNumber,
        RecipeSnapshotBuilder.Deserialize(revision.SnapshotJson).Name,
        (RecipeScopeType)recipe.ScopeType, (RecipeStatus)recipe.Status, isLocal, updatedAtUtc);

    private static RecipeCatalogEntry MapLocal(
        Guid entryId, RecipeRecord recipe, RecipeRevisionRecord? latest, DateTimeOffset updatedAtUtc) =>
        new(
            entryId, recipe.Id, latest?.Id, latest?.RevisionNumber, recipe.Name,
            (RecipeScopeType)recipe.ScopeType, (RecipeStatus)recipe.Status, true, updatedAtUtc);
}
