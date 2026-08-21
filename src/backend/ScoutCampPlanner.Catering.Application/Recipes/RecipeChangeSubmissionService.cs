using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Application.Recipes;

public sealed record RecipeSubmissionCandidate(
    Guid LocalRevisionId,
    RecipeScopeType LocalScope,
    Guid LocalScopeId);

public sealed record CentralRecipeChangeComparison(
    Guid SubmissionId,
    Guid CentralRecipeId,
    Guid SourceCentralRevisionId,
    RecipeSnapshot SourceCentralRevision,
    Guid LatestCentralRevisionId,
    RecipeSnapshot LatestCentralRevision,
    Guid SubmittedLocalRevisionId,
    RecipeSnapshot SubmittedLocalRevision,
    Guid SubmittedBy,
    DateTimeOffset SubmittedAtUtc);

public enum RecipeSubmissionStatus
{
    Submitted,
    NotFound,
    InvalidSource,
    NoCentralLineage,
    AlreadySubmitted,
    Forbidden,
}

public sealed record RecipeSubmissionResult(RecipeSubmissionStatus Status, Guid? SubmissionId = null);

public interface IRecipeChangeSubmissionStore
{
    Task<RecipeSubmissionCandidate?> FindCandidateAsync(
        Guid localRevisionId,
        CancellationToken cancellationToken = default);
    Task<RecipeSubmissionResult> SubmitAsync(
        Guid submissionId,
        Guid localRevisionId,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CentralRecipeChangeComparison>> ListPendingAsync(
        CancellationToken cancellationToken = default);
}

public interface IRecipeChangeSubmissionAuthorization
{
    Task<bool> CanSubmitAsync(
        Guid actorUserId,
        RecipeScopeType scope,
        Guid scopeId,
        CancellationToken cancellationToken = default);
    Task<bool> CanReviewAsync(Guid actorUserId, CancellationToken cancellationToken = default);
}

public sealed class RecipeChangeSubmissionService(
    IRecipeChangeSubmissionStore store,
    IRecipeChangeSubmissionAuthorization authorization)
{
    public async Task<RecipeSubmissionResult> SubmitAsync(
        Guid localRevisionId,
        Guid actorUserId,
        DateTimeOffset timestampUtc,
        CancellationToken cancellationToken = default)
    {
        Required(localRevisionId, nameof(localRevisionId));
        Required(actorUserId, nameof(actorUserId));
        RecipeSubmissionCandidate? candidate = await store.FindCandidateAsync(localRevisionId, cancellationToken);
        if (candidate is null) return new RecipeSubmissionResult(RecipeSubmissionStatus.NotFound);
        if (!await authorization.CanSubmitAsync(
                actorUserId, candidate.LocalScope, candidate.LocalScopeId, cancellationToken))
            return new RecipeSubmissionResult(RecipeSubmissionStatus.Forbidden);
        return await store.SubmitAsync(
            Guid.NewGuid(), localRevisionId, actorUserId, timestampUtc, cancellationToken);
    }

    public async Task<IReadOnlyList<CentralRecipeChangeComparison>> ListPendingAsync(
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        Required(actorUserId, nameof(actorUserId));
        if (!await authorization.CanReviewAsync(actorUserId, cancellationToken))
            return [];
        return await store.ListPendingAsync(cancellationToken);
    }

    private static Guid Required(Guid value, string parameterName) =>
        value == Guid.Empty ? throw new ArgumentException("ID is required.", parameterName) : value;
}
