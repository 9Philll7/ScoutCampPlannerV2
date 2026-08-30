namespace ScoutCampPlanner.Catering.Infrastructure.Ingredients;

internal sealed class IngredientIdentityRecord
{
    public Guid Id { get; set; }
    public int ScopeType { get; set; }
    public Guid? ScopeId { get; set; }
    public Guid? SourceIngredientId { get; set; }
    public Guid? SourceRevisionId { get; set; }
    public Guid? CurrentPublishedRevisionId { get; set; }
    public int Status { get; set; }
}

internal sealed class IngredientRevisionRecord
{
    public Guid Id { get; set; }
    public Guid IngredientId { get; set; }
    public int RevisionNumber { get; set; }
    public int State { get; set; }
    public Guid? BasedOnRevisionId { get; set; }
    public Guid? MergedCentralRevisionId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public Guid CategoryId { get; set; }
    public Guid BaseUnitId { get; set; }
    public int AllergenReviewState { get; set; }
    public int IntoleranceReviewState { get; set; }
    public int OriginReviewState { get; set; }
    public long RowVersion { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public Guid UpdatedBy { get; set; }
    public DateTimeOffset? PublishedAtUtc { get; set; }
    public Guid? PublishedBy { get; set; }
}

internal sealed class IngredientCategoryRecord
{
    public Guid Id { get; set; }
    public Guid? ParentCategoryId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public int Status { get; set; }
}

internal sealed class IngredientAllergenDefinitionRecord
{
    public Guid Id { get; set; }
    public Guid? ParentAllergenId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsEuMajorAllergen { get; set; }
    public int Status { get; set; }
}

internal sealed class IngredientIntoleranceDefinitionRecord
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsQuantityDependent { get; set; }
    public int Status { get; set; }
}

internal sealed class IngredientOriginPropertyRecord
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsAnimalOrigin { get; set; }
    public int Status { get; set; }
}

internal sealed class IngredientRevisionAllergenRecord
{
    public Guid IngredientRevisionId { get; set; }
    public Guid AllergenId { get; set; }
    public int State { get; set; }
    public int Source { get; set; }
}

internal sealed class IngredientRevisionIntoleranceRecord
{
    public Guid IngredientRevisionId { get; set; }
    public Guid IntoleranceId { get; set; }
    public int State { get; set; }
    public int Source { get; set; }
}

internal sealed class IngredientRevisionOriginRecord
{
    public Guid IngredientRevisionId { get; set; }
    public Guid OriginPropertyId { get; set; }
    public int State { get; set; }
    public int Source { get; set; }
}

internal sealed class IngredientRevisionUnitConversionRecord
{
    public Guid IngredientRevisionId { get; set; }
    public Guid SourceUnitId { get; set; }
    public decimal FactorToBaseUnit { get; set; }
    public int Precision { get; set; }
}

internal sealed class IngredientVariantRevisionRecord
{
    public Guid Id { get; set; }
    public Guid IngredientRevisionId { get; set; }
    public string VariantKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public int Status { get; set; }
    public int SortOrder { get; set; }
}

internal sealed class IngredientVariantAllergenOverrideRecord
{
    public Guid VariantRevisionId { get; set; }
    public Guid AllergenId { get; set; }
    public int State { get; set; }
    public int Source { get; set; }
}

internal sealed class IngredientVariantIntoleranceOverrideRecord
{
    public Guid VariantRevisionId { get; set; }
    public Guid IntoleranceId { get; set; }
    public int State { get; set; }
    public int Source { get; set; }
}

internal sealed class IngredientVariantOriginOverrideRecord
{
    public Guid VariantRevisionId { get; set; }
    public Guid OriginPropertyId { get; set; }
    public int State { get; set; }
    public int Source { get; set; }
}

internal sealed class IngredientVariantUnitConversionOverrideRecord
{
    public Guid VariantRevisionId { get; set; }
    public Guid SourceUnitId { get; set; }
    public decimal FactorToBaseUnit { get; set; }
    public int Precision { get; set; }
}

