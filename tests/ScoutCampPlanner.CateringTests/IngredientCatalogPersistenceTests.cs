using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Catering.Infrastructure.Ingredients;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class IngredientCatalogPersistenceTests
{
    [Fact]
    public async Task Complete_ingredient_catalog_graph_round_trips()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Guid ingredientId = Guid.NewGuid();
        var unit = new MeasurementUnit(Guid.NewGuid(), "Gramm", "g", MeasurementDimension.Mass, 1m);
        var ingredient = new BaseIngredient(ingredientId, IngredientScopeType.Central, null, "Mehl", "Österreich");
        var allergen = new Allergen(Guid.NewGuid(), "Gluten");
        var intolerance = new Intolerance(Guid.NewGuid(), "Glutensensitivität");
        var requirement = new DietaryRequirement(Guid.NewGuid(), "Vegan");

        fixture.Database.AddRange(unit, ingredient, allergen, intolerance, requirement);
        fixture.Database.AddRange(
            new IngredientVariant(Guid.NewGuid(), ingredientId, "Bio-Weizenmehl"),
            new IngredientUnitConversion(ingredientId, unit.Id, 1m),
            new BaseIngredientAllergen(ingredientId, allergen.Id),
            new BaseIngredientIntolerance(ingredientId, intolerance.Id),
            new BaseIngredientDietaryRequirement(ingredientId, requirement.Id));
        await fixture.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        fixture.Database.ChangeTracker.Clear();

        CancellationToken cancellationToken = TestContext.Current.CancellationToken;
        Assert.Equal("Mehl", (await fixture.Database.BaseIngredients.SingleAsync(cancellationToken)).Name);
        Assert.Equal("Bio-Weizenmehl", (await fixture.Database.IngredientVariants.SingleAsync(cancellationToken)).Name);
        Assert.Single(await fixture.Database.IngredientUnitConversions.ToArrayAsync(cancellationToken));
        Assert.Single(await fixture.Database.BaseIngredientAllergens.ToArrayAsync(cancellationToken));
        Assert.Single(await fixture.Database.BaseIngredientIntolerances.ToArrayAsync(cancellationToken));
        Assert.Single(await fixture.Database.BaseIngredientDietaryRequirements.ToArrayAsync(cancellationToken));
    }

    [Fact]
    public async Task Central_ingredient_name_is_unique_despite_null_owner()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        fixture.Database.BaseIngredients.Add(
            new BaseIngredient(Guid.NewGuid(), IngredientScopeType.Central, null, "Olivenöl"));
        await fixture.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        fixture.Database.BaseIngredients.Add(
            new BaseIngredient(Guid.NewGuid(), IngredientScopeType.Central, null, "  OLIVENÖL "));

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            fixture.Database.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Same_ingredient_name_is_allowed_for_different_tenants_only()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Guid firstTenantId = Guid.NewGuid();
        fixture.Database.BaseIngredients.AddRange(
            new BaseIngredient(Guid.NewGuid(), IngredientScopeType.Tenant, firstTenantId, "Gewürzmischung"),
            new BaseIngredient(Guid.NewGuid(), IngredientScopeType.Tenant, Guid.NewGuid(), "Gewürzmischung"));
        await fixture.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        fixture.Database.BaseIngredients.Add(
            new BaseIngredient(Guid.NewGuid(), IngredientScopeType.Tenant, firstTenantId, "GEWÜRZMISCHUNG"));

        await Assert.ThrowsAsync<DbUpdateException>(() =>
            fixture.Database.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Catalog_query_returns_complete_graph_and_respects_visibility_scope()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Guid tenantId = Guid.NewGuid();
        Guid otherTenantId = Guid.NewGuid();
        Guid campId = Guid.NewGuid();
        Guid centralId = Guid.NewGuid();
        var unit = new MeasurementUnit(Guid.NewGuid(), "Gramm", "g", MeasurementDimension.Mass, 1m);
        var allergen = new Allergen(Guid.NewGuid(), "Gluten");
        fixture.Database.AddRange(
            unit, allergen,
            new BaseIngredient(centralId, IngredientScopeType.Central, null, "Mehl", "Regional"),
            new BaseIngredient(Guid.NewGuid(), IngredientScopeType.Tenant, tenantId, "Hausgewürz"),
            new BaseIngredient(Guid.NewGuid(), IngredientScopeType.Tenant, otherTenantId, "Fremd"),
            new BaseIngredient(Guid.NewGuid(), IngredientScopeType.Camp, campId, "Lagerzutat"));
        fixture.Database.AddRange(
            new IngredientVariant(Guid.NewGuid(), centralId, "Dinkelmehl"),
            new IngredientUnitConversion(centralId, unit.Id, 1m),
            new BaseIngredientAllergen(centralId, allergen.Id));
        await fixture.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        var catalog = new IngredientCatalogStore(fixture.Database);

        var central = await catalog.ListCentralAsync(TestContext.Current.CancellationToken);
        var tenant = await catalog.ListTenantAsync(tenantId, TestContext.Current.CancellationToken);
        var camp = await catalog.ListCampAsync(tenantId, campId, TestContext.Current.CancellationToken);

        var flour = Assert.Single(central);
        Assert.Equal("Dinkelmehl", Assert.Single(flour.Variants).Name);
        Assert.Equal("g", Assert.Single(flour.Units).Symbol);
        Assert.Equal("Gluten", Assert.Single(flour.Conflicts).Name);
        Assert.Equal(2, tenant.Count);
        Assert.Equal(3, camp.Count);
        Assert.DoesNotContain(tenant, value => value.Name == "Fremd");
        Assert.Contains(camp, value => value.Name == "Lagerzutat");
    }

    [Fact]
    public async Task Camp_ingredient_creation_persists_variants_and_rejects_duplicate_name()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Guid campId = Guid.NewGuid();
        var store = new IngredientManagementStore(fixture.Database);
        var request = new ScoutCampPlanner.Catering.Application.Ingredients.CreateIngredientRequest(
            "  Lagerkäse  ", "  Aus der Region  ", ["Mild", "Würzig"]);

        var created = await store.CreateAsync(
            Guid.NewGuid(), IngredientScopeType.Camp, campId, request,
            TestContext.Current.CancellationToken);
        var duplicate = await store.CreateAsync(
            Guid.NewGuid(), IngredientScopeType.Camp, campId,
            request with { Name = "LAGERKÄSE" }, TestContext.Current.CancellationToken);

        Assert.Equal(ScoutCampPlanner.Catering.Application.Ingredients.IngredientMutationStatus.Created,
            created.Status);
        Assert.Equal("Lagerkäse", created.Ingredient!.Name);
        Assert.Equal("Aus der Region", created.Ingredient.OriginInformation);
        Assert.Equal(2, created.Ingredient.Variants.Count);
        Assert.Equal(ScoutCampPlanner.Catering.Application.Ingredients.IngredientMutationStatus.DuplicateName,
            duplicate.Status);
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

        public async ValueTask DisposeAsync()
        {
            await Database.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
