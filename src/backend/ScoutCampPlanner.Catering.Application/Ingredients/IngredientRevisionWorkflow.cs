using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Application.Ingredients;

public sealed record SaveIngredientRevisionDraftRequest(
    string Name,
    Guid CategoryId,
    Guid BaseUnitId,
    IngredientPropertyReviewState AllergenReviewState,
    IngredientPropertyReviewState IntoleranceReviewState,
    IngredientPropertyReviewState OriginReviewState,
    long ExpectedRowVersion);

public enum IngredientRevisionMutationStatus
{
    Saved,
    Published,
    NotFound,
    NotDraft,
    ConcurrencyConflict,
    Forbidden,
    Invalid,
}

public sealed record IngredientRevisionMutationResult(
    IngredientRevisionMutationStatus Status,
    long? RowVersion = null);

public sealed record IngredientRevisionScope(IngredientScopeType ScopeType, Guid? ScopeId);

public interface IIngredientRevisionWorkflowStore
{
    Task<IngredientRevisionScope?> GetScopeAsync(
        Guid revisionId,
        CancellationToken cancellationToken = default);

    Task<IngredientRevisionMutationResult> SaveDraftAsync(
        Guid revisionId,
        IngredientRevisionDraftContent content,
        long expectedRowVersion,
        Guid actorUserId,
        DateTimeOffset changedAtUtc,
        CancellationToken cancellationToken = default);

    Task<IngredientRevisionMutationResult> PublishAsync(
        Guid revisionId,
        long expectedRowVersion,
        Guid actorUserId,
        DateTimeOffset publishedAtUtc,
        CancellationToken cancellationToken = default);
}

public sealed class IngredientRevisionWorkflowService(
    IIngredientRevisionWorkflowStore store,
    IIngredientManagementAuthorization authorization,
    TimeProvider timeProvider)
{
    public async Task<IngredientRevisionMutationResult> SaveDraftAsync(
        Guid revisionId,
        SaveIngredientRevisionDraftRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        Required(revisionId, nameof(revisionId));
        Required(actorUserId, nameof(actorUserId));
        ArgumentNullException.ThrowIfNull(request);
        if (request.ExpectedRowVersion <= 0)
            return new(IngredientRevisionMutationStatus.Invalid);

        IngredientRevisionDraftContent content;
        try
        {
            content = IngredientRevisionDraftContent.Create(
                request.Name,
                request.CategoryId,
                request.BaseUnitId,
                request.AllergenReviewState,
                request.IntoleranceReviewState,
                request.OriginReviewState);
        }
        catch (ArgumentException)
        {
            return new(IngredientRevisionMutationStatus.Invalid);
        }

        IngredientRevisionScope? scope = await store.GetScopeAsync(revisionId, cancellationToken);
        if (scope is null)
            return new(IngredientRevisionMutationStatus.NotFound);
        if (!await IsAuthorizedAsync(actorUserId, scope, cancellationToken))
            return new(IngredientRevisionMutationStatus.Forbidden);

        return await store.SaveDraftAsync(
            revisionId,
            content,
            request.ExpectedRowVersion,
            actorUserId,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    public async Task<IngredientRevisionMutationResult> PublishAsync(
        Guid revisionId,
        long expectedRowVersion,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        Required(revisionId, nameof(revisionId));
        Required(actorUserId, nameof(actorUserId));
        if (expectedRowVersion <= 0)
            return new(IngredientRevisionMutationStatus.Invalid);

        IngredientRevisionScope? scope = await store.GetScopeAsync(revisionId, cancellationToken);
        if (scope is null)
            return new(IngredientRevisionMutationStatus.NotFound);
        if (!await IsAuthorizedAsync(actorUserId, scope, cancellationToken))
            return new(IngredientRevisionMutationStatus.Forbidden);

        return await store.PublishAsync(
            revisionId,
            expectedRowVersion,
            actorUserId,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    private Task<bool> IsAuthorizedAsync(
        Guid actorUserId,
        IngredientRevisionScope scope,
        CancellationToken cancellationToken) => scope.ScopeType switch
        {
            IngredientScopeType.Central => authorization.CanManageCentralAsync(actorUserId, cancellationToken),
            IngredientScopeType.Tenant when scope.ScopeId.HasValue =>
                authorization.CanManageTenantAsync(actorUserId, scope.ScopeId.Value, cancellationToken),
            IngredientScopeType.Camp when scope.ScopeId.HasValue =>
                authorization.CanManageCampAsync(actorUserId, scope.ScopeId.Value, cancellationToken),
            _ => Task.FromResult(false),
        };

    private static Guid Required(Guid value, string parameterName) =>
        value == Guid.Empty ? throw new ArgumentException("ID is required.", parameterName) : value;
}
