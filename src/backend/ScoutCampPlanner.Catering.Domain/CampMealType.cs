namespace ScoutCampPlanner.Catering.Domain;

public sealed class CampMealType
{
    private CampMealType() { }

    public CampMealType(Guid id, Guid campId, string name, int sortOrder)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Meal type ID is required.", nameof(id)) : id;
        CampId = campId == Guid.Empty ? throw new ArgumentException("Camp ID is required.", nameof(campId)) : campId;
        Rename(name);
        SortOrder = sortOrder >= 0 ? sortOrder : throw new ArgumentOutOfRangeException(nameof(sortOrder));
    }

    public Guid Id { get; private set; }
    public Guid CampId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }

    public void Rename(string name)
    {
        Name = string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100
            ? throw new ArgumentException("A meal type name with at most 100 characters is required.", nameof(name))
            : name.Trim();
        NormalizedName = Name.ToUpperInvariant();
    }

    public void Update(string name, int sortOrder)
    {
        Rename(name);
        SortOrder = sortOrder >= 0 ? sortOrder : throw new ArgumentOutOfRangeException(nameof(sortOrder));
    }
}
