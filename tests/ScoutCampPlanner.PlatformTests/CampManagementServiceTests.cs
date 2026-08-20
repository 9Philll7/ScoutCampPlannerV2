using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Api.Camps;
using ScoutCampPlanner.Api.Catering;
using ScoutCampPlanner.Camp.Infrastructure;
using ScoutCampPlanner.Catering.Infrastructure;
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
    private CateringDbContext catering = null!;
    private CampManagementService service = null!;
    private CateringPlanningService cateringService = null!;
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
        var shallowLeaf = await service.CreateStructureNodeAsync(otherUserId, campId,
            new CreateStructureNodeRequest(null, "Süd"), TestContext.Current.CancellationToken);
        var tooDeep = await service.CreateStructureNodeAsync(otherUserId, campId,
            new CreateStructureNodeRequest(child.Node!.Id, "Zu tief"), TestContext.Current.CancellationToken);
        var stages = await service.GetCampStagesAsync(otherUserId, campId, TestContext.Current.CancellationToken);
        var emptyEstimates = stages!.Select(stage => new ParticipantEstimateInput(stage.Id, 0, 0)).ToArray();

        Assert.True(child.IsSuccessful);
        Assert.Equal(UpdateParticipantEstimatesFailure.NotParticipantLevel,
            await service.UpdateParticipantEstimatesAsync(otherUserId, campId, shallowLeaf.Node!.Id,
                new UpdateParticipantEstimatesRequest(emptyEstimates), TestContext.Current.CancellationToken));
        Assert.Equal(UpdateParticipantEstimatesFailure.None,
            await service.UpdateParticipantEstimatesAsync(otherUserId, campId, child.Node.Id,
                new UpdateParticipantEstimatesRequest(emptyEstimates), TestContext.Current.CancellationToken));
        Assert.Equal(CreateStructureNodeFailure.MaximumDepthReached, tooDeep.Failure);
        Assert.False(await service.UpdateStructureConfigurationAsync(otherUserId, campId,
            new UpdateStructureConfigurationRequest(["Bereich"]), TestContext.Current.CancellationToken));
        Assert.Equal("Fixed", (await service.GetStructureConfigurationAsync(
            otherUserId, campId, TestContext.Current.CancellationToken))!.Mode);
    }

    [Fact]
    public async Task CampAdminCanDeleteOnlyLeafStructureNodes()
    {
        CreateCampResult created = await service.CreateAsync(ownerUserId, tenantId,
            new CreateCampRequest("Löschen", new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 14),
                [otherMembershipId]), TestContext.Current.CancellationToken);
        var parent = await service.CreateStructureNodeAsync(otherUserId, created.Camp!.Id,
            new CreateStructureNodeRequest(null, "Bereich"), TestContext.Current.CancellationToken);
        var child = await service.CreateStructureNodeAsync(otherUserId, created.Camp.Id,
            new CreateStructureNodeRequest(parent.Node!.Id, "Gruppe"), TestContext.Current.CancellationToken);

        Assert.Equal(DeleteStructureNodeFailure.HasChildren, await service.DeleteStructureNodeAsync(
            otherUserId, created.Camp.Id, parent.Node.Id, TestContext.Current.CancellationToken));
        Assert.Equal(DeleteStructureNodeFailure.None, await service.DeleteStructureNodeAsync(
            otherUserId, created.Camp.Id, child.Node!.Id, TestContext.Current.CancellationToken));
        Assert.Equal(DeleteStructureNodeFailure.None, await service.DeleteStructureNodeAsync(
            otherUserId, created.Camp.Id, parent.Node.Id, TestContext.Current.CancellationToken));
        Assert.Empty(await camps.StructureNodes.ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Equal(2, await platform.AuditEvents.CountAsync(value =>
            value.Action == "camp.structure-node.deleted", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task CampAdminCanRenameStructureNodeButNotDuplicateSiblingName()
    {
        CreateCampResult created = await service.CreateAsync(ownerUserId, tenantId,
            new CreateCampRequest("Umbenennen", new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 14),
                [otherMembershipId]), TestContext.Current.CancellationToken);
        Guid campId = created.Camp!.Id;
        var north = await service.CreateStructureNodeAsync(otherUserId, campId,
            new CreateStructureNodeRequest(null, "Nord"), TestContext.Current.CancellationToken);
        await service.CreateStructureNodeAsync(otherUserId, campId,
            new CreateStructureNodeRequest(null, "Süd"), TestContext.Current.CancellationToken);

        Assert.Equal(RenameStructureNodeFailure.None, await service.RenameStructureNodeAsync(
            otherUserId, campId, north.Node!.Id, new RenameStructureNodeRequest("West"),
            TestContext.Current.CancellationToken));
        Assert.Equal(RenameStructureNodeFailure.DuplicateName, await service.RenameStructureNodeAsync(
            otherUserId, campId, north.Node.Id, new RenameStructureNodeRequest("Süd"),
            TestContext.Current.CancellationToken));
        Assert.Contains((await service.ListStructureAsync(otherUserId, campId,
            TestContext.Current.CancellationToken))!, node => node.Name == "West");
    }

    [Fact]
    public async Task MovingStructureBranchEnforcesTreeAndFixedDepth()
    {
        CreateCampResult created = await service.CreateAsync(ownerUserId, tenantId,
            new CreateCampRequest("Verschieben", new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 14),
                [otherMembershipId]), TestContext.Current.CancellationToken);
        Guid campId = created.Camp!.Id;
        var north = await service.CreateStructureNodeAsync(otherUserId, campId,
            new CreateStructureNodeRequest(null, "Nord"), TestContext.Current.CancellationToken);
        var south = await service.CreateStructureNodeAsync(otherUserId, campId,
            new CreateStructureNodeRequest(null, "Süd"), TestContext.Current.CancellationToken);
        var group = await service.CreateStructureNodeAsync(otherUserId, campId,
            new CreateStructureNodeRequest(north.Node!.Id, "Gruppe"), TestContext.Current.CancellationToken);
        var patrol = await service.CreateStructureNodeAsync(otherUserId, campId,
            new CreateStructureNodeRequest(group.Node!.Id, "Patrulle"), TestContext.Current.CancellationToken);

        Assert.Equal(MoveStructureNodeFailure.Cycle, await service.MoveStructureNodeAsync(otherUserId, campId,
            north.Node.Id, new MoveStructureNodeRequest(patrol.Node!.Id), TestContext.Current.CancellationToken));
        Assert.True(await service.UpdateStructureConfigurationAsync(otherUserId, campId,
            new UpdateStructureConfigurationRequest(["Bereich", "Gruppe", "Patrulle"]), TestContext.Current.CancellationToken));
        Assert.Equal(MoveStructureNodeFailure.MaximumDepthReached, await service.MoveStructureNodeAsync(
            otherUserId, campId, north.Node.Id, new MoveStructureNodeRequest(south.Node!.Id), TestContext.Current.CancellationToken));
        Assert.Equal(MoveStructureNodeFailure.None, await service.MoveStructureNodeAsync(
            otherUserId, campId, group.Node.Id, new MoveStructureNodeRequest(south.Node.Id), TestContext.Current.CancellationToken));
        Assert.Equal(south.Node.Id, (await camps.StructureNodes.SingleAsync(value =>
            value.Id == group.Node.Id, TestContext.Current.CancellationToken)).ParentId);
    }

    [Fact]
    public async Task TenantAdministratorCanConfigureOrderedStageTemplate()
    {
        var suggested = await service.GetStageTemplateAsync(
            ownerUserId, tenantId, TestContext.Current.CancellationToken);
        Assert.Equal(new[] { "Biber", "WiWö", "GuSp", "CaEx", "RaRo", "Mitarbeiter" },
            suggested!.Select(value => value.Name));

        Assert.Equal(UpdateStageTemplateFailure.Forbidden, await service.UpdateStageTemplateAsync(
            otherUserId, tenantId, new UpdateStageTemplateRequest(["Nicht erlaubt"]),
            TestContext.Current.CancellationToken));
        Assert.Equal(UpdateStageTemplateFailure.None, await service.UpdateStageTemplateAsync(
            ownerUserId, tenantId, new UpdateStageTemplateRequest(["Jung", "Alt"]),
            TestContext.Current.CancellationToken));

        Assert.Equal(new[] { "Jung", "Alt" }, (await service.GetStageTemplateAsync(
            ownerUserId, tenantId, TestContext.Current.CancellationToken))!.Select(value => value.Name));
        Assert.Equal("tenant.stage-template.updated", (await platform.AuditEvents.SingleAsync(
            TestContext.Current.CancellationToken)).Action);
    }

    [Fact]
    public async Task NewCampReceivesStableEditableStageCopy()
    {
        await service.UpdateStageTemplateAsync(ownerUserId, tenantId,
            new UpdateStageTemplateRequest(["Jung", "Alt"]), TestContext.Current.CancellationToken);
        CreateCampResult created = await service.CreateAsync(ownerUserId, tenantId,
            new CreateCampRequest("Stufenlager", new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 14),
                [otherMembershipId]), TestContext.Current.CancellationToken);
        await service.UpdateStageTemplateAsync(ownerUserId, tenantId,
            new UpdateStageTemplateRequest(["Später"]), TestContext.Current.CancellationToken);

        Assert.Equal(new[] { "Jung", "Alt" }, (await service.GetCampStagesAsync(otherUserId,
            created.Camp!.Id, TestContext.Current.CancellationToken))!.Select(value => value.Name));
        Assert.Equal(UpdateStageTemplateFailure.None, await service.UpdateCampStagesAsync(otherUserId,
            created.Camp.Id, new UpdateStageTemplateRequest(["Lagerspezifisch"]), TestContext.Current.CancellationToken));
        Assert.Equal("Lagerspezifisch", (await service.GetCampStagesAsync(otherUserId,
            created.Camp.Id, TestContext.Current.CancellationToken))!.Single().Name);
    }

    [Fact]
    public async Task AnonymousEstimatesAreStoredOnlyOnLeafAndBlockChildrenAndDeletion()
    {
        CreateCampResult created = await service.CreateAsync(ownerUserId, tenantId,
            new CreateCampRequest("Schätzung", new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 14),
                [otherMembershipId]), TestContext.Current.CancellationToken);
        var leaf = await service.CreateStructureNodeAsync(otherUserId, created.Camp!.Id,
            new CreateStructureNodeRequest(null, "Gruppe"), TestContext.Current.CancellationToken);
        var stages = await service.GetCampStagesAsync(otherUserId, created.Camp.Id, TestContext.Current.CancellationToken);
        var inputs = stages!.Select((stage, index) => new ParticipantEstimateInput(stage.Id,
            index == 0 ? 12 : 0, index == 0 ? 3 : 0)).ToArray();

        Assert.Equal(UpdateParticipantEstimatesFailure.None, await service.UpdateParticipantEstimatesAsync(
            otherUserId, created.Camp.Id, leaf.Node!.Id, new UpdateParticipantEstimatesRequest(inputs),
            TestContext.Current.CancellationToken));
        Assert.Equal(CreateStructureNodeFailure.HasEstimates, (await service.CreateStructureNodeAsync(
            otherUserId, created.Camp.Id, new CreateStructureNodeRequest(leaf.Node.Id, "Darunter"),
            TestContext.Current.CancellationToken)).Failure);
        Assert.Equal(DeleteStructureNodeFailure.HasEstimates, await service.DeleteStructureNodeAsync(
            otherUserId, created.Camp.Id, leaf.Node.Id, TestContext.Current.CancellationToken));
        var result = await service.GetParticipantEstimatesAsync(otherUserId, created.Camp.Id, leaf.Node.Id,
            TestContext.Current.CancellationToken);
        Assert.Equal(12, result!.First().ChildYouthCount);
        Assert.Equal(3, result!.First().LeaderCount);
    }

    [Fact]
    public async Task PlanningSummaryAggregatesStagesAndStructureBranches()
    {
        CreateCampResult created = await service.CreateAsync(ownerUserId, tenantId,
            new CreateCampRequest("Übersicht", new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 14),
                [otherMembershipId]), TestContext.Current.CancellationToken);
        Guid campId = created.Camp!.Id;
        var root = await service.CreateStructureNodeAsync(otherUserId, campId,
            new CreateStructureNodeRequest(null, "Bereich"), TestContext.Current.CancellationToken);
        var first = await service.CreateStructureNodeAsync(otherUserId, campId,
            new CreateStructureNodeRequest(root.Node!.Id, "Gruppe 1"), TestContext.Current.CancellationToken);
        var second = await service.CreateStructureNodeAsync(otherUserId, campId,
            new CreateStructureNodeRequest(root.Node.Id, "Gruppe 2"), TestContext.Current.CancellationToken);
        var stages = (await service.GetCampStagesAsync(otherUserId, campId, TestContext.Current.CancellationToken))!;
        ParticipantEstimateInput[] Values(int children, int leaders) => stages.Select((stage, index) =>
            new ParticipantEstimateInput(stage.Id, index == 0 ? children : 0, index == 0 ? leaders : 0)).ToArray();
        await service.UpdateParticipantEstimatesAsync(otherUserId, campId, first.Node!.Id,
            new UpdateParticipantEstimatesRequest(Values(10, 2)), TestContext.Current.CancellationToken);
        await service.UpdateParticipantEstimatesAsync(otherUserId, campId, second.Node!.Id,
            new UpdateParticipantEstimatesRequest(Values(7, 1)), TestContext.Current.CancellationToken);

        CampPlanningSummary summary = (await service.GetPlanningSummaryAsync(
            otherUserId, campId, TestContext.Current.CancellationToken))!;
        Assert.Equal(17, summary.StageTotals.First().ChildYouthCount);
        Assert.Equal(3, summary.StageTotals.First().LeaderCount);
        StructureEstimateTotal rootTotal = summary.StructureTotals.Single(value => value.StructureNodeId == root.Node.Id);
        Assert.Equal(17, rootTotal.ChildYouthCount);
        Assert.Equal(3, rootTotal.LeaderCount);
    }

    [Fact]
    public async Task TenantAdministratorCanConfigureFoodFactorsForExactStages()
    {
        string[] stages = ["Biber", "WiWö"];
        var defaults = await cateringService.GetTenantFactorsAsync(
            ownerUserId, tenantId, stages, TestContext.Current.CancellationToken);
        Assert.All(defaults!, value => Assert.Equal(1m, value.Factor));
        Assert.Equal(UpdateFoodFactorsFailure.Forbidden, await cateringService.UpdateTenantFactorsAsync(
            otherUserId, tenantId, stages,
            new UpdateTenantStageFoodFactorsRequest([new("Biber", 0.7m), new("WiWö", 0.8m)]),
            TestContext.Current.CancellationToken));
        Assert.Equal(UpdateFoodFactorsFailure.None, await cateringService.UpdateTenantFactorsAsync(
            ownerUserId, tenantId, stages,
            new UpdateTenantStageFoodFactorsRequest([new("Biber", 0.7m), new("WiWö", 0.8m)]),
            TestContext.Current.CancellationToken));
        Assert.Equal(new[] { 0.7m, 0.8m }, (await cateringService.GetTenantFactorsAsync(
            ownerUserId, tenantId, stages, TestContext.Current.CancellationToken))!.Select(value => value.Factor));
    }

    [Fact]
    public async Task NewCampReceivesStableFoodFactorsAndWeightedTotalsCountLeadersFully()
    {
        await service.UpdateStageTemplateAsync(ownerUserId, tenantId,
            new UpdateStageTemplateRequest(["Biber"]), TestContext.Current.CancellationToken);
        await cateringService.UpdateTenantFactorsAsync(ownerUserId, tenantId, ["Biber"],
            new UpdateTenantStageFoodFactorsRequest([new("Biber", 0.5m)]), TestContext.Current.CancellationToken);
        CreateCampResult created = await service.CreateAsync(ownerUserId, tenantId,
            new CreateCampRequest("Faktorlager", new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 14),
                [otherMembershipId]), TestContext.Current.CancellationToken);
        CampStageContext context = (await service.GetCampStageContextAsync(
            otherUserId, created.Camp!.Id, TestContext.Current.CancellationToken))!;
        await cateringService.UpdateTenantFactorsAsync(ownerUserId, tenantId, ["Biber"],
            new UpdateTenantStageFoodFactorsRequest([new("Biber", 0.9m)]), TestContext.Current.CancellationToken);
        var factors = await cateringService.GetCampFactorsAsync(tenantId, created.Camp.Id,
            context.Stages.Select(value => new CampStageReference(value.Id, value.Name)).ToList(),
            TestContext.Current.CancellationToken);
        Assert.Equal(0.5m, factors.Single().Factor);

        var summary = new CampPlanningSummary(
            [new StageEstimateTotal(context.Stages.Single().Id, "Biber", 20, 4)], []);
        WeightedStageTotal weighted = CateringPlanningService.CalculateWeightedTotals(summary, factors).Single();
        Assert.Equal(14m, weighted.FoodUnits);
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

    [Fact]
    public async Task NewCampGetsDefaultMealsForEveryCampDay()
    {
        var created = await service.CreateAsync(ownerUserId, tenantId,
            new CreateCampRequest("Mahlzeiten", new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 3), [otherMembershipId]),
            TestContext.Current.CancellationToken);
        var plan = await cateringService.GetMealPlanAsync(created.Camp!.Id, new DateOnly(2027, 7, 1),
            new DateOnly(2027, 7, 3), TestContext.Current.CancellationToken);
        Assert.Equal(["Frühstück", "Mittagessen", "Abendessen"], plan.MealTypes.Select(value => value.Name));
        Assert.Equal(9, plan.Meals.Count);
        Assert.All(plan.Meals, meal => Assert.True(meal.IsActive));
    }

    [Fact]
    public async Task MealTypesAndIndividualActivityCanBeChanged()
    {
        var created = await service.CreateAsync(ownerUserId, tenantId,
            new CreateCampRequest("Mahlzeiten ändern", new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 2), [otherMembershipId]),
            TestContext.Current.CancellationToken);
        Guid campId = created.Camp!.Id;
        Assert.Equal(UpdateCampMealsFailure.None, await cateringService.UpdateMealTypesAsync(ownerUserId, tenantId,
            campId, new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 2), false,
            new UpdateCampMealTypesRequest(["Brunch", "Abendessen"]), TestContext.Current.CancellationToken));
        var plan = await cateringService.GetMealPlanAsync(campId, new DateOnly(2027, 7, 1),
            new DateOnly(2027, 7, 2), TestContext.Current.CancellationToken);
        Assert.Equal(4, plan.Meals.Count);
        var brunch = plan.Meals.First(value => value.MealTypeName == "Brunch");
        Assert.Equal(UpdateCampMealsFailure.None, await cateringService.SetMealActivityAsync(ownerUserId, tenantId, campId, brunch.Id, false,
            new UpdateCampMealActivityRequest(false), TestContext.Current.CancellationToken));
        Assert.False((await cateringService.GetMealPlanAsync(campId, new DateOnly(2027, 7, 1),
            new DateOnly(2027, 7, 2), TestContext.Current.CancellationToken)).Meals.Single(value => value.Id == brunch.Id).IsActive);
    }

    public async ValueTask InitializeAsync()
    {
        SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());
        connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        platform = new PlatformDbContext(new DbContextOptionsBuilder<PlatformDbContext>().UseSqlite(connection).Options);
        camps = new CampDbContext(new DbContextOptionsBuilder<CampDbContext>().UseSqlite(connection).Options);
        catering = new CateringDbContext(new DbContextOptionsBuilder<CateringDbContext>().UseSqlite(connection).Options);
        await ExecuteScriptAsync(platform.Database.GenerateCreateScript());
        await ExecuteScriptAsync(camps.Database.GenerateCreateScript());
        await ExecuteScriptAsync(catering.Database.GenerateCreateScript());

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
        service = new CampManagementService(platform, camps, catering, new AuditedOperationExecutor(platform, keys),
            new AuditRuntimeState(instanceId), new FixedTimeProvider(Now));
        cateringService = new CateringPlanningService(platform, catering,
            new AuditedOperationExecutor(platform, keys), new AuditRuntimeState(instanceId), new FixedTimeProvider(Now));
    }

    public async ValueTask DisposeAsync()
    {
        await camps.DisposeAsync();
        await catering.DisposeAsync();
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
