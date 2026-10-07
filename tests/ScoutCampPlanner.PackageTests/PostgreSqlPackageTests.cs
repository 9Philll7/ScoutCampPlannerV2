using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Text.Json;
using ScoutCampPlanner.Camp.Domain;
using ScoutCampPlanner.Camp.Infrastructure;
using ScoutCampPlanner.Catering.Domain;
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
        await platform.SaveChangesAsync();
        await camp.SaveChangesAsync();
        var packages = new CampPackageService(platform, camp, catering, TimeProvider.System, new CampPackageTests.TestParticipantAccess());
        byte[] initial = await packages.StartOfflineTransferAsync(campId);
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
        await using var local = await CampPackageTests.DatabaseHarness.CreateAsync();
        await local.Packages.ImportInitialPackageAsync(initial);
        var imported = await local.Camp.Participants.SingleAsync();
        imported.Rename("Edited dummy person");
        imported.SetDayAbsent(new(2027, 7, 2), true);
        await local.SaveAsync();
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
        await packages.ImportReturnPackageAsync(returned);
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
