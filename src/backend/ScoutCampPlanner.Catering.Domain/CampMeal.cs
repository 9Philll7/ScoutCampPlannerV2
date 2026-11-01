namespace ScoutCampPlanner.Catering.Domain;

public sealed class CampMeal
{
    private CampMeal() { }

    public CampMeal(Guid id, Guid campId, Guid mealTypeId, DateOnly date, bool isActive = true, int changeVersion = 0, int sortOrder = 0)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Meal ID is required.", nameof(id)) : id;
        CampId = campId == Guid.Empty ? throw new ArgumentException("Camp ID is required.", nameof(campId)) : campId;
        MealTypeId = mealTypeId == Guid.Empty ? throw new ArgumentException("Meal type ID is required.", nameof(mealTypeId)) : mealTypeId;
        Date = date;
        SetSortOrder(sortOrder);
        IsActive = isActive;
        ChangeVersion = changeVersion >= 0 ? changeVersion : throw new ArgumentOutOfRangeException(nameof(changeVersion));
    }

    public Guid Id { get; private set; }
    public Guid CampId { get; private set; }
    public Guid MealTypeId { get; private set; }
    public DateOnly Date { get; private set; }
    public bool IsActive { get; private set; }
    public int ChangeVersion { get; private set; }

    public int SortOrder { get; private set; }

    public void SetSortOrder(int value) => SortOrder = value >= 0 ? value : throw new ArgumentOutOfRangeException(nameof(value));

    public void SetActive(bool isActive)
    {
        if (IsActive == isActive) return;
        IsActive = isActive;
        ChangeVersion = checked(ChangeVersion + 1);
    }
}
