using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Camp.Contracts;
using ScoutCampPlanner.Camp.Domain;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Package;
using ScoutCampPlanner.Platform.Domain;
using Xunit;

namespace ScoutCampPlanner.PackageTests;

public sealed partial class CampPackageTests
{
    [Fact]
    public async Task Participant_planning_configuration_and_actual_snapshot_survive_replace_and_missing_section_rolls_back()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var cloud = await DatabaseHarness.CreateAsync();
        Guid campId = await SeedParticipantCampAsync(cloud);
        Guid unit = Guid.NewGuid(), meal = Guid.NewGuid(), type = Guid.NewGuid();
        Guid node = Guid.NewGuid();
        cloud.Camp.StructureNodes.Add(new StructureNode(node, campId, null, "Dummy leaf"));
        (await cloud.Camp.Participants.SingleAsync(ct)).AssignStructureNode(node);
        cloud.Catering.CookingUnits.Add(new CookingUnit(unit, campId, "Dummy kitchen", 0));
        cloud.Catering.CookingUnitStructureAssignments.Add(new(Guid.NewGuid(), campId, unit, null, node));
        cloud.Catering.CampMealTypes.Add(new CampMealType(type, campId, "Lunch", 0));
        cloud.Catering.CampMeals.Add(new CampMeal(meal, campId, type, new(2027, 7, 1)));
        await cloud.SaveAsync();
        Guid person = (await cloud.Camp.Participants.SingleAsync(ct)).Id;
        var service = new ScoutCampPlanner.Catering.Application.MealPlanning.MealPlanningService(
            new MealPlanningStore(cloud.Catering), cloud.Camp, TimeProvider.System, cloud.Camp);
        Assert.True((await service.SaveParticipantConfigurationAsync(campId, new(0, MealPlanningDemandMode.UseActualParticipants), ct)).IsSuccess);
        await service.CalculateAsync(campId, unit, meal, ct);
        var packages = ParticipantPackages(cloud);
        var initial = await packages.StartOfflineTransferAsync(campId, ct);
        await using var local = await DatabaseHarness.CreateAsync();
        await local.Packages.ImportInitialPackageAsync(initial, ct);
        Assert.Equal(EffectiveDemandBasis.ActualParticipants, (await local.Catering.CookingUnitMealStates.SingleAsync(ct)).DemandBasis);
        var localService = new ScoutCampPlanner.Catering.Application.MealPlanning.MealPlanningService(
            new MealPlanningStore(local.Catering), local.Camp, TimeProvider.System, local.Camp);
        var configuration = await localService.GetParticipantConfigurationAsync(campId, ct);
        Assert.Equal(node, (await local.Camp.Participants.SingleAsync(ct)).StructureNodeId);
        Assert.True((await localService.SaveParticipantConfigurationAsync(campId, new(configuration.Version, configuration.DemandMode), ct)).IsSuccess);
        (await local.Catering.CookingUnits.SingleAsync(ct)).SetParticipantFilter(CookingUnitParticipantFilter.SpecialCateringOnly);
        await local.SaveAsync();
        var returned = await ParticipantPackages(local).CreateReturnPackageAsync(campId, ct);
        var payload = CampPackageSerializer.Deserialize(returned);
        var planning = ScoutCampPlanner.Catering.Infrastructure.Offline.CampMealPlanningPackageStore.ReadPackageData(payload.CateringMealPlanningData, campId);
        var missing = payload with { CateringMealPlanningData = ScoutCampPlanner.Catering.Infrastructure.Offline.CampMealPlanningPackageStore.CreatePackageData(planning with { ParticipantConfiguration = null }) };
        await Assert.ThrowsAsync<CampPackageValidationException>(() => packages.ImportReturnPackageAsync(CampPackageSerializer.Serialize(missing), ct));
        Assert.True((await cloud.Camp.Camps.SingleAsync(ct)).IsFrozen);
        await packages.ImportReturnPackageAsync(returned, ct);
        var final = await service.GetParticipantConfigurationAsync(campId, ct);
        Assert.Equal(node, (await cloud.Camp.Participants.SingleAsync(ct)).StructureNodeId);
        Assert.Equal(CookingUnitParticipantFilter.SpecialCateringOnly, (await cloud.Catering.CookingUnits.SingleAsync(ct)).ParticipantFilter);
        Assert.Equal(2, final.Version);
    }

    [Fact]
    public async Task Participant_diet_rules_survive_import_and_conflicting_local_catalog_rolls_back()
    {
        await using var cloud = await DatabaseHarness.CreateAsync();
        Guid campId = await SeedParticipantCampAsync(cloud);
        var diet = await cloud.Catering.DietaryRequirements.SingleAsync();
        var references = await new ScoutCampPlanner.Catering.Infrastructure.Ingredients.IngredientEditorReferenceDataStore(cloud.Catering)
            .GetAsync(default);
        Guid origin = references.Origins.First(value => value.Code == "PLANT").Id;
        diet.Revise(diet.Name, "Dummy rules", 2, [new(origin, DietaryOriginDecision.Allowed)]);
        await cloud.SaveAsync();
        byte[] initial = await ParticipantPackages(cloud).StartOfflineTransferAsync(campId);
        await using var local = await DatabaseHarness.CreateAsync();
        await local.Packages.ImportInitialPackageAsync(initial);
        var imported = await local.Catering.DietaryRequirements.SingleAsync();
        Assert.Equal(diet.Version, imported.Version);
        Assert.Equal(DietaryOriginDecision.Allowed, imported.DecisionFor(origin));
        await using var conflicting = await DatabaseHarness.CreateAsync();
        conflicting.Catering.DietaryRequirements.Add(new DietaryRequirement(diet.Id, "Different local rules"));
        await conflicting.SaveAsync();
        await Assert.ThrowsAsync<CampPackageValidationException>(() => conflicting.Packages.ImportInitialPackageAsync(initial));
        Assert.Empty(await conflicting.Camp.Participants.ToArrayAsync());
        Assert.Empty(await conflicting.Camp.Camps.ToArrayAsync());
    }

    internal sealed class TestParticipantAccess(bool read = true, bool edit = true) : ICampPackageParticipantAccess
    {
        public Task DemandReadAsync(Guid campId, CancellationToken ct) => read ? Task.CompletedTask : throw new CampPackageParticipantAccessException();
        public Task DemandEditAsync(Guid campId, CancellationToken ct) => edit ? Task.CompletedTask : throw new CampPackageParticipantAccessException();
    }

    private static CampPackageService ParticipantPackages(DatabaseHarness database, bool read = true, bool edit = true) =>
        new(database.Platform, database.Camp, database.Catering, TimeProvider.System, new TestParticipantAccess(read, edit));

    private static async Task<Guid> SeedParticipantCampAsync(DatabaseHarness database)
    {
        var tenant = new Tenant(Guid.NewGuid(), "Dummy tenant");
        var camp = new Camp.Domain.Camp(Guid.NewGuid(), tenant.Id, "Dummy camp", new(2027, 7, 1), new(2027, 7, 14));
        database.Platform.Tenants.Add(tenant);
        database.Camp.Camps.Add(camp);
        database.Camp.CampStages.Add(new CampStage(Guid.NewGuid(), camp.Id, "GuSp", 0));
        var diet = new DietaryRequirement(Guid.NewGuid(), "Dummy diet");
        database.Catering.DietaryRequirements.Add(diet);
        var catalog = await new ParticipantRequirementCatalogStore(database.Catering).ReadAsync();
        var participant = new Participant(Guid.NewGuid(), camp.Id, "Dummy participant");
        participant.SetRequirements(diet.Id, [catalog.Allergens[0].Id],
            [new(catalog.Substances.First(value => value.Code == "LACTOSE").Id, 1.234567m, "Dummy threshold")]);
        participant.SetDayAbsent(new(2027, 7, 2), true);
        database.Camp.Participants.Add(participant);
        await database.SaveAsync();
        return camp.Id;
    }

    [Fact]
    public async Task Participant_transport_preserves_ids_requirements_attendance_and_never_imports_grants()
    {
        await using var cloud = await DatabaseHarness.CreateAsync();
        Guid campId = await SeedParticipantCampAsync(cloud);
        var packages = ParticipantPackages(cloud);
        byte[] initial = await packages.StartOfflineTransferAsync(campId);
        var before = Assert.Single((await cloud.Camp.GetParticipantsAsync(campId)));
        await using var local = await DatabaseHarness.CreateAsync();
        var device = new LocalDeviceIdentity(Guid.NewGuid());
        local.Platform.LocalDeviceIdentities.Add(device);
        await local.SaveAsync();
        await local.Packages.ImportInitialPackageForDeviceAsync(initial, device.Id);
        var participant = await local.Camp.Participants.SingleAsync();
        Assert.Equal(before.Id, participant.Id);
        Assert.Equal(before.DietTypeId, participant.DietTypeId);
        Assert.Equal(1.234567m, Assert.Single(participant.Intolerances).ThresholdGramsPerPortion);
        Assert.Empty(await local.Platform.CampPermissionGrants.ToArrayAsync());
        Assert.Empty(await local.Platform.LocalCampPermissionGrants.ToArrayAsync());
        await Assert.ThrowsAsync<CampPackageParticipantAccessException>(() => local.Packages.CreateReturnPackageAsync(campId));
        participant.Rename("Changed dummy");
        participant.SetDayAbsent(new(2027, 7, 3), true);
        await local.SaveAsync();
        byte[] returned = await ParticipantPackages(local).CreateReturnPackageAsync(campId);
        await packages.ImportReturnPackageAsync(returned);
        var after = Assert.Single(await cloud.Camp.GetParticipantsAsync(campId));
        Assert.Equal(before.Id, after.Id);
        Assert.Equal("Changed dummy", after.DisplayName);
        Assert.Equal(2, after.AbsentDays.Count);
        Assert.Equal(before.Intolerances, after.Intolerances);
        Assert.False((await cloud.Camp.Camps.SingleAsync()).IsFrozen);
    }

    [Fact]
    public async Task Failed_export_does_not_freeze_and_failed_import_preserves_participants_and_baseline()
    {
        await using var cloud = await DatabaseHarness.CreateAsync();
        Guid campId = await SeedParticipantCampAsync(cloud);
        await Assert.ThrowsAsync<CampPackageParticipantAccessException>(() => cloud.Packages.StartOfflineTransferAsync(campId));
        Assert.False((await cloud.Camp.Camps.SingleAsync()).IsFrozen);
        byte[] initial = await ParticipantPackages(cloud).StartOfflineTransferAsync(campId);
        var payload = CampPackageSerializer.Deserialize(initial);
        var returned = payload with { Manifest = payload.Manifest with { Direction = CampPackageDirection.LocalToCloud } };
        await Assert.ThrowsAsync<CampPackageParticipantAccessException>(() =>
            ParticipantPackages(cloud, edit: false).ImportReturnPackageAsync(CampPackageSerializer.Serialize(returned)));
        await Assert.ThrowsAsync<CampPackageValidationException>(() => ParticipantPackages(cloud).ImportReturnPackageAsync(
            CampPackageSerializer.Serialize(returned with { Participants = null })));
        Assert.Single(await cloud.Camp.Participants.ToArrayAsync());
        Assert.True((await cloud.Camp.Camps.SingleAsync()).IsFrozen);
        Assert.Equal(payload.Manifest.TransferId, (await cloud.Camp.Camps.SingleAsync()).ActiveTransferId);
    }

    [Fact]
    public async Task Participant_return_replace_rolls_back_deletion_when_reference_is_not_authoritative_in_cloud()
    {
        await using var cloud = await DatabaseHarness.CreateAsync();
        Guid campId = await SeedParticipantCampAsync(cloud);
        var packages = ParticipantPackages(cloud);
        var payload = CampPackageSerializer.Deserialize(await packages.StartOfflineTransferAsync(campId));
        var old = Assert.Single(payload.Participants!.Items);
        var unknownDiet = new ParticipantDietReference(Guid.NewGuid(), "Local-only dummy diet");
        var returned = payload with
        {
            Manifest = payload.Manifest with { Direction = CampPackageDirection.LocalToCloud },
            Participants = payload.Participants with { Items = [old with { DietTypeId = unknownDiet.Id }], DietTypes = [unknownDiet] }
        };
        await Assert.ThrowsAsync<CampPackageValidationException>(() => packages.ImportReturnPackageAsync(CampPackageSerializer.Serialize(returned)));
        var actual = Assert.Single(await cloud.Camp.GetParticipantsAsync(campId));
        Assert.Equal(old.DietTypeId, actual.DietTypeId);
        Assert.Equal(old.Intolerances, actual.Intolerances);
        Assert.True((await cloud.Camp.Camps.SingleAsync()).IsFrozen);
    }

    [Fact]
    public void Participant_payload_rejects_duplicates_foreign_camp_and_missing_development_marker()
    {
        var payload = CreatePayload();
        var participant = new ParticipantPlanningData(Guid.NewGuid(), payload.Camp.Id, "Dummy", null, [], [], [], []);
        var data = new CampParticipantPackageData(1, true, [participant], []);
        Assert.NotNull(CampPackageSerializer.Deserialize(CampPackageSerializer.Serialize(payload with { Participants = data })).Participants);
        foreach (var invalid in new[] { data with { DummyDataOnly = false }, data with { SchemaVersion = 99 },
                     data with { Items = [participant, participant] }, data with { Items = [participant with { CampId = Guid.NewGuid() }] },
                     data with { Items = [participant with { AbsentMealIds = [Guid.NewGuid()] }] } })
            Assert.Throws<CampPackageValidationException>(() => CampPackageSerializer.Serialize(payload with { Participants = invalid }));
    }
}
