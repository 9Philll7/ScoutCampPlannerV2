using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Platform.Application.Auditing;
using ScoutCampPlanner.Platform.Infrastructure;
using ScoutCampPlanner.Platform.Infrastructure.Auditing;
using ScoutCampPlanner.Platform.Domain;
using Xunit;

namespace ScoutCampPlanner.PlatformTests;

public sealed class AuditJournalAppenderTests
{
    private static readonly Guid InstanceId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid SegmentId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly byte[] KeyMaterial = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();

    [Fact]
    public async Task SqliteAppenderPersistsContiguousEventsAndAdvancesHead()
    {
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var database = CreateDatabase(connection);
        await database.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var keys = new FixedKeyProvider();
        await new AuditJournalInitializer(database, keys).InitializeAsync(
            InstanceId, SegmentId, DateTimeOffset.UnixEpoch, TestContext.Current.CancellationToken);
        var appender = new AuditJournalAppender(database, keys);

        AuditAppendReceipt first = await appender.AppendAsync(Draft(1), TestContext.Current.CancellationToken);
        AuditAppendReceipt second = await appender.AppendAsync(Draft(2), TestContext.Current.CancellationToken);

        AuditEventRecord[] events = await database.AuditEvents.OrderBy(value => value.Sequence)
            .ToArrayAsync(TestContext.Current.CancellationToken);
        AuditJournalHead head = await database.AuditJournalHeads.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(1, first.Sequence);
        Assert.Equal(2, second.Sequence);
        Assert.Equal(events[0].Hmac, events[1].PreviousHash);
        Assert.Equal(second.Head, head.Head);
        Assert.Equal(2, head.Sequence);
    }

    [Fact]
    public async Task InvalidEventDoesNotChangeEventOrHeadState()
    {
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var database = CreateDatabase(connection);
        await database.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var keys = new FixedKeyProvider();
        await new AuditJournalInitializer(database, keys).InitializeAsync(
            InstanceId, SegmentId, DateTimeOffset.UnixEpoch, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<ArgumentException>(() => new AuditJournalAppender(database, keys).AppendAsync(
            Draft(1) with { TimestampUtc = new DateTimeOffset(2026, 8, 14, 10, 0, 0, TimeSpan.FromHours(2)) },
            TestContext.Current.CancellationToken));

        Assert.Empty(await database.AuditEvents.ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, (await database.AuditJournalHeads.SingleAsync(TestContext.Current.CancellationToken)).Sequence);
    }

    [Fact]
    public async Task SqliteGateSerializesConcurrentProductiveAppends()
    {
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
        string path = Path.Combine(Path.GetTempPath(), $"scoutcampplanner-productive-audit-{Guid.NewGuid():N}.db");
        string connectionString = $"Data Source={path};Default Timeout=10;Pooling=False";
        try
        {
            await using (var setup = CreateDatabase(connectionString))
            {
                await setup.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
                await new AuditJournalInitializer(setup, new FixedKeyProvider()).InitializeAsync(
                    InstanceId, SegmentId, DateTimeOffset.UnixEpoch, TestContext.Current.CancellationToken);
            }

            await Task.WhenAll(Enumerable.Range(1, 12).Select(async number =>
            {
                await using var database = CreateDatabase(connectionString);
                await new AuditJournalAppender(database, new FixedKeyProvider()).AppendAsync(
                    Draft(number), TestContext.Current.CancellationToken);
            }));

            await using var verification = CreateDatabase(connectionString);
            long[] sequences = await verification.AuditEvents.OrderBy(value => value.Sequence)
                .Select(value => value.Sequence).ToArrayAsync(TestContext.Current.CancellationToken);
            Assert.Equal(Enumerable.Range(1, 12).Select(value => (long)value), sequences);
            Assert.Equal(12, (await verification.AuditJournalHeads.SingleAsync(TestContext.Current.CancellationToken)).Sequence);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public async Task AuditedOperationCommitsBusinessStateEventAndHeadTogether()
    {
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var database = CreateDatabase(connection);
        await database.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var keys = new FixedKeyProvider();
        await new AuditJournalInitializer(database, keys).InitializeAsync(
            InstanceId, SegmentId, DateTimeOffset.UnixEpoch, TestContext.Current.CancellationToken);
        Guid tenantId = Guid.NewGuid();

        AuditAppendReceipt receipt = await new AuditedOperationExecutor(database, keys).ExecuteAsync(
            Draft(1),
            _ => { database.Tenants.Add(new Tenant(tenantId, "Audited tenant")); return Task.CompletedTask; },
            TestContext.Current.CancellationToken);

        Assert.Equal(1, receipt.Sequence);
        Assert.True(await database.Tenants.AnyAsync(value => value.Id == tenantId, TestContext.Current.CancellationToken));
        Assert.Single(await database.AuditEvents.ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, (await database.AuditJournalHeads.SingleAsync(TestContext.Current.CancellationToken)).Sequence);
    }

    [Fact]
    public async Task BusinessFailureRollsBackSavedStateAndLeavesAuditHeadUnchanged()
    {
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var database = CreateDatabase(connection);
        await database.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var keys = new FixedKeyProvider();
        await new AuditJournalInitializer(database, keys).InitializeAsync(
            InstanceId, SegmentId, DateTimeOffset.UnixEpoch, TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<InvalidOperationException>(() => new AuditedOperationExecutor(database, keys).ExecuteAsync(
            Draft(1),
            async cancellationToken =>
            {
                database.Tenants.Add(new Tenant(Guid.NewGuid(), "Must roll back"));
                await database.SaveChangesAsync(cancellationToken);
                throw new InvalidOperationException("Synthetic business failure.");
            },
            TestContext.Current.CancellationToken));

        Assert.Empty(await database.Tenants.AsNoTracking().ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await database.AuditEvents.AsNoTracking().ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, (await database.AuditJournalHeads.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken)).Sequence);
    }

    [Fact]
    public async Task AuditConstraintFailureRollsBackBusinessState()
    {
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using var database = CreateDatabase(connection);
        await database.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
        var keys = new FixedKeyProvider();
        await new AuditJournalInitializer(database, keys).InitializeAsync(
            InstanceId, SegmentId, DateTimeOffset.UnixEpoch, TestContext.Current.CancellationToken);
        AuditEventDraft duplicate = Draft(1);
        var executor = new AuditedOperationExecutor(database, keys);
        await executor.ExecuteAsync(duplicate, _ => Task.CompletedTask, TestContext.Current.CancellationToken);
        Guid tenantId = Guid.NewGuid();

        await Assert.ThrowsAsync<DbUpdateException>(() => executor.ExecuteAsync(
            duplicate,
            _ => { database.Tenants.Add(new Tenant(tenantId, "Must roll back")); return Task.CompletedTask; },
            TestContext.Current.CancellationToken));

        Assert.False(await database.Tenants.AsNoTracking().AnyAsync(value => value.Id == tenantId, TestContext.Current.CancellationToken));
        Assert.Single(await database.AuditEvents.AsNoTracking().ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(1, (await database.AuditJournalHeads.AsNoTracking().SingleAsync(TestContext.Current.CancellationToken)).Sequence);
    }

    private static PlatformDbContext CreateDatabase(SqliteConnection connection) =>
        new(new DbContextOptionsBuilder<PlatformDbContext>().UseSqlite(connection).Options);

    private static PlatformDbContext CreateDatabase(string connectionString) =>
        new(new DbContextOptionsBuilder<PlatformDbContext>().UseSqlite(connectionString).Options);

    private static AuditEventDraft Draft(int number) => new(
        Guid.NewGuid(), DateTimeOffset.UnixEpoch.AddSeconds(number), "test.event", "success",
        null, null, null, null, null, "test", InstanceId, Guid.NewGuid(), null, null,
        new Dictionary<string, string> { ["number"] = number.ToString(System.Globalization.CultureInfo.InvariantCulture) });

    private sealed class FixedKeyProvider : IAuditSigningKeyProvider
    {
        public Task<AuditSigningKey> GetActiveAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AuditSigningKey("key-1", KeyMaterial.ToArray()));
    }
}
