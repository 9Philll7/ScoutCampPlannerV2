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
    private DietaryRequirement() { }

    public DietaryRequirement(Guid id, string name)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Conflict entry ID is required.", nameof(id)) : id;
        (Name, NormalizedName) = CatalogName.Normalize(name, nameof(name), 100);
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
}
