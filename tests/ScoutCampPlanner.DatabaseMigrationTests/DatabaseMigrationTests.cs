using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ScoutCampPlanner.Camp.Domain;
using ScoutCampPlanner.Camp.Infrastructure;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Migrations.PostgreSql;
using ScoutCampPlanner.Migrations.Sqlite;
using ScoutCampPlanner.Platform.Domain;
using ScoutCampPlanner.Platform.Infrastructure;
using Xunit;

namespace ScoutCampPlanner.DatabaseMigrationTests;

public sealed class DatabaseMigrationTests
{
    private const string SqlitePlatformV1 = "20260808204812_InitialPlatform";
    private const string SqliteCampV1 = "20260808204825_InitialCamp";
    private const string SqliteCateringV1 = "20260808204828_InitialCatering";
    private const string PostgreSqlPlatformV1 = "20260808204848_InitialPlatform";
    private const string PostgreSqlCampV1 = "20260808204851_InitialCamp";
    private const string PostgreSqlCateringV1 = "20260808204854_InitialCatering";

    [Fact]
    public async Task Sqlite_upgrade_preserves_existing_data_and_applies_each_module_history()
    {
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var databases = CreateSqliteDatabases(connection);

        await MigrateToV1Async(databases, SqlitePlatformV1, SqliteCampV1, SqliteCateringV1);
        var identities = await AddBaselineDataAsync(databases);

        await MigrateToCurrentAsync(databases);
        await AssertBaselineDataAsync(databases, identities);

        Assert.Equal(3, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM __EFMigrationsHistory_platform"));
        Assert.Equal(2, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM __EFMigrationsHistory_camp"));
        Assert.Equal(2, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM __EFMigrationsHistory_catering"));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'IX_Camps_TenantId_Name'"));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'IX_MealPlans_CampId'"));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'IX_UserAccounts_NormalizedEmail'"));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'IX_TenantMemberships_UserId_TenantId'"));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'IX_TenantRoleAssignments_MembershipId'"));

        await MigrateToCurrentAsync(databases);
        Assert.Empty(await databases.Platform.Database.GetPendingMigrationsAsync());
        Assert.Empty(await databases.Camp.Database.GetPendingMigrationsAsync());
        Assert.Empty(await databases.Catering.Database.GetPendingMigrationsAsync());
    }

    [Fact]
    public async Task PostgreSql_upgrade_preserves_existing_data_and_applies_each_module_history()
    {
        var connectionString = Environment.GetEnvironmentVariable("SCOUTCAMPPLANNER_POSTGRES_TEST");
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await ResetPostgreSqlSchemasAsync(connection);
        await using var databases = CreatePostgreSqlDatabases(connection);

        await MigrateToV1Async(databases, PostgreSqlPlatformV1, PostgreSqlCampV1, PostgreSqlCateringV1);
        var identities = await AddBaselineDataAsync(databases);

        await MigrateToCurrentAsync(databases);
        await AssertBaselineDataAsync(databases, identities);

        Assert.Equal(3, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM platform.\"__EFMigrationsHistory\""));
        Assert.Equal(2, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM camp.\"__EFMigrationsHistory\""));
        Assert.Equal(2, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM catering.\"__EFMigrationsHistory\""));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM pg_indexes WHERE schemaname = 'camp' AND indexname = 'IX_Camps_TenantId_Name'"));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM pg_indexes WHERE schemaname = 'catering' AND indexname = 'IX_MealPlans_CampId'"));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM pg_indexes WHERE schemaname = 'platform' AND indexname = 'IX_UserAccounts_NormalizedEmail'"));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM pg_indexes WHERE schemaname = 'platform' AND indexname = 'IX_TenantMemberships_UserId_TenantId'"));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM pg_indexes WHERE schemaname = 'platform' AND indexname = 'IX_TenantRoleAssignments_MembershipId'"));
    }

    private static ModuleDatabases CreateSqliteDatabases(SqliteConnection connection) => new(
        new PlatformDbContext(new DbContextOptionsBuilder<PlatformDbContext>().UseSqlite(connection, options =>
        {
            options.MigrationsAssembly(typeof(SqliteMigrationsAssembly).Assembly.FullName);
            options.MigrationsHistoryTable("__EFMigrationsHistory_platform");
        }).Options),
        new CampDbContext(new DbContextOptionsBuilder<CampDbContext>().UseSqlite(connection, options =>
        {
            options.MigrationsAssembly(typeof(SqliteMigrationsAssembly).Assembly.FullName);
            options.MigrationsHistoryTable("__EFMigrationsHistory_camp");
        }).Options),
        new CateringDbContext(new DbContextOptionsBuilder<CateringDbContext>().UseSqlite(connection, options =>
        {
            options.MigrationsAssembly(typeof(SqliteMigrationsAssembly).Assembly.FullName);
            options.MigrationsHistoryTable("__EFMigrationsHistory_catering");
        }).Options));

    private static ModuleDatabases CreatePostgreSqlDatabases(NpgsqlConnection connection) => new(
        new PlatformDbContext(new DbContextOptionsBuilder<PlatformDbContext>().UseNpgsql(connection, options =>
        {
            options.MigrationsAssembly(typeof(PostgreSqlMigrationsAssembly).Assembly.FullName);
            options.MigrationsHistoryTable("__EFMigrationsHistory", "platform");
        }).Options),
        new CampDbContext(new DbContextOptionsBuilder<CampDbContext>().UseNpgsql(connection, options =>
        {
            options.MigrationsAssembly(typeof(PostgreSqlMigrationsAssembly).Assembly.FullName);
            options.MigrationsHistoryTable("__EFMigrationsHistory", "camp");
        }).Options),
        new CateringDbContext(new DbContextOptionsBuilder<CateringDbContext>().UseNpgsql(connection, options =>
        {
            options.MigrationsAssembly(typeof(PostgreSqlMigrationsAssembly).Assembly.FullName);
            options.MigrationsHistoryTable("__EFMigrationsHistory", "catering");
        }).Options));

    private static async Task MigrateToV1Async(ModuleDatabases databases, string platform, string camp, string catering)
    {
        await databases.Platform.Database.MigrateAsync(platform);
        await databases.Camp.Database.MigrateAsync(camp);
        await databases.Catering.Database.MigrateAsync(catering);
    }

    private static async Task MigrateToCurrentAsync(ModuleDatabases databases)
    {
        await databases.Platform.Database.MigrateAsync();
        await databases.Camp.Database.MigrateAsync();
        await databases.Catering.Database.MigrateAsync();
    }

    private static async Task<(Guid TenantId, Guid CampId, Guid UnitId, Guid MealId)> AddBaselineDataAsync(ModuleDatabases databases)
    {
        var identities = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        databases.Platform.Tenants.Add(new Tenant(identities.Item1, "Migration Tenant"));
        databases.Camp.Camps.Add(new Camp.Domain.Camp(identities.Item2, identities.Item1, "Migration Camp"));
        databases.Camp.CookingUnits.Add(new CookingUnit(identities.Item3, identities.Item2, "Migration Unit"));
        databases.Catering.MealPlans.Add(new MealPlan(identities.Item4, identities.Item2, "Migration Meal"));
        await databases.Platform.SaveChangesAsync();
        await databases.Camp.SaveChangesAsync();
        await databases.Catering.SaveChangesAsync();
        return identities;
    }

    private static async Task AssertBaselineDataAsync(
        ModuleDatabases databases,
        (Guid TenantId, Guid CampId, Guid UnitId, Guid MealId) identities)
    {
        Assert.True(await databases.Platform.Tenants.AnyAsync(x => x.Id == identities.TenantId));
        Assert.True(await databases.Camp.Camps.AnyAsync(x => x.Id == identities.CampId));
        Assert.True(await databases.Camp.CookingUnits.AnyAsync(x => x.Id == identities.UnitId));
        Assert.True(await databases.Catering.MealPlans.AnyAsync(x => x.Id == identities.MealId));
    }

    private static async Task ResetPostgreSqlSchemasAsync(NpgsqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "DROP SCHEMA IF EXISTS catering CASCADE; DROP SCHEMA IF EXISTS camp CASCADE; DROP SCHEMA IF EXISTS platform CASCADE; CREATE SCHEMA platform; CREATE SCHEMA camp; CREATE SCHEMA catering;";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<T> ScalarAsync<T>(DbConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (T)Convert.ChangeType(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Query returned no value."), typeof(T));
    }

    private sealed class ModuleDatabases(
        PlatformDbContext platform,
        CampDbContext camp,
        CateringDbContext catering) : IAsyncDisposable
    {
        public PlatformDbContext Platform { get; } = platform;
        public CampDbContext Camp { get; } = camp;
        public CateringDbContext Catering { get; } = catering;

        public async ValueTask DisposeAsync()
        {
            await Catering.DisposeAsync();
            await Camp.DisposeAsync();
            await Platform.DisposeAsync();
        }
    }
}
