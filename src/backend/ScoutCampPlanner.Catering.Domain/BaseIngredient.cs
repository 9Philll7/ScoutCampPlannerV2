namespace ScoutCampPlanner.Catering.Domain;

public enum IngredientScopeType
{
    Central,
    Tenant,
    Camp,
}

public sealed class BaseIngredient
{
    private BaseIngredient() { }

    public BaseIngredient(
        Guid id,
        IngredientScopeType scopeType,
        Guid? scopeId,
        string name,
        string? originInformation = null)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Ingredient ID is required.", nameof(id)) : id;
        ValidateScope(scopeType, scopeId);
        ScopeType = scopeType;
        ScopeId = scopeId;
        Rename(name);
        SetOriginInformation(originInformation);
    }

    public Guid Id { get; private set; }
    public IngredientScopeType ScopeType { get; private set; }
    public Guid? ScopeId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public string? OriginInformation { get; private set; }

    public void Rename(string name) =>
        (Name, NormalizedName) = CatalogName.Normalize(name, nameof(name), 200);

    public void SetOriginInformation(string? value)
    {
        string? trimmed = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        if (trimmed?.Length > 2_000)
            throw new ArgumentException("Origin information must not exceed 2000 characters.", nameof(value));
        OriginInformation = trimmed;
    }

    private static void ValidateScope(IngredientScopeType scopeType, Guid? scopeId)
    {
        if (!Enum.IsDefined(scopeType))
            throw new ArgumentOutOfRangeException(nameof(scopeType));
        if (scopeType == IngredientScopeType.Central && scopeId is not null)
            throw new ArgumentException("A central ingredient must not have an owner ID.", nameof(scopeId));
        if (scopeType != IngredientScopeType.Central && (!scopeId.HasValue || scopeId == Guid.Empty))
            throw new ArgumentException("A tenant or camp ingredient requires an owner ID.", nameof(scopeId));
    }
}
