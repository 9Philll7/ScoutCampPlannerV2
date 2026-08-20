namespace ScoutCampPlanner.Catering.Domain;

public sealed class RecipeIngredientGroup
{
    public RecipeIngredientGroup(Guid id, Guid recipeId, string? name, int sortOrder)
    {
        Id = Required(id, nameof(id));
        RecipeId = Required(recipeId, nameof(recipeId));
        SetName(name);
        SortOrder = sortOrder;
    }

    public Guid Id { get; }
    public Guid RecipeId { get; }
    public string Name { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }

    public void Update(string? name, int sortOrder)
    {
        SetName(name);
        SortOrder = sortOrder;
    }

    private void SetName(string? value) => Name = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    private static Guid Required(Guid value, string parameterName) =>
        value == Guid.Empty ? throw new ArgumentException("ID is required.", parameterName) : value;
}

public sealed record StepwiseScaling(decimal? StepSize, decimal? QuantityPerStep);

public sealed class RecipeIngredientPosition
{
    private readonly List<IngredientReplacementRule> replacementRules = [];

    public RecipeIngredientPosition(
        Guid id,
        Guid recipeId,
        Guid? groupId,
        Guid? baseIngredientId,
        decimal? quantity,
        Guid? unitId,
        int sortOrder,
        ScalingMode scalingMode = ScalingMode.Linear,
        AgeGroupScalingMode ageGroupScaling = AgeGroupScalingMode.Inherit,
        StepwiseScaling? stepwiseScaling = null)
    {
        Id = Required(id, nameof(id));
        RecipeId = Required(recipeId, nameof(recipeId));
        GroupId = groupId;
        BaseIngredientId = baseIngredientId;
        Quantity = quantity;
        UnitId = unitId;
        SortOrder = sortOrder;
        ScalingMode = scalingMode;
        AgeGroupScaling = ageGroupScaling;
        StepwiseScaling = stepwiseScaling;
    }

    public Guid Id { get; }
    public Guid RecipeId { get; }
    public Guid? GroupId { get; private set; }
    public Guid? BaseIngredientId { get; private set; }
    public decimal? Quantity { get; private set; }
    public Guid? UnitId { get; private set; }
    public int SortOrder { get; private set; }
    public ScalingMode ScalingMode { get; private set; }
    public AgeGroupScalingMode AgeGroupScaling { get; private set; }
    public StepwiseScaling? StepwiseScaling { get; private set; }
    public IReadOnlyList<IngredientReplacementRule> ReplacementRules => replacementRules.AsReadOnly();

    public void AddReplacementRule(IngredientReplacementRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        if (rule.IngredientPositionId != Id)
            throw new InvalidOperationException("Replacement rule belongs to another ingredient position.");
        if (replacementRules.Any(value => value.Id == rule.Id))
            throw new InvalidOperationException("A replacement rule with this ID already exists.");
        replacementRules.Add(rule);
    }

    private static Guid Required(Guid value, string parameterName) =>
        value == Guid.Empty ? throw new ArgumentException("ID is required.", parameterName) : value;
}

public sealed class IngredientReplacementRule
{
    private readonly HashSet<ConflictReference> conflicts;

    public IngredientReplacementRule(
        Guid id,
        Guid ingredientPositionId,
        Guid? replacementBaseIngredientId,
        decimal? replacementQuantity,
        Guid? replacementUnitId,
        IEnumerable<ConflictReference>? conflicts = null)
    {
        Id = Required(id, nameof(id));
        IngredientPositionId = Required(ingredientPositionId, nameof(ingredientPositionId));
        ReplacementBaseIngredientId = replacementBaseIngredientId;
        ReplacementQuantity = replacementQuantity;
        ReplacementUnitId = replacementUnitId;
        this.conflicts = conflicts?.ToHashSet() ?? [];
    }

    public Guid Id { get; }
    public Guid IngredientPositionId { get; }
    public Guid? ReplacementBaseIngredientId { get; private set; }
    public decimal? ReplacementQuantity { get; private set; }
    public Guid? ReplacementUnitId { get; private set; }
    public IReadOnlySet<ConflictReference> Conflicts => new HashSet<ConflictReference>(conflicts);

    private static Guid Required(Guid value, string parameterName) =>
        value == Guid.Empty ? throw new ArgumentException("ID is required.", parameterName) : value;
}
