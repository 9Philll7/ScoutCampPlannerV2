namespace ScoutCampPlanner.Catering.Domain;

public sealed class RecipeSubrecipePosition
{
    private readonly List<RecipeReplacementRule> replacementRules = [];

    public RecipeSubrecipePosition(
        Guid id,
        Guid recipeId,
        Guid? groupId,
        Guid? recipeRevisionId,
        decimal? requiredServings,
        decimal? requiredQuantity,
        Guid? requiredUnitId,
        int sortOrder)
    {
        Id = Required(id, nameof(id));
        RecipeId = Required(recipeId, nameof(recipeId));
        GroupId = groupId;
        RecipeRevisionId = recipeRevisionId;
        RequiredServings = requiredServings;
        RequiredQuantity = requiredQuantity;
        RequiredUnitId = requiredUnitId;
        SortOrder = sortOrder;
    }

    public Guid Id { get; }
    public Guid RecipeId { get; }
    public Guid? GroupId { get; private set; }
    public Guid? RecipeRevisionId { get; private set; }
    public decimal? RequiredServings { get; private set; }
    public decimal? RequiredQuantity { get; private set; }
    public Guid? RequiredUnitId { get; private set; }
    public int SortOrder { get; private set; }
    public IReadOnlyList<RecipeReplacementRule> ReplacementRules => replacementRules.AsReadOnly();

    public void AddReplacementRule(RecipeReplacementRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (rule.SubrecipePositionId != Id)
            throw new InvalidOperationException("Replacement rule belongs to another subrecipe position.");
        if (replacementRules.Any(value => value.Id == rule.Id))
            throw new InvalidOperationException("A replacement rule with this ID already exists.");
        replacementRules.Add(rule);
    }

    private static Guid Required(Guid value, string parameterName) =>
        value == Guid.Empty ? throw new ArgumentException("ID is required.", parameterName) : value;
}

public sealed class RecipeReplacementRule
{
    private readonly HashSet<ConflictReference> conflicts;

    public RecipeReplacementRule(
        Guid id,
        Guid subrecipePositionId,
        Guid? replacementRecipeRevisionId,
        decimal? replacementServings,
        decimal? replacementQuantity,
        Guid? replacementUnitId,
        IEnumerable<ConflictReference>? conflicts = null)
    {
        Id = Required(id, nameof(id));
        SubrecipePositionId = Required(subrecipePositionId, nameof(subrecipePositionId));
        ReplacementRecipeRevisionId = replacementRecipeRevisionId;
        ReplacementServings = replacementServings;
        ReplacementQuantity = replacementQuantity;
        ReplacementUnitId = replacementUnitId;
        this.conflicts = conflicts?.ToHashSet() ?? [];
    }

    public Guid Id { get; }
    public Guid SubrecipePositionId { get; }
    public Guid? ReplacementRecipeRevisionId { get; private set; }
    public decimal? ReplacementServings { get; private set; }
    public decimal? ReplacementQuantity { get; private set; }
    public Guid? ReplacementUnitId { get; private set; }
    public IReadOnlySet<ConflictReference> Conflicts => new HashSet<ConflictReference>(conflicts);

    private static Guid Required(Guid value, string parameterName) =>
        value == Guid.Empty ? throw new ArgumentException("ID is required.", parameterName) : value;
}
