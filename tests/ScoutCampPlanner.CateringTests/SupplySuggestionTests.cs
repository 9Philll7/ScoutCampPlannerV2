using ScoutCampPlanner.Catering.Application.MealPlanning;
using ScoutCampPlanner.Catering.Domain;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class SupplySuggestionTests
{
    private static CookingUnitMealDocument State(OperationalSupplyDocument supply) => new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
        MealPlanSubscriptionState.FollowStandard, null, 10, 10, OperationalMealPlanStatus.Current, null, null,
        DateTimeOffset.UtcNow, [], [], [], [], Supply: supply, SupplyVersion: 7);

    [Fact]
    public void Finds_catalogue_recipes_for_whole_and_missing_scopes_without_mutating_coverage()
    {
        var f = new SupplySolutionTests.Fixture();
        var good = f.Recipe(f.Position(f.Ingredient(IngredientPropertyState.DoesNotContain, f.Plant)));
        var bad = f.Recipe(f.Position(f.Ingredient(IngredientPropertyState.Contains, f.Plant)));
        var offer = Guid.NewGuid(); var normal = new RequirementSignature(null, [], []); var allergic = new RequirementSignature(null, [f.Allergen], []);
        var existing = f.Solution(bad) with { OfferGroupId = offer, Portions = 9, Allocations = [new(normal.Key, 9)] };
        var state = State(new([existing], [], [new(MealPlanningProblemCategory.Coverage, MealPlanningProblemCode.RequirementGroupUndercovered, allergic.Key, offer)], [new(normal, 9), new(allergic, 1)]));
        MealPlanningRecipeOption[] recipes = [new(good, Guid.NewGuid(), "Good", 1, true), new(bad, Guid.NewGuid(), "Bad", 1, true)];
        var builder = new SupplySuggestionBuilder(f);
        var result = builder.Build(state, CookingTogetherPreference.AlwaysTogether, recipes, new Dictionary<Guid,string>(), TestContext.Current.CancellationToken);
        var options = Assert.Single(result.Offers).Options;
        Assert.Equal(2, options.Count);
        Assert.All(options, value => Assert.Equal(good, value.Solution.RecipeRevisionId));
        Assert.Equal(10, Assert.Single(options, value => value.Scope == "whole").Solution.Portions);
        Assert.Equal(1, Assert.Single(options, value => value.Scope == "missing").Solution.Portions);
        Assert.Equal(9, Assert.Single(state.Supply!.Solutions).Portions);
        Assert.Equal(options.Select(value => value.Id), builder.Build(state, CookingTogetherPreference.AlwaysTogether, recipes, new Dictionary<Guid,string>(), TestContext.Current.CancellationToken).Offers[0].Options.Select(value => value.Id));
        Assert.Empty(builder.Build(state with { Status = OperationalMealPlanStatus.Stale }, CookingTogetherPreference.AlwaysTogether, recipes, new Dictionary<Guid,string>(), TestContext.Current.CancellationToken).Offers);
    }

    [Fact]
    public void Searches_adjustments_and_prefers_unchanged_recipe_when_suitable()
    {
        var f = new SupplySolutionTests.Fixture(); var good = f.Ingredient(IngredientPropertyState.DoesNotContain, f.Plant);
        var recipe = f.Recipe(f.Position(f.Ingredient(IngredientPropertyState.Contains, f.Plant)) with { Replacements = [f.Replacement(good)] });
        var resolver = new SupplySolutionResolver(f);
        Assert.Empty(Assert.Single(resolver.FindSuitable(f.Solution(recipe), [new(null, [], [])], TestContext.Current.CancellationToken)).PositionSelections);
        Assert.Single(Assert.Single(resolver.FindSuitable(f.Solution(recipe), [new(null, [f.Allergen], [])], TestContext.Current.CancellationToken)).PositionSelections);
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => resolver.FindSuitable(f.Solution(recipe), [new(null, [], [])], cancellation.Token));
    }
}
