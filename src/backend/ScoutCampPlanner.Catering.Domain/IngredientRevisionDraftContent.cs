namespace ScoutCampPlanner.Catering.Domain;

public sealed record IngredientRevisionDraftContent
{
    private IngredientRevisionDraftContent(
        string name,
        string normalizedName,
        Guid categoryId,
        Guid baseUnitId,
        IngredientPropertyReviewState allergenReviewState,
        IngredientPropertyReviewState intoleranceReviewState,
        IngredientPropertyReviewState originReviewState,
        IReadOnlyList<IngredientPropertyValue> allergens,
        IReadOnlyList<IngredientPropertyValue> intolerances,
        IReadOnlyList<IngredientPropertyValue> origins)
    {
        Name = name;
        NormalizedName = normalizedName;
        CategoryId = categoryId;
        BaseUnitId = baseUnitId;
        AllergenReviewState = allergenReviewState;
        IntoleranceReviewState = intoleranceReviewState;
        OriginReviewState = originReviewState;
        Allergens = allergens;
        Intolerances = intolerances;
        Origins = origins;
    }

    public string Name { get; }
    public string NormalizedName { get; }
    public Guid CategoryId { get; }
    public Guid BaseUnitId { get; }
    public IngredientPropertyReviewState AllergenReviewState { get; }
    public IngredientPropertyReviewState IntoleranceReviewState { get; }
    public IngredientPropertyReviewState OriginReviewState { get; }
    public IReadOnlyList<IngredientPropertyValue> Allergens { get; }
    public IReadOnlyList<IngredientPropertyValue> Intolerances { get; }
    public IReadOnlyList<IngredientPropertyValue> Origins { get; }

    public static IngredientRevisionDraftContent Create(
        string name,
        Guid categoryId,
        Guid baseUnitId,
        IngredientPropertyReviewState allergenReviewState,
        IngredientPropertyReviewState intoleranceReviewState,
        IngredientPropertyReviewState originReviewState,
        IEnumerable<IngredientPropertyValue>? allergens = null,
        IEnumerable<IngredientPropertyValue>? intolerances = null,
        IEnumerable<IngredientPropertyValue>? origins = null)
    {
        (string display, string normalized) = CatalogName.Normalize(name, nameof(name), 200);
        if (categoryId == Guid.Empty)
            throw new ArgumentException("Category ID is required.", nameof(categoryId));
        if (baseUnitId == Guid.Empty)
            throw new ArgumentException("Base unit ID is required.", nameof(baseUnitId));
        if (!Enum.IsDefined(allergenReviewState) ||
            !Enum.IsDefined(intoleranceReviewState) ||
            !Enum.IsDefined(originReviewState))
            throw new ArgumentOutOfRangeException(nameof(allergenReviewState));

        return new IngredientRevisionDraftContent(
            display,
            normalized,
            categoryId,
            baseUnitId,
            allergenReviewState,
            intoleranceReviewState,
            originReviewState,
            NormalizeProperties(allergens, nameof(allergens)),
            NormalizeProperties(intolerances, nameof(intolerances)),
            NormalizeProperties(origins, nameof(origins)));
    }

    private static IReadOnlyList<IngredientPropertyValue> NormalizeProperties(
        IEnumerable<IngredientPropertyValue>? values,
        string parameterName)
    {
        IngredientPropertyValue[] result = values?.OrderBy(value => value.PropertyId).ToArray() ?? [];
        if (result.Select(value => value.PropertyId).Distinct().Count() != result.Length)
            throw new ArgumentException("Property IDs must be unique.", parameterName);
        return result;
    }
}
