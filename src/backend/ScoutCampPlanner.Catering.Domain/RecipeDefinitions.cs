namespace ScoutCampPlanner.Catering.Domain;

public enum RecipeScopeType
{
    Central,
    Tenant,
    Camp,
}

public enum RecipeStatus
{
    Draft,
    Active,
    Archived,
}

public enum RecipeType
{
    PortionBased,
    QuantityBased,
}

public enum ScalingMode
{
    Linear,
    Fixed,
    Stepwise,
}

public enum AgeGroupScalingMode
{
    Inherit,
    Apply,
    Ignore,
}

public enum ConflictType
{
    Allergen,
    Intolerance,
    DietaryRequirement,
}

public readonly record struct ConflictReference
{
    public ConflictReference(ConflictType type, Guid id)
    {
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        Type = type;
        Id = id == Guid.Empty ? throw new ArgumentException("Conflict ID is required.", nameof(id)) : id;
    }

    public ConflictType Type { get; }
    public Guid Id { get; }
}

public sealed record AuthoringStageSnapshot
{
    public AuthoringStageSnapshot(Guid stageId, string stageName, decimal factor)
    {
        StageId = stageId == Guid.Empty ? throw new ArgumentException("Stage ID is required.", nameof(stageId)) : stageId;
        StageName = string.IsNullOrWhiteSpace(stageName)
            ? throw new ArgumentException("Stage name is required.", nameof(stageName))
            : stageName.Trim();
        Factor = factor > 0 ? factor : throw new ArgumentOutOfRangeException(nameof(factor));
    }

    public Guid StageId { get; }
    public string StageName { get; }
    public decimal Factor { get; }
}
