using ScoutCampPlanner.Catering.Domain;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class IngredientCatalogTests
{
    [Fact]
    public void Central_ingredient_has_no_owner_and_normalizes_whitespace_and_case()
    {
        var ingredient = new BaseIngredient(Guid.NewGuid(), IngredientScopeType.Central, null, "  Weiße   Bohnen ");

        Assert.Equal("Weiße Bohnen", ingredient.Name);
        Assert.Equal("WEIßE BOHNEN", ingredient.NormalizedName);
        Assert.Null(ingredient.ScopeId);
    }

    [Theory]
    [InlineData(IngredientScopeType.Tenant)]
    [InlineData(IngredientScopeType.Camp)]
    public void Local_ingredient_requires_owner(IngredientScopeType scope) =>
        Assert.Throws<ArgumentException>(() => new BaseIngredient(Guid.NewGuid(), scope, null, "Zutat"));

    [Fact]
    public void Central_ingredient_rejects_owner() =>
        Assert.Throws<ArgumentException>(() =>
            new BaseIngredient(Guid.NewGuid(), IngredientScopeType.Central, Guid.NewGuid(), "Zutat"));

    [Fact]
    public void Globally_compatible_units_convert_exactly()
    {
        var grams = new MeasurementUnit(Guid.NewGuid(), "Gramm", "g", MeasurementDimension.Mass, 1m);
        var kilograms = new MeasurementUnit(Guid.NewGuid(), "Kilogramm", "kg", MeasurementDimension.Mass, 1_000m);

        Assert.Equal(1_250m, kilograms.ConvertTo(1.25m, grams));
        Assert.Equal(1.25m, grams.ConvertTo(1_250m, kilograms));
    }

    [Fact]
    public void Units_of_different_dimensions_cannot_convert_directly()
    {
        var grams = new MeasurementUnit(Guid.NewGuid(), "Gramm", "g", MeasurementDimension.Mass, 1m);
        var liters = new MeasurementUnit(Guid.NewGuid(), "Liter", "l", MeasurementDimension.Volume, 1m);

        Assert.Throws<InvalidOperationException>(() => grams.ConvertTo(100m, liters));
    }

    [Fact]
    public void Ingredient_specific_conversion_supports_cross_dimension_amounts()
    {
        Guid ingredientId = Guid.NewGuid();
        var piece = new IngredientUnitConversion(ingredientId, Guid.NewGuid(), 60m);
        var grams = new IngredientUnitConversion(ingredientId, Guid.NewGuid(), 1m);

        Assert.Equal(120m, piece.ConvertTo(2m, grams));
        Assert.Equal(2m, grams.ConvertTo(120m, piece));
    }

    [Fact]
    public void Ingredient_specific_conversion_cannot_cross_ingredients()
    {
        var source = new IngredientUnitConversion(Guid.NewGuid(), Guid.NewGuid(), 1m);
        var target = new IngredientUnitConversion(Guid.NewGuid(), Guid.NewGuid(), 1m);

        Assert.Throws<InvalidOperationException>(() => source.ConvertTo(1m, target));
    }

    [Fact]
    public void Variant_is_owned_by_exactly_one_base_ingredient()
    {
        Guid ingredientId = Guid.NewGuid();
        var variant = new IngredientVariant(Guid.NewGuid(), ingredientId, "  Bio   Hausmarke ");

        Assert.Equal(ingredientId, variant.BaseIngredientId);
        Assert.Equal("Bio Hausmarke", variant.Name);
    }

    [Fact]
    public void Conflict_catalogs_use_stable_typed_entries()
    {
        var allergen = new Allergen(Guid.NewGuid(), " Gluten ");
        var intolerance = new Intolerance(Guid.NewGuid(), "Laktose");
        var requirement = new DietaryRequirement(Guid.NewGuid(), "Vegan");

        Assert.Equal("GLUTEN", allergen.NormalizedName);
        Assert.Equal("LAKTOSE", intolerance.NormalizedName);
        Assert.Equal("VEGAN", requirement.NormalizedName);
    }
}
