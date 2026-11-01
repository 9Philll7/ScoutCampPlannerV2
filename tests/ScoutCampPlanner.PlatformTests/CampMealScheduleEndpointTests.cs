using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScoutCampPlanner.Api.Catering;
using ScoutCampPlanner.Platform.Application.Authorization;
using ScoutCampPlanner.Platform.Domain;
using Xunit;

namespace ScoutCampPlanner.PlatformTests;

public sealed partial class CampManagementServiceTests
{
    [Theory]
    [InlineData("editor", "add", 201)]
    [InlineData("editor", "order", 200)]
    [InlineData("editor", "delete", 200)]
    [InlineData("viewer", "add", 404)]
    [InlineData("viewer", "order", 404)]
    [InlineData("viewer", "delete", 404)]
    [InlineData("foreign", "add", 404)]
    [InlineData("frozen", "add", 409)]
    [InlineData("frozen", "order", 409)]
    [InlineData("frozen", "delete", 409)]
    [InlineData("anonymous", "add", 401)]
    public async Task Schedule_endpoints_enforce_camp_edit_and_freeze(string scenario, string operation, int status)
    {
        var ct = TestContext.Current.CancellationToken;
        var id = await ScheduleCampAsync();
        var snack = (await AddSnackAsync(id)).Meal!;
        if (scenario == "viewer")
        {
            var membership = await platform.CampMemberships.SingleAsync(ct);
            platform.CampRoleAssignments.RemoveRange(await platform.CampRoleAssignments.ToArrayAsync(ct));
            platform.CampRoleAssignments.Add(new(membership.Id, Roles.CampViewer)); await platform.SaveChangesAsync(ct);
        }
        if (scenario == "frozen") { (await camps.Camps.SingleAsync(ct)).Freeze(Guid.NewGuid()); await camps.SaveChangesAsync(ct); }
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthentication().AddCookie(options => options.Events.OnRedirectToLogin = context => {
            context.Response.StatusCode = 401; return Task.CompletedTask;
        });
        builder.Services.AddAuthorization(); builder.Services.AddSingleton(service); builder.Services.AddSingleton(cateringService);
        await using var app = builder.Build(); app.Urls.Add("http://127.0.0.1:0");
        app.UseAuthentication();
        app.Use((context, next) => {
            if (scenario != "anonymous") context.User = new ClaimsPrincipal(new ClaimsIdentity([
                new Claim(ClaimTypes.NameIdentifier, otherUserId.ToString())], "isolated-test"));
            return next(context);
        });
        app.UseAuthorization(); app.MapMealScheduleEndpoints(); await app.StartAsync(ct);
        try
        {
            using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = new(app.Urls.Single()) };
            Guid campId = scenario == "foreign" ? Guid.NewGuid() : id;
            HttpResponseMessage response;
            if (operation == "add") response = await client.PostAsJsonAsync($"/api/camps/{campId}/meals/additional", new AddAdditionalMealRequest(ScheduleEnd, "Snack", 1), ct);
            else if (operation == "order") response = await client.PutAsJsonAsync($"/api/camps/{campId}/meals/days/{ScheduleStart:yyyy-MM-dd}/order",
                new OrderDayMealsRequest(await catering.CampMeals.Where(x => x.Date == ScheduleStart).Select(x => x.Id).ToArrayAsync(ct), 1), ct);
            else
            {
                using var request = new HttpRequestMessage(HttpMethod.Delete, $"/api/camps/{campId}/meals/{snack.Id}");
                request.Headers.IfMatch.ParseAdd("\"1\""); response = await client.SendAsync(request, ct);
            }
            using (response) Assert.Equal((HttpStatusCode)status, response.StatusCode);
        }
        finally { await app.StopAsync(ct); }
    }
}
