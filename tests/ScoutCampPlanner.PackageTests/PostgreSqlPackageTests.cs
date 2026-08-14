using Microsoft.EntityFrameworkCore;
using Npgsql;
using ScoutCampPlanner.Camp.Domain;
using ScoutCampPlanner.Camp.Infrastructure;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Package;
using ScoutCampPlanner.Platform.Domain;
using ScoutCampPlanner.Platform.Infrastructure;
using Xunit;

namespace ScoutCampPlanner.PackageTests;

public sealed class PostgreSqlPackageTests
{
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
        var unitId = Guid.NewGuid();
        var mealId = Guid.NewGuid();
        platform.Tenants.Add(new Tenant(tenantId, "PostgreSQL Tenant"));
        camp.Camps.Add(new Camp.Domain.Camp(
            campId, tenantId, "PostgreSQL Camp", new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 14)));
        camp.CookingUnits.Add(new CookingUnit(unitId, campId, "Unit"));
        catering.MealPlans.Add(new MealPlan(mealId, campId, "Original"));
        await platform.SaveChangesAsync();
        await camp.SaveChangesAsync();
        await catering.SaveChangesAsync();

        var service = new CampPackageService(platform, camp, catering, TimeProvider.System);
        var initialBytes = await service.StartOfflineTransferAsync(campId);
        var initial = CampPackageSerializer.Deserialize(initialBytes);
        var returnManifest = initial.Manifest with { Direction = CampPackageDirection.LocalToCloud };

        var duplicateId = Guid.NewGuid();
        var invalidReturn = initial with
        {
            Manifest = returnManifest,
            MealPlans =
            [
                new MealPlanData(duplicateId, campId, "Duplicate A"),
                new MealPlanData(duplicateId, campId, "Duplicate B")
            ]
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
            MealPlans = [new MealPlanData(mealId, campId, "Changed offline")]
        };
        await service.ImportReturnPackageAsync(CampPackageSerializer.Serialize(validReturn));

        Assert.Equal(unitId, (await camp.CookingUnits.AsNoTracking().SingleAsync()).Id);
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
