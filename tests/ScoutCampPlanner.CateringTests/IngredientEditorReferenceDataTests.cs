using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Catering.Infrastructure.Ingredients;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class IngredientEditorReferenceDataTests
{
    [Fact]
    public async Task Reference_data_contains_documented_catalogs_and_existing_editor_choices()
    {
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var database = new CateringDbContext(
            new DbContextOptionsBuilder<CateringDbContext>().UseSqlite(connection).Options);
        await database.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var category = new IngredientCategoryRecord
        {
            Id = Guid.NewGuid(), Code = "TEST", Name = "Test", NormalizedName = "TEST",
        };
        var unit = new MeasurementUnit(Guid.NewGuid(), "Gramm", "g", MeasurementDimension.Mass, 1m);
        database.AddRange(category, unit);
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);
        var store = new IngredientEditorReferenceDataStore(database);

        var result = await store.GetAsync(TestContext.Current.CancellationToken);

        Assert.Equal(29, result.Allergens.Count);
        Assert.Equal(14, result.Allergens.Count(value => value.IsEuMajorAllergen));
        var gluten = Assert.Single(result.Allergens, value => value.Code == "GLUTEN_CEREALS");
        Assert.Equal(7, result.Allergens.Count(value => value.ParentAllergenId == gluten.Id));
        var nuts = Assert.Single(result.Allergens, value => value.Code == "TREE_NUTS");
        Assert.Equal(8, result.Allergens.Count(value => value.ParentAllergenId == nuts.Id));
        Assert.Equal(10, result.Intolerances.Count);
        Assert.Contains(result.Intolerances, value => value.Code == "LACTOSE");
        Assert.Equal(19, result.Origins.Count);
        Assert.Equal(13, result.Origins.Count(value => value.IsAnimalOrigin));
        Assert.Contains(result.Origins, value => value.Code == "UNKNOWN_ORIGIN" && !value.IsAnimalOrigin);
        Assert.Equal(19, result.Categories.Count);
        Assert.Contains(result.Categories, value => value.Id == category.Id);
        Assert.Contains(result.Categories, value => value.Code == "CEREALS_GRAIN_PRODUCTS"
            && value.Name == "Getreide & Getreideprodukte");
        Assert.Contains(result.Categories, value => value.Code == "OTHER" && value.Name == "Sonstiges");
        Assert.Equal(unit.Id, Assert.Single(result.Units).Id);
    }
}
