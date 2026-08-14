using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Platform.Application.Authentication;
using ScoutCampPlanner.Platform.Application.Auditing;
using ScoutCampPlanner.Platform.Infrastructure;
using ScoutCampPlanner.Platform.Infrastructure.Authentication;
using ScoutCampPlanner.Platform.Infrastructure.Auditing;
using Xunit;

namespace ScoutCampPlanner.PlatformTests;

public sealed class PasswordAuthenticationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 14, 21, 0, 0, TimeSpan.Zero);
    private const string Password = "River maple lantern orbit 47!";

    [Fact]
    public async Task ValidPasswordCreatesServerManagedSession()
    {
        await using var fixture = await AuthenticationFixture.CreateAsync();

        SignInResult result = await fixture.Authentication.SignInAsync(
            new SignInRequest(" OWNER@example.com ", Password), TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccessful);
        AuthenticationSession session = await fixture.Database.AuthenticationSessions
            .SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(result.User!.UserId, session.UserId);
        Assert.Equal(Now, session.CreatedAtUtc);
        Assert.Equal(Now.AddHours(12), session.AbsoluteExpiresAtUtc);
        Assert.True(session.IsValid(Now.AddMinutes(29), 1));
        Assert.False(session.IsValid(Now.AddMinutes(31), 1));
        Assert.False(session.IsValid(Now.AddMinutes(1), 2));
        Assert.Equal("success", (await fixture.Database.AuditEvents.OrderBy(value => value.Sequence)
            .LastAsync(TestContext.Current.CancellationToken)).Result);
    }

    [Fact]
    public async Task WrongPasswordReturnsGenericFailureWithoutSession()
    {
        await using var fixture = await AuthenticationFixture.CreateAsync();

        SignInResult result = await fixture.Authentication.SignInAsync(
            new SignInRequest("owner@example.com", "definitely wrong"),
            TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccessful);
        Assert.Null(result.User);
        Assert.Empty(await fixture.Database.AuthenticationSessions.ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal("invalid-credentials", (await fixture.Database.AuditEvents.OrderBy(value => value.Sequence)
            .LastAsync(TestContext.Current.CancellationToken)).Result);
    }

    [Fact]
    public async Task UnknownAccountReturnsSameGenericFailure()
    {
        await using var fixture = await AuthenticationFixture.CreateAsync();

        SignInResult result = await fixture.Authentication.SignInAsync(
            new SignInRequest("unknown@example.com", Password), TestContext.Current.CancellationToken);

        Assert.False(result.IsSuccessful);
        Assert.Null(result.User);
        Assert.Empty(await fixture.Database.AuthenticationSessions.ToArrayAsync(TestContext.Current.CancellationToken));
    }

    private sealed class AuthenticationFixture(
        SqliteConnection connection,
        PlatformDbContext database,
        Argon2idPasswordVerifier verifier) : IAsyncDisposable
    {
        public PlatformDbContext Database { get; } = database;
        public PasswordAuthenticationService Authentication { get; private set; } = null!;

        public static async Task<AuthenticationFixture> CreateAsync()
        {
            SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync(TestContext.Current.CancellationToken);
            var database = new PlatformDbContext(
                new DbContextOptionsBuilder<PlatformDbContext>().UseSqlite(connection).Options);
            await database.Database.EnsureCreatedAsync(TestContext.Current.CancellationToken);
            var verifier = new Argon2idPasswordVerifier(Argon2idOperatingMode.SingleDevice);
            var timeProvider = new FixedTimeProvider(Now);
            Guid instanceId = Guid.NewGuid();
            var keys = new FixedAuditKeyProvider();
            var auditRuntime = new AuditRuntimeState(instanceId);
            var auditedOperation = new AuditedOperationExecutor(database, keys);
            await new AuditJournalInitializer(database, keys).InitializeAsync(
                instanceId, Guid.NewGuid(), Now, TestContext.Current.CancellationToken);
            var setup = new InitialSetupService(
                database, new PasswordPolicy(), verifier, timeProvider, auditedOperation, auditRuntime);
            InitialSetupResult result = await setup.CompleteAsync(
                new InitialSetupRequest("Test", "owner@example.com", Password),
                TestContext.Current.CancellationToken);
            Assert.True(result.IsSuccessful);
            var fixture = new AuthenticationFixture(connection, database, verifier);
            fixture.Authentication = new PasswordAuthenticationService(
                database, verifier, timeProvider, auditedOperation,
                new AuditJournalAppender(database, keys), auditRuntime);
            return fixture;
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

    private sealed class FixedAuditKeyProvider : IAuditSigningKeyProvider
    {
        public Task<AuditSigningKey> GetActiveAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AuditSigningKey("test-key", Enumerable.Range(1, 32).Select(value => (byte)value).ToArray()));
    }
}
