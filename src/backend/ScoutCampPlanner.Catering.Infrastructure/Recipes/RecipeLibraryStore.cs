using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Infrastructure.Recipes;

public sealed class RecipeLibraryStore(
    CateringDbContext database,
    RecipeDraftStore drafts) : IRecipeLibraryStore
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

    public async Task<RecipeLibraryMutationResult> ConvertTenantEntryToLocalRecipeAsync(
        Guid entryId,
        Guid newRecipeId,
        string newName,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        TenantRecipeEntryRecord? entry = await database.Set<TenantRecipeEntryRecord>()
            .SingleOrDefaultAsync(value => value.Id == entryId, cancellationToken);
        if (entry is null) return new RecipeLibraryMutationResult(RecipeLibraryMutationStatus.NotFound);
        if (!entry.CentralRecipeRevisionId.HasValue)
            return new RecipeLibraryMutationResult(RecipeLibraryMutationStatus.AlreadyLocal, entry.Id);

        RecipeRevisionSnapshot source = GetRevision(entry.CentralRecipeRevisionId.Value);
        RecipeDraft draft = RecipeDraftCopy.FromSnapshot(
            newRecipeId, RecipeScopeType.Tenant, entry.TenantId, RecipeStatus.Draft, source.Snapshot, newName);
        await drafts.CreateDerivedAsync(
            draft, new RecipeDraftLineage(source.RecipeId, entry.CentralRecipeRevisionId.Value, RecipeScopeType.Central),
            actorUserId, timestampUtc, cancellationToken);
        entry.CentralRecipeRevisionId = null;
        entry.TenantRecipeId = newRecipeId;
        entry.UpdatedBy = actorUserId;
        entry.UpdatedAtUtc = timestampUtc;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new RecipeLibraryMutationResult(RecipeLibraryMutationStatus.Converted, entry.Id);
    }

    public async Task<RecipeLibraryMutationResult> ConvertCampEntryToLocalRecipeAsync(
        Guid entryId,
        Guid newRecipeId,
        string newName,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        CampRecipeEntryRecord? entry = await database.Set<CampRecipeEntryRecord>()
            .SingleOrDefaultAsync(value => value.Id == entryId, cancellationToken);
        if (entry is null) return new RecipeLibraryMutationResult(RecipeLibraryMutationStatus.NotFound);
        if (!entry.UpstreamRecipeRevisionId.HasValue)
            return new RecipeLibraryMutationResult(RecipeLibraryMutationStatus.AlreadyLocal, entry.Id);

        Guid sourceRevisionId = entry.UpstreamRecipeRevisionId.Value;
        RecipeRevisionSnapshot source = GetRevision(sourceRevisionId);
        RecipeScopeType sourceScope = source.ScopeType ?? throw new InvalidOperationException("Source scope is missing.");
        RecipeDraft draft = RecipeDraftCopy.FromSnapshot(
            newRecipeId, RecipeScopeType.Camp, entry.CampId, RecipeStatus.Draft, source.Snapshot, newName);
        await drafts.CreateDerivedAsync(
            draft, new RecipeDraftLineage(source.RecipeId, sourceRevisionId, sourceScope),
            actorUserId, timestampUtc, cancellationToken);
        entry.UpstreamRecipeRevisionId = null;
        entry.CampRecipeId = newRecipeId;
        entry.UpdatedBy = actorUserId;
        entry.UpdatedAtUtc = timestampUtc;
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new RecipeLibraryMutationResult(RecipeLibraryMutationStatus.Converted, entry.Id);
    }

    private RecipeRevisionSnapshot GetRevision(Guid revisionId)
    {
        var source = (from revision in database.Set<RecipeRevisionRecord>().AsNoTracking()
            join recipe in database.Set<RecipeRecord>().AsNoTracking() on revision.RecipeId equals recipe.Id
            where revision.Id == revisionId
            select new { revision.RecipeId, revision.SnapshotJson, recipe.ScopeType }).Single();
        return new RecipeRevisionSnapshot(
            source.RecipeId, RecipeSnapshotBuilder.Deserialize(source.SnapshotJson),
            (RecipeScopeType)source.ScopeType);
    }
}
