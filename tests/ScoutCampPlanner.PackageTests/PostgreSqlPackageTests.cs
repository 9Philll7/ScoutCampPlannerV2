using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.Json;
using ScoutCampPlanner.Camp.Domain;
using ScoutCampPlanner.Camp.Infrastructure;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Application.MealPlanning;
using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Catering.Infrastructure.Recipes;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Catering.Infrastructure.Offline;
using ScoutCampPlanner.Package;
using ScoutCampPlanner.Platform.Domain;
using ScoutCampPlanner.Platform.Infrastructure;
using Xunit;

namespace ScoutCampPlanner.PackageTests;

public sealed class PostgreSqlPackageTests
{
    [Fact]
    public async Task Dummy_participants_roundtrip_PostgreSql_to_Sqlite_with_atomic_return_replacement()
    {
        var connectionString = Environment.GetEnvironmentVariable("SCOUTCAMPPLANNER_POSTGRES_TEST");
        if (string.IsNullOrWhiteSpace(connectionString)) return;
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await ResetSchemasAsync(connection);
        await using var platform = new PlatformDbContext(new DbContextOptionsBuilder<PlatformDbContext>().UseNpgsql(connection).Options);
        await using var camp = new CampDbContext(new DbContextOptionsBuilder<CampDbContext>().UseNpgsql(connection).Options);
        await using var catering = new CateringDbContext(new DbContextOptionsBuilder<CateringDbContext>().UseNpgsql(connection).Options);
        await platform.Database.ExecuteSqlRawAsync(platform.Database.GenerateCreateScript());
        await camp.Database.ExecuteSqlRawAsync(camp.Database.GenerateCreateScript());
        await catering.Database.ExecuteSqlRawAsync(catering.Database.GenerateCreateScript());
        var tenantId = Guid.NewGuid();
        var campId = Guid.NewGuid();
        platform.Tenants.Add(new Tenant(tenantId, "Dummy tenant"));
        camp.Camps.Add(new Camp.Domain.Camp(campId, tenantId, "Dummy camp", new(2027, 7, 1), new(2027, 7, 3)));
        camp.CampStages.Add(new CampStage(Guid.NewGuid(), campId, "GuSp", 0));
        var catalog = await new ParticipantRequirementCatalogStore(catering).ReadAsync();
        var participant = new Participant(Guid.NewGuid(), campId, "Dummy person");
        participant.SetRequirements(null, [catalog.Allergens[0].Id],
            [new(catalog.Substances.First(value => value.Code == "LACTOSE").Id, 0.123456m, "Dummy source")]);
        camp.Participants.Add(participant);
        var parent = new StructureNode(Guid.NewGuid(), campId, null, "Dummy parent");
        camp.StructureNodes.Add(parent);
        var node = new StructureNode(Guid.NewGuid(), campId, parent.Id, "Dummy group");
        camp.StructureNodes.Add(node);
        participant.AssignStructureNode(node.Id);
        var unit = new CookingUnit(Guid.NewGuid(), campId, "Dummy kitchen", 0);
        var mealType = new CampMealType(Guid.NewGuid(), campId, "Dummy meal", 0);
        var meal = new CampMeal(Guid.NewGuid(), campId, mealType.Id, new(2027, 7, 1));
        var recipeId = Guid.NewGuid(); var revisionId = Guid.NewGuid(); var actor = Guid.NewGuid();
        catering.AddRange(unit, mealType, meal, new CookingUnitStructureAssignment(Guid.NewGuid(), campId, unit.Id, null, node.Id),
            new RecipeRecord { Id = recipeId, Name = "Dummy reference recipe", NormalizedName = "DUMMY REFERENCE RECIPE",
                ScopeType = (int)RecipeScopeType.Central, Status = (int)RecipeStatus.Active, RecipeType = (int)RecipeType.PortionBased,
                CreatedBy = actor, UpdatedBy = actor, CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedAtUtc = DateTimeOffset.UtcNow },
            new RecipeRevisionRecord { Id = revisionId, RecipeId = recipeId, RevisionNumber = 1, PublishedBy = actor,
                PublishedAtUtc = DateTimeOffset.UtcNow, SnapshotSchemaVersion = 2,
                SnapshotJson = RecipeSnapshotBuilder.Serialize(new(2, "Dummy reference recipe", null, null, null,
                    RecipeType.PortionBased, new(1, 1, null, null), null, false, [], [], [], [], [])) });
        await platform.SaveChangesAsync();
        await camp.SaveChangesAsync();
        await catering.SaveChangesAsync();
        var ct = TestContext.Current.CancellationToken;
        var store = new MealPlanningStore(catering);
        await store.SaveParticipantConfigurationAsync(campId, 0, MealPlanningDemandMode.UseActualParticipants, "[]", ct);
        await store.ConfigureCookingUnitMealAsync(campId, unit.Id, meal.Id,
            new(MealPlanSubscriptionState.Custom, null, null, [], [new(Guid.NewGuid(), revisionId, null, null, 0)]), ct);
        var cloudPlanning = new MealPlanningService(store, camp, TimeProvider.System, camp, new EfSupplySuitabilityReferences(catering));
        await cloudPlanning.CalculateAsync(campId, unit.Id, meal.Id, ct);
        var originalState = await catering.CookingUnitMealStates.AsNoTracking().SingleAsync(ct);
        var packages = new CampPackageService(platform, camp, catering, TimeProvider.System, new CampPackageTests.TestParticipantAccess());
        byte[] initial = await packages.StartOfflineTransferAsync(campId);
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
        await using var local = await CampPackageTests.DatabaseHarness.CreateAsync();
        await local.Packages.ImportInitialPackageAsync(initial);
        var imported = await local.Camp.Participants.SingleAsync();
        imported.Rename("Edited dummy person");
        imported.SetDayAbsent(new(2027, 7, 2), true);
        await local.SaveAsync();
        var localPlanning = new MealPlanningService(new MealPlanningStore(local.Catering), local.Camp, TimeProvider.System,
            local.Camp, new EfSupplySuitabilityReferences(local.Catering));
        var before = Assert.Single((await localPlanning.GetOverviewAsync(campId, ct))!.OperationalMeals);
        var manual = new SupplySolution(Guid.NewGuid(), revisionId, null, 2,
            new Dictionary<string, RecipePositionSelection>(),
            [new(Assert.Single(before.RequirementGroups!).Requirements.Key, 1)], SupplyDecisionSource.Manual);
        Assert.True((await localPlanning.SaveSupplyAsync(campId, unit.Id, meal.Id, new(before.SupplyVersion, [manual]), ct)).IsSuccess);
        await localPlanning.CalculateAsync(campId, unit.Id, meal.Id, ct);
        var recalculated = Assert.Single((await localPlanning.GetOverviewAsync(campId, ct))!.OperationalMeals);
        Assert.True((await localPlanning.VerifySupplyAsync(campId, unit.Id, meal.Id, actor,
            new(recalculated.SupplyVersion, MealVerificationKind.VerifiedWithDeviation, "Dummy unassigned portion"), ct)).IsSuccess);
        var expectedState = await local.Catering.CookingUnitMealStates.AsNoTracking().SingleAsync(ct);
        var localPackages = new CampPackageService(local.Platform, local.Camp, local.Catering,
            TimeProvider.System, new CampPackageTests.TestParticipantAccess());
        byte[] returned = await localPackages.CreateReturnPackageAsync(campId);
        // Force a failure after deleting the old participant, not merely during deserialization.
        var payload = CampPackageSerializer.Deserialize(returned);
        var invalid = payload with { Participants = payload.Participants! with
        {
            DietTypes = [new(Guid.NewGuid(), "Unavailable cloud diet")]
        } };
        invalid = invalid with { Participants = invalid.Participants! with
        {
            Items = [invalid.Participants.Items[0] with { DietTypeId = invalid.Participants.DietTypes[0].Id }]
        } };
        await Assert.ThrowsAsync<CampPackageValidationException>(() => packages.ImportReturnPackageAsync(CampPackageSerializer.Serialize(invalid)));
        Assert.Equal("Dummy person", (await camp.Participants.AsNoTracking().SingleAsync()).DisplayName);
        Assert.True((await camp.Camps.AsNoTracking().SingleAsync()).IsFrozen);
        await catering.Database.ExecuteSqlRawAsync("CREATE FUNCTION catering.reject_supply() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'supply rollback test'; END $$; CREATE TRIGGER reject_supply BEFORE INSERT ON catering.\"CookingUnitMealStates\" FOR EACH ROW EXECUTE FUNCTION catering.reject_supply();", ct);
        await Assert.ThrowsAsync<DbUpdateException>(() => packages.ImportReturnPackageAsync(returned, ct));
        var rolledBack = await catering.CookingUnitMealStates.AsNoTracking().SingleAsync(ct);
        Assert.Equal(originalState.CalculationSnapshotJson, rolledBack.CalculationSnapshotJson);
        Assert.Equal(originalState.ManualSupplySolutionsJson, rolledBack.ManualSupplySolutionsJson);
        Assert.Equal(originalState.VerificationSnapshotJson, rolledBack.VerificationSnapshotJson);
        Assert.True((await camp.Camps.AsNoTracking().SingleAsync(ct)).IsFrozen);
        await catering.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_supply ON catering.\"CookingUnitMealStates\"; DROP FUNCTION catering.reject_supply();", ct);
        await packages.ImportReturnPackageAsync(returned);
        var actualState = await catering.CookingUnitMealStates.AsNoTracking().SingleAsync(ct);
        Assert.Equal(expectedState.ManualSupplySolutionsJson, actualState.ManualSupplySolutionsJson);
        Assert.Equal(expectedState.CalculationSnapshotJson, actualState.CalculationSnapshotJson);
        Assert.Equal(expectedState.VerificationSnapshotJson, actualState.VerificationSnapshotJson);
        Assert.Equal(expectedState.VerificationFingerprint, actualState.VerificationFingerprint);
        Assert.Equal(expectedState.SupplyVersion, actualState.SupplyVersion);
        Assert.False(Assert.Single((await cloudPlanning.GetOverviewAsync(campId, ct))!.OperationalMeals).VerificationStale);
        var metadataPerson = await camp.Participants.SingleAsync(ct);
        metadataPerson.Rename("Only metadata changed");
        metadataPerson.SetRequirements(null, [catalog.Allergens[0].Id],
            [new(catalog.Substances.First(value => value.Code == "LACTOSE").Id, 0.123456m, "Other dummy source")]);
        await camp.SaveChangesAsync(ct);
        var afterMetadata = Assert.Single((await cloudPlanning.GetOverviewAsync(campId, ct))!.OperationalMeals);
        Assert.False(afterMetadata.VerificationStale);
        Assert.Equal(OperationalMealPlanStatus.Current, afterMetadata.Status);
        metadataPerson.Rename("Edited dummy person");
        await camp.SaveChangesAsync(ct);
        var actual = Assert.Single(await camp.GetParticipantsAsync(campId));
        Assert.Equal(participant.Id, actual.Id);
        Assert.Equal("Edited dummy person", actual.DisplayName);
        Assert.Equal(new DateOnly(2027, 7, 2), Assert.Single(actual.AbsentDays));
        Assert.Equal(0.123456m, Assert.Single(actual.Intolerances).ThresholdGramsPerPortion);
        Assert.False((await camp.Camps.AsNoTracking().SingleAsync()).IsFrozen);
    }

    [Fact]
    public async Task Return_import_uses_module_schemas_and_rolls_back_all_modules_atomically()
    {
        var connectionString = Environment.GetEnvironmentVariable("SCOUTCAMPPLANNER_POSTGRES_TEST");
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await ResetSchemasAsync(connection);

        await using var platform = new PlatformDbContext(
            new DbContextOptionsBuilder<PlatformDbContext>().UseNpgsql(connection).Options);
        await using var camp = new CampDbContext(
            new DbContextOptionsBuilder<CampDbContext>().UseNpgsql(connection).Options);
        await using var catering = new CateringDbContext(
            new DbContextOptionsBuilder<CateringDbContext>().UseNpgsql(connection).Options);
        await platform.Database.ExecuteSqlRawAsync(platform.Database.GenerateCreateScript());
        await camp.Database.ExecuteSqlRawAsync(camp.Database.GenerateCreateScript());
        await catering.Database.ExecuteSqlRawAsync(catering.Database.GenerateCreateScript());

        var tenantId = Guid.NewGuid();
        var campId = Guid.NewGuid();
        var structureNodeId = Guid.NewGuid();
        var mealId = Guid.NewGuid();
        platform.Tenants.Add(new Tenant(tenantId, "PostgreSQL Tenant"));
        camp.Camps.Add(new Camp.Domain.Camp(
            campId, tenantId, "PostgreSQL Camp", new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 14)));
        camp.CampStages.Add(new CampStage(Guid.NewGuid(), campId, "GuSp", 0));
        camp.StructureNodes.Add(new StructureNode(structureNodeId, campId, null, "Region"));
        catering.MealPlans.Add(new MealPlan(mealId, campId, "Original"));
        await platform.SaveChangesAsync();
        await camp.SaveChangesAsync();
        await catering.SaveChangesAsync();

        var service = new CampPackageService(platform, camp, catering, TimeProvider.System);
        var initialBytes = await service.StartOfflineTransferAsync(campId);
        var initial = CampPackageSerializer.Deserialize(initialBytes);
        var returnManifest = initial.Manifest with { Direction = CampPackageDirection.LocalToCloud };

        var duplicateId = Guid.NewGuid();
        MealPlanningPackageData mealPlanning = CampMealPlanningPackageStore.ReadPackageData(
            initial.CateringMealPlanningData, campId);
        var invalidReturn = initial with
        {
            Manifest = returnManifest,
            CateringMealPlanningData = JsonSerializer.SerializeToElement(mealPlanning with
            {
                MealPlans =
                [
                    new MealPlanPackageRecord(duplicateId, campId, "Duplicate A", 0, 0),
                    new MealPlanPackageRecord(duplicateId, campId, "Duplicate B", 1, 0)
                ]
            }, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        };
        await Assert.ThrowsAnyAsync<Exception>(() =>
            service.ImportReturnPackageAsync(CampPackageSerializer.Serialize(invalidReturn)));

        await using (var verification = new NpgsqlConnection(connectionString))
        {
            await verification.OpenAsync();
            await using var command = verification.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM catering.\"MealPlans\" WHERE \"CampId\" = @campId AND \"Name\" = 'Original'";
            command.Parameters.AddWithValue("campId", campId);
            Assert.Equal(1L, Convert.ToInt64(await command.ExecuteScalarAsync()));
        }

        camp.ChangeTracker.Clear();
        catering.ChangeTracker.Clear();
        var validReturn = initial with
        {
            Manifest = returnManifest,
            CateringMealPlanningData = CampMealPlanningPackageStore.CreatePackageData(mealPlanning with
            {
                MealPlans = [new MealPlanPackageRecord(mealId, campId, "Changed offline", 0, 0)]
            })
        };
        await service.ImportReturnPackageAsync(CampPackageSerializer.Serialize(validReturn));

        Assert.Equal(structureNodeId, (await camp.StructureNodes.AsNoTracking().SingleAsync()).Id);
        Assert.Equal("Changed offline", (await catering.MealPlans.AsNoTracking().SingleAsync()).Name);
        Assert.False((await camp.Camps.AsNoTracking().SingleAsync()).IsFrozen);
        Assert.Equal(["camp", "catering", "platform"], await ReadModuleSchemasAsync(connection));
    }

    private static async Task ResetSchemasAsync(NpgsqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "DROP SCHEMA IF EXISTS catering CASCADE; DROP SCHEMA IF EXISTS camp CASCADE; DROP SCHEMA IF EXISTS platform CASCADE;";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string[]> ReadModuleSchemasAsync(NpgsqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT schema_name FROM information_schema.schemata WHERE schema_name IN ('platform', 'camp', 'catering') ORDER BY schema_name";
        await using var reader = await command.ExecuteReaderAsync();
        var schemas = new List<string>();
        while (await reader.ReadAsync()) schemas.Add(reader.GetString(0));
        return schemas.ToArray();
    }
}
