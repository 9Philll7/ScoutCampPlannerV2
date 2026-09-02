using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Application.Ingredients;

public sealed record CreateIngredientRevisionDraftRequest(
    string Name,
    Guid CategoryId,
    Guid BaseUnitId);

public sealed record SaveIngredientRevisionDraftRequest(
    string Name,
    Guid CategoryId,
    Guid BaseUnitId,
    IngredientPropertyReviewState AllergenReviewState,
    IngredientPropertyReviewState IntoleranceReviewState,
    IngredientPropertyReviewState OriginReviewState,
    long ExpectedRowVersion,
    IReadOnlyList<IngredientRevisionPropertyItem>? Allergens = null,
    IReadOnlyList<IngredientRevisionPropertyItem>? Intolerances = null,
    IReadOnlyList<IngredientRevisionPropertyItem>? Origins = null,
    IReadOnlyList<IngredientRevisionUnitConversionItem>? UnitConversions = null,
    IReadOnlyList<IngredientVariantDraftItem>? Variants = null);

public sealed record PublishIngredientRevisionRequest(long ExpectedRowVersion);

public sealed record IngredientRevisionPropertyItem(
    Guid PropertyId,
    IngredientPropertyState State,
    IngredientPropertySource Source);

public sealed record IngredientRevisionUnitConversionItem(
    Guid SourceUnitId,
    decimal FactorToBaseUnit,
    IngredientConversionPrecision Precision);

public sealed record IngredientVariantDraftItem(
    Guid Id,
    string VariantKey,
    string Name,
    bool IsActive,
    int SortOrder);

public sealed record IngredientVariantRevisionItem(
    Guid Id,
    string VariantKey,
    string Name,
    bool IsActive,
    int SortOrder,
    IReadOnlyList<IngredientRevisionPropertyItem> AllergenOverrides,
    IReadOnlyList<IngredientRevisionPropertyItem> IntoleranceOverrides,
    IReadOnlyList<IngredientRevisionPropertyItem> OriginOverrides,
    IReadOnlyList<IngredientRevisionUnitConversionItem> UnitConversionOverrides);

public sealed record IngredientRevisionDraftDetails(
    Guid Id,
    Guid IngredientId,
    IngredientScopeType ScopeType,
    Guid? ScopeId,
    int RevisionNumber,
    IngredientRevisionState State,
    Guid? BasedOnRevisionId,
    string Name,
    Guid CategoryId,
    Guid BaseUnitId,
    IngredientPropertyReviewState AllergenReviewState,
    IngredientPropertyReviewState IntoleranceReviewState,
    IngredientPropertyReviewState OriginReviewState,
    long RowVersion,
    IReadOnlyList<IngredientRevisionPropertyItem> Allergens,
    IReadOnlyList<IngredientRevisionPropertyItem> Intolerances,
    IReadOnlyList<IngredientRevisionPropertyItem> Origins,
    IReadOnlyList<IngredientRevisionUnitConversionItem> UnitConversions,
    IReadOnlyList<IngredientVariantRevisionItem> Variants);

public enum IngredientRevisionQueryStatus
{
    Found,
    NotFound,
    Forbidden,
}

public sealed record IngredientRevisionQueryResult(
    IngredientRevisionQueryStatus Status,
    IngredientRevisionDraftDetails? Revision = null);

public sealed record IngredientRevisionSummary(
    Guid IngredientId,
    Guid RevisionId,
    string Name,
    IngredientRevisionState State,
    long RowVersion);

public sealed record IngredientRevisionListResult(
    bool IsAuthorized,
    IReadOnlyList<IngredientRevisionSummary> Revisions);

public enum IngredientRevisionMutationStatus
{
    Created,
    Saved,
    Published,
    NotFound,
    NotDraft,
    ConcurrencyConflict,
    DraftAlreadyExists,
    Forbidden,
    Invalid,
}

public sealed record IngredientRevisionMutationResult(
    IngredientRevisionMutationStatus Status,
    long? RowVersion = null,
    Guid? IngredientId = null,
    Guid? RevisionId = null);

public sealed record IngredientRevisionScope(IngredientScopeType ScopeType, Guid? ScopeId);

public interface IIngredientRevisionWorkflowStore
{
    Task<IngredientRevisionScope?> GetScopeAsync(
        Guid revisionId,
        CancellationToken cancellationToken = default);

    Task<IngredientRevisionDraftDetails?> GetAsync(
        Guid revisionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<IngredientRevisionSummary>> ListAsync(
        IngredientRevisionScope scope,
        CancellationToken cancellationToken = default);

    Task<IngredientRevisionMutationResult> CreateDraftAsync(
        Guid ingredientId,
        Guid revisionId,
        IngredientRevisionScope scope,
        IngredientRevisionDraftContent content,
        Guid actorUserId,
        DateTimeOffset createdAtUtc,
        CancellationToken cancellationToken = default);

    Task<IngredientRevisionMutationResult> CreateDraftFromPublishedAsync(
        Guid publishedRevisionId,
        Guid newRevisionId,
        Guid actorUserId,
        DateTimeOffset createdAtUtc,
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
    public Task<IngredientRevisionMutationResult> CreateCentralDraftAsync(
        CreateIngredientRevisionDraftRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default) =>
        CreateDraftAsync(
            new IngredientRevisionScope(IngredientScopeType.Central, null),
            request,
            actorUserId,
            cancellationToken);

    public Task<IngredientRevisionMutationResult> CreateTenantDraftAsync(
        Guid tenantId,
        CreateIngredientRevisionDraftRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        Required(tenantId, nameof(tenantId));
        return CreateDraftAsync(
            new IngredientRevisionScope(IngredientScopeType.Tenant, tenantId),
            request,
            actorUserId,
            cancellationToken);
    }

    public Task<IngredientRevisionMutationResult> CreateCampDraftAsync(
        Guid campId,
        CreateIngredientRevisionDraftRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        Required(campId, nameof(campId));
        return CreateDraftAsync(
            new IngredientRevisionScope(IngredientScopeType.Camp, campId),
            request,
            actorUserId,
            cancellationToken);
    }

    public async Task<IngredientRevisionQueryResult> GetAsync(
        Guid revisionId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        Required(revisionId, nameof(revisionId));
        Required(actorUserId, nameof(actorUserId));
        IngredientRevisionScope? scope = await store.GetScopeAsync(revisionId, cancellationToken);
        if (scope is null)
            return new(IngredientRevisionQueryStatus.NotFound);
        if (!await IsAuthorizedAsync(actorUserId, scope, cancellationToken))
            return new(IngredientRevisionQueryStatus.Forbidden);

        IngredientRevisionDraftDetails? revision = await store.GetAsync(revisionId, cancellationToken);
        return revision is null
            ? new(IngredientRevisionQueryStatus.NotFound)
            : new(IngredientRevisionQueryStatus.Found, revision);
    }

    public async Task<IngredientRevisionListResult> ListCampAsync(
        Guid campId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        Required(campId, nameof(campId));
        Required(actorUserId, nameof(actorUserId));
        var scope = new IngredientRevisionScope(IngredientScopeType.Camp, campId);
        if (!await IsAuthorizedAsync(actorUserId, scope, cancellationToken))
            return new(false, []);
        return new(true, await store.ListAsync(scope, cancellationToken));
    }

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
                request.OriginReviewState,
                ToPropertyValues(request.Allergens),
                ToPropertyValues(request.Intolerances),
                ToPropertyValues(request.Origins),
                ToUnitConversions(request.UnitConversions),
                ToVariants(request.Variants));
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

    public async Task<IngredientRevisionMutationResult> CreateDraftFromPublishedAsync(
        Guid publishedRevisionId,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        Required(publishedRevisionId, nameof(publishedRevisionId));
        Required(actorUserId, nameof(actorUserId));
        IngredientRevisionScope? scope = await store.GetScopeAsync(publishedRevisionId, cancellationToken);
        if (scope is null)
            return new(IngredientRevisionMutationStatus.NotFound);
        if (!await IsAuthorizedAsync(actorUserId, scope, cancellationToken))
            return new(IngredientRevisionMutationStatus.Forbidden);

        return await store.CreateDraftFromPublishedAsync(
            publishedRevisionId,
            Guid.NewGuid(),
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

    private async Task<IngredientRevisionMutationResult> CreateDraftAsync(
        IngredientRevisionScope scope,
        CreateIngredientRevisionDraftRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken)
    {
        Required(actorUserId, nameof(actorUserId));
        ArgumentNullException.ThrowIfNull(request);
        if (!await IsAuthorizedAsync(actorUserId, scope, cancellationToken))
            return new(IngredientRevisionMutationStatus.Forbidden);

        IngredientRevisionDraftContent content;
        try
        {
            content = IngredientRevisionDraftContent.Create(
                request.Name,
                request.CategoryId,
                request.BaseUnitId,
                IngredientPropertyReviewState.Unreviewed,
                IngredientPropertyReviewState.Unreviewed,
                IngredientPropertyReviewState.Unreviewed);
        }
        catch (ArgumentException)
        {
            return new(IngredientRevisionMutationStatus.Invalid);
        }

        return await store.CreateDraftAsync(
            Guid.NewGuid(),
            Guid.NewGuid(),
            scope,
            content,
            actorUserId,
            timeProvider.GetUtcNow(),
            cancellationToken);
    }

    private static Guid Required(Guid value, string parameterName) =>
        value == Guid.Empty ? throw new ArgumentException("ID is required.", parameterName) : value;

    private static IEnumerable<IngredientPropertyValue> ToPropertyValues(
        IReadOnlyList<IngredientRevisionPropertyItem>? values) =>
        values?.Select(value => new IngredientPropertyValue(value.PropertyId, value.State, value.Source)) ?? [];

    private static IEnumerable<IngredientRevisionUnitConversion> ToUnitConversions(
        IReadOnlyList<IngredientRevisionUnitConversionItem>? values) =>
        values?.Select(value => new IngredientRevisionUnitConversion(
            value.SourceUnitId,
            value.FactorToBaseUnit,
            value.Precision)) ?? [];

    private static IEnumerable<IngredientVariantDraftContent>? ToVariants(
        IReadOnlyList<IngredientVariantDraftItem>? values) =>
        values?.Select(value => new IngredientVariantDraftContent(
            value.Id,
            value.VariantKey,
            value.Name,
            value.IsActive,
            value.SortOrder));
}
