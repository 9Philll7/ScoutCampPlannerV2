using ScoutCampPlanner.Catering.Application.MealPlanning;
using ScoutCampPlanner.Catering.Domain;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed partial class MealPlanningTests
{
    [Theory]
    [InlineData("diet-rule", false, true)]
    [InlineData("other-diet-rule", false, false)]
    [InlineData("unused-origin", false, false)]
    [InlineData("unused-recipe", false, false)]
    [InlineData("diet", false, true)]
    [InlineData("allergen", false, true)]
    [InlineData("threshold", false, true)]
    [InlineData("attendance", false, true)]
    [InlineData("absent-person", false, false)]
    [InlineData("mode", false, true)]
    [InlineData("position", false, true)]
    [InlineData("portions", false, true)]
    [InlineData("allocation", false, true)]
    [InlineData("name", false, false)]
    [InlineData("meal", false, true)]
    [InlineData("diet-rule", true, false)]
    [InlineData("diet", true, false)]
    [InlineData("allergen", true, false)]
    [InlineData("threshold", true, false)]
    [InlineData("attendance", true, false)]
    [InlineData("mode", true, false)]
    [InlineData("name", true, false)]
    [InlineData("meal", true, true)]
    public async Task Supply_and_verification_invalidation_matrix(string change, bool noSupply, bool stale)
    {
        var ct = TestContext.Current.CancellationToken;
        var context = CreateContext(true, true);
        var f = new SupplySolutionTests.Fixture();
        var ingredient = f.Ingredient(IngredientPropertyState.DoesNotContain, f.Plant, 0);
        var replacement = f.Replacement(ingredient);
        var position = f.Position(ingredient) with { Replacements = [replacement] };
        var recipe = f.Recipe(position);
        var revision = context.Data.Entries[0].RecipeRevisionId;
        f.Recipes[revision] = f.Recipes[recipe];
        var store = new RecordingStore(context.Data);
        var people = new ParticipantLookup();
        people.Values.Add(Person(context.LeafNodeId) with { DietTypeId = f.Diet, AllergenIds = [f.Allergen],
            Intolerances = [new(f.Substance, 1)] });
        people.Values.Add(Person(null) with { IsPresent = false });
        var service = new MealPlanningService(store, new PlanningLookup(context.Camp), TimeProvider.System, people, f);
        await service.SaveParticipantConfigurationAsync(context.CampId, new(0, MealPlanningDemandMode.UseActualParticipants), ct);
        await service.CalculateAsync(context.CampId, context.UnitId, context.MealId, ct);
        if (noSupply)
        {
            (await store.LoadAsync(context.CampId, ct)).MealStates[0].Configure(MealPlanSubscriptionState.NoSupplyRequired, null);
            await service.CalculateAsync(context.CampId, context.UnitId, context.MealId, ct);
        }
        var before = Assert.Single((await service.GetOverviewAsync(context.CampId, ct))!.OperationalMeals);
        Assert.True((await service.VerifySupplyAsync(context.CampId, context.UnitId, context.MealId, Guid.NewGuid(),
            new(before.SupplyVersion, MealVerificationKind.Clean, null), ct)).IsSuccess);
        before = Assert.Single((await service.GetOverviewAsync(context.CampId, ct))!.OperationalMeals);
        switch (change)
        {
            case "diet-rule": f.DietRules[(f.Diet, f.Plant)] = DietaryOriginDecision.Excluded; break;
            case "other-diet-rule": f.DietRules[(Guid.NewGuid(), f.Plant)] = DietaryOriginDecision.Excluded; break;
            case "unused-origin": f.DietRules[(f.Diet, f.Animal)] = DietaryOriginDecision.Allowed; break;
            case "unused-recipe": f.Recipes[recipe] = f.Recipes[recipe] with { Name = "Unused revision changed" }; break;
            case "diet": people.Values[0] = people.Values[0] with { DietTypeId = null }; break;
            case "allergen": people.Values[0] = people.Values[0] with { AllergenIds = [] }; break;
            case "threshold": people.Values[0] = people.Values[0] with { Intolerances = [new(f.Substance, 2)] }; break;
            case "attendance": people.Values[0] = people.Values[0] with { IsPresent = false }; break;
            case "absent-person": people.Values[1] = people.Values[1] with { DietTypeId = Guid.NewGuid(), AllergenIds = [f.Allergen] }; break;
            case "mode": await service.SaveParticipantConfigurationAsync(context.CampId, new(1, MealPlanningDemandMode.EstimatedPlanning), ct); break;
            case "name": context.Data.CookingUnits[0].Update("Different name", 3, null, context.PlanId); break;
            case "meal": context.Data.Meals[0].SetActive(false); break;
            default:
                var solution = Assert.Single(before.Supply!.Solutions) with { Source = SupplyDecisionSource.Manual };
                solution = change switch {
                    "position" => solution with { PositionSelections = new Dictionary<string, ScoutCampPlanner.Catering.Application.Recipes.RecipePositionSelection> {
                        [revision.ToString("N") + "/" + position.Id.ToString("N")] = new(replacement.Id) } },
                    "portions" => solution with { Portions = 2 },
                    _ => solution with { Allocations = [new(solution.Allocations[0].RequirementGroupKey, 0)] }
                };
                Assert.True((await service.SaveSupplyAsync(context.CampId, context.UnitId, context.MealId, new(before.SupplyVersion, [solution]), ct)).IsSuccess);
                break;
        }
        var after = Assert.Single((await service.GetOverviewAsync(context.CampId, ct))!.OperationalMeals);
        Assert.Equal(stale ? OperationalMealPlanStatus.Stale : OperationalMealPlanStatus.Current, after.Status);
        Assert.Equal(stale, after.VerificationStale);
        Assert.Equal(System.Text.Json.JsonSerializer.Serialize(before.Verification), System.Text.Json.JsonSerializer.Serialize(after.Verification));
    }
}
