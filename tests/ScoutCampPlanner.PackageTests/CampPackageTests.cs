using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;
using ScoutCampPlanner.Camp.Domain;
using ScoutCampPlanner.Camp.Infrastructure;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure;
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
    public async Task Round_trip_preserves_ids_and_atomically_replaces_included_data()
    {
        await using var cloud = await DatabaseHarness.CreateAsync();
        var tenantId = Guid.NewGuid();
        var campId = Guid.NewGuid();
        var structureNodeId = Guid.NewGuid();
        var stageId = Guid.NewGuid();
        var estimateId = Guid.NewGuid();
        var mealId = Guid.NewGuid();
        cloud.Platform.Tenants.Add(new Tenant(tenantId, "Stamm Nord"));
        cloud.Camp.Camps.Add(new Camp.Domain.Camp(
            campId, tenantId, "Sommerlager", new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 14)));
        cloud.Camp.CampStages.Add(new CampStage(stageId, campId, "GuSp", 0));
        cloud.Camp.StructureNodes.Add(new StructureNode(structureNodeId, campId, null, "Nord"));
        cloud.Camp.ParticipantEstimates.Add(new ParticipantEstimate(estimateId, campId, structureNodeId, stageId, 18, 4));
        cloud.Catering.MealPlans.Add(new MealPlan(mealId, campId, "Montag"));
        await cloud.SaveAsync();

        var initialPackage = await cloud.Packages.StartOfflineTransferAsync(campId);
        var frozenCloudCamp = await cloud.Camp.Camps.SingleAsync();
        Assert.True(frozenCloudCamp.IsFrozen);

        await using var local = await DatabaseHarness.CreateAsync();
        await local.Packages.ImportInitialPackageAsync(initialPackage);
        var localMeal = await local.Catering.MealPlans.SingleAsync();
        localMeal.Rename("Montag offline geändert");
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
        Assert.Equal(new DateOnly(2027, 7, 1), completedCamp.StartDate);
        Assert.Equal(new DateOnly(2027, 7, 14), completedCamp.EndDate);
        Assert.False(completedCamp.IsFrozen);
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
        return new CampPackagePayload(
            new CampPackageManifest(1, tenantId, campId, Guid.NewGuid(), 1, CampPackageDirection.CloudToLocal,
                ["Camp", "Catering"], DateTimeOffset.UtcNow),
            new TenantData(tenantId, "Tenant"), new CampData(
                campId, tenantId, "Camp", new DateOnly(2027, 7, 1), new DateOnly(2027, 7, 14), "Free", []),
            [new CampStageData(Guid.NewGuid(), campId, "GuSp", 0)], [], [], []);
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
