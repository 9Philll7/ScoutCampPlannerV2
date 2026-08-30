namespace ScoutCampPlanner.Catering.Domain;

public sealed record IngredientPropertyDefinition
{
    public IngredientPropertyDefinition(Guid id, string code, Guid? parentId = null)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Property definition ID is required.", nameof(id)) : id;
        if (string.IsNullOrWhiteSpace(code))
            throw new ArgumentException("Property definition code is required.", nameof(code));
        string normalized = code.Trim().ToUpperInvariant();
        if (normalized.Length > 100 || normalized.Any(value => !char.IsAsciiLetterOrDigit(value) && value != '_'))
            throw new ArgumentException("Property definition code must use at most 100 ASCII letters, digits, or underscores.", nameof(code));
        if (parentId == Guid.Empty)
            throw new ArgumentException("Parent ID must be non-empty when specified.", nameof(parentId));
        Code = normalized;
        ParentId = parentId;
    }

    public Guid Id { get; }
    public string Code { get; }
    public Guid? ParentId { get; }
}

public sealed class IngredientSuitabilityEvaluator
{
    private static readonly string[] VeganForbiddenOrigins =
    [
        "MEAT", "POULTRY", "FISH", "CRUSTACEAN", "MOLLUSC", "DAIRY", "EGG", "HONEY",
        "INSECT", "ANIMAL_FAT", "GELATIN", "ANIMAL_RENNET", "OTHER_ANIMAL_DERIVED",
    ];

    private static readonly string[] VegetarianForbiddenOrigins =
    [
        "MEAT", "POULTRY", "FISH", "CRUSTACEAN", "MOLLUSC", "INSECT", "ANIMAL_FAT",
        "GELATIN", "ANIMAL_RENNET", "OTHER_ANIMAL_DERIVED",
    ];

    private static readonly string[] PescetarianForbiddenOrigins =
    [
        "MEAT", "POULTRY", "INSECT", "ANIMAL_FAT", "GELATIN", "ANIMAL_RENNET",
        "OTHER_ANIMAL_DERIVED",
    ];

    private readonly IReadOnlyDictionary<string, IngredientPropertyDefinition> allergensByCode;
    private readonly IReadOnlyDictionary<Guid, IngredientPropertyDefinition> allergensById;
    private readonly IReadOnlyDictionary<string, IngredientPropertyDefinition> intolerancesByCode;
    private readonly IReadOnlyDictionary<string, IngredientPropertyDefinition> originsByCode;

    public IngredientSuitabilityEvaluator(
        IEnumerable<IngredientPropertyDefinition> allergens,
        IEnumerable<IngredientPropertyDefinition> intolerances,
        IEnumerable<IngredientPropertyDefinition> origins)
    {
        allergensByCode = CreateCodeIndex(allergens, nameof(allergens));
        allergensById = allergensByCode.Values.ToDictionary(value => value.Id);
        intolerancesByCode = CreateCodeIndex(intolerances, nameof(intolerances));
        originsByCode = CreateCodeIndex(origins, nameof(origins));
        ValidateAllergenHierarchy();
    }

    public IngredientCompatibility EvaluateAllergen(
        IngredientRevision revision,
        string allergenCode,
        string? variantKey = null)
    {
        ArgumentNullException.ThrowIfNull(revision);
        IngredientPropertyDefinition allergen = Required(allergensByCode, allergenCode, nameof(allergenCode));
        IngredientVariantRevision? variant = GetVariant(revision, variantKey);
        IEnumerable<IngredientPropertyDefinition> relevant = allergensById.Values
            .Where(value => value.Id == allergen.Id || IsDescendantOf(value, allergen.Id));
        return Combine(relevant.Select(value => IngredientPropertyResolver.Evaluate(
            IngredientPropertyResolver.EffectiveState(
                revision.Allergens,
                variant?.AllergenOverrides,
                value.Id),
            revision.AllergenReviewState)));
    }

    public IngredientCompatibility EvaluateIntolerance(
        IngredientRevision revision,
        string intoleranceCode,
        string? variantKey = null)
    {
        ArgumentNullException.ThrowIfNull(revision);
        IngredientPropertyDefinition intolerance = Required(
            intolerancesByCode, intoleranceCode, nameof(intoleranceCode));
        IngredientVariantRevision? variant = GetVariant(revision, variantKey);
        return IngredientPropertyResolver.Evaluate(
            IngredientPropertyResolver.EffectiveState(
                revision.Intolerances,
                variant?.IntoleranceOverrides,
                intolerance.Id),
            revision.IntoleranceReviewState);
    }

    public IngredientCompatibility EvaluateVegan(IngredientRevision revision, string? variantKey = null) =>
        EvaluateOrigins(revision, VeganForbiddenOrigins, variantKey);

    public IngredientCompatibility EvaluateVegetarian(IngredientRevision revision, string? variantKey = null) =>
        EvaluateOrigins(revision, VegetarianForbiddenOrigins, variantKey);

    public IngredientCompatibility EvaluatePescetarian(IngredientRevision revision, string? variantKey = null) =>
        EvaluateOrigins(revision, PescetarianForbiddenOrigins, variantKey);

    public IngredientCompatibility EvaluateLactoseFree(IngredientRevision revision, string? variantKey = null) =>
        EvaluateIntolerance(revision, "LACTOSE", variantKey);

    public IngredientCompatibility EvaluateMilkFree(IngredientRevision revision, string? variantKey = null) =>
        EvaluateAllergen(revision, "MILK", variantKey);

    public IngredientCompatibility EvaluateGlutenFree(IngredientRevision revision, string? variantKey = null) =>
        Combine(
        [
            EvaluateIntolerance(revision, "GLUTEN", variantKey),
            EvaluateAllergen(revision, "GLUTEN_CEREALS", variantKey),
        ]);

    private IngredientCompatibility EvaluateOrigins(
        IngredientRevision revision,
        IEnumerable<string> forbiddenCodes,
        string? variantKey)
    {
        ArgumentNullException.ThrowIfNull(revision);
        IngredientVariantRevision? variant = GetVariant(revision, variantKey);
        var results = forbiddenCodes.Select(code =>
        {
            IngredientPropertyDefinition definition = Required(originsByCode, code, nameof(forbiddenCodes));
            return IngredientPropertyResolver.Evaluate(
                IngredientPropertyResolver.EffectiveState(
                    revision.Origins,
                    variant?.OriginOverrides,
                    definition.Id),
                revision.OriginReviewState);
        }).ToList();

        IngredientPropertyDefinition unknownOrigin = Required(originsByCode, "UNKNOWN_ORIGIN", nameof(originsByCode));
        IngredientPropertyState? unknownState = IngredientPropertyResolver.EffectiveState(
            revision.Origins, variant?.OriginOverrides, unknownOrigin.Id);
        if (unknownState is IngredientPropertyState.Contains or IngredientPropertyState.MayContain or IngredientPropertyState.Unknown)
            results.Add(IngredientCompatibility.Unknown);

        return Combine(results);
    }

    private bool IsDescendantOf(IngredientPropertyDefinition candidate, Guid ancestorId)
    {
        Guid? parentId = candidate.ParentId;
        var visited = new HashSet<Guid>();
        while (parentId.HasValue)
        {
            if (parentId == ancestorId)
                return true;
            if (!visited.Add(parentId.Value) || !allergensById.TryGetValue(parentId.Value, out IngredientPropertyDefinition? parent))
                return false;
            parentId = parent.ParentId;
        }
        return false;
    }

    private void ValidateAllergenHierarchy()
    {
        foreach (IngredientPropertyDefinition definition in allergensById.Values)
        {
            if (definition.ParentId.HasValue && !allergensById.ContainsKey(definition.ParentId.Value))
                throw new ArgumentException($"Allergen '{definition.Code}' references an unknown parent.");
            if (IsDescendantOf(definition, definition.Id))
                throw new ArgumentException($"Allergen hierarchy contains a cycle at '{definition.Code}'.");
        }
    }

    private static IngredientVariantRevision? GetVariant(IngredientRevision revision, string? variantKey)
    {
        if (string.IsNullOrWhiteSpace(variantKey))
            return null;
        string normalized = IngredientVariantRevision.NormalizeKey(variantKey);
        return revision.Variants.SingleOrDefault(value => value.VariantKey == normalized)
            ?? throw new ArgumentException("The selected variant does not belong to the ingredient revision.", nameof(variantKey));
    }

    private static IngredientCompatibility Combine(IEnumerable<IngredientCompatibility> results)
    {
        IngredientCompatibility[] values = results.ToArray();
        if (values.Contains(IngredientCompatibility.Incompatible))
            return IngredientCompatibility.Incompatible;
        return values.Contains(IngredientCompatibility.Unknown)
            ? IngredientCompatibility.Unknown
            : IngredientCompatibility.Compatible;
    }

    private static IReadOnlyDictionary<string, IngredientPropertyDefinition> CreateCodeIndex(
        IEnumerable<IngredientPropertyDefinition> definitions,
        string parameterName)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        IngredientPropertyDefinition[] values = definitions.ToArray();
        if (values.Select(value => value.Id).Distinct().Count() != values.Length)
            throw new ArgumentException("Property IDs must be unique.", parameterName);
        if (values.Select(value => value.Code).Distinct(StringComparer.Ordinal).Count() != values.Length)
            throw new ArgumentException("Property codes must be unique.", parameterName);
        return values.ToDictionary(value => value.Code, StringComparer.Ordinal);
    }

    private static IngredientPropertyDefinition Required(
        IReadOnlyDictionary<string, IngredientPropertyDefinition> definitions,
        string code,
        string parameterName)
    {
        string normalized = string.IsNullOrWhiteSpace(code) ? string.Empty : code.Trim().ToUpperInvariant();
        return definitions.TryGetValue(normalized, out IngredientPropertyDefinition? definition)
            ? definition
            : throw new ArgumentException($"Unknown property code '{code}'.", parameterName);
    }
}

