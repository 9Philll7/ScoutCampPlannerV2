using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;
using ScoutCampPlanner.Camp.Domain;
using ScoutCampPlanner.Camp.Infrastructure;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Catering.Infrastructure.Recipes;
using ScoutCampPlanner.Catering.Infrastructure.Ingredients;
using ScoutCampPlanner.Catering.Infrastructure.Offline;
using ScoutCampPlanner.Package;
using ScoutCampPlanner.Platform.Domain;
using ScoutCampPlanner.Platform.Infrastructure;
using Xunit;

namespace ScoutCampPlanner.PackageTests;

public sealed class CampPackageTests
{
    static CampPackageTests() => SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());

    [Fact]
    public async Task Removing_local_copy_preserves_other_camps_and_allows_reimport()
    {
        await using var cloud = await DatabaseHarness.CreateAsync();
        await using var local = await DatabaseHarness.CreateAsync();
        var tenant = new Tenant(Guid.NewGuid(), "Removal test");
        cloud.Platform.Tenants.Add(tenant);
        var first = new Camp.Domain.Camp(Guid.NewGuid(), tenant.Id, "First", new(2027, 7, 1), new(2027, 7, 3));
        var second = new Camp.Domain.Camp(Guid.NewGuid(), tenant.Id, "Second", new(2027, 7, 1), new(2027, 7, 3));
        cloud.Camp.Camps.AddRange(first, second);
        cloud.Camp.CampStages.AddRange(new CampStage(Guid.NewGuid(), first.Id, "GuSp", 0),
            new CampStage(Guid.NewGuid(), second.Id, "GuSp", 0));
        var parent = new StructureNode(Guid.NewGuid(), first.Id, null, "Parent");
        cloud.Camp.StructureNodes.AddRange(parent, new StructureNode(Guid.NewGuid(), first.Id, parent.Id, "Child"));
        await cloud.SaveAsync();
        var device = new LocalDeviceIdentity(Guid.NewGuid());
        local.Platform.LocalDeviceIdentities.Add(device);
        await local.SaveAsync();
        byte[] package = await cloud.Packages.StartOfflineTransferAsync(first.Id);
        await local.Packages.ImportInitialPackageForDeviceAsync(package, device.Id);
        await local.Packages.ImportInitialPackageForDeviceAsync(await cloud.Packages.StartOfflineTransferAsync(second.Id), device.Id);
        Guid transfer = first.ActiveTransferId!.Value;
        Assert.False(await local.Packages.RemoveLocalCampAsync(Guid.NewGuid(), first.Id, transfer));
        Assert.False(await local.Packages.RemoveLocalCampAsync(device.Id, first.Id, Guid.NewGuid()));
        Assert.Equal(2, await local.Camp.Camps.CountAsync());
        await local.Camp.Database.ExecuteSqlRawAsync("CREATE TRIGGER block_local_removal BEFORE DELETE ON Camps BEGIN SELECT RAISE(ABORT, 'test rollback'); END;");
        await Assert.ThrowsAsync<SqliteException>(() => local.Packages.RemoveLocalCampAsync(device.Id, first.Id, transfer));
        Assert.Equal(2, await local.Camp.StructureNodes.CountAsync());
        Assert.Equal(2, await local.Platform.LocalCampAccessGrants.CountAsync());
        await local.Camp.Database.ExecuteSqlRawAsync("DROP TRIGGER block_local_removal;");
        Assert.True(await local.Packages.RemoveLocalCampAsync(device.Id, first.Id, transfer));
        Assert.Equal(second.Id, (await local.Camp.Camps.SingleAsync()).Id);
        Assert.Equal(second.Id, (await local.Platform.LocalCampAccessGrants.SingleAsync()).CampId);
        Assert.Empty(await local.Camp.StructureNodes.ToArrayAsync());
        Assert.True(first.IsFrozen);
        await local.Packages.ImportInitialPackageForDeviceAsync(package, device.Id);
        Assert.Equal(2, await local.Camp.Camps.CountAsync());
        Assert.Equal(2, await local.Camp.StructureNodes.CountAsync());
        Assert.Equal(2, await local.Platform.LocalCampAccessGrants.CountAsync());
    }

    [Fact]
    public async Task Abandoned_transfer_return_is_rejected_even_after_a_new_transfer_starts()
    {
        await using var cloud = await DatabaseHarness.CreateAsync();
        await using var local = await DatabaseHarness.CreateAsync();
        var tenantId = Guid.NewGuid();
        var campId = Guid.NewGuid();
        cloud.Platform.Tenants.Add(new Tenant(tenantId, "Recovery"));
        var camp = new Camp.Domain.Camp(campId, tenantId, "Preserved",
            new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 3));
        cloud.Camp.Camps.Add(camp);
        cloud.Camp.CampStages.Add(new CampStage(Guid.NewGuid(), campId, "GuSp", 0));
        await cloud.SaveAsync();
        await local.Packages.ImportInitialPackageAsync(await cloud.Packages.StartOfflineTransferAsync(campId));
        byte[] abandoned = await local.Packages.CreateReturnPackageAsync(campId);
        camp.CompleteTransfer(camp.ActiveTransferId!.Value, camp.BaselineVersion);
        await cloud.SaveAsync();
        await Assert.ThrowsAsync<CampPackageValidationException>(() => cloud.Packages.ImportReturnPackageAsync(abandoned));
        await cloud.Packages.StartOfflineTransferAsync(campId);
        await Assert.ThrowsAsync<CampPackageValidationException>(() => cloud.Packages.ImportReturnPackageAsync(abandoned));
        Assert.Equal("Preserved", camp.Name);
        Assert.True(camp.IsFrozen);
    }

    [Fact]
    public async Task Repeated_transfers_preserve_source_baseline_and_allow_local_domain_changes()
    {
        await using var cloud = await DatabaseHarness.CreateAsync();
        var tenantId = Guid.NewGuid();
        var campId = Guid.NewGuid();
        cloud.Platform.Tenants.Add(new Tenant(tenantId, "Roundtrip"));
        var mealType = new CampMealType(Guid.NewGuid(), campId, "Frühstück", 0);
        cloud.Catering.CampMealTypes.Add(mealType);
        cloud.Catering.CampMeals.Add(new CampMeal(Guid.NewGuid(), campId, mealType.Id, new DateOnly(2027, 7, 1)));
        cloud.Camp.Camps.Add(new Camp.Domain.Camp(campId, tenantId, "Lager",
            new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 3)));
        cloud.Camp.CampStages.Add(new CampStage(Guid.NewGuid(), campId, "GuSp", 0));
        await cloud.SaveAsync();

        for (var cycle = 0; cycle < 2; cycle++)
        {
            byte[] outbound = await cloud.Packages.StartOfflineTransferAsync(campId);
            var manifest = CampPackageSerializer.Deserialize(outbound).Manifest;
            Assert.Equal(1 + cycle * 2, manifest.BaselineVersion);
            await Assert.ThrowsAsync<InvalidOperationException>(() => cloud.Packages.CreateReturnPackageAsync(campId));

            await using var local = await DatabaseHarness.CreateAsync();
            var deviceId = Guid.NewGuid();
            local.Platform.LocalDeviceIdentities.Add(new LocalDeviceIdentity(deviceId));
            await local.SaveAsync();
            await Assert.ThrowsAsync<CampPackageValidationException>(() =>
                local.Packages.ImportInitialPackageForDeviceAsync(outbound, Guid.NewGuid()));
            Assert.Empty(await local.Platform.LocalCampAccessGrants.ToArrayAsync());
            await local.Packages.ImportInitialPackageForDeviceAsync(outbound, deviceId);
            var grant = await local.Platform.LocalCampAccessGrants.SingleAsync();
            Assert.Equal(deviceId, grant.DeviceIdentityId);
            Assert.Equal(campId, grant.CampId);
            Assert.Equal(tenantId, grant.TenantId);
            Assert.Equal(manifest.TransferId, grant.TransferId);
            var imported = await local.Camp.Camps.SingleAsync();
            Assert.False(imported.IsFrozen);
            Assert.Equal(manifest.TransferId, imported.ActiveTransferId);
            Assert.Equal(manifest.BaselineVersion, imported.BaselineVersion);
            imported.ConfigureStructure(["Gruppe"]);
            imported.UpdateDetails($"Local change {cycle}", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 5));
            await local.SaveAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => local.Packages.StartOfflineTransferAsync(campId));

            byte[] returned = await local.Packages.CreateReturnPackageAsync(campId);
            await cloud.Packages.ImportReturnPackageAsync(returned);
            Assert.Empty(await cloud.Platform.LocalCampAccessGrants.ToArrayAsync());
            Assert.False((await cloud.Camp.Camps.SingleAsync()).IsFrozen);
            Assert.Equal($"Local change {cycle}", (await cloud.Camp.Camps.SingleAsync()).Name);
            Assert.Equal(new DateOnly(2026, 10, 5), (await cloud.Camp.Camps.SingleAsync()).EndDate);
            Assert.Equal(new DateOnly(2027, 7, 1), (await cloud.Catering.CampMeals.SingleAsync()).Date);
            await Assert.ThrowsAsync<CampPackageValidationException>(() => cloud.Packages.ImportReturnPackageAsync(returned));
        }
    }

    [Fact]
    public void Serializer_rejects_tampered_package()
    {
        var package = CreatePayload();
        var bytes = CampPackageSerializer.Serialize(package);
        byte[] tampered;
        using (var stream = new MemoryStream())
        {
            stream.Write(bytes);
            stream.Position = 0;
            {
                using var archive = new ZipArchive(stream, ZipArchiveMode.Update, leaveOpen: true);
                var entry = archive.GetEntry("payload.json")!;
                byte[] payload;
                using (var payloadStream = entry.Open())
                using (var copy = new MemoryStream())
                {
                    payloadStream.CopyTo(copy);
                    payload = copy.ToArray();
                }
                payload[0] ^= 0x01;
                entry.Delete();
                using var replacement = archive.CreateEntry("payload.json").Open();
                replacement.Write(payload);
            }
            tampered = stream.ToArray();
        }
        Assert.Throws<CampPackageValidationException>(() => CampPackageSerializer.Deserialize(tampered));
    }

    [Fact]
    public void Serializer_rejects_older_development_package_without_catering_references()
    {
        CampPackagePayload package = CreatePayload() with { CateringReferenceData = default };

        CampPackageValidationException exception = Assert.Throws<CampPackageValidationException>(
            () => CampPackageSerializer.Deserialize(CampPackageSerializer.Serialize(package)));

        Assert.Contains("Catering reference data is missing", exception.Message);
    }

    [Fact]
    public void Serializer_rejects_estimates_above_final_fixed_structure_level()
    {
        var package = CreatePayload();
        var nodeId = Guid.NewGuid();
        package = package with
        {
            Camp = package.Camp with { StructureMode = "Fixed", StructureLevelNames = ["Bereich", "Gruppe"] },
            StructureNodes = [new StructureNodeData(nodeId, package.Camp.Id, null, "Nord")],
            ParticipantEstimates = [new ParticipantEstimateData(Guid.NewGuid(), package.Camp.Id, nodeId,
                package.CampStages.Single().Id, 10, 2)]
        };

        var exception = Assert.Throws<CampPackageValidationException>(() => CampPackageSerializer.Serialize(package));
        Assert.Contains("final fixed structure level", exception.Message);
    }

    [Fact]
    public async Task Round_trip_preserves_ids_and_atomically_replaces_included_data()
    {
        await using var cloud = await DatabaseHarness.CreateAsync();
        var tenantId = Guid.NewGuid();
        var campId = Guid.NewGuid();
        var stageId = Guid.NewGuid();
        var structureNodeId = Guid.NewGuid();
        var estimateId = Guid.NewGuid();
        var mealId = Guid.NewGuid();
        var mealTypeId = Guid.NewGuid();
        var campMealId = Guid.NewGuid();
        var offerGroupId = Guid.NewGuid();
        var planEntryId = Guid.NewGuid();
        var recipeRevisionId = Guid.NewGuid();
        var unitGroupId = Guid.NewGuid();
        var cookingUnitId = Guid.NewGuid();
        var mealStateId = Guid.NewGuid();
        var snapshotId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        cloud.Platform.Tenants.Add(new Tenant(tenantId, "Stamm Nord"));
        cloud.Camp.Camps.Add(new Camp.Domain.Camp(
            campId, tenantId, "Sommerlager", new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 14)));
        cloud.Camp.CampStages.Add(new CampStage(stageId, campId, "GuSp", 0));
        cloud.Camp.StructureNodes.Add(new StructureNode(structureNodeId, campId, null, "Nord"));
        cloud.Camp.ParticipantEstimates.Add(new ParticipantEstimate(estimateId, campId, structureNodeId, stageId, 18, 4));
        cloud.Catering.MealPlans.Add(new MealPlan(mealId, campId, "Montag", version: 1));
        cloud.Catering.CampMealTypes.Add(new CampMealType(mealTypeId, campId, "Frühstück", 0));
        cloud.Catering.CampMeals.Add(new CampMeal(campMealId, campId, mealTypeId, new DateOnly(2027, 7, 1)));
        cloud.Catering.MealPlanSnapshots.Add(new MealPlanSnapshot(
            snapshotId, mealId, campId, 1, "{\"version\":1}", DateTimeOffset.UtcNow));
        cloud.Catering.MealPlanOfferGroups.Add(new MealPlanOfferGroup(
            offerGroupId, mealId, campMealId, "Frühstück", 0));
        cloud.Catering.MealPlanEntries.Add(new MealPlanEntry(
            planEntryId, offerGroupId, recipeRevisionId, true, null, MealPlanEntryRole.MainDish, null, 0));
        cloud.Catering.CookingUnitGroups.Add(new CookingUnitGroup(unitGroupId, campId, "Nord", 0));
        cloud.Catering.CookingUnits.Add(new CookingUnit(
            cookingUnitId, campId, "Küche Nord", 0, unitGroupId, mealId));
        cloud.Catering.CookingUnitStructureAssignments.Add(new CookingUnitStructureAssignment(
            Guid.NewGuid(), campId, cookingUnitId, null, structureNodeId));
        var mealState = new CookingUnitMealState(mealStateId, campId, cookingUnitId, campMealId);
        mealState.Configure(MealPlanSubscriptionState.Custom, 24m);
        mealState.ApplyCalculation(22m, null, null, null, "{}", "ABC", "[]", DateTimeOffset.UtcNow, true);
        cloud.Catering.CookingUnitMealStates.Add(mealState);
        cloud.Catering.CookingUnitMealOfferTargets.Add(new CookingUnitMealOfferTarget(
            Guid.NewGuid(), mealStateId, offerGroupId, 20m));
        cloud.Catering.CookingUnitMealRecipeChoices.Add(new CookingUnitMealRecipeChoice(
            Guid.NewGuid(), mealStateId, recipeRevisionId, offerGroupId, planEntryId, 0));
        cloud.Catering.AddRange(
            new RecipeRecord
            {
                Id = Guid.NewGuid(), ScopeType = (int)RecipeScopeType.Central, Name = "Milchreis",
                NormalizedName = "MILCHREIS", Status = (int)RecipeStatus.Active,
                RecipeType = (int)RecipeType.PortionBased, CreatedBy = actorId,
                CreatedAtUtc = DateTimeOffset.UtcNow, UpdatedBy = actorId, UpdatedAtUtc = DateTimeOffset.UtcNow,
            });
        RecipeRecord recipe = cloud.Catering.Set<RecipeRecord>().Local.Single();
        cloud.Catering.AddRange(
            new RecipeRevisionRecord
            {
                Id = recipeRevisionId, RecipeId = recipe.Id, RevisionNumber = 1,
                PublishedAtUtc = DateTimeOffset.UtcNow, PublishedBy = actorId,
                SnapshotSchemaVersion = 2,
                SnapshotJson = RecipeSnapshotBuilder.Serialize(new RecipeSnapshot(
                    2, "Milchreis", null, null, null, RecipeType.PortionBased,
                    new RecipeReferenceSnapshot(10, 1m, null, null), null, true, [], [], [], [], [])),
            },
            new CampRecipeEntryRecord
            {
                Id = Guid.NewGuid(), CampId = campId, UpstreamRecipeRevisionId = recipeRevisionId,
                CreatedBy = actorId, CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedBy = actorId, UpdatedAtUtc = DateTimeOffset.UtcNow,
            });
        await cloud.SaveAsync();

        var initialPackage = await cloud.Packages.StartOfflineTransferAsync(campId);
        var frozenCloudCamp = await cloud.Camp.Camps.SingleAsync();
        Assert.True(frozenCloudCamp.IsFrozen);

        await using var local = await DatabaseHarness.CreateAsync();
        await local.Packages.ImportInitialPackageAsync(initialPackage);
        var localMeal = await local.Catering.MealPlans.SingleAsync();
        localMeal.Rename("Montag offline geändert");
        (await local.Catering.CampMeals.SingleAsync()).SetActive(false);
        await local.Catering.SaveChangesAsync();

        var returnPackage = await local.Packages.CreateReturnPackageAsync(campId);
        await cloud.Packages.ImportReturnPackageAsync(returnPackage);

        var importedNode = await cloud.Camp.StructureNodes.SingleAsync();
        var importedMeal = await cloud.Catering.MealPlans.SingleAsync();
        var completedCamp = await cloud.Camp.Camps.SingleAsync();
        Assert.Equal(structureNodeId, importedNode.Id);
        var importedEstimate = await cloud.Camp.ParticipantEstimates.SingleAsync();
        Assert.Equal(estimateId, importedEstimate.Id);
        Assert.Equal(18, importedEstimate.ChildYouthCount);
        Assert.Equal(mealId, importedMeal.Id);
        Assert.Equal("Montag offline geändert", importedMeal.Name);
        Assert.Equal(1, importedMeal.Version);
        Assert.Equal(snapshotId, (await cloud.Catering.MealPlanSnapshots.SingleAsync()).Id);
        Assert.Equal(cookingUnitId, (await cloud.Catering.CookingUnits.SingleAsync()).Id);
        CookingUnitMealState importedState = await cloud.Catering.CookingUnitMealStates.SingleAsync();
        Assert.Equal(MealPlanSubscriptionState.Custom, importedState.SubscriptionState);
        Assert.Equal(24m, importedState.DemandOverride);
        Assert.Single(await cloud.Catering.CookingUnitStructureAssignments.ToArrayAsync());
        Assert.Single(await cloud.Catering.CookingUnitMealOfferTargets.ToArrayAsync());
        Assert.Single(await cloud.Catering.CookingUnitMealRecipeChoices.ToArrayAsync());
        Assert.False((await cloud.Catering.CampMeals.SingleAsync()).IsActive);
        Assert.Equal(new DateOnly(2027, 7, 1), completedCamp.StartDate);
        Assert.Equal(new DateOnly(2027, 7, 14), completedCamp.EndDate);
        Assert.False(completedCamp.IsFrozen);
    }

    [Fact]
    public async Task Initial_import_includes_transitive_recipe_ingredient_and_nutrition_references()
    {
        await using var cloud = await DatabaseHarness.CreateAsync();
        Guid tenantId = Guid.NewGuid();
        Guid campId = Guid.NewGuid();
        Guid ingredientId = Guid.NewGuid();
        Guid ingredientRevisionId = Guid.NewGuid();
        Guid variantId = Guid.NewGuid();
        Guid unitId = Guid.NewGuid();
        Guid recipeId = Guid.NewGuid();
        Guid recipeRevisionId = Guid.NewGuid();
        Guid entryId = Guid.NewGuid();
        Guid userId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 9, 14, 12, 0, 0, TimeSpan.Zero);
        Guid categoryId = Guid.Parse("51111111-1111-1111-1111-000000000002");

        cloud.Platform.Tenants.Add(new Tenant(tenantId, "Stamm Nord"));
        cloud.Camp.Camps.Add(new Camp.Domain.Camp(campId, tenantId, "Sommerlager",
            new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 14)));
        cloud.Camp.CampStages.Add(new CampStage(Guid.NewGuid(), campId, "GuSp", 0));
        cloud.Catering.AddRange(
            new MeasurementUnit(unitId, "Testgramm", "tg", MeasurementDimension.Mass, 1m),
            new IngredientIdentityRecord
            {
                Id = ingredientId, ScopeType = (int)IngredientScopeType.Central,
                Status = (int)IngredientIdentityStatus.Active,
            },
            new IngredientRevisionRecord
            {
                Id = ingredientRevisionId, IngredientId = ingredientId, RevisionNumber = 1,
                State = (int)IngredientRevisionState.Published, Name = "Haferflocken",
                NormalizedName = "HAFERFLOCKEN", CategoryId = categoryId, BaseUnitId = unitId,
                AllergenReviewState = (int)IngredientPropertyReviewState.Reviewed,
                IntoleranceReviewState = (int)IngredientPropertyReviewState.Reviewed,
                OriginReviewState = (int)IngredientPropertyReviewState.Reviewed,
                SourceSummary = "BLS 4.0, Testquelle",
                RowVersion = 1, CreatedAtUtc = now, CreatedBy = userId, UpdatedAtUtc = now,
                UpdatedBy = userId, PublishedAtUtc = now, PublishedBy = userId,
            },
            new IngredientRevisionNutritionProfileRecord
            {
                IngredientRevisionId = ingredientRevisionId, ReferenceQuantity = 100m,
                ReferenceUnitId = unitId, EnergyKilojoules = 1_500m, FatGrams = 7m,
                SaturatedFatGrams = 1.2m, CarbohydrateGrams = 60m, SugarsGrams = 1m,
                ProteinGrams = 13m, SaltGrams = 0.02m, FiberGrams = 10m,
                SourceType = (int)IngredientNutritionSourceType.OfficialDatabase,
                SourceReference = "Testquelle", ReviewState = (int)IngredientNutritionReviewState.Reviewed,
                ReferenceDate = new DateOnly(2026, 9, 14),
            },
            new IngredientRevisionUnitConversionRecord
            {
                IngredientRevisionId = ingredientRevisionId, SourceUnitId = unitId,
                FactorToBaseUnit = 1m, Precision = 0,
            },
            new IngredientVariantRevisionRecord
            {
                Id = variantId, IngredientRevisionId = ingredientRevisionId, VariantKey = "GLUTENFREI",
                Name = "Glutenfrei", NormalizedName = "GLUTENFREI", Status = 0, SortOrder = 0,
            });

        var unit = new MeasurementUnitSnapshot(unitId, "Testgramm", "tg", MeasurementDimension.Mass, 1m);
        var nutrition = new IngredientNutritionSnapshot(100m, unit, 100m, 1_500m, 7m, 1.2m,
            60m, 1m, 13m, 0.02m, 10m, IngredientNutritionSourceType.OfficialDatabase,
            "Testquelle", IngredientNutritionReviewState.Reviewed, new DateOnly(2026, 9, 14));
        var ingredient = new IngredientSnapshotSource(ingredientRevisionId, "Haferflocken", [], nutrition);
        var snapshot = new RecipeSnapshot(RecipeSnapshotBuilder.CurrentSchemaVersion, "Porridge", null, null,
            null, RecipeType.PortionBased, new RecipeReferenceSnapshot(10m, 1m, null, null), null, true,
            [], [], [new IngredientPositionSnapshot(Guid.NewGuid(), null, 0, ingredient, 1_000m,
                new IngredientUnitSnapshot(unit, 1m), ScalingMode.Linear, AgeGroupScalingMode.Inherit, null, [])], [], []);
        cloud.Catering.AddRange(
            new RecipeRecord
            {
                Id = recipeId, ScopeType = (int)RecipeScopeType.Central, Name = "Porridge",
                NormalizedName = "PORRIDGE", Status = (int)RecipeStatus.Active,
                RecipeType = (int)RecipeType.PortionBased, ReferenceServings = 10m,
                DefaultAgeGroupScalingApplies = true, CreatedBy = userId, CreatedAtUtc = now,
                UpdatedBy = userId, UpdatedAtUtc = now,
            },
            new RecipeRevisionRecord
            {
                Id = recipeRevisionId, RecipeId = recipeId, RevisionNumber = 1,
                PublishedAtUtc = now, PublishedBy = userId,
                SnapshotSchemaVersion = RecipeSnapshotBuilder.CurrentSchemaVersion,
                SnapshotJson = RecipeSnapshotBuilder.Serialize(snapshot),
            },
            new CampRecipeEntryRecord
            {
                Id = entryId, CampId = campId, UpstreamRecipeRevisionId = recipeRevisionId,
                CreatedBy = userId, CreatedAtUtc = now, UpdatedBy = userId, UpdatedAtUtc = now,
            });
        await cloud.SaveAsync();
        IngredientIdentityRecord cloudIdentity = await cloud.Catering.Set<IngredientIdentityRecord>()
            .SingleAsync(value => value.Id == ingredientId);
        cloudIdentity.CurrentPublishedRevisionId = ingredientRevisionId;
        await cloud.Catering.SaveChangesAsync();

        byte[] package = await cloud.Packages.StartOfflineTransferAsync(campId);
        await using var local = await DatabaseHarness.CreateAsync();
        await local.Packages.ImportInitialPackageAsync(package);

        Assert.Equal(recipeRevisionId, (await local.Catering.Set<CampRecipeEntryRecord>().SingleAsync()).UpstreamRecipeRevisionId);
        Assert.Equal("Porridge", (await local.Catering.Set<RecipeRevisionRecord>().SingleAsync()).SnapshotJson is { } json
            ? RecipeSnapshotBuilder.Deserialize(json).Name : null);
        IngredientRevisionRecord importedIngredient = await local.Catering.Set<IngredientRevisionRecord>().SingleAsync();
        Assert.Equal(ingredientRevisionId, importedIngredient.Id);
        Assert.Equal("BLS 4.0, Testquelle", importedIngredient.SourceSummary);
        Assert.Equal(variantId, (await local.Catering.Set<IngredientVariantRevisionRecord>().SingleAsync()).Id);
        IngredientRevisionNutritionProfileRecord importedNutrition = await local.Catering
            .Set<IngredientRevisionNutritionProfileRecord>().SingleAsync();
        Assert.Equal(1_500m, importedNutrition.EnergyKilojoules);
        Assert.Equal("Testquelle", importedNutrition.SourceReference);
        Assert.True(await local.Catering.Set<IngredientRevisionUnitConversionRecord>().AnyAsync());
    }

    [Fact]
    public async Task Stale_return_package_is_rejected_without_changing_cloud_data()
    {
        await using var cloud = await DatabaseHarness.CreateAsync();
        var tenantId = Guid.NewGuid();
        var campId = Guid.NewGuid();
        cloud.Platform.Tenants.Add(new Tenant(tenantId, "Stamm Süd"));
        cloud.Camp.Camps.Add(new Camp.Domain.Camp(
            campId, tenantId, "Pfingstlager", new DateOnly(2027, 5, 14), new DateOnly(2027, 5, 17)));
        cloud.Camp.CampStages.Add(new CampStage(Guid.NewGuid(), campId, "GuSp", 0));
        cloud.Catering.MealPlans.Add(new MealPlan(Guid.NewGuid(), campId, "Original"));
        await cloud.SaveAsync();
        var initial = await cloud.Packages.StartOfflineTransferAsync(campId);

        await using var local = await DatabaseHarness.CreateAsync();
        await local.Packages.ImportInitialPackageAsync(initial);
        var returned = await local.Packages.CreateReturnPackageAsync(campId);
        await cloud.Packages.ImportReturnPackageAsync(returned);

        await Assert.ThrowsAsync<CampPackageValidationException>(() => cloud.Packages.ImportReturnPackageAsync(returned));
        Assert.Equal("Original", (await cloud.Catering.MealPlans.SingleAsync()).Name);
    }

    private static CampPackagePayload CreatePayload()
    {
        var tenantId = Guid.NewGuid();
        var campId = Guid.NewGuid();
        var stageId = Guid.NewGuid();
        return new CampPackagePayload(
            new CampPackageManifest(1, tenantId, campId, Guid.NewGuid(), 1, CampPackageDirection.CloudToLocal,
                ["Camp", "Catering"], DateTimeOffset.UtcNow),
            new TenantData(tenantId, "Tenant"), new CampData(
                campId, tenantId, "Camp", new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 14), "Free", []),
            [new CampStageData(stageId, campId, "GuSp", 0)], [],
            [new CampStageFoodFactorData(Guid.NewGuid(), campId, stageId, "GuSp", 1m)], [],
            CateringReferenceData: CampOfflineReferenceStore.CreateEmptyPackageData(),
            CateringMealPlanningData: CampMealPlanningPackageStore.CreateEmptyPackageData(campId));
    }

    private sealed class DatabaseHarness : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        public PlatformDbContext Platform { get; }
        public CampDbContext Camp { get; }
        public CateringDbContext Catering { get; }
        public CampPackageService Packages { get; }

        private DatabaseHarness(SqliteConnection connection, PlatformDbContext platform, CampDbContext camp, CateringDbContext catering)
        {
            this.connection = connection;
            Platform = platform;
            Camp = camp;
            Catering = catering;
            Packages = new CampPackageService(platform, camp, catering, TimeProvider.System);
        }

        public static async Task<DatabaseHarness> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var platform = new PlatformDbContext(new DbContextOptionsBuilder<PlatformDbContext>().UseSqlite(connection).Options);
            var camp = new CampDbContext(new DbContextOptionsBuilder<CampDbContext>().UseSqlite(connection).Options);
            var catering = new CateringDbContext(new DbContextOptionsBuilder<CateringDbContext>().UseSqlite(connection).Options);
            await platform.Database.ExecuteSqlRawAsync(platform.Database.GenerateCreateScript());
            await camp.Database.ExecuteSqlRawAsync(camp.Database.GenerateCreateScript());
            await catering.Database.ExecuteSqlRawAsync(catering.Database.GenerateCreateScript());
            return new DatabaseHarness(connection, platform, camp, catering);
        }

        public async Task SaveAsync()
        {
            await Platform.SaveChangesAsync();
            await Camp.SaveChangesAsync();
            await Catering.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync()
        {
            await Platform.DisposeAsync();
            await Camp.DisposeAsync();
            await Catering.DisposeAsync();
            await connection.DisposeAsync();
        }
    }
}
