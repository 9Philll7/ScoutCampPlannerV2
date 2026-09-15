using ScoutCampPlanner.Catering.Application.Ingredients;
using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Application.Recipes;

public enum CampRecipePublicationStatus
{
    Published,
    NotFound,
    Forbidden,
    VersionConflict,
    ValidationFailed,
    WarningAcknowledgementRequired,
    Archived,
}

public sealed record CampRecipePublicationResult(
    CampRecipePublicationStatus Status,
    RecipeValidationResult? Validation = null,
    Guid? RevisionId = null,
    int? RevisionNumber = null,
    long? DraftVersion = null);

public sealed class CampRecipePublicationService(
    IRecipeEditorStore drafts,
    IRecipeEditorAuthorization authorization,
    ICampTenantResolver camps,
    IRecipePublisher publisher,
    TimeProvider timeProvider)
{
    public async Task<CampRecipePublicationResult> PublishAsync(
        Guid campId,
        Guid recipeId,
        long expectedVersion,
        bool acknowledgeWarnings,
        Guid actorUserId,
        string? changeNote = null,
        CancellationToken cancellationToken = default)
    {
        Required(campId, nameof(campId));
        Required(recipeId, nameof(recipeId));
        Required(actorUserId, nameof(actorUserId));
        if (expectedVersion < 0) throw new ArgumentOutOfRangeException(nameof(expectedVersion));

        if (!await authorization.CanPublishCampAsync(actorUserId, campId, cancellationToken))
            return new(CampRecipePublicationStatus.Forbidden);

        RecipeDraft? draft = await drafts.FindAsync(recipeId, cancellationToken);
        if (draft is null || draft.ScopeType != RecipeScopeType.Camp || draft.ScopeId != campId)
            return new(CampRecipePublicationStatus.NotFound);

        Guid? tenantId = await camps.FindTenantIdAsync(campId, cancellationToken);
        if (!tenantId.HasValue)
            return new(CampRecipePublicationStatus.NotFound);

        RecipePublicationResult result = await publisher.PublishAsync(
            recipeId,
            expectedVersion,
            actorUserId,
            timeProvider.GetUtcNow(),
            acknowledgeWarnings,
            new RecipeValidationContext(tenantId),
            changeNote,
            cancellationToken);

        return new CampRecipePublicationResult(
            Map(result.Status),
            result.Validation,
            result.Revision?.Id,
            result.Revision?.RevisionNumber,
            result.CurrentDraft?.DraftVersion);
    }

    private static CampRecipePublicationStatus Map(RecipePublicationStatus status) => status switch
    {
        RecipePublicationStatus.Published => CampRecipePublicationStatus.Published,
        RecipePublicationStatus.NotFound => CampRecipePublicationStatus.NotFound,
        RecipePublicationStatus.VersionConflict => CampRecipePublicationStatus.VersionConflict,
        RecipePublicationStatus.ValidationFailed => CampRecipePublicationStatus.ValidationFailed,
        RecipePublicationStatus.WarningAcknowledgementRequired =>
            CampRecipePublicationStatus.WarningAcknowledgementRequired,
        RecipePublicationStatus.Archived => CampRecipePublicationStatus.Archived,
        _ => throw new ArgumentOutOfRangeException(nameof(status)),
    };

    private static Guid Required(Guid value, string parameterName) =>
        value == Guid.Empty ? throw new ArgumentException("ID is required.", parameterName) : value;
}
