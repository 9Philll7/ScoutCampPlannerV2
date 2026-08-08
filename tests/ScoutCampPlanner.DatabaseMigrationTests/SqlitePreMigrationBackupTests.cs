using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Camp.Infrastructure;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Migrations.Sqlite;
using ScoutCampPlanner.Platform.Domain;
using ScoutCampPlanner.Platform.Infrastructure;
using Xunit;

namespace ScoutCampPlanner.DatabaseMigrationTests;

public sealed class SqlitePreMigrationBackupTests
{
    [Fact]
    public async Task Bootstrap_does_not_migrate_when_backup_creation_fails()
    {
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
        var testDirectory = Path.Combine(Path.GetTempPath(), $"scoutcampplanner-backup-failure-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);

        try
        {
            var databasePath = Path.Combine(testDirectory, "scoutcampplanner.db");
            await using (var baselineConnection = new SqliteConnection($"Data Source={databasePath};Pooling=False"))
            {
                await baselineConnection.OpenAsync();
                await using var baseline = CreateDatabases(baselineConnection);
                await baseline.Platform.Database.MigrateAsync("20260808204812_InitialPlatform");
                await baseline.Camp.Database.MigrateAsync("20260808204825_InitialCamp");
                await baseline.Catering.Database.MigrateAsync("20260808204828_InitialCatering");
            }

            await File.WriteAllTextAsync(Path.Combine(testDirectory, "backups"), "blocks backup directory creation");
            await using var upgradeConnection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
            await using var upgrade = CreateDatabases(upgradeConnection);

            await Assert.ThrowsAnyAsync<IOException>(() => DatabaseBootstrapper.InitializeAsync(
                upgradeConnection,
                upgrade.Platform,
                upgrade.Camp,
                upgrade.Catering,
                "Sqlite",
                TimeProvider.System,
                sqliteBackupRetention: 3));

            Assert.Contains("20260808210000_AddCampIndexes", await upgrade.Camp.Database.GetPendingMigrationsAsync());
            Assert.Equal(0, await ScalarLongAsync(upgradeConnection, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'IX_Camps_TenantId_Name'"));
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task Bootstrap_creates_one_backup_only_when_an_upgrade_is_pending()
    {
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
        var testDirectory = Path.Combine(Path.GetTempPath(), $"scoutcampplanner-bootstrap-backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);

        try
        {
            var databasePath = Path.Combine(testDirectory, "scoutcampplanner.db");
            await using (var baselineConnection = new SqliteConnection($"Data Source={databasePath};Pooling=False"))
            {
                await baselineConnection.OpenAsync();
                await using var baseline = CreateDatabases(baselineConnection);
                await baseline.Platform.Database.MigrateAsync("20260808204812_InitialPlatform");
                await baseline.Camp.Database.MigrateAsync("20260808204825_InitialCamp");
                await baseline.Catering.Database.MigrateAsync("20260808204828_InitialCatering");
                baseline.Platform.Tenants.Add(new Tenant(Guid.NewGuid(), "Backup Tenant"));
                await baseline.Platform.SaveChangesAsync();
            }

            await using var upgradeConnection = new SqliteConnection($"Data Source={databasePath};Pooling=False");
            await using var upgrade = CreateDatabases(upgradeConnection);
            var time = new AdjustableTimeProvider(new DateTimeOffset(2026, 8, 8, 21, 0, 0, TimeSpan.Zero));

            await DatabaseBootstrapper.InitializeAsync(
                upgradeConnection,
                upgrade.Platform,
                upgrade.Camp,
                upgrade.Catering,
                "Sqlite",
                time,
                sqliteBackupRetention: 3);

            var backupDirectory = Path.Combine(testDirectory, "backups");
            var backup = Assert.Single(Directory.GetFiles(backupDirectory, "*-pre-migration.db"));
            Assert.Empty(await upgrade.Camp.Database.GetPendingMigrationsAsync());
            Assert.Empty(await upgrade.Catering.Database.GetPendingMigrationsAsync());

            await DatabaseBootstrapper.InitializeAsync(
                upgradeConnection,
                upgrade.Platform,
                upgrade.Camp,
                upgrade.Catering,
                "Sqlite",
                time,
                sqliteBackupRetention: 3);

            Assert.Single(Directory.GetFiles(backupDirectory, "*-pre-migration.db"));
            await using var restored = new SqliteConnection($"Data Source={backup};Mode=ReadOnly;Pooling=False");
            await restored.OpenAsync();
            Assert.Equal("Backup Tenant", await ScalarAsync(restored, "SELECT Name FROM Tenants"));
            Assert.Equal(0, await ScalarLongAsync(restored, "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'IX_Camps_TenantId_Name'"));
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task Backup_is_integral_preserves_data_and_prunes_old_generations()
    {
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
        var testDirectory = Path.Combine(Path.GetTempPath(), $"scoutcampplanner-backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(testDirectory);

        try
        {
            var databasePath = Path.Combine(testDirectory, "scoutcampplanner.db");
            await using var source = new SqliteConnection($"Data Source={databasePath};Pooling=False");
            await source.OpenAsync();
            await ExecuteAsync(source, "CREATE TABLE TestData (Value TEXT NOT NULL); INSERT INTO TestData VALUES ('before-upgrade');");

            var time = new AdjustableTimeProvider(new DateTimeOffset(2026, 8, 8, 21, 0, 0, TimeSpan.Zero));
            var backups = new SqlitePreMigrationBackup();
            var first = await backups.CreateAsync(source, time, retentionCount: 2);
            time.Advance(TimeSpan.FromMinutes(1));
            var second = await backups.CreateAsync(source, time, retentionCount: 2);
            time.Advance(TimeSpan.FromMinutes(1));
            var third = await backups.CreateAsync(source, time, retentionCount: 2);

            Assert.NotNull(first);
            Assert.NotNull(second);
            Assert.NotNull(third);
            Assert.False(File.Exists(first));
            Assert.True(File.Exists(second));
            Assert.True(File.Exists(third));
            Assert.Equal(2, Directory.GetFiles(Path.Combine(testDirectory, "backups"), "*-pre-migration.db").Length);

            await using var restored = new SqliteConnection($"Data Source={third};Mode=ReadOnly;Pooling=False");
            await restored.OpenAsync();
            Assert.Equal("before-upgrade", await ScalarAsync(restored, "SELECT Value FROM TestData"));
            Assert.Equal("ok", await ScalarAsync(restored, "PRAGMA integrity_check"));
        }
        finally
        {
            Directory.Delete(testDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task In_memory_database_does_not_create_a_backup()
    {
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
        await using var source = new SqliteConnection("Data Source=:memory:");
        await source.OpenAsync();

        var backup = await new SqlitePreMigrationBackup().CreateAsync(source, TimeProvider.System, retentionCount: 3);

        Assert.Null(backup);
    }

    private static async Task ExecuteAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<string?> ScalarAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync());
    }

    private static async Task<long> ScalarLongAsync(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static ModuleDatabases CreateDatabases(SqliteConnection connection) => new(
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

    private sealed class AdjustableTimeProvider(DateTimeOffset current) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => current;

        public void Advance(TimeSpan duration) => current = current.Add(duration);
    }
}
