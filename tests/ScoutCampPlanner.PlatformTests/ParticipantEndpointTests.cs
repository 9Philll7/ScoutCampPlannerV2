using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ScoutCampPlanner.Api.Camps;
using ScoutCampPlanner.Platform.Application.Authorization;
using Xunit;

namespace ScoutCampPlanner.PlatformTests;

public sealed partial class CampManagementServiceTests
{
    [Fact]
    public async Task Participant_HTTP_workflow_requires_explicit_grants_and_rejects_stale_edits()
    {
        var ct = TestContext.Current.CancellationToken;
        var (campId, participants) = await ParticipantFixtureAsync(false, false);
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddAuthentication().AddCookie();
        builder.Services.AddAuthorization();
        builder.Services.AddSingleton(participants);
        builder.Services.AddSingleton(service);
        builder.Services.AddSingleton(camps);
        builder.Services.AddSingleton(catering);
        builder.Services.AddSingleton(await GrantServiceAsync());
        await using var app = builder.Build();
        app.Urls.Add("http://127.0.0.1:0");
        app.UseAuthentication();
        // This identity exists only in the isolated HTTP test host; production uses real authentication.
        app.Use((context, next) =>
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, otherUserId.ToString())], "test"));
            return next(context);
        });
        app.UseAuthorization();
        app.MapParticipantEndpoints();
        app.MapCampExplicitPermissionEndpoints();
        await app.StartAsync(ct);
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
            string path = $"/api/camps/{campId}/participants";
            using var denied = await client.GetAsync(path, ct);
            Assert.Equal(HttpStatusCode.NotFound, denied.StatusCode);
            Guid membership = (await platform.CampMemberships.SingleAsync(ct)).Id;
            foreach (string permission in new[] { Permissions.Health.ReadParticipantRequirements, Permissions.Health.EditParticipantRequirements })
            {
                using var granted = await client.PutAsJsonAsync($"/api/camps/{campId}/explicit-permissions/{membership}",
                    new SetExplicitPermissionRequest(permission, true), ct);
                Assert.Equal(HttpStatusCode.NoContent, granted.StatusCode);
            }
            using var created = await client.PostAsJsonAsync(path, DummyParticipant(), ct);
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
            using var list = await client.GetAsync(path, ct);
            Assert.True(list.Headers.CacheControl!.NoStore);
            JsonElement overview = await list.Content.ReadFromJsonAsync<JsonElement>(ct);
            Assert.True(overview.GetProperty("dummyDataOnly").GetBoolean());
            var document = overview.GetProperty("participants")[0];
            Guid id = document.GetProperty("data").GetProperty("id").GetGuid();
            string token = document.GetProperty("stateToken").GetString()!;
            using var saved = await client.PutAsJsonAsync($"{path}/{id}",
                new UpdateParticipantRequest(token, DummyParticipant() with { DisplayName = "HTTP changed dummy" }), ct);
            Assert.Equal(HttpStatusCode.OK, saved.StatusCode);
            using var stale = await client.PutAsJsonAsync($"{path}/{id}", new UpdateParticipantRequest(token, DummyParticipant()), ct);
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            Assert.Equal("HTTP changed dummy", (await camps.Participants.AsNoTracking().SingleAsync(ct)).DisplayName);
        }
        finally { await app.StopAsync(ct); }
    }
}
