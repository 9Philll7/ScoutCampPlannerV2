using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Catering.Infrastructure.Recipes;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class RecipeDraftStoreTests
{
    [Fact]
    public async Task Incomplete_draft_graph_round_trips()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Guid recipeId = Guid.NewGuid();
        var draft = new RecipeDraft(recipeId, RecipeScopeType.Central, null, RecipeType.PortionBased);
        draft.ReplaceTags(["Schnell"]);
        var group = new RecipeIngredientGroup(Guid.NewGuid(), recipeId, null, 0);
        draft.AddGroup(group);
        var ingredientPosition = new RecipeIngredientPosition(
            Guid.NewGuid(), recipeId, group.Id, null, null, null, 0);
        ingredientPosition.AddReplacementRule(new IngredientReplacementRule(
            Guid.NewGuid(), ingredientPosition.Id, null, null, null));
        draft.AddIngredientPosition(ingredientPosition);
        draft.AddSubrecipePosition(new RecipeSubrecipePosition(
            Guid.NewGuid(), recipeId, null, null, null, null, null, 0));

        await fixture.Store.CreateAsync(
            draft, Guid.NewGuid(), DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);
        fixture.Database.ChangeTracker.Clear();
        RecipeDraft? loaded = await fixture.Store.FindAsync(recipeId, TestContext.Current.CancellationToken);

        Assert.NotNull(loaded);
        Assert.Equal(0, loaded.DraftVersion);
        Assert.Single(loaded.Groups);
        Assert.Single(loaded.IngredientPositions);
        Assert.Single(loaded.IngredientPositions[0].ReplacementRules);
        Assert.Single(loaded.SubrecipePositions);
        Assert.Contains("SCHNELL", loaded.Tags);

        loaded.SetName("Weiterbearbeitet");
        RecipeDraftSaveResult saved = await fixture.Store.SaveAsync(
            loaded, 0, Guid.NewGuid(), DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);
        Assert.Equal(RecipeDraftSaveStatus.Saved, saved.Status);
        Assert.Single((await fixture.Store.FindAsync(recipeId, TestContext.Current.CancellationToken))!.Groups);
    }

    [Fact]
    public async Task Non_stale_save_increments_version()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var draft = new RecipeDraft(
            Guid.NewGuid(), RecipeScopeType.Central, null, RecipeType.PortionBased, "Erste Fassung");
        await fixture.Store.CreateAsync(
            draft, Guid.NewGuid(), DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);
        draft.SetName("Zweite Fassung");

        RecipeDraftSaveResult result = await fixture.Store.SaveAsync(
            draft, expectedVersion: 0, Guid.NewGuid(), DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);

        Assert.Equal(RecipeDraftSaveStatus.Saved, result.Status);
        Assert.Equal(1, result.CurrentDraft!.DraftVersion);
        Assert.Equal("Zweite Fassung", (await fixture.Store.FindAsync(
            draft.Id, TestContext.Current.CancellationToken))!.Name);
    }

    [Fact]
    public async Task Stale_save_returns_current_draft_without_overwriting_it()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var initial = new RecipeDraft(
            Guid.NewGuid(), RecipeScopeType.Central, null, RecipeType.PortionBased, "Initial");
        await fixture.Store.CreateAsync(
            initial, Guid.NewGuid(), DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);
        RecipeDraft firstEditor = (await fixture.Store.FindAsync(
            initial.Id, TestContext.Current.CancellationToken))!;
        RecipeDraft staleEditor = (await fixture.Store.FindAsync(
            initial.Id, TestContext.Current.CancellationToken))!;
        firstEditor.SetName("Gespeicherte Änderung");
        await fixture.Store.SaveAsync(
            firstEditor, 0, Guid.NewGuid(), DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);
        staleEditor.SetName("Veraltete Änderung");

        RecipeDraftSaveResult conflict = await fixture.Store.SaveAsync(
            staleEditor, 0, Guid.NewGuid(), DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        Assert.Equal(RecipeDraftSaveStatus.VersionConflict, conflict.Status);
        Assert.Equal(1, conflict.CurrentDraft!.DraftVersion);
        Assert.Equal("Gespeicherte Änderung", conflict.CurrentDraft.Name);
    }

    private sealed class DatabaseFixture(
        SqliteConnection connection,
        CateringDbContext database,
        RecipeDraftStore store) : IAsyncDisposable
    {
        public CateringDbContext Database { get; } = database;
        public RecipeDraftStore Store { get; } = store;

        public static async Task<DatabaseFixture> CreateAsync()
        {
            SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
            var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            var database = new CateringDbContext(
                new DbContextOptionsBuilder<CateringDbContext>().UseSqlite(connection).Options);
            await database.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            return new DatabaseFixture(connection, database, new RecipeDraftStore(database));
        }

        public async ValueTask DisposeAsync()
        {
            await Database.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
