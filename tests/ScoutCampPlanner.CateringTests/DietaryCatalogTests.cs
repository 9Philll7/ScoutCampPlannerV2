using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Application.MealPlanning;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Catering.Infrastructure.Ingredients;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed partial class IngredientCatalogPersistenceTests
{
    [Fact]
    public async Task Tenant_catalog_manager_cannot_write_central_rules_review_or_defaults()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await DatabaseFixture.CreateAsync();
        Guid tenant = Guid.NewGuid(), actor = Guid.NewGuid(), id = Guid.NewGuid();
        var service = new DietaryCatalogService(new DietaryCatalogStore(fixture.Database), new TenantDietaryAuthorization(tenant), TimeProvider.System);
        var request = new SaveDietaryTypeRequest("Tenant type", null, 0, 0, []);
        Assert.Equal(DietaryMutationStatus.Success, (await service.SaveAsync(actor, tenant, id, request, ct)).Status);
        Assert.Equal(DietaryMutationStatus.Forbidden, (await service.SaveAsync(actor, null, Guid.NewGuid(), request, ct)).Status);
        Assert.Equal(DietaryMutationStatus.Forbidden, (await service.SaveAsync(actor, Guid.NewGuid(), Guid.NewGuid(), request, ct)).Status);
        Assert.Equal(DietaryMutationStatus.Forbidden, (await service.ReviewAsync(actor, Guid.NewGuid(), true, null, ct)).Status);
        Assert.Equal(DietaryMutationStatus.Forbidden, (await service.SetThresholdDefaultAsync(actor, Guid.NewGuid(), new(1m, "Synthetic", 0), ct)).Status);
        Assert.Null(await service.ContributionsAsync(actor, ct));
        Assert.Single(await fixture.Database.DietaryRequirements.ToArrayAsync(ct));
    }

    private sealed class TenantDietaryAuthorization(Guid tenant) : ScoutCampPlanner.Catering.Application.Ingredients.IIngredientManagementAuthorization
    {
        public Task<bool> CanManageCentralAsync(Guid actorUserId, CancellationToken cancellationToken = default) => Task.FromResult(false);
        public Task<bool> CanManageTenantAsync(Guid actorUserId, Guid tenantId, CancellationToken cancellationToken = default) => Task.FromResult(tenantId == tenant);
        public Task<bool> CanManageCampAsync(Guid actorUserId, Guid campId, CancellationToken cancellationToken = default) => Task.FromResult(false);
    }

    [Fact]
    public async Task Dietary_rules_are_scoped_versioned_and_missing_rules_stay_unknown()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await DatabaseFixture.CreateAsync();
        var store = new DietaryCatalogStore(fixture.Database);
        Guid tenant = Guid.NewGuid(), id = Guid.NewGuid();
        Guid plant = await fixture.Database.Set<IngredientOriginPropertyRecord>().Where(value => value.Code == "PLANT").Select(value => value.Id).SingleAsync(ct);
        var request = new SaveDietaryTypeRequest("Custom", null, 0, 0, [new(plant, DietaryOriginDecision.Allowed)]);
        Assert.Equal(DietaryMutationStatus.Success, (await store.SaveAsync(id, tenant, request, ct)).Status);
        Assert.Empty(await store.ListAsync(Guid.NewGuid(), ct));
        Assert.Single(await store.ListAsync(tenant, ct));
        Assert.Equal(DietaryMutationStatus.NotFound, (await store.SaveAsync(id, null, request, ct)).Status);
        Assert.Equal(DietaryMutationStatus.Conflict, (await store.SaveAsync(id, tenant, request, ct)).Status);
        var diet = await fixture.Database.DietaryRequirements.SingleAsync(ct);
        Assert.Null(diet.DecisionFor(Guid.NewGuid()));
        Assert.Equal(DietaryOriginDecision.Allowed, diet.DecisionFor(plant));
        Assert.Equal(DietaryMutationStatus.Success, (await store.SaveAsync(id, tenant, request with { ExpectedVersion = 1, Rules = [] }, ct)).Status);
        Assert.Equal(2, await fixture.Database.Set<DietaryRequirementRevisionRecord>().CountAsync(ct));
        Assert.Null(diet.DecisionFor(plant));
        var snapshot = await fixture.Database.Set<DietaryRequirementRevisionRecord>().FirstAsync(ct);
        snapshot.SnapshotJson = "changed";
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Database.SaveChangesAsync(ct));
    }

    [Fact]
    public async Task Dietary_contribution_reviews_the_submitted_revision_not_later_edits()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await DatabaseFixture.CreateAsync();
        var store = new DietaryCatalogStore(fixture.Database);
        Guid tenant = Guid.NewGuid(), id = Guid.NewGuid(), actor = Guid.NewGuid();
        var request = new SaveDietaryTypeRequest("Submitted name", null, 0, 0, []);
        await store.SaveAsync(id, tenant, request, ct);
        var submitted = await store.SubmitAsync(id, tenant, 1, actor, DateTimeOffset.UtcNow, ct);
        Assert.Equal(DietaryMutationStatus.Success, submitted.Status);
        await store.SaveAsync(id, tenant, request with { Name = "Newer local name", ExpectedVersion = 1 }, ct);
        var accepted = await store.ReviewAsync(submitted.Id!.Value, true, null, actor, DateTimeOffset.UtcNow, ct);
        Assert.Equal(DietaryMutationStatus.Success, accepted.Status);
        Assert.Equal("Submitted name", Assert.Single(await store.ListAsync(null, ct)).Name);
        Assert.Equal(DietaryMutationStatus.Conflict, (await store.ReviewAsync(submitted.Id.Value, false, null, actor, DateTimeOffset.UtcNow, ct)).Status);
        Assert.Equal("Newer local name", (await fixture.Database.DietaryRequirements.SingleAsync(value => value.Id == id, ct)).Name);
    }

    [Fact]
    public async Task Participant_catalog_does_not_offer_or_accept_another_tenants_diet()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await DatabaseFixture.CreateAsync();
        Guid tenant = Guid.NewGuid(), id = Guid.NewGuid();
        fixture.Database.DietaryRequirements.Add(new DietaryRequirement(id, "Tenant only", tenant));
        await fixture.Database.SaveChangesAsync(ct);
        var catalog = new ParticipantRequirementCatalogStore(fixture.Database);
        Assert.Empty((await catalog.ReadAsync(ct)).DietTypes);
        Assert.Single((await catalog.ReadAsync(ct, tenant)).DietTypes);
        Assert.False(await catalog.ContainsAsync(id, [], [], ct, Guid.NewGuid()));
        Assert.True(await catalog.ContainsAsync(id, [], [], ct, tenant));
    }

    [Fact]
    public async Task Threshold_defaults_start_unknown_require_explicit_source_and_exclude_histamine()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await DatabaseFixture.CreateAsync();
        var catalog = new ParticipantRequirementCatalogStore(fixture.Database);
        var initial = (await catalog.ReadAsync(ct)).Substances;
        Assert.All(initial, value => Assert.Null(value.DefaultThresholdGramsPerPortion));
        Guid lactose = initial.Single(value => value.Code == "LACTOSE").Id;
        Guid histamine = initial.Single(value => value.Code == "HISTAMINE").Id;
        var store = new DietaryCatalogStore(fixture.Database);
        Assert.Equal(DietaryMutationStatus.Invalid, (await store.SetThresholdDefaultAsync(histamine, new(1m, "Dummy", 0), ct)).Status);
        Assert.Equal(DietaryMutationStatus.Invalid, (await store.SetThresholdDefaultAsync(lactose, new(1m, null, 0), ct)).Status);
        Assert.Equal(DietaryMutationStatus.Success, (await store.SetThresholdDefaultAsync(lactose, new(1.25m, "Synthetic test value", 0), ct)).Status);
        var saved = (await catalog.ReadAsync(ct)).Substances.Single(value => value.Id == lactose);
        Assert.Equal(1.25m, saved.DefaultThresholdGramsPerPortion);
        Assert.Equal(DietaryMutationStatus.Conflict, (await store.SetThresholdDefaultAsync(lactose, new(2m, "Synthetic test", 0), ct)).Status);
    }
}
