using Microsoft.Data.Sqlite;

namespace ScoutCampPlanner.Api;

public static class SqliteRuntimeConnection
{
    public static SqliteConnection Create(string connectionString) => new(
        new SqliteConnectionStringBuilder(connectionString) { ForeignKeys = true }.ToString());
}
