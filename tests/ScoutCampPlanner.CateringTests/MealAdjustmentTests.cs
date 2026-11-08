using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Application.MealPlanning;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed partial class MealPlanningTests
{
    [Fact]
    public async Task Adjustment_save_preserves_confirmed_choices_and_all_reset_clears_every_override_atomically()
    {
        var ct = TestContext.Current.CancellationToken; await using var f = await DatabaseFixture.CreateAsync();
        var camp = Guid.NewGuid(); var unitId = Guid.NewGuid(); var mealId = Guid.NewGuid(); var typeId = Guid.NewGuid(); var planId = Guid.NewGuid(); var offerId = Guid.NewGuid();
        var state = new CookingUnitMealState(Guid.NewGuid(), camp, unitId, mealId);
        var choice = new SupplySolution(Guid.NewGuid(), Guid.NewGuid(), offerId, 1, new Dictionary<string, ScoutCampPlanner.Catering.Application.Recipes.RecipePositionSelection>(), [new("group", 1)], SupplyDecisionSource.Manual);
        var own = choice with { Id = Guid.NewGuid(), RecipeRevisionId = Guid.NewGuid() };
        state.SaveManualSupply(new ManualSupplyDocument([choice, own], [new(offerId, CookingTogetherPreference.NeverTogether)], true).Serialize());
        f.Database.AddRange(new MealPlan(planId, camp, "Plan"), new CookingUnit(unitId, camp, "Kitchen", 0, standardMealPlanId: planId),
            new CampMealType(typeId, camp, "Meal", 0), new CampMeal(mealId, camp, typeId, new(2027,7,2)), new MealPlanOfferGroup(offerId, planId, mealId, "Main", 0), state);
        await f.Database.SaveChangesAsync(ct);
        var store = new MealPlanningStore(f.Database); var version = state.SupplyVersion;
        var request = new ConfigureCookingUnitMealRequest(MealPlanSubscriptionState.FollowStandard, 12, [Guid.NewGuid()],
            [new(Guid.NewGuid(), offerId, 8)], [], new(version, ResetRecipes: true, RetainRecipeSolutionIds: [choice.Id], UpdateParticipantFilter: true, ParticipantFilterOverride: CookingUnitParticipantFilter.SpecialCateringOnly));
        Assert.True((await store.ConfigureCookingUnitMealAsync(camp, unitId, mealId, request, ct)).IsSuccess);
        var saved = await f.Database.CookingUnitMealStates.AsNoTracking().SingleAsync(ct);
        Assert.Equal(12, saved.DemandOverride); var manual = ManualSupplyDocument.Read(saved.ManualSupplySolutionsJson);
        Assert.Equal(choice.Id, Assert.Single(manual.Solutions).Id); Assert.Single(manual.ScenarioSelections); Assert.False(manual.CompleteOwnSupply); Assert.Equal(CookingUnitParticipantFilter.SpecialCateringOnly, manual.ParticipantFilterOverride);
        var stale = request with { DemandOverride = 99, SupplyAdjustments = new(version, true, CookingTogetherPreference.AlwaysTogether) };
        Assert.Equal(MealPlanningMutationStatus.Conflict, (await store.ConfigureCookingUnitMealAsync(camp, unitId, mealId, stale, ct)).Status);
        Assert.Equal(12, (await f.Database.CookingUnitMealStates.AsNoTracking().SingleAsync(ct)).DemandOverride);
        var reset = request with { DemandOverride = null, StructureOverrideNodeIds = [], OfferTargets = [], SupplyAdjustments = new(saved.SupplyVersion, true, null, true, [], true, null) };
        Assert.True((await store.ConfigureCookingUnitMealAsync(camp, unitId, mealId, reset, ct)).IsSuccess);
        saved = await f.Database.CookingUnitMealStates.AsNoTracking().SingleAsync(ct);
        Assert.Null(saved.DemandOverride); manual = ManualSupplyDocument.Read(saved.ManualSupplySolutionsJson);
        Assert.Empty(manual.Solutions); Assert.Empty(manual.ScenarioSelections);
        Assert.Empty(await f.Database.CookingUnitMealOfferTargets.ToArrayAsync(ct));
        Assert.Empty(await f.Database.CookingUnitStructureAssignments.ToArrayAsync(ct));
        var preference = reset with { DemandOverride = 7, OfferTargets = [new(Guid.NewGuid(), offerId, 7)], SupplyAdjustments = new(saved.SupplyVersion, true, CookingTogetherPreference.AlwaysTogether) };
        Assert.True((await store.ConfigureCookingUnitMealAsync(camp, unitId, mealId, preference, ct)).IsSuccess);
        saved = await f.Database.CookingUnitMealStates.AsNoTracking().SingleAsync(ct);
        Assert.Equal(7, saved.DemandOverride); Assert.Equal(CookingTogetherPreference.AlwaysTogether, Assert.Single(ManualSupplyDocument.Read(saved.ManualSupplySolutionsJson).ScenarioSelections).Preference);
        // Fail after ExecuteDelete has removed the old targets: the transaction must restore them too.
        var duplicateId = Guid.NewGuid();
        var broken = preference with { DemandOverride = 99, OfferTargets = [new(duplicateId, offerId, 1), new(duplicateId, offerId, 2)],
            SupplyAdjustments = new(saved.SupplyVersion, true, CookingTogetherPreference.NeverTogether) };
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.ConfigureCookingUnitMealAsync(camp, unitId, mealId, broken, ct));
        f.Database.ChangeTracker.Clear();
        saved = await f.Database.CookingUnitMealStates.AsNoTracking().SingleAsync(ct);
        Assert.Equal(7, saved.DemandOverride);
        Assert.Equal(7, (await f.Database.CookingUnitMealOfferTargets.AsNoTracking().SingleAsync(ct)).TargetOverride);
        Assert.Equal(CookingTogetherPreference.AlwaysTogether, Assert.Single(ManualSupplyDocument.Read(saved.ManualSupplySolutionsJson).ScenarioSelections).Preference);
    }
}
