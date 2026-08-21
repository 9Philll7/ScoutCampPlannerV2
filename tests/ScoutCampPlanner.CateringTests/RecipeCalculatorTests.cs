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
        var replacementIngredient = new IngredientSnapshotSource(Guid.NewGuid(), "Ersatz", [conflict]);
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
