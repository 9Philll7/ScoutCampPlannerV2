using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Domain;
using Xunit;

namespace ScoutCampPlanner.DatabaseMigrationTests;

public sealed partial class DatabaseMigrationTests
{
    [Fact]
    public async Task Sqlite_participant_filter_configuration_roundtrips_and_checks_version()
    {
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var databases = CreateSqliteDatabases(connection);
        await databases.Catering.Database.MigrateAsync();
        var definition = new ParticipantFilterDefinition(1, [new([
            new(ParticipantFilterField.Category, ParticipantFilterOperator.NoneOf, ["ChildYouth", null])])]);
        var filter = new CampParticipantFilter(Guid.NewGuid(), Guid.NewGuid(), "Test filter", definition);
        databases.Catering.Add(filter);
        await databases.Catering.SaveChangesAsync();
        databases.Catering.ChangeTracker.Clear();
        var loaded = await databases.Catering.Set<CampParticipantFilter>().SingleAsync();
        Assert.Equal(definition.CanonicalKey(), loaded.ReadDefinition().CanonicalKey());
        Assert.False(loaded.Update(1, "Renamed", loaded.ReadDefinition()));
        await databases.Catering.SaveChangesAsync();
        databases.Catering.ChangeTracker.Clear();
        Assert.Equal(2, (await databases.Catering.Set<CampParticipantFilter>().SingleAsync()).Version);
    }
}
