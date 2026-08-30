using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Application.Ingredients;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Catering.Infrastructure.Ingredients;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class IngredientRevisionWorkflowPersistenceTests
{
    [Fact]
    public async Task Save_draft_updates_content_and_reports_stale_version()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Seed seed = await fixture.SeedDraftAsync(reviewed: false);
        var store = new IngredientRevisionWorkflowStore(fixture.Database);
        IngredientRevisionDraftContent content = IngredientRevisionDraftContent.Create(
            "Gelbe Linsen",
            seed.CategoryId,
            seed.UnitId,
            IngredientPropertyReviewState.Reviewed,
            IngredientPropertyReviewState.Reviewed,
            IngredientPropertyReviewState.Reviewed);

        IngredientRevisionMutationResult saved = await store.SaveDraftAsync(
            seed.RevisionId, content, 1, seed.ActorId, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);
        IngredientRevisionMutationResult stale = await store.SaveDraftAsync(
            seed.RevisionId, content, 1, seed.ActorId, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);

        Assert.Equal(IngredientRevisionMutationStatus.Saved, saved.Status);
        Assert.Equal(2, saved.RowVersion);
        Assert.Equal(IngredientRevisionMutationStatus.ConcurrencyConflict, stale.Status);
        Assert.Equal(2, stale.RowVersion);
        IngredientRevisionRecord stored = await fixture.Database.Set<IngredientRevisionRecord>()
            .AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal("Gelbe Linsen", stored.Name);
        Assert.Equal("GELBE LINSEN", stored.NormalizedName);
    }

    [Fact]
    public async Task Publish_is_transactional_and_updates_identity_pointer()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Seed seed = await fixture.SeedDraftAsync(reviewed: true);
        var store = new IngredientRevisionWorkflowStore(fixture.Database);

        IngredientRevisionMutationResult result = await store.PublishAsync(
            seed.RevisionId, 1, seed.ActorId, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);

        Assert.Equal(IngredientRevisionMutationStatus.Published, result.Status);
        Assert.Equal(2, result.RowVersion);
        IngredientRevisionRecord revision = await fixture.Database.Set<IngredientRevisionRecord>()
            .AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        IngredientIdentityRecord identity = await fixture.Database.Set<IngredientIdentityRecord>()
            .AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal((int)IngredientRevisionState.Published, revision.State);
        Assert.Equal(seed.RevisionId, identity.CurrentPublishedRevisionId);
    }

    [Fact]
    public async Task Invalid_publication_leaves_draft_and_identity_unchanged()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Seed seed = await fixture.SeedDraftAsync(reviewed: false);
        var store = new IngredientRevisionWorkflowStore(fixture.Database);

        IngredientRevisionMutationResult result = await store.PublishAsync(
            seed.RevisionId, 1, seed.ActorId, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);

        Assert.Equal(IngredientRevisionMutationStatus.Invalid, result.Status);
        IngredientRevisionRecord revision = await fixture.Database.Set<IngredientRevisionRecord>()
            .AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        IngredientIdentityRecord identity = await fixture.Database.Set<IngredientIdentityRecord>()
            .AsNoTracking().SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal((int)IngredientRevisionState.Draft, revision.State);
        Assert.Null(identity.CurrentPublishedRevisionId);
    }

    private sealed class DatabaseFixture(SqliteConnection connection, CateringDbContext database) : IAsyncDisposable
    {
        public CateringDbContext Database { get; } = database;

        public static async Task<DatabaseFixture> CreateAsync()
        {
            SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
            var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
            await connection.OpenAsync();
            var database = new CateringDbContext(
                new DbContextOptionsBuilder<CateringDbContext>().UseSqlite(connection).Options);
            await database.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            return new DatabaseFixture(connection, database);
        }

        public async Task<Seed> SeedDraftAsync(bool reviewed)
        {
            Guid ingredientId = Guid.NewGuid();
            Guid revisionId = Guid.NewGuid();
            Guid actorId = Guid.NewGuid();
            var unit = new MeasurementUnit(Guid.NewGuid(), "Gramm", "g", MeasurementDimension.Mass, 1m);
            var category = new IngredientCategoryRecord
            {
                Id = Guid.NewGuid(), Code = "LEGUMES", Name = "Hülsenfrüchte", NormalizedName = "HÜLSENFRÜCHTE",
            };
            Database.AddRange(
                unit,
                category,
                new IngredientIdentityRecord
                {
                    Id = ingredientId,
                    ScopeType = (int)IngredientScopeType.Central,
                    Status = (int)IngredientIdentityStatus.Active,
                },
                new IngredientRevisionRecord
                {
                    Id = revisionId,
                    IngredientId = ingredientId,
                    RevisionNumber = 1,
                    State = (int)IngredientRevisionState.Draft,
                    Name = "Linsen",
                    NormalizedName = "LINSEN",
                    CategoryId = category.Id,
                    BaseUnitId = unit.Id,
                    AllergenReviewState = reviewed ? 1 : 0,
                    IntoleranceReviewState = reviewed ? 1 : 0,
                    OriginReviewState = reviewed ? 1 : 0,
                    RowVersion = 1,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    CreatedBy = actorId,
                    UpdatedAtUtc = DateTimeOffset.UtcNow,
                    UpdatedBy = actorId,
                });
            await Database.SaveChangesAsync(TestContext.Current.CancellationToken);
            Database.ChangeTracker.Clear();
            return new Seed(revisionId, category.Id, unit.Id, actorId);
        }

        public async ValueTask DisposeAsync()
        {
            await Database.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed record Seed(Guid RevisionId, Guid CategoryId, Guid UnitId, Guid ActorId);
}
