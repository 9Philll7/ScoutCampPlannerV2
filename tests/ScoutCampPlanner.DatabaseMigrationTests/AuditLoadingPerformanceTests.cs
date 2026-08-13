using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Npgsql;
using NpgsqlTypes;
using ScoutCampPlanner.AuditSecuritySpike;
using Xunit;

namespace ScoutCampPlanner.DatabaseMigrationTests;

public sealed class AuditLoadingPerformanceTests(ITestOutputHelper output)
{
    private const int EventCount = 100_000;
    private const int StartupCount = 1_000;
    private static readonly byte[] Key = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();

    [Fact]
    public async Task Sqlite_loadsAndVerifiesStartupWindowAndFullJournalWithinSpikeLimit()
    {
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
        string path = Path.Combine(Path.GetTempPath(), $"scoutcampplanner-audit-load-{Guid.NewGuid():N}.db");
        try
        {
            await using var connection = new SqliteConnection($"Data Source={path};Pooling=False");
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            await CreateSchemaAsync(connection);
            IReadOnlyList<AuditChainEntry> source = CreateChain();
            await InsertSqliteAsync(connection, source);

            await AssertLoadingPerformanceAsync(connection, source, "SQLite");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task PostgreSql_loadsAndVerifiesStartupWindowAndFullJournalWithinSpikeLimit()
    {
        string? connectionString = Environment.GetEnvironmentVariable("SCOUTCAMPPLANNER_POSTGRES_TEST");
        if (string.IsNullOrWhiteSpace(connectionString)) return;

        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await ExecuteAsync(connection, "DROP TABLE IF EXISTS audit_spike_loading;");
        await CreateSchemaAsync(connection);
        IReadOnlyList<AuditChainEntry> source = CreateChain();
        await InsertPostgreSqlAsync(connection, source);

        await AssertLoadingPerformanceAsync(connection, source, "PostgreSQL");
    }

    private async Task AssertLoadingPerformanceAsync(
        DbConnection connection,
        IReadOnlyList<AuditChainEntry> source,
        string provider)
    {
        var stopwatch = Stopwatch.StartNew();
        IReadOnlyList<AuditChainEntry> startup = await ReadAsync(connection, EventCount - StartupCount);
        AuditChainVerification startupVerification = AuditHmacChain.Verify(
            startup, source[EventCount - StartupCount - 1].Hmac, source[^1].Hmac, ResolveKey);
        stopwatch.Stop();
        TimeSpan startupDuration = stopwatch.Elapsed;

        stopwatch.Restart();
        IReadOnlyList<AuditChainEntry> full = await ReadAsync(connection, 0);
        AuditChainVerification fullVerification = AuditHmacChain.Verify(
            full, new byte[32], source[^1].Hmac, ResolveKey);
        stopwatch.Stop();

        Assert.True(startupVerification.IsValid);
        Assert.True(fullVerification.IsValid);
        Assert.Equal(StartupCount, startup.Count);
        Assert.Equal(EventCount, full.Count);
        Assert.True(startupDuration < TimeSpan.FromSeconds(5),
            $"{provider} startup loading and verification took {startupDuration.TotalMilliseconds:F1} ms.");
        Assert.True(stopwatch.Elapsed < TimeSpan.FromSeconds(20),
            $"{provider} full loading and verification took {stopwatch.Elapsed.TotalMilliseconds:F1} ms.");
        output.WriteLine(
            $"{provider}: load+verify latest {StartupCount} = {startupDuration.TotalMilliseconds:F1} ms; " +
            $"load+verify {EventCount} = {stopwatch.Elapsed.TotalMilliseconds:F1} ms");
    }

    private static IReadOnlyList<AuditChainEntry> CreateChain()
    {
        var auditEvent = new AuditEventData(
            Guid.Parse("11111111-1111-1111-1111-111111111111"),
            new DateTimeOffset(2026, 8, 13, 0, 0, 0, TimeSpan.Zero),
            "spike.performance", "success", null, null, null, null, null, "spike",
            Guid.Parse("22222222-2222-2222-2222-222222222222"),
            Guid.Parse("33333333-3333-3333-3333-333333333333"), null, null,
            new Dictionary<string, string>());
        var entries = new List<AuditChainEntry>(EventCount);
        byte[] head = new byte[32];
        for (var sequence = 1; sequence <= EventCount; sequence++)
        {
            AuditChainEntry entry = AuditHmacChain.Append(auditEvent, sequence, head, "key-1", Key);
            entries.Add(entry);
            head = entry.Hmac;
        }
        return entries;
    }

    private static async Task CreateSchemaAsync(DbConnection connection)
    {
        string binary = connection is NpgsqlConnection ? "bytea" : "BLOB";
        await ExecuteAsync(connection, $"CREATE TABLE audit_spike_loading(sequence bigint PRIMARY KEY, previous_hash {binary} NOT NULL, key_id text NOT NULL, format_version integer NOT NULL, event_json text NOT NULL, hmac {binary} NOT NULL);");
    }

    private static async Task InsertSqliteAsync(SqliteConnection connection, IReadOnlyList<AuditChainEntry> entries)
    {
        await using DbTransaction transaction = await connection.BeginTransactionAsync(TestContext.Current.CancellationToken);
        await using SqliteCommand command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText = "INSERT INTO audit_spike_loading VALUES ($sequence, $previous, $key, $format, $event, $hmac)";
        command.Parameters.Add("$sequence", SqliteType.Integer);
        command.Parameters.Add("$previous", SqliteType.Blob);
        command.Parameters.Add("$key", SqliteType.Text);
        command.Parameters.Add("$format", SqliteType.Integer);
        command.Parameters.Add("$event", SqliteType.Text);
        command.Parameters.Add("$hmac", SqliteType.Blob);
        foreach (AuditChainEntry entry in entries)
        {
            command.Parameters[0].Value = entry.Sequence;
            command.Parameters[1].Value = entry.PreviousHash;
            command.Parameters[2].Value = entry.KeyId;
            command.Parameters[3].Value = entry.FormatVersion;
            command.Parameters[4].Value = JsonSerializer.Serialize(entry.Event);
            command.Parameters[5].Value = entry.Hmac;
            await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
        await transaction.CommitAsync(TestContext.Current.CancellationToken);
    }

    private static async Task InsertPostgreSqlAsync(NpgsqlConnection connection, IReadOnlyList<AuditChainEntry> entries)
    {
        await using NpgsqlBinaryImporter writer = await connection.BeginBinaryImportAsync(
            "COPY audit_spike_loading(sequence, previous_hash, key_id, format_version, event_json, hmac) FROM STDIN (FORMAT BINARY)",
            TestContext.Current.CancellationToken);
        foreach (AuditChainEntry entry in entries)
        {
            await writer.StartRowAsync(TestContext.Current.CancellationToken);
            await writer.WriteAsync(entry.Sequence, NpgsqlDbType.Bigint, TestContext.Current.CancellationToken);
            await writer.WriteAsync(entry.PreviousHash, NpgsqlDbType.Bytea, TestContext.Current.CancellationToken);
            await writer.WriteAsync(entry.KeyId, NpgsqlDbType.Text, TestContext.Current.CancellationToken);
            await writer.WriteAsync(entry.FormatVersion, NpgsqlDbType.Integer, TestContext.Current.CancellationToken);
            await writer.WriteAsync(JsonSerializer.Serialize(entry.Event), NpgsqlDbType.Text, TestContext.Current.CancellationToken);
            await writer.WriteAsync(entry.Hmac, NpgsqlDbType.Bytea, TestContext.Current.CancellationToken);
        }
        await writer.CompleteAsync(TestContext.Current.CancellationToken);
    }

    private static async Task<IReadOnlyList<AuditChainEntry>> ReadAsync(DbConnection connection, int afterSequence)
    {
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = "SELECT sequence, previous_hash, key_id, format_version, event_json, hmac FROM audit_spike_loading WHERE sequence > @after ORDER BY sequence";
        DbParameter parameter = command.CreateParameter();
        parameter.ParameterName = "@after";
        parameter.Value = afterSequence;
        command.Parameters.Add(parameter);
        await using DbDataReader reader = await command.ExecuteReaderAsync(TestContext.Current.CancellationToken);
        var entries = new List<AuditChainEntry>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
        {
            AuditEventData auditEvent = JsonSerializer.Deserialize<AuditEventData>(reader.GetString(4))!;
            entries.Add(new(reader.GetInt64(0), (byte[])reader.GetValue(1), reader.GetString(2),
                reader.GetInt32(3), auditEvent, (byte[])reader.GetValue(5)));
        }
        return entries;
    }

    private static byte[]? ResolveKey(string keyId) => keyId == "key-1" ? Key : null;

    private static async Task ExecuteAsync(DbConnection connection, string sql)
    {
        await using DbCommand command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }
}
