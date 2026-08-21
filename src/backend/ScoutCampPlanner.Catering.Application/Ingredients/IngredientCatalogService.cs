using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Application.Recipes;

namespace ScoutCampPlanner.Catering.Application.Ingredients;

public sealed record IngredientVariantItem(Guid Id, string Name);
public sealed record IngredientUnitItem(
    Guid UnitId, string Name, string Symbol, MeasurementDimension Dimension,
    decimal BaseUnitFactor, decimal ReferenceQuantityPerUnit);
public sealed record IngredientConflictItem(ConflictType Type, Guid Id, string Name);
public sealed record IngredientCatalogEntry(
    Guid Id,
    string Name,
    IngredientScopeType Scope,
    Guid? ScopeId,
    string? OriginInformation,
    IReadOnlyList<IngredientVariantItem> Variants,
    IReadOnlyList<IngredientUnitItem> Units,
    IReadOnlyList<IngredientConflictItem> Conflicts);

public sealed record IngredientCatalogResult(bool IsAuthorized, IReadOnlyList<IngredientCatalogEntry> Entries);

public interface IIngredientCatalogStore
{
    Task<IReadOnlyList<IngredientCatalogEntry>> ListCentralAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IngredientCatalogEntry>> ListTenantAsync(
        Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<IngredientCatalogEntry>> ListCampAsync(
        Guid tenantId, Guid campId, CancellationToken cancellationToken = default);
}

public interface ICampTenantResolver
{
    Task<Guid?> FindTenantIdAsync(Guid campId, CancellationToken cancellationToken = default);
}

public sealed class IngredientCatalogService(
    IIngredientCatalogStore store,
    IRecipeCatalogAuthorization authorization,
    ICampTenantResolver camps)
{
    public async Task<IngredientCatalogResult> ListCentralAsync(
        Guid actorUserId, CancellationToken cancellationToken = default)
    {
        Required(actorUserId, nameof(actorUserId));
        return await authorization.CanReadCentralAsync(actorUserId, cancellationToken)
            ? new(true, await store.ListCentralAsync(cancellationToken))
            : new(false, []);
    }

    public async Task<IngredientCatalogResult> ListTenantAsync(
        Guid tenantId, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        Required(tenantId, nameof(tenantId));
        Required(actorUserId, nameof(actorUserId));
        return await authorization.CanReadTenantAsync(actorUserId, tenantId, cancellationToken)
            ? new(true, await store.ListTenantAsync(tenantId, cancellationToken))
            : new(false, []);
    }

    public async Task<IngredientCatalogResult> ListCampAsync(
        Guid campId, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        Required(campId, nameof(campId));
        Required(actorUserId, nameof(actorUserId));
        if (!await authorization.CanReadCampAsync(actorUserId, campId, cancellationToken))
            return new(false, []);
        Guid? tenantId = await camps.FindTenantIdAsync(campId, cancellationToken);
        return tenantId.HasValue
            ? new(true, await store.ListCampAsync(tenantId.Value, campId, cancellationToken))
            : new(false, []);
    }

    private static Guid Required(Guid value, string parameterName) =>
        value == Guid.Empty ? throw new ArgumentException("ID is required.", parameterName) : value;
}
