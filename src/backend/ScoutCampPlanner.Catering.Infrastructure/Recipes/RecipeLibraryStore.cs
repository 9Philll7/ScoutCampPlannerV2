using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Infrastructure.Recipes;

public sealed class RecipeLibraryStore(CateringDbContext database) : IRecipeLibraryStore
{
    public async Task<RecipeLibraryMutationResult> AddCentralRevisionToTenantAsync(
        Guid entryId,
        Guid tenantId,
        Guid revisionId,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default)
    {
        RecipeScopeType? scope = await FindRevisionScopeAsync(revisionId, cancellationToken);
        if (scope is null) return new RecipeLibraryMutationResult(RecipeLibraryMutationStatus.NotFound);
        if (scope != RecipeScopeType.Central)
            return new RecipeLibraryMutationResult(RecipeLibraryMutationStatus.InvalidSourceScope);
        if (await database.Set<TenantRecipeEntryRecord>().AsNoTracking().AnyAsync(
                value => value.TenantId == tenantId && value.CentralRecipeRevisionId == revisionId,
                cancellationToken))
            return new RecipeLibraryMutationResult(RecipeLibraryMutationStatus.AlreadyExists);

        database.Add(new TenantRecipeEntryRecord
        {
            Id = entryId, TenantId = tenantId, CentralRecipeRevisionId = revisionId,
            CreatedBy = actorUserId, CreatedAtUtc = timestampUtc,
            UpdatedBy = actorUserId, UpdatedAtUtc = timestampUtc,
        });
        await database.SaveChangesAsync(cancellationToken);
        return new RecipeLibraryMutationResult(RecipeLibraryMutationStatus.Added, entryId);
    }

    public async Task<RecipeLibraryMutationResult> AddUpstreamRevisionToCampAsync(
        Guid entryId,
        Guid campId,
        Guid revisionId,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default)
    {
        RecipeScopeType? scope = await FindRevisionScopeAsync(revisionId, cancellationToken);
        if (scope is null) return new RecipeLibraryMutationResult(RecipeLibraryMutationStatus.NotFound);
        if (scope is not (RecipeScopeType.Central or RecipeScopeType.Tenant))
            return new RecipeLibraryMutationResult(RecipeLibraryMutationStatus.InvalidSourceScope);
        if (await database.Set<CampRecipeEntryRecord>().AsNoTracking().AnyAsync(
                value => value.CampId == campId && value.UpstreamRecipeRevisionId == revisionId,
                cancellationToken))
            return new RecipeLibraryMutationResult(RecipeLibraryMutationStatus.AlreadyExists);

        database.Add(new CampRecipeEntryRecord
        {
            Id = entryId, CampId = campId, UpstreamRecipeRevisionId = revisionId,
            CreatedBy = actorUserId, CreatedAtUtc = timestampUtc,
            UpdatedBy = actorUserId, UpdatedAtUtc = timestampUtc,
        });
        await database.SaveChangesAsync(cancellationToken);
        return new RecipeLibraryMutationResult(RecipeLibraryMutationStatus.Added, entryId);
    }

    public async Task<IReadOnlyList<TenantRecipeLibraryEntry>> ListTenantEntriesAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default) =>
        await database.Set<TenantRecipeEntryRecord>().AsNoTracking()
            .Where(value => value.TenantId == tenantId)
            .OrderBy(value => value.Id)
            .Select(value => new TenantRecipeLibraryEntry(
                value.Id, value.TenantId,
                value.CentralRecipeRevisionId.HasValue
                    ? RecipeLibraryEntryType.UpstreamRevision
                    : RecipeLibraryEntryType.LocalRecipe,
                value.CentralRecipeRevisionId ?? value.TenantRecipeId!.Value,
                value.UpdatedAtUtc))
            .ToArrayAsync(cancellationToken);

    public async Task<IReadOnlyList<CampRecipeLibraryEntry>> ListCampEntriesAsync(
        Guid campId,
        CancellationToken cancellationToken = default) =>
        await (from entry in database.Set<CampRecipeEntryRecord>().AsNoTracking()
            join revision in database.Set<RecipeRevisionRecord>().AsNoTracking()
                on entry.UpstreamRecipeRevisionId equals revision.Id into revisions
            from revision in revisions.DefaultIfEmpty()
            join recipe in database.Set<RecipeRecord>().AsNoTracking()
                on revision.RecipeId equals recipe.Id into recipes
            from recipe in recipes.DefaultIfEmpty()
            where entry.CampId == campId
            orderby entry.Id
            select new CampRecipeLibraryEntry(
                entry.Id, entry.CampId,
                entry.UpstreamRecipeRevisionId.HasValue
                    ? RecipeLibraryEntryType.UpstreamRevision
                    : RecipeLibraryEntryType.LocalRecipe,
                entry.UpstreamRecipeRevisionId ?? entry.CampRecipeId!.Value,
                entry.UpstreamRecipeRevisionId.HasValue ? (RecipeScopeType?)recipe.ScopeType : null,
                entry.UpdatedAtUtc)).ToArrayAsync(cancellationToken);

    private async Task<RecipeScopeType?> FindRevisionScopeAsync(
        Guid revisionId,
        CancellationToken cancellationToken) =>
        await (from revision in database.Set<RecipeRevisionRecord>().AsNoTracking()
            join recipe in database.Set<RecipeRecord>().AsNoTracking() on revision.RecipeId equals recipe.Id
            where revision.Id == revisionId
            select (RecipeScopeType?)recipe.ScopeType).SingleOrDefaultAsync(cancellationToken);
}
