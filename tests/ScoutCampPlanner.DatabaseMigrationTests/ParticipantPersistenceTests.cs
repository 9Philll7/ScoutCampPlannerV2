using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Npgsql;
using ScoutCampPlanner.Api.Camps;
using ScoutCampPlanner.Platform.Application.Authorization;
using ScoutCampPlanner.Platform.Domain;
using ScoutCampPlanner.Platform.Infrastructure.Auditing;
using ScoutCampPlanner.Camp.Domain;
using ScoutCampPlanner.Camp.Infrastructure;
using Xunit;

namespace ScoutCampPlanner.DatabaseMigrationTests;

public sealed partial class DatabaseMigrationTests
{
    [Fact]
    public async Task Sqlite_increment_one_upgrade_preserves_meal_plan_and_does_not_grant_sensitive_access()
    {
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        await using var databases = CreateSqliteDatabases(connection);
        await AssertIncrementOneUpgradeAsync(databases);
    }

    [Fact]
    public async Task PostgreSql_increment_one_upgrade_preserves_meal_plan_and_does_not_grant_sensitive_access()
    {
        string? connectionString = Environment.GetEnvironmentVariable("SCOUTCAMPPLANNER_POSTGRES_TEST");
        Assert.SkipWhen(string.IsNullOrWhiteSpace(connectionString), "SCOUTCAMPPLANNER_POSTGRES_TEST is not configured.");
        await using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync();
        await ResetPostgreSqlSchemasAsync(connection);
        await using var databases = CreatePostgreSqlDatabases(connection);
        await AssertIncrementOneUpgradeAsync(databases);
    }

    private static async Task AssertIncrementOneUpgradeAsync(ModuleDatabases databases)
    {
        await databases.Platform.Database.MigrateAsync(databases.Platform.Database.GetMigrations()
            .Single(value => value.EndsWith("_AddLocalDeviceCampAccess", StringComparison.Ordinal)));
        await databases.Camp.Database.MigrateAsync(databases.Camp.Database.GetMigrations()
            .Single(value => value.EndsWith("_AddParticipantEstimates", StringComparison.Ordinal)));
        await databases.Catering.Database.MigrateAsync();
        var camp = new Camp.Domain.Camp(Guid.NewGuid(), Guid.NewGuid(), "Dummy increment 1",
            new DateOnly(2026, 10, 4), new DateOnly(2026, 10, 5));
        databases.Camp.Camps.Add(camp);
        await databases.Camp.SaveChangesAsync();
        var plan = new Catering.Domain.MealPlan(Guid.NewGuid(), camp.Id, "Preserved plan", 2, 4);
        var unit = new Catering.Domain.CookingUnit(Guid.NewGuid(), camp.Id, "Preserved unit", 1, standardMealPlanId: plan.Id);
        databases.Catering.AddRange(plan, unit);
        await databases.Catering.SaveChangesAsync();

        await MigrateToCurrentAsync(databases);
        databases.Catering.ChangeTracker.Clear();
        Assert.Equal(4, (await databases.Catering.MealPlans.SingleAsync()).Version);
        Assert.Equal(plan.Id, (await databases.Catering.CookingUnits.SingleAsync()).StandardMealPlanId);
        Assert.Equal(camp.Name, (await databases.Camp.Camps.AsNoTracking().SingleAsync()).Name);
        Assert.Empty(await databases.Platform.CampPermissionGrants.ToListAsync());
        Assert.Empty(await databases.Platform.LocalCampPermissionGrants.ToListAsync());
        await AssertParticipantPersistenceAsync(databases.Camp);
        await AssertParticipantServiceAsync(databases);
        await MigrateToCurrentAsync(databases);
        Assert.Empty(await databases.Camp.Database.GetPendingMigrationsAsync());
        Assert.Empty(await databases.Platform.Database.GetPendingMigrationsAsync());
    }

    private static async Task AssertParticipantServiceAsync(ModuleDatabases databases)
    {
        var tenant = new Tenant(Guid.NewGuid(), "Dummy service tenant");
        var user = new UserAccount(Guid.NewGuid(), "dummy-participant-service@example.test");
        user.ActivateAfterInitialSetup();
        var tenantMembership = new TenantMembership(Guid.NewGuid(), user.Id, tenant.Id);
        var camp = new Camp.Domain.Camp(Guid.NewGuid(), tenant.Id, "Dummy service camp",
            new DateOnly(2026, 10, 4), new DateOnly(2026, 10, 5));
        var membership = new CampMembership(Guid.NewGuid(), tenantMembership.Id, camp.Id);
        databases.Platform.AddRange(tenant, user, tenantMembership, membership,
            new CampPermissionGrant(membership.Id, Permissions.Health.ReadParticipantRequirements),
            new CampPermissionGrant(membership.Id, Permissions.Health.EditParticipantRequirements));
        await databases.Platform.SaveChangesAsync();
        databases.Camp.Camps.Add(camp);
        await databases.Camp.SaveChangesAsync();
        Guid instance = Guid.NewGuid();
        var keys = new FixedAuditKeyProvider();
        await new AuditJournalInitializer(databases.Platform, keys).InitializeAsync(instance, Guid.NewGuid(), DateTimeOffset.UtcNow);
        var executor = new AuditedOperationExecutor(databases.Platform, keys);
        var runtime = new AuditRuntimeState(instance);
        var authorization = new CampManagementService(databases.Platform, databases.Camp, databases.Catering,
            executor, runtime, TimeProvider.System);
        var participants = new ParticipantManagementService(authorization, databases.Camp, databases.Catering,
            databases.Platform, executor, runtime, TimeProvider.System);
        var input = new ParticipantEditRequest("Dummy provider service", null, [], [], [], []);
        Assert.Equal(ParticipantMutationStatus.Success,
            (await participants.SaveAsync(user.Id, camp.Id, null, null, input)).Status);
        var first = Assert.Single((await participants.ListAsync(user.Id, camp.Id))!);
        Assert.Equal(ParticipantMutationStatus.Success, (await participants.SaveAsync(user.Id, camp.Id,
            first.Data.Id, first.StateToken, input with { DisplayName = "Dummy updated" })).Status);
        Assert.Equal(ParticipantMutationStatus.Conflict, (await participants.DeleteAsync(user.Id, camp.Id,
            first.Data.Id, first.StateToken)).Status);
        var updated = Assert.Single((await participants.ListAsync(user.Id, camp.Id))!);
        Assert.Equal("Dummy updated", updated.Data.DisplayName);
        Assert.Equal(ParticipantMutationStatus.Success, (await participants.DeleteAsync(user.Id, camp.Id,
            updated.Data.Id, updated.StateToken)).Status);
        Assert.Empty((await participants.ListAsync(user.Id, camp.Id))!);
        Assert.Equal(3, await databases.Platform.AuditEvents.CountAsync(value => value.InstanceId == instance &&
            (value.Action == "health.participant.created" || value.Action == "health.participant.updated" || value.Action == "health.participant.deleted")));
    }

    [Theory]
    [InlineData("0.0000001")]
    [InlineData("1000000000000")]
    public void Participant_threshold_rejects_values_that_cannot_roundtrip_without_rounding(string input)
    {
        decimal threshold = decimal.Parse(input, System.Globalization.CultureInfo.InvariantCulture);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ParticipantIntolerance(Guid.NewGuid(), threshold, null));
        Assert.Equal(0.000001m, new ParticipantIntolerance(Guid.NewGuid(), 0.000001m, null).ThresholdGramsPerPortion);
        Assert.Equal(999999999999.999999m,
            new ParticipantIntolerance(Guid.NewGuid(), 999999999999.999999m, null).ThresholdGramsPerPortion);
    }

    private static async Task AssertParticipantPersistenceAsync(CampDbContext database)
    {
        Guid campId = Guid.NewGuid(), substance = Guid.NewGuid(), allergen = Guid.NewGuid(), meal = Guid.NewGuid();
        var date = new DateOnly(2026, 10, 4);
        database.Camps.Add(new Camp.Domain.Camp(campId, Guid.NewGuid(), "Dummy participant persistence", date, date.AddDays(2)));
        var participant = new Participant(Guid.NewGuid(), campId, "Dummy One");
        participant.SetDayAbsent(date, true);
        participant.SetMealAbsent(meal, true);
        participant.SetRequirements(Guid.NewGuid(), [allergen], [new(substance, 1.25m, "Dummy individual value")]);
        database.Participants.Add(participant);
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();
        var result = Assert.Single(await database.GetParticipantsAsync(campId));
        Assert.Equal(participant.Id, result.Id);
        Assert.Equal(participant.DietTypeId, result.DietTypeId);
        Assert.Equal(date, Assert.Single(result.AbsentDays));
        Assert.Equal(meal, Assert.Single(result.AbsentMealIds));
        Assert.Equal(allergen, Assert.Single(result.AllergenIds));
        Assert.Equal(1.25m, Assert.Single(result.Intolerances).ThresholdGramsPerPortion);
        Assert.Empty(await database.GetParticipantsAsync(Guid.NewGuid()));
        var tracked = await database.Participants.SingleAsync(value => value.Id == participant.Id);
        tracked.SetDayAbsent(date, true);
        tracked.SetMealAbsent(meal, true);
        await database.SaveChangesAsync();
        Assert.Single(tracked.AbsentDays);
        Assert.Single(tracked.AbsentMeals);
        tracked.SetRequirements(null, [], [new(substance, 1.25m, "Changed source only")]);
        tracked.SetDayAbsent(date, false);
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();
        result = Assert.Single(await database.GetParticipantsAsync(campId));
        Assert.Null(result.DietTypeId);
        Assert.Empty(result.AllergenIds);
        Assert.Empty(result.AbsentDays);
        Assert.Equal("Changed source only", Assert.Single(result.Intolerances).ThresholdSource);
        await database.Camps.Where(value => value.Id == campId).ExecuteDeleteAsync();
        Assert.Empty(await database.GetParticipantsAsync(campId));
    }

    [Fact]
    public void Participant_absence_and_requirement_updates_are_explicit_and_atomic()
    {
        var participant = new Participant(Guid.NewGuid(), Guid.NewGuid(), " Dummy ");
        Guid meal = Guid.NewGuid(), substance = Guid.NewGuid();
        var date = new DateOnly(2026, 10, 4);
        Assert.Equal("Dummy", participant.DisplayName);
        Assert.True(participant.IsPresent(date, meal));
        participant.SetDayAbsent(date, true);
        participant.SetMealAbsent(meal, true);
        Assert.False(participant.IsPresent(date, Guid.NewGuid()));
        participant.SetDayAbsent(date, false);
        Assert.False(participant.IsPresent(date, meal));
        participant.SetMealAbsent(meal, false);
        Assert.True(participant.IsPresent(date, meal));
        participant.SetRequirements(null, [], [new(substance, 2m, "Copied default")]);
        Assert.Throws<ArgumentException>(() => participant.SetRequirements(Guid.NewGuid(), [],
            [new(substance, 1m, null), new(substance, 3m, null)]));
        Assert.Null(participant.DietTypeId);
        Assert.Equal(2m, Assert.Single(participant.Intolerances).ThresholdGramsPerPortion);
        Assert.Throws<ArgumentOutOfRangeException>(() => new ParticipantIntolerance(substance, -1m, null));
    }
}
