namespace ScoutCampPlanner.Catering.Infrastructure.Recipes;

internal sealed class RecipeRecord
{
    public Guid Id { get; set; }
    public int ScopeType { get; set; }
    public Guid? ScopeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public int Status { get; set; }
    public int RecipeType { get; set; }
    public string? Description { get; set; }
    public string? Source { get; set; }
    public string? InternalNotes { get; set; }
    public decimal? ReferenceServings { get; set; }
    public Guid? AuthoringStageId { get; set; }
    public string? AuthoringStageName { get; set; }
    public decimal? AuthoringStageFactor { get; set; }
    public decimal? ReferenceQuantity { get; set; }
    public Guid? ReferenceUnitId { get; set; }
    public bool? DefaultAgeGroupScalingApplies { get; set; }
    public long DraftVersion { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public Guid UpdatedBy { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public Guid? ArchivedBy { get; set; }
    public DateTimeOffset? ArchivedAtUtc { get; set; }
    public Guid? ReactivatedBy { get; set; }
    public DateTimeOffset? ReactivatedAtUtc { get; set; }
    public Guid? DerivedFromRecipeId { get; set; }
    public Guid? DerivedFromRevisionId { get; set; }
    public Guid? CentralSourceRecipeId { get; set; }
    public Guid? CentralSourceRevisionId { get; set; }
    public Guid? TenantSourceRecipeId { get; set; }
    public Guid? TenantSourceRevisionId { get; set; }
}

internal sealed class RecipeDraftTagRecord
{
    public Guid RecipeId { get; set; }
    public string Value { get; set; } = string.Empty;
}

internal sealed class RecipeGroupRecord
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}

internal sealed class RecipeIngredientPositionRecord
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public Guid? GroupId { get; set; }
    public Guid? BaseIngredientId { get; set; }
    public decimal? Quantity { get; set; }
    public Guid? UnitId { get; set; }
    public int SortOrder { get; set; }
    public int ScalingMode { get; set; }
    public int AgeGroupScaling { get; set; }
    public decimal? StepSize { get; set; }
    public decimal? QuantityPerStep { get; set; }
}

internal sealed class RecipeIngredientReplacementRecord
{
    public Guid Id { get; set; }
    public Guid IngredientPositionId { get; set; }
    public Guid? ReplacementBaseIngredientId { get; set; }
    public decimal? ReplacementQuantity { get; set; }
    public Guid? ReplacementUnitId { get; set; }
}

internal sealed class RecipeSubrecipePositionRecord
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public Guid? GroupId { get; set; }
    public Guid? RecipeRevisionId { get; set; }
    public decimal? RequiredServings { get; set; }
    public decimal? RequiredQuantity { get; set; }
    public Guid? RequiredUnitId { get; set; }
    public int SortOrder { get; set; }
}

internal sealed class RecipeSubrecipeReplacementRecord
{
    public Guid Id { get; set; }
    public Guid SubrecipePositionId { get; set; }
    public Guid? ReplacementRecipeRevisionId { get; set; }
    public decimal? ReplacementServings { get; set; }
    public decimal? ReplacementQuantity { get; set; }
    public Guid? ReplacementUnitId { get; set; }
}

internal sealed class RecipeRevisionRecord
{
    public Guid Id { get; set; }
    public Guid RecipeId { get; set; }
    public int RevisionNumber { get; set; }
    public DateTimeOffset PublishedAtUtc { get; set; }
    public Guid PublishedBy { get; set; }
    public string? ChangeNote { get; set; }
    public int SnapshotSchemaVersion { get; set; }
    public string SnapshotJson { get; set; } = string.Empty;
    public Guid? RestoredFromRevisionId { get; set; }
    public Guid? CentralSubmissionId { get; set; }
}

internal sealed class RecipeRevisionWarningRecord
{
    public Guid Id { get; set; }
    public Guid RecipeRevisionId { get; set; }
    public string WarningCode { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string ContextJson { get; set; } = string.Empty;
    public Guid? SnapshotPositionId { get; set; }
    public Guid? SnapshotReplacementId { get; set; }
    public int? ConflictType { get; set; }
    public Guid? ConflictId { get; set; }
    public Guid AcknowledgedBy { get; set; }
    public DateTimeOffset AcknowledgedAtUtc { get; set; }
}

internal sealed class TenantRecipeEntryRecord
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid? CentralRecipeRevisionId { get; set; }
    public Guid? TenantRecipeId { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public Guid UpdatedBy { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class CampRecipeEntryRecord
{
    public Guid Id { get; set; }
    public Guid CampId { get; set; }
    public Guid? UpstreamRecipeRevisionId { get; set; }
    public Guid? CampRecipeId { get; set; }
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public Guid UpdatedBy { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
}

internal sealed class CampRecipeNoteRecord
{
    public Guid Id { get; set; }
    public Guid CampRecipeEntryId { get; set; }
    public string Text { get; set; } = string.Empty;
    public Guid CreatedBy { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public Guid UpdatedBy { get; set; }
    public DateTimeOffset UpdatedAtUtc { get; set; }
    public Guid? DeletedBy { get; set; }
    public DateTimeOffset? DeletedAtUtc { get; set; }
}

internal enum CentralRecipeChangeSubmissionStatus
{
    Pending,
    Accepted,
    Rejected,
}

internal sealed class CentralRecipeChangeSubmissionRecord
{
    public Guid Id { get; set; }
    public Guid CentralRecipeId { get; set; }
    public Guid SourceCentralRevisionId { get; set; }
    public Guid SubmittedLocalRecipeRevisionId { get; set; }
    public int Status { get; set; }
    public Guid SubmittedBy { get; set; }
    public DateTimeOffset SubmittedAtUtc { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public Guid? ResultingCentralRevisionId { get; set; }
}

internal sealed class RecipeIngredientReplacementAllergenRecord
{
    public Guid ReplacementId { get; set; }
    public Guid AllergenId { get; set; }
}

internal sealed class RecipeIngredientReplacementIntoleranceRecord
{
    public Guid ReplacementId { get; set; }
    public Guid IntoleranceId { get; set; }
}

internal sealed class RecipeIngredientReplacementDietaryRequirementRecord
{
    public Guid ReplacementId { get; set; }
    public Guid DietaryRequirementId { get; set; }
}

internal sealed class RecipeSubrecipeReplacementAllergenRecord
{
    public Guid ReplacementId { get; set; }
    public Guid AllergenId { get; set; }
}

internal sealed class RecipeSubrecipeReplacementIntoleranceRecord
{
    public Guid ReplacementId { get; set; }
    public Guid IntoleranceId { get; set; }
}

internal sealed class RecipeSubrecipeReplacementDietaryRequirementRecord
{
    public Guid ReplacementId { get; set; }
    public Guid DietaryRequirementId { get; set; }
}
