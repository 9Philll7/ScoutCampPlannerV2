using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Platform.Application.Authentication;
using ScoutCampPlanner.Platform.Application.Authorization;
using ScoutCampPlanner.Platform.Domain;
using ScoutCampPlanner.Platform.Infrastructure;
using ScoutCampPlanner.Platform.Infrastructure.Authentication;
using Xunit;

namespace ScoutCampPlanner.PlatformTests;

public sealed class InitialSetupServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 14, 20, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task EmptyInstallationCanCreateFirstOwner()
    {
        await using var fixture = await SetupFixture.CreateAsync();

        Assert.True((await fixture.Service.GetStatusAsync(TestContext.Current.CancellationToken)).IsRequired);
        InitialSetupResult result = await fixture.Service.CompleteAsync(
            new InitialSetupRequest("Pfadfindergruppe Nord", "owner@example.com", "River maple lantern orbit 47!"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccessful);
        Assert.False((await fixture.Service.GetStatusAsync(TestContext.Current.CancellationToken)).IsRequired);
        UserAccount user = await fixture.Database.UserAccounts.SingleAsync(TestContext.Current.CancellationToken);
        Tenant tenant = await fixture.Database.Tenants.SingleAsync(TestContext.Current.CancellationToken);
        TenantMembership membership = await fixture.Database.TenantMemberships.SingleAsync(TestContext.Current.CancellationToken);
        PasswordCredential credential = await fixture.Database.PasswordCredentials.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(UserAccountState.Active, user.State);
        Assert.Equal(user.Id, membership.UserId);
        Assert.Equal(tenant.Id, membership.TenantId);
        Assert.Equal(Now, credential.ChangedAtUtc);
        Assert.NotEqual("River maple lantern orbit 47!", credential.Verifier);
        Assert.Equal(Roles.TenantOwner,
            (await fixture.Database.TenantRoleAssignments.SingleAsync(TestContext.Current.CancellationToken)).RoleIdentifier);
    }

    [Fact]
    public async Task CompletedInstallationRejectsAnotherSetup()
    {
        await using var fixture = await SetupFixture.CreateAsync();
        await fixture.Service.CompleteAsync(
            new InitialSetupRequest("First", "first@example.com", "River maple lantern orbit 47!"),
            TestContext.Current.CancellationToken);

        InitialSetupResult result = await fixture.Service.CompleteAsync(
            new InitialSetupRequest("Second", "second@example.com", "Copper meadow compass cloud 82!"),
            TestContext.Current.CancellationToken);

        Assert.Equal(InitialSetupFailure.AlreadyCompleted, result.Failure);
        Assert.Equal(1, await fixture.Database.UserAccounts.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await fixture.Database.Tenants.CountAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task WeakPasswordDoesNotCreateSetupData()
    {
        await using var fixture = await SetupFixture.CreateAsync();

        InitialSetupResult result = await fixture.Service.CompleteAsync(
            new InitialSetupRequest("Test", "owner@example.com", "passwordpassword"),
            TestContext.Current.CancellationToken);

        Assert.Equal(InitialSetupFailure.PasswordTooWeak, result.Failure);
        Assert.Empty(await fixture.Database.UserAccounts.ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await fixture.Database.Tenants.ToArrayAsync(TestContext.Current.CancellationToken));
    }

    private sealed class SetupFixture(
        SqliteConnection connection,
        PlatformDbContext database,
        Argon2idPasswordVerifier verifier) : IAsyncDisposable
    {
        public PlatformDbContext Database { get; } = database;
        public InitialSetupService Service { get; } = new(
            database, new PasswordPolicy(), verifier, new FixedTimeProvider(Now));

        public static async Task<SetupFixture> CreateAsync()
        {
            SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            var database = new PlatformDbContext(
                new DbContextOptionsBuilder<PlatformDbContext>().UseSqlite(connection).Options);
            await database.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            return new SetupFixture(connection, database,
                new Argon2idPasswordVerifier(Argon2idOperatingMode.SingleDevice));
        }

        public async ValueTask DisposeAsync()
        {
            verifier.Dispose();
            await Database.DisposeAsync();
            await connection.DisposeAsync();
        }
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
