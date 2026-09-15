using ScoutCampPlanner.Catering.Application.Ingredients;
using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Catering.Domain;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class CampRecipePublicationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Missing_publish_permission_does_not_access_the_recipe()
    {
        var drafts = new FakeDraftStore();
        var publisher = new FakePublisher();
        var service = CreateService(drafts, publisher, allowed: false, Guid.NewGuid());

        CampRecipePublicationResult result = await service.PublishAsync(
            Guid.NewGuid(), Guid.NewGuid(), 0, false, Guid.NewGuid(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(CampRecipePublicationStatus.Forbidden, result.Status);
        Assert.Equal(0, drafts.FindCallCount);
        Assert.Equal(0, publisher.CallCount);
    }

    [Fact]
    public async Task Recipe_from_another_camp_is_not_published()
    {
        Guid requestedCampId = Guid.NewGuid();
        var drafts = new FakeDraftStore
        {
            Draft = new RecipeDraft(
                Guid.NewGuid(), RecipeScopeType.Camp, Guid.NewGuid(), RecipeType.PortionBased, "Suppe"),
        };
        var publisher = new FakePublisher();
        var service = CreateService(drafts, publisher, allowed: true, Guid.NewGuid());

        CampRecipePublicationResult result = await service.PublishAsync(
            requestedCampId, drafts.Draft.Id, 0, false, Guid.NewGuid(),
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(CampRecipePublicationStatus.NotFound, result.Status);
        Assert.Equal(0, publisher.CallCount);
    }

    [Fact]
    public async Task Publication_uses_the_camps_tenant_as_validation_context()
    {
        Guid campId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        Guid actorId = Guid.NewGuid();
        var draft = new RecipeDraft(
            Guid.NewGuid(), RecipeScopeType.Camp, campId, RecipeType.PortionBased, "Suppe");
        var drafts = new FakeDraftStore { Draft = draft };
        var publisher = new FakePublisher
        {
            Result = new RecipePublicationResult(
                RecipePublicationStatus.WarningAcknowledgementRequired,
                new RecipeValidationResult([
                    new RecipeValidationIssue(
                        RecipeValidationCodes.SourceMissing,
                        RecipeValidationSeverity.Warning,
                        RecipeValidationCodes.SourceMissing),
                ]),
                null,
                draft),
        };
        var service = CreateService(drafts, publisher, allowed: true, tenantId);

        CampRecipePublicationResult result = await service.PublishAsync(
            campId, draft.Id, 3, false, actorId, "Erste Fassung",
            TestContext.Current.CancellationToken);

        Assert.Equal(CampRecipePublicationStatus.WarningAcknowledgementRequired, result.Status);
        Assert.Equal(tenantId, publisher.ValidationContext?.TenantId);
        Assert.Equal(actorId, publisher.ActorUserId);
        Assert.Equal(3, publisher.ExpectedVersion);
        Assert.False(publisher.AcknowledgeWarnings);
        Assert.Equal("Erste Fassung", publisher.ChangeNote);
        Assert.Equal(Now, publisher.TimestampUtc);
        Assert.Single(result.Validation!.Warnings);
    }

    private static CampRecipePublicationService CreateService(
        FakeDraftStore drafts,
        FakePublisher publisher,
        bool allowed,
        Guid? tenantId) => new(
        drafts,
        new FakeAuthorization(allowed),
        new FakeCampTenantResolver(tenantId),
        publisher,
        new FixedTimeProvider(Now));

    private sealed class FakeAuthorization(bool allowed) : IRecipeEditorAuthorization
    {
        public Task<bool> CanReadCampAsync(
            Guid actorUserId, Guid campId, CancellationToken cancellationToken = default) =>
            Task.FromResult(allowed);

        public Task<bool> CanEditCampAsync(
            Guid actorUserId, Guid campId, CancellationToken cancellationToken = default) =>
            Task.FromResult(allowed);

        public Task<bool> CanPublishCampAsync(
            Guid actorUserId, Guid campId, CancellationToken cancellationToken = default) =>
            Task.FromResult(allowed);
    }

    private sealed class FakeCampTenantResolver(Guid? tenantId) : ICampTenantResolver
    {
        public Task<Guid?> FindTenantIdAsync(
            Guid campId, CancellationToken cancellationToken = default) => Task.FromResult(tenantId);
    }

    private sealed class FakeDraftStore : IRecipeEditorStore
    {
        public RecipeDraft? Draft { get; init; }
        public int FindCallCount { get; private set; }

        public Task<RecipeDraft?> FindAsync(Guid recipeId, CancellationToken cancellationToken = default)
        {
            FindCallCount++;
            return Task.FromResult(Draft);
        }

        public Task<RecipeDraft> CreateCampAsync(
            RecipeDraft draft, Guid campId, Guid actorUserId, DateTimeOffset timestampUtc,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<RecipeDraftSaveResult> SaveAsync(
            RecipeDraft draft, long expectedVersion, Guid actorUserId, DateTimeOffset timestampUtc,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class FakePublisher : IRecipePublisher
    {
        public RecipePublicationResult Result { get; init; } =
            new(RecipePublicationStatus.NotFound, null, null, null);
        public int CallCount { get; private set; }
        public long ExpectedVersion { get; private set; }
        public Guid ActorUserId { get; private set; }
        public DateTimeOffset TimestampUtc { get; private set; }
        public bool AcknowledgeWarnings { get; private set; }
        public RecipeValidationContext? ValidationContext { get; private set; }
        public string? ChangeNote { get; private set; }

        public Task<RecipePublicationResult> PublishAsync(
            Guid recipeId, long expectedVersion, Guid actorUserId, DateTimeOffset timestampUtc,
            bool acknowledgeWarnings, RecipeValidationContext? validationContext = null,
            string? changeNote = null, CancellationToken cancellationToken = default)
        {
            CallCount++;
            ExpectedVersion = expectedVersion;
            ActorUserId = actorUserId;
            TimestampUtc = timestampUtc;
            AcknowledgeWarnings = acknowledgeWarnings;
            ValidationContext = validationContext;
            ChangeNote = changeNote;
            return Task.FromResult(Result);
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
