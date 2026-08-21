using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Catering.Infrastructure.Recipes;
using ScoutCampPlanner.Catering.Domain;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class RecipePersistenceTests
{
    [Fact]
    public async Task Incomplete_recipe_draft_can_be_persisted()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var draft = CreateDraftRecord();

        fixture.Database.Set<RecipeRecord>().Add(draft);
        await fixture.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        fixture.Database.ChangeTracker.Clear();

        RecipeRecord stored = await fixture.Database.Set<RecipeRecord>()
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Empty(stored.Name);
        Assert.Null(stored.ReferenceServings);
        Assert.Null(stored.ReferenceQuantity);
        Assert.Equal(0, stored.DraftVersion);
    }

    [Fact]
    public async Task Central_recipe_name_is_unique_despite_null_owner()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        RecipeRecord first = CreateDraftRecord();
        first.Name = "Curry";
        first.NormalizedName = "CURRY";
        fixture.Database.Set<RecipeRecord>().Add(first);
        await fixture.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        RecipeRecord duplicate = CreateDraftRecord();
        duplicate.Name = "CURRY";
        duplicate.NormalizedName = "CURRY";
        fixture.Database.Set<RecipeRecord>().Add(duplicate);

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            fixture.Database.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Published_revision_snapshot_cannot_be_modified()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        RecipeRecord draft = CreateDraftRecord();
        var revision = new RecipeRevisionRecord
        {
            Id = Guid.NewGuid(),
            RecipeId = draft.Id,
            RevisionNumber = 1,
            PublishedAtUtc = DateTimeOffset.UtcNow,
            PublishedBy = Guid.NewGuid(),
            SnapshotSchemaVersion = 1,
            SnapshotJson = "{\"schemaVersion\":1}",
        };
        fixture.Database.AddRange(draft, revision);
        await fixture.Database.SaveChangesAsync(TestContext.Current.CancellationToken);

        revision.SnapshotJson = "{\"schemaVersion\":2}";

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.Database.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Duplicate_ungrouped_ingredient_is_rejected_by_database()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        RecipeRecord draft = CreateDraftRecord();
        var ingredient = new BaseIngredient(Guid.NewGuid(), IngredientScopeType.Central, null, "Reis");
        var unit = new MeasurementUnit(Guid.NewGuid(), "Gramm", "g", MeasurementDimension.Mass, 1m);
        fixture.Database.AddRange(draft, ingredient, unit);
        fixture.Database.Set<RecipeIngredientPositionRecord>().AddRange(
            new RecipeIngredientPositionRecord
            {
                Id = Guid.NewGuid(), RecipeId = draft.Id, BaseIngredientId = ingredient.Id,
                UnitId = unit.Id, Quantity = 1m, SortOrder = 0,
            },
            new RecipeIngredientPositionRecord
            {
                Id = Guid.NewGuid(), RecipeId = draft.Id, BaseIngredientId = ingredient.Id,
                UnitId = unit.Id, Quantity = 2m, SortOrder = 1,
            });

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            fixture.Database.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    private static RecipeRecord CreateDraftRecord()
    {
        Guid userId = Guid.NewGuid();
        return new RecipeRecord
        {
            Id = Guid.NewGuid(),
            ScopeType = 0,
            Name = string.Empty,
            NormalizedName = string.Empty,
            Status = 0,
            RecipeType = 0,
            CreatedBy = userId,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            UpdatedBy = userId,
            UpdatedAtUtc = DateTimeOffset.UtcNow,
        };
    }

    private sealed class DatabaseFixture(SqliteConnection connection, CateringDbContext database) : IAsyncDisposable
    {
        public CateringDbContext Database { get; } = database;

        public static async Task<DatabaseFixture> CreateAsync()
        {
            SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
            var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            var database = new CateringDbContext(
                new DbContextOptionsBuilder<CateringDbContext>().UseSqlite(connection).Options);
            await database.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            return new DatabaseFixture(connection, database);
        }

        public async ValueTask DisposeAsync()
        {
            await Database.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
