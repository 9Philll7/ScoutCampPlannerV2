using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Application.Ingredients;

public enum IngredientCentralContributionStatus
{
    Pending,
    Accepted,
    Rejected,
}

public sealed record CentralIngredientCandidate(
    Guid IngredientId,
    Guid RevisionId,
    string Name,
    Guid CategoryId,
    Guid BaseUnitId);

public sealed record IngredientCentralContributionSummary(
    Guid Id,
    Guid SubmittedRevisionId,
    Guid LocalIngredientId,
    IngredientScopeType SourceScopeType,
    Guid SourceScopeId,
    string Name,
    Guid CategoryId,
    Guid BaseUnitId,
    Guid? SuggestedCentralIngredientId,
    string? SuggestedCentralIngredientName,
    DateTimeOffset SubmittedAtUtc,
    Guid SubmittedBy);

public enum IngredientContributionMutationStatus
{
    Created,
    Accepted,
    Rejected,
    Replaced,
    NotFound,
    NotPublished,
    AlreadySubmitted,
    AlreadyReviewed,
    DraftAlreadyExists,
    Invalid,
    Forbidden,
}

public sealed record IngredientContributionMutationResult(
    IngredientContributionMutationStatus Status,
    Guid? ContributionId = null,
    Guid? CentralIngredientId = null,
    Guid? CentralRevisionId = null);

public sealed record AcceptIngredientContributionRequest(Guid? TargetCentralIngredientId);

public sealed record ReplaceIngredientWithCentralRequest(Guid CentralRevisionId);

public interface IIngredientCentralContributionStore
{
    Task<IngredientRevisionScope?> GetActiveScopeAsync(Guid revisionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CentralIngredientCandidate>> FindCentralCandidatesAsync(
        Guid localRevisionId, CancellationToken cancellationToken = default);
    Task<IngredientContributionMutationResult> SubmitAsync(
        Guid contributionId, Guid localRevisionId, Guid actorUserId, DateTimeOffset submittedAtUtc,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IngredientCentralContributionSummary>> ListPendingAsync(
        CancellationToken cancellationToken = default);
    Task<IngredientContributionMutationResult> AcceptAsync(
        Guid contributionId, Guid? targetCentralIngredientId, Guid newCentralIngredientId,
        Guid newCentralRevisionId, Guid actorUserId, DateTimeOffset reviewedAtUtc,
        CancellationToken cancellationToken = default);
    Task<IngredientContributionMutationResult> RejectAsync(
        Guid contributionId, Guid actorUserId, DateTimeOffset reviewedAtUtc,
        CancellationToken cancellationToken = default);
    Task<IngredientContributionMutationResult> ReplaceAsync(
        Guid localRevisionId, Guid centralRevisionId, Guid actorUserId, DateTimeOffset replacedAtUtc,
        CancellationToken cancellationToken = default);
}

public sealed class IngredientCentralContributionService(
    IIngredientCentralContributionStore store,
    IIngredientManagementAuthorization authorization,
    TimeProvider timeProvider)
{
    public async Task<(bool IsAuthorized, IReadOnlyList<CentralIngredientCandidate> Candidates)> FindCandidatesAsync(
        Guid localRevisionId, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        Required(localRevisionId, nameof(localRevisionId));
        Required(actorUserId, nameof(actorUserId));
        IngredientRevisionScope? scope = await store.GetActiveScopeAsync(localRevisionId, cancellationToken);
        if (scope is null) return (true, []);
        if (!await CanManageLocalAsync(actorUserId, scope, cancellationToken)) return (false, []);
        return (true, await store.FindCentralCandidatesAsync(localRevisionId, cancellationToken));
    }

    public async Task<IngredientContributionMutationResult> SubmitAsync(
        Guid localRevisionId, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        Required(localRevisionId, nameof(localRevisionId));
        Required(actorUserId, nameof(actorUserId));
        IngredientRevisionScope? scope = await store.GetActiveScopeAsync(localRevisionId, cancellationToken);
        if (scope is null) return new(IngredientContributionMutationStatus.NotFound);
        if (!await CanManageLocalAsync(actorUserId, scope, cancellationToken))
            return new(IngredientContributionMutationStatus.Forbidden);
        return await store.SubmitAsync(Guid.NewGuid(), localRevisionId, actorUserId,
            timeProvider.GetUtcNow(), cancellationToken);
    }

    public async Task<(bool IsAuthorized, IReadOnlyList<IngredientCentralContributionSummary> Contributions)>
        ListPendingAsync(Guid actorUserId, CancellationToken cancellationToken = default)
    {
        Required(actorUserId, nameof(actorUserId));
        if (!await authorization.CanManageCentralAsync(actorUserId, cancellationToken)) return (false, []);
        return (true, await store.ListPendingAsync(cancellationToken));
    }

    public async Task<IngredientContributionMutationResult> AcceptAsync(
        Guid contributionId, Guid? targetCentralIngredientId, Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        Required(contributionId, nameof(contributionId));
        Required(actorUserId, nameof(actorUserId));
        if (!await authorization.CanManageCentralAsync(actorUserId, cancellationToken))
            return new(IngredientContributionMutationStatus.Forbidden);
        return await store.AcceptAsync(contributionId, targetCentralIngredientId, Guid.NewGuid(), Guid.NewGuid(),
            actorUserId, timeProvider.GetUtcNow(), cancellationToken);
    }

    public async Task<IngredientContributionMutationResult> RejectAsync(
        Guid contributionId, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        Required(contributionId, nameof(contributionId));
        Required(actorUserId, nameof(actorUserId));
        if (!await authorization.CanManageCentralAsync(actorUserId, cancellationToken))
            return new(IngredientContributionMutationStatus.Forbidden);
        return await store.RejectAsync(contributionId, actorUserId, timeProvider.GetUtcNow(), cancellationToken);
    }

    public async Task<IngredientContributionMutationResult> ReplaceAsync(
        Guid localRevisionId, Guid centralRevisionId, Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        Required(localRevisionId, nameof(localRevisionId));
        Required(centralRevisionId, nameof(centralRevisionId));
        Required(actorUserId, nameof(actorUserId));
        IngredientRevisionScope? scope = await store.GetActiveScopeAsync(localRevisionId, cancellationToken);
        if (scope is null) return new(IngredientContributionMutationStatus.NotFound);
        if (!await CanManageLocalAsync(actorUserId, scope, cancellationToken))
            return new(IngredientContributionMutationStatus.Forbidden);
        return await store.ReplaceAsync(localRevisionId, centralRevisionId, actorUserId,
            timeProvider.GetUtcNow(), cancellationToken);
    }

    private Task<bool> CanManageLocalAsync(
        Guid actorUserId, IngredientRevisionScope scope, CancellationToken cancellationToken) =>
        scope.ScopeType switch
        {
            IngredientScopeType.Tenant when scope.ScopeId.HasValue =>
                authorization.CanManageTenantAsync(actorUserId, scope.ScopeId.Value, cancellationToken),
            IngredientScopeType.Camp when scope.ScopeId.HasValue =>
                authorization.CanManageCampAsync(actorUserId, scope.ScopeId.Value, cancellationToken),
            _ => Task.FromResult(false),
        };

    private static Guid Required(Guid value, string parameterName) =>
        value == Guid.Empty ? throw new ArgumentException("ID is required.", parameterName) : value;
}
