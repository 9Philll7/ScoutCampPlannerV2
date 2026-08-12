using System.Data.Common;
using Microsoft.Data.Sqlite;
using Npgsql;
using Xunit;

namespace ScoutCampPlanner.DatabaseMigrationTests;

public sealed class AuditPersistenceSpikeTests
{
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

    private static async Task ExecuteAsync(DbConnection connection, string sql, DbTransaction? transaction = null)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.Transaction = transaction;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<T> ScalarAsync<T>(DbConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        object value = await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("No value returned.");
        return (T)Convert.ChangeType(value, typeof(T));
    }
}
