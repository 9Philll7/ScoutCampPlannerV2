using ScoutCampPlanner.Camp.Contracts;
using ScoutCampPlanner.Catering.Application.MealPlanning;
using ScoutCampPlanner.Catering.Domain;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed partial class MealPlanningTests
{
    private sealed class ParticipantLookup : ICampCateringParticipantLookup
    {
        public List<CateringParticipantData> Values { get; } = [];
        public Task<IReadOnlyList<CateringParticipantData>> GetMealParticipantsAsync(Guid campId, DateOnly date, Guid mealId, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<CateringParticipantData>>(Values.ToArray());
    }
    private static CateringParticipantData Person(Guid? node, bool special = false) =>
        new(Guid.NewGuid(), node, true, special ? Guid.NewGuid() : null, [], []);

    [Theory]
    [InlineData(CookingUnitParticipantFilter.All, 2)]
    [InlineData(CookingUnitParticipantFilter.SpecialCateringOnly, 1)]
    [InlineData(CookingUnitParticipantFilter.WithoutSpecialCatering, 1)]
    public void Structure_filters_use_derived_requirements(CookingUnitParticipantFilter filter, int expected)
    {
        var context = CreateContext(true, true);
        var result = StructureParticipantProjection.Derive(context.Camp.Nodes,
            [Person(context.LeafNodeId), Person(context.LeafNodeId, true)],
            [new(context.UnitId, [context.RootNodeId], filter)]);
        Assert.Equal(expected, result.ParticipantsByUnit[context.UnitId].Count);
        Assert.Empty(result.Overlapping);
    }

    [Fact]
    public void Overlapping_present_people_are_not_silently_assigned_and_complementary_filters_are_disjoint()
    {
        var context = CreateContext(true, true); Guid other = Guid.NewGuid();
        var person = Person(context.LeafNodeId, true);
        var units = new StructureParticipantSelection[] {
            new(context.UnitId, [context.RootNodeId], CookingUnitParticipantFilter.All),
            new(other, [context.LeafNodeId], CookingUnitParticipantFilter.SpecialCateringOnly) };
        var overlap = StructureParticipantProjection.Derive(context.Camp.Nodes, [person], units);
        Assert.Single(overlap.Overlapping); Assert.All(overlap.ParticipantsByUnit.Values, Assert.Empty);
        Assert.Empty(StructureParticipantProjection.Derive(context.Camp.Nodes, [person with { IsPresent = false }], units).Overlapping);
        units[0] = units[0] with { Filter = CookingUnitParticipantFilter.WithoutSpecialCatering };
        Assert.Empty(StructureParticipantProjection.Derive(context.Camp.Nodes, [person], units).Overlapping);
    }

    [Fact]
    public async Task Actual_basis_groups_attendance_and_unassigned_are_operational()
    {
        var ct = TestContext.Current.CancellationToken;
        var context = CreateContext(true, true); var store = new RecordingStore(context.Data);
        var lookup = new ParticipantLookup(); lookup.Values.Add(Person(context.LeafNodeId));
        var service = new MealPlanningService(store, new PlanningLookup(context.Camp), TimeProvider.System, lookup);
        Assert.True((await service.SaveParticipantConfigurationAsync(context.CampId, new(0, MealPlanningDemandMode.UseActualParticipants), ct)).IsSuccess);
        await service.CalculateAsync(context.CampId, context.UnitId, context.MealId, ct);
        Assert.Equal(1m, store.LastCalculatedDemand); Assert.True(store.LastCalculationComplete);
        var state = Assert.Single((await service.GetOverviewAsync(context.CampId, ct))!.OperationalMeals);
        Assert.Single(state.RequirementGroups!); Assert.Equal(EffectiveDemandBasis.ActualParticipants, state.DemandBasis);
        lookup.Values[0] = lookup.Values[0] with { IsPresent = false };
        Assert.Equal(OperationalMealPlanStatus.Stale, Assert.Single((await service.GetOverviewAsync(context.CampId, ct))!.OperationalMeals).Status);
        await service.CalculateAsync(context.CampId, context.UnitId, context.MealId, ct);
        Assert.Equal(0m, store.LastCalculatedDemand);
        Assert.Equal(EffectiveDemandBasis.ActualParticipants, Assert.Single((await service.GetOverviewAsync(context.CampId, ct))!.OperationalMeals).DemandBasis);
        lookup.Values.Add(Person(null));
        await service.CalculateAsync(context.CampId, context.UnitId, context.MealId, ct);
        Assert.False(store.LastCalculationComplete);
        Assert.Single(Assert.Single((await service.GetOverviewAsync(context.CampId, ct))!.OperationalMeals).UnassignedParticipantIds!);
    }

    [Fact]
    public async Task Unit_save_rejects_overlap_and_direct_assignment_endpoint_no_longer_accepts_people()
    {
        var ct = TestContext.Current.CancellationToken;
        var context = CreateContext(true, true); var lookup = new ParticipantLookup(); lookup.Values.Add(Person(context.LeafNodeId));
        var service = new MealPlanningService(new RecordingStore(context.Data), new PlanningLookup(context.Camp), TimeProvider.System, lookup);
        var result = await service.SaveCookingUnitAsync(context.CampId, null, new("Other", 1, null, null, [context.LeafNodeId]), ct);
        Assert.Equal(MealPlanningMutationStatus.Invalid, result.Status);
        Assert.Equal("overlapping_cooking_units", result.Code);
        Assert.False((await service.SaveParticipantConfigurationAsync(context.CampId,
            new(0, MealPlanningDemandMode.UseActualParticipants, [new(context.UnitId, [lookup.Values[0].ParticipantReference], [])]), ct)).IsSuccess);
    }

    [Fact]
    public async Task Special_filter_with_only_standard_people_is_actual_zero_not_estimated_fallback()
    {
        var ct = TestContext.Current.CancellationToken; var context = CreateContext(true, true);
        context.Data.CookingUnits[0].SetParticipantFilter(CookingUnitParticipantFilter.SpecialCateringOnly);
        var store = new RecordingStore(context.Data); var lookup = new ParticipantLookup(); lookup.Values.Add(Person(context.LeafNodeId));
        var service = new MealPlanningService(store, new PlanningLookup(context.Camp), TimeProvider.System, lookup);
        await service.SaveParticipantConfigurationAsync(context.CampId, new(0, MealPlanningDemandMode.UseActualParticipants), ct);
        await service.CalculateAsync(context.CampId, context.UnitId, context.MealId, ct);
        Assert.Equal(0m, store.LastCalculatedDemand);
        Assert.Equal(EffectiveDemandBasis.ActualParticipants, Assert.Single((await service.GetOverviewAsync(context.CampId, ct))!.OperationalMeals).DemandBasis);
    }

    [Fact]
    public void Projection_contract_has_no_names_health_dossier_or_source_metadata()
    {
        Assert.Equal(new[] { "AllergenIds", "DietTypeId", "Intolerances", "IsPresent", "ParticipantReference", "RequiresSpecialCatering", "StructureNodeId" },
            typeof(CateringParticipantData).GetProperties().Select(value => value.Name).Order().ToArray());
        Assert.DoesNotContain(typeof(CateringIntoleranceData).GetProperties(), value => value.Name.Contains("Source"));
    }

    [Fact]
    public void Legacy_migration_requires_unambiguous_leaf_and_never_discards_meal_overrides()
    {
        var context = CreateContext(true, true); Guid person = Guid.NewGuid();
        CookingUnitParticipantAssignments[] assignments = [new(context.UnitId, [person], [])];
        var nodes = new Dictionary<Guid, IReadOnlyList<Guid>> { [context.UnitId] = new[] { context.RootNodeId } };
        Assert.Equal(context.LeafNodeId, LegacyParticipantStructureMigration.Resolve(context.Camp.Nodes, assignments, nodes)[person]);
        Assert.Throws<InvalidOperationException>(() => LegacyParticipantStructureMigration.Resolve(context.Camp.Nodes,
            [assignments[0] with { MealOverrides = [new(context.MealId, [])] }], nodes));
        Assert.Throws<InvalidOperationException>(() => LegacyParticipantStructureMigration.Resolve(
            context.Camp.Nodes.Append(new CampPlanningNode(Guid.NewGuid(), context.RootNodeId, "Second leaf")).ToArray(), assignments, nodes));
    }
}
