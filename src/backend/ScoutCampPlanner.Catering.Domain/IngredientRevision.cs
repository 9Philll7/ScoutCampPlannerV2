namespace ScoutCampPlanner.Catering.Domain;

public enum IngredientRevisionState
{
    Draft,
    Published,
}

public enum IngredientPropertyReviewState
{
    Unreviewed,
    Reviewed,
}

public sealed class IngredientRevision
{
    private IngredientRevision() { }

    internal IngredientRevision(
        Guid id,
        Guid ingredientId,
        int revisionNumber,
        Guid? basedOnRevisionId,
        string name,
        Guid categoryId,
        Guid baseUnitId,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        Id = RequireId(id, nameof(id));
        IngredientId = RequireId(ingredientId, nameof(ingredientId));
        if (revisionNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(revisionNumber));
        RevisionNumber = revisionNumber;
        BasedOnRevisionId = basedOnRevisionId;
        CreatedBy = RequireId(createdBy, nameof(createdBy));
        CreatedAt = createdAt;
        UpdatedBy = CreatedBy;
        UpdatedAt = createdAt;
        (Name, NormalizedName) = CatalogName.Normalize(name, nameof(name), 200);
        CategoryId = RequireId(categoryId, nameof(categoryId));
        BaseUnitId = RequireId(baseUnitId, nameof(baseUnitId));
    }

    public Guid Id { get; private set; }
    public Guid IngredientId { get; private set; }
    public int RevisionNumber { get; private set; }
    public IngredientRevisionState State { get; private set; }
    public Guid? BasedOnRevisionId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public Guid CategoryId { get; private set; }
    public Guid BaseUnitId { get; private set; }
    public IngredientPropertyReviewState AllergenReviewState { get; private set; }
    public IngredientPropertyReviewState IntoleranceReviewState { get; private set; }
    public IngredientPropertyReviewState OriginReviewState { get; private set; }
    public long RowVersion { get; private set; } = 1;
    public DateTimeOffset CreatedAt { get; private set; }
    public Guid CreatedBy { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public Guid UpdatedBy { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public Guid? PublishedBy { get; private set; }

    public void SetContent(
        string name,
        Guid categoryId,
        Guid baseUnitId,
        Guid changedBy,
        DateTimeOffset changedAt)
    {
        EnsureDraft();
        (Name, NormalizedName) = CatalogName.Normalize(name, nameof(name), 200);
        CategoryId = RequireId(categoryId, nameof(categoryId));
        BaseUnitId = RequireId(baseUnitId, nameof(baseUnitId));
        MarkChanged(changedBy, changedAt);
    }

    public void SetReviewStates(
        IngredientPropertyReviewState allergens,
        IngredientPropertyReviewState intolerances,
        IngredientPropertyReviewState origins,
        Guid changedBy,
        DateTimeOffset changedAt)
    {
        EnsureDraft();
        if (!Enum.IsDefined(allergens) || !Enum.IsDefined(intolerances) || !Enum.IsDefined(origins))
            throw new ArgumentOutOfRangeException(nameof(allergens));
        AllergenReviewState = allergens;
        IntoleranceReviewState = intolerances;
        OriginReviewState = origins;
        MarkChanged(changedBy, changedAt);
    }

    internal void Publish(Guid publishedBy, DateTimeOffset publishedAt)
    {
        EnsureDraft();
        PublishedBy = RequireId(publishedBy, nameof(publishedBy));
        PublishedAt = publishedAt;
        UpdatedBy = PublishedBy.Value;
        UpdatedAt = publishedAt;
        RowVersion++;
        State = IngredientRevisionState.Published;
    }

    private void EnsureDraft()
    {
        if (State != IngredientRevisionState.Draft)
            throw new InvalidOperationException("Published ingredient revisions are immutable.");
    }

    private void MarkChanged(Guid changedBy, DateTimeOffset changedAt)
    {
        UpdatedBy = RequireId(changedBy, nameof(changedBy));
        UpdatedAt = changedAt;
        RowVersion++;
    }

    private static Guid RequireId(Guid value, string parameterName) =>
        value == Guid.Empty ? throw new ArgumentException("A non-empty ID is required.", parameterName) : value;
}
