using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Camp.Domain;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Package;
using ScoutCampPlanner.Platform.Domain;
using Xunit;

namespace ScoutCampPlanner.PackageTests;

public sealed partial class CampPackageTests
{
    [Fact]
    public async Task Additional_meal_schedule_survives_cloud_local_return_including_outside_period()
    {
        var ct = TestContext.Current.CancellationToken;
        await using var cloud = await DatabaseHarness.CreateAsync();
        await using var local = await DatabaseHarness.CreateAsync();
        var tenant = new Tenant(Guid.NewGuid(), "Schedule"); cloud.Platform.Add(tenant);
        var camp = new Camp.Domain.Camp(Guid.NewGuid(), tenant.Id, "Schedule", new(2027, 7, 2), new(2027, 7, 3)); cloud.Camp.Add(camp);
        cloud.Camp.Add(new CampStage(Guid.NewGuid(), camp.Id, "GuSp", 0));
        var regular = new CampMealType(Guid.NewGuid(), camp.Id, "Lunch", 0);
        var additional = new CampMealType(Guid.NewGuid(), camp.Id, "Snack", 1, CampMealKind.Additional);
        var meal = new CampMeal(Guid.NewGuid(), camp.Id, additional.Id, new(2027, 7, 1), false, 3, 7);
        cloud.Catering.AddRange(regular, additional, meal, new CampMealScheduleState(camp.Id, 12));
        await cloud.SaveAsync();
        byte[] bytes = await cloud.Packages.StartOfflineTransferAsync(camp.Id);
        var package = CampPackageSerializer.Deserialize(bytes);
        Assert.Equal(2, package.Manifest.FormatVersion);
        Assert.Equal(12, package.MealScheduleVersion);
        await local.Packages.ImportInitialPackageAsync(bytes);
        var imported = await local.Catering.CampMeals.SingleAsync(ct);
        Assert.Equal(meal.Id, imported.Id); Assert.Equal(7, imported.SortOrder);
        Assert.False(imported.IsActive); Assert.Equal(3, imported.ChangeVersion);
        Assert.Equal(CampMealKind.Additional, (await local.Catering.CampMealTypes.SingleAsync(x => x.Id == additional.Id, ct)).Kind);
        var state = await local.Catering.CampMealScheduleStates.SingleAsync(ct); state.Advance();
        imported.SetSortOrder(2); await local.SaveAsync();
        await cloud.Packages.ImportReturnPackageAsync(await local.Packages.CreateReturnPackageAsync(camp.Id));
        cloud.Catering.ChangeTracker.Clear();
        Assert.Equal(13, await cloud.Catering.CampMealScheduleStates.Select(x => x.Version).SingleAsync(ct));
        Assert.Equal(2, await cloud.Catering.CampMeals.Select(x => x.SortOrder).SingleAsync(ct));

        // A format-1 payload is explicitly upgraded using type order, not zero defaults.
        var legacy = package with { Manifest = package.Manifest with { FormatVersion = 1 }, MealScheduleVersion = 0,
            CampMealTypes = package.CampMealTypes!.Select(x => x with { Kind = 0 }).ToArray(),
            CampMeals = package.CampMeals!.Select(x => x with { SortOrder = 0 }).ToArray() };
        var upgraded = CampPackageSerializer.Deserialize(CampPackageSerializer.Serialize(legacy));
        Assert.Equal(2, upgraded.Manifest.FormatVersion);
        Assert.All(upgraded.CampMealTypes!, type => Assert.Equal(0, type.Kind));
        Assert.Equal(0, Assert.Single(upgraded.CampMeals!).SortOrder);
        Assert.Throws<CampPackageValidationException>(() => CampPackageSerializer.Serialize(package with {
            Manifest = package.Manifest with { FormatVersion = 1 } }));
    }
}
