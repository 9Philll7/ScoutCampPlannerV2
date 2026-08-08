using Microsoft.Data.Sqlite;

namespace ScoutCampPlanner.Migrations.Sqlite;

public sealed class SqlitePreMigrationBackup
{
    public async Task<string?> CreateAsync(
        SqliteConnection source,
        TimeProvider timeProvider,
        int retentionCount,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(timeProvider);
        if (retentionCount < 1)
            throw new ArgumentOutOfRangeException(nameof(retentionCount), "At least one SQLite backup must be retained.");
        if (source.DataSource is "" or ":memory:")
            return null;

        var databasePath = Path.GetFullPath(source.DataSource);
        if (!File.Exists(databasePath))
            return null;

        var backupDirectory = Path.Combine(Path.GetDirectoryName(databasePath)!, "backups");
        Directory.CreateDirectory(backupDirectory);

        var databaseName = Path.GetFileNameWithoutExtension(databasePath);
        var timestamp = timeProvider.GetUtcNow().ToString("yyyyMMdd'T'HHmmssfff'Z'");
        var backupPath = Path.Combine(backupDirectory, $"{databaseName}-{timestamp}-{Guid.NewGuid():N}-pre-migration.db");
        var temporaryPath = $"{backupPath}.tmp";

        try
        {
            await using (var destination = new SqliteConnection($"Data Source={temporaryPath};Mode=ReadWriteCreate;Pooling=False"))
            {
                await destination.OpenAsync(cancellationToken);
                source.BackupDatabase(destination);

                await using var integrityCommand = destination.CreateCommand();
                integrityCommand.CommandText = "PRAGMA integrity_check;";
                var integrityResult = Convert.ToString(await integrityCommand.ExecuteScalarAsync(cancellationToken));
                if (!string.Equals(integrityResult, "ok", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException($"SQLite backup integrity check failed: {integrityResult}");
            }

            File.Move(temporaryPath, backupPath);
            PruneBackups(backupDirectory, databaseName, retentionCount);
            return backupPath;
        }
        catch
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
            throw;
        }
    }

    private static void PruneBackups(string backupDirectory, string databaseName, int retentionCount)
    {
        var backups = new DirectoryInfo(backupDirectory)
            .EnumerateFiles($"{databaseName}-*-pre-migration.db")
            .OrderByDescending(file => file.Name, StringComparer.Ordinal)
            .Skip(retentionCount);

        foreach (var backup in backups)
            backup.Delete();
    }
}
