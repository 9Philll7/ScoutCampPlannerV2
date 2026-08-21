using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Catering.Infrastructure.Recipes;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class RecipePublisherTests
{
    [Fact]
    public async Task Warning_requires_acknowledgement_and_published_snapshot_normalizes_standard_servings()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Guid ingredientId = Guid.NewGuid();
        Guid unitId = Guid.NewGuid();
        fixture.Database.AddRange(
            new BaseIngredient(ingredientId, IngredientScopeType.Tenant, fixture.TenantId, "Reis"),
            new MeasurementUnit(unitId, "Gramm", "g", MeasurementDimension.Mass, 1m),
            new IngredientUnitConversion(ingredientId, unitId, 1m));
        await fixture.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        var draft = new RecipeDraft(
            Guid.NewGuid(), RecipeScopeType.Tenant, fixture.TenantId, RecipeType.PortionBased, "Reisgericht");
        draft.SetDetails("Beschreibung", null, null);
        draft.ConfigurePortionReference(
            20m, true, new AuthoringStageSnapshot(Guid.NewGuid(), "Biber", 0.5m));
        draft.AddIngredientPosition(new RecipeIngredientPosition(
            Guid.NewGuid(), draft.Id, null, ingredientId, 2_000m, unitId, 0));
        await fixture.Store.CreateAsync(
            draft, fixture.UserId, fixture.Now, TestContext.Current.CancellationToken);

        RecipePublicationResult pending = await fixture.Publisher.PublishAsync(
            draft.Id, 0, fixture.UserId, fixture.Now, acknowledgeWarnings: false,
            cancellationToken: TestContext.Current.CancellationToken);
        RecipePublicationResult published = await fixture.Publisher.PublishAsync(
            draft.Id, 0, fixture.UserId, fixture.Now, acknowledgeWarnings: true,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(RecipePublicationStatus.WarningAcknowledgementRequired, pending.Status);
        Assert.Equal(RecipePublicationStatus.Published, published.Status);
        Assert.Equal(RecipeStatus.Active, published.CurrentDraft!.Status);
        Assert.Equal(1, published.CurrentDraft.DraftVersion);
        RecipeSnapshot snapshot = RecipeSnapshotBuilder.Deserialize(published.Revision!.SnapshotJson);
        Assert.Equal(10m, snapshot.Reference.StandardServings);
        Assert.Equal(1m, snapshot.Reference.StandardPortionFactor);
        Assert.Equal(20m, snapshot.AuthoringStage!.EnteredServings);
        Assert.Equal(0.5m, snapshot.AuthoringStage.Factor);
        RecipeRevisionWarningRecord warning = await fixture.Database.Set<RecipeRevisionWarningRecord>()
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(RecipeValidationCodes.SourceMissing, warning.WarningCode);
        Assert.Equal(fixture.UserId, warning.AcknowledgedBy);

        published.CurrentDraft.SetDetails("Beschreibung", "Erprobte Quelle", null);
        RecipeDraftSaveResult saved = await fixture.Store.SaveAsync(
            published.CurrentDraft, 1, fixture.UserId, fixture.Now.AddMinutes(1),
            TestContext.Current.CancellationToken);
        RecipePublicationResult secondRevision = await fixture.Publisher.PublishAsync(
            draft.Id, saved.CurrentDraft!.DraftVersion, fixture.UserId, fixture.Now.AddMinutes(2),
            acknowledgeWarnings: false, cancellationToken: TestContext.Current.CancellationToken);
        Assert.Equal(RecipePublicationStatus.Published, secondRevision.Status);
        Assert.Equal(2, secondRevision.Revision!.RevisionNumber);
    }

    [Fact]
    public async Task Publication_errors_create_no_revision()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var draft = new RecipeDraft(Guid.NewGuid(), RecipeScopeType.Central, null, RecipeType.PortionBased);
        await fixture.Store.CreateAsync(
            draft, fixture.UserId, fixture.Now, TestContext.Current.CancellationToken);

        RecipePublicationResult result = await fixture.Publisher.PublishAsync(
            draft.Id, 0, fixture.UserId, fixture.Now, acknowledgeWarnings: true,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(RecipePublicationStatus.ValidationFailed, result.Status);
        Assert.NotEmpty(result.Validation!.Errors);
        Assert.Empty(await fixture.Database.Set<RecipeRevisionRecord>().ToArrayAsync(
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Publication_rejects_stale_version_without_new_revision()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        var draft = new RecipeDraft(
            Guid.NewGuid(), RecipeScopeType.Central, null, RecipeType.PortionBased, "Entwurf");
        await fixture.Store.CreateAsync(
            draft, fixture.UserId, fixture.Now, TestContext.Current.CancellationToken);
        draft.SetName("Aktueller Entwurf");
        await fixture.Store.SaveAsync(
            draft, 0, fixture.UserId, fixture.Now, TestContext.Current.CancellationToken);

        RecipePublicationResult result = await fixture.Publisher.PublishAsync(
            draft.Id, 0, fixture.UserId, fixture.Now, acknowledgeWarnings: true,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(RecipePublicationStatus.VersionConflict, result.Status);
        Assert.Equal(1, result.CurrentDraft!.DraftVersion);
        Assert.Empty(await fixture.Database.Set<RecipeRevisionRecord>().ToArrayAsync(
            TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Archived_published_recipe_reactivates_as_active_and_keeps_history()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Guid ingredientId = Guid.NewGuid();
        Guid unitId = Guid.NewGuid();
        fixture.Database.AddRange(
            new BaseIngredient(ingredientId, IngredientScopeType.Tenant, fixture.TenantId, "Reis"),
            new MeasurementUnit(unitId, "Gramm", "g", MeasurementDimension.Mass, 1m),
            new IngredientUnitConversion(ingredientId, unitId, 1m));
        await fixture.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        var draft = new RecipeDraft(
            Guid.NewGuid(), RecipeScopeType.Tenant, fixture.TenantId, RecipeType.PortionBased, "Reisgericht");
        draft.SetDetails("Beschreibung", "Quelle", null);
        draft.ConfigurePortionReference(10m, true);
        draft.AddIngredientPosition(new RecipeIngredientPosition(
            Guid.NewGuid(), draft.Id, null, ingredientId, 1_000m, unitId, 0));
        await fixture.Store.CreateAsync(
            draft, fixture.UserId, fixture.Now, TestContext.Current.CancellationToken);
        RecipePublicationResult published = await fixture.Publisher.PublishAsync(
            draft.Id, 0, fixture.UserId, fixture.Now, acknowledgeWarnings: true,
            cancellationToken: TestContext.Current.CancellationToken);

        RecipeLifecycleResult archived = await fixture.Store.ArchiveAsync(
            draft.Id, 1, fixture.UserId, fixture.Now.AddMinutes(1), TestContext.Current.CancellationToken);
        RecipeLifecycleResult reactivated = await fixture.Store.ReactivateAsync(
            draft.Id, 2, fixture.UserId, fixture.Now.AddMinutes(2), TestContext.Current.CancellationToken);

        Assert.Equal(RecipePublicationStatus.Published, published.Status);
        Assert.Equal(RecipeStatus.Archived, archived.CurrentDraft!.Status);
        Assert.Equal(RecipeStatus.Active, reactivated.CurrentDraft!.Status);
        Assert.Equal(3, reactivated.CurrentDraft.DraftVersion);
        Assert.Single(await fixture.Database.Set<RecipeRevisionRecord>().ToArrayAsync(
            TestContext.Current.CancellationToken));

        RecipeLifecycleResult reset = await fixture.Store.ResetToDraftAsync(
            draft.Id, 3, fixture.UserId, fixture.Now.AddMinutes(3), TestContext.Current.CancellationToken);
        RecipePublicationResult republished = await fixture.Publisher.PublishAsync(
            draft.Id, 4, fixture.UserId, fixture.Now.AddMinutes(4), acknowledgeWarnings: true,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(RecipeStatus.Draft, reset.CurrentDraft!.Status);
        Assert.Equal(RecipePublicationStatus.Published, republished.Status);
        Assert.Equal(1, republished.Revision!.RevisionNumber);
    }

    [Fact]
    public async Task Reset_to_draft_is_blocked_when_revision_has_a_derived_recipe()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Guid ingredientId = Guid.NewGuid();
        Guid unitId = Guid.NewGuid();
        fixture.Database.AddRange(
            new BaseIngredient(ingredientId, IngredientScopeType.Tenant, fixture.TenantId, "Nudeln"),
            new MeasurementUnit(unitId, "Gramm", "g", MeasurementDimension.Mass, 1m),
            new IngredientUnitConversion(ingredientId, unitId, 1m));
        await fixture.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        var source = new RecipeDraft(
            Guid.NewGuid(), RecipeScopeType.Tenant, fixture.TenantId, RecipeType.PortionBased, "Nudelgericht");
        source.SetDetails("Beschreibung", "Quelle", null);
        source.ConfigurePortionReference(10m, true);
        source.AddIngredientPosition(new RecipeIngredientPosition(
            Guid.NewGuid(), source.Id, null, ingredientId, 1_000m, unitId, 0));
        await fixture.Store.CreateAsync(
            source, fixture.UserId, fixture.Now, TestContext.Current.CancellationToken);
        RecipePublicationResult published = await fixture.Publisher.PublishAsync(
            source.Id, 0, fixture.UserId, fixture.Now, acknowledgeWarnings: true,
            cancellationToken: TestContext.Current.CancellationToken);
        var derived = new RecipeDraft(
            Guid.NewGuid(), RecipeScopeType.Tenant, fixture.TenantId, RecipeType.PortionBased, "Nudelvariante");
        await fixture.Store.CreateDerivedAsync(
            derived, new RecipeDraftLineage(source.Id, published.Revision!.Id), fixture.UserId,
            fixture.Now.AddMinutes(1), TestContext.Current.CancellationToken);

        RecipeLifecycleResult result = await fixture.Store.ResetToDraftAsync(
            source.Id, 1, fixture.UserId, fixture.Now.AddMinutes(2), TestContext.Current.CancellationToken);

        Assert.Equal(RecipeLifecycleStatus.ReferenceBlocked, result.Status);
        Assert.Equal(RecipeStatus.Active, result.CurrentDraft!.Status);
        Assert.Single(await fixture.Database.Set<RecipeRevisionRecord>().ToArrayAsync(
            TestContext.Current.CancellationToken));
    }

    private sealed class DatabaseFixture(
        SqliteConnection connection,
        CateringDbContext database,
        RecipeDraftStore store,
        RecipePublisher publisher) : IAsyncDisposable
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid TenantId { get; } = Guid.NewGuid();
        public DateTimeOffset Now { get; } = DateTimeOffset.UtcNow;
        public CateringDbContext Database { get; } = database;
        public RecipeDraftStore Store { get; } = store;
        public RecipePublisher Publisher { get; } = publisher;

        public static async Task<DatabaseFixture> CreateAsync()
        {
            SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
            var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            var database = new CateringDbContext(
                new DbContextOptionsBuilder<CateringDbContext>().UseSqlite(connection).Options);
            await database.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            var store = new RecipeDraftStore(database);
            var references = new EfRecipeReferences(database);
            var publisher = new RecipePublisher(
                database, store, new RecipePublicationValidator(references), new RecipeSnapshotBuilder(references));
            return new DatabaseFixture(connection, database, store, publisher);
        }

        public async ValueTask DisposeAsync()
        {
            await Database.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
