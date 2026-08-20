namespace ScoutCampPlanner.Catering.Domain;

public sealed class RecipeDraft
{
    private readonly List<RecipeIngredientGroup> groups = [];
    private readonly List<RecipeIngredientPosition> ingredientPositions = [];
    private readonly List<RecipeSubrecipePosition> subrecipePositions = [];
    private readonly HashSet<string> tags = new(StringComparer.Ordinal);

    public RecipeDraft(Guid id, RecipeScopeType scopeType, Guid? scopeId, RecipeType recipeType, string? name = null)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Recipe ID is required.", nameof(id)) : id;
        ValidateScope(scopeType, scopeId);
        if (!Enum.IsDefined(recipeType)) throw new ArgumentOutOfRangeException(nameof(recipeType));
        ScopeType = scopeType;
        ScopeId = scopeId;
        RecipeType = recipeType;
        SetName(name);
    }

    public Guid Id { get; }
    public RecipeScopeType ScopeType { get; }
    public Guid? ScopeId { get; }
    public RecipeStatus Status { get; private set; } = RecipeStatus.Draft;
    public RecipeType RecipeType { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? Source { get; private set; }
    public string? InternalNotes { get; private set; }
    public decimal? ReferenceServings { get; private set; }
    public decimal? ReferenceQuantity { get; private set; }
    public Guid? ReferenceUnitId { get; private set; }
    public bool? DefaultAgeGroupScalingApplies { get; private set; }
    public AuthoringStageSnapshot? AuthoringStage { get; private set; }
    public long DraftVersion { get; private set; }
    public IReadOnlyList<RecipeIngredientGroup> Groups => groups.AsReadOnly();
    public IReadOnlyList<RecipeIngredientPosition> IngredientPositions => ingredientPositions.AsReadOnly();
    public IReadOnlyList<RecipeSubrecipePosition> SubrecipePositions => subrecipePositions.AsReadOnly();
    public IReadOnlySet<string> Tags => new HashSet<string>(tags, StringComparer.Ordinal);

    public void SetName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Name = string.Empty;
            NormalizedName = string.Empty;
            return;
        }

        (Name, NormalizedName) = CatalogName.Normalize(value, nameof(value), 200);
    }

    public void SetDetails(string? description, string? source, string? internalNotes)
    {
        Description = NormalizeOptional(description);
        Source = NormalizeOptional(source);
        InternalNotes = NormalizeOptional(internalNotes);
    }

    public void ConfigurePortionReference(
        decimal? referenceServings,
        bool? defaultAgeGroupScalingApplies,
        AuthoringStageSnapshot? authoringStage = null)
    {
        RecipeType = RecipeType.PortionBased;
        ReferenceServings = referenceServings;
        DefaultAgeGroupScalingApplies = defaultAgeGroupScalingApplies;
        AuthoringStage = authoringStage;
        ReferenceQuantity = null;
        ReferenceUnitId = null;
    }

    public void ConfigureQuantityReference(decimal? referenceQuantity, Guid? referenceUnitId)
    {
        RecipeType = RecipeType.QuantityBased;
        ReferenceQuantity = referenceQuantity;
        ReferenceUnitId = referenceUnitId;
        ReferenceServings = null;
        DefaultAgeGroupScalingApplies = null;
        AuthoringStage = null;
    }

    public void ReplaceTags(IEnumerable<string> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        tags.Clear();
        foreach (string value in values)
        {
            (_, string normalized) = CatalogName.Normalize(value, nameof(values), 100);
            tags.Add(normalized);
        }
    }

    public void AddGroup(RecipeIngredientGroup group)
    {
        ArgumentNullException.ThrowIfNull(group);
        EnsureRecipe(group.RecipeId);
        EnsureUniqueId(group.Id, groups.Select(value => value.Id));
        groups.Add(group);
    }

    public void AddIngredientPosition(RecipeIngredientPosition position)
    {
        ArgumentNullException.ThrowIfNull(position);
        EnsureRecipe(position.RecipeId);
        EnsureKnownGroup(position.GroupId);
        EnsureUniqueId(position.Id, ingredientPositions.Select(value => value.Id));
        ingredientPositions.Add(position);
    }

    public void AddSubrecipePosition(RecipeSubrecipePosition position)
    {
        ArgumentNullException.ThrowIfNull(position);
        EnsureRecipe(position.RecipeId);
        EnsureKnownGroup(position.GroupId);
        EnsureUniqueId(position.Id, subrecipePositions.Select(value => value.Id));
        subrecipePositions.Add(position);
    }

    public void SetPersistedVersion(long version)
    {
        if (version < 0) throw new ArgumentOutOfRangeException(nameof(version));
        DraftVersion = version;
    }

    private void EnsureKnownGroup(Guid? groupId)
    {
        if (groupId.HasValue && groups.All(value => value.Id != groupId))
            throw new InvalidOperationException("Position group does not belong to this recipe draft.");
    }

    private void EnsureRecipe(Guid recipeId)
    {
        if (recipeId != Id) throw new InvalidOperationException("Child entity belongs to another recipe draft.");
    }

    private static void EnsureUniqueId(Guid id, IEnumerable<Guid> existingIds)
    {
        if (existingIds.Contains(id)) throw new InvalidOperationException("A child entity with this ID already exists.");
    }

    private static string? NormalizeOptional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void ValidateScope(RecipeScopeType scopeType, Guid? scopeId)
    {
        if (!Enum.IsDefined(scopeType)) throw new ArgumentOutOfRangeException(nameof(scopeType));
        if (scopeType == RecipeScopeType.Central && scopeId is not null)
            throw new ArgumentException("A central recipe must not have an owner ID.", nameof(scopeId));
        if (scopeType != RecipeScopeType.Central && (!scopeId.HasValue || scopeId == Guid.Empty))
            throw new ArgumentException("A tenant or camp recipe requires an owner ID.", nameof(scopeId));
    }
}
