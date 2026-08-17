using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Api.Camps;
using ScoutCampPlanner.Camp.Infrastructure;
using ScoutCampPlanner.Platform.Application.Authorization;
using ScoutCampPlanner.Platform.Domain;
using ScoutCampPlanner.Platform.Infrastructure;
using ScoutCampPlanner.Platform.Infrastructure.Auditing;
using Xunit;

namespace ScoutCampPlanner.PlatformTests;

public sealed class CampManagementServiceTests : IAsyncLifetime
{
    private static readonly DateTimeOffset Now = new(2026, 8, 15, 8, 0, 0, TimeSpan.Zero);
    private SqliteConnection connection = null!;
    private PlatformDbContext platform = null!;
    private CampDbContext camps = null!;
    private CampManagementService service = null!;
    private Guid tenantId;
    private Guid ownerUserId;
    private Guid otherUserId;
    private Guid otherMembershipId;

    [Fact]
    public async Task OwnerCanCreateCampForAnotherActiveTenantMember()
    {
        Assert.Single(await service.ListTenantsAsync(
            ownerUserId, TestContext.Current.CancellationToken));
        Assert.Equal(2, (await service.ListAdministratorCandidatesAsync(
            ownerUserId, tenantId, TestContext.Current.CancellationToken)).Count);

        CreateCampResult result = await service.CreateAsync(ownerUserId, tenantId,
            new CreateCampRequest("Sommerlager", new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 14),
                [otherMembershipId]),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccessful);
        Assert.Single(await camps.Camps.ToListAsync(TestContext.Current.CancellationToken));
        CampMembership membership = await platform.CampMemberships.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(otherMembershipId, membership.TenantMembershipId);
        Assert.Equal(Roles.CampAdmin,
            (await platform.CampRoleAssignments.SingleAsync(TestContext.Current.CancellationToken)).RoleIdentifier);
        Assert.Equal("camp.created",
            (await platform.AuditEvents.SingleAsync(TestContext.Current.CancellationToken)).Action);
        Assert.Empty(await service.ListCampsAsync(ownerUserId, tenantId, TestContext.Current.CancellationToken));
        Guid administratorUserId = await platform.TenantMemberships.Where(value => value.Id == otherMembershipId)
            .Select(value => value.UserId).SingleAsync(TestContext.Current.CancellationToken);
        Assert.Single(await service.ListCampsAsync(
            administratorUserId, tenantId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TenantMemberCannotCreateCamp()
    {
        Guid memberUserId = await platform.TenantMemberships.Where(value => value.Id == otherMembershipId)
            .Select(value => value.UserId).SingleAsync(TestContext.Current.CancellationToken);

        CreateCampResult result = await service.CreateAsync(memberUserId, tenantId,
            new CreateCampRequest("Nicht erlaubt", new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 14),
                [otherMembershipId]),
            TestContext.Current.CancellationToken);

        Assert.Equal(CreateCampFailure.Forbidden, result.Failure);
        Assert.Empty(await camps.Camps.ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await platform.AuditEvents.ToArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SameNormalizedNameAndPeriodCannotBeCreatedTwice()
    {
        var request = new CreateCampRequest(
            "Sommerlager", new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 14), [otherMembershipId]);
        Assert.True((await service.CreateAsync(
            ownerUserId, tenantId, request, TestContext.Current.CancellationToken)).IsSuccessful);

        CreateCampResult duplicate = await service.CreateAsync(ownerUserId, tenantId,
            request with { Name = "  SOMMERLAGER  " }, TestContext.Current.CancellationToken);

        Assert.Equal(CreateCampFailure.DuplicateCamp, duplicate.Failure);
        Assert.Equal(1, await camps.Camps.CountAsync(TestContext.Current.CancellationToken));

        CreateCampResult anotherPeriod = await service.CreateAsync(ownerUserId, tenantId,
            request with { StartDate = new DateOnly(2028, 7, 1), EndDate = new DateOnly(2028, 7, 14) },
            TestContext.Current.CancellationToken);
        Assert.True(anotherPeriod.IsSuccessful);
    }

    [Fact]
    public async Task CampAdminCanUpdateCampButUnassignedOwnerCannot()
    {
        CreateCampResult created = await service.CreateAsync(ownerUserId, tenantId,
            new CreateCampRequest("Alt", new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 14),
                [otherMembershipId]), TestContext.Current.CancellationToken);

        UpdateCampResult denied = await service.UpdateAsync(ownerUserId, created.Camp!.Id,
            new UpdateCampRequest("Nicht erlaubt", new DateOnly(2028, 7, 1), new DateOnly(2028, 7, 14)),
            TestContext.Current.CancellationToken);
        UpdateCampResult updated = await service.UpdateAsync(otherUserId, created.Camp.Id,
            new UpdateCampRequest("Neu", new DateOnly(2028, 7, 1), new DateOnly(2028, 7, 14)),
            TestContext.Current.CancellationToken);

        Assert.Equal(UpdateCampFailure.NotFound, denied.Failure);
        Assert.True(updated.IsSuccessful);
        Assert.Equal("Neu", (await camps.Camps.SingleAsync(TestContext.Current.CancellationToken)).Name);
        Assert.Equal(new[] { "camp.created", "camp.updated" },
            await platform.AuditEvents.OrderBy(value => value.Sequence)
                .Select(value => value.Action).ToArrayAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CampAdminCanCreateFreeTreeWithSiblingScopedNames()
    {
        CreateCampResult created = await service.CreateAsync(ownerUserId, tenantId,
            new CreateCampRequest("Strukturlager", new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 14),
                [otherMembershipId]), TestContext.Current.CancellationToken);
        Guid campId = created.Camp!.Id;

        CreateStructureNodeResult north = await service.CreateStructureNodeAsync(otherUserId, campId,
            new CreateStructureNodeRequest(null, "Nord"), TestContext.Current.CancellationToken);
        CreateStructureNodeResult duplicate = await service.CreateStructureNodeAsync(otherUserId, campId,
            new CreateStructureNodeRequest(null, " NORD "), TestContext.Current.CancellationToken);
        CreateStructureNodeResult south = await service.CreateStructureNodeAsync(otherUserId, campId,
            new CreateStructureNodeRequest(null, "Süd"), TestContext.Current.CancellationToken);
        CreateStructureNodeResult northGroup = await service.CreateStructureNodeAsync(otherUserId, campId,
            new CreateStructureNodeRequest(north.Node!.Id, "Gruppe 1"), TestContext.Current.CancellationToken);
        CreateStructureNodeResult southGroup = await service.CreateStructureNodeAsync(otherUserId, campId,
            new CreateStructureNodeRequest(south.Node!.Id, "Gruppe 1"), TestContext.Current.CancellationToken);

        Assert.Equal(CreateStructureNodeFailure.DuplicateName, duplicate.Failure);
        Assert.True(northGroup.IsSuccessful);
        Assert.True(southGroup.IsSuccessful);
        Assert.Equal(4, (await service.ListStructureAsync(
            otherUserId, campId, TestContext.Current.CancellationToken))!.Count);
        Assert.Equal(4, await platform.AuditEvents.CountAsync(
            value => value.Action == "camp.structure-node.created", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task FixedStructureLimitsDepthButStillAllowsNodes()
    {
        CreateCampResult created = await service.CreateAsync(ownerUserId, tenantId,
            new CreateCampRequest("Fixiert", new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 14),
                [otherMembershipId]), TestContext.Current.CancellationToken);
        Guid campId = created.Camp!.Id;
        Assert.True(await service.UpdateStructureConfigurationAsync(otherUserId, campId,
            new UpdateStructureConfigurationRequest(["Bereich", "Gruppe"]), TestContext.Current.CancellationToken));

        var root = await service.CreateStructureNodeAsync(otherUserId, campId,
            new CreateStructureNodeRequest(null, "Nord"), TestContext.Current.CancellationToken);
        var child = await service.CreateStructureNodeAsync(otherUserId, campId,
            new CreateStructureNodeRequest(root.Node!.Id, "Gruppe 1"), TestContext.Current.CancellationToken);
        var tooDeep = await service.CreateStructureNodeAsync(otherUserId, campId,
            new CreateStructureNodeRequest(child.Node!.Id, "Zu tief"), TestContext.Current.CancellationToken);

        Assert.True(child.IsSuccessful);
        Assert.Equal(CreateStructureNodeFailure.MaximumDepthReached, tooDeep.Failure);
        Assert.False(await service.UpdateStructureConfigurationAsync(otherUserId, campId,
            new UpdateStructureConfigurationRequest(["Bereich"]), TestContext.Current.CancellationToken));
        Assert.Equal("Fixed", (await service.GetStructureConfigurationAsync(
            otherUserId, campId, TestContext.Current.CancellationToken))!.Mode);
    }

    [Fact]
    public async Task FailedAdministratorPersistenceRollsBackCampAndAudit()
    {
        await ExecuteScriptAsync("""
            CREATE TRIGGER RejectCampRoleAssignment
            BEFORE INSERT ON CampRoleAssignments
            BEGIN
                SELECT RAISE(ABORT, 'simulated role persistence failure');
            END;
            """);

        await Assert.ThrowsAsync<DbUpdateException>(() => service.CreateAsync(ownerUserId, tenantId,
            new CreateCampRequest("Rollback-Lager", new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 14),
                [otherMembershipId]),
            TestContext.Current.CancellationToken));

        Assert.Empty(await camps.Camps.AsNoTracking().ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await platform.CampMemberships.AsNoTracking().ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Empty(await platform.AuditEvents.AsNoTracking().ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(0, (await platform.AuditJournalHeads.AsNoTracking()
            .SingleAsync(TestContext.Current.CancellationToken)).Sequence);
    }

    public async ValueTask InitializeAsync()
    {
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
        connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        platform = new PlatformDbContext(new DbContextOptionsBuilder<PlatformDbContext>().UseSqlite(connection).Options);
        camps = new CampDbContext(new DbContextOptionsBuilder<CampDbContext>().UseSqlite(connection).Options);
        await ExecuteScriptAsync(platform.Database.GenerateCreateScript());
        await ExecuteScriptAsync(camps.Database.GenerateCreateScript());

        tenantId = Guid.NewGuid();
        ownerUserId = Guid.NewGuid();
        var owner = new UserAccount(ownerUserId, "owner@example.com");
        owner.ActivateAfterInitialSetup();
        otherUserId = Guid.NewGuid();
        var other = new UserAccount(otherUserId, "admin@example.com");
        other.ActivateAfterInitialSetup();
        var ownerMembership = new TenantMembership(Guid.NewGuid(), owner.Id, tenantId);
        var otherMembership = new TenantMembership(Guid.NewGuid(), other.Id, tenantId);
        otherMembershipId = otherMembership.Id;
        platform.AddRange(new Tenant(tenantId, "Test"), owner, other, ownerMembership, otherMembership);
        platform.TenantRoleAssignments.AddRange(
            new TenantRoleAssignment(ownerMembership.Id, Roles.TenantOwner),
            new TenantRoleAssignment(otherMembership.Id, Roles.TenantMember));
        await platform.SaveChangesAsync(TestContext.Current.CancellationToken);

        Guid instanceId = Guid.NewGuid();
        var keys = new FixedAuditKeyProvider();
        await new AuditJournalInitializer(platform, keys).InitializeAsync(
            instanceId, Guid.NewGuid(), Now, TestContext.Current.CancellationToken);
        service = new CampManagementService(platform, camps, new AuditedOperationExecutor(platform, keys),
            new AuditRuntimeState(instanceId), new FixedTimeProvider(Now));
    }

    public async ValueTask DisposeAsync()
    {
        await camps.DisposeAsync();
        await platform.DisposeAsync();
        await connection.DisposeAsync();
    }

    private async Task ExecuteScriptAsync(string script)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = script;
        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
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
