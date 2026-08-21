using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Catering.Domain;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class RecipeLifecycleServiceTests
{
    [Fact]
    public async Task Restore_copies_revision_into_same_recipe_without_reusing_child_ids()
    {
        Guid recipeId = Guid.NewGuid();
        Guid revisionId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        Guid nestedRevisionId = Guid.NewGuid();
        RecipeSnapshot snapshot = Snapshot(nestedRevisionId);
        var current = new RecipeDraft(
            recipeId, RecipeScopeType.Tenant, tenantId, RecipeType.PortionBased, "Aktuell", RecipeStatus.Active);
        current.SetPersistedVersion(4);
        var store = new FakeStore(current);
        var service = new RecipeLifecycleService(
            store, new FakeRevisionSource(revisionId, new RecipeRevisionSnapshot(recipeId, snapshot)));

        RecipeDraftSaveResult result = await service.RestoreRevisionAsync(
            recipeId, revisionId, 4, Guid.NewGuid(), DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);

        Assert.Equal(RecipeDraftSaveStatus.Saved, result.Status);
        RecipeDraft restored = Assert.IsType<RecipeDraft>(store.Saved);
        Assert.Equal(recipeId, restored.Id);
        Assert.Equal(RecipeStatus.Active, restored.Status);
        Assert.Equal("Veröffentlichter Stand", restored.Name);
        Assert.NotEqual(snapshot.Groups[0].Id, restored.Groups[0].Id);
        Assert.Equal(restored.Groups[0].Id, restored.IngredientPositions[0].GroupId);
        Assert.Equal(nestedRevisionId, restored.SubrecipePositions[0].RecipeRevisionId);
        Assert.Equal(4, store.ExpectedVersion);
    }

    [Fact]
    public async Task Duplicate_creates_independent_draft_with_lineage_and_exact_nested_revision()
    {
        Guid sourceRecipeId = Guid.NewGuid();
        Guid sourceRevisionId = Guid.NewGuid();
        Guid nestedRevisionId = Guid.NewGuid();
        Guid newRecipeId = Guid.NewGuid();
        Guid campId = Guid.NewGuid();
        var store = new FakeStore(null);
        var service = new RecipeLifecycleService(
            store,
            new FakeRevisionSource(
                sourceRevisionId,
                new RecipeRevisionSnapshot(sourceRecipeId, Snapshot(nestedRevisionId))));

        RecipeDraft duplicate = await service.DuplicateRevisionAsync(
            sourceRevisionId, newRecipeId, RecipeScopeType.Camp, campId, "Eigene Variante",
            Guid.NewGuid(), DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        Assert.Equal(newRecipeId, duplicate.Id);
        Assert.Equal(RecipeStatus.Draft, duplicate.Status);
        Assert.Equal(RecipeScopeType.Camp, duplicate.ScopeType);
        Assert.Equal(campId, duplicate.ScopeId);
        Assert.Equal("Eigene Variante", duplicate.Name);
        Assert.Equal(nestedRevisionId, duplicate.SubrecipePositions[0].RecipeRevisionId);
        Assert.Equal(new RecipeDraftLineage(sourceRecipeId, sourceRevisionId), store.Lineage);
    }

    [Fact]
    public async Task Restore_rejects_revision_from_another_recipe()
    {
        Guid recipeId = Guid.NewGuid();
        Guid revisionId = Guid.NewGuid();
        var current = new RecipeDraft(
            recipeId, RecipeScopeType.Tenant, Guid.NewGuid(), RecipeType.PortionBased, "Aktuell");
        var service = new RecipeLifecycleService(
            new FakeStore(current),
            new FakeRevisionSource(
                revisionId, new RecipeRevisionSnapshot(Guid.NewGuid(), Snapshot(Guid.NewGuid()))));

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RestoreRevisionAsync(
            recipeId, revisionId, 0, Guid.NewGuid(), DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken));
    }

    private static RecipeSnapshot Snapshot(Guid nestedRevisionId)
    {
        var unit = new MeasurementUnitSnapshot(
            Guid.NewGuid(), "Gramm", "g", MeasurementDimension.Mass, 1m);
        Guid groupId = Guid.NewGuid();
        return new RecipeSnapshot(
            1, "Veröffentlichter Stand", "Beschreibung", "Quelle", "Notiz",
            RecipeType.PortionBased, new RecipeReferenceSnapshot(10m, 1m, null, null), null, true,
            ["einfach"], [new RecipeGroupSnapshot(groupId, "Teig", 0)],
            [
                new IngredientPositionSnapshot(
                    Guid.NewGuid(), groupId, 0,
                    new IngredientSnapshotSource(Guid.NewGuid(), "Mehl", []), 500m,
                    new IngredientUnitSnapshot(unit, 1m), ScalingMode.Linear,
                    AgeGroupScalingMode.Inherit, null, [])
            ],
            [
                new SubrecipePositionSnapshot(
                    Guid.NewGuid(), null, 1, nestedRevisionId, 2m, null, null, [], [])
            ],
            []);
    }

    private sealed class FakeRevisionSource(Guid revisionId, RecipeRevisionSnapshot revision) : IRecipeRevisionSource
    {
        public RecipeRevisionSnapshot GetRevisionSnapshot(Guid requestedRevisionId)
        {
            Assert.Equal(revisionId, requestedRevisionId);
            return revision;
        }
    }

    private sealed class FakeStore(RecipeDraft? current) : IRecipeDraftStore
    {
        public RecipeDraft? Saved { get; private set; }
        public long? ExpectedVersion { get; private set; }
        public RecipeDraftLineage? Lineage { get; private set; }

        public Task<RecipeDraft?> FindAsync(Guid recipeId, CancellationToken cancellationToken = default) =>
            Task.FromResult(current);

        public Task<RecipeDraft> CreateAsync(
            RecipeDraft draft, Guid actorUserId, DateTimeOffset timestampUtc,
            CancellationToken cancellationToken = default) => Task.FromResult(draft);

        public Task<RecipeDraft> CreateDerivedAsync(
            RecipeDraft draft, RecipeDraftLineage lineage, Guid actorUserId, DateTimeOffset timestampUtc,
            CancellationToken cancellationToken = default)
        {
            Lineage = lineage;
            return Task.FromResult(draft);
        }

        public Task<RecipeDraftSaveResult> SaveAsync(
            RecipeDraft draft, long expectedVersion, Guid actorUserId, DateTimeOffset timestampUtc,
            CancellationToken cancellationToken = default)
        {
            Saved = draft;
            ExpectedVersion = expectedVersion;
            return Task.FromResult(new RecipeDraftSaveResult(RecipeDraftSaveStatus.Saved, draft));
        }

        public Task<RecipeLifecycleResult> ArchiveAsync(
            Guid recipeId, long expectedVersion, Guid actorUserId, DateTimeOffset timestampUtc,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RecipeLifecycleResult(RecipeLifecycleStatus.Changed, current));

        public Task<RecipeLifecycleResult> ReactivateAsync(
            Guid recipeId, long expectedVersion, Guid actorUserId, DateTimeOffset timestampUtc,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RecipeLifecycleResult(RecipeLifecycleStatus.Changed, current));

        public Task<RecipeLifecycleResult> ResetToDraftAsync(
            Guid recipeId, long expectedVersion, Guid actorUserId, DateTimeOffset timestampUtc,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new RecipeLifecycleResult(RecipeLifecycleStatus.Changed, current));
    }
}
