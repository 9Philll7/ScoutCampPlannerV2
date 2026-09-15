using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Catering.Domain;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class RecipeNutritionPreviewServiceTests
{
    [Fact]
    public async Task Preview_calculates_saved_camp_recipe_at_reference_demand()
    {
        Guid campId = Guid.NewGuid();
        Guid recipeId = Guid.NewGuid();
        Guid ingredientId = Guid.NewGuid();
        Guid unitId = Guid.NewGuid();
        var draft = new RecipeDraft(
            recipeId, RecipeScopeType.Camp, campId, RecipeType.PortionBased, "Reis");
        draft.ConfigurePortionReference(10m, true);
        draft.AddIngredientPosition(new RecipeIngredientPosition(
            Guid.NewGuid(), recipeId, null, ingredientId, 100m, unitId, 0));
        var references = new FakeReferences(ingredientId, unitId);
        var service = new RecipeNutritionPreviewService(
            new FakeStore(draft), new FakeAuthorization(true), new RecipeSnapshotBuilder(references),
            new RecipeCalculator(references));

        RecipeNutritionPreviewResult result = await service.PreviewCampAsync(
            campId, recipeId, Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Equal(RecipeNutritionPreviewStatus.Found, result.Status);
        Assert.True(result.Nutrition!.IsComplete);
        Assert.Equal(1_000m, result.Nutrition.Total!.EnergyKilojoules);
        Assert.Equal(100m, result.Nutrition.PerStandardPortion!.EnergyKilojoules);
    }

    [Fact]
    public async Task Preview_rejects_incomplete_recipe_without_throwing()
    {
        Guid campId = Guid.NewGuid();
        var draft = new RecipeDraft(
            Guid.NewGuid(), RecipeScopeType.Camp, campId, RecipeType.PortionBased, "Leer");
        var references = new FakeReferences(Guid.NewGuid(), Guid.NewGuid());
        var service = new RecipeNutritionPreviewService(
            new FakeStore(draft), new FakeAuthorization(true), new RecipeSnapshotBuilder(references),
            new RecipeCalculator(references));

        RecipeNutritionPreviewResult result = await service.PreviewCampAsync(
            campId, draft.Id, Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Equal(RecipeNutritionPreviewStatus.NotCalculable, result.Status);
        Assert.Null(result.Nutrition);
    }

    [Fact]
    public async Task Unauthorized_preview_does_not_read_recipe()
    {
        var store = new FakeStore(null);
        var references = new FakeReferences(Guid.NewGuid(), Guid.NewGuid());
        var service = new RecipeNutritionPreviewService(
            store, new FakeAuthorization(false), new RecipeSnapshotBuilder(references),
            new RecipeCalculator(references));

        RecipeNutritionPreviewResult result = await service.PreviewCampAsync(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.Equal(RecipeNutritionPreviewStatus.Forbidden, result.Status);
        Assert.Equal(0, store.FindCallCount);
    }

    private sealed class FakeStore(RecipeDraft? draft) : IRecipeEditorStore
    {
        public int FindCallCount { get; private set; }

        public Task<RecipeDraft?> FindAsync(Guid recipeId, CancellationToken cancellationToken = default)
        {
            FindCallCount++;
            return Task.FromResult(draft);
        }

        public Task<RecipeDraft> CreateCampAsync(
            RecipeDraft value, Guid campId, Guid actorUserId, DateTimeOffset timestampUtc,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<RecipeDraftSaveResult> SaveAsync(
            RecipeDraft value, long expectedVersion, Guid actorUserId, DateTimeOffset timestampUtc,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

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

    private sealed class FakeReferences(Guid ingredientId, Guid unitId) :
        IRecipeSnapshotReferences,
        IRecipeSnapshotSource
    {
        private readonly MeasurementUnitSnapshot unit =
            new(unitId, "Gramm", "g", MeasurementDimension.Mass, 1m);

        public IngredientSnapshotSource GetIngredient(Guid requestedIngredientId)
        {
            Assert.Equal(ingredientId, requestedIngredientId);
            return new IngredientSnapshotSource(
                ingredientId, "Reis", [],
                new IngredientNutritionSnapshot(
                    100m, unit, 100m, 1_000m, 1m, 0.2m, 75m, 1m, 8m, 0.01m, null,
                    IngredientNutritionSourceType.OfficialDatabase, "Testquelle",
                    IngredientNutritionReviewState.Reviewed, null));
        }

        public IngredientUnitSnapshot GetIngredientUnit(Guid requestedIngredientId, Guid requestedUnitId)
        {
            Assert.Equal(ingredientId, requestedIngredientId);
            Assert.Equal(unitId, requestedUnitId);
            return new IngredientUnitSnapshot(unit, 1m);
        }

        public MeasurementUnitSnapshot GetUnit(Guid requestedUnitId) =>
            requestedUnitId == unitId ? unit : throw new KeyNotFoundException();

        public IReadOnlySet<ConflictReference> GetRevisionConflicts(Guid revisionId) =>
            new HashSet<ConflictReference>();

        public RecipeSnapshot GetRevision(Guid revisionId) => throw new KeyNotFoundException();
    }
}
