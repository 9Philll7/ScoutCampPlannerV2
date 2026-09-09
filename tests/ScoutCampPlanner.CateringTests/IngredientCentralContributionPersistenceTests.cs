using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Application.Ingredients;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Catering.Infrastructure.Ingredients;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class IngredientCentralContributionPersistenceTests
{
    [Fact]
    public async Task Published_local_revision_can_only_be_submitted_once()
    {
        await using var fixture = await Fixture.CreateAsync();
        Seed local = await fixture.AddPublishedAsync(IngredientScopeType.Tenant, Guid.NewGuid(), "Haferflocken");
        Seed central = await fixture.AddPublishedAsync(IngredientScopeType.Central, null, "Haferflocken");
        var store = new IngredientCentralContributionStore(fixture.Database);

        CentralIngredientCandidate candidate = Assert.Single(await store.FindCentralCandidatesAsync(
            local.RevisionId, TestContext.Current.CancellationToken));

        IngredientContributionMutationResult first = await store.SubmitAsync(
            Guid.NewGuid(), local.RevisionId, local.ActorId, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);
        IngredientContributionMutationResult second = await store.SubmitAsync(
            Guid.NewGuid(), local.RevisionId, local.ActorId, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);

        Assert.Equal(IngredientContributionMutationStatus.Created, first.Status);
        Assert.Equal(IngredientContributionMutationStatus.AlreadySubmitted, second.Status);
        Assert.Equal(central.IngredientId, candidate.IngredientId);
        Assert.Single(await store.ListPendingAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Accepting_submission_creates_unreviewed_central_draft_without_changing_local_revision()
    {
        await using var fixture = await Fixture.CreateAsync();
        Seed local = await fixture.AddPublishedAsync(IngredientScopeType.Camp, Guid.NewGuid(), "Polenta");
        var store = new IngredientCentralContributionStore(fixture.Database);
        IngredientContributionMutationResult submitted = await store.SubmitAsync(
            Guid.NewGuid(), local.RevisionId, local.ActorId, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);

        IngredientContributionMutationResult accepted = await store.AcceptAsync(
            submitted.ContributionId!.Value, null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        Assert.Equal(IngredientContributionMutationStatus.Accepted, accepted.Status);
        IngredientRevisionRecord centralDraft = await fixture.Database.Set<IngredientRevisionRecord>()
            .AsNoTracking().SingleAsync(value => value.Id == accepted.CentralRevisionId,
                TestContext.Current.CancellationToken);
        Assert.Equal("Polenta", centralDraft.Name);
        Assert.Equal((int)IngredientRevisionState.Draft, centralDraft.State);
        Assert.Equal((int)IngredientPropertyReviewState.Unreviewed, centralDraft.AllergenReviewState);
        Assert.Equal((int)IngredientPropertyReviewState.Unreviewed, centralDraft.IntoleranceReviewState);
        Assert.Equal((int)IngredientPropertyReviewState.Unreviewed, centralDraft.OriginReviewState);
        IngredientRevisionRecord unchangedLocal = await fixture.Database.Set<IngredientRevisionRecord>()
            .AsNoTracking().SingleAsync(value => value.Id == local.RevisionId,
                TestContext.Current.CancellationToken);
        Assert.Equal((int)IngredientRevisionState.Published, unchangedLocal.State);
        Assert.Empty(await store.ListPendingAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Publishing_accepted_central_draft_replaces_unchanged_local_identity()
    {
        await using var fixture = await Fixture.CreateAsync();
        Seed local = await fixture.AddPublishedAsync(IngredientScopeType.Tenant, Guid.NewGuid(), "Bulgur");
        var contributionStore = new IngredientCentralContributionStore(fixture.Database);
        IngredientContributionMutationResult submitted = await contributionStore.SubmitAsync(
            Guid.NewGuid(), local.RevisionId, local.ActorId, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);
        IngredientContributionMutationResult accepted = await contributionStore.AcceptAsync(
            submitted.ContributionId!.Value, null, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);
        IngredientRevisionRecord centralDraft = await fixture.Database.Set<IngredientRevisionRecord>()
            .SingleAsync(value => value.Id == accepted.CentralRevisionId,
                TestContext.Current.CancellationToken);
        centralDraft.AllergenReviewState = (int)IngredientPropertyReviewState.Reviewed;
        centralDraft.IntoleranceReviewState = (int)IngredientPropertyReviewState.Reviewed;
        centralDraft.OriginReviewState = (int)IngredientPropertyReviewState.Reviewed;
        await fixture.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        fixture.Database.ChangeTracker.Clear();

        IngredientRevisionMutationResult published = await new IngredientRevisionWorkflowStore(fixture.Database)
            .PublishAsync(centralDraft.Id, centralDraft.RowVersion, local.ActorId, DateTimeOffset.UtcNow,
                TestContext.Current.CancellationToken);

        Assert.Equal(IngredientRevisionMutationStatus.Published, published.Status);
        IngredientIdentityRecord localIdentity = await fixture.Database.Set<IngredientIdentityRecord>()
            .AsNoTracking().SingleAsync(value => value.Id == local.IngredientId,
                TestContext.Current.CancellationToken);
        Assert.Equal((int)IngredientIdentityStatus.Archived, localIdentity.Status);
        Assert.Equal(accepted.CentralIngredientId, localIdentity.ReplacedByCentralIngredientId);
        Assert.NotNull(localIdentity.ReplacedAtUtc);
    }

    [Fact]
    public async Task Replacing_local_draft_archives_identity_and_records_central_target()
    {
        await using var fixture = await Fixture.CreateAsync();
        Seed central = await fixture.AddPublishedAsync(IngredientScopeType.Central, null, "Reis");
        Seed local = await fixture.AddDraftAsync(IngredientScopeType.Camp, Guid.NewGuid(), "Reis");
        var store = new IngredientCentralContributionStore(fixture.Database);

        IngredientContributionMutationResult result = await store.ReplaceAsync(
            local.RevisionId, central.RevisionId, local.ActorId, DateTimeOffset.UtcNow,
            TestContext.Current.CancellationToken);

        Assert.Equal(IngredientContributionMutationStatus.Replaced, result.Status);
        IngredientIdentityRecord identity = await fixture.Database.Set<IngredientIdentityRecord>()
            .AsNoTracking().SingleAsync(value => value.Id == local.IngredientId,
                TestContext.Current.CancellationToken);
        Assert.Equal((int)IngredientIdentityStatus.Archived, identity.Status);
        Assert.Equal(central.IngredientId, identity.ReplacedByCentralIngredientId);
        Assert.NotNull(identity.ReplacedAtUtc);
    }

    private sealed class Fixture(SqliteConnection connection, CateringDbContext database) : IAsyncDisposable
    {
        public CateringDbContext Database { get; } = database;

        public static async Task<Fixture> CreateAsync()
        {
            SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
            var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
            await connection.OpenAsync();
            var database = new CateringDbContext(
                new DbContextOptionsBuilder<CateringDbContext>().UseSqlite(connection).Options);
            await database.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            return new Fixture(connection, database);
        }

        public Task<Seed> AddPublishedAsync(IngredientScopeType scope, Guid? scopeId, string name) =>
            AddAsync(scope, scopeId, name, IngredientRevisionState.Published);

        public Task<Seed> AddDraftAsync(IngredientScopeType scope, Guid? scopeId, string name) =>
            AddAsync(scope, scopeId, name, IngredientRevisionState.Draft);

        private async Task<Seed> AddAsync(
            IngredientScopeType scope, Guid? scopeId, string name, IngredientRevisionState state)
        {
            Guid ingredientId = Guid.NewGuid();
            Guid revisionId = Guid.NewGuid();
            Guid actorId = Guid.NewGuid();
            var unit = new MeasurementUnit(Guid.NewGuid(), $"Gramm {ingredientId:N}",
                $"g-{ingredientId:N}"[..20], MeasurementDimension.Mass, 1m);
            var identity = new IngredientIdentityRecord
            {
                Id = ingredientId,
                ScopeType = (int)scope,
                ScopeId = scopeId,
                Status = (int)IngredientIdentityStatus.Active,
            };
            Database.AddRange(unit, identity, new IngredientRevisionRecord
            {
                Id = revisionId,
                IngredientId = ingredientId,
                RevisionNumber = 1,
                State = (int)state,
                Name = name,
                NormalizedName = name.ToUpperInvariant(),
                CategoryId = Guid.Parse("51111111-1111-1111-1111-000000000002"),
                BaseUnitId = unit.Id,
                AllergenReviewState = (int)IngredientPropertyReviewState.Reviewed,
                IntoleranceReviewState = (int)IngredientPropertyReviewState.Reviewed,
                OriginReviewState = (int)IngredientPropertyReviewState.Reviewed,
                RowVersion = 1,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                CreatedBy = actorId,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedBy = actorId,
            });
            await Database.SaveChangesAsync(TestContext.Current.CancellationToken);
            if (state == IngredientRevisionState.Published)
            {
                identity.CurrentPublishedRevisionId = revisionId;
                await Database.SaveChangesAsync(TestContext.Current.CancellationToken);
            }
            Database.ChangeTracker.Clear();
            return new Seed(ingredientId, revisionId, actorId);
        }

        public async ValueTask DisposeAsync()
        {
            await Database.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed record Seed(Guid IngredientId, Guid RevisionId, Guid ActorId);
}
