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
            Guid.NewGuid(), recipeId, group.Id, null, null, null, 0, minimumQuantity: 5m, maximumQuantity: 100m);
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
        Assert.Equal(5m, loaded.IngredientPositions[0].MinimumQuantity);
        Assert.Equal(100m, loaded.IngredientPositions[0].MaximumQuantity);
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

    [Fact]
    public async Task Derived_draft_persists_source_lineage()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Guid sourceRecipeId = Guid.NewGuid();
        Guid sourceRevisionId = Guid.NewGuid();
        var source = new RecipeDraft(
            sourceRecipeId, RecipeScopeType.Central, null, RecipeType.PortionBased, "Quelle");
        await fixture.Store.CreateAsync(
            source, Guid.NewGuid(), DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);
        var derived = new RecipeDraft(
            Guid.NewGuid(), RecipeScopeType.Central, null, RecipeType.PortionBased, "Kopie");

        await fixture.Store.CreateDerivedAsync(
            derived, new RecipeDraftLineage(sourceRecipeId, sourceRevisionId),
            Guid.NewGuid(), DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        await using var command = fixture.Database.Database.GetDbConnection().CreateCommand();
        command.CommandText =
            "SELECT DerivedFromRecipeId || '|' || DerivedFromRevisionId FROM Recipes WHERE Id = $id";
        command.Parameters.Add(new SqliteParameter("$id", derived.Id));
        string lineage = Assert.IsType<string>(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
        Assert.Equal($"{sourceRecipeId}|{sourceRevisionId}", lineage, ignoreCase: true);

        RecipePermanentDeleteResult blocked = await fixture.Store.DeletePermanentlyAsync(
            sourceRecipeId, 0, TestContext.Current.CancellationToken);
        Assert.Equal(RecipePermanentDeleteStatus.ReferenceBlocked, blocked.Status);
    }

    [Fact]
    public async Task Unreferenced_recipe_is_permanently_deleted_with_its_draft_graph()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Guid recipeId = Guid.NewGuid();
        var draft = new RecipeDraft(
            recipeId, RecipeScopeType.Central, null, RecipeType.PortionBased, "Löschbar");
        draft.ReplaceTags(["Test"]);
        draft.AddGroup(new RecipeIngredientGroup(Guid.NewGuid(), recipeId, "Gruppe", 0));
        await fixture.Store.CreateAsync(
            draft, Guid.NewGuid(), DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        RecipePermanentDeleteResult result = await fixture.Store.DeletePermanentlyAsync(
            recipeId, 0, TestContext.Current.CancellationToken);

        Assert.Equal(RecipePermanentDeleteStatus.Deleted, result.Status);
        Assert.Null(await fixture.Store.FindAsync(recipeId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Draft_archive_and_reactivation_are_versioned_and_audited()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Guid actorId = Guid.NewGuid();
        DateTimeOffset archivedAt = DateTimeOffset.UtcNow;
        var draft = new RecipeDraft(
            Guid.NewGuid(), RecipeScopeType.Central, null, RecipeType.PortionBased, "Entwurf");
        await fixture.Store.CreateAsync(
            draft, actorId, archivedAt.AddMinutes(-1), TestContext.Current.CancellationToken);

        RecipeLifecycleResult archived = await fixture.Store.ArchiveAsync(
            draft.Id, 0, actorId, archivedAt, TestContext.Current.CancellationToken);
        RecipeLifecycleResult stale = await fixture.Store.ReactivateAsync(
            draft.Id, 0, actorId, archivedAt, TestContext.Current.CancellationToken);
        RecipeLifecycleResult reactivated = await fixture.Store.ReactivateAsync(
            draft.Id, 1, actorId, archivedAt.AddMinutes(1), TestContext.Current.CancellationToken);

        Assert.Equal(RecipeStatus.Archived, archived.CurrentDraft!.Status);
        Assert.Equal(RecipeLifecycleStatus.VersionConflict, stale.Status);
        Assert.Equal(RecipeStatus.Draft, reactivated.CurrentDraft!.Status);
        Assert.Equal(2, reactivated.CurrentDraft.DraftVersion);

        await using var command = fixture.Database.Database.GetDbConnection().CreateCommand();
        command.CommandText =
            "SELECT ArchivedBy IS NOT NULL AND ArchivedAtUtc IS NOT NULL AND " +
            "ReactivatedBy IS NOT NULL AND ReactivatedAtUtc IS NOT NULL FROM Recipes WHERE Id = $id";
        command.Parameters.Add(new SqliteParameter("$id", draft.Id));
        Assert.Equal(1L, await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Subrecipe_metadata_is_pinned_scoped_and_marks_cycles_without_reading_mutable_draft_names()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var database = fixture.Database;
        Guid camp = Guid.NewGuid(), parent = Guid.NewGuid(), child = Guid.NewGuid(), ancestor = Guid.NewGuid(), foreign = Guid.NewGuid();
        Guid gram = Guid.NewGuid(), kilogram = Guid.NewGuid(), litre = Guid.NewGuid();
        database.AddRange(new MeasurementUnit(gram, "Test gram", "tg", MeasurementDimension.Mass, 1),
            new MeasurementUnit(kilogram, "Test kilogram", "tkg", MeasurementDimension.Mass, 1000),
            new MeasurementUnit(litre, "Test litre", "tl", MeasurementDimension.Volume, 1000));
        var mass = new MeasurementUnitSnapshot(gram, "Test gram", "tg", MeasurementDimension.Mass, 1);
        Guid Revision(Guid recipe, string name, int number, Guid libraryCamp, bool quantity = false)
        {
            if (number == 1)
            {
                database.Add(new RecipeRecord { Id = recipe, ScopeType = 2, ScopeId = libraryCamp, Name = "Mutable " + recipe,
                    NormalizedName = recipe.ToString(), Status = 1, RecipeType = quantity ? 1 : 0 });
                database.Add(new CampRecipeEntryRecord { Id = Guid.NewGuid(), CampId = libraryCamp, CampRecipeId = recipe });
            }
            var snapshot = new RecipeSnapshot(RecipeSnapshotBuilder.CurrentSchemaVersion, name, null, null, null,
                quantity ? RecipeType.QuantityBased : RecipeType.PortionBased,
                new RecipeReferenceSnapshot(quantity ? null : 10, 1, quantity ? 1000 : null, quantity ? mass : null),
                null, false, [], [], [], [], []);
            Guid id = Guid.NewGuid();
            database.Add(new RecipeRevisionRecord { Id = id, RecipeId = recipe, RevisionNumber = number,
                SnapshotSchemaVersion = RecipeSnapshotBuilder.CurrentSchemaVersion, SnapshotJson = RecipeSnapshotBuilder.Serialize(snapshot) });
            return id;
        }
        Guid parentRevision = Revision(parent, "Parent", 1, camp);
        Guid old = Revision(child, "Pinned original", 1, camp, true);
        Guid latest = Revision(child, "Published new", 2, camp, true);
        Guid cyclic = Revision(ancestor, "Ancestor", 1, camp);
        Guid hidden = Revision(foreign, "Other camp", 1, Guid.NewGuid());
        database.Add(new RecipeSubrecipePositionRecord { Id = Guid.NewGuid(), RecipeId = ancestor, RecipeRevisionId = parentRevision, RequiredServings = 1 });
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        var result = await new RecipeEditorSubrecipeReferenceStore(database).FindAsync(camp, parent, [old, hidden], TestContext.Current.CancellationToken);
        Assert.DoesNotContain(result, value => value.RevisionId == hidden);
        Assert.Equal("Pinned original", Assert.Single(result, value => value.RevisionId == old).Name);
        Assert.False(Assert.Single(result, value => value.RevisionId == old).CanSelect);
        Assert.False(Assert.Single(result, value => value.RevisionId == parentRevision).CanSelect);
        Assert.False(Assert.Single(result, value => value.RevisionId == cyclic).CanSelect);
        var current = Assert.Single(result, value => value.RevisionId == latest);
        Assert.True(current.CanSelect);
        Assert.Equal(RecipeType.QuantityBased, current.RecipeType);
        Assert.Equal(1000, current.ReferenceQuantity);
        Assert.Contains(current.Units, value => value.UnitId == kilogram);
        Assert.DoesNotContain(current.Units, value => value.UnitId == litre);
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
