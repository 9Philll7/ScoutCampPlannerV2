using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ScoutCampPlanner.Api;
using ScoutCampPlanner.Api.Catering;
using ScoutCampPlanner.Catering.Application.MealPlanning;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Catering.Infrastructure.Recipes;
using ScoutCampPlanner.Platform.Application.Auditing;
using ScoutCampPlanner.Platform.Application.Authorization;
using ScoutCampPlanner.Platform.Domain;
using ScoutCampPlanner.Platform.Infrastructure.Auditing;
using Xunit;

namespace ScoutCampPlanner.PlatformTests;

public sealed partial class CampManagementServiceTests
{
    [Fact]
    public async Task Verification_requires_explicit_permission_and_audit_failure_rolls_back_snapshot()
    {
        var ct = TestContext.Current.CancellationToken;
        var (campId, _) = await ParticipantFixtureAsync();
        var unit = new CookingUnit(Guid.NewGuid(), campId, "Dummy kitchen", 0);
        var mealType = new CampMealType(Guid.NewGuid(), campId, "Dummy meal", 20);
        var meal = new CampMeal(Guid.NewGuid(), campId, mealType.Id, new DateOnly(2027, 7, 1));
        catering.AddRange(unit, mealType, meal);
        await catering.SaveChangesAsync(ct);
        var store = new MealPlanningStore(catering);
        await store.ConfigureCookingUnitMealAsync(campId, unit.Id, meal.Id,
            new(MealPlanSubscriptionState.NoSupplyRequired, null, [], [], []), ct);
        var planning = new MealPlanningService(store, camps, new FixedTimeProvider(Now), camps, new EfSupplySuitabilityReferences(catering));
        await planning.CalculateAsync(campId, unit.Id, meal.Id, ct);
        var state = await catering.CookingUnitMealStates.AsNoTracking().SingleAsync(ct);
        var head = await platform.AuditJournalHeads.SingleAsync(ct);
        var audit = new AuditedOperationExecutor(platform, new FixedAuditKeyProvider());
        var device = new SingleDeviceRuntime(new ConfigurationBuilder().Build());
        MealVerificationService Verification(IAuditedOperationExecutor executor) => new(service, planning, platform, catering, camps,
            executor, new AuditRuntimeState(head.InstanceId), new FixedTimeProvider(Now), device);
        var request = new VerifyMealRequest(state.SupplyVersion, MealVerificationKind.Clean, null);
        Assert.Equal(MealPlanningMutationStatus.NotFound,
            (await Verification(audit).VerifyAsync(campId, unit.Id, meal.Id, otherUserId, request, ct)).Status);
        var membership = await platform.CampMemberships.SingleAsync(ct);
        platform.CampPermissionGrants.Add(new(membership.Id, Permissions.Catering.VerifyMealPlanning));
        await platform.SaveChangesAsync(ct);
        Assert.True((await Verification(audit).VerifyAsync(campId, unit.Id, meal.Id, otherUserId, request, ct)).IsSuccess);
        Assert.Contains(await platform.AuditEvents.AsNoTracking().ToListAsync(ct), value => value.Action == "catering.meal-planning.verified");
        state = await catering.CookingUnitMealStates.AsNoTracking().SingleAsync(ct);
        var before = state.VerificationSnapshotJson;
        long version = state.SupplyVersion;
        await Assert.ThrowsAsync<InvalidOperationException>(() => Verification(new FailingParticipantAudit(audit))
            .VerifyAsync(campId, unit.Id, meal.Id, otherUserId,
                new(version, MealVerificationKind.VerifiedWithDeviation, "Dummy audit rollback"), ct));
        state = await catering.CookingUnitMealStates.AsNoTracking().SingleAsync(ct);
        Assert.Equal(before, state.VerificationSnapshotJson);
        Assert.Equal(version, state.SupplyVersion);
    }
}
