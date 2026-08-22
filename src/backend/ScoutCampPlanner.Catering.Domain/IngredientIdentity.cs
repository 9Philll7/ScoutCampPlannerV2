namespace ScoutCampPlanner.Catering.Domain;

public enum IngredientIdentityStatus
{
    Active,
    Archived,
}

public sealed class IngredientIdentity
{
    private readonly List<IngredientRevision> revisions = [];

    private IngredientIdentity() { }

    private IngredientIdentity(
        Guid id,
        IngredientScopeType scopeType,
        Guid? scopeId,
        Guid? sourceIngredientId,
        Guid? sourceRevisionId)
    {
        Id = RequireId(id, nameof(id));
        ValidateScope(scopeType, scopeId);
        if (sourceIngredientId.HasValue != sourceRevisionId.HasValue)
            throw new ArgumentException("Fork source ingredient and revision must be provided together.");
        ScopeType = scopeType;
        ScopeId = scopeId;
        SourceIngredientId = sourceIngredientId;
        SourceRevisionId = sourceRevisionId;
    }

    public Guid Id { get; private set; }
    public IngredientScopeType ScopeType { get; private set; }
    public Guid? ScopeId { get; private set; }
    public Guid? SourceIngredientId { get; private set; }
    public Guid? SourceRevisionId { get; private set; }
    public Guid? CurrentPublishedRevisionId { get; private set; }
    public IngredientIdentityStatus Status { get; private set; }
    public IReadOnlyCollection<IngredientRevision> Revisions => revisions.AsReadOnly();

    public static IngredientIdentity CreateCentral(Guid id) =>
        new(id, IngredientScopeType.Central, null, null, null);

    public static IngredientIdentity CreateLocal(Guid id, IngredientScopeType scopeType, Guid scopeId)
    {
        EnsureLocalScope(scopeType);
        return new IngredientIdentity(id, scopeType, scopeId, null, null);
    }

    public static IngredientIdentity ForkCentral(
        Guid id,
        IngredientScopeType scopeType,
        Guid scopeId,
        IngredientIdentity centralIngredient,
        IngredientRevision sourceRevision,
        Guid draftId,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        ArgumentNullException.ThrowIfNull(centralIngredient);
        ArgumentNullException.ThrowIfNull(sourceRevision);
        EnsureLocalScope(scopeType);
        if (centralIngredient.ScopeType != IngredientScopeType.Central)
            throw new ArgumentException("Only a central ingredient can be forked.", nameof(centralIngredient));
        if (sourceRevision.IngredientId != centralIngredient.Id ||
            sourceRevision.State != IngredientRevisionState.Published)
            throw new ArgumentException("The fork source must be a published revision of the central ingredient.", nameof(sourceRevision));

        var fork = new IngredientIdentity(
            id, scopeType, scopeId, centralIngredient.Id, sourceRevision.Id);
        IngredientRevision draft = fork.CreateDraft(
            draftId,
            sourceRevision.Name,
            sourceRevision.CategoryId,
            sourceRevision.BaseUnitId,
            createdBy,
            createdAt,
            sourceRevision.Id);
        draft.CopyRevisionDetailsFrom(sourceRevision);
        return fork;
    }

    public IngredientRevision CreateDraft(
        Guid revisionId,
        string name,
        Guid categoryId,
        Guid baseUnitId,
        Guid createdBy,
        DateTimeOffset createdAt,
        Guid? basedOnRevisionId = null)
    {
        EnsureActive();
        if (revisions.Any(value => value.State == IngredientRevisionState.Draft))
            throw new InvalidOperationException("An ingredient can have only one draft.");
        if (basedOnRevisionId.HasValue && revisions.All(value => value.Id != basedOnRevisionId) &&
            basedOnRevisionId != SourceRevisionId)
            throw new ArgumentException("The base revision is not part of this ingredient or its fork source.", nameof(basedOnRevisionId));

        var draft = new IngredientRevision(
            revisionId,
            Id,
            revisions.Count == 0 ? 1 : revisions.Max(value => value.RevisionNumber) + 1,
            basedOnRevisionId,
            name,
            categoryId,
            baseUnitId,
            createdBy,
            createdAt);
        revisions.Add(draft);
        return draft;
    }

    public IngredientRevision CreateDraftFromPublished(
        Guid revisionId,
        Guid createdBy,
        DateTimeOffset createdAt)
    {
        IngredientRevision published = GetCurrentPublished();
        IngredientRevision draft = CreateDraft(
            revisionId,
            published.Name,
            published.CategoryId,
            published.BaseUnitId,
            createdBy,
            createdAt,
            published.Id);
        draft.CopyRevisionDetailsFrom(published);
        return draft;
    }

    public void PublishDraft(Guid draftId, Guid publishedBy, DateTimeOffset publishedAt)
    {
        EnsureActive();
        IngredientRevision draft = revisions.SingleOrDefault(value => value.Id == draftId)
            ?? throw new ArgumentException("The draft does not belong to this ingredient.", nameof(draftId));
        if (draft.State != IngredientRevisionState.Draft)
            throw new InvalidOperationException("Only a draft can be published.");
        draft.Publish(publishedBy, publishedAt);
        CurrentPublishedRevisionId = draft.Id;
    }

    public void Archive() => Status = IngredientIdentityStatus.Archived;

    private IngredientRevision GetCurrentPublished() =>
        CurrentPublishedRevisionId.HasValue
            ? revisions.Single(value => value.Id == CurrentPublishedRevisionId.Value)
            : throw new InvalidOperationException("The ingredient has no published revision.");

    private void EnsureActive()
    {
        if (Status == IngredientIdentityStatus.Archived)
            throw new InvalidOperationException("An archived ingredient cannot be changed.");
    }

    private static void EnsureLocalScope(IngredientScopeType scopeType)
    {
        if (scopeType is not (IngredientScopeType.Tenant or IngredientScopeType.Camp))
            throw new ArgumentException("A local ingredient must use tenant or camp scope.", nameof(scopeType));
    }

    private static void ValidateScope(IngredientScopeType scopeType, Guid? scopeId)
    {
        if (!Enum.IsDefined(scopeType))
            throw new ArgumentOutOfRangeException(nameof(scopeType));
        if (scopeType == IngredientScopeType.Central && scopeId.HasValue)
            throw new ArgumentException("A central ingredient must not have an owner ID.", nameof(scopeId));
        if (scopeType != IngredientScopeType.Central && (!scopeId.HasValue || scopeId == Guid.Empty))
            throw new ArgumentException("A tenant or camp ingredient requires an owner ID.", nameof(scopeId));
    }

    private static Guid RequireId(Guid value, string parameterName) =>
        value == Guid.Empty ? throw new ArgumentException("A non-empty ID is required.", parameterName) : value;
}
