using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Camp.Infrastructure;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Platform.Infrastructure;

internal static class DatabaseBootstrapper
{
    public static async Task InitializeAsync(
        DbConnection connection,
        PlatformDbContext platform,
        CampDbContext camp,
        CateringDbContext catering,
        string provider,
        CancellationToken cancellationToken = default)
    {
        await connection.OpenAsync(cancellationToken);
        var isPostgreSql = provider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase);
        if (isPostgreSql)
            await SetPostgreSqlMigrationLockAsync(connection, acquire: true, cancellationToken);

        try
        {
            if (isPostgreSql)
                await CreatePostgreSqlSchemasAsync(connection, cancellationToken);

            await RejectPreMigrationBaselineAsync(connection, isPostgreSql, cancellationToken);

            await platform.Database.MigrateAsync(cancellationToken);
            await camp.Database.MigrateAsync(cancellationToken);
            await catering.Database.MigrateAsync(cancellationToken);
        }
        finally
        {
            if (isPostgreSql)
                await SetPostgreSqlMigrationLockAsync(connection, acquire: false, CancellationToken.None);
        }
    }

    private static async Task RejectPreMigrationBaselineAsync(
        DbConnection connection,
        bool isPostgreSql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = isPostgreSql
            ? "SELECT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'platform' AND table_name = 'Tenants') AND NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'platform' AND table_name = '__EFMigrationsHistory');"
            : "SELECT EXISTS (SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = 'Tenants') AND NOT EXISTS (SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = '__EFMigrationsHistory_platform');";
        var isPreMigrationDatabase = Convert.ToBoolean(await command.ExecuteScalarAsync(cancellationToken));
        if (isPreMigrationDatabase)
        {
            throw new InvalidOperationException(
                "The database predates the production migration baseline and cannot be upgraded automatically. " +
                "Back up any required spike data and create a new development database.");
        }
    }

    private static async Task CreatePostgreSqlSchemasAsync(
        DbConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "CREATE SCHEMA IF NOT EXISTS platform; CREATE SCHEMA IF NOT EXISTS camp; CREATE SCHEMA IF NOT EXISTS catering;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task SetPostgreSqlMigrationLockAsync(
        DbConnection connection,
        bool acquire,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = acquire
            ? "SELECT pg_advisory_lock(72118455371801);"
            : "SELECT pg_advisory_unlock(72118455371801);";
        await command.ExecuteScalarAsync(cancellationToken);
    }
}
