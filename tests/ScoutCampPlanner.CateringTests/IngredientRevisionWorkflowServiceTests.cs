using ScoutCampPlanner.Catering.Application.Ingredients;
using ScoutCampPlanner.Catering.Domain;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class IngredientRevisionWorkflowServiceTests
{
    [Fact]
    public async Task Create_central_draft_starts_with_unreviewed_property_groups()
    {
        var store = new FakeStore(null);
        var service = new IngredientRevisionWorkflowService(
            store,
            new FakeAuthorization { CentralAllowed = true },
            TimeProvider.System);

        IngredientRevisionMutationResult result = await service.CreateCentralDraftAsync(
            new CreateIngredientRevisionDraftRequest("Haferflocken", Guid.NewGuid(), Guid.NewGuid()),
            Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        Assert.Equal(IngredientRevisionMutationStatus.Created, result.Status);
        Assert.Equal(IngredientScopeType.Central, store.CreatedScope!.ScopeType);
        Assert.Equal(IngredientPropertyReviewState.Unreviewed, store.CreatedContent!.AllergenReviewState);
        Assert.Equal(IngredientPropertyReviewState.Unreviewed, store.CreatedContent.IntoleranceReviewState);
        Assert.Equal(IngredientPropertyReviewState.Unreviewed, store.CreatedContent.OriginReviewState);
    }

    [Fact]
    public async Task Save_draft_normalizes_content_and_uses_scope_authorization()
    {
        Guid tenantId = Guid.NewGuid();
        var store = new FakeStore(new IngredientRevisionScope(IngredientScopeType.Tenant, tenantId));
        var authorization = new FakeAuthorization { TenantAllowed = true };
        var service = new IngredientRevisionWorkflowService(
            store,
            authorization,
            new FixedTimeProvider(new DateTimeOffset(2026, 8, 30, 12, 0, 0, TimeSpan.Zero)));

        IngredientRevisionMutationResult result = await service.SaveDraftAsync(
            Guid.NewGuid(),
            new SaveIngredientRevisionDraftRequest(
                "  Rote   Linsen ",
                Guid.NewGuid(),
                Guid.NewGuid(),
                IngredientPropertyReviewState.Reviewed,
                IngredientPropertyReviewState.Unreviewed,
                IngredientPropertyReviewState.Reviewed,
                4),
            Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        Assert.Equal(IngredientRevisionMutationStatus.Saved, result.Status);
        Assert.Equal(tenantId, authorization.RequestedTenantId);
        Assert.Equal("Rote Linsen", store.SavedContent!.Name);
        Assert.Equal("ROTE LINSEN", store.SavedContent.NormalizedName);
        Assert.Equal(4, store.ExpectedRowVersion);
    }

    [Fact]
    public async Task Forbidden_actor_cannot_publish_revision()
    {
        var store = new FakeStore(new IngredientRevisionScope(IngredientScopeType.Central, null));
        var service = new IngredientRevisionWorkflowService(
            store,
            new FakeAuthorization(),
            TimeProvider.System);

        IngredientRevisionMutationResult result = await service.PublishAsync(
            Guid.NewGuid(), 1, Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Equal(IngredientRevisionMutationStatus.Forbidden, result.Status);
        Assert.False(store.PublishCalled);
    }

    [Fact]
    public async Task Invalid_draft_is_rejected_before_store_mutation()
    {
        var store = new FakeStore(new IngredientRevisionScope(IngredientScopeType.Central, null));
        var service = new IngredientRevisionWorkflowService(
            store,
            new FakeAuthorization { CentralAllowed = true },
            TimeProvider.System);

        IngredientRevisionMutationResult result = await service.SaveDraftAsync(
            Guid.NewGuid(),
            new SaveIngredientRevisionDraftRequest(
                " ", Guid.NewGuid(), Guid.NewGuid(),
                IngredientPropertyReviewState.Reviewed,
                IngredientPropertyReviewState.Reviewed,
                IngredientPropertyReviewState.Reviewed,
                1),
            Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        Assert.Equal(IngredientRevisionMutationStatus.Invalid, result.Status);
        Assert.Null(store.SavedContent);
    }

    private sealed class FakeStore(IngredientRevisionScope? scope) : IIngredientRevisionWorkflowStore
    {
        public IngredientRevisionDraftContent? SavedContent { get; private set; }
        public long ExpectedRowVersion { get; private set; }
        public bool PublishCalled { get; private set; }
        public IngredientRevisionScope? CreatedScope { get; private set; }
        public IngredientRevisionDraftContent? CreatedContent { get; private set; }

        public Task<IngredientRevisionScope?> GetScopeAsync(Guid revisionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(scope);

        public Task<IngredientRevisionDraftDetails?> GetAsync(
            Guid revisionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IngredientRevisionDraftDetails?>(null);

        public Task<IReadOnlyList<IngredientRevisionSummary>> ListAsync(
            IngredientRevisionScope revisionScope,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<IngredientRevisionSummary>>([]);

        public Task<IngredientRevisionMutationResult> CreateDraftAsync(
            Guid ingredientId,
            Guid revisionId,
            IngredientRevisionScope revisionScope,
            IngredientRevisionDraftContent content,
            Guid actorUserId,
            DateTimeOffset createdAtUtc,
            CancellationToken cancellationToken = default)
        {
            CreatedScope = revisionScope;
            CreatedContent = content;
            return Task.FromResult(new IngredientRevisionMutationResult(
                IngredientRevisionMutationStatus.Created, 1, ingredientId, revisionId));
        }

        public Task<IngredientRevisionMutationResult> SaveDraftAsync(
            Guid revisionId,
            IngredientRevisionDraftContent content,
            long expectedRowVersion,
            Guid actorUserId,
            DateTimeOffset changedAtUtc,
            CancellationToken cancellationToken = default)
        {
            SavedContent = content;
            ExpectedRowVersion = expectedRowVersion;
            return Task.FromResult(new IngredientRevisionMutationResult(
                IngredientRevisionMutationStatus.Saved,
                expectedRowVersion + 1));
        }

        public Task<IngredientRevisionMutationResult> PublishAsync(
            Guid revisionId,
            long expectedRowVersion,
            Guid actorUserId,
            DateTimeOffset publishedAtUtc,
            CancellationToken cancellationToken = default)
        {
            PublishCalled = true;
            return Task.FromResult(new IngredientRevisionMutationResult(
                IngredientRevisionMutationStatus.Published,
                expectedRowVersion + 1));
        }
    }

    private sealed class FakeAuthorization : IIngredientManagementAuthorization
    {
        public bool CentralAllowed { get; init; }
        public bool TenantAllowed { get; init; }
        public Guid? RequestedTenantId { get; private set; }

        public Task<bool> CanManageCentralAsync(Guid actorUserId, CancellationToken cancellationToken = default) =>
            Task.FromResult(CentralAllowed);

        public Task<bool> CanManageTenantAsync(
            Guid actorUserId, Guid tenantId, CancellationToken cancellationToken = default)
        {
            RequestedTenantId = tenantId;
            return Task.FromResult(TenantAllowed);
        }

        public Task<bool> CanManageCampAsync(
            Guid actorUserId, Guid campId, CancellationToken cancellationToken = default) =>
            Task.FromResult(false);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
