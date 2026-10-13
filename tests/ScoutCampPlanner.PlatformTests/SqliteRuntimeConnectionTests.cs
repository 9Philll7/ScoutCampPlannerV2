using Microsoft.Data.Sqlite;
using ScoutCampPlanner.Api;
using Xunit;

namespace ScoutCampPlanner.PlatformTests;

public sealed class SqliteRuntimeConnectionTests
{
    [Theory]
    [InlineData("Data Source=:memory:")]
    [InlineData("Data Source=:memory:;Foreign Keys=False")]
    public async Task Runtime_connection_enforces_foreign_keys_and_cascades(string connectionString)
    {
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
        await using var connection = SqliteRuntimeConnection.Create(connectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys;";
        Assert.Equal(1L, await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
        command.CommandText = """
            CREATE TABLE Parents (Id INTEGER PRIMARY KEY);
            CREATE TABLE Children (Id INTEGER PRIMARY KEY, ParentId INTEGER NOT NULL REFERENCES Parents(Id) ON DELETE CASCADE);
            INSERT INTO Parents VALUES (1);
            INSERT INTO Children VALUES (2, 1);
            DELETE FROM Parents WHERE Id = 1;
            SELECT COUNT(*) FROM Children;
            """;
        Assert.Equal(0L, await command.ExecuteScalarAsync(TestContext.Current.CancellationToken));
        command.CommandText = "INSERT INTO Children VALUES (3, 99);";
        var exception = await Assert.ThrowsAsync<SqliteException>(() => command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken));
        Assert.Equal(19, exception.SqliteErrorCode);
    }
}
