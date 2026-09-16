using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;
using ScoutCampPlanner.Camp.Domain;
using ScoutCampPlanner.Camp.Infrastructure;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Catering.Infrastructure.Ingredients;
using ScoutCampPlanner.Catering.Infrastructure.Offline;
using ScoutCampPlanner.Catering.Infrastructure.Recipes;
using ScoutCampPlanner.Package;
using ScoutCampPlanner.Platform.Domain;
using ScoutCampPlanner.Platform.Infrastructure;
using Xunit;

namespace ScoutCampPlanner.PackageTests;

public sealed class CampPackageTests
{
    static CampPackageTests() => SQLitePCL.raw.SetProvider(new SQLitePCL.SQLite3Provider_winsqlite3());

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
        cloud.Platform.Tenants.Add(new Tenant(tenantId, "Stamm Nord"));
        cloud.Camp.Camps.Add(new Camp.Domain.Camp(
            campId, tenantId, "Sommerlager", new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 14)));
        cloud.Camp.CampStages.Add(new CampStage(stageId, campId, "GuSp", 0));
        cloud.Camp.StructureNodes.Add(new StructureNode(structureNodeId, campId, null, "Nord"));
        cloud.Camp.ParticipantEstimates.Add(new ParticipantEstimate(estimateId, campId, structureNodeId, stageId, 18, 4));
        cloud.Catering.MealPlans.Add(new MealPlan(mealId, campId, "Montag"));
        cloud.Catering.CampMealTypes.Add(new CampMealType(mealTypeId, campId, "Frühstück", 0));
        cloud.Catering.CampMeals.Add(new CampMeal(campMealId, campId, mealTypeId, new DateOnly(2027, 7, 1)));
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
            [new CampStageFoodFactorData(Guid.NewGuid(), campId, stageId, "GuSp", 1m)], [], [],
            CateringReferenceData: CampOfflineReferenceStore.CreateEmptyPackageData());
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
