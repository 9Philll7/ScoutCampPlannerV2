using ScoutCampPlanner.Catering.Application.MealPlanning;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Catering.Infrastructure.Offline;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed partial class MealPlanningTests
{
    [Theory]
    [InlineData(CookingTogetherPreference.AlwaysTogether, 1, false)]
    [InlineData(CookingTogetherPreference.Ask, 0, true)]
    [InlineData(CookingTogetherPreference.NeverTogether, 2, false)]
    public async Task Seed_comparison_population_uses_preference_through_service_and_recalculation(
        CookingTogetherPreference preference, int expectedCount, bool ask)
    {
        // Same composition as the July 1 lunch seed: two standard participants,
        // two milk-allergic participants, one explicitly milk-free variant.
        var ct = TestContext.Current.CancellationToken;
        var context = CreateContext(true, true);
        context.Data.CookingUnits[0].SetTogetherPreference(preference);
        var f = new SupplySolutionTests.Fixture();
        var ingredient = f.Ingredient(IngredientPropertyState.Contains, f.Plant, variants: [
            new(Guid.NewGuid(), "free", "Milk-free", true, 0,
                allergenOverrides: [new(f.Allergen, IngredientPropertyState.DoesNotContain, IngredientPropertySource.ManuallyVerified)])]);
        var recipe = f.Recipe(f.Position(ingredient));
        f.Recipes[context.Data.Entries[0].RecipeRevisionId] = f.Recipes[recipe];
        var people = new ParticipantLookup();
        people.Values.AddRange([Person(context.LeafNodeId), Person(context.LeafNodeId),
            Person(context.LeafNodeId) with { AllergenIds = [f.Allergen] },
            Person(context.LeafNodeId) with { AllergenIds = [f.Allergen] }]);
        var store = new RecordingStore(context.Data);
        var service = new MealPlanningService(store, new PlanningLookup(context.Camp), TimeProvider.System, people, f);
        Assert.True((await service.SaveParticipantConfigurationAsync(context.CampId, new(0, MealPlanningDemandMode.UseActualParticipants), ct)).IsSuccess);
        for (int pass = 0; pass < 2; pass++)
        {
            Assert.True((await service.CalculateAsync(context.CampId, context.UnitId, context.MealId, ct)).IsSuccess);
            var state = Assert.Single((await service.GetOverviewAsync(context.CampId, ct))!.OperationalMeals);
            Assert.Equal(expectedCount, state.Supply!.Solutions.Count);
            Assert.Equal(ask, state.Supply.Problems.Any(value => value.Code == MealPlanningProblemCode.CommonSupplyDecisionRequired));
            Assert.All(state.Supply.Solutions, solution => Assert.Equal(SupplyDecisionSource.Automatic, solution.Source));
            Assert.Equal(ask ? 0 : 4, state.Supply.Solutions.Sum(value => value.Portions));
            // Current denotes freshness; the unresolved operator decision is a separate problem.
            Assert.Equal(OperationalMealPlanStatus.Current, state.Status);
            if (ask)
                Assert.Equal(MealPlanningMutationStatus.Invalid, (await service.VerifySupplyAsync(context.CampId,
                    context.UnitId, context.MealId, Guid.NewGuid(), new(state.SupplyVersion, MealVerificationKind.Clean, null), ct)).Status);
        }
        if (ask)
        {
            foreach (bool together in new[] { true, false, true, false })
            {
                var current = Assert.Single((await service.GetOverviewAsync(context.CampId, ct))!.OperationalMeals);
                var choice = Assert.Single(current.Supply!.CommonSupplyChoices!);
                var selected = (together ? choice.TogetherOptions.Take(1) : choice.SeparateSolutions)
                    .Select(value => value with { Source = SupplyDecisionSource.Manual }).ToArray();
                Assert.True((await service.SaveSupplyAsync(context.CampId, context.UnitId, context.MealId,
                    new(current.SupplyVersion, selected), ct)).IsSuccess);
                // Recreate the service and recalculate twice: neither reload nor recalc may
                // rematerialize the other option or convert the selected choice back to Auto.
                service = new(store, new PlanningLookup(context.Camp), TimeProvider.System, people, f);
                for (int pass = 0; pass < 2; pass++)
                {
                    Assert.True((await service.CalculateAsync(context.CampId, context.UnitId, context.MealId, ct)).IsSuccess);
                    current = Assert.Single((await service.GetOverviewAsync(context.CampId, ct))!.OperationalMeals);
                    Assert.Equal(together ? 1 : 2, current.Supply!.Solutions.Count);
                    Assert.All(current.Supply.Solutions, value => Assert.Equal(SupplyDecisionSource.Manual, value.Source));
                    Assert.Equal(selected.Select(value => value.Id).Order(), current.Supply.Solutions.Select(value => value.Id).Order());
                    Assert.Equal(4, current.Supply.Solutions.Sum(value => value.Portions));
                    Assert.Equal(2, current.Supply.Solutions.SelectMany(value => value.Allocations).Count());
                    Assert.Empty(current.Supply.Problems);
                    Assert.Single(current.Supply.CommonSupplyChoices!);
                }
            }
        }
    }

    [Fact]
    public async Task Zero_substance_without_threshold_allows_clean_verification()
    {
        var ct = TestContext.Current.CancellationToken;
        var context = CreateContext(true, true);
        var f = new SupplySolutionTests.Fixture();
        var recipe = f.Recipe(f.Position(f.Ingredient(IngredientPropertyState.DoesNotContain, f.Plant, 0)));
        f.Recipes[context.Data.Entries[0].RecipeRevisionId] = f.Recipes[recipe];
        var people = new ParticipantLookup();
        people.Values.Add(Person(context.LeafNodeId) with { Intolerances = [new(f.Substance, null)] });
        var store = new RecordingStore(context.Data);
        var service = new MealPlanningService(store, new PlanningLookup(context.Camp), TimeProvider.System, people, f);
        await service.SaveParticipantConfigurationAsync(context.CampId, new(0, MealPlanningDemandMode.UseActualParticipants), ct);
        await service.CalculateAsync(context.CampId, context.UnitId, context.MealId, ct);
        var state = Assert.Single((await service.GetOverviewAsync(context.CampId, ct))!.OperationalMeals);
        Assert.Empty(state.Supply!.Problems);
        Assert.Equal(OperationalMealPlanStatus.Current, state.Status);
        Assert.True((await service.VerifySupplyAsync(context.CampId, context.UnitId, context.MealId, Guid.NewGuid(),
            new(state.SupplyVersion, MealVerificationKind.Clean, null), ct)).IsSuccess);
    }

    [Fact]
    public async Task Only_the_meal_using_a_changed_recipe_is_stale_and_metadata_is_ignored()
    {
        var ct = TestContext.Current.CancellationToken;
        var context = CreateContext(true, true);
        var secondMeal = new CampMeal(Guid.NewGuid(), context.CampId, context.Data.MealTypes[0].Id, new(2027, 7, 3));
        var secondGroup = new MealPlanOfferGroup(Guid.NewGuid(), context.PlanId, secondMeal.Id, "Other", 0);
        var secondEntry = new MealPlanEntry(Guid.NewGuid(), secondGroup.Id, Guid.NewGuid(), true, null, null, null, 0);
        var store = new RecordingStore(context.Data with { Meals = [.. context.Data.Meals, secondMeal],
            OfferGroups = [.. context.Data.OfferGroups, secondGroup], Entries = [.. context.Data.Entries, secondEntry] });
        var service = new MealPlanningService(store, new PlanningLookup(context.Camp), TimeProvider.System);
        await service.CalculateAsync(context.CampId, context.UnitId, context.MealId, ct);
        await service.CalculateAsync(context.CampId, context.UnitId, secondMeal.Id, ct);
        secondEntry.Update(Guid.NewGuid(), true, null, null, null, 0);
        // Simulate the old broad Inkrement-1 store invalidation: read-side freshness is selective.
        foreach (var state in (await store.LoadAsync(context.CampId, ct)).MealStates) state.MarkStale();
        var overview = (await service.GetOverviewAsync(context.CampId, ct))!;
        Assert.Equal(OperationalMealPlanStatus.Current, overview.OperationalMeals.Single(value => value.CampMealId == context.MealId).Status);
        Assert.Equal(OperationalMealPlanStatus.Stale, overview.OperationalMeals.Single(value => value.CampMealId == secondMeal.Id).Status);
        var first = context.Data.Entries[0];
        first.Update(first.RecipeRevisionId, true, "Display changed", MealPlanEntryRole.MainDish, "Only a note", 5);
        Assert.Equal(OperationalMealPlanStatus.Current, (await service.GetOverviewAsync(context.CampId, ct))!.OperationalMeals.Single(value => value.CampMealId == context.MealId).Status);
        first.Update(Guid.NewGuid(), true, null, null, null, 0);
        Assert.Equal(OperationalMealPlanStatus.Stale, (await service.GetOverviewAsync(context.CampId, ct))!.OperationalMeals.Single(value => value.CampMealId == context.MealId).Status);
    }

    [Fact]
    public async Task Persisted_manual_supply_and_preference_survive_package_replace_and_reject_old_version()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await DatabaseFixture.CreateAsync();
        var context = CreateContext(false, true);
        var unit = context.Data.CookingUnits[0];
        unit.Update(unit.Name, 0, null, null);
        unit.SetTogetherPreference(CookingTogetherPreference.NeverTogether);
        var state = new CookingUnitMealState(Guid.NewGuid(), context.CampId, unit.Id, context.MealId);
        fixture.Database.AddRange(context.Data.MealTypes.Cast<object>().Concat(context.Data.Meals)
            .Concat([unit, state]).ToArray());
        await fixture.Database.SaveChangesAsync(ct);
        var solution = new SupplySolution(Guid.NewGuid(), Guid.NewGuid(), null, 2,
            new Dictionary<string, ScoutCampPlanner.Catering.Application.Recipes.RecipePositionSelection>(),
            [new(new RequirementSignature(null, [], []).Key, 2)], SupplyDecisionSource.Manual);
        var json = JsonSerializer.Serialize(new[] { solution }, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var store = new MealPlanningStore(fixture.Database);
        Assert.True((await store.SaveSupplyStateAsync(context.CampId, unit.Id, context.MealId, 0, json, null, null, ct)).IsSuccess);
        Assert.Equal(MealPlanningMutationStatus.Conflict,
            (await store.SaveSupplyStateAsync(context.CampId, unit.Id, context.MealId, 0, "[]", null, null, ct)).Status);
        var verification = new MealVerificationSnapshot(unit.Id, context.MealId, null, null, null,
            EffectiveDemandBasis.Estimated, 2, [new(new(null, [], []), 2)], new([solution], [], []),
            MealVerificationKind.Clean, null, Guid.NewGuid(), DateTimeOffset.UtcNow);
        var verificationJson = JsonSerializer.Serialize(verification, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.True((await store.SaveSupplyStateAsync(context.CampId, unit.Id, context.MealId, 1, null,
            verificationJson, new string('A', 64), ct)).IsSuccess);
        var packages = new CampMealPlanningPackageStore(fixture.Database);
        var package = await packages.ExportAsync(context.CampId, ct);
        Assert.Contains(solution.RecipeRevisionId, CampMealPlanningPackageStore.ReadRecipeRevisionIds(package, context.CampId));
        fixture.Database.ChangeTracker.Clear();
        await packages.DeleteCampDataAsync(context.CampId, ct);
        await packages.ImportAsync(package, context.CampId, ct);
        await fixture.Database.SaveChangesAsync(ct);
        fixture.Database.ChangeTracker.Clear();
        var restored = await fixture.Database.CookingUnitMealStates.SingleAsync(ct);
        Assert.Equal(json, restored.ManualSupplySolutionsJson);
        Assert.Equal(2, restored.SupplyVersion);
        Assert.Equal(verificationJson, restored.VerificationSnapshotJson);
        Assert.Equal(new string('A', 64), restored.VerificationFingerprint);
        Assert.Equal(CookingTogetherPreference.NeverTogether, (await fixture.Database.CookingUnits.SingleAsync(ct)).TogetherPreference);
    }

    [Fact]
    public async Task Supply_is_saved_in_existing_state_manual_choice_survives_recalc_and_versions_conflict()
    {
        var ct = TestContext.Current.CancellationToken;
        var context = CreateContext(true, true);
        var references = new SupplySolutionTests.Fixture();
        var recipe = references.Recipe(references.Position(references.Ingredient(IngredientPropertyState.DoesNotContain, references.Plant)));
        references.Recipes[context.Data.Entries[0].RecipeRevisionId] = references.Recipes[recipe];
        var store = new RecordingStore(context.Data);
        var service = new MealPlanningService(store, new PlanningLookup(context.Camp), TimeProvider.System, supplyReferences: references);
        await service.CalculateAsync(context.CampId, context.UnitId, context.MealId, ct);
        var first = Assert.Single((await service.GetOverviewAsync(context.CampId, ct))!.OperationalMeals);
        var automatic = Assert.Single(first.Supply!.Solutions);
        Assert.Equal(7, automatic.Portions);
        Assert.Empty(first.Supply.Problems);
        var actor = Guid.NewGuid();
        Assert.True((await service.VerifySupplyAsync(context.CampId, context.UnitId, context.MealId, actor,
            new(first.SupplyVersion, MealVerificationKind.Clean, null), ct)).IsSuccess);
        first = Assert.Single((await service.GetOverviewAsync(context.CampId, ct))!.OperationalMeals);
        Assert.Equal(actor, first.Verification!.VerifiedBy);
        Assert.False(first.VerificationStale);
        var manual = automatic with { Source = SupplyDecisionSource.Manual, Portions = 3,
            Allocations = [new(automatic.Allocations[0].RequirementGroupKey, 3)] };
        var duplicate = await service.SaveSupplyAsync(context.CampId, context.UnitId, context.MealId,
            new(first.SupplyVersion, [manual, manual with { Id = Guid.NewGuid() }]), ct);
        Assert.Equal(MealPlanningMutationStatus.Invalid, duplicate.Status);
        Assert.Equal("duplicate_supply_decision", duplicate.Code);
        Assert.True((await service.SaveSupplyAsync(context.CampId, context.UnitId, context.MealId,
            new(first.SupplyVersion, [manual]), ct)).IsSuccess);
        Assert.Equal(MealPlanningMutationStatus.Conflict, (await service.SaveSupplyAsync(context.CampId, context.UnitId, context.MealId,
            new(first.SupplyVersion, []), ct)).Status);
        Assert.Equal(OperationalMealPlanStatus.Stale,
            Assert.Single((await service.GetOverviewAsync(context.CampId, ct))!.OperationalMeals).Status);
        await service.CalculateAsync(context.CampId, context.UnitId, context.MealId, ct);
        var next = Assert.Single((await service.GetOverviewAsync(context.CampId, ct))!.OperationalMeals);
        Assert.Equal(3, Assert.Single(next.Supply!.Solutions).Portions);
        Assert.Equal(SupplyDecisionSource.Manual, next.Supply.Solutions[0].Source);
        Assert.Contains(next.Supply.Problems, value => value.Code == MealPlanningProblemCode.RequirementGroupUndercovered);
        Assert.True(next.VerificationStale);
        Assert.Equal(MealPlanningMutationStatus.Invalid, (await service.VerifySupplyAsync(context.CampId, context.UnitId,
            context.MealId, actor, new(next.SupplyVersion, MealVerificationKind.Clean, null), ct)).Status);
        Assert.Equal(MealPlanningMutationStatus.Invalid, (await service.VerifySupplyAsync(context.CampId, context.UnitId,
            context.MealId, actor, new(next.SupplyVersion, MealVerificationKind.VerifiedWithDeviation, " "), ct)).Status);
        Assert.True((await service.VerifySupplyAsync(context.CampId, context.UnitId,
            context.MealId, actor, new(next.SupplyVersion, MealVerificationKind.VerifiedWithDeviation, "Dummy-Testabweichung"), ct)).IsSuccess);
        var verified = Assert.Single((await service.GetOverviewAsync(context.CampId, ct))!.OperationalMeals);
        Assert.Equal(MealVerificationKind.VerifiedWithDeviation, verified.Verification!.Kind);
        Assert.False(verified.VerificationStale);
        Assert.Equal(3, Assert.Single(verified.Verification.Supply.Solutions).Portions);
    }

    [Fact]
    public async Task Name_changes_do_not_invalidate_but_recipe_changes_do_and_no_supply_ignores_them()
    {
        var ct = TestContext.Current.CancellationToken;
        var context = CreateContext(true, true);
        var store = new RecordingStore(context.Data);
        var service = new MealPlanningService(store, new PlanningLookup(context.Camp), TimeProvider.System);
        await service.CalculateAsync(context.CampId, context.UnitId, context.MealId, ct);
        var unit = context.Data.CookingUnits[0];
        unit.Update("New name", 2, null, unit.StandardMealPlanId);
        Assert.Equal(OperationalMealPlanStatus.Current, Assert.Single((await service.GetOverviewAsync(context.CampId, ct))!.OperationalMeals).Status);
        unit.SetTogetherPreference(CookingTogetherPreference.NeverTogether);
        Assert.Equal(OperationalMealPlanStatus.Stale, Assert.Single((await service.GetOverviewAsync(context.CampId, ct))!.OperationalMeals).Status);
        var state = (await store.LoadAsync(context.CampId, ct)).MealStates[0];
        state.Configure(MealPlanSubscriptionState.NoSupplyRequired, null);
        await service.CalculateAsync(context.CampId, context.UnitId, context.MealId, ct);
        unit.SetTogetherPreference(CookingTogetherPreference.AlwaysTogether);
        unit.Update(unit.Name, 3, null, null);
        Assert.Equal(OperationalMealPlanStatus.Current, Assert.Single((await service.GetOverviewAsync(context.CampId, ct))!.OperationalMeals).Status);
    }
}
