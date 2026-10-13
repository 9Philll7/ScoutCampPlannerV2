using System.Text.Json;
using System.Text.Json.Nodes;
using ScoutCampPlanner.Catering.Application.MealPlanning;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Catering.Infrastructure.Offline;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed partial class MealPlanningTests
{
    [Theory]
    [InlineData("plan")]
    [InlineData("snapshot")]
    [InlineData("version")]
    [InlineData("diet")]
    [InlineData("allergen")]
    [InlineData("substance")]
    [InlineData("key")]
    [InlineData("allocation")]
    public void Verification_package_rejects_dangling_or_forged_references(string change)
    {
        var camp = Guid.NewGuid(); var unit = Guid.NewGuid(); var meal = Guid.NewGuid();
        var plan = Guid.NewGuid(); var snapshot = Guid.NewGuid(); var diet = Guid.NewGuid();
        var allergen = Guid.NewGuid(); var substance = Guid.NewGuid();
        var requirements = new RequirementSignature(diet, [allergen], [new(substance, 1)]);
        var solution = new SupplySolution(Guid.NewGuid(), Guid.NewGuid(), null, 1,
            new Dictionary<string, ScoutCampPlanner.Catering.Application.Recipes.RecipePositionSelection>(),
            [new(requirements.Key, 1)], SupplyDecisionSource.Manual);
        var verified = new MealVerificationSnapshot(unit, meal, plan, 1, snapshot, EffectiveDemandBasis.ActualParticipants,
            1, [new(requirements, 1)], new([solution], [], [], [new(requirements, 1)]), MealVerificationKind.Clean,
            null, Guid.NewGuid(), DateTimeOffset.UtcNow);
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var state = new CookingUnitMealStatePackageRecord(Guid.NewGuid(), camp, unit, meal, 0, null, 1, 1, 0,
            plan, 1, snapshot, null, null, null, null, VerificationSnapshotJson: JsonSerializer.Serialize(verified, options), VerificationFingerprint: new string('A', 64));
        var data = CampMealPlanningPackageStore.ReadPackageData(CampMealPlanningPackageStore.CreateEmptyPackageData(camp), camp) with {
            MealPlans = [new(plan, camp, "Renamed newer plan", 0, 2)],
            Snapshots = [new(snapshot, plan, camp, 1, "{}", DateTimeOffset.UtcNow)], MealStates = [state]
        };
        // A newer plan version and changed current participants do not invalidate old immutable references.
        VerificationPackageValidation.Validate(data, new HashSet<Guid> { diet }, new HashSet<Guid> { allergen }, new HashSet<Guid> { substance });
        var diets = new HashSet<Guid> { diet }; var allergens = new HashSet<Guid> { allergen }; var substances = new HashSet<Guid> { substance };
        switch (change)
        {
            case "plan": data = data with { MealPlans = [] }; break;
            case "snapshot": data = data with { Snapshots = [] }; break;
            case "version": data = data with { Snapshots = [data.Snapshots[0] with { Version = 2 }] }; break;
            case "diet": diets.Clear(); break;
            case "allergen": allergens.Clear(); break;
            case "substance": substances.Clear(); break;
            case "allocation": data = data with { MealStates = [state with { VerificationSnapshotJson = JsonSerializer.Serialize(verified with {
                Supply = new([solution with { Allocations = [new("forged", 1)] }], [], []) }, options) }] }; break;
            case "key":
                var json = JsonNode.Parse(state.VerificationSnapshotJson!)!;
                json["requirementGroups"]![0]!["requirements"]!["key"] = "forged";
                data = data with { MealStates = [state with { VerificationSnapshotJson = json.ToJsonString() }] }; break;
        }
        Assert.Throws<InvalidDataException>(() => VerificationPackageValidation.Validate(data, diets, allergens, substances));
    }

    [Fact]
    public async Task Plan_used_only_by_old_verification_cannot_be_deleted_until_snapshot_is_replaced()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await DatabaseFixture.CreateAsync();
        var context = CreateContext(true, true);
        var unit = context.Data.CookingUnits[0]; unit.Update(unit.Name, 0, null, null);
        var state = new CookingUnitMealState(Guid.NewGuid(), context.CampId, unit.Id, context.MealId);
        var verification = new MealVerificationSnapshot(unit.Id, context.MealId, context.PlanId, 1, context.Data.Snapshots[0].Id,
            EffectiveDemandBasis.Estimated, 0, [], new([], [], []), MealVerificationKind.Clean, null, Guid.NewGuid(), DateTimeOffset.UtcNow);
        state.SetVerification(JsonSerializer.Serialize(verification, new JsonSerializerOptions(JsonSerializerDefaults.Web)), new string('A', 64));
        fixture.Database.AddRange(context.Data.MealPlans.Cast<object>().Concat(context.Data.Snapshots)
            .Concat(context.Data.MealTypes).Concat(context.Data.Meals).Concat([unit, state]).ToArray());
        await fixture.Database.SaveChangesAsync(ct);
        var store = new MealPlanningStore(fixture.Database);
        Assert.Equal(MealPlanningMutationStatus.Blocked, (await store.DeleteMealPlanAsync(context.CampId, context.PlanId, ct)).Status);
        state.SetVerification(JsonSerializer.Serialize(verification with { MealPlanId = null, MealPlanVersion = null, MealPlanSnapshotId = null },
            new JsonSerializerOptions(JsonSerializerDefaults.Web)), new string('B', 64));
        await fixture.Database.SaveChangesAsync(ct);
        fixture.Database.ChangeTracker.Clear(); // A later HTTP request has a fresh EF scope.
        Assert.True((await store.DeleteMealPlanAsync(context.CampId, context.PlanId, ct)).IsSuccess);
    }
}
