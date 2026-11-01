using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Domain;
using Xunit;

namespace ScoutCampPlanner.DatabaseMigrationTests;

public sealed partial class DatabaseMigrationTests
{
    [Fact]
    public async Task Sqlite_meal_schedule_upgrade_preserves_ids_activity_and_versions_and_backfills_day_order()
    {
        var ct = TestContext.Current.CancellationToken;
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
        await using var connection = new SqliteConnection("Data Source=:memory:"); await connection.OpenAsync(ct);
        await using var databases = CreateSqliteDatabases(connection);
        await databases.Catering.Database.MigrateAsync("20261020204735_AddCampParticipantFilters", ct);
        Guid camp = Guid.NewGuid(), breakfast = Guid.NewGuid(), dinner = Guid.NewGuid(), first = Guid.NewGuid(), last = Guid.NewGuid();
        // Insert using the old schema; the current EF model already contains the new properties.
        await databases.Catering.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO CampMealTypes (Id, CampId, Name, NormalizedName, SortOrder) VALUES ({breakfast}, {camp}, 'Breakfast', 'BREAKFAST', 0), ({dinner}, {camp}, 'Dinner', 'DINNER', 1)", ct);
        await databases.Catering.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO CampMeals (Id, CampId, MealTypeId, Date, IsActive, ChangeVersion) VALUES ({last}, {camp}, {dinner}, '2027-07-01', 0, 4), ({first}, {camp}, {breakfast}, '2027-07-01', 1, 2)", ct);
        await databases.Catering.Database.MigrateAsync(ct);
        Assert.All(await databases.Catering.CampMealTypes.ToArrayAsync(ct), type => Assert.Equal(CampMealKind.Regular, type.Kind));
        var meals = await databases.Catering.CampMeals.OrderBy(x => x.SortOrder).ToArrayAsync(ct);
        Assert.Equal([first, last], meals.Select(x => x.Id));
        Assert.Equal([0, 1], meals.Select(x => x.SortOrder));
        Assert.Equal([2, 4], meals.Select(x => x.ChangeVersion)); Assert.False(meals[1].IsActive);
        Assert.Equal(0, (await databases.Catering.CampMealScheduleStates.SingleAsync(ct)).Version);
    }
}
