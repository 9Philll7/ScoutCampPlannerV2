using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Catering.Domain;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class RecipeCalculatorTests
{
    [Fact]
    public void Calculates_linear_fixed_and_stepwise_positions_without_rounding()
    {
        Guid revisionId = Guid.NewGuid();
        RecipeSnapshot recipe = PortionRecipe(10m, true,
            Ingredient(1.25m, ScalingMode.Linear),
            Ingredient(3m, ScalingMode.Fixed),
            Ingredient(1m, ScalingMode.Stepwise, stepwise: new StepwiseScaling(10m, 2m)));

        RecipeCalculationResult result = Calculator((revisionId, recipe)).Calculate(
            new RecipeCalculationRequest(revisionId, 15m, 15m));

        Assert.Equal([1.875m, 3m, 4m], result.Ingredients.Select(value => value.Quantity));
    }

    [Fact]
    public void Applies_age_factor_only_where_configured()
    {
        Guid revisionId = Guid.NewGuid();
        RecipeSnapshot recipe = PortionRecipe(10m, true,
            Ingredient(10m, ageMode: AgeGroupScalingMode.Inherit),
            Ingredient(10m, ageMode: AgeGroupScalingMode.Apply),
            Ingredient(10m, ageMode: AgeGroupScalingMode.Ignore));

        RecipeCalculationResult result = Calculator((revisionId, recipe)).Calculate(
            new RecipeCalculationRequest(revisionId, 5m, 10m));

        Assert.Equal([5m, 5m, 10m], result.Ingredients.Select(value => value.Quantity));
    }

    [Fact]
    public void Expands_portion_subrecipe_without_reapplying_age_factor()
    {
        Guid rootId = Guid.NewGuid();
        Guid childId = Guid.NewGuid();
        RecipeSnapshot child = PortionRecipe(10m, true,
            Ingredient(10m, ageMode: AgeGroupScalingMode.Apply));
        RecipeSnapshot root = PortionRecipe(10m, true, subrecipes:
        [
            new SubrecipePositionSnapshot(Guid.NewGuid(), null, 0, childId, 5m, null, null, [], [])
        ]);

        RecipeCalculationResult result = Calculator((rootId, root), (childId, child)).Calculate(
            new RecipeCalculationRequest(rootId, 20m, 10m));

        Assert.Equal(10m, Assert.Single(result.Ingredients).Quantity);
        Assert.Equal([rootId, childId], result.Ingredients[0].RecipeRevisionPath);
    }

    [Fact]
    public void Converts_quantity_subrecipe_units()
    {
        Guid rootId = Guid.NewGuid();
        Guid childId = Guid.NewGuid();
        RecipeSnapshot child = QuantityRecipe(1m, Kilogram,
            Ingredient(4m));
        RecipeSnapshot root = PortionRecipe(10m, false, subrecipes:
        [
            new SubrecipePositionSnapshot(Guid.NewGuid(), null, 0, childId, null, 500m, Gram, [], [])
        ]);

        RecipeCalculationResult result = Calculator((rootId, root), (childId, child)).Calculate(
            new RecipeCalculationRequest(rootId, 20m, 20m));

        Assert.Equal(4m, Assert.Single(result.Ingredients).Quantity);
    }

    [Fact]
    public void Uses_selected_ingredient_replacement_and_propagates_its_conflicts()
    {
        Guid revisionId = Guid.NewGuid();
        Guid positionId = Guid.NewGuid();
        Guid replacementId = Guid.NewGuid();
        var conflict = new ConflictReference(ConflictType.Allergen, Guid.NewGuid());
        var ruleConflict = new ConflictReference(ConflictType.Intolerance, Guid.NewGuid());
        var replacementIngredient = new IngredientSnapshotSource(
            Guid.NewGuid(), "Ersatz", [conflict], Nutrition(100m));
        var replacement = new IngredientReplacementSnapshot(
            replacementId, replacementIngredient, 3m, new IngredientUnitSnapshot(Gram, 1m), [ruleConflict]);
        IngredientPositionSnapshot position = Ingredient(1m) with
        {
            Id = positionId,
            Replacements = [replacement],
        };

        RecipeCalculationResult result = Calculator((revisionId, PortionRecipe(10m, false, position))).Calculate(
            new RecipeCalculationRequest(revisionId, 20m, 20m,
                new Dictionary<Guid, Guid> { [positionId] = replacementId }));

        CalculatedIngredient ingredient = Assert.Single(result.Ingredients);
        Assert.Equal("Ersatz", ingredient.IngredientName);
        Assert.Equal(6m, ingredient.Quantity);
        Assert.Equal(replacementId, ingredient.AppliedReplacementRuleId);
        Assert.Equal([conflict, ruleConflict], result.Conflicts);
        Assert.True(result.Nutrition.IsComplete);
        Assert.Equal(6m, result.Nutrition.Total!.EnergyKilojoules);
    }

    [Fact]
    public void Calculates_complete_nutrition_totals_and_values_per_standard_portion()
    {
        Guid revisionId = Guid.NewGuid();
        IngredientPositionSnapshot grams = Ingredient(500m) with
        {
            Ingredient = NutritionIngredient("Reis", Nutrition(100m, fiber: 2m)),
        };
        IngredientPositionSnapshot kilograms = Ingredient(1m) with
        {
            Ingredient = NutritionIngredient("Bohnen", Nutrition(200m, fiber: 4m)),
            Unit = new IngredientUnitSnapshot(Kilogram, 1_000m),
        };

        RecipeCalculationResult result = Calculator((revisionId,
            PortionRecipe(10m, false, grams, kilograms))).Calculate(
                new RecipeCalculationRequest(revisionId, 20m, 20m));

        Assert.True(result.Nutrition.IsComplete);
        Assert.Empty(result.Nutrition.MissingContributions);
        Assert.Equal(5_000m, result.Nutrition.Total!.EnergyKilojoules);
        Assert.Equal(250m, result.Nutrition.PerStandardPortion!.EnergyKilojoules);
        Assert.Equal(100m, result.Nutrition.Total.FiberGrams);
        Assert.Equal(result.Nutrition.Total.EnergyKilojoules / 4.184m,
            result.Nutrition.Total.EnergyKilocalories);
    }

    [Fact]
    public void Marks_nutrition_incomplete_and_lists_every_missing_contribution()
    {
        Guid revisionId = Guid.NewGuid();
        IngredientPositionSnapshot missing = Ingredient(1m);
        IngredientPositionSnapshot unreviewed = Ingredient(1m) with
        {
            Ingredient = NutritionIngredient("Ungeprüft", Nutrition(100m) with
            {
                ReviewState = IngredientNutritionReviewState.Unreviewed,
            }),
        };

        RecipeCalculationResult result = Calculator((revisionId,
            PortionRecipe(10m, false, missing, unreviewed))).Calculate(
                new RecipeCalculationRequest(revisionId, 10m, 10m));

        Assert.False(result.Nutrition.IsComplete);
        Assert.Null(result.Nutrition.Total);
        Assert.Null(result.Nutrition.PerStandardPortion);
        Assert.Equal(
            [MissingNutritionReason.ProfileMissing, MissingNutritionReason.ProfileUnreviewed],
            result.Nutrition.MissingContributions.Select(value => value.Reason));
    }

    [Fact]
    public void Keeps_complete_core_nutrition_when_optional_fiber_is_unknown()
    {
        Guid revisionId = Guid.NewGuid();
        IngredientPositionSnapshot position = Ingredient(100m) with
        {
            Ingredient = NutritionIngredient("Zutat", Nutrition(100m, fiber: null)),
        };

        RecipeCalculationResult result = Calculator((revisionId,
            PortionRecipe(10m, false, position))).Calculate(
                new RecipeCalculationRequest(revisionId, 10m, 10m));

        Assert.True(result.Nutrition.IsComplete);
        Assert.Null(result.Nutrition.Total!.FiberGrams);
        Assert.Null(result.Nutrition.PerStandardPortion!.FiberGrams);
    }

    [Fact]
    public void Deserializes_legacy_snapshot_without_nutrition_data()
    {
        const string json = """
            {
              "schemaVersion": 1,
              "name": "Alt",
              "recipeType": "PortionBased",
              "reference": { "standardServings": 10, "standardPortionFactor": 1 },
              "defaultAgeGroupScalingApplies": false,
              "tags": [], "groups": [],
              "ingredientPositions": [{
                "id": "11111111-1111-1111-1111-111111111111", "sortOrder": 0,
                "ingredient": { "ingredientId": "22222222-2222-2222-2222-222222222222", "name": "Alt", "conflicts": [] },
                "quantity": 100,
                "unit": { "unit": { "unitId": "33333333-3333-3333-3333-333333333333", "name": "Gramm", "symbol": "g", "dimension": "Mass", "baseUnitFactor": 1 }, "referenceQuantityPerUnit": 1 },
                "scalingMode": "Linear", "ageGroupScaling": "Inherit", "replacements": []
              }],
              "subrecipePositions": [], "exposedConflicts": []
            }
            """;

        RecipeSnapshot snapshot = RecipeSnapshotBuilder.Deserialize(json);

        Assert.Null(Assert.Single(snapshot.IngredientPositions).Ingredient.Nutrition);
    }

    [Fact]
    public void Rejects_recipe_cycles_defensively()
    {
        Guid firstId = Guid.NewGuid();
        Guid secondId = Guid.NewGuid();
        RecipeSnapshot first = PortionRecipe(1m, false, subrecipes:
            [new SubrecipePositionSnapshot(Guid.NewGuid(), null, 0, secondId, 1m, null, null, [], [])]);
        RecipeSnapshot second = PortionRecipe(1m, false, subrecipes:
            [new SubrecipePositionSnapshot(Guid.NewGuid(), null, 0, firstId, 1m, null, null, [], [])]);

        Assert.Throws<InvalidOperationException>(() => Calculator((firstId, first), (secondId, second)).Calculate(
            new RecipeCalculationRequest(firstId, 1m, 1m)));
    }

    private static RecipeCalculator Calculator(params (Guid Id, RecipeSnapshot Recipe)[] recipes) =>
        new(new FakeSnapshotSource(recipes.ToDictionary(value => value.Id, value => value.Recipe)));

    private static IngredientPositionSnapshot Ingredient(
        decimal quantity,
        ScalingMode scalingMode = ScalingMode.Linear,
        AgeGroupScalingMode ageMode = AgeGroupScalingMode.Inherit,
        StepwiseScaling? stepwise = null) =>
        new(Guid.NewGuid(), null, 0, new IngredientSnapshotSource(Guid.NewGuid(), "Zutat", []),
            quantity, new IngredientUnitSnapshot(Gram, 1m), scalingMode, ageMode, stepwise, []);

    private static IngredientSnapshotSource NutritionIngredient(
        string name,
        IngredientNutritionSnapshot nutrition) =>
        new(Guid.NewGuid(), name, [], nutrition);

    private static IngredientNutritionSnapshot Nutrition(decimal energyKilojoules, decimal? fiber = 1m) =>
        new(100m, Gram, 100m, energyKilojoules, 10m, 2m, 20m, 5m, 6m, 0.5m, fiber,
            IngredientNutritionSourceType.OfficialDatabase, "Testquelle",
            IngredientNutritionReviewState.Reviewed, new DateOnly(2026, 9, 14));

    private static RecipeSnapshot PortionRecipe(
        decimal servings,
        bool defaultAgeFactor,
        params IngredientPositionSnapshot[] ingredients) =>
        PortionRecipe(servings, defaultAgeFactor, ingredients, []);

    private static RecipeSnapshot PortionRecipe(
        decimal servings,
        bool defaultAgeFactor,
        IngredientPositionSnapshot[]? ingredients = null,
        SubrecipePositionSnapshot[]? subrecipes = null) =>
        new(1, "Rezept", null, null, null, RecipeType.PortionBased,
            new RecipeReferenceSnapshot(servings, 1m, null, null), null, defaultAgeFactor,
            [], [], ingredients ?? [], subrecipes ?? [], []);

    private static RecipeSnapshot QuantityRecipe(
        decimal quantity,
        MeasurementUnitSnapshot unit,
        params IngredientPositionSnapshot[] ingredients) =>
        new(1, "Grundrezept", null, null, null, RecipeType.QuantityBased,
            new RecipeReferenceSnapshot(null, 1m, quantity, unit), null, null,
            [], [], ingredients, [], []);

    private static readonly MeasurementUnitSnapshot Gram =
        new(Guid.NewGuid(), "Gramm", "g", MeasurementDimension.Mass, 1m);

    private static readonly MeasurementUnitSnapshot Kilogram =
        new(Guid.NewGuid(), "Kilogramm", "kg", MeasurementDimension.Mass, 1_000m);

    private sealed class FakeSnapshotSource(IReadOnlyDictionary<Guid, RecipeSnapshot> recipes) : IRecipeSnapshotSource
    {
        public RecipeSnapshot GetRevision(Guid revisionId) => recipes[revisionId];
    }
}
