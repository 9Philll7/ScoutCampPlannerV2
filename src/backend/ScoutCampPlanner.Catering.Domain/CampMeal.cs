namespace ScoutCampPlanner.Catering.Domain;

public sealed class CampMeal
{
    private CampMeal() { }

    public CampMeal(Guid id, Guid campId, Guid mealTypeId, DateOnly date, bool isActive = true)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Meal ID is required.", nameof(id)) : id;
        CampId = campId == Guid.Empty ? throw new ArgumentException("Camp ID is required.", nameof(campId)) : campId;
        MealTypeId = mealTypeId == Guid.Empty ? throw new ArgumentException("Meal type ID is required.", nameof(mealTypeId)) : mealTypeId;
        Date = date;
        IsActive = isActive;
    }

    public Guid Id { get; private set; }
    public Guid CampId { get; private set; }
    public Guid MealTypeId { get; private set; }
    public DateOnly Date { get; private set; }
    public bool IsActive { get; private set; }

    public void SetActive(bool isActive) => IsActive = isActive;
}
