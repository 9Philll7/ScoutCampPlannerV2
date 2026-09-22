using System.Net;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ScoutCampPlanner.Api;
using ScoutCampPlanner.Camp.Infrastructure;
using ScoutCampPlanner.Platform.Application.Authorization;
using ScoutCampPlanner.Platform.Domain;
using ScoutCampPlanner.Platform.Infrastructure;
using Xunit;

namespace ScoutCampPlanner.PlatformTests;

public sealed class SingleDeviceRuntimeTests
{
    private const string Token = "test-only-launch-token-with-32-characters";
    private static SingleDeviceRuntime Create(bool enabled = true, string token = Token) => new(
        new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SingleDevice:Enabled"] = enabled.ToString(), ["SingleDevice:AccessToken"] = token,
        }).Build());

    [Fact]
    public void Launch_token_requires_explicit_mode_and_loopback()
    {
        Assert.Throws<InvalidOperationException>(() => Create(token: "short"));
        Assert.False(Create(false).Accepts(IPAddress.Loopback, Token));
        var runtime = Create();
        Assert.True(runtime.Accepts(IPAddress.Loopback, Token));
        Assert.True(runtime.Accepts(IPAddress.IPv6Loopback, Token));
        Assert.False(runtime.Accepts(IPAddress.Parse("192.168.1.2"), Token));
        Assert.False(runtime.Accepts(null, Token));
        Assert.False(runtime.Accepts(IPAddress.Loopback, "wrong"));
        Assert.False(runtime.IsOperator(Guid.Empty));
    }

    [Fact]
    public async Task Persistent_identity_only_sees_matching_active_local_transfers()
    {
        var ct = TestContext.Current.CancellationToken;
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(ct);
        await using var platform = new PlatformDbContext(new DbContextOptionsBuilder<PlatformDbContext>().UseSqlite(connection).Options);
        await using var camps = new CampDbContext(new DbContextOptionsBuilder<CampDbContext>().UseSqlite(connection).Options);
        foreach (string script in new[] { platform.Database.GenerateCreateScript(), camps.Database.GenerateCreateScript() })
        {
            await using var command = connection.CreateCommand();
            command.CommandText = script;
            await command.ExecuteNonQueryAsync(ct);
        }
        var runtime = Create();
        await runtime.InitializeAsync(platform);
        var restarted = Create(token: new string('x', 64));
        await restarted.InitializeAsync(platform);
        Assert.Equal(runtime.IdentityId, restarted.IdentityId);
        Assert.Empty(await platform.UserAccounts.ToArrayAsync(ct));
        var tenant = new Tenant(Guid.NewGuid(), "Imported tenant");
        platform.Tenants.Add(tenant);
        var local = new ScoutCampPlanner.Camp.Domain.Camp(Guid.NewGuid(), tenant.Id, "Local");
        local.BeginLocalTransfer(Guid.NewGuid(), 7);
        var frozen = new ScoutCampPlanner.Camp.Domain.Camp(Guid.NewGuid(), tenant.Id, "Frozen");
        frozen.Freeze(Guid.NewGuid());
        var stale = new ScoutCampPlanner.Camp.Domain.Camp(Guid.NewGuid(), tenant.Id, "Stale");
        stale.BeginLocalTransfer(Guid.NewGuid(), 8);
        var ungranted = new ScoutCampPlanner.Camp.Domain.Camp(Guid.NewGuid(), tenant.Id, "No grant");
        ungranted.BeginLocalTransfer(Guid.NewGuid(), 1);
        camps.Camps.AddRange(local, frozen, stale, ungranted);
        platform.LocalCampAccessGrants.AddRange(
            new(runtime.IdentityId, tenant.Id, local.Id, local.ActiveTransferId!.Value),
            new(runtime.IdentityId, tenant.Id, frozen.Id, frozen.ActiveTransferId!.Value),
            new(runtime.IdentityId, tenant.Id, stale.Id, Guid.NewGuid()));
        await platform.SaveChangesAsync(ct);
        await camps.SaveChangesAsync(ct);
        var access = new LocalDeviceAccess(restarted, platform, camps);
        Assert.Equal(new[] { local.Id }, await access.CampIdsAsync(runtime.IdentityId, tenant.Id, Permissions.Camp.Edit, ct));
        Assert.Equal(new[] { tenant.Id }, await access.TenantIdsAsync(runtime.IdentityId, ct));
        Assert.Empty(await access.CampIdsAsync(Guid.NewGuid(), tenant.Id, Permissions.Camp.View, ct));
        Assert.False(await access.AllowsAsync(runtime.IdentityId, local.Id, Permissions.Camp.ManageMembers, ct));
        Assert.False(await access.AllowsAsync(runtime.IdentityId, ungranted.Id, Permissions.Camp.Edit, ct));
        Assert.Empty(await new LocalDeviceAccess(Create(false), platform, camps)
            .CampIdsAsync(runtime.IdentityId, tenant.Id, Permissions.Camp.Edit, ct));
    }
}
