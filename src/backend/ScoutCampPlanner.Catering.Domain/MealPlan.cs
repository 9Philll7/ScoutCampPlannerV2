namespace ScoutCampPlanner.Catering.Domain;

public sealed class MealPlan
{
    private MealPlan() { }
    public MealPlan(Guid id, Guid campId, string name, int sortOrder = 0, int version = 0)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Meal plan ID is required.", nameof(id)) : id;
        CampId = campId == Guid.Empty ? throw new ArgumentException("Camp ID is required.", nameof(campId)) : campId;
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Name is required.", nameof(name)) : name.Trim();
        if (sortOrder < 0) throw new ArgumentOutOfRangeException(nameof(sortOrder));
        if (version < 0) throw new ArgumentOutOfRangeException(nameof(version));
        SortOrder = sortOrder;
        Version = version;
    }
    public Guid Id { get; private set; }
    public Guid CampId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
    public int Version { get; private set; }

    public void Rename(string name) => Name = string.IsNullOrWhiteSpace(name)
        ? throw new ArgumentException("Name is required.", nameof(name))
        : name.Trim();

    public void SetSortOrder(int sortOrder) => SortOrder = sortOrder >= 0
        ? sortOrder
        : throw new ArgumentOutOfRangeException(nameof(sortOrder));

    public int CommitVersion() => Version = checked(Version + 1);
}
