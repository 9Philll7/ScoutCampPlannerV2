using ScoutCampPlanner.Catering.Application.MealPlanning;
using ScoutCampPlanner.Catering.Domain;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class SupplyPreferenceContractTests
{
    [Fact]
    public void Together_prefers_an_unchanged_recipe_over_its_unnecessary_adjustment_for_the_same_resolvable_batch()
    {
        var f = new SupplySolutionTests.Fixture();
        var recipe = f.Recipe(f.Position(f.Ingredient(IngredientPropertyState.Contains, f.Plant)) with {
            Replacements = [f.Replacement(f.Ingredient(IngredientPropertyState.DoesNotContain, f.Plant))] });
        var impossible = new RequirementSignature(Guid.NewGuid(), [f.Allergen], []);
        var result = new OperationalSupplyCalculator(f).Calculate([new(null, recipe, 3, [])],
            [new(new(null, [], []), 2), new(impossible, 1)], [], CookingTogetherPreference.AlwaysTogether, TestContext.Current.CancellationToken);
        var solved = Assert.Single(result.Solutions);
        Assert.Equal(2, solved.Portions); Assert.Empty(solved.PositionSelections);
        Assert.Empty(result.Decisions!);
        Assert.NotEmpty(result.Problems);
        Assert.All(result.Problems, value => Assert.Equal(impossible.Key, value.RequirementGroupKey));
    }

    [Fact]
    public void Complete_own_supply_never_fills_missing_portions_from_the_plan_and_roundtrips()
    {
        var f = new SupplySolutionTests.Fixture();
        var recipe = f.Recipe(f.Position(f.Ingredient(IngredientPropertyState.DoesNotContain, f.Plant)));
        var normal = new RequirementSignature(null, [], []);
        var allergy = new RequirementSignature(null, [f.Allergen], []);
        var manual = new SupplySolution(Guid.NewGuid(), recipe, null, 2,
            new Dictionary<string, ScoutCampPlanner.Catering.Application.Recipes.RecipePositionSelection>(),
            [new(normal.Key, 2)], SupplyDecisionSource.Manual);
        var persisted = ManualSupplyDocument.Read(new ManualSupplyDocument([manual], [], true).Serialize());
        Assert.True(persisted.CompleteOwnSupply);
        var calculator = new OperationalSupplyCalculator(f);
        var own = calculator.Calculate([new(null, recipe, 5, [])], [new(normal, 2), new(allergy, 3)],
            persisted.Solutions, CookingTogetherPreference.Ask, TestContext.Current.CancellationToken, persisted.ScenarioSelections, persisted.CompleteOwnSupply);
        Assert.Single(own.Solutions); Assert.Equal(2, own.Solutions[0].Portions);
        Assert.Contains(own.Problems, value => value.Code == MealPlanningProblemCode.RequirementGroupUndercovered && value.RequirementGroupKey == allergy.Key);
        Assert.Empty(own.PreferenceOptions!); Assert.Empty(own.Decisions!); Assert.Empty(own.CommonSupplyChoices!);
        var partial = calculator.Calculate([new(null, recipe, 5, [])], [new(normal, 2), new(allergy, 3)],
            [manual], CookingTogetherPreference.Ask, TestContext.Current.CancellationToken);
        Assert.Equal(5, partial.Solutions.Sum(value => value.Portions)); Assert.Empty(partial.Problems);
        Assert.False(ManualSupplyDocument.Read(new ManualSupplyDocument([manual], []).Serialize()).CompleteOwnSupply);
    }

    [Theory]
    [InlineData(CookingTogetherPreference.AlwaysTogether, CookingTogetherPreference.NeverTogether, 2)]
    [InlineData(CookingTogetherPreference.NeverTogether, CookingTogetherPreference.AlwaysTogether, 1)]
    [InlineData(CookingTogetherPreference.AlwaysTogether, CookingTogetherPreference.Ask, 0)]
    [InlineData(CookingTogetherPreference.NeverTogether, CookingTogetherPreference.Ask, 0)]
    public void Meal_preference_overrides_every_unit_default(CookingTogetherPreference unitDefault, CookingTogetherPreference selected, int count)
    {
        var f = new SupplySolutionTests.Fixture();
        var recipe = f.Recipe(f.Position(f.Ingredient(IngredientPropertyState.Contains, f.Plant)) with {
            Replacements = [f.Replacement(f.Ingredient(IngredientPropertyState.DoesNotContain, f.Plant))] });
        var persisted = ManualSupplyDocument.Read(new ManualSupplyDocument([], [new(null, selected)]).Serialize());
        var result = new OperationalSupplyCalculator(f).Calculate([new(null, recipe, 10, [])],
            [new(new(null, [], []), 7), new(new(null, [f.Allergen], []), 3)], persisted.Solutions, unitDefault,
            TestContext.Current.CancellationToken, persisted.ScenarioSelections);
        Assert.Equal(count, result.Solutions.Count);
        Assert.Single(result.PreferenceOptions!);
        Assert.Equal(selected == CookingTogetherPreference.Ask, result.CommonSupplyChoices!.Count == 1);
        if (selected != CookingTogetherPreference.Ask) Assert.Empty(result.Problems);
        var defaults = new OperationalSupplyCalculator(f).Calculate([new(null, recipe, 10, [])],
            [new(new(null, [], []), 7), new(new(null, [f.Allergen], []), 3)], [], unitDefault, TestContext.Current.CancellationToken);
        Assert.Equal(unitDefault == CookingTogetherPreference.AlwaysTogether ? 1 : 2, defaults.Solutions.Count);
        Assert.Empty(defaults.CommonSupplyChoices!); Assert.Single(defaults.PreferenceOptions!);
    }
    [Theory]
    [InlineData(CookingTogetherPreference.AlwaysTogether)]
    [InlineData(CookingTogetherPreference.NeverTogether)]
    public void Ask_uses_identical_scenarios_without_a_redundant_question(CookingTogetherPreference selected)
    {
        var f = new SupplySolutionTests.Fixture();
        var recipe = f.Recipe(f.Position(f.Ingredient(IngredientPropertyState.DoesNotContain, f.Plant)));
        var calculator = new OperationalSupplyCalculator(f);
        SupplyOffer[] offers = [new(null, recipe, 7, [])]; SupplyRequirement[] groups = [new(new(null, [], []), 7)];
        var pending = calculator.Calculate(offers, groups, [], CookingTogetherPreference.Ask, TestContext.Current.CancellationToken);
        Assert.Equal(7, Assert.Single(pending.Solutions).Portions);
        Assert.NotEmpty(pending.Quantities!); Assert.Empty(pending.CommonSupplyChoices!); Assert.Empty(pending.Problems);
        var persisted = ManualSupplyDocument.Read(new ManualSupplyDocument([], [new(null, selected)]).Serialize());
        var result = calculator.Calculate(offers, groups, persisted.Solutions, CookingTogetherPreference.Ask, TestContext.Current.CancellationToken, persisted.ScenarioSelections);
        Assert.Equal(7, Assert.Single(result.Solutions).Portions); Assert.Empty(result.Problems);
        Assert.Empty(result.CommonSupplyChoices!);
    }

    [Fact]
    public void Together_asks_once_for_two_whole_offer_alternatives_while_separate_keeps_standard()
    {
        var f = new SupplySolutionTests.Fixture();
        var standard = f.Recipe(f.Position(f.Ingredient(IngredientPropertyState.Contains, f.Plant)));
        var first = f.Recipe(f.Position(f.Ingredient(IngredientPropertyState.DoesNotContain, f.Plant)));
        var second = f.Recipe(f.Position(f.Ingredient(IngredientPropertyState.DoesNotContain, f.Plant)));
        SupplyOffer[] offers = [new(Guid.NewGuid(), standard, 10, [first, second])];
        SupplyRequirement[] groups = [new(new(null, [], []), 7), new(new(null, [f.Allergen], []), 3)];
        var calculator = new OperationalSupplyCalculator(f);
        var together = calculator.Calculate(offers, groups, [], CookingTogetherPreference.AlwaysTogether, TestContext.Current.CancellationToken);
        Assert.Empty(together.Solutions);
        var decision = Assert.Single(together.Decisions!); Assert.True(decision.Together);
        Assert.Equal(2, decision.RequirementGroupKeys.Count); Assert.Equal(2, decision.Options.Count);
        Assert.All(decision.Options, value => Assert.Equal(10, value.Portions));
        Assert.DoesNotContain(together.Problems, value => value.Code == MealPlanningProblemCode.AllergenConflict);
        var separate = calculator.Calculate(offers, groups, [], CookingTogetherPreference.NeverTogether, TestContext.Current.CancellationToken);
        Assert.Equal(7, Assert.Single(separate.Solutions).Portions);
        Assert.Single(Assert.Single(separate.Decisions!).RequirementGroupKeys);
    }

    [Fact]
    public void Together_combines_resolvable_groups_and_only_conflicts_the_impossible_group()
    {
        var f = new SupplySolutionTests.Fixture();
        var recipe = f.Recipe(f.Position(f.Ingredient(IngredientPropertyState.Contains, f.Plant)) with {
            Replacements = [f.Replacement(f.Ingredient(IngredientPropertyState.DoesNotContain, f.Plant))] });
        var unknown = new RequirementSignature(Guid.NewGuid(), [], []);
        SupplyRequirement[] groups = [new(new(null, [], []), 7), new(new(null, [f.Allergen], []), 2), new(unknown, 1)];
        var result = new OperationalSupplyCalculator(f).Calculate([new(null, recipe, 10, [])], groups, [], CookingTogetherPreference.AlwaysTogether, TestContext.Current.CancellationToken);
        Assert.Equal(9, Assert.Single(result.Solutions).Portions); Assert.Equal(2, Assert.Single(result.Solutions).Allocations.Count);
        Assert.NotEmpty(result.Problems);
        Assert.All(result.Problems, value => Assert.Equal(unknown.Key, value.RequirementGroupKey));
    }

    [Theory]
    [InlineData(CookingTogetherPreference.NeverTogether)]
    [InlineData(CookingTogetherPreference.Ask)]
    public void Api_decisions_include_all_suitable_variants_and_no_partial_proposals(CookingTogetherPreference preference)
    {
        var f = new SupplySolutionTests.Fixture();
        var bad = f.Ingredient(IngredientPropertyState.Contains, f.Plant);
        var good = f.Ingredient(IngredientPropertyState.DoesNotContain, f.Plant);
        var recipe = f.Recipe(f.Position(bad) with { Replacements = [f.Replacement(good), f.Replacement(good), f.Replacement(good)] },
            f.Position(bad) with { Replacements = [f.Replacement(good)] });
        var requirement = new RequirementSignature(null, [f.Allergen], []);
        var result = new OperationalSupplyCalculator(f).Calculate([new(null, recipe, 3, [])], [new(requirement, 3)], [], preference, TestContext.Current.CancellationToken);
        var options = Assert.Single(result.Decisions!).Options; Assert.Equal(3, options.Count);
        Assert.All(options, option => Assert.True(new SupplySuitabilityEvaluator(f).Evaluate(option, requirement).IsSuitable));
        Assert.Equal(options.Count, result.Candidates.Count);
        Assert.Empty(result.CommonSupplyChoices!);
    }

    [Fact]
    public void Ask_preserves_an_explicit_direction_even_when_neither_scenario_is_complete()
    {
        var f = new SupplySolutionTests.Fixture();
        var recipe = f.Recipe(f.Position(f.Ingredient(IngredientPropertyState.Contains, f.Plant)));
        var group = new RequirementSignature(null, [f.Allergen], []);
        var calculator = new OperationalSupplyCalculator(f);
        var initial = calculator.Calculate([new(null, recipe, 3, [])], [new(group, 3)], [], CookingTogetherPreference.Ask, TestContext.Current.CancellationToken);
        Assert.Empty(initial.CommonSupplyChoices!); Assert.Empty(initial.Solutions);
        Assert.Contains(initial.Problems, value => value.Code == MealPlanningProblemCode.AllergenConflict);
        var confirmed = calculator.Calculate([new(null, recipe, 3, [])], [new(group, 3)], [], CookingTogetherPreference.Ask, TestContext.Current.CancellationToken,
            [new(null, CookingTogetherPreference.NeverTogether)]);
        Assert.DoesNotContain(confirmed.Problems, value => value.Code == MealPlanningProblemCode.CommonSupplyDecisionRequired);
        Assert.Contains(confirmed.Problems, value => value.Code == MealPlanningProblemCode.AllergenConflict);
    }
}
