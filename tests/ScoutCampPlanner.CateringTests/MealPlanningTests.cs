using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Camp.Contracts;
using ScoutCampPlanner.Catering.Application.MealPlanning;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Catering.Infrastructure.Recipes;
using ScoutCampPlanner.Catering.Application.Recipes;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class MealPlanningTests
{
    [Fact]
    public async Task Plan_requires_exactly_one_standard_and_no_duplicate_revision_per_group()
    {
        TestContextData context = CreateContext();
        var store = new RecordingStore(context.Data);
        var service = new MealPlanningService(store, new PlanningLookup(context.Camp), TimeProvider.System);
        var withoutStandard = new MealPlanOfferGroupDocument(
            context.GroupId, context.MealId, "Menü", 0,
            [new MealPlanEntryDocument(Guid.NewGuid(), Guid.NewGuid(), false, null, null, null, 0)]);

        MealPlanningMutationResult missing = await service.SaveMealPlanAsync(
            context.CampId, context.PlanId, Guid.NewGuid(),
            new SaveMealPlanRequest(0, "Standard", 0, [withoutStandard]), TestContext.Current.CancellationToken);
        Assert.Equal(MealPlanningMutationStatus.Invalid, missing.Status);

        Guid revisionId = Guid.NewGuid();
        var duplicate = withoutStandard with
        {
            Entries =
            [
                new MealPlanEntryDocument(Guid.NewGuid(), revisionId, true, null, null, null, 0),
                new MealPlanEntryDocument(Guid.NewGuid(), revisionId, false, null, null, null, 1),
            ],
        };
        MealPlanningMutationResult duplicated = await service.SaveMealPlanAsync(
            context.CampId, context.PlanId, Guid.NewGuid(),
            new SaveMealPlanRequest(0, "Standard", 0, [duplicate]), TestContext.Current.CancellationToken);

        Assert.Equal(MealPlanningMutationStatus.Invalid, duplicated.Status);
        Assert.Equal(0, store.SavePlanCalls);
    }

    [Fact]
    public async Task Parent_and_child_nodes_cannot_be_assigned_to_the_same_cooking_unit()
    {
        TestContextData context = CreateContext();
        var store = new RecordingStore(context.Data);
        var service = new MealPlanningService(store, new PlanningLookup(context.Camp), TimeProvider.System);

        MealPlanningMutationResult result = await service.SaveCookingUnitAsync(
            context.CampId, null,
            new SaveCookingUnitRequest("Küche", 0, null, null, [context.RootNodeId, context.LeafNodeId]),
            TestContext.Current.CancellationToken);

        Assert.Equal(MealPlanningMutationStatus.Invalid, result.Status);
        Assert.Equal("structure_assignment_invalid", result.Code);
    }

    [Fact]
    public async Task Calculation_uses_descendant_estimates_stage_factors_and_leaders_as_one()
    {
        TestContextData context = CreateContext(includeCompletePlan: true, includeCookingUnit: true);
        var store = new RecordingStore(context.Data);
        var service = new MealPlanningService(store, new PlanningLookup(context.Camp), TimeProvider.System);

        MealPlanningMutationResult result = await service.CalculateAsync(
            context.CampId, context.UnitId, context.MealId, TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(7m, store.LastCalculatedDemand); // 10 KiJu * 0.5 + 2 Leiter
        Assert.True(store.LastCalculationComplete);
        Assert.Equal(context.PlanId, store.LastCalculationPlanId);
        Assert.Equal(1, store.LastCalculationPlanVersion);
    }

    [Fact]
    public async Task Store_creates_one_version_per_supply_change_and_keeps_stable_child_ids()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Guid campId = Guid.NewGuid();
        Guid planId = Guid.NewGuid();
        Guid mealTypeId = Guid.NewGuid();
        Guid mealId = Guid.NewGuid();
        Guid groupId = Guid.NewGuid();
        Guid entryId = Guid.NewGuid();
        Guid firstRevision = Guid.NewGuid();
        Guid secondRevision = Guid.NewGuid();
        fixture.Database.AddRange(
            new MealPlan(planId, campId, "Plan"),
            new CampMealType(mealTypeId, campId, "Mittagessen", 0),
            new CampMeal(mealId, campId, mealTypeId, new DateOnly(2027, 7, 2)));
        await fixture.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        var store = new MealPlanningStore(fixture.Database);
        MealPlanOfferGroupDocument First(string name, Guid revision) => new(
            groupId, mealId, name, 0,
            [new MealPlanEntryDocument(entryId, revision, true, name, MealPlanEntryRole.MainDish, null, 0)]);

        MealPlanningMutationResult versionOne = await store.SaveMealPlanAsync(
            campId, planId, 0, "Plan", 0, [First("Menü", firstRevision)], "{}",
            DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);
        MealPlanningMutationResult displayOnly = await store.SaveMealPlanAsync(
            campId, planId, 1, "Neuer Anzeigename", 0, [First("Neue Bezeichnung", firstRevision)], "{}",
            DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);
        MealPlanningMutationResult versionTwo = await store.SaveMealPlanAsync(
            campId, planId, 1, "Neuer Anzeigename", 0, [First("Neue Bezeichnung", secondRevision)], "{}",
            DateTimeOffset.UtcNow, TestContext.Current.CancellationToken);

        Assert.Equal(1, versionOne.Version);
        Assert.Equal(1, displayOnly.Version);
        Assert.Equal(2, versionTwo.Version);
        Assert.Equal(2, await fixture.Database.MealPlanSnapshots.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(groupId, (await fixture.Database.MealPlanOfferGroups.SingleAsync(
            TestContext.Current.CancellationToken)).Id);
        Assert.Equal(entryId, (await fixture.Database.MealPlanEntries.SingleAsync(
            TestContext.Current.CancellationToken)).Id);
    }

    [Fact]
    public async Task Used_recipe_revision_cannot_be_removed_from_camp_library()
    {
        await using var fixture = await DatabaseFixture.CreateAsync();
        Guid campId = Guid.NewGuid();
        Guid recipeId = Guid.NewGuid();
        Guid revisionId = Guid.NewGuid();
        Guid planId = Guid.NewGuid();
        Guid mealTypeId = Guid.NewGuid();
        Guid mealId = Guid.NewGuid();
        Guid groupId = Guid.NewGuid();
        Guid libraryEntryId = Guid.NewGuid();
        Guid actorId = Guid.NewGuid();
        fixture.Database.AddRange(
            new RecipeRecord
            {
                Id = recipeId, ScopeType = (int)RecipeScopeType.Central, Name = "Reis",
                NormalizedName = "REIS", Status = (int)RecipeStatus.Active,
                RecipeType = (int)RecipeType.PortionBased, CreatedBy = actorId,
                CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedBy = actorId, UpdatedAtUtc = DateTimeOffset.UtcNow,
            },
            new RecipeRevisionRecord
            {
                Id = revisionId, RecipeId = recipeId, RevisionNumber = 1,
                PublishedAtUtc = DateTimeOffset.UtcNow, PublishedBy = actorId,
                SnapshotSchemaVersion = 2, SnapshotJson = "{}",
            },
            new CampRecipeEntryRecord
            {
                Id = libraryEntryId, CampId = campId, UpstreamRecipeRevisionId = revisionId,
                CreatedBy = actorId, CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedBy = actorId, UpdatedAtUtc = DateTimeOffset.UtcNow,
            },
            new MealPlan(planId, campId, "Standard"),
            new CampMealType(mealTypeId, campId, "Mittagessen", 0),
            new CampMeal(mealId, campId, mealTypeId, new DateOnly(2027, 7, 2)),
            new MealPlanOfferGroup(groupId, planId, mealId, null, 0),
            new MealPlanEntry(Guid.NewGuid(), groupId, revisionId, true, null, null, null, 0));
        await fixture.Database.SaveChangesAsync(TestContext.Current.CancellationToken);
        var library = new RecipeLibraryStore(fixture.Database, new RecipeDraftStore(fixture.Database));

        RecipeLibraryMutationResult result = await library.RemoveCampEntryAsync(
            campId, libraryEntryId, TestContext.Current.CancellationToken);

        Assert.Equal(RecipeLibraryMutationStatus.ReferenceBlocked, result.Status);
        Assert.Contains("Mahlzeitenplan: Standard", result.References!);
        Assert.True(await fixture.Database.Set<CampRecipeEntryRecord>().AnyAsync(
            value => value.Id == libraryEntryId, TestContext.Current.CancellationToken));
    }

    private static TestContextData CreateContext(bool includeCompletePlan = false, bool includeCookingUnit = false)
    {
        Guid campId = Guid.NewGuid();
        Guid tenantId = Guid.NewGuid();
        Guid planId = Guid.NewGuid();
        Guid mealTypeId = Guid.NewGuid();
        Guid mealId = Guid.NewGuid();
        Guid stageId = Guid.NewGuid();
        Guid rootNodeId = Guid.NewGuid();
        Guid leafNodeId = Guid.NewGuid();
        Guid groupId = Guid.NewGuid();
        Guid entryId = Guid.NewGuid();
        Guid revisionId = Guid.NewGuid();
        Guid unitId = Guid.NewGuid();
        var camp = new CampPlanningData(
            campId, tenantId, new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 10),
            [new CampPlanningNode(rootNodeId, null, "Lager"), new CampPlanningNode(leafNodeId, rootNodeId, "Gruppe")],
            [new CampPlanningStage(stageId, "Biber", 0)],
            [new CampPlanningEstimate(leafNodeId, stageId, 10, 2)]);
        var plan = new MealPlan(planId, campId, "Standard", 0, includeCompletePlan ? 1 : 0);
        var groups = includeCompletePlan
            ? new[] { new MealPlanOfferGroup(groupId, planId, mealId, "Menü", 0) }
            : [];
        var entries = includeCompletePlan
            ? new[] { new MealPlanEntry(entryId, groupId, revisionId, true, null, null, null, 0) }
            : [];
        var snapshots = includeCompletePlan
            ? new[] { new MealPlanSnapshot(Guid.NewGuid(), planId, campId, 1, "{}", DateTimeOffset.UtcNow) }
            : [];
        var units = includeCookingUnit
            ? new[] { new CookingUnit(unitId, campId, "Küche", 0, standardMealPlanId: planId) }
            : [];
        var assignments = includeCookingUnit
            ? new[] { new CookingUnitStructureAssignment(Guid.NewGuid(), campId, unitId, null, rootNodeId) }
            : [];
        var data = new MealPlanningData(
            [plan], snapshots, groups, entries,
            [new CampMealType(mealTypeId, campId, "Mittagessen", 0)],
            [new CampMeal(mealId, campId, mealTypeId, new DateOnly(2027, 7, 2))],
            [new CampStageFoodFactor(Guid.NewGuid(), campId, stageId, "Biber", 0.5m)],
            [], units, assignments, [], [], []);
        return new TestContextData(campId, planId, mealId, groupId, unitId, rootNodeId, leafNodeId, camp, data);
    }

    private sealed record TestContextData(
        Guid CampId, Guid PlanId, Guid MealId, Guid GroupId, Guid UnitId,
        Guid RootNodeId, Guid LeafNodeId, CampPlanningData Camp, MealPlanningData Data);

    private sealed class PlanningLookup(CampPlanningData data) : ICampPlanningLookup
    {
        public Task<CampPlanningData?> GetPlanningDataAsync(Guid campId, CancellationToken cancellationToken = default) =>
            Task.FromResult<CampPlanningData?>(campId == data.CampId ? data : null);
    }

    private sealed class RecordingStore(MealPlanningData data) : IMealPlanningStore
    {
        public int SavePlanCalls { get; private set; }
        public decimal? LastCalculatedDemand { get; private set; }
        public bool LastCalculationComplete { get; private set; }
        public Guid? LastCalculationPlanId { get; private set; }
        public int? LastCalculationPlanVersion { get; private set; }
        public Task<MealPlanningData> LoadAsync(Guid campId, CancellationToken cancellationToken = default) => Task.FromResult(data);
        public Task<IReadOnlyList<MealPlanningRecipeOption>> ListAccessibleRecipesAsync(Guid campId, Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MealPlanningRecipeOption>>([]);
        public Task<bool> AreRecipeRevisionsAccessibleAsync(Guid campId, Guid tenantId, IReadOnlyCollection<Guid> revisionIds, CancellationToken cancellationToken = default) => Task.FromResult(true);
        public Task EnsureCampRecipeLibraryAsync(Guid campId, Guid actorUserId, IReadOnlyCollection<Guid> revisionIds, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<MealPlanningMutationResult> CreateMealPlanAsync(MealPlan mealPlan, CancellationToken cancellationToken = default) => Success(mealPlan.Id);
        public Task<MealPlanningMutationResult> SaveMealPlanAsync(Guid campId, Guid mealPlanId, int expectedVersion, string name, int sortOrder, IReadOnlyList<MealPlanOfferGroupDocument> groups, string snapshotJson, DateTimeOffset savedAtUtc, CancellationToken cancellationToken = default) { SavePlanCalls++; return Success(mealPlanId, expectedVersion + 1); }
        public Task<MealPlanningMutationResult> DeleteMealPlanAsync(Guid campId, Guid mealPlanId, CancellationToken cancellationToken = default) => Success(mealPlanId);
        public Task<MealPlanningMutationResult> ReorderMealPlansAsync(Guid campId, IReadOnlyList<Guid> mealPlanIds, CancellationToken cancellationToken = default) => Success();
        public Task<MealPlanningMutationResult> SaveCookingUnitGroupAsync(CookingUnitGroup group, CancellationToken cancellationToken = default) => Success(group.Id);
        public Task<MealPlanningMutationResult> DeleteCookingUnitGroupAsync(Guid campId, Guid groupId, CancellationToken cancellationToken = default) => Success(groupId);
        public Task<MealPlanningMutationResult> SaveCookingUnitAsync(CookingUnit unit, IReadOnlyCollection<Guid> defaultStructureNodeIds, CancellationToken cancellationToken = default) => Success(unit.Id);
        public Task<MealPlanningMutationResult> DeleteCookingUnitAsync(Guid campId, Guid cookingUnitId, CancellationToken cancellationToken = default) => Success(cookingUnitId);
        public Task<MealPlanningMutationResult> ConfigureCookingUnitMealAsync(Guid campId, Guid cookingUnitId, Guid mealId, ConfigureCookingUnitMealRequest request, CancellationToken cancellationToken = default) => Success();
        public Task<MealPlanningMutationResult> ResetStructureOverrideAsync(Guid campId, Guid cookingUnitId, Guid mealId, CancellationToken cancellationToken = default) => Success();
        public Task SaveCalculationAsync(Guid campId, Guid cookingUnitId, Guid mealId, decimal? calculatedDemand, Guid? mealPlanId, int? mealPlanVersion, Guid? snapshotId, string calculationJson, string sourceFingerprint, IReadOnlyList<string> warnings, bool complete, DateTimeOffset calculatedAtUtc, CancellationToken cancellationToken = default)
        {
            LastCalculatedDemand = calculatedDemand;
            LastCalculationComplete = complete;
            LastCalculationPlanId = mealPlanId;
            LastCalculationPlanVersion = mealPlanVersion;
            return Task.CompletedTask;
        }
        private static Task<MealPlanningMutationResult> Success(Guid? id = null, int? version = null) => Task.FromResult(new MealPlanningMutationResult(MealPlanningMutationStatus.Success, Id: id, Version: version));
    }

    private sealed class DatabaseFixture(SqliteConnection connection, CateringDbContext database) : IAsyncDisposable
    {
        public CateringDbContext Database { get; } = database;
        public static async Task<DatabaseFixture> CreateAsync()
        {
            SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
            var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            var database = new CateringDbContext(new DbContextOptionsBuilder<CateringDbContext>().UseSqlite(connection).Options);
            await database.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            return new DatabaseFixture(connection, database);
        }
        public async ValueTask DisposeAsync()
        {
            await Database.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
