using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Catering.Domain;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class RecipeVariantConflictSummaryTests
{
    private static readonly ConflictReference Milk = new(ConflictType.Allergen, Guid.NewGuid());
    private static readonly MeasurementUnitSnapshot Gram = new(Guid.NewGuid(), "Gram", "g", MeasurementDimension.Mass, 1);
    private static IngredientPositionSnapshot Ingredient(Guid id, string name) => new(Guid.NewGuid(), null, 0,
        new IngredientSnapshotSource(id, name, [Milk]), 100, new IngredientUnitSnapshot(Gram, 1), ScalingMode.Linear,
        AgeGroupScalingMode.Inherit, null, []);
    private static RecipeSnapshot Recipe(IngredientPositionSnapshot[] ingredients, SubrecipePositionSnapshot[]? children = null) => new(
        RecipeSnapshotBuilder.CurrentSchemaVersion, "Recipe", null, null, null, RecipeType.PortionBased,
        new RecipeReferenceSnapshot(10, 1, null, null), null, false, [], [], ingredients, children ?? [], [Milk]);
    private static SubrecipePositionSnapshot Child(Guid id) => new(Guid.NewGuid(), null, 0, id, 1, null, null, [Milk], []);
    private static RecipeEditorIngredientReference Reference(Guid id, params string[] variants) => new(id, Guid.NewGuid(), 1,
        "Ingredient", IngredientScopeType.Central, [], [new RecipeEditorConflictReference(Milk.Type, Milk.Id, "Milk", variants)], null);

    [Fact]
    public void Reports_variants_for_all_conflicting_ingredients_in_nested_pinned_revisions()
    {
        Guid milk = Guid.NewGuid(), cream = Guid.NewGuid(), child = Guid.NewGuid();
        var nested = Recipe([Ingredient(cream, "Cream")]);
        var source = Recipe([Ingredient(milk, "Milk")], [Child(child)]);
        var result = RecipeVariantConflictSummary.Find(source, Milk, id => id == child ? nested : null,
            new Dictionary<Guid, RecipeEditorIngredientReference> { [milk] = Reference(milk, "Oat"), [cream] = Reference(cream, "Soy") });
        Assert.Equal(["Cream: Soy", "Milk: Oat"], result);
    }

    [Fact]
    public void A_single_uncovered_occurrence_prevents_a_false_solvable_label()
    {
        Guid milk = Guid.NewGuid(), cream = Guid.NewGuid(), child = Guid.NewGuid();
        var source = Recipe([Ingredient(milk, "Milk")], [Child(child)]);
        var result = RecipeVariantConflictSummary.Find(source, Milk, _ => Recipe([Ingredient(cream, "Cream")]),
            new Dictionary<Guid, RecipeEditorIngredientReference> { [milk] = Reference(milk, "Oat"), [cream] = Reference(cream) });
        Assert.Empty(result);
    }

    [Fact]
    public void Missing_recipe_or_ingredient_metadata_is_not_reported_as_covered()
    {
        Assert.Empty(RecipeVariantConflictSummary.Find(Recipe([], [Child(Guid.NewGuid())]), Milk, _ => null,
            new Dictionary<Guid, RecipeEditorIngredientReference>()));
        Assert.Empty(RecipeVariantConflictSummary.Find(Recipe([Ingredient(Guid.NewGuid(), "Missing")]), Milk, _ => null,
            new Dictionary<Guid, RecipeEditorIngredientReference>()));
    }

    [Fact]
    public void Cycles_are_not_reported_as_covered()
    {
        Guid id = Guid.NewGuid(); var source = Recipe([], [Child(id)]);
        Assert.Empty(RecipeVariantConflictSummary.Find(source, Milk, _ => source, new Dictionary<Guid, RecipeEditorIngredientReference>()));
    }
}
