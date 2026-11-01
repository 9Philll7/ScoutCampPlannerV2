using System.Net;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScoutCampPlanner.Api.Catering;
using ScoutCampPlanner.Catering.Application.MealPlanning;
using ScoutCampPlanner.Platform.Application.Authorization;
using ScoutCampPlanner.Platform.Domain;
using Xunit;

namespace ScoutCampPlanner.PlatformTests;

public sealed partial class CampManagementServiceTests
{
    [Theory]
    [InlineData("viewer", 200)]
    [InlineData("frozen", 200)]
    [InlineData("foreign-camp", 404)]
    [InlineData("missing-revision", 404)]
    [InlineData("anonymous", 401)]
    public async Task Recipe_hints_are_read_only_recipe_data_with_camp_authorization(string scenario, int status)
    {
        var ct = TestContext.Current.CancellationToken;
        var (campId, _) = await ParticipantFixtureAsync(false, false);
        var membership = await platform.CampMemberships.SingleAsync(ct);
        platform.CampRoleAssignments.RemoveRange(await platform.CampRoleAssignments.ToArrayAsync(ct));
        platform.CampRoleAssignments.Add(new(membership.Id, Roles.CampViewer));
        await platform.SaveChangesAsync(ct);
        if (scenario == "frozen") { (await camps.Camps.SingleAsync(value => value.Id == campId, ct)).Freeze(Guid.NewGuid()); await camps.SaveChangesAsync(ct); }
        var reader = new HintReaderProbe();
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthentication().AddCookie(options => {
            options.Events.OnRedirectToLogin = context => { context.Response.StatusCode = 401; return Task.CompletedTask; };
        }); builder.Services.AddAuthorization();
        builder.Services.AddSingleton(service); builder.Services.AddSingleton<IRecipePlanningHintsReader>(reader);
        await using var app = builder.Build(); app.Urls.Add("http://127.0.0.1:0");
        app.UseAuthentication();
        app.Use((context, next) => {
            if (scenario != "anonymous") context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, otherUserId.ToString())], "isolated-test"));
            return next(context);
        });
        app.UseAuthorization(); app.MapRecipePlanningHintsEndpoints(); await app.StartAsync(ct);
        try
        {
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new(app.Urls.Single()) };
            var requestedCamp = scenario == "foreign-camp" ? Guid.NewGuid() : campId;
            var revision = scenario == "missing-revision" ? Guid.NewGuid() : reader.Revision;
            using var result = await client.GetAsync($"/api/camps/{requestedCamp}/meal-planning/recipes/{revision}/hints", ct);
            Assert.Equal((HttpStatusCode)status, result.StatusCode);
            Assert.Equal(scenario is "anonymous" or "foreign-camp" ? 0 : 1, reader.Calls);
            if (status == 200) Assert.Contains("no-store", result.Headers.CacheControl!.ToString());
        }
        finally { await app.StopAsync(ct); }
    }

    private sealed class HintReaderProbe : IRecipePlanningHintsReader
    {
        public Guid Revision { get; } = Guid.NewGuid();
        public int Calls { get; private set; }
        public Task<RecipePlanningHints?> ReadAsync(Guid campId, Guid tenantId, Guid revision, CancellationToken ct)
        {
            Calls++;
            return Task.FromResult<RecipePlanningHints?>(revision == Revision ? new(revision, "PublishedStandard", DateTimeOffset.UnixEpoch, true, []) : null);
        }
    }
}
