using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ScoutCampPlanner.Api;
using ScoutCampPlanner.Api.Camps;
using ScoutCampPlanner.Package;
using ScoutCampPlanner.Platform.Application.Auditing;
using ScoutCampPlanner.Platform.Infrastructure.Auditing;
using Xunit;

namespace ScoutCampPlanner.PlatformTests;

public sealed partial class CampManagementServiceTests
{
    private async Task<AuditedCampPackageService> AuditedPackagesAsync(bool failAudit = false)
    {
        var ct = TestContext.Current.CancellationToken;
        var context = new TestHttpContextAccessor { HttpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, otherUserId.ToString())], "test"))
        } };
        IAuditedOperationExecutor executor = new AuditedOperationExecutor(platform, new FixedAuditKeyProvider());
        if (failAudit) executor = new FailingParticipantAudit(executor);
        var raw = new CampPackageService(platform, camps, catering, new FixedTimeProvider(Now), new ParticipantPackageAccess(context, service));
        return new(raw, executor, new AuditRuntimeState((await platform.AuditJournalHeads.SingleAsync(ct)).InstanceId),
            new FixedTimeProvider(Now), context, new SingleDeviceRuntime(new ConfigurationBuilder().Build()), camps, catering);
    }

    private sealed class TestHttpContextAccessor : IHttpContextAccessor
    {
        public HttpContext? HttpContext { get; set; }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Participant_export_commits_freeze_and_metadata_audit_together(bool failAudit)
    {
        var ct = TestContext.Current.CancellationToken;
        var (campId, participants) = await ParticipantFixtureAsync();
        await participants.SaveAsync(otherUserId, campId, null, null, DummyParticipant(), ct);
        var packages = await AuditedPackagesAsync(failAudit);
        if (failAudit)
            await Assert.ThrowsAsync<InvalidOperationException>(() => packages.StartOfflineTransferAsync(campId, ct));
        else
        {
            byte[] result = await packages.StartOfflineTransferAsync(campId, ct);
            Assert.Single(CampPackageSerializer.Deserialize(result).Participants!.Items);
        }
        Assert.Equal(!failAudit, (await camps.Camps.AsNoTracking().SingleAsync(ct)).IsFrozen);
        var events = await platform.AuditEvents.Where(value => value.Action == "camp.package.exported").ToArrayAsync(ct);
        Assert.Equal(failAudit ? 0 : 1, events.Length);
        Assert.All(events, value => Assert.Equal("{}", value.MetadataJson));
    }

    [Fact]
    public async Task Participant_export_without_explicit_read_is_denied_audited_and_not_frozen()
    {
        var ct = TestContext.Current.CancellationToken;
        var (campId, participants) = await ParticipantFixtureAsync(grantRead: false);
        await participants.SaveAsync(otherUserId, campId, null, null, DummyParticipant(), ct);
        var packages = await AuditedPackagesAsync();
        await Assert.ThrowsAsync<CampPackageParticipantAccessException>(() => packages.StartOfflineTransferAsync(campId, ct));
        Assert.False((await camps.Camps.AsNoTracking().SingleAsync(ct)).IsFrozen);
        Assert.Single(await platform.AuditEvents.Where(value => value.Action == "camp.package.denied").ToArrayAsync(ct));
        Assert.Empty(await platform.AuditEvents.Where(value => value.Action == "camp.package.exported").ToArrayAsync(ct));
    }
}
