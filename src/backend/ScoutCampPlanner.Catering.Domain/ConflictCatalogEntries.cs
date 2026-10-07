namespace ScoutCampPlanner.Catering.Domain;

public sealed class Allergen
{
    private Allergen() { }

    public Allergen(Guid id, string name)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Conflict entry ID is required.", nameof(id)) : id;
        (Name, NormalizedName) = CatalogName.Normalize(name, nameof(name), 100);
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
}

public sealed class Intolerance
{
    private Intolerance() { }

    public Intolerance(Guid id, string name)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Conflict entry ID is required.", nameof(id)) : id;
        (Name, NormalizedName) = CatalogName.Normalize(name, nameof(name), 100);
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
}

public sealed class DietaryRequirement
{
    private readonly List<DietaryOriginRule> originRules = [];
    private DietaryRequirement() { }

    public DietaryRequirement(Guid id, string name, Guid? tenantId = null)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Conflict entry ID is required.", nameof(id)) : id;
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is invalid.", nameof(tenantId));
        TenantId = tenantId;
        (Name, NormalizedName) = CatalogName.Normalize(name, nameof(name), 100);
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public Guid? TenantId { get; private set; }
    public string? Description { get; private set; }
    public int SortOrder { get; private set; }
    public int Version { get; private set; }
    public IReadOnlyCollection<DietaryOriginRule> OriginRules => originRules.AsReadOnly();

    public void Revise(string name, string? description, int sortOrder, IEnumerable<DietaryOriginRule> rules)
    {
        var normalized = CatalogName.Normalize(name, nameof(name), 100);
        if (description?.Length > 2000 || sortOrder < 0) throw new ArgumentException("Invalid dietary requirement metadata.");
        var next = rules.ToArray();
        if (next.Any(value => value is null) || next.Select(value => value.OriginId).Distinct().Count() != next.Length)
            throw new ArgumentException("Origin rules must be unique.", nameof(rules));
        (Name, NormalizedName) = normalized;
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        SortOrder = sortOrder;
        originRules.Clear(); originRules.AddRange(next);
        Version++;
    }

    public DietaryOriginDecision? DecisionFor(Guid originId) =>
        originRules.SingleOrDefault(value => value.OriginId == originId)?.Decision;

    public static DietaryRequirement Restore(Guid id, string name, Guid? tenantId, string? description,
        int sortOrder, int version, IEnumerable<DietaryOriginRule> rules)
    {
        if (version < 0) throw new ArgumentOutOfRangeException(nameof(version));
        var result = new DietaryRequirement(id, name, tenantId);
        result.Revise(name, description, sortOrder, rules);
        result.Version = version;
        return result;
    }
}

public enum DietaryOriginDecision { Allowed, Excluded }

public sealed class DietaryOriginRule
{
    private DietaryOriginRule() { }
    public DietaryOriginRule(Guid originId, DietaryOriginDecision decision)
    {
        if (originId == Guid.Empty || !Enum.IsDefined(decision)) throw new ArgumentException("Invalid origin rule.");
        OriginId = originId; Decision = decision;
    }
    public Guid OriginId { get; private set; }
    public DietaryOriginDecision Decision { get; private set; }
}
