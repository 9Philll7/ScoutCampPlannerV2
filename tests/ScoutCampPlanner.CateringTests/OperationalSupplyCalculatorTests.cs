using ScoutCampPlanner.Catering.Application.MealPlanning;
using ScoutCampPlanner.Catering.Domain;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class OperationalSupplyCalculatorTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Ask_choice_replaces_its_scope_and_survives_serialized_recalculation(bool togetherFirst)
    {
        var f = new SupplySolutionTests.Fixture();
        var recipe = f.Recipe(f.Position(f.Ingredient(IngredientPropertyState.Contains, f.Plant)) with {
            Replacements = [f.Replacement(f.Ingredient(IngredientPropertyState.DoesNotContain, f.Plant))] });
        var otherRecipe = f.Recipe(f.Position(f.Ingredient(IngredientPropertyState.DoesNotContain, f.Plant)));
        var offerId = Guid.NewGuid();
        var otherOfferId = Guid.NewGuid();
        var offers = new SupplyOffer[] { new(offerId, recipe, 4, []), new(otherOfferId, otherRecipe, 4, []) };
        var requirements = new SupplyRequirement[] { new(new(null, [], []), 2), new(new(null, [f.Allergen], []), 2) };
        var calculator = new OperationalSupplyCalculator(f);
        var result = calculator.Calculate(offers, requirements, [], CookingTogetherPreference.Ask, TestContext.Current.CancellationToken);
        Assert.Single(result.Solutions); // Only the unrelated, unambiguous offer is active.
        foreach (bool together in new[] { togetherFirst, !togetherFirst, togetherFirst })
        {
            var choice = Assert.Single(result.CommonSupplyChoices!);
            var selection = (together ? choice.TogetherOptions.Take(1) : choice.SeparateSolutions)
                .Select(value => value with { Source = SupplyDecisionSource.Manual }).ToArray();
            var restored = System.Text.Json.JsonSerializer.Deserialize<SupplySolution[]>(System.Text.Json.JsonSerializer.Serialize(selection))!;
            result = calculator.Calculate(offers, requirements, restored, CookingTogetherPreference.Ask, TestContext.Current.CancellationToken);
            Assert.Empty(result.Problems);
            Assert.Equal(together ? 1 : 2, result.Solutions.Count(value => value.OfferGroupId == offerId));
            Assert.All(result.Solutions.Where(value => value.OfferGroupId == offerId), value => Assert.Equal(SupplyDecisionSource.Manual, value.Source));
            Assert.Equal(SupplyDecisionSource.Automatic, Assert.Single(result.Solutions, value => value.OfferGroupId == otherOfferId).Source);
            Assert.Equal(4, result.Solutions.Where(value => value.OfferGroupId == offerId).Sum(value => value.Portions));
            Assert.Single(result.CommonSupplyChoices!);
        }
    }

    [Theory]
    [InlineData(CookingTogetherPreference.AlwaysTogether, 2, false)]
    [InlineData(CookingTogetherPreference.Ask, 1, true)]
    [InlineData(CookingTogetherPreference.NeverTogether, 3, false)]
    public void Manual_group_does_not_disable_preference_for_remaining_automatic_groups(
        CookingTogetherPreference preference, int count, bool ask)
    {
        var f = new SupplySolutionTests.Fixture();
        var replacement = f.Replacement(f.Ingredient(IngredientPropertyState.DoesNotContain, f.Plant, 0));
        var recipe = f.Recipe(f.Position(f.Ingredient(IngredientPropertyState.Contains, f.Plant, 0)) with { Replacements = [replacement] });
        var manualGroup = new RequirementSignature(null, [], [new(f.Substance, 10)]);
        var manual = f.Solution(recipe) with { Source = SupplyDecisionSource.Manual, Allocations = [new(manualGroup.Key, 1)] };
        var result = new OperationalSupplyCalculator(f).Calculate([new(null, recipe, 3, [])],
            [new(new(null, [], []), 1), new(new(null, [f.Allergen], []), 1), new(manualGroup, 1)],
            [manual], preference, TestContext.Current.CancellationToken);
        Assert.Equal(count, result.Solutions.Count);
        var retained = result.Solutions.Single(value => value.Source == SupplyDecisionSource.Manual);
        Assert.Equal(manual.Id, retained.Id);
        Assert.Equal(manual.RecipeRevisionId, retained.RecipeRevisionId);
        Assert.Equal(manual.Allocations.ToArray(), retained.Allocations.ToArray());
        Assert.Equal(ask ? 0 : 2, result.Solutions.Where(value => value.Source == SupplyDecisionSource.Automatic).Sum(value => value.Portions));
        Assert.Equal(ask, result.Problems.Any(value => value.Code == MealPlanningProblemCode.CommonSupplyDecisionRequired));
    }

    [Theory]
    [InlineData(CookingTogetherPreference.AlwaysTogether)]
    [InlineData(CookingTogetherPreference.Ask)]
    [InlineData(CookingTogetherPreference.NeverTogether)]
    public void No_common_suitable_solution_keeps_separate_solutions_without_common_question(CookingTogetherPreference preference)
    {
        var f = new SupplySolutionTests.Fixture();
        var original = f.Ingredient(IngredientPropertyState.Contains, f.Plant, 0);
        var replacement = f.Replacement(f.Ingredient(IngredientPropertyState.DoesNotContain, f.Plant, 100));
        var recipe = f.Recipe(f.Position(original) with { Replacements = [replacement] });
        var groups = new SupplyRequirement[] { new(new(null, [f.Allergen], []), 1), new(new(null, [], [new(f.Substance, 0)]), 1) };
        var result = new OperationalSupplyCalculator(f).Calculate([new(null, recipe, 2, [])], groups, [], preference, TestContext.Current.CancellationToken);
        Assert.Equal(2, result.Solutions.Count);
        Assert.Empty(result.Problems);
        Assert.All(result.Solutions, solution => Assert.Single(solution.Allocations));
    }

    [Fact]
    public void Ask_does_not_offer_common_vs_separate_when_separate_is_unresolved()
    {
        var f = new SupplySolutionTests.Fixture();
        var original = f.Ingredient(IngredientPropertyState.Contains, f.Plant, 0);
        var recipe = f.Recipe(f.Position(original) with { Replacements = [
            f.Replacement(f.Ingredient(IngredientPropertyState.DoesNotContain, f.Plant, 0)),
            f.Replacement(f.Ingredient(IngredientPropertyState.DoesNotContain, f.Plant, 0))] });
        var result = new OperationalSupplyCalculator(f).Calculate([new(null, recipe, 2, [])],
            [new(new(null, [], []), 1), new(new(null, [f.Allergen], []), 1)], [], CookingTogetherPreference.Ask, TestContext.Current.CancellationToken);
        Assert.DoesNotContain(result.Problems, value => value.Code == MealPlanningProblemCode.CommonSupplyDecisionRequired);
        Assert.Contains(result.Problems, value => value.Code == MealPlanningProblemCode.MultipleDirectSolutions);
    }

    [Theory]
    [InlineData(CookingTogetherPreference.AlwaysTogether)]
    [InlineData(CookingTogetherPreference.Ask)]
    [InlineData(CookingTogetherPreference.NeverTogether)]
    public void Identical_suitable_decisions_merge_without_operator_question(CookingTogetherPreference preference)
    {
        var f = new SupplySolutionTests.Fixture();
        var recipe = f.Recipe(f.Position(f.Ingredient(IngredientPropertyState.DoesNotContain, f.Plant, 0)));
        var result = new OperationalSupplyCalculator(f).Calculate([new(null, recipe, 2, [])],
            [new(new(null, [], []), 1), new(new(null, [f.Allergen], []), 1)], [], preference, TestContext.Current.CancellationToken);
        Assert.Equal(2, Assert.Single(result.Solutions).Allocations.Count);
        Assert.Empty(result.Problems);
    }

    [Theory]
    [InlineData(ScalingMode.Fixed, 10)]
    [InlineData(ScalingMode.Stepwise, 90)]
    public void Resulting_quantities_include_existing_scaling_parameters(ScalingMode mode, int expected)
    {
        var f = new SupplySolutionTests.Fixture();
        var recipe = f.Recipe(f.Position(f.Ingredient(IngredientPropertyState.DoesNotContain, f.Plant)) with {
            Quantity = 10, ScalingMode = mode, StepwiseScaling = mode == ScalingMode.Stepwise ? new(3, 30) : null });
        var result = new OperationalSupplyCalculator(f).Calculate([new(null, recipe, 7, [])],
            [new(new(null, [], []), 7)], [], CookingTogetherPreference.AlwaysTogether, TestContext.Current.CancellationToken);
        var quantity = Assert.Single(result.Quantities!);
        Assert.Equal(expected, quantity.Quantity);
        Assert.Equal(mode, quantity.ScalingMode);
        Assert.Equal("g", quantity.Unit);
    }
    [Theory]
    [InlineData(CookingTogetherPreference.AlwaysTogether)]
    [InlineData(CookingTogetherPreference.Ask)]
    public void Unique_common_solution_is_found_even_when_one_group_has_multiple_direct_choices(CookingTogetherPreference preference)
    {
        var f = new SupplySolutionTests.Fixture();
        var original = f.Ingredient(IngredientPropertyState.Contains, f.Plant, 0);
        var unsuitableForSecond = f.Ingredient(IngredientPropertyState.DoesNotContain, f.Plant, 100);
        var suitableForBoth = f.Ingredient(IngredientPropertyState.DoesNotContain, f.Plant, 0);
        var commonReplacement = f.Replacement(suitableForBoth);
        var recipe = f.Recipe(f.Position(original) with { Replacements = [f.Replacement(unsuitableForSecond), commonReplacement] });
        var result = new OperationalSupplyCalculator(f).Calculate([new(null, recipe, 2, [])],
            [new(new(null, [f.Allergen], []), 1), new(new(null, [], [new(f.Substance, 0)]), 1)],
            [], preference, TestContext.Current.CancellationToken);
        var selected = Assert.Single(result.Solutions);
        Assert.Equal(commonReplacement.Id, Assert.Single(selected.PositionSelections).Value.ReplacementRuleId);
        Assert.Equal(2, selected.Allocations.Count);
        Assert.Empty(result.Problems);
    }

    [Theory]
    [InlineData(CookingTogetherPreference.AlwaysTogether, 1, false)]
    [InlineData(CookingTogetherPreference.Ask, 0, true)]
    [InlineData(CookingTogetherPreference.NeverTogether, 2, false)]
    public void Preference_uses_only_suitable_common_solutions(CookingTogetherPreference preference, int count, bool ask)
    {
        var f = new SupplySolutionTests.Fixture();
        var original = f.Ingredient(IngredientPropertyState.Contains, f.Plant);
        var replacement = f.Ingredient(IngredientPropertyState.DoesNotContain, f.Plant);
        var recipe = f.Recipe(f.Position(original) with { Replacements = [f.Replacement(replacement)] });
        var result = new OperationalSupplyCalculator(f).Calculate([new(null, recipe, 2, [])],
            [new(new(null, [], []), 1), new(new(null, [f.Allergen], []), 1)], [], preference, TestContext.Current.CancellationToken);
        Assert.Equal(count, result.Solutions.Count);
        Assert.Equal(ask, result.Problems.Any(value => value.Code == MealPlanningProblemCode.CommonSupplyDecisionRequired));
        Assert.Equal(ask ? 0 : 2, result.Solutions.Sum(value => value.Portions));
        if (ask)
        {
            var choice = Assert.Single(result.CommonSupplyChoices!);
            Assert.Equal(2, choice.SeparateSolutions.Count);
            Assert.Equal(2, Assert.Single(choice.TogetherOptions).Portions);
            Assert.Empty(result.Candidates);
        }
        if (count == 1) Assert.Equal(2, Assert.Single(result.Solutions).Allocations.Count);
    }

    [Fact]
    public void Manual_unsuitable_choice_survives_and_is_not_replaced_by_common_solution()
    {
        var f = new SupplySolutionTests.Fixture();
        var recipe = f.Recipe(f.Position(f.Ingredient(IngredientPropertyState.Contains, f.Plant)));
        var group = new RequirementSignature(null, [f.Allergen], []);
        var manual = f.Solution(recipe) with { Source = SupplyDecisionSource.Manual, Allocations = [new(group.Key, 1)] };
        var result = new OperationalSupplyCalculator(f).Calculate([new(null, recipe, 2, [])],
            [new(new(null, [], []), 1), new(group, 1)], [manual], CookingTogetherPreference.AlwaysTogether, TestContext.Current.CancellationToken);
        Assert.Contains(result.Solutions, value => value.Id == manual.Id && value.Source == SupplyDecisionSource.Manual);
        Assert.Contains(result.Problems, value => value.Code == MealPlanningProblemCode.ManualDecisionNoLongerSuitable);
    }

    [Fact]
    public void Identical_solutions_are_reassessed_after_stepwise_merge()
    {
        var f = new SupplySolutionTests.Fixture();
        var ingredient = f.Ingredient(IngredientPropertyState.DoesNotContain, f.Plant, 10);
        var recipe = f.Recipe(f.Position(ingredient) with { ScalingMode = ScalingMode.Stepwise, StepwiseScaling = new(10, 300) });
        var strict = new RequirementSignature(null, [], [new(f.Substance, 3)]);
        var relaxed = new RequirementSignature(null, [], [new(f.Substance, 30)]);
        var result = new OperationalSupplyCalculator(f).Calculate([new(null, recipe, 11, [])],
            [new(strict, 10), new(relaxed, 1)], [], CookingTogetherPreference.NeverTogether, TestContext.Current.CancellationToken);
        Assert.Equal(11, Assert.Single(result.Solutions).Portions);
        Assert.Contains(result.Problems, value => value.Code == MealPlanningProblemCode.SubstanceThresholdExceeded && value.RequirementGroupKey == strict.Key);
    }
}
