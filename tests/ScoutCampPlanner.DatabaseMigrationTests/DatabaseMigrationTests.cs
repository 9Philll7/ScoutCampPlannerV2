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
using ScoutCampPlanner.Platform.Application.Auditing;
using ScoutCampPlanner.Platform.Infrastructure.Auditing;
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

        Assert.Equal(6, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM __EFMigrationsHistory_platform"));
        Assert.Equal(8, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM __EFMigrationsHistory_camp"));
        Assert.Equal(5, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM __EFMigrationsHistory_catering"));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'IX_Camps_TenantId_Name'"));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'IX_Camps_TenantId_NormalizedName_StartDate_EndDate'"));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'StructureNodes'"));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'TenantStageTemplateEntries'"));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'CampStages'"));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'ParticipantEstimates'"));
        Assert.Equal(0, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'CookingUnits'"));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'IX_MealPlans_CampId'"));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'TenantStageFoodFactors'"));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'CampStageFoodFactors'"));
        Assert.Equal(2, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('CampMealTypes', 'CampMeals')"));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'IX_UserAccounts_NormalizedEmail'"));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'IX_TenantMemberships_UserId_TenantId'"));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'IX_TenantRoleAssignments_MembershipId'"));
        Assert.Equal(3, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('AuditEvents', 'AuditJournalHeads', 'AuditSegments')"));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = 'AuthenticationSessions'"));
        Assert.Equal(2, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name IN ('CampMemberships', 'CampRoleAssignments')"));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'IX_AuditEvents_InstanceId_EventId'"));

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

        Assert.Equal(6, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM platform.\"__EFMigrationsHistory\""));
        Assert.Equal(8, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM camp.\"__EFMigrationsHistory\""));
        Assert.Equal(5, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM catering.\"__EFMigrationsHistory\""));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM pg_indexes WHERE schemaname = 'camp' AND indexname = 'IX_Camps_TenantId_Name'"));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM pg_indexes WHERE schemaname = 'camp' AND indexname = 'IX_Camps_TenantId_NormalizedName_StartDate_EndDate'"));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'camp' AND table_name = 'StructureNodes'"));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'camp' AND table_name = 'TenantStageTemplateEntries'"));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'camp' AND table_name = 'CampStages'"));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'camp' AND table_name = 'ParticipantEstimates'"));
        Assert.Equal(0, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'camp' AND table_name = 'CookingUnits'"));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM pg_indexes WHERE schemaname = 'catering' AND indexname = 'IX_MealPlans_CampId'"));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'catering' AND table_name = 'TenantStageFoodFactors'"));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'catering' AND table_name = 'CampStageFoodFactors'"));
        Assert.Equal(2, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'catering' AND table_name IN ('CampMealTypes', 'CampMeals')"));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM pg_indexes WHERE schemaname = 'platform' AND indexname = 'IX_UserAccounts_NormalizedEmail'"));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM pg_indexes WHERE schemaname = 'platform' AND indexname = 'IX_TenantMemberships_UserId_TenantId'"));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM pg_indexes WHERE schemaname = 'platform' AND indexname = 'IX_TenantRoleAssignments_MembershipId'"));
        Assert.Equal(3, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'platform' AND table_name IN ('AuditEvents', 'AuditJournalHeads', 'AuditSegments')"));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'platform' AND table_name = 'AuthenticationSessions'"));
        Assert.Equal(2, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'platform' AND table_name IN ('CampMemberships', 'CampRoleAssignments')"));
        Assert.Equal(1, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM pg_indexes WHERE schemaname = 'platform' AND indexname = 'IX_AuditEvents_InstanceId_EventId'"));

        await AssertConcurrentProductivePostgreSqlAuditAppendsAsync(connectionString, databases.Platform);
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
        databases.Catering.MealPlans.Add(new MealPlan(identities.Item4, identities.Item2, "Migration Meal"));
        await databases.Platform.SaveChangesAsync();
        if (databases.Camp.Database.IsNpgsql())
        {
            await databases.Camp.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO camp."Camps" ("Id", "TenantId", "Name", "IsFrozen", "ActiveTransferId", "BaselineVersion")
                VALUES ({identities.Item2}, {identities.Item1}, {"Migration Camp"}, {false}, {null}, {0L})
                """);
            await databases.Camp.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO camp."CookingUnits" ("Id", "CampId", "Name")
                VALUES ({identities.Item3}, {identities.Item2}, {"Legacy Unit"})
                """);
        }
        else
        {
            await databases.Camp.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "Camps" ("Id", "TenantId", "Name", "IsFrozen", "ActiveTransferId", "BaselineVersion")
                VALUES ({identities.Item2}, {identities.Item1}, {"Migration Camp"}, {false}, {null}, {0L})
                """);
            await databases.Camp.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO "CookingUnits" ("Id", "CampId", "Name")
                VALUES ({identities.Item3}, {identities.Item2}, {"Legacy Unit"})
                """);
        }
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
        Assert.False(await TableExistsAsync(databases.Camp.Database.GetDbConnection(), "CookingUnits"));
        Assert.True(await databases.Catering.MealPlans.AnyAsync(x => x.Id == identities.MealId));
    }

    private static async Task ResetPostgreSqlSchemasAsync(NpgsqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "DROP SCHEMA IF EXISTS catering CASCADE; DROP SCHEMA IF EXISTS camp CASCADE; DROP SCHEMA IF EXISTS platform CASCADE; CREATE SCHEMA platform; CREATE SCHEMA camp; CREATE SCHEMA catering;";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task AssertConcurrentProductivePostgreSqlAuditAppendsAsync(
        string connectionString,
        PlatformDbContext initializedDatabase)
    {
        Guid instanceId = Guid.NewGuid();
        var keys = new FixedAuditKeyProvider();
        await new AuditJournalInitializer(initializedDatabase, keys).InitializeAsync(
            instanceId, Guid.NewGuid(), DateTimeOffset.UnixEpoch);

        await Task.WhenAll(Enumerable.Range(1, 12).Select(async number =>
        {
            await using var database = new PlatformDbContext(
                new DbContextOptionsBuilder<PlatformDbContext>().UseNpgsql(connectionString).Options);
            var draft = new AuditEventDraft(
                Guid.NewGuid(), DateTimeOffset.UnixEpoch.AddSeconds(number), "migration.audit", "success",
                null, null, null, null, null, "test", instanceId, Guid.NewGuid(), null, null,
                new Dictionary<string, string>());
            await new AuditJournalAppender(database, keys).AppendAsync(draft);
        }));

        initializedDatabase.ChangeTracker.Clear();
        long[] sequences = await initializedDatabase.AuditEvents.Where(value => value.InstanceId == instanceId)
            .OrderBy(value => value.Sequence).Select(value => value.Sequence).ToArrayAsync();
        Assert.Equal(Enumerable.Range(1, 12).Select(value => (long)value), sequences);
        Assert.Equal(12, (await initializedDatabase.AuditJournalHeads.SingleAsync(
            value => value.InstanceId == instanceId)).Sequence);

        Guid committedTenantId = Guid.NewGuid();
        await using (var database = new PlatformDbContext(
            new DbContextOptionsBuilder<PlatformDbContext>().UseNpgsql(connectionString).Options))
        {
            var draft = new AuditEventDraft(
                Guid.NewGuid(), DateTimeOffset.UnixEpoch.AddMinutes(1), "tenant.created", "success",
                null, null, null, "tenant", committedTenantId, "test", instanceId, Guid.NewGuid(), null, null,
                new Dictionary<string, string>());
            await new AuditedOperationExecutor(database, keys).ExecuteAsync(draft, async cancellationToken =>
            {
                database.Tenants.Add(new Tenant(committedTenantId, "Committed Tenant"));
                await database.SaveChangesAsync(cancellationToken);
            });
        }

        Guid rolledBackTenantId = Guid.NewGuid();
        await using (var database = new PlatformDbContext(
            new DbContextOptionsBuilder<PlatformDbContext>().UseNpgsql(connectionString).Options))
        {
            var draft = new AuditEventDraft(
                Guid.NewGuid(), DateTimeOffset.UnixEpoch.AddMinutes(2), "tenant.created", "failure",
                null, null, null, "tenant", rolledBackTenantId, "test", instanceId, Guid.NewGuid(), null, null,
                new Dictionary<string, string>());
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                new AuditedOperationExecutor(database, keys).ExecuteAsync(draft, async cancellationToken =>
                {
                    database.Tenants.Add(new Tenant(rolledBackTenantId, "Rolled Back Tenant"));
                    await database.SaveChangesAsync(cancellationToken);
                    throw new InvalidOperationException("Simulated business failure.");
                }));
        }

        initializedDatabase.ChangeTracker.Clear();
        Assert.True(await initializedDatabase.Tenants.AnyAsync(value => value.Id == committedTenantId));
        Assert.False(await initializedDatabase.Tenants.AnyAsync(value => value.Id == rolledBackTenantId));
        Assert.Equal(13, await initializedDatabase.AuditEvents.CountAsync(value => value.InstanceId == instanceId));
        Assert.Equal(13, (await initializedDatabase.AuditJournalHeads.SingleAsync(
            value => value.InstanceId == instanceId)).Sequence);
    }

    private static async Task<T> ScalarAsync<T>(DbConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (T)Convert.ChangeType(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Query returned no value."), typeof(T));
    }

    private static async Task<bool> TableExistsAsync(DbConnection connection, string tableName) =>
        connection is SqliteConnection
            ? await ScalarAsync<long>(connection,
                $"SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '{tableName}'") > 0
            : await ScalarAsync<long>(connection,
                $"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'camp' AND table_name = '{tableName}'") > 0;

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

    private sealed class FixedAuditKeyProvider : IAuditSigningKeyProvider
    {
        private static readonly byte[] KeyMaterial = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();

        public Task<AuditSigningKey> GetActiveAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AuditSigningKey("migration-key", KeyMaterial.ToArray()));
    }
}
