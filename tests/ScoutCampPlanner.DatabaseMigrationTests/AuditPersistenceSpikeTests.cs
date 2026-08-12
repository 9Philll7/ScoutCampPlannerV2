using System.Data.Common;
using Microsoft.Data.Sqlite;
using Npgsql;
using Xunit;

namespace ScoutCampPlanner.DatabaseMigrationTests;

public sealed class AuditPersistenceSpikeTests
{
    private const int ConcurrentAppendCount = 12;

    [Fact]
    public async Task Sqlite_rollsBackBusinessEventAndDatabaseHeadTogether()
    {
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await CreateSchemaAsync(connection, sqlite: true);

        await AssertAtomicRollbackAsync(connection);
    }

    [Fact]
    public async Task PostgreSql_rollsBackBusinessEventAndDatabaseHeadTogether()
    {
        string? connectionString = Environment.GetEnvironmentVariable("SCOUTCAMPPLANNER_POSTGRES_TEST");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await ExecuteAsync(connection, "DROP TABLE IF EXISTS audit_spike_events; DROP TABLE IF EXISTS audit_spike_head; DROP TABLE IF EXISTS audit_spike_business;");
        await CreateSchemaAsync(connection, sqlite: false);

        await AssertAtomicRollbackAsync(connection);
    }

    [Fact]
    public async Task Sqlite_applicationGate_serializesConcurrentAppends()
    {
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
        string databasePath = Path.Combine(Path.GetTempPath(), $"scoutcampplanner-audit-spike-{Guid.NewGuid():N}.db");
        string connectionString = $"Data Source={databasePath};Default Timeout=10";
        using var appendGate = new SemaphoreSlim(1, 1);
        try
        {
            await using (var setup = new SqliteConnection(connectionString))
            {
                await setup.OpenAsync();
                await CreateConcurrentSchemaAsync(setup);
            }

            await Task.WhenAll(Enumerable.Range(0, ConcurrentAppendCount).Select(_ =>
                AppendSqliteAsync(connectionString, appendGate)));

            await using var verification = new SqliteConnection(connectionString);
            await verification.OpenAsync();
            await AssertContiguousSequencesAsync(verification);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(databasePath)) File.Delete(databasePath);
        }
    }

    [Fact]
    public async Task PostgreSql_headRowLock_serializesConcurrentAppends()
    {
        string? connectionString = Environment.GetEnvironmentVariable("SCOUTCAMPPLANNER_POSTGRES_TEST");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using (var setup = new NpgsqlConnection(connectionString))
        {
            await setup.OpenAsync();
            await ExecuteAsync(setup, "DROP TABLE IF EXISTS audit_spike_concurrent_events; DROP TABLE IF EXISTS audit_spike_concurrent_head;");
            await CreateConcurrentSchemaAsync(setup);
        }

        await Task.WhenAll(Enumerable.Range(0, ConcurrentAppendCount).Select(_ =>
            AppendPostgreSqlAsync(connectionString)));

        await using var verification = new NpgsqlConnection(connectionString);
        await verification.OpenAsync();
        await AssertContiguousSequencesAsync(verification);
    }

    private static async Task AssertAtomicRollbackAsync(DbConnection connection)
    {
        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await ExecuteAsync(connection, "UPDATE audit_spike_business SET value = 'changed' WHERE id = 1", transaction);
            await ExecuteAsync(connection, "INSERT INTO audit_spike_events(sequence, hmac) VALUES (1, 'head-1')", transaction);
            await ExecuteAsync(connection, "UPDATE audit_spike_head SET sequence = 1, hmac = 'head-1' WHERE id = 1", transaction);
            await transaction.RollbackAsync();
        }

        Assert.Equal("initial", await ScalarAsync<string>(connection, "SELECT value FROM audit_spike_business WHERE id = 1"));
        Assert.Equal(0L, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM audit_spike_events"));
        Assert.Equal(0L, await ScalarAsync<long>(connection, "SELECT sequence FROM audit_spike_head WHERE id = 1"));

        await using (var transaction = await connection.BeginTransactionAsync())
        {
            await ExecuteAsync(connection, "UPDATE audit_spike_business SET value = 'changed' WHERE id = 1", transaction);
            await ExecuteAsync(connection, "INSERT INTO audit_spike_events(sequence, hmac) VALUES (1, 'head-1')", transaction);
            await ExecuteAsync(connection, "UPDATE audit_spike_head SET sequence = 1, hmac = 'head-1' WHERE id = 1", transaction);
            await transaction.CommitAsync();
        }

        Assert.Equal("changed", await ScalarAsync<string>(connection, "SELECT value FROM audit_spike_business WHERE id = 1"));
        Assert.Equal(1L, await ScalarAsync<long>(connection, "SELECT COUNT(*) FROM audit_spike_events"));
        Assert.Equal("head-1", await ScalarAsync<string>(connection, "SELECT hmac FROM audit_spike_head WHERE id = 1"));
    }

    private static async Task CreateSchemaAsync(DbConnection connection, bool sqlite)
    {
        string identity = sqlite ? "INTEGER PRIMARY KEY" : "integer PRIMARY KEY";
        await ExecuteAsync(connection, $"CREATE TABLE audit_spike_business(id {identity}, value text NOT NULL);");
        await ExecuteAsync(connection, "CREATE TABLE audit_spike_events(sequence bigint PRIMARY KEY, hmac text NOT NULL);");
        await ExecuteAsync(connection, $"CREATE TABLE audit_spike_head(id {identity}, sequence bigint NOT NULL, hmac text NOT NULL);");
        await ExecuteAsync(connection, "INSERT INTO audit_spike_business(id, value) VALUES (1, 'initial');");
        await ExecuteAsync(connection, "INSERT INTO audit_spike_head(id, sequence, hmac) VALUES (1, 0, 'genesis');");
    }

    private static async Task CreateConcurrentSchemaAsync(DbConnection connection)
    {
        await ExecuteAsync(connection, "CREATE TABLE audit_spike_concurrent_head(id integer PRIMARY KEY, sequence bigint NOT NULL);");
        await ExecuteAsync(connection, "CREATE TABLE audit_spike_concurrent_events(sequence bigint PRIMARY KEY);");
        await ExecuteAsync(connection, "INSERT INTO audit_spike_concurrent_head(id, sequence) VALUES (1, 0);");
    }

    private static async Task AppendSqliteAsync(string connectionString, SemaphoreSlim appendGate)
    {
        await appendGate.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            long current = await ScalarAsync<long>(connection,
                "SELECT sequence FROM audit_spike_concurrent_head WHERE id = 1", transaction);
            long next = current + 1;
            await ExecuteAsync(connection,
                $"INSERT INTO audit_spike_concurrent_events(sequence) VALUES ({next})", transaction);
            await ExecuteAsync(connection,
                $"UPDATE audit_spike_concurrent_head SET sequence = {next} WHERE id = 1", transaction);
            await transaction.CommitAsync();
        }
        finally
        {
            appendGate.Release();
        }
    }

    private static async Task AppendPostgreSqlAsync(string connectionString)
    {
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        long current = await ScalarAsync<long>(connection,
            "SELECT sequence FROM audit_spike_concurrent_head WHERE id = 1 FOR UPDATE", transaction);
        long next = current + 1;
        await ExecuteAsync(connection,
            $"INSERT INTO audit_spike_concurrent_events(sequence) VALUES ({next})", transaction);
        await ExecuteAsync(connection,
            $"UPDATE audit_spike_concurrent_head SET sequence = {next} WHERE id = 1", transaction);
        await transaction.CommitAsync();
    }

    private static async Task AssertContiguousSequencesAsync(DbConnection connection)
    {
        Assert.Equal(ConcurrentAppendCount, await ScalarAsync<long>(connection,
            "SELECT sequence FROM audit_spike_concurrent_head WHERE id = 1"));
        Assert.Equal(ConcurrentAppendCount, await ScalarAsync<long>(connection,
            "SELECT COUNT(*) FROM audit_spike_concurrent_events"));
        Assert.Equal(ConcurrentAppendCount * (ConcurrentAppendCount + 1) / 2, await ScalarAsync<long>(connection,
            "SELECT SUM(sequence) FROM audit_spike_concurrent_events"));
    }

    private static async Task ExecuteAsync(DbConnection connection, string sql, DbTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<T> ScalarAsync<T>(DbConnection connection, string sql, DbTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        object value = await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("No value returned.");
        return (T)Convert.ChangeType(value, typeof(T));
    }
}
