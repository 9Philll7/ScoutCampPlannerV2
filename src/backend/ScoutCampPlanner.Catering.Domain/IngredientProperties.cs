namespace ScoutCampPlanner.Catering.Domain;

public enum IngredientPropertyState
{
    Contains,
    DoesNotContain,
    MayContain,
    Unknown,
}

public enum IngredientPropertySource
{
    Inherent,
    Derived,
    ManuallyVerified,
    ArticleDependent,
}

public enum IngredientCompatibility
{
    Compatible,
    Incompatible,
    Unknown,
}

public sealed record IngredientPropertyValue
{
    public IngredientPropertyValue(
        Guid propertyId,
        IngredientPropertyState state,
        IngredientPropertySource source)
    {
        PropertyId = propertyId == Guid.Empty
            ? throw new ArgumentException("Property ID is required.", nameof(propertyId))
            : propertyId;
        if (!Enum.IsDefined(state))
            throw new ArgumentOutOfRangeException(nameof(state));
        if (!Enum.IsDefined(source))
            throw new ArgumentOutOfRangeException(nameof(source));
        State = state;
        Source = source;
    }

    public Guid PropertyId { get; }
    public IngredientPropertyState State { get; }
    public IngredientPropertySource Source { get; }
}

public sealed class IngredientVariantRevision
{
    private readonly Dictionary<Guid, IngredientPropertyValue> allergenOverrides = [];
    private readonly Dictionary<Guid, IngredientPropertyValue> intoleranceOverrides = [];
    private readonly Dictionary<Guid, IngredientPropertyValue> originOverrides = [];

    internal IngredientVariantRevision(Guid id, string variantKey, string name)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Variant ID is required.", nameof(id)) : id;
        VariantKey = NormalizeKey(variantKey);
        Rename(name);
    }

    public Guid Id { get; }
    public string VariantKey { get; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public IReadOnlyCollection<IngredientPropertyValue> AllergenOverrides => allergenOverrides.Values.ToArray();
    public IReadOnlyCollection<IngredientPropertyValue> IntoleranceOverrides => intoleranceOverrides.Values.ToArray();
    public IReadOnlyCollection<IngredientPropertyValue> OriginOverrides => originOverrides.Values.ToArray();

    internal void Rename(string name) =>
        (Name, NormalizedName) = CatalogName.Normalize(name, nameof(name), 200);

    internal void SetAllergenOverride(IngredientPropertyValue value) => allergenOverrides[value.PropertyId] = value;
    internal void SetIntoleranceOverride(IngredientPropertyValue value) => intoleranceOverrides[value.PropertyId] = value;
    internal void SetOriginOverride(IngredientPropertyValue value) => originOverrides[value.PropertyId] = value;

    internal IngredientVariantRevision Copy(Guid id)
    {
        var copy = new IngredientVariantRevision(id, VariantKey, Name);
        foreach (IngredientPropertyValue value in allergenOverrides.Values)
            copy.SetAllergenOverride(value);
        foreach (IngredientPropertyValue value in intoleranceOverrides.Values)
            copy.SetIntoleranceOverride(value);
        foreach (IngredientPropertyValue value in originOverrides.Values)
            copy.SetOriginOverride(value);
        return copy;
    }

    internal static string NormalizeKey(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("Variant key is required.", nameof(value));
        string normalized = value.Trim().ToLowerInvariant();
        if (normalized.Length > 100)
            throw new ArgumentException("Variant key must not exceed 100 characters.", nameof(value));
        if (normalized.Any(character => !char.IsAsciiLetterOrDigit(character) && character != '_'))
            throw new ArgumentException("Variant key may contain only ASCII letters, digits, and underscores.", nameof(value));
        return normalized;
    }
}

public static class IngredientPropertyResolver
{
    public static IngredientPropertyState? EffectiveState(
        IEnumerable<IngredientPropertyValue> baseValues,
        IEnumerable<IngredientPropertyValue>? overrides,
        Guid propertyId)
    {
        IngredientPropertyValue? overridden = overrides?.SingleOrDefault(value => value.PropertyId == propertyId);
        if (overridden is not null)
            return overridden.State;
        return baseValues.SingleOrDefault(value => value.PropertyId == propertyId)?.State;
    }

    public static IngredientCompatibility Evaluate(
        IngredientPropertyState? state,
        IngredientPropertyReviewState reviewState) => state switch
        {
            IngredientPropertyState.Contains => IngredientCompatibility.Incompatible,
            IngredientPropertyState.DoesNotContain => IngredientCompatibility.Compatible,
            IngredientPropertyState.MayContain or IngredientPropertyState.Unknown => IngredientCompatibility.Unknown,
            null when reviewState == IngredientPropertyReviewState.Reviewed => IngredientCompatibility.Compatible,
            _ => IngredientCompatibility.Unknown,
        };
}
