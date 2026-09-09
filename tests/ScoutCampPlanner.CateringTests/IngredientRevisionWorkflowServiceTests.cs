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
        Guid allergenId = Guid.NewGuid();
        Guid sourceUnitId = Guid.NewGuid();
        Guid variantId = Guid.NewGuid();

        IngredientRevisionMutationResult result = await service.SaveDraftAsync(
            Guid.NewGuid(),
            new SaveIngredientRevisionDraftRequest(
                "  Rote   Linsen ",
                Guid.NewGuid(),
                Guid.NewGuid(),
                IngredientPropertyReviewState.Reviewed,
                IngredientPropertyReviewState.Unreviewed,
                IngredientPropertyReviewState.Reviewed,
                4,
                [new IngredientRevisionPropertyItem(
                    allergenId,
                    IngredientPropertyState.Contains,
                    IngredientPropertySource.ManuallyVerified)],
                UnitConversions: [new IngredientRevisionUnitConversionItem(
                    sourceUnitId,
                    12m,
                    IngredientConversionPrecision.Average)],
                Variants: [new IngredientVariantDraftItem(
                    variantId, " SMOKED ", "  Geräuchert ", true, 0,
                    AllergenOverrides: [new IngredientRevisionPropertyItem(
                        allergenId,
                        IngredientPropertyState.DoesNotContain,
                        IngredientPropertySource.ManuallyVerified)])]),
            Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        Assert.Equal(IngredientRevisionMutationStatus.Saved, result.Status);
        Assert.Equal(tenantId, authorization.RequestedTenantId);
        Assert.Equal("Rote Linsen", store.SavedContent!.Name);
        Assert.Equal("ROTE LINSEN", store.SavedContent.NormalizedName);
        Assert.Equal(allergenId, Assert.Single(store.SavedContent.Allergens).PropertyId);
        IngredientRevisionUnitConversion conversion = Assert.Single(store.SavedContent.UnitConversions);
        Assert.Equal(sourceUnitId, conversion.SourceUnitId);
        Assert.Equal(12m, conversion.FactorToBaseUnit);
        IngredientVariantDraftContent variant = Assert.Single(store.SavedContent.Variants!);
        Assert.Equal(variantId, variant.Id);
        Assert.Equal("smoked", variant.VariantKey);
        Assert.Equal("Geräuchert", variant.Name);
        Assert.Equal(IngredientPropertyState.DoesNotContain,
            Assert.Single(variant.AllergenOverrides).State);
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
    public async Task Platform_administrator_can_list_central_revisions()
    {
        var store = new FakeStore(null);
        var service = new IngredientRevisionWorkflowService(
            store,
            new FakeAuthorization { CentralAllowed = true },
            TimeProvider.System);

        IngredientRevisionListResult result = await service.ListCentralAsync(
            Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.True(result.IsAuthorized);
        Assert.Equal(IngredientScopeType.Central, store.ListedScope!.ScopeType);
        Assert.Null(store.ListedScope.ScopeId);
    }

    [Fact]
    public async Task Actor_without_platform_permission_cannot_list_central_revisions()
    {
        var store = new FakeStore(null);
        var service = new IngredientRevisionWorkflowService(store, new FakeAuthorization(), TimeProvider.System);

        IngredientRevisionListResult result = await service.ListCentralAsync(
            Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.False(result.IsAuthorized);
        Assert.Null(store.ListedScope);
    }

    [Fact]
    public async Task Tenant_administrator_can_list_only_own_tenant_revisions()
    {
        Guid tenantId = Guid.NewGuid();
        var store = new FakeStore(null);
        var authorization = new FakeAuthorization { TenantAllowed = true };
        var service = new IngredientRevisionWorkflowService(store, authorization, TimeProvider.System);

        IngredientRevisionListResult result = await service.ListTenantAsync(
            tenantId, Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.True(result.IsAuthorized);
        Assert.Equal(tenantId, authorization.RequestedTenantId);
        Assert.Equal(IngredientScopeType.Tenant, store.ListedScope!.ScopeType);
        Assert.Equal(tenantId, store.ListedScope.ScopeId);
    }

    [Fact]
    public async Task Authorized_actor_can_create_follow_up_draft_from_published_revision()
    {
        Guid publishedRevisionId = Guid.NewGuid();
        var store = new FakeStore(new IngredientRevisionScope(IngredientScopeType.Central, null));
        var service = new IngredientRevisionWorkflowService(
            store,
            new FakeAuthorization { CentralAllowed = true },
            TimeProvider.System);

        IngredientRevisionMutationResult result = await service.CreateDraftFromPublishedAsync(
            publishedRevisionId,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        Assert.Equal(IngredientRevisionMutationStatus.Created, result.Status);
        Assert.Equal(publishedRevisionId, store.PublishedRevisionIdForDraft);
    }

    [Fact]
    public async Task Camp_fork_is_not_created_until_source_content_is_changed()
    {
        Guid sourceRevisionId = Guid.NewGuid();
        Guid categoryId = Guid.NewGuid();
        Guid unitId = Guid.NewGuid();
        var store = new FakeStore(new IngredientRevisionScope(IngredientScopeType.Central, null))
        {
            Revision = new IngredientRevisionDraftDetails(
                sourceRevisionId, Guid.NewGuid(), IngredientScopeType.Central, null, 1,
                IngredientRevisionState.Published, null, "Linsen", categoryId, unitId,
                IngredientPropertyReviewState.Reviewed, IngredientPropertyReviewState.Reviewed,
                IngredientPropertyReviewState.Reviewed, 3, [], [], [], [], []),
        };
        var service = new IngredientRevisionWorkflowService(
            store, new FakeAuthorization { CampAllowed = true }, TimeProvider.System);
        var unchanged = new CreateIngredientForkRequest(
            "Linsen", categoryId, unitId,
            IngredientPropertyReviewState.Reviewed, IngredientPropertyReviewState.Reviewed,
            IngredientPropertyReviewState.Reviewed, 3);

        IngredientRevisionMutationResult ignored = await service.CreateCampForkAsync(
            Guid.NewGuid(), sourceRevisionId, unchanged, Guid.NewGuid(),
            TestContext.Current.CancellationToken);
        IngredientRevisionMutationResult created = await service.CreateCampForkAsync(
            Guid.NewGuid(), sourceRevisionId, unchanged with { Name = "Rote Linsen" }, Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        Assert.Equal(IngredientRevisionMutationStatus.NoChanges, ignored.Status);
        Assert.Equal(IngredientRevisionMutationStatus.Created, created.Status);
        Assert.True(store.ForkCalled);
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
        public Guid? PublishedRevisionIdForDraft { get; private set; }
        public IngredientRevisionDraftDetails? Revision { get; init; }
        public bool ForkCalled { get; private set; }
        public IngredientRevisionScope? ListedScope { get; private set; }

        public Task<IngredientRevisionScope?> GetScopeAsync(Guid revisionId, CancellationToken cancellationToken = default) =>
            Task.FromResult(scope);

        public Task<IngredientRevisionDraftDetails?> GetAsync(
            Guid revisionId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Revision);

        public Task<IReadOnlyList<IngredientRevisionSummary>> ListAsync(
            IngredientRevisionScope revisionScope,
            CancellationToken cancellationToken = default)
        {
            ListedScope = revisionScope;
            return Task.FromResult<IReadOnlyList<IngredientRevisionSummary>>([]);
        }

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

        public Task<IngredientRevisionMutationResult> CreateDraftFromPublishedAsync(
            Guid publishedRevisionId,
            Guid newRevisionId,
            Guid actorUserId,
            DateTimeOffset createdAtUtc,
            CancellationToken cancellationToken = default)
        {
            PublishedRevisionIdForDraft = publishedRevisionId;
            return Task.FromResult(new IngredientRevisionMutationResult(
                IngredientRevisionMutationStatus.Created, 1, Guid.NewGuid(), newRevisionId));
        }

        public Task<IngredientRevisionMutationResult> CreateForkDraftAsync(
            Guid sourceRevisionId,
            long expectedSourceRowVersion,
            Guid ingredientId,
            Guid revisionId,
            IngredientRevisionScope revisionScope,
            IngredientRevisionDraftContent content,
            Guid actorUserId,
            DateTimeOffset createdAtUtc,
            CancellationToken cancellationToken = default)
        {
            ForkCalled = true;
            return Task.FromResult(new IngredientRevisionMutationResult(
                IngredientRevisionMutationStatus.Created, 1, ingredientId, revisionId));
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
        public bool CampAllowed { get; init; }
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
            Task.FromResult(CampAllowed);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
