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
        await CreateModuleAsync(connection, platform, provider, "platform", "Tenants", cancellationToken);
        await CreateModuleAsync(connection, camp, provider, "camp", "Camps", cancellationToken);
        await CreateModuleAsync(connection, catering, provider, "catering", "MealPlans", cancellationToken);
    }

    private static async Task CreateModuleAsync(
        DbConnection connection,
        DbContext context,
        string provider,
        string schema,
        string sentinelTable,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = provider.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase)
            ? $"SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = '{schema}' AND table_name = '{sentinelTable}'"
            : $"SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = '{sentinelTable}'";
        var exists = Convert.ToInt64(await command.ExecuteScalarAsync(cancellationToken)) > 0;
        if (!exists)
            await context.Database.ExecuteSqlRawAsync(context.Database.GenerateCreateScript(), cancellationToken);
    }
}
