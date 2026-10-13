using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ScoutCampPlanner.Api;
using ScoutCampPlanner.Api.Catering;
using ScoutCampPlanner.Catering.Application.MealPlanning;
using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Catering.Infrastructure.Recipes;
using ScoutCampPlanner.Platform.Application.Authorization;
using ScoutCampPlanner.Platform.Domain;
using ScoutCampPlanner.Platform.Infrastructure.Auditing;
using Xunit;

namespace ScoutCampPlanner.PlatformTests;

public sealed partial class CampManagementServiceTests
{
    [Theory]
    [InlineData("allowed", true, true, true, 200, 200)]
    [InlineData("edit-only", true, false, true, 200, 404)]
    [InlineData("verify-only", false, true, true, 403, 200)]
    [InlineData("neither", false, false, true, 403, 404)]
    [InlineData("health-missing", true, true, false, 404, 404)]
    [InlineData("stale", true, true, true, 409, 409)]
    [InlineData("frozen", true, true, true, 409, 409)]
    [InlineData("foreign-camp", true, true, true, 404, 404)]
    [InlineData("foreign-tenant", true, true, true, 404, 404)]
    [InlineData("foreign-unit", true, true, true, 404, 404)]
    [InlineData("anonymous", true, true, true, 401, 401)]
    public async Task Supply_HTTP_permissions_scope_and_version_matrix(string scenario, bool edit, bool verify, bool health,
        int supplyStatus, int verifyStatus)
    {
        var ct = TestContext.Current.CancellationToken;
        var (campId, _) = await ParticipantFixtureAsync(health, false);
        var membership = await platform.CampMemberships.SingleAsync(ct);
        platform.CampRoleAssignments.RemoveRange(await platform.CampRoleAssignments.ToArrayAsync(ct));
        platform.CampRoleAssignments.Add(new(membership.Id, edit ? Roles.CampEditor : Roles.CampViewer));
        if (verify) platform.CampPermissionGrants.Add(new(membership.Id, Permissions.Catering.VerifyMealPlanning));
        await platform.SaveChangesAsync(ct);
        var unit = new CookingUnit(Guid.NewGuid(), campId, "Dummy kitchen", 0);
        var meal = await catering.CampMeals.FirstAsync(value => value.CampId == campId, ct);
        catering.Add(unit); await catering.SaveChangesAsync(ct);
        var store = new MealPlanningStore(catering);
        await store.SaveParticipantConfigurationAsync(campId, 0, MealPlanningDemandMode.UseActualParticipants, "[]", ct);
        await store.ConfigureCookingUnitMealAsync(campId, unit.Id, meal.Id,
            new(MealPlanSubscriptionState.NoSupplyRequired, null, null, [], []), ct);
        var planning = new MealPlanningService(store, camps, TimeProvider.System, camps, new EfSupplySuitabilityReferences(catering));
        await planning.CalculateAsync(campId, unit.Id, meal.Id, ct);
        var state = await catering.CookingUnitMealStates.AsNoTracking().SingleAsync(ct);
        Guid requestCamp = campId;
        if (scenario.StartsWith("foreign-") && scenario != "foreign-unit")
        {
            Guid otherTenant = tenantId;
            if (scenario == "foreign-tenant") { otherTenant = Guid.NewGuid(); platform.Tenants.Add(new(otherTenant, "Foreign dummy tenant")); await platform.SaveChangesAsync(ct); }
            requestCamp = Guid.NewGuid();
            camps.Camps.Add(new(requestCamp, otherTenant, "Foreign dummy camp", new(2027, 7, 1), new(2027, 7, 3)));
            await camps.SaveChangesAsync(ct);
        }
        if (scenario == "frozen") { (await camps.Camps.SingleAsync(value => value.Id == campId, ct)).Freeze(Guid.NewGuid()); await camps.SaveChangesAsync(ct); }
        var head = await platform.AuditJournalHeads.SingleAsync(ct);
        var verification = new MealVerificationService(service, planning, platform, catering, camps,
            new AuditedOperationExecutor(platform, new FixedAuditKeyProvider()), new AuditRuntimeState(head.InstanceId),
            TimeProvider.System, new SingleDeviceRuntime(new ConfigurationBuilder().Build()));
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthentication().AddCookie(); builder.Services.AddAuthorization();
        builder.Services.AddSingleton(service); builder.Services.AddSingleton(planning); builder.Services.AddSingleton(verification);
        await using var app = builder.Build(); app.Urls.Add("http://127.0.0.1:0");
        app.UseAuthentication();
        app.Use((context, next) => {
            if (scenario != "anonymous") context.User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, otherUserId.ToString())], "isolated-test"));
            return next(context);
        });
        app.UseAuthorization(); app.MapMealSupplyEndpoints();
        await app.StartAsync(ct);
        try
        {
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new(app.Urls.Single()) };
            Guid requestUnit = scenario == "foreign-unit" ? Guid.NewGuid() : unit.Id;
            string route = $"/api/camps/{requestCamp}/cooking-units/{requestUnit}/meals/{meal.Id}";
            long version = scenario == "stale" ? state.SupplyVersion - 1 : state.SupplyVersion;
            using var supply = await client.PutAsJsonAsync(route + "/supply", new SaveSupplyRequest(version, []), ct);
            Assert.Equal((HttpStatusCode)supplyStatus, supply.StatusCode);
            using var result = await client.PostAsJsonAsync(route + "/verify", new VerifyMealRequest(version, MealVerificationKind.Clean, null), ct);
            Assert.Equal((HttpStatusCode)verifyStatus, result.StatusCode);
            Assert.Equal(verifyStatus == 200, (await catering.CookingUnitMealStates.AsNoTracking().SingleAsync(ct)).VerificationSnapshotJson is not null);
            if (scenario == "allowed")
            {
                Guid revisionId = Guid.NewGuid(); // Duplicate input is invalid independently of catalogue lookup.
                var original = await catering.CookingUnitMealStates.AsNoTracking().SingleAsync(ct);
                var choice = new SupplySolution(Guid.NewGuid(), revisionId, null, 1,
                    new Dictionary<string, RecipePositionSelection>(), [], SupplyDecisionSource.Manual);
                using var duplicate = await client.PutAsJsonAsync(route + "/supply",
                    new SaveSupplyRequest(original.SupplyVersion, [choice, choice with { Id = Guid.NewGuid() }]), ct);
                Assert.Equal(HttpStatusCode.UnprocessableEntity, duplicate.StatusCode);
                Assert.Contains("duplicate_supply_decision", await duplicate.Content.ReadAsStringAsync(ct));
                Assert.Equal(original.SupplyVersion, (await catering.CookingUnitMealStates.AsNoTracking().SingleAsync(ct)).SupplyVersion);
            }
        }
        finally { await app.StopAsync(ct); }
    }
}
