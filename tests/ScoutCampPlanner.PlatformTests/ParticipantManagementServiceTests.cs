using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Api.Camps;
using ScoutCampPlanner.Camp.Contracts;
using ScoutCampPlanner.Platform.Application.Auditing;
using ScoutCampPlanner.Platform.Application.Authorization;
using ScoutCampPlanner.Platform.Domain;
using ScoutCampPlanner.Platform.Infrastructure.Auditing;
using Xunit;

namespace ScoutCampPlanner.PlatformTests;

public sealed partial class CampManagementServiceTests
{
    private static ParticipantEditRequest DummyParticipant() => new("Dummy private name", null, [], [], [], []);

    private async Task<(Guid CampId, ParticipantManagementService Participants)> ParticipantFixtureAsync(bool grantRead = true, bool grantEdit = true,
        bool failAuditAfterOperation = false)
    {
        var created = await service.CreateAsync(ownerUserId, tenantId,
            new CreateCampRequest("Dummy participant service", new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 3), [otherMembershipId]), TestContext.Current.CancellationToken);
        var membership = await platform.CampMemberships.SingleAsync(TestContext.Current.CancellationToken);
        if (grantRead) platform.CampPermissionGrants.Add(new(membership.Id, Permissions.Health.ReadParticipantRequirements));
        if (grantEdit) platform.CampPermissionGrants.Add(new(membership.Id, Permissions.Health.EditParticipantRequirements));
        await platform.SaveChangesAsync(TestContext.Current.CancellationToken);
        var head = await platform.AuditJournalHeads.SingleAsync(TestContext.Current.CancellationToken);
        IAuditedOperationExecutor executor = new AuditedOperationExecutor(platform, new FixedAuditKeyProvider());
        if (failAuditAfterOperation) executor = new FailingParticipantAudit(executor);
        return (created.Camp!.Id, new(service, camps, catering, platform, executor,
            new AuditRuntimeState(head.InstanceId), new FixedTimeProvider(Now)));
    }

    [Fact]
    public async Task Participant_crud_is_audited_and_stale_edits_or_deletes_do_not_overwrite()
    {
        var (campId, participants) = await ParticipantFixtureAsync();
        var created = await participants.SaveAsync(otherUserId, campId, null, null, DummyParticipant(), TestContext.Current.CancellationToken);
        Assert.Equal(ParticipantMutationStatus.Success, created.Status);
        var first = Assert.Single((await participants.ListAsync(otherUserId, campId, TestContext.Current.CancellationToken))!);
        Assert.Equal(created.ParticipantId, first.Data.Id);
        Assert.Equal(ParticipantMutationStatus.Success, (await participants.SaveAsync(otherUserId, campId,
            first.Data.Id, first.StateToken, DummyParticipant() with { DisplayName = "Dummy changed" }, TestContext.Current.CancellationToken)).Status);
        Assert.Equal(ParticipantMutationStatus.Conflict, (await participants.SaveAsync(otherUserId, campId,
            first.Data.Id, first.StateToken, DummyParticipant(), TestContext.Current.CancellationToken)).Status);
        Assert.Equal(ParticipantMutationStatus.Conflict, (await participants.DeleteAsync(otherUserId, campId,
            first.Data.Id, first.StateToken, TestContext.Current.CancellationToken)).Status);
        var current = Assert.Single((await participants.ListAsync(otherUserId, campId, TestContext.Current.CancellationToken))!);
        Assert.Equal("Dummy changed", current.Data.DisplayName);
        Assert.Equal(ParticipantMutationStatus.Success, (await participants.DeleteAsync(otherUserId, campId,
            current.Data.Id, current.StateToken, TestContext.Current.CancellationToken)).Status);
        Assert.Empty((await participants.ListAsync(otherUserId, campId, TestContext.Current.CancellationToken))!);
        var events = await platform.AuditEvents.Where(value => value.Action.StartsWith("health.")).ToArrayAsync(TestContext.Current.CancellationToken);
        Assert.Single(events, value => value.Action == "health.participant.created");
        Assert.Single(events, value => value.Action == "health.participant.updated");
        Assert.Single(events, value => value.Action == "health.participant.deleted");
        Assert.All(events, value =>
        {
            Assert.DoesNotContain("Dummy", value.MetadataJson);
            Assert.DoesNotContain(first.StateToken, value.MetadataJson);
            Assert.Equal("{}", value.MetadataJson);
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Participant_access_never_infers_read_or_edit_from_admin_or_other_permission(bool read, bool edit)
    {
        var (campId, participants) = await ParticipantFixtureAsync(read, edit);
        Assert.Equal(read, await participants.ListAsync(otherUserId, campId, TestContext.Current.CancellationToken) is not null);
        Assert.Equal(edit ? ParticipantMutationStatus.Success : ParticipantMutationStatus.NotFound,
            (await participants.SaveAsync(otherUserId, campId, null, null, DummyParticipant(), TestContext.Current.CancellationToken)).Status);
        Assert.Null(await participants.ListAsync(ownerUserId, campId, TestContext.Current.CancellationToken));
        Assert.Equal(ParticipantMutationStatus.NotFound,
            (await participants.SaveAsync(ownerUserId, campId, null, null, DummyParticipant(), TestContext.Current.CancellationToken)).Status);
        Assert.Contains(await platform.AuditEvents.ToListAsync(TestContext.Current.CancellationToken), value => value.Action == "health.participants.denied");
    }

    [Fact]
    public async Task Participant_references_and_period_are_validated_without_partial_writes()
    {
        var (campId, participants) = await ParticipantFixtureAsync();
        ParticipantEditRequest[] invalid =
        [
            DummyParticipant() with { DisplayName = " " },
            DummyParticipant() with { DietTypeId = Guid.NewGuid() },
            DummyParticipant() with { AllergenIds = [Guid.NewGuid()] },
            DummyParticipant() with { AbsentMealIds = [Guid.NewGuid()] },
            DummyParticipant() with { AbsentDays = [new DateOnly(2027, 6, 30)] },
            DummyParticipant() with { Intolerances = [new(Guid.NewGuid(), 1m, "Dummy source")] },
            DummyParticipant() with { Intolerances = [new(Guid.NewGuid(), .0000001m, null)] },
        ];
        foreach (var input in invalid)
            Assert.Equal(ParticipantMutationStatus.Invalid,
                (await participants.SaveAsync(otherUserId, campId, null, null, input, TestContext.Current.CancellationToken)).Status);
        Assert.Empty(await camps.Participants.ToListAsync(TestContext.Current.CancellationToken));
        Assert.DoesNotContain(await platform.AuditEvents.ToListAsync(TestContext.Current.CancellationToken), value => value.Action == "health.participant.created");
    }

    [Fact]
    public async Task Participant_requirements_absences_and_metadata_survive_an_update()
    {
        var (campId, participants) = await ParticipantFixtureAsync();
        var catalog = await new Catering.Infrastructure.ParticipantRequirementCatalogStore(catering)
            .ReadAsync(TestContext.Current.CancellationToken);
        var allergen = catalog.Allergens.First();
        var substance = catalog.Substances.Single(value => value.Code == "LACTOSE");
        var diet = new Catering.Domain.DietaryRequirement(Guid.NewGuid(), "Dummy diet");
        catering.Add(diet);
        await catering.SaveChangesAsync(TestContext.Current.CancellationToken);
        Guid mealId = await catering.CampMeals.Where(value => value.CampId == campId).Select(value => value.Id).FirstAsync(TestContext.Current.CancellationToken);
        var input = DummyParticipant() with { DietTypeId = diet.Id, AllergenIds = [allergen.Id],
            Intolerances = [new(substance.Id, 1.234567m, "Private dummy source")],
            AbsentDays = [new DateOnly(2027, 7, 1)], AbsentMealIds = [mealId] };
        Assert.Equal(ParticipantMutationStatus.Success,
            (await participants.SaveAsync(otherUserId, campId, null, null, input, TestContext.Current.CancellationToken)).Status);
        var first = Assert.Single((await participants.ListAsync(otherUserId, campId, TestContext.Current.CancellationToken))!);
        Assert.Equal(ParticipantMutationStatus.Success, (await participants.SaveAsync(otherUserId, campId,
            first.Data.Id, first.StateToken, input with { Intolerances = [new(substance.Id, 1.234567m, "Changed source")] }, TestContext.Current.CancellationToken)).Status);
        var result = Assert.Single((await participants.ListAsync(otherUserId, campId, TestContext.Current.CancellationToken))!);
        Assert.NotEqual(first.StateToken, result.StateToken);
        Assert.Equal("Changed source", Assert.Single(result.Data.Intolerances).ThresholdSource);
        Assert.Single(result.Data.AbsentDays);
        Assert.Single(result.Data.AbsentMealIds);
        Assert.All(await platform.AuditEvents.ToArrayAsync(TestContext.Current.CancellationToken), value => Assert.DoesNotContain("source", value.MetadataJson));
    }

    [Fact]
    public async Task Frozen_camp_rejects_participant_writes_but_allows_authorized_reads()
    {
        var (campId, participants) = await ParticipantFixtureAsync();
        var camp = await camps.Camps.SingleAsync(value => value.Id == campId, TestContext.Current.CancellationToken);
        camp.Freeze(Guid.NewGuid());
        await camps.SaveChangesAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ParticipantMutationStatus.Frozen,
            (await participants.SaveAsync(otherUserId, campId, null, null, DummyParticipant(), TestContext.Current.CancellationToken)).Status);
        Assert.Empty((await participants.ListAsync(otherUserId, campId, TestContext.Current.CancellationToken))!);
    }

    [Fact]
    public async Task Participant_write_and_success_event_roll_back_together_when_audit_fails()
    {
        var (campId, participants) = await ParticipantFixtureAsync(failAuditAfterOperation: true);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            participants.SaveAsync(otherUserId, campId, null, null, DummyParticipant(), TestContext.Current.CancellationToken));
        Assert.Empty(await camps.Participants.AsNoTracking().ToListAsync(TestContext.Current.CancellationToken));
        Assert.DoesNotContain(await platform.AuditEvents.ToListAsync(TestContext.Current.CancellationToken), value => value.Action.StartsWith("health."));
    }

    [Fact]
    public async Task Foreign_participant_and_foreign_meal_cannot_be_changed_through_an_authorized_camp()
    {
        var ct = TestContext.Current.CancellationToken;
        var (campId, participants) = await ParticipantFixtureAsync();
        var other = await service.CreateAsync(ownerUserId, tenantId,
            new CreateCampRequest("Other dummy camp", new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 3), [otherMembershipId]), ct);
        var foreign = new Camp.Domain.Participant(Guid.NewGuid(), other.Camp!.Id, "Other dummy");
        camps.Participants.Add(foreign);
        await camps.SaveChangesAsync(ct);
        Assert.Equal(ParticipantMutationStatus.NotFound, (await participants.SaveAsync(otherUserId, campId,
            foreign.Id, "foreign-token", DummyParticipant(), ct)).Status);
        Assert.Equal(ParticipantMutationStatus.NotFound, (await participants.DeleteAsync(otherUserId, campId,
            foreign.Id, "foreign-token", ct)).Status);
        Guid foreignMeal = await catering.CampMeals.Where(value => value.CampId == other.Camp.Id)
            .Select(value => value.Id).FirstAsync(ct);
        Assert.Equal(ParticipantMutationStatus.Invalid, (await participants.SaveAsync(otherUserId, campId,
            null, null, DummyParticipant() with { AbsentMealIds = [foreignMeal] }, ct)).Status);
        Assert.Equal("Other dummy", (await camps.Participants.AsNoTracking().SingleAsync(ct)).DisplayName);
    }

    private sealed class FailingParticipantAudit(IAuditedOperationExecutor inner) : IAuditedOperationExecutor
    {
        public Task<AuditAppendReceipt> ExecuteAsync(AuditEventDraft draft, Func<CancellationToken, Task> operation,
            CancellationToken cancellationToken = default) => inner.ExecuteAsync(draft, async ct =>
            {
                await operation(ct);
                throw new InvalidOperationException("Simulated audit failure");
            }, cancellationToken);
    }

    [Fact]
    public async Task Participant_read_does_not_return_data_if_its_required_audit_fails()
    {
        var (campId, participants) = await ParticipantFixtureAsync(failAuditAfterOperation: true);
        camps.Participants.Add(new Camp.Domain.Participant(Guid.NewGuid(), campId, "Private dummy"));
        await camps.SaveChangesAsync(TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            participants.ListAsync(otherUserId, campId, TestContext.Current.CancellationToken));
        Assert.DoesNotContain(await platform.AuditEvents.ToListAsync(TestContext.Current.CancellationToken),
            value => value.Action == "health.participants.read");
    }
}
