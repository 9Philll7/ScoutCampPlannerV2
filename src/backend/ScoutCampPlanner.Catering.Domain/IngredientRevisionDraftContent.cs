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
        IngredientPropertyReviewState originReviewState)
    {
        Name = name;
        NormalizedName = normalizedName;
        CategoryId = categoryId;
        BaseUnitId = baseUnitId;
        AllergenReviewState = allergenReviewState;
        IntoleranceReviewState = intoleranceReviewState;
        OriginReviewState = originReviewState;
    }

    public string Name { get; }
    public string NormalizedName { get; }
    public Guid CategoryId { get; }
    public Guid BaseUnitId { get; }
    public IngredientPropertyReviewState AllergenReviewState { get; }
    public IngredientPropertyReviewState IntoleranceReviewState { get; }
    public IngredientPropertyReviewState OriginReviewState { get; }

    public static IngredientRevisionDraftContent Create(
        string name,
        Guid categoryId,
        Guid baseUnitId,
        IngredientPropertyReviewState allergenReviewState,
        IngredientPropertyReviewState intoleranceReviewState,
        IngredientPropertyReviewState originReviewState)
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
            originReviewState);
    }
}
