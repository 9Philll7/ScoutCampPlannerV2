using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Platform.Infrastructure;
using ScoutCampPlanner.Platform.Infrastructure.Auditing;
using Xunit;

namespace ScoutCampPlanner.PlatformTests;

public sealed class AuditRuntimeBootstrapperTests : IAsyncLifetime
{
    private readonly string directory = Path.Combine(
        Path.GetTempPath(), $"scoutcampplanner-audit-runtime-{Guid.NewGuid():N}");

    [Fact]
    public async Task BootstrapCreatesAndReloadsStableProtectedInstance()
    {
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var database = new PlatformDbContext(
            new DbContextOptionsBuilder<PlatformDbContext>().UseSqlite(connection).Options);
        await database.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var store = new FileAuditProtectedMaterialStore(directory, new PlainAuditKeyBundleProtection());
        var keys = new ProtectedMaterialAuditSigningKeyProvider(store);
        var firstState = new AuditRuntimeState();

        await new AuditRuntimeBootstrapper(database, store, keys, firstState, TimeProvider.System)
            .InitializeAsync(TestContext.Current.CancellationToken);
        var secondState = new AuditRuntimeState();
        await new AuditRuntimeBootstrapper(database, store, keys, secondState, TimeProvider.System)
            .InitializeAsync(TestContext.Current.CancellationToken);

        Assert.True(firstState.IsReady);
        Assert.Equal(firstState.InstanceId, secondState.InstanceId);
        Assert.Equal(firstState.InstanceId,
            (await database.AuditJournalHeads.SingleAsync(TestContext.Current.CancellationToken)).InstanceId);
        Assert.True(File.Exists(Path.Combine(directory, "audit-keys.bin")));
        Assert.True(File.Exists(Path.Combine(directory, "audit-checkpoint.json")));
    }

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
        return ValueTask.CompletedTask;
    }
}
