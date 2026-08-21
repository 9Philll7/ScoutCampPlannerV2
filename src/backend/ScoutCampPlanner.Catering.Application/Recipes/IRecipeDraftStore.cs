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
}
