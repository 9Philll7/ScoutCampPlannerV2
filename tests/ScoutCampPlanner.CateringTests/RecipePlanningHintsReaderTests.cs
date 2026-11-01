using System.Text.Json;
using ScoutCampPlanner.Catering.Infrastructure;
using ScoutCampPlanner.Catering.Infrastructure.Recipes;
using ScoutCampPlanner.Catering.Domain;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed partial class MealPlanningTests
{
    [Theory]
    [InlineData("central", true)]
    [InlineData("tenant", true)]
    [InlineData("camp", true)]
    [InlineData("foreign-tenant", false)]
    [InlineData("foreign-camp", false)]
    [InlineData("unpublished", false)]
    public async Task Recipe_hint_reader_only_exposes_accessible_published_revisions(string scenario, bool allowed)
    {
        var ct = TestContext.Current.CancellationToken;
        await using var fixture = await DatabaseFixture.CreateAsync();
        Guid tenantId = Guid.NewGuid(), campId = Guid.NewGuid(), recipeId = Guid.NewGuid(), revisionId = Guid.NewGuid();
        var references = new SupplySolutionTests.Fixture();
        var snapshot = references.Recipes[references.Recipe()];
        var scope = scenario == "central" ? RecipeScopeType.Central : scenario.Contains("tenant") ? RecipeScopeType.Tenant : RecipeScopeType.Camp;
        Guid? scopeId = scope == RecipeScopeType.Central ? null : scenario.StartsWith("foreign") ? Guid.NewGuid()
            : scope == RecipeScopeType.Tenant ? tenantId : campId;
        fixture.Database.Add(new RecipeRecord { Id = recipeId, ScopeType = (int)scope, ScopeId = scopeId,
            Name = "Test", NormalizedName = "TEST", RecipeType = (int)RecipeType.PortionBased, Status = (int)RecipeStatus.Active });
        if (scenario != "unpublished") fixture.Database.Add(new RecipeRevisionRecord {
            Id = revisionId, RecipeId = recipeId, RevisionNumber = 1, PublishedBy = Guid.NewGuid(),
            PublishedAtUtc = DateTimeOffset.UtcNow, SnapshotSchemaVersion = 2,
            SnapshotJson = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web)) });
        await fixture.Database.SaveChangesAsync(ct);
        var result = await new RecipePlanningHintsReader(fixture.Database, new MealPlanningStore(fixture.Database), TimeProvider.System)
            .ReadAsync(campId, tenantId, revisionId, ct);
        Assert.Equal(allowed, result is not null);
        if (result is not null) Assert.True(result.HasUnknownData); // Empty standard recipe is never a clearance.
    }
}
