using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using ScoutCampPlanner.Api;
using ScoutCampPlanner.Api.Camps;
using ScoutCampPlanner.Platform.Application.Authorization;
using ScoutCampPlanner.Platform.Domain;
using ScoutCampPlanner.Platform.Infrastructure.Auditing;
using Xunit;

namespace ScoutCampPlanner.PlatformTests;

public sealed partial class CampManagementServiceTests
{
    private async Task<CampExplicitPermissionService> GrantServiceAsync(bool failAudit = false, bool local = false)
    {
        var runtime = new AuditRuntimeState((await platform.AuditJournalHeads.SingleAsync(TestContext.Current.CancellationToken)).InstanceId);
        var executor = new AuditedOperationExecutor(platform, new FixedAuditKeyProvider());
        var device = new SingleDeviceRuntime(new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["SingleDevice:Enabled"] = local.ToString(),
            ["SingleDevice:AccessToken"] = new string('x', 32)
        }).Build());
        return new(platform, camps, service, failAudit ? new FailingParticipantAudit(executor) : executor,
            runtime, new FixedTimeProvider(Now), device);
    }

    [Theory]
    [InlineData(Permissions.Health.ReadParticipantRequirements)]
    [InlineData(Permissions.Health.EditParticipantRequirements)]
    [InlineData(Permissions.Catering.VerifyMealPlanning)]
    public async Task Camp_administrator_can_explicitly_grant_and_revoke_but_does_not_gain_access_implicitly(string permission)
    {
        var ct = TestContext.Current.CancellationToken;
        var (campId, _) = await ParticipantFixtureAsync(false, false);
        var grants = await GrantServiceAsync();
        Guid membershipId = (await platform.CampMemberships.SingleAsync(ct)).Id;
        Assert.False(await service.HasCampPermissionAsync(otherUserId, campId, permission, ct));
        Assert.Equal(ExplicitPermissionResult.Success, await grants.SetCloudAsync(otherUserId, campId, membershipId, permission, true, ct));
        Assert.True(await service.HasCampPermissionAsync(otherUserId, campId, permission, ct));
        Assert.Equal(ExplicitPermissionResult.Success, await grants.SetCloudAsync(otherUserId, campId, membershipId, permission, true, ct));
        Assert.Single(await platform.CampPermissionGrants.ToArrayAsync(ct));
        var member = Assert.Single((await grants.ListAsync(otherUserId, campId, ct))!);
        Assert.Equal(membershipId, member.MembershipId);
        Assert.Equal(permission, Assert.Single(member.Permissions));
        Assert.Equal(ExplicitPermissionResult.Success, await grants.SetCloudAsync(otherUserId, campId, membershipId, permission, false, ct));
        Assert.False(await service.HasCampPermissionAsync(otherUserId, campId, permission, ct));
        Assert.Empty(await platform.CampPermissionGrants.ToArrayAsync(ct));
        var audit = await platform.AuditEvents.Where(value => value.Action.StartsWith("camp.permission.")).ToArrayAsync(ct);
        Assert.Contains(audit, value => value.Action == "camp.permission.granted");
        Assert.Contains(audit, value => value.Action == "camp.permission.revoked");
        Assert.All(audit, value =>
        {
            Assert.Contains(permission, value.MetadataJson);
            Assert.DoesNotContain("example.com", value.MetadataJson);
            Assert.Equal(membershipId, value.TargetId);
        });
    }

    [Fact]
    public async Task Permission_grants_reject_non_admin_wrong_membership_unknown_right_and_single_device_http_path()
    {
        var ct = TestContext.Current.CancellationToken;
        var (campId, _) = await ParticipantFixtureAsync(false, false);
        var grants = await GrantServiceAsync();
        Guid membershipId = (await platform.CampMemberships.SingleAsync(ct)).Id;
        string permission = Permissions.Health.ReadParticipantRequirements;
        Assert.Equal(ExplicitPermissionResult.NotFound, await grants.SetCloudAsync(ownerUserId, campId, membershipId, permission, true, ct));
        Assert.Equal(ExplicitPermissionResult.NotFound, await grants.SetCloudAsync(otherUserId, campId, Guid.NewGuid(), permission, true, ct));
        Assert.Equal(ExplicitPermissionResult.Invalid, await grants.SetCloudAsync(otherUserId, campId, membershipId, Permissions.Camp.ManageMembers, true, ct));
        var local = await GrantServiceAsync(local: true);
        Assert.Equal(ExplicitPermissionResult.NotFound, await local.SetCloudAsync(otherUserId, campId, membershipId, permission, true, ct));
        Assert.Null(await local.ListAsync(otherUserId, campId, ct));
        Assert.Empty(await platform.CampPermissionGrants.ToArrayAsync(ct));
        Assert.Equal(3, await platform.AuditEvents.CountAsync(value => value.Action == "camp.permission.denied", ct));
    }

    [Fact]
    public async Task Explicit_grant_is_rolled_back_when_audit_fails()
    {
        var ct = TestContext.Current.CancellationToken;
        var (campId, _) = await ParticipantFixtureAsync(false, false);
        var grants = await GrantServiceAsync(failAudit: true);
        Guid membershipId = (await platform.CampMemberships.SingleAsync(ct)).Id;
        await Assert.ThrowsAsync<InvalidOperationException>(() => grants.SetCloudAsync(otherUserId, campId, membershipId,
            Permissions.Health.ReadParticipantRequirements, true, ct));
        Assert.Empty(await platform.CampPermissionGrants.ToArrayAsync(ct));
        Assert.DoesNotContain(await platform.AuditEvents.ToArrayAsync(ct), value => value.Action == "camp.permission.granted");
    }

    [Fact]
    public async Task Local_command_requires_an_existing_device_access_and_current_transfer()
    {
        var ct = TestContext.Current.CancellationToken;
        var (campId, _) = await ParticipantFixtureAsync(false, false);
        var grants = await GrantServiceAsync();
        string permission = Permissions.Health.ReadParticipantRequirements;
        Guid transfer = Guid.NewGuid();
        Assert.Equal(ExplicitPermissionResult.NotFound, await grants.SetLocalFromCommandAsync(campId, transfer, permission, true, ct));
        var device = new LocalDeviceIdentity(Guid.NewGuid());
        platform.LocalDeviceIdentities.Add(device);
        var camp = await camps.Camps.SingleAsync(value => value.Id == campId, ct);
        camp.BeginLocalTransfer(transfer, 1);
        await camps.SaveChangesAsync(ct);
        await platform.SaveChangesAsync(ct);
        Assert.Equal(ExplicitPermissionResult.Conflict, await grants.SetLocalFromCommandAsync(campId, transfer, permission, true, ct));
        platform.LocalCampAccessGrants.Add(new(device.Id, tenantId, campId, transfer));
        await platform.SaveChangesAsync(ct);
        Assert.Equal(ExplicitPermissionResult.Conflict, await grants.SetLocalFromCommandAsync(campId, Guid.NewGuid(), permission, true, ct));
        Assert.Equal(ExplicitPermissionResult.Success, await grants.SetLocalFromCommandAsync(campId, transfer, permission, true, ct));
        var grant = Assert.Single(await platform.LocalCampPermissionGrants.ToArrayAsync(ct));
        Assert.Equal(device.Id, grant.DeviceIdentityId);
        Assert.Equal(transfer, grant.TransferId);
        Assert.Empty(await platform.CampPermissionGrants.ToArrayAsync(ct));
        Assert.Equal(ExplicitPermissionResult.Success, await grants.SetLocalFromCommandAsync(campId, transfer, permission, false, ct));
        Assert.Empty(await platform.LocalCampPermissionGrants.ToArrayAsync(ct));
        Assert.Equal(2, await platform.AuditEvents.CountAsync(value => value.Action == "camp.permission.denied" && value.Origin == "single-device", ct));
    }
}

public sealed class LocalPermissionCommandTests
{
    [Fact]
    public void Command_requires_explicit_arguments_and_confirmation()
    {
        string camp = Guid.NewGuid().ToString(), transfer = Guid.NewGuid().ToString();
        string[] args = ["--local-permission-command", "grant", "--local-permission-camp", camp,
            "--local-permission-transfer", transfer, "--local-permission-name", Permissions.Health.ReadParticipantRequirements,
            "--local-permission-confirm", "true"];
        var command = LocalPermissionCommand.Parse(args);
        Assert.NotNull(command);
        Assert.Equal(Guid.Parse(camp), command.CampId);
        Assert.True(command.Granted);
        Assert.Null(LocalPermissionCommand.Parse([]));
        Assert.Throws<ArgumentException>(() => LocalPermissionCommand.Parse(args[..^2]));
        Assert.Throws<ArgumentException>(() => LocalPermissionCommand.Parse([..args, "--local-permission-confirm", "true"]));
        Assert.Throws<ArgumentException>(() => LocalPermissionCommand.Parse(["--local-permission-command=grant"]));
    }
}
