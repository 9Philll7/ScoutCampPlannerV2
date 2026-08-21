using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Application.Recipes;

public enum RecipeLibraryEntryType
{
    UpstreamRevision,
    LocalRecipe,
}

public sealed record TenantRecipeLibraryEntry(
    Guid Id,
    Guid TenantId,
    RecipeLibraryEntryType Type,
    Guid SourceId,
    DateTimeOffset UpdatedAtUtc);

public sealed record CampRecipeLibraryEntry(
    Guid Id,
    Guid CampId,
    RecipeLibraryEntryType Type,
    Guid SourceId,
    RecipeScopeType? UpstreamScope,
    DateTimeOffset UpdatedAtUtc);

public enum RecipeLibraryMutationStatus
{
    Added,
    NotFound,
    InvalidSourceScope,
    AlreadyExists,
    Converted,
    AlreadyLocal,
}

public sealed record RecipeLibraryMutationResult(
    RecipeLibraryMutationStatus Status,
    Guid? EntryId = null);

public interface IRecipeLibraryStore
{
    Task<RecipeLibraryMutationResult> AddCentralRevisionToTenantAsync(
        Guid entryId,
        Guid tenantId,
        Guid revisionId,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default);
    Task<RecipeLibraryMutationResult> AddUpstreamRevisionToCampAsync(
        Guid entryId,
        Guid campId,
        Guid revisionId,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TenantRecipeLibraryEntry>> ListTenantEntriesAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CampRecipeLibraryEntry>> ListCampEntriesAsync(
        Guid campId,
        CancellationToken cancellationToken = default);
    Task<RecipeLibraryMutationResult> ConvertTenantEntryToLocalRecipeAsync(
        Guid entryId,
        Guid newRecipeId,
        string newName,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default);
    Task<RecipeLibraryMutationResult> ConvertCampEntryToLocalRecipeAsync(
        Guid entryId,
        Guid newRecipeId,
        string newName,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default);
}

public sealed class RecipeLibraryService(IRecipeLibraryStore store)
{
    public Task<RecipeLibraryMutationResult> AddCentralRevisionToTenantAsync(
        Guid tenantId,
        Guid revisionId,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default) =>
        store.AddCentralRevisionToTenantAsync(
            Guid.NewGuid(), Required(tenantId, nameof(tenantId)), Required(revisionId, nameof(revisionId)),
            Required(actorUserId, nameof(actorUserId)), timestampUtc, cancellationToken);

    public Task<RecipeLibraryMutationResult> AddUpstreamRevisionToCampAsync(
        Guid campId,
        Guid revisionId,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default) =>
        store.AddUpstreamRevisionToCampAsync(
            Guid.NewGuid(), Required(campId, nameof(campId)), Required(revisionId, nameof(revisionId)),
            Required(actorUserId, nameof(actorUserId)), timestampUtc, cancellationToken);

    public Task<IReadOnlyList<TenantRecipeLibraryEntry>> ListTenantEntriesAsync(
        Guid tenantId,
        CancellationToken cancellationToken = default) =>
        store.ListTenantEntriesAsync(Required(tenantId, nameof(tenantId)), cancellationToken);

    public Task<IReadOnlyList<CampRecipeLibraryEntry>> ListCampEntriesAsync(
        Guid campId,
        CancellationToken cancellationToken = default) =>
        store.ListCampEntriesAsync(Required(campId, nameof(campId)), cancellationToken);

    public Task<RecipeLibraryMutationResult> ConvertTenantEntryToLocalRecipeAsync(
        Guid entryId,
        Guid newRecipeId,
        string newName,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default) =>
        store.ConvertTenantEntryToLocalRecipeAsync(
            Required(entryId, nameof(entryId)), Required(newRecipeId, nameof(newRecipeId)), newName,
            Required(actorUserId, nameof(actorUserId)), timestampUtc, cancellationToken);

    public Task<RecipeLibraryMutationResult> ConvertCampEntryToLocalRecipeAsync(
        Guid entryId,
        Guid newRecipeId,
        string newName,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default) =>
        store.ConvertCampEntryToLocalRecipeAsync(
            Required(entryId, nameof(entryId)), Required(newRecipeId, nameof(newRecipeId)), newName,
            Required(actorUserId, nameof(actorUserId)), timestampUtc, cancellationToken);

    private static Guid Required(Guid value, string parameterName) =>
        value == Guid.Empty ? throw new ArgumentException("ID is required.", parameterName) : value;
}
