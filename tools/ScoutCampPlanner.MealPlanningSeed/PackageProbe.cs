using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Camp.Infrastructure;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Catering.Infrastructure.Recipes;
using ScoutCampPlanner.Catering.Application.MealPlanning;
using ScoutCampPlanner.Migrations.Sqlite;
using ScoutCampPlanner.Package;
using ScoutCampPlanner.Platform.Infrastructure;

namespace ScoutCampPlanner.MealPlanningSeed;

internal static class PackageProbe
{
    internal static async Task RunAsync(PlatformDbContext platform, CampDbContext camp, CateringDbContext catering, string localFile)
    {
        var access = new DummyAccess();
        var cloud = new CampPackageService(platform, camp, catering, TimeProvider.System, access);
        Guid campId = AcceptanceSeed.Id("camp");
        byte[] outbound = await cloud.StartOfflineTransferAsync(campId);
        await File.WriteAllBytesAsync(localFile + ".scoutcamp", outbound);
        Console.WriteLine("PASS fresh outbound package");
        await using var connection = new SqliteConnection($"Data Source={localFile};Pooling=False");
        await connection.OpenAsync();
        DbContextOptions<T> Options<T>(string module) where T : DbContext => new DbContextOptionsBuilder<T>()
            .UseSqlite(connection, sqlite => sqlite.MigrationsAssembly(typeof(SqliteMigrationsAssembly).Assembly.FullName)
                .MigrationsHistoryTable("__EFMigrationsHistory_" + module)).Options;
        await using var localPlatform = new PlatformDbContext(Options<PlatformDbContext>("platform"));
        await using var localCamp = new CampDbContext(Options<CampDbContext>("camp"));
        await using var localCatering = new CateringDbContext(Options<CateringDbContext>("catering"));
        await localPlatform.Database.MigrateAsync(); await localCamp.Database.MigrateAsync(); await localCatering.Database.MigrateAsync();
        var local = new CampPackageService(localPlatform, localCamp, localCatering, TimeProvider.System, access);
        await local.ImportInitialPackageAsync(outbound);
        Console.WriteLine("PASS import into fresh local DB");
        if (await localPlatform.CampPermissionGrants.AnyAsync()) throw new InvalidOperationException("Permissions imported!");
        var service = new MealPlanningService(new MealPlanningStore(localCatering), localCamp, TimeProvider.System, localCamp, new EfSupplySuitabilityReferences(localCatering));
        var mixed = (await service.GetOverviewAsync(campId))!.OperationalMeals.Single(x =>
            x.CookingUnitId == AcceptanceSeed.Id("comparison-unit1") && x.CampMealId == AcceptanceSeed.Id("meal0-1"));
        var chosen = mixed.Supply!.CommonSupplyChoices!.Single().SeparateSolutions.Single(x => x.PositionSelections.Count > 0) with { Source = SupplyDecisionSource.Manual };
        Ensure(await service.SaveSupplyAsync(campId, mixed.CookingUnitId, mixed.CampMealId, new(mixed.SupplyVersion, [chosen])));
        localCatering.ChangeTracker.Clear();
        Ensure(await service.CalculateAsync(campId, mixed.CookingUnitId, mixed.CampMealId));
        localCatering.ChangeTracker.Clear();
        mixed = (await service.GetOverviewAsync(campId))!.OperationalMeals.Single(x => x.Id == mixed.Id);
        if (mixed.Supply!.Solutions.Count(x => x.Source == SupplyDecisionSource.Manual) != 1 ||
            mixed.Supply.Solutions.Count(x => x.Source == SupplyDecisionSource.Automatic) != 1)
            throw new InvalidOperationException("Mixed provenance lost on persistence/reload/recalculation");
        Console.WriteLine("PASS mixed Auto/Manual provenance after persisted save, reload and recalculation");
        Ensure(await service.SaveSupplyAsync(campId, mixed.CookingUnitId, mixed.CampMealId, new(mixed.SupplyVersion, [])));
        Ensure(await service.CalculateAsync(campId, mixed.CookingUnitId, mixed.CampMealId));
        foreach (bool together in new[] { true, false, true })
        {
            mixed = (await service.GetOverviewAsync(campId))!.OperationalMeals.Single(x => x.Id == mixed.Id);
            var choice = mixed.Supply!.CommonSupplyChoices!.Single();
            var selected = (together ? choice.TogetherOptions.Take(1) : choice.SeparateSolutions)
                .Select(value => value with { Source = SupplyDecisionSource.Manual }).ToArray();
            Ensure(await service.SaveSupplyAsync(campId, mixed.CookingUnitId, mixed.CampMealId, new(mixed.SupplyVersion, selected)));
            localCatering.ChangeTracker.Clear();
            Ensure(await service.CalculateAsync(campId, mixed.CookingUnitId, mixed.CampMealId));
            localCatering.ChangeTracker.Clear();
            mixed = (await service.GetOverviewAsync(campId))!.OperationalMeals.Single(x => x.Id == mixed.Id);
            if (mixed.Supply!.Solutions.Count != (together ? 1 : 2) || mixed.Supply.Solutions.Any(x => x.Source != SupplyDecisionSource.Manual) ||
                mixed.Supply.Solutions.Sum(x => x.Portions) != 4 || mixed.Supply.Problems.Count != 0)
                throw new InvalidOperationException("Ask selection switch/reload/recalculation failed");
        }
        var expectedAsk = await localCatering.CookingUnitMealStates.AsNoTracking().SingleAsync(x => x.Id == mixed.Id);
        Console.WriteLine("PASS Ask together/separate/together after persisted save, reload and recalculation, without duplicate coverage");
        Guid unit = AcceptanceSeed.Id("unit0"), meal = AcceptanceSeed.Id("meal0-0");
        var state = (await service.GetOverviewAsync(campId))!.OperationalMeals.Single(x => x.CookingUnitId == unit && x.CampMealId == meal);
        var solution = state.Supply!.Solutions.First() with { Source = SupplyDecisionSource.Manual };
        Ensure(await service.SaveSupplyAsync(campId, unit, meal, new(state.SupplyVersion, [solution])));
        Ensure(await service.CalculateAsync(campId, unit, meal));
        state = (await service.GetOverviewAsync(campId))!.OperationalMeals.Single(x => x.CookingUnitId == unit && x.CampMealId == meal);
        Ensure(await service.VerifySupplyAsync(campId, unit, meal, AcceptanceSeed.Id("actor"),
            new(state.SupplyVersion, MealVerificationKind.Clean, null)));
        var expected = await localCatering.CookingUnitMealStates.AsNoTracking().SingleAsync(x => x.CookingUnitId == unit && x.CampMealId == meal);
        await cloud.ImportReturnPackageAsync(await local.CreateReturnPackageAsync(campId));
        var actual = await catering.CookingUnitMealStates.AsNoTracking().SingleAsync(x => x.CookingUnitId == unit && x.CampMealId == meal);
        if (actual.ManualSupplySolutionsJson != expected.ManualSupplySolutionsJson || actual.VerificationSnapshotJson != expected.VerificationSnapshotJson)
            throw new InvalidOperationException("Supply/verification roundtrip mismatch");
        var actualAsk = await catering.CookingUnitMealStates.AsNoTracking().SingleAsync(x => x.Id == mixed.Id);
        if (actualAsk.ManualSupplySolutionsJson != expectedAsk.ManualSupplySolutionsJson || actualAsk.CalculationSnapshotJson != expectedAsk.CalculationSnapshotJson)
            throw new InvalidOperationException("Ask decision/options roundtrip mismatch");
        Console.WriteLine("PASS local manual supply, verification, return, cloud Replace, no imported rights");
    }
    private static void Ensure(MealPlanningMutationResult result) { if (!result.IsSuccess) throw new InvalidOperationException(result.ToString()); }
    private sealed class DummyAccess : ICampPackageParticipantAccess
    {
        public Task DemandReadAsync(Guid campId, CancellationToken ct) => Task.CompletedTask;
        public Task DemandEditAsync(Guid campId, CancellationToken ct) => Task.CompletedTask;
    }
}
