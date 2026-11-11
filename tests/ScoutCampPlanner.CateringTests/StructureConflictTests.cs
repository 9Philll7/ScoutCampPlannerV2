using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Camp.Contracts;
using ScoutCampPlanner.Catering.Application.MealPlanning;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed partial class MealPlanningTests
{
    [Fact]
    public async Task Structure_resolution_groups_meals_preserves_siblings_and_previews_overrides_without_writing()
    {
        var ct = TestContext.Current.CancellationToken; var context = CreateContext(false, true);
        var second = new CookingUnit(Guid.NewGuid(), context.CampId, "Zentral", 1);
        var sibling = new CampPlanningNode(Guid.NewGuid(), context.RootNodeId, "Andere Gruppe");
        var camp = context.Camp with { Nodes = context.Camp.Nodes.Append(sibling).ToArray() };
        var extraMeal = new CampMeal(Guid.NewGuid(), context.CampId, context.Data.MealTypes[0].Id, new(2027, 7, 3));
        var data = context.Data with {
            CookingUnits = context.Data.CookingUnits.Append(second).ToArray(), Meals = context.Data.Meals.Append(extraMeal).ToArray(),
            StructureAssignments = context.Data.StructureAssignments.Concat(new[] {
                new CookingUnitStructureAssignment(Guid.NewGuid(), context.CampId, second.Id, null, context.LeafNodeId),
                new CookingUnitStructureAssignment(Guid.NewGuid(), context.CampId, context.UnitId, extraMeal.Id, context.RootNodeId) }).ToArray()
        };
        var store = new RecordingStore(data); var service = new MealPlanningService(store, new PlanningLookup(camp), TimeProvider.System);
        var conflicts = (await service.GetStructureConflictsAsync(context.CampId, ct))!;
        var conflict = Assert.Single(conflicts.Conflicts); Assert.Equal(2, conflict.MealIds.Count); Assert.Null(conflict.Persons);
        var request = new ResolveStructureConflictRequest(conflicts.Version, conflict.Id, second.Id, true, conflict.MealIds);
        var preview = (await service.PreviewStructureResolutionAsync(context.CampId, request, ct))!;
        Assert.Equal(1, preview.Resolved); Assert.Equal(1, preview.Remaining); Assert.Equal(0, preview.NewConflicts);
        Assert.Equal(sibling.Id, Assert.Single(Assert.Single(preview.Changes).NodeIds));
        Assert.Equal(3, (await store.LoadAsync(context.CampId, ct)).StructureAssignments.Count);
        var selected = (await service.PreviewStructureResolutionAsync(context.CampId, request with { UseDefaults = false, MealIds = [extraMeal.Id] }, ct))!;
        Assert.Equal(1, selected.Resolved); Assert.Equal(1, selected.Remaining); Assert.Equal(extraMeal.Id, Assert.Single(selected.Changes).MealId);
        Assert.Null(await service.PreviewStructureResolutionAsync(context.CampId, request with { ExpectedVersion = "outdated" }, ct));
    }

    [Fact]
    public async Task Structure_conflicts_respect_filters_and_preview_marks_newly_uncovered_people()
    {
        var ct = TestContext.Current.CancellationToken; var context = CreateContext(false, true);
        var special = new CookingUnit(Guid.NewGuid(), context.CampId, "Sonderkost", 1);
        special.SetParticipantFilter(CookingUnitParticipantFilter.SpecialCateringOnly);
        var data = context.Data with { CookingUnits = context.Data.CookingUnits.Append(special).ToArray(),
            StructureAssignments = context.Data.StructureAssignments.Append(new CookingUnitStructureAssignment(Guid.NewGuid(), context.CampId, special.Id, null, context.RootNodeId)).ToArray() };
        var people = new ParticipantLookup(); people.Values.AddRange([Person(context.LeafNodeId), Person(context.LeafNodeId, true)]);
        var store = new RecordingStore(data); var service = new MealPlanningService(store, new PlanningLookup(context.Camp), TimeProvider.System, people);
        await service.SaveParticipantConfigurationAsync(context.CampId, new(0, MealPlanningDemandMode.UseActualParticipants), ct);
        var document = (await service.GetStructureConflictsAsync(context.CampId, ct))!;
        var conflict = Assert.Single(document.Conflicts); Assert.Equal(1, conflict.Persons);
        var overviewBefore = (await service.GetOverviewAsync(context.CampId, ct))!;
        Assert.Equal(document.Conflicts.Select(value => value.Id), overviewBefore.StructureConflicts!.Conflicts.Select(value => value.Id));
        Assert.Contains(overviewBefore.CoverageWarnings, warning => warning.Contains("Mehrfachzuordnung"));
        var request = new ResolveStructureConflictRequest(document.Version, conflict.Id, special.Id, true, conflict.MealIds);
        var preview = (await service.PreviewStructureResolutionAsync(context.CampId, request, ct))!;
        Assert.Equal(1, preview.NewConflicts);
        var mealPreview = (await service.PreviewStructureResolutionAsync(context.CampId, request with { UseDefaults = false }, ct))!;
        Assert.Equal(1, mealPreview.NewConflicts);
        Assert.Equal("structure_resolution_incomplete", (await service.ApplyStructureResolutionAsync(context.CampId, request, ct)).Code);
        context.Data.CookingUnits[0].SetParticipantFilter(CookingUnitParticipantFilter.WithoutSpecialCatering);
        Assert.Empty((await service.GetStructureConflictsAsync(context.CampId, ct))!.Conflicts);
        Assert.Empty((await service.GetOverviewAsync(context.CampId, ct))!.CoverageWarnings);
    }

    [Fact]
    public async Task Structure_conflicts_use_estimated_fallback_when_actual_participants_are_not_yet_available()
    {
        var ct = TestContext.Current.CancellationToken; var context = CreateContext(false, true);
        var second = new CookingUnit(Guid.NewGuid(), context.CampId, "Zentral", 1);
        var data = context.Data with { CookingUnits = context.Data.CookingUnits.Append(second).ToArray(),
            StructureAssignments = context.Data.StructureAssignments.Append(new CookingUnitStructureAssignment(Guid.NewGuid(), context.CampId, second.Id, null, context.RootNodeId)).ToArray() };
        var store = new RecordingStore(data); var service = new MealPlanningService(store, new PlanningLookup(context.Camp), TimeProvider.System, new ParticipantLookup());
        await service.SaveParticipantConfigurationAsync(context.CampId, new(0, MealPlanningDemandMode.UseActualParticipants), ct);
        var document = (await service.GetStructureConflictsAsync(context.CampId, ct))!;
        Assert.False(document.ActualParticipants); Assert.Null(Assert.Single(document.Conflicts).Persons);
    }

    [Fact]
    public async Task Missing_supply_is_assigned_directly_when_no_explicit_exclusion_exists()
    {
        var ct = TestContext.Current.CancellationToken; await using var f = await DatabaseFixture.CreateAsync();
        var context = CreateContext(false, true);
        f.Database.AddRange(context.Data.MealPlans.Cast<object>().Concat(context.Data.CookingUnits).Concat(context.Data.MealTypes).Concat(context.Data.Meals));
        await f.Database.SaveChangesAsync(ct); f.Database.ChangeTracker.Clear();
        var store = new MealPlanningStore(f.Database); var service = new MealPlanningService(store, new PlanningLookup(context.Camp), TimeProvider.System);
        var document = (await service.GetStructureConflictsAsync(context.CampId, ct))!; var conflict = Assert.Single(document.Conflicts);
        Assert.Equal("missing", conflict.Kind);
        var request = new ResolveStructureConflictRequest(document.Version, conflict.Id, context.UnitId, true, [context.MealId]);
        var preview = (await service.PreviewStructureResolutionAsync(context.CampId, request, ct))!;
        Assert.Equal(1, preview.Resolved); Assert.Equal(0, preview.NewConflicts);
        Assert.True((await service.ApplyStructureResolutionAsync(context.CampId, request, ct)).IsSuccess);
        Assert.Empty((await service.GetStructureConflictsAsync(context.CampId, ct))!.Conflicts);
        Assert.Equal(MealPlanningMutationStatus.Conflict, (await service.ApplyStructureResolutionAsync(context.CampId, request, ct)).Status);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Explicit_no_supply_is_not_a_missing_assignment_or_a_global_participant_problem(bool actual)
    {
        var ct = TestContext.Current.CancellationToken; var context = CreateContext(false, true);
        var sibling = new CampPlanningNode(Guid.NewGuid(), context.RootNodeId, "Andere Gruppe");
        var camp = context.Camp with { Nodes = context.Camp.Nodes.Append(sibling).ToArray() };
        var active = new CookingUnit(Guid.NewGuid(), context.CampId, "Aktive Küche", 1);
        var state = new CookingUnitMealState(Guid.NewGuid(), context.CampId, context.UnitId, context.MealId);
        state.Configure(MealPlanSubscriptionState.NoSupplyRequired, null);
        var data = context.Data with { CookingUnits = context.Data.CookingUnits.Append(active).ToArray(), MealStates = [state], StructureAssignments = [
            new(Guid.NewGuid(), context.CampId, context.UnitId, null, context.LeafNodeId),
            new(Guid.NewGuid(), context.CampId, active.Id, null, sibling.Id)] };
        var people = new ParticipantLookup(); people.Values.AddRange([Person(context.LeafNodeId), Person(context.LeafNodeId, true), Person(sibling.Id)]);
        var store = new RecordingStore(data); var service = new MealPlanningService(store, new PlanningLookup(camp), TimeProvider.System, people);
        if (actual) await service.SaveParticipantConfigurationAsync(context.CampId, new(0, MealPlanningDemandMode.UseActualParticipants), ct);
        Assert.Empty((await service.GetStructureConflictsAsync(context.CampId, ct))!.Conflicts);
        var overview = (await service.GetOverviewAsync(context.CampId, ct))!;
        Assert.Empty(overview.CoverageWarnings); Assert.Empty(overview.StructureConflicts!.Conflicts);
        await service.CalculateAsync(context.CampId, active.Id, context.MealId, ct);
        var activeState = (await service.GetOverviewAsync(context.CampId, ct))!.OperationalMeals.Single(value => value.CookingUnitId == active.Id);
        Assert.Empty(activeState.UnassignedParticipantIds!);
        Assert.DoesNotContain(activeState.ParticipantProblems!, value => value.ReasonCode == "UnassignedParticipant");
    }

    [Fact]
    public async Task Overview_and_resolver_share_the_same_version_with_inactive_meals()
    {
        var ct = TestContext.Current.CancellationToken; var context = CreateContext(false, true);
        var second = new CookingUnit(Guid.NewGuid(), context.CampId, "Zentral", 1);
        var inactive = new CampMeal(Guid.NewGuid(), context.CampId, context.Data.MealTypes[0].Id, new(2027, 7, 3));
        inactive.SetActive(false);
        var data = context.Data with { CookingUnits = context.Data.CookingUnits.Append(second).ToArray(), Meals = context.Data.Meals.Append(inactive).ToArray(),
            StructureAssignments = context.Data.StructureAssignments.Append(new CookingUnitStructureAssignment(Guid.NewGuid(), context.CampId, second.Id, null, context.RootNodeId)).ToArray() };
        var people = new ParticipantLookup(); people.Values.Add(Person(context.LeafNodeId));
        var store = new RecordingStore(data); var service = new MealPlanningService(store, new PlanningLookup(context.Camp), TimeProvider.System, people);
        await service.SaveParticipantConfigurationAsync(context.CampId, new(0, MealPlanningDemandMode.UseActualParticipants), ct);
        var overview = (await service.GetOverviewAsync(context.CampId, ct))!;
        var document = (await service.GetStructureConflictsAsync(context.CampId, ct))!;
        Assert.Equal(document.Version, overview.StructureConflicts!.Version);
        var conflict = Assert.Single(document.Conflicts);
        var request = new ResolveStructureConflictRequest(overview.StructureConflicts.Version, conflict.Id, context.UnitId, true, conflict.MealIds);
        Assert.NotNull(await service.PreviewStructureResolutionAsync(context.CampId, request, ct));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Multiple_selected_units_cover_complementary_requirements_and_reject_double_supply(bool useDefaults)
    {
        var ct = TestContext.Current.CancellationToken; await using var f = await DatabaseFixture.CreateAsync();
        var context = CreateContext(false, true);
        var special = context.Data.CookingUnits[0]; special.SetParticipantFilter(CookingUnitParticipantFilter.SpecialCateringOnly);
        var standard = new CookingUnit(Guid.NewGuid(), context.CampId, "Standard", 1);
        standard.SetParticipantFilter(CookingUnitParticipantFilter.WithoutSpecialCatering);
        var all = new CookingUnit(Guid.NewGuid(), context.CampId, "Alle", 2);
        f.Database.AddRange(context.Data.MealPlans.Cast<object>().Concat(context.Data.CookingUnits).Concat(context.Data.MealTypes).Concat(context.Data.Meals));
        f.Database.AddRange(standard, all); await f.Database.SaveChangesAsync(ct); f.Database.ChangeTracker.Clear();
        var people = new ParticipantLookup(); people.Values.AddRange([Person(context.LeafNodeId), Person(context.LeafNodeId, true)]);
        var store = new MealPlanningStore(f.Database); var service = new MealPlanningService(store, new PlanningLookup(context.Camp), TimeProvider.System, people);
        await service.SaveParticipantConfigurationAsync(context.CampId, new(0, MealPlanningDemandMode.UseActualParticipants), ct);
        var document = (await service.GetStructureConflictsAsync(context.CampId, ct))!; var conflict = Assert.Single(document.Conflicts);
        var request = new ResolveStructureConflictRequest(document.Version, conflict.Id, special.Id, useDefaults, [context.MealId], [special.Id, standard.Id]);
        var unsafeRequest = request with { CookingUnitIds = [all.Id, standard.Id] };
        var unsafePreview = (await service.PreviewStructureResolutionAsync(context.CampId, unsafeRequest, ct))!;
        Assert.Equal(1, unsafePreview.OverlappingMeals);
        Assert.Equal("structure_resolution_incomplete", (await service.ApplyStructureResolutionAsync(context.CampId, unsafeRequest, ct)).Code);
        Assert.Empty(await f.Database.CookingUnitStructureAssignments.ToArrayAsync(ct));
        Assert.Null(await service.PreviewStructureResolutionAsync(context.CampId, request with { CookingUnitIds = [] }, ct));
        Assert.Null(await service.PreviewStructureResolutionAsync(context.CampId, request with { CookingUnitIds = [special.Id, special.Id] }, ct));
        Assert.Null(await service.PreviewStructureResolutionAsync(context.CampId, request with { CookingUnitIds = [Guid.NewGuid()] }, ct));
        var preview = (await service.PreviewStructureResolutionAsync(context.CampId, request, ct))!;
        Assert.Equal(1, preview.Resolved); Assert.Equal(0, preview.NewConflicts); Assert.Equal(0, preview.OverlappingMeals);
        Assert.Equal(2, preview.Changes.Count);
        Assert.True((await service.ApplyStructureResolutionAsync(context.CampId, request, ct)).IsSuccess);
        Assert.Empty((await service.GetStructureConflictsAsync(context.CampId, ct))!.Conflicts);
        var assigned = await f.Database.CookingUnitStructureAssignments.AsNoTracking().ToArrayAsync(ct);
        Assert.Equal(2, assigned.Length); Assert.All(assigned, value => Assert.Equal(useDefaults ? (Guid?)null : context.MealId, value.CampMealId));
        Assert.Equal(new[] { special.Id, standard.Id }.Order(), assigned.Select(value => value.CookingUnitId).Order());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Resolution_rejects_overlapping_filters_even_without_matching_people(bool useDefaults, bool specialPerson)
    {
        var ct = TestContext.Current.CancellationToken; var context = CreateContext(false, true);
        var filtered = new CookingUnit(Guid.NewGuid(), context.CampId, "Filtered", 1);
        filtered.SetParticipantFilter(specialPerson ? CookingUnitParticipantFilter.WithoutSpecialCatering : CookingUnitParticipantFilter.SpecialCateringOnly);
        var data = context.Data with { StructureAssignments = [], CookingUnits = context.Data.CookingUnits.Append(filtered).ToArray() };
        var people = new ParticipantLookup(); people.Values.Add(Person(context.LeafNodeId, specialPerson));
        var store = new RecordingStore(data);
        var service = new MealPlanningService(store, new PlanningLookup(context.Camp), TimeProvider.System, people);
        await service.SaveParticipantConfigurationAsync(context.CampId, new(0, MealPlanningDemandMode.UseActualParticipants), ct);
        var document = (await service.GetStructureConflictsAsync(context.CampId, ct))!;
        var conflict = Assert.Single(document.Conflicts);
        var request = new ResolveStructureConflictRequest(document.Version, conflict.Id, context.UnitId, useDefaults, [context.MealId], [context.UnitId, filtered.Id]);
        var preview = (await service.PreviewStructureResolutionAsync(context.CampId, request, ct))!;
        Assert.Equal(0, preview.OverlappingMeals);
        Assert.True(preview.OverlappingFilters);
        Assert.Equal("structure_resolution_incomplete", (await service.ApplyStructureResolutionAsync(context.CampId, request, ct)).Code);
        Assert.Empty((await store.LoadAsync(context.CampId, ct)).StructureAssignments);
    }

    [Fact]
    public async Task Structure_resolution_commits_selected_meal_and_rejects_stale_second_save()
    {
        var ct = TestContext.Current.CancellationToken; await using var f = await DatabaseFixture.CreateAsync();
        var context = CreateContext(false, true); var second = new CookingUnit(Guid.NewGuid(), context.CampId, "Zentral", 1);
        f.Database.AddRange(context.Data.MealPlans.Cast<object>().Concat(context.Data.CookingUnits).Concat(context.Data.MealTypes).Concat(context.Data.Meals).Concat(context.Data.StructureAssignments));
        f.Database.Add(second); f.Database.Add(new CookingUnitStructureAssignment(Guid.NewGuid(), context.CampId, second.Id, null, context.RootNodeId));
        await f.Database.SaveChangesAsync(ct); f.Database.ChangeTracker.Clear();
        var store = new MealPlanningStore(f.Database); var service = new MealPlanningService(store, new PlanningLookup(context.Camp), TimeProvider.System);
        var document = (await service.GetStructureConflictsAsync(context.CampId, ct))!; var conflict = Assert.Single(document.Conflicts);
        var request = new ResolveStructureConflictRequest(document.Version, conflict.Id, context.UnitId, false, [context.MealId]);
        var preview = (await service.PreviewStructureResolutionAsync(context.CampId, request, ct))!;
        Assert.True(Assert.Single(preview.Changes).NoSupply); Assert.Equal(0, preview.NewConflicts);
        Assert.True((await service.ApplyStructureResolutionAsync(context.CampId, request, ct)).IsSuccess);
        Assert.Empty((await service.GetStructureConflictsAsync(context.CampId, ct))!.Conflicts);
        var saved = await f.Database.CookingUnitMealStates.AsNoTracking().SingleAsync(ct);
        Assert.Equal(second.Id, saved.CookingUnitId); Assert.Equal(MealPlanSubscriptionState.NoSupplyRequired, saved.SubscriptionState);
        Assert.Equal(MealPlanningMutationStatus.Conflict, (await service.ApplyStructureResolutionAsync(context.CampId, request, ct)).Status);
        Assert.Equal(2, await f.Database.CookingUnitStructureAssignments.CountAsync(ct));
    }
}
