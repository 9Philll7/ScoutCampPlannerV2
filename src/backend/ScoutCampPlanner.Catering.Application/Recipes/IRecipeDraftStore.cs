using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Application.Recipes;

public enum RecipeDraftSaveStatus
{
    Saved,
    NotFound,
    VersionConflict,
}

public sealed record RecipeDraftSaveResult(RecipeDraftSaveStatus Status, RecipeDraft? CurrentDraft);

public sealed record RecipeDraftLineage(Guid SourceRecipeId, Guid SourceRevisionId);

public enum RecipeLifecycleStatus
{
    Changed,
    NotFound,
    VersionConflict,
    InvalidStatus,
    ReferenceBlocked,
}

public sealed record RecipeLifecycleResult(RecipeLifecycleStatus Status, RecipeDraft? CurrentDraft);

public interface IRecipeDraftStore
{
    Task<RecipeDraft?> FindAsync(Guid recipeId, CancellationToken cancellationToken = default);
    Task<RecipeDraft> CreateAsync(
        RecipeDraft draft,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default);
    Task<RecipeDraft> CreateDerivedAsync(
        RecipeDraft draft,
        RecipeDraftLineage lineage,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default);
    Task<RecipeDraftSaveResult> SaveAsync(
        RecipeDraft draft,
        long expectedVersion,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default);
    Task<RecipeLifecycleResult> ArchiveAsync(
        Guid recipeId,
        long expectedVersion,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default);
    Task<RecipeLifecycleResult> ReactivateAsync(
        Guid recipeId,
        long expectedVersion,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default);
    Task<RecipeLifecycleResult> ResetToDraftAsync(
        Guid recipeId,
        long expectedVersion,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default);
}
