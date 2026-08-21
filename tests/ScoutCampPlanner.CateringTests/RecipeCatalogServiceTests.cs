using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Catering.Domain;
using Xunit;

namespace ScoutCampPlanner.CateringTests;

public sealed class RecipeCatalogServiceTests
{
    [Fact]
    public async Task Unauthorized_catalog_query_does_not_access_store()
    {
        var store = new FakeStore();
        var service = new RecipeCatalogService(store, new FakeAuthorization(false));

        RecipeCatalogResult result = await service.ListCampAsync(
            Guid.NewGuid(), Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.False(result.IsAuthorized);
        Assert.Empty(result.Entries);
        Assert.Equal(0, store.CallCount);
    }

    [Fact]
    public async Task Authorized_catalog_query_returns_store_projection()
    {
        var store = new FakeStore();
        var service = new RecipeCatalogService(store, new FakeAuthorization(true));

        RecipeCatalogResult result = await service.ListTenantAsync(
            Guid.NewGuid(), Guid.NewGuid(), TestContext.Current.CancellationToken);

        Assert.True(result.IsAuthorized);
        Assert.Single(result.Entries);
        Assert.Equal(1, store.CallCount);
    }

    private sealed class FakeStore : IRecipeCatalogStore
    {
        public int CallCount { get; private set; }

        public Task<IReadOnlyList<RecipeCatalogEntry>> ListCentralAsync(
            CancellationToken cancellationToken = default) => Result();

        public Task<IReadOnlyList<RecipeCatalogEntry>> ListTenantAsync(
            Guid tenantId, CancellationToken cancellationToken = default) => Result();

        public Task<IReadOnlyList<RecipeCatalogEntry>> ListCampAsync(
            Guid campId, CancellationToken cancellationToken = default) => Result();

        private Task<IReadOnlyList<RecipeCatalogEntry>> Result()
        {
            CallCount++;
            IReadOnlyList<RecipeCatalogEntry> entries =
            [new(null, Guid.NewGuid(), null, null, "Rezept", RecipeScopeType.Tenant,
                RecipeStatus.Draft, true, DateTimeOffset.UtcNow)];
            return Task.FromResult(entries);
        }
    }

    private sealed class FakeAuthorization(bool allowed) : IRecipeCatalogAuthorization
    {
        public Task<bool> CanReadCentralAsync(
            Guid actorUserId, CancellationToken cancellationToken = default) => Task.FromResult(allowed);

        public Task<bool> CanReadTenantAsync(
            Guid actorUserId, Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(allowed);

        public Task<bool> CanReadCampAsync(
            Guid actorUserId, Guid campId, CancellationToken cancellationToken = default) =>
            Task.FromResult(allowed);
    }
}
