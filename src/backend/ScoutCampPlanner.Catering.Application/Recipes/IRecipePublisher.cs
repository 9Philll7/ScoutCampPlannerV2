using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Application.Recipes;

public enum RecipePublicationStatus
{
    Published,
    NotFound,
    VersionConflict,
    ValidationFailed,
    WarningAcknowledgementRequired,
    Archived,
}

public sealed record RecipePublicationResult(
    RecipePublicationStatus Status,
    RecipeValidationResult? Validation,
    RecipeRevision? Revision,
    RecipeDraft? CurrentDraft);

public interface IRecipePublisher
{
    Task<RecipePublicationResult> PublishAsync(
        Guid recipeId,
        long expectedVersion,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        bool acknowledgeWarnings,
        RecipeValidationContext? validationContext = null,
        string? changeNote = null,
        CancellationToken cancellationToken = default);
}
