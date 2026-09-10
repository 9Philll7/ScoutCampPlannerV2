using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Catering.Domain;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class RecipeEditorServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 10, 18, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Unauthorized_create_does_not_access_store()
    {
        var store = new FakeStore();
        var service = new RecipeEditorService(store, new FakeAuthorization(false), new FixedTimeProvider(Now));

        RecipeEditorResult result = await service.CreateCampAsync(
            Guid.NewGuid(), Content(), Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Equal(RecipeEditorStatus.Forbidden, result.Status);
        Assert.Equal(0, store.CreateCallCount);
    }

    [Fact]
    public async Task Create_preserves_the_exact_ingredient_revision_reference()
    {
        Guid campId = Guid.NewGuid();
        Guid revisionId = Guid.NewGuid();
        Guid unitId = Guid.NewGuid();
        var store = new FakeStore();
        var service = new RecipeEditorService(store, new FakeAuthorization(true), new FixedTimeProvider(Now));
        RecipeEditorContent content = Content() with
        {
            IngredientPositions =
            [
                new(Guid.NewGuid(), null, revisionId, 1.25m, unitId, 0,
                    ScalingMode.Linear, AgeGroupScalingMode.Inherit, null, null, []),
            ],
        };

        RecipeEditorResult result = await service.CreateCampAsync(
            campId, content, Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Equal(RecipeEditorStatus.Created, result.Status);
        Assert.Equal(campId, store.CreatedDraft!.ScopeId);
        Assert.Equal(revisionId, Assert.Single(store.CreatedDraft.IngredientPositions).IngredientRevisionId);
        Assert.Equal(revisionId, Assert.Single(result.Draft!.Content.IngredientPositions).IngredientRevisionId);
        Assert.Equal(Now, store.CreatedAtUtc);
    }

    [Fact]
    public async Task Stale_save_returns_the_current_draft()
    {
        Guid campId = Guid.NewGuid();
        Guid recipeId = Guid.NewGuid();
        var current = new RecipeDraft(
            recipeId, RecipeScopeType.Camp, campId, RecipeType.PortionBased, "Aktueller Stand");
        current.SetPersistedVersion(2);
        var store = new FakeStore
        {
            FoundDraft = current,
            SaveResult = new(RecipeDraftSaveStatus.VersionConflict, current),
        };
        var service = new RecipeEditorService(store, new FakeAuthorization(true), new FixedTimeProvider(Now));

        RecipeEditorResult result = await service.SaveCampAsync(
            campId, recipeId, 1, Content() with { Name = "Veraltete Änderung" }, Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        Assert.Equal(RecipeEditorStatus.VersionConflict, result.Status);
        Assert.Equal(2, result.Draft!.DraftVersion);
        Assert.Equal("Aktueller Stand", result.Draft.Content.Name);
    }

    private static RecipeEditorContent Content() => new(
        "Neues Rezept", null, null, null, RecipeType.PortionBased, 10, null, null, true, null,
        [], [], [], []);

    private sealed class FakeStore : IRecipeEditorStore
    {
        public int CreateCallCount { get; private set; }
        public RecipeDraft? CreatedDraft { get; private set; }
        public DateTimeOffset? CreatedAtUtc { get; private set; }
        public RecipeDraft? FoundDraft { get; init; }
        public RecipeDraftSaveResult SaveResult { get; init; } = new(RecipeDraftSaveStatus.Saved, null);

        public Task<RecipeDraft?> FindAsync(Guid recipeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(FoundDraft);

        public Task<RecipeDraft> CreateCampAsync(
            RecipeDraft draft, Guid campId, Guid actorUserId, DateTimeOffset timestampUtc,
            CancellationToken cancellationToken = default)
        {
            CreateCallCount++;
            CreatedDraft = draft;
            CreatedAtUtc = timestampUtc;
            return Task.FromResult(draft);
        }

        public Task<RecipeDraftSaveResult> SaveAsync(
            RecipeDraft draft, long expectedVersion, Guid actorUserId, DateTimeOffset timestampUtc,
            CancellationToken cancellationToken = default) => Task.FromResult(SaveResult);
    }

    private sealed class FakeAuthorization(bool allowed) : IRecipeEditorAuthorization
    {
        public Task<bool> CanReadCampAsync(
            Guid actorUserId, Guid campId, CancellationToken cancellationToken = default) =>
            Task.FromResult(allowed);

        public Task<bool> CanEditCampAsync(
            Guid actorUserId, Guid campId, CancellationToken cancellationToken = default) =>
            Task.FromResult(allowed);
    }

    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
