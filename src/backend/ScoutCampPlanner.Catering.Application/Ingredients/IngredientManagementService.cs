using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Application.Ingredients;

public sealed record CreateIngredientRequest(
    string Name,
    string? OriginInformation,
    IReadOnlyList<string> Variants);

public enum IngredientMutationStatus
{
    Created,
    Invalid,
    DuplicateName,
    Forbidden,
}

public sealed record IngredientMutationResult(
    IngredientMutationStatus Status,
    IngredientCatalogEntry? Ingredient = null);

public interface IIngredientManagementStore
{
    Task<IngredientMutationResult> CreateAsync(
        Guid ingredientId,
        IngredientScopeType scope,
        Guid? scopeId,
        CreateIngredientRequest request,
        CancellationToken cancellationToken = default);
}

public interface IIngredientManagementAuthorization
{
    Task<bool> CanManageCentralAsync(Guid actorUserId, CancellationToken cancellationToken = default);
    Task<bool> CanManageTenantAsync(
        Guid actorUserId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> CanManageCampAsync(
        Guid actorUserId, Guid campId, CancellationToken cancellationToken = default);
}

public sealed class IngredientManagementService(
    IIngredientManagementStore store,
    IIngredientManagementAuthorization authorization)
{
    public async Task<IngredientMutationResult> CreateCampAsync(
        Guid campId,
        CreateIngredientRequest request,
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        Required(campId, nameof(campId));
        Required(actorUserId, nameof(actorUserId));
        ArgumentNullException.ThrowIfNull(request);
        if (!await authorization.CanManageCampAsync(actorUserId, campId, cancellationToken))
            return new(IngredientMutationStatus.Forbidden);
        if (!IsValid(request)) return new(IngredientMutationStatus.Invalid);
        return await store.CreateAsync(
            Guid.NewGuid(), IngredientScopeType.Camp, campId, Normalize(request), cancellationToken);
    }

    private static bool IsValid(CreateIngredientRequest request) =>
        !string.IsNullOrWhiteSpace(request.Name) && request.Name.Trim().Length <= 200 &&
        (string.IsNullOrWhiteSpace(request.OriginInformation) || request.OriginInformation.Trim().Length <= 2_000) &&
        request.Variants.All(value => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= 200) &&
        request.Variants.Select(value => value.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() ==
        request.Variants.Count;

    private static CreateIngredientRequest Normalize(CreateIngredientRequest request) => new(
        request.Name.Trim(),
        string.IsNullOrWhiteSpace(request.OriginInformation) ? null : request.OriginInformation.Trim(),
        request.Variants.Select(value => value.Trim()).ToArray());

    private static Guid Required(Guid value, string parameterName) =>
        value == Guid.Empty ? throw new ArgumentException("ID is required.", parameterName) : value;
}
