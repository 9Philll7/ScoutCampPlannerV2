using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Application.Recipes;

public sealed record RecipeCatalogEntry(
    Guid? LibraryEntryId,
    Guid RecipeId,
    Guid? RevisionId,
    int? RevisionNumber,
    string Name,
    RecipeScopeType Scope,
    RecipeStatus Status,
    bool IsLocal,
    DateTimeOffset UpdatedAtUtc);

public sealed record RecipeCatalogResult(bool IsAuthorized, IReadOnlyList<RecipeCatalogEntry> Entries);

public interface IRecipeCatalogStore
{
    Task<IReadOnlyList<RecipeCatalogEntry>> ListCentralAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecipeCatalogEntry>> ListTenantAsync(
        Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RecipeCatalogEntry>> ListCampAsync(
        Guid campId, CancellationToken cancellationToken = default);
}

public interface IRecipeCatalogAuthorization
{
    Task<bool> CanReadCentralAsync(Guid actorUserId, CancellationToken cancellationToken = default);
    Task<bool> CanReadTenantAsync(
        Guid actorUserId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> CanReadCampAsync(
        Guid actorUserId, Guid campId, CancellationToken cancellationToken = default);
}

public sealed class RecipeCatalogService(
    IRecipeCatalogStore store,
    IRecipeCatalogAuthorization authorization)
{
    public async Task<RecipeCatalogResult> ListCentralAsync(
        Guid actorUserId, CancellationToken cancellationToken = default)
    {
        Required(actorUserId, nameof(actorUserId));
        return await authorization.CanReadCentralAsync(actorUserId, cancellationToken)
            ? new(true, await store.ListCentralAsync(cancellationToken))
            : new(false, []);
    }

    public async Task<RecipeCatalogResult> ListTenantAsync(
        Guid tenantId, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        Required(tenantId, nameof(tenantId));
        Required(actorUserId, nameof(actorUserId));
        return await authorization.CanReadTenantAsync(actorUserId, tenantId, cancellationToken)
            ? new(true, await store.ListTenantAsync(tenantId, cancellationToken))
            : new(false, []);
    }

    public async Task<RecipeCatalogResult> ListCampAsync(
        Guid campId, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        Required(campId, nameof(campId));
        Required(actorUserId, nameof(actorUserId));
        return await authorization.CanReadCampAsync(actorUserId, campId, cancellationToken)
            ? new(true, await store.ListCampAsync(campId, cancellationToken))
            : new(false, []);
    }

    private static Guid Required(Guid value, string parameterName) =>
        value == Guid.Empty ? throw new ArgumentException("ID is required.", parameterName) : value;
}
