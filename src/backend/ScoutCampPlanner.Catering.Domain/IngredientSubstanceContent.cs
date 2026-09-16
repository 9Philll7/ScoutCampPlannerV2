namespace ScoutCampPlanner.Catering.Domain;

public enum IngredientSubstanceContentSourceType
{
    Manufacturer,
    OfficialDatabase,
    ManualEstimate,
}

public enum IngredientSubstanceContentReviewState
{
    Unreviewed,
    Reviewed,
}

/// <summary>
/// A measurable constituent of an ingredient. This is deliberately separate from a
/// person's intolerance and from the qualitative allergen/property model.
/// </summary>
public sealed record IngredientSubstanceContent
{
    public IngredientSubstanceContent(
        Guid substanceId,
        decimal amount,
        Guid amountUnitId,
        decimal referenceQuantity,
        Guid referenceUnitId,
        IngredientSubstanceContentSourceType sourceType,
        string sourceReference,
        IngredientSubstanceContentReviewState reviewState)
    {
        SubstanceId = Required(substanceId, nameof(substanceId));
        if (amount < 0) throw new ArgumentOutOfRangeException(nameof(amount));
        Amount = amount;
        AmountUnitId = Required(amountUnitId, nameof(amountUnitId));
        if (referenceQuantity <= 0) throw new ArgumentOutOfRangeException(nameof(referenceQuantity));
        ReferenceQuantity = referenceQuantity;
        ReferenceUnitId = Required(referenceUnitId, nameof(referenceUnitId));
        if (!Enum.IsDefined(sourceType)) throw new ArgumentOutOfRangeException(nameof(sourceType));
        if (!Enum.IsDefined(reviewState)) throw new ArgumentOutOfRangeException(nameof(reviewState));
        SourceType = sourceType;
        SourceReference = string.IsNullOrWhiteSpace(sourceReference)
            ? throw new ArgumentException("Source reference is required.", nameof(sourceReference))
            : sourceReference.Trim();
        if (SourceReference.Length > 500)
            throw new ArgumentException("Source reference must not exceed 500 characters.", nameof(sourceReference));
        ReviewState = reviewState;
    }

    public Guid SubstanceId { get; }
    public decimal Amount { get; }
    public Guid AmountUnitId { get; }
    public decimal ReferenceQuantity { get; }
    public Guid ReferenceUnitId { get; }
    public IngredientSubstanceContentSourceType SourceType { get; }
    public string SourceReference { get; }
    public IngredientSubstanceContentReviewState ReviewState { get; }

    private static Guid Required(Guid value, string name) =>
        value == Guid.Empty ? throw new ArgumentException("ID is required.", name) : value;
}
