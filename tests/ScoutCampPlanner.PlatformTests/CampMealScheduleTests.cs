using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Api.Camps;
using ScoutCampPlanner.Api.Catering;
using ScoutCampPlanner.Catering.Domain;
using Xunit;

namespace ScoutCampPlanner.PlatformTests;

public sealed partial class CampManagementServiceTests
{
    private static readonly DateOnly ScheduleStart = new(2027, 7, 1);
    private static readonly DateOnly ScheduleEnd = new(2027, 7, 3);

    private async Task<Guid> ScheduleCampAsync()
    {
        var created = await service.CreateAsync(ownerUserId, tenantId,
            new CreateCampRequest("Schedule", ScheduleStart, ScheduleEnd, [otherMembershipId]), TestContext.Current.CancellationToken);
        return created.Camp!.Id;
    }

    private Task<MealScheduleMutation> AddSnackAsync(Guid campId, long version = 0, DateOnly? date = null, string name = "Mitternachtssnack") =>
        cateringService.AddAdditionalMealAsync(ownerUserId, tenantId, campId, ScheduleStart, ScheduleEnd, false,
            new(date ?? ScheduleStart, name, version), TestContext.Current.CancellationToken);

    [Fact]
    public async Task Additional_meal_only_creates_selected_day_and_reuses_type_on_other_days()
    {
        var id = await ScheduleCampAsync();
        var first = await AddSnackAsync(id);
        Assert.Equal(UpdateCampMealsFailure.None, first.Failure);
        Assert.Equal(1, first.ScheduleVersion);
        Assert.Equal(3, first.Meal!.SortOrder);
        var second = await AddSnackAsync(id, 1, ScheduleEnd);
        Assert.Equal(first.Meal.MealTypeId, second.Meal!.MealTypeId);
        Assert.Equal(11, await catering.CampMeals.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, await catering.CampMeals.CountAsync(x => x.MealTypeId == first.Meal.MealTypeId, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("duplicate", UpdateCampMealsFailure.AlreadyExists)]
    [InlineData("stale", UpdateCampMealsFailure.Changed)]
    [InlineData("regular-name", UpdateCampMealsFailure.NameConflict)]
    [InlineData("outside", UpdateCampMealsFailure.Invalid)]
    [InlineData("empty", UpdateCampMealsFailure.Invalid)]
    public async Task Invalid_additional_meal_does_not_change_schedule_or_audit(string scenario, UpdateCampMealsFailure failure)
    {
        var id = await ScheduleCampAsync();
        var added = await AddSnackAsync(id);
        long auditCount = await platform.AuditEvents.LongCountAsync(TestContext.Current.CancellationToken);
        string regularName = await catering.CampMealTypes.Where(x => x.Kind == CampMealKind.Regular).Select(x => x.Name).FirstAsync(TestContext.Current.CancellationToken);
        var result = await AddSnackAsync(id, scenario == "stale" ? 0 : 1,
            scenario == "outside" ? ScheduleEnd.AddDays(1) : null,
            scenario == "regular-name" ? regularName : scenario == "empty" ? " " : "Mitternachtssnack");
        Assert.Equal(failure, result.Failure);
        Assert.Equal(auditCount, await platform.AuditEvents.LongCountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(added.ScheduleVersion, await catering.CampMealScheduleStates.Select(x => x.Version).SingleAsync(TestContext.Current.CancellationToken));
        Assert.Equal(10, await catering.CampMeals.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Reordering_is_day_scoped_and_does_not_change_semantic_versions()
    {
        var id = await ScheduleCampAsync();
        await AddSnackAsync(id);
        var before = await catering.CampMeals.AsNoTracking().ToArrayAsync(TestContext.Current.CancellationToken);
        Guid[] day = before.Where(x => x.Date == ScheduleStart).OrderByDescending(x => x.SortOrder).Select(x => x.Id).ToArray();
        var result = await cateringService.OrderDayMealsAsync(ownerUserId, tenantId, id, ScheduleStart,
            ScheduleStart, ScheduleEnd, false, new(day, 1), TestContext.Current.CancellationToken);
        Assert.Equal(UpdateCampMealsFailure.None, result.Failure);
        Assert.Equal(day, await catering.CampMeals.Where(x => x.Date == ScheduleStart).OrderBy(x => x.SortOrder).Select(x => x.Id).ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.All(await catering.CampMeals.ToArrayAsync(TestContext.Current.CancellationToken), meal => {
            var old = before.Single(x => x.Id == meal.Id);
            Assert.Equal(old.ChangeVersion, meal.ChangeVersion);
            if (meal.Date != ScheduleStart) Assert.Equal(old.SortOrder, meal.SortOrder);
        });
        var invalid = await cateringService.OrderDayMealsAsync(ownerUserId, tenantId, id, ScheduleStart,
            ScheduleStart, ScheduleEnd, false, new(day.Skip(1).ToArray(), 2), TestContext.Current.CancellationToken);
        Assert.Equal(UpdateCampMealsFailure.Invalid, invalid.Failure);
    }

    [Theory]
    [InlineData("none", "Deleted")]
    [InlineData("offer", "Deactivated")]
    [InlineData("state", "Deactivated")]
    [InlineData("structure", "Deactivated")]
    [InlineData("absence", "Deactivated")]
    [InlineData("legacy-override", "Deactivated")]
    public async Task Removing_additional_meal_preserves_every_reference(string reference, string outcome)
    {
        var id = await ScheduleCampAsync();
        var snack = (await AddSnackAsync(id)).Meal!;
        if (reference == "offer")
        {
            var plan = new MealPlan(Guid.NewGuid(), id, "Unused plan"); catering.Add(plan);
            catering.Add(new MealPlanOfferGroup(Guid.NewGuid(), plan.Id, snack.Id, null, 0));
        }
        if (reference is "state" or "structure")
        {
            var unit = new CookingUnit(Guid.NewGuid(), id, "Unit", 0); catering.Add(unit);
            if (reference == "state") catering.Add(new CookingUnitMealState(Guid.NewGuid(), id, unit.Id, snack.Id));
            else catering.Add(new CookingUnitStructureAssignment(Guid.NewGuid(), id, unit.Id, snack.Id, Guid.NewGuid()));
        }
        if (reference == "absence")
        {
            var participant = new ScoutCampPlanner.Camp.Domain.Participant(Guid.NewGuid(), id, "Dummy");
            participant.SetMealAbsent(snack.Id, true); camps.Add(participant); await camps.SaveChangesAsync(TestContext.Current.CancellationToken);
        }
        if (reference == "legacy-override")
        {
            var config = new MealPlanningParticipantConfiguration(id);
            config.Update(MealPlanningDemandMode.EstimatedPlanning, "[{\"cookingUnitId\":\"" + Guid.NewGuid() + "\",\"defaultParticipantIds\":[],\"mealOverrides\":[{\"campMealId\":\"" + snack.Id + "\",\"participantIds\":[]}]}]");
            catering.Add(config);
        }
        await catering.SaveChangesAsync(TestContext.Current.CancellationToken);
        var result = await cateringService.RemoveAdditionalMealAsync(ownerUserId, tenantId, id, snack.Id, false, 1, TestContext.Current.CancellationToken);
        Assert.Equal(UpdateCampMealsFailure.None, result.Failure);
        Assert.Equal(outcome, result.Outcome);
        var saved = await catering.CampMeals.SingleOrDefaultAsync(x => x.Id == snack.Id, TestContext.Current.CancellationToken);
        if (outcome == "Deleted") Assert.Null(saved);
        else { Assert.NotNull(saved); Assert.False(saved.IsActive); Assert.Equal(1, saved.ChangeVersion); }
    }

    [Fact]
    public async Task Regular_configuration_and_period_changes_never_expand_additional_types()
    {
        var id = await ScheduleCampAsync();
        var snack = (await AddSnackAsync(id)).Meal!;
        string[] names = await catering.CampMealTypes.Where(x => x.Kind == CampMealKind.Regular).OrderByDescending(x => x.SortOrder).Select(x => x.Name).ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Equal(UpdateCampMealsFailure.None, await cateringService.UpdateMealTypesAsync(ownerUserId, tenantId, id,
            ScheduleStart, ScheduleEnd, false, new(names, 1), TestContext.Current.CancellationToken));
        Assert.Equal(UpdateCampFailure.None, (await service.UpdateAsync(otherUserId, id, new UpdateCampRequest("Schedule", ScheduleStart, ScheduleEnd.AddDays(1)), TestContext.Current.CancellationToken)).Failure);
        Assert.Equal(1, await catering.CampMeals.CountAsync(x => x.MealTypeId == snack.MealTypeId, TestContext.Current.CancellationToken));
        Assert.Equal(13, await catering.CampMeals.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(UpdateCampFailure.None, (await service.UpdateAsync(otherUserId, id, new UpdateCampRequest("Schedule", ScheduleStart.AddDays(1), ScheduleEnd), TestContext.Current.CancellationToken)).Failure);
        Assert.True(await catering.CampMeals.AnyAsync(x => x.Id == snack.Id, TestContext.Current.CancellationToken));
        Assert.Equal(UpdateCampFailure.None, (await service.UpdateAsync(otherUserId, id, new UpdateCampRequest("Schedule", ScheduleStart, ScheduleEnd.AddDays(1)), TestContext.Current.CancellationToken)).Failure);
        Assert.Equal(snack.Id, await catering.CampMeals.Where(x => x.MealTypeId == snack.MealTypeId).Select(x => x.Id).SingleAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Frozen_schedule_rejects_all_new_operations()
    {
        var id = await ScheduleCampAsync();
        var result = await cateringService.AddAdditionalMealAsync(ownerUserId, tenantId, id, ScheduleStart, ScheduleEnd, true,
            new(ScheduleStart, "Snack", 0), TestContext.Current.CancellationToken);
        Assert.Equal(UpdateCampMealsFailure.Frozen, result.Failure);
        Assert.Equal(9, await catering.CampMeals.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Schedule_change_and_version_roll_back_when_audit_insert_fails()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = await ScheduleCampAsync();
        long auditCount = await platform.AuditEvents.LongCountAsync(ct);
        await platform.Database.ExecuteSqlRawAsync("CREATE TRIGGER fail_schedule_audit BEFORE INSERT ON AuditEvents BEGIN SELECT RAISE(ABORT, 'audit failed'); END;", ct);
        await Assert.ThrowsAsync<DbUpdateException>(() => AddSnackAsync(id));
        catering.ChangeTracker.Clear();
        Assert.Equal(9, await catering.CampMeals.CountAsync(ct));
        Assert.Equal(0, await catering.CampMealScheduleStates.Select(x => x.Version).SingleAsync(ct));
        Assert.Equal(auditCount, await platform.AuditEvents.LongCountAsync(ct));
    }

    [Fact]
    public async Task Two_schedule_writers_with_same_version_cannot_both_commit()
    {
        var ct = TestContext.Current.CancellationToken;
        var id = await ScheduleCampAsync();
        await using var secondPlatform = new ScoutCampPlanner.Platform.Infrastructure.PlatformDbContext(
            new DbContextOptionsBuilder<ScoutCampPlanner.Platform.Infrastructure.PlatformDbContext>().UseSqlite(connection).Options);
        await using var secondCatering = new ScoutCampPlanner.Catering.Infrastructure.CateringDbContext(
            new DbContextOptionsBuilder<ScoutCampPlanner.Catering.Infrastructure.CateringDbContext>().UseSqlite(connection).Options);
        await using var secondCamps = new ScoutCampPlanner.Camp.Infrastructure.CampDbContext(
            new DbContextOptionsBuilder<ScoutCampPlanner.Camp.Infrastructure.CampDbContext>().UseSqlite(connection).Options);
        Guid instance = await platform.AuditJournalHeads.Select(x => x.InstanceId).SingleAsync(ct);
        var secondService = new CateringPlanningService(secondPlatform, secondCatering,
            new ScoutCampPlanner.Platform.Infrastructure.Auditing.AuditedOperationExecutor(secondPlatform, new FixedAuditKeyProvider()),
            new ScoutCampPlanner.Platform.Infrastructure.Auditing.AuditRuntimeState(instance), new FixedTimeProvider(Now), secondCamps);
        var results = await Task.WhenAll(AddSnackAsync(id), secondService.AddAdditionalMealAsync(ownerUserId, tenantId, id,
            ScheduleStart, ScheduleEnd, false, new(ScheduleEnd, "Second snack", 0), ct));
        Assert.Single(results, x => x.Failure == UpdateCampMealsFailure.None);
        Assert.Single(results, x => x.Failure == UpdateCampMealsFailure.Changed);
        catering.ChangeTracker.Clear();
        Assert.Equal(10, await catering.CampMeals.CountAsync(ct));
        Assert.Equal(1, await catering.CampMealScheduleStates.Select(x => x.Version).SingleAsync(ct));
    }
}
