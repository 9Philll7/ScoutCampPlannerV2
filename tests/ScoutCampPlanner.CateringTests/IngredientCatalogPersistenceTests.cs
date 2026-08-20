using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure;
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
