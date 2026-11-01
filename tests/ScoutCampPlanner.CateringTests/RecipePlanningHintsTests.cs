using ScoutCampPlanner.Catering.Application.MealPlanning;
using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Catering.Domain;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class RecipePlanningHintsTests
{
    private static RecipePlanningHints Hints(SupplySolutionTests.Fixture f, Guid revision) =>
        new RecipePlanningHintsProjector(f).Build(revision,
            [new(f.Allergen, "MILK", "Milch")], [new(f.Diet, "", "Vegan")],
            [new(f.Substance, "LACTOSE", "Laktose")], DateTimeOffset.UnixEpoch);

    [Fact]
    public void Standard_recipe_explains_allergens_excluded_diet_and_quantity_dependent_substance()
    {
        var f = new SupplySolutionTests.Fixture();
        var revision = f.Recipe(f.Position(f.Ingredient(IngredientPropertyState.Contains, f.Animal, 3)));
        var result = Hints(f, revision);
        Assert.Equal("PublishedStandard", result.Basis);
        Assert.Equal(revision, result.RecipeRevisionId);
        Assert.Equal(DateTimeOffset.UnixEpoch, result.EvaluatedAtUtc);
        Assert.False(result.HasUnknownData);
        Assert.Equal("Contains", result.Hints.Single(hint => hint.Kind == "Allergen").State);
        Assert.Equal("Excluded", result.Hints.Single(hint => hint.Kind == "Diet").State);
        Assert.Equal("QuantityDependent", result.Hints.Single(hint => hint.Kind == "Substance").State);
        Assert.Equal("Unavailable", result.Hints.Single(hint => hint.Kind == "Allergen").Adjustment);
        Assert.Equal("Unknown", result.Hints.Single(hint => hint.Kind == "Substance").Adjustment);
        Assert.All(result.Hints, hint => Assert.All(hint.Evidence, evidence => {
            Assert.Equal("Dummy", evidence.IngredientName);
            Assert.StartsWith(revision.ToString("N"), evidence.PositionPath);
        }));
    }

    [Fact]
    public void Missing_substance_data_never_becomes_a_free_from_label()
    {
        var f = new SupplySolutionTests.Fixture();
        var revision = f.Recipe(f.Position(f.Ingredient(IngredientPropertyState.DoesNotContain, f.Plant)));
        var result = Hints(f, revision);
        Assert.True(result.HasUnknownData);
        Assert.Equal("Unknown", result.Hints.Single(hint => hint.Kind == "Substance").State);
        Assert.Equal("Compatible", result.Hints.Single(hint => hint.Kind == "Diet").State);
        Assert.Equal("NotContained", result.Hints.Single(hint => hint.Kind == "Allergen").State);
    }

    [Fact]
    public void Pinned_subrecipe_ingredients_are_included_without_selecting_a_newer_revision()
    {
        var f = new SupplySolutionTests.Fixture();
        var child = f.Recipe(f.Position(f.Ingredient(IngredientPropertyState.Contains, f.Animal, 2)));
        var root = f.Recipe();
        f.Recipes[root] = f.Recipes[root] with { SubrecipePositions = [new SubrecipePositionSnapshot(Guid.NewGuid(), null, 0, child, 1, null, null, [], [])] };
        var other = f.Recipe(f.Position(f.Ingredient(IngredientPropertyState.DoesNotContain, f.Plant, 0)));
        var result = Hints(f, root);
        Assert.Equal("Contains", result.Hints.Single(hint => hint.Kind == "Allergen").State);
        Assert.Contains(child.ToString("N"), Assert.Single(result.Hints.Single(hint => hint.Kind == "Allergen").Evidence).PositionPath);
        Assert.Equal("NotContained", Hints(f, other).Hints.Single(hint => hint.Kind == "Allergen").State);
    }

    [Fact]
    public void Empty_recipe_and_missing_diet_policy_are_incomplete_not_compatible()
    {
        var f = new SupplySolutionTests.Fixture();
        Assert.True(Hints(f, f.Recipe()).HasUnknownData);
        var revision = f.Recipe(f.Position(f.Ingredient(IngredientPropertyState.DoesNotContain, f.Plant, 0)));
        f.DietRules[(f.Diet, f.Plant)] = null;
        Assert.Equal("Unknown", Hints(f, revision).Hints.Single(hint => hint.Kind == "Diet").State);
    }

    [Fact]
    public void Known_presence_is_retained_when_another_ingredient_has_missing_data()
    {
        var f = new SupplySolutionTests.Fixture();
        var first = f.Position(f.Ingredient(IngredientPropertyState.Contains, f.Animal, 3));
        var second = f.Position(f.Ingredient(IngredientPropertyState.DoesNotContain, f.Plant)) with { SortOrder = 1 };
        var result = Hints(f, f.Recipe(first, second));
        Assert.True(result.HasUnknownData);
        var substance = result.Hints.Single(hint => hint.Kind == "Substance");
        Assert.Equal("QuantityDependent", substance.State);
        Assert.Contains("teilweise", substance.Explanation);
        Assert.Equal(2, substance.Evidence.Count);
    }

    [Theory]
    [InlineData(1, true, false, "Available")]
    [InlineData(2, true, false, "Available")]
    [InlineData(1, false, false, "Unavailable")]
    [InlineData(1, true, true, "Unavailable")]
    public void Adjustment_requires_a_complete_active_solution_not_partial_candidates(int count, bool active, bool partial, string expected)
    {
        var f = new SupplySolutionTests.Fixture();
        var variants = Enumerable.Range(0, count).Select(index => new IngredientVariantDraftContent(Guid.NewGuid(), $"free{index}", $"Milk free {index}", active, index,
            allergenOverrides: [new(f.Allergen, IngredientPropertyState.DoesNotContain, IngredientPropertySource.ManuallyVerified)])).ToArray();
        var first = f.Position(f.Ingredient(IngredientPropertyState.Contains, f.Plant, 0, variants));
        var second = f.Position(f.Ingredient(IngredientPropertyState.Contains, f.Plant, 0)) with { SortOrder = 1 };
        var recipe = partial ? f.Recipe(first, second) : f.Recipe(first);
        var result = Hints(f, recipe);
        Assert.Equal(expected, result.Hints.Single(hint => hint.Kind == "Allergen").Adjustment);
        Assert.Equal("Contains", result.Hints.Single(hint => hint.Kind == "Allergen").State); // Default recipe is not silently changed.
    }
}
