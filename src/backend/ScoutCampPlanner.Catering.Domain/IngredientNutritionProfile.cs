namespace ScoutCampPlanner.Catering.Domain;

public enum IngredientNutritionReviewState
{
    Unreviewed,
    Reviewed,
}

public enum IngredientNutritionSourceType
{
    Manufacturer,
    OfficialDatabase,
    ManualEstimate,
}

public sealed record IngredientNutritionProfile
{
    public IngredientNutritionProfile(
        decimal referenceQuantity,
        Guid referenceUnitId,
        decimal? energyKilojoules,
        decimal? fatGrams,
        decimal? saturatedFatGrams,
        decimal? carbohydrateGrams,
        decimal? sugarsGrams,
        decimal? proteinGrams,
        decimal? saltGrams,
        decimal? fiberGrams,
        IngredientNutritionSourceType sourceType,
        string sourceReference,
        IngredientNutritionReviewState reviewState,
        DateOnly? referenceDate = null)
    {
        if (referenceQuantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(referenceQuantity));
        if (referenceUnitId == Guid.Empty)
            throw new ArgumentException("Reference unit ID is required.", nameof(referenceUnitId));
        if (!Enum.IsDefined(sourceType))
            throw new ArgumentOutOfRangeException(nameof(sourceType));
        if (!Enum.IsDefined(reviewState))
            throw new ArgumentOutOfRangeException(nameof(reviewState));
        string normalizedSource = sourceReference?.Trim() ?? string.Empty;
        if (normalizedSource.Length is 0 or > 500)
            throw new ArgumentException("Source reference is required and must not exceed 500 characters.", nameof(sourceReference));

        ValidateNonNegative(energyKilojoules, nameof(energyKilojoules));
        ValidateNonNegative(fatGrams, nameof(fatGrams));
        ValidateNonNegative(saturatedFatGrams, nameof(saturatedFatGrams));
        ValidateNonNegative(carbohydrateGrams, nameof(carbohydrateGrams));
        ValidateNonNegative(sugarsGrams, nameof(sugarsGrams));
        ValidateNonNegative(proteinGrams, nameof(proteinGrams));
        ValidateNonNegative(saltGrams, nameof(saltGrams));
        ValidateNonNegative(fiberGrams, nameof(fiberGrams));
        if (saturatedFatGrams > fatGrams)
            throw new ArgumentException("Saturated fat must not exceed total fat.", nameof(saturatedFatGrams));
        if (sugarsGrams > carbohydrateGrams)
            throw new ArgumentException("Sugars must not exceed total carbohydrate.", nameof(sugarsGrams));
        if (reviewState == IngredientNutritionReviewState.Reviewed &&
            (energyKilojoules is null || fatGrams is null || saturatedFatGrams is null ||
             carbohydrateGrams is null || sugarsGrams is null || proteinGrams is null || saltGrams is null))
            throw new ArgumentException("A reviewed nutrition profile requires all core values.", nameof(reviewState));

        ReferenceQuantity = referenceQuantity;
        ReferenceUnitId = referenceUnitId;
        EnergyKilojoules = energyKilojoules;
        FatGrams = fatGrams;
        SaturatedFatGrams = saturatedFatGrams;
        CarbohydrateGrams = carbohydrateGrams;
        SugarsGrams = sugarsGrams;
        ProteinGrams = proteinGrams;
        SaltGrams = saltGrams;
        FiberGrams = fiberGrams;
        SourceType = sourceType;
        SourceReference = normalizedSource;
        ReviewState = reviewState;
        ReferenceDate = referenceDate;
    }

    public decimal ReferenceQuantity { get; }
    public Guid ReferenceUnitId { get; }
    public decimal? EnergyKilojoules { get; }
    public decimal? EnergyKilocalories => EnergyKilojoules / 4.184m;
    public decimal? FatGrams { get; }
    public decimal? SaturatedFatGrams { get; }
    public decimal? CarbohydrateGrams { get; }
    public decimal? SugarsGrams { get; }
    public decimal? ProteinGrams { get; }
    public decimal? SaltGrams { get; }
    public decimal? FiberGrams { get; }
    public IngredientNutritionSourceType SourceType { get; }
    public string SourceReference { get; }
    public IngredientNutritionReviewState ReviewState { get; }
    public DateOnly? ReferenceDate { get; }

    public bool IsComplete => ReviewState == IngredientNutritionReviewState.Reviewed;

    private static void ValidateNonNegative(decimal? value, string parameterName)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(parameterName);
    }
}

public static class IngredientNutritionResolver
{
    public static IngredientNutritionProfile? EffectiveProfile(
        IngredientNutritionProfile? revisionProfile,
        IngredientNutritionProfile? variantProfile) => variantProfile ?? revisionProfile;
}
