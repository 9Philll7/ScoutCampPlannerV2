using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Platform.Domain;
using ScoutCampPlanner.Platform.Infrastructure;
using ScoutCampPlanner.Platform.Infrastructure.Authentication;
using Xunit;

namespace ScoutCampPlanner.PlatformTests;

public sealed class IdentityModelTests
{
    [Fact]
    public void UserAccount_retainsTrimmedDisplayEmailAndNormalizesLookupEmail()
    {
        var account = new UserAccount(Guid.NewGuid(), "  Max.Example@example.com  ");

        Assert.Equal("Max.Example@example.com", account.Email);
        Assert.Equal("MAX.EXAMPLE@EXAMPLE.COM", account.NormalizedEmail);
        Assert.Equal(UserAccountState.PendingConfirmation, account.State);
    }

    [Fact]
    public void RemovedMembershipCannotBeRestored()
    {
        var membership = new TenantMembership(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        membership.Suspend();
        membership.Restore();
        membership.Remove();

        Assert.Equal(TenantMembershipState.Removed, membership.State);
        Assert.Throws<InvalidOperationException>(membership.Restore);
        Assert.Throws<InvalidOperationException>(membership.Suspend);
    }

    [Fact]
    public async Task Sqlite_enforcesIdentityUniquenessAndAllowsMembershipAfterRemoval()
    {
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        var options = new DbContextOptionsBuilder<PlatformDbContext>().UseSqlite(connection).Options;
        await using var database = new PlatformDbContext(options);
        await database.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        var tenant = new Tenant(Guid.NewGuid(), "Test Tenant");
        var user = new UserAccount(Guid.NewGuid(), "person@example.com");
        var membership = new TenantMembership(Guid.NewGuid(), user.Id, tenant.Id);
        database.AddRange(tenant, user, membership);
        database.PasswordCredentials.Add(new PasswordCredential(
            user.Id,
            "$argon2id$v=19$m=19456,t=2,p=1$synthetic$synthetic",
            DateTimeOffset.UtcNow));
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);

        database.UserAccounts.Add(new UserAccount(Guid.NewGuid(), "PERSON@example.com"));
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            database.SaveChangesAsync(TestContext.Current.CancellationToken));
        database.ChangeTracker.Clear();

        database.TenantMemberships.Add(new TenantMembership(Guid.NewGuid(), user.Id, tenant.Id));
        await Assert.ThrowsAsync<DbUpdateException>(() =>
            database.SaveChangesAsync(TestContext.Current.CancellationToken));
        database.ChangeTracker.Clear();

        var storedMembership = await database.TenantMemberships.SingleAsync(TestContext.Current.CancellationToken);
        storedMembership.Remove();
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);

        database.TenantMemberships.Add(new TenantMembership(Guid.NewGuid(), user.Id, tenant.Id));
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, await database.TenantMemberships.CountAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, await database.PasswordCredentials.CountAsync(TestContext.Current.CancellationToken));
    }
}
