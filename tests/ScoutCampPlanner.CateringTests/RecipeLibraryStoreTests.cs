using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Catering.Infrastructure.Recipes;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class RecipeLibraryStoreTests
{
    [Fact]
    public async Task Tenant_library_accepts_only_central_revision_and_deduplicates_it()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Guid centralRevision = await fixture.PublishAsync(RecipeScopeType.Central, null, "Zentral");
        Guid tenantRevision = await fixture.PublishAsync(RecipeScopeType.Tenant, fixture.TenantId, "Mandant");

        RecipeLibraryMutationResult added = await fixture.Libraries.AddCentralRevisionToTenantAsync(
            Guid.NewGuid(), fixture.TenantId, centralRevision, fixture.UserId, fixture.Now,
            TestContext.Current.CancellationToken);
        RecipeLibraryMutationResult duplicate = await fixture.Libraries.AddCentralRevisionToTenantAsync(
            Guid.NewGuid(), fixture.TenantId, centralRevision, fixture.UserId, fixture.Now,
            TestContext.Current.CancellationToken);
        RecipeLibraryMutationResult invalid = await fixture.Libraries.AddCentralRevisionToTenantAsync(
            Guid.NewGuid(), fixture.TenantId, tenantRevision, fixture.UserId, fixture.Now,
            TestContext.Current.CancellationToken);

        Assert.Equal(RecipeLibraryMutationStatus.Added, added.Status);
        Assert.Equal(RecipeLibraryMutationStatus.AlreadyExists, duplicate.Status);
        Assert.Equal(RecipeLibraryMutationStatus.InvalidSourceScope, invalid.Status);
        TenantRecipeLibraryEntry entry = Assert.Single(await fixture.Libraries.ListTenantEntriesAsync(
            fixture.TenantId, TestContext.Current.CancellationToken));
        Assert.Equal(centralRevision, entry.SourceId);
        Assert.Equal(RecipeLibraryEntryType.UpstreamRevision, entry.Type);
    }

    [Fact]
    public async Task Camp_library_accepts_central_and_tenant_but_not_camp_revision()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Guid centralRevision = await fixture.PublishAsync(RecipeScopeType.Central, null, "Zentral");
        Guid tenantRevision = await fixture.PublishAsync(RecipeScopeType.Tenant, fixture.TenantId, "Mandant");
        Guid campRevision = await fixture.PublishAsync(RecipeScopeType.Camp, fixture.CampId, "Lager");

        RecipeLibraryMutationResult central = await fixture.Libraries.AddUpstreamRevisionToCampAsync(
            Guid.NewGuid(), fixture.CampId, centralRevision, fixture.UserId, fixture.Now,
            TestContext.Current.CancellationToken);
        RecipeLibraryMutationResult tenant = await fixture.Libraries.AddUpstreamRevisionToCampAsync(
            Guid.NewGuid(), fixture.CampId, tenantRevision, fixture.UserId, fixture.Now,
            TestContext.Current.CancellationToken);
        RecipeLibraryMutationResult invalid = await fixture.Libraries.AddUpstreamRevisionToCampAsync(
            Guid.NewGuid(), fixture.CampId, campRevision, fixture.UserId, fixture.Now,
            TestContext.Current.CancellationToken);

        Assert.Equal(RecipeLibraryMutationStatus.Added, central.Status);
        Assert.Equal(RecipeLibraryMutationStatus.Added, tenant.Status);
        Assert.Equal(RecipeLibraryMutationStatus.InvalidSourceScope, invalid.Status);
        CampRecipeLibraryEntry[] entries = (await fixture.Libraries.ListCampEntriesAsync(
            fixture.CampId, TestContext.Current.CancellationToken)).ToArray();
        Assert.Equal(2, entries.Length);
        Assert.Contains(entries, value => value.SourceId == centralRevision && value.UpstreamScope == RecipeScopeType.Central);
        Assert.Contains(entries, value => value.SourceId == tenantRevision && value.UpstreamScope == RecipeScopeType.Tenant);
    }

    private sealed class DatabaseFixture(
        SqliteConnection connection,
        CateringDbContext database,
        RecipeDraftStore drafts,
        RecipePublisher publisher,
        RecipeLibraryStore libraries) : IAsyncDisposable
    {
        public Guid UserId { get; } = Guid.NewGuid();
        public Guid TenantId { get; } = Guid.NewGuid();
        public Guid CampId { get; } = Guid.NewGuid();
        public DateTimeOffset Now { get; } = DateTimeOffset.UtcNow;
        public RecipeLibraryStore Libraries { get; } = libraries;

        public static async Task<DatabaseFixture> CreateAsync()
        {
            SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
            var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            var database = new CateringDbContext(
                new DbContextOptionsBuilder<CateringDbContext>().UseSqlite(connection).Options);
            await database.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            var drafts = new RecipeDraftStore(database);
            var references = new EfRecipeReferences(database);
            return new DatabaseFixture(
                connection, database, drafts,
                new RecipePublisher(
                    database, drafts, new RecipePublicationValidator(references),
                    new RecipeSnapshotBuilder(references)),
                new RecipeLibraryStore(database));
        }

        public async Task<Guid> PublishAsync(RecipeScopeType scope, Guid? scopeId, string name)
        {
            Guid ingredientId = Guid.NewGuid();
            Guid unitId = Guid.NewGuid();
            IngredientScopeType ingredientScope = scope switch
            {
                RecipeScopeType.Central => IngredientScopeType.Central,
                RecipeScopeType.Tenant => IngredientScopeType.Tenant,
                RecipeScopeType.Camp => IngredientScopeType.Camp,
                _ => throw new ArgumentOutOfRangeException(nameof(scope)),
            };
            database.AddRange(
                new BaseIngredient(ingredientId, ingredientScope, scopeId, $"Zutat {name}"),
                new MeasurementUnit(unitId, $"Gramm {name}", $"g{name[0]}", MeasurementDimension.Mass, 1m),
                new IngredientUnitConversion(ingredientId, unitId, 1m));
            await database.SaveChangesAsync(TestContext.Current.CancellationToken);
            var draft = new RecipeDraft(Guid.NewGuid(), scope, scopeId, RecipeType.PortionBased, name);
            draft.SetDetails("Beschreibung", "Quelle", null);
            draft.ConfigurePortionReference(10m, true);
            draft.AddIngredientPosition(new RecipeIngredientPosition(
                Guid.NewGuid(), draft.Id, null, ingredientId, 1_000m, unitId, 0));
            await drafts.CreateAsync(draft, UserId, Now, TestContext.Current.CancellationToken);
            RecipePublicationResult result = await publisher.PublishAsync(
                draft.Id, 0, UserId, Now, acknowledgeWarnings: true,
                cancellationToken: TestContext.Current.CancellationToken);
            return result.Revision!.Id;
        }

        public async ValueTask DisposeAsync()
        {
            await database.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
