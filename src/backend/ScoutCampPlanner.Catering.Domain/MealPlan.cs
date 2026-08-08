namespace ScoutCampPlanner.Catering.Domain;

public sealed class MealPlan
{
    private MealPlan() { }
    public MealPlan(Guid id, Guid campId, string name)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Meal plan ID is required.", nameof(id)) : id;
        CampId = campId == Guid.Empty ? throw new ArgumentException("Camp ID is required.", nameof(campId)) : campId;
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Name is required.", nameof(name)) : name.Trim();
    }
    public Guid Id { get; private set; }
    public Guid CampId { get; private set; }
    public string Name { get; private set; } = string.Empty;

    public void Rename(string name) => Name = string.IsNullOrWhiteSpace(name)
        ? throw new ArgumentException("Name is required.", nameof(name))
        : name.Trim();
}
