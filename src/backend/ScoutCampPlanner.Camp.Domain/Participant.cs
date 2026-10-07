namespace ScoutCampPlanner.Camp.Domain;

/// <summary>Minimal camp-owned participant; requirements never belong to Catering master data.</summary>
public sealed class Participant
{
    private readonly List<ParticipantAbsentDay> absentDays = [];
    private readonly List<ParticipantAbsentMeal> absentMeals = [];
    private readonly List<ParticipantAllergen> allergens = [];
    private readonly List<ParticipantIntolerance> intolerances = [];

    private Participant() { }

    public Participant(Guid id, Guid campId, string displayName)
    {
        Id = Required(id);
        CampId = Required(campId);
        Rename(displayName);
    }

    public Guid Id { get; private set; }
    public Guid CampId { get; private set; }
    public Guid? StructureNodeId { get; private set; }
    public void AssignStructureNode(Guid? nodeId)
    {
        if (nodeId == Guid.Empty) throw new ArgumentException("Structure node ID is invalid.", nameof(nodeId));
        StructureNodeId = nodeId;
    }
    public string DisplayName { get; private set; } = string.Empty;
    public Guid? DietTypeId { get; private set; }
    public IReadOnlyCollection<ParticipantAbsentDay> AbsentDays => absentDays.AsReadOnly();
    public IReadOnlyCollection<ParticipantAbsentMeal> AbsentMeals => absentMeals.AsReadOnly();
    public IReadOnlyCollection<ParticipantAllergen> Allergens => allergens.AsReadOnly();
    public IReadOnlyCollection<ParticipantIntolerance> Intolerances => intolerances.AsReadOnly();

    public void Rename(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (displayName.Trim().Length > 200) throw new ArgumentException("Display name is too long.", nameof(displayName));
        DisplayName = displayName.Trim();
    }

    public void SetDayAbsent(DateOnly date, bool absent)
    {
        if (!absent) absentDays.RemoveAll(value => value.Date == date);
        else if (absentDays.All(value => value.Date != date)) absentDays.Add(new ParticipantAbsentDay(date));
    }

    // The caller resolves the dated meal through the existing module contract.
    public void SetMealAbsent(Guid mealId, bool absent)
    {
        Required(mealId);
        if (!absent) absentMeals.RemoveAll(value => value.MealId == mealId);
        else if (absentMeals.All(value => value.MealId != mealId)) absentMeals.Add(new ParticipantAbsentMeal(mealId));
    }

    public bool IsPresent(DateOnly date, Guid mealId) =>
        absentDays.All(value => value.Date != date) && absentMeals.All(value => value.MealId != mealId);

    public void SetRequirements(Guid? dietTypeId, IEnumerable<Guid> allergenIds,
        IEnumerable<ParticipantIntolerance> requirements)
    {
        if (dietTypeId == Guid.Empty) throw new ArgumentException("Diet type ID is invalid.", nameof(dietTypeId));
        ArgumentNullException.ThrowIfNull(allergenIds);
        ArgumentNullException.ThrowIfNull(requirements);
        ParticipantAllergen[] nextAllergens = allergenIds.Distinct().Select(id => new ParticipantAllergen(Required(id))).ToArray();
        ParticipantIntolerance[] nextIntolerances = requirements.ToArray();
        if (nextIntolerances.Any(value => value is null) ||
            nextIntolerances.Select(value => value.SubstanceId).Distinct().Count() != nextIntolerances.Length)
            throw new ArgumentException("Intolerance references must be unique.", nameof(requirements));
        // Validate the full replacement before changing any current value.
        DietTypeId = dietTypeId;
        allergens.Clear();
        allergens.AddRange(nextAllergens);
        intolerances.Clear();
        intolerances.AddRange(nextIntolerances);
    }

    private static Guid Required(Guid id) => id == Guid.Empty
        ? throw new ArgumentException("ID is required.", nameof(id)) : id;
}

public sealed class ParticipantAbsentDay
{
    private ParticipantAbsentDay() { }
    public ParticipantAbsentDay(DateOnly date) => Date = date;
    public DateOnly Date { get; private set; }
}

public sealed class ParticipantAbsentMeal
{
    private ParticipantAbsentMeal() { }
    public ParticipantAbsentMeal(Guid mealId)
    {
        if (mealId == Guid.Empty) throw new ArgumentException("Meal ID is required.", nameof(mealId));
        MealId = mealId;
    }
    public Guid MealId { get; private set; }
}

public sealed class ParticipantAllergen
{
    private ParticipantAllergen() { }
    public ParticipantAllergen(Guid allergenId)
    {
        if (allergenId == Guid.Empty) throw new ArgumentException("Allergen ID is required.", nameof(allergenId));
        AllergenId = allergenId;
    }
    public Guid AllergenId { get; private set; }
}

/// <summary>Threshold is stored canonically in grams per portion, never on the ingredient.</summary>
public sealed class ParticipantIntolerance
{
    private ParticipantIntolerance() { }
    public ParticipantIntolerance(Guid substanceId, decimal? thresholdGramsPerPortion, string? thresholdSource)
    {
        if (substanceId == Guid.Empty) throw new ArgumentException("Substance ID is required.", nameof(substanceId));
        if (thresholdGramsPerPortion is < 0) throw new ArgumentOutOfRangeException(nameof(thresholdGramsPerPortion));
        // Match the common numeric(18,6) persistence boundary without provider-specific rounding.
        if (thresholdGramsPerPortion is decimal threshold &&
            (threshold > 999999999999.999999m || decimal.Round(threshold, 6) != threshold))
            throw new ArgumentOutOfRangeException(nameof(thresholdGramsPerPortion),
                "Threshold must fit 12 integer and 6 fractional digits without rounding.");
        if (thresholdSource?.Length > 500) throw new ArgumentException("Threshold source is too long.", nameof(thresholdSource));
        SubstanceId = substanceId;
        ThresholdGramsPerPortion = thresholdGramsPerPortion;
        ThresholdSource = string.IsNullOrWhiteSpace(thresholdSource) ? null : thresholdSource.Trim();
    }
    public Guid SubstanceId { get; private set; }
    public decimal? ThresholdGramsPerPortion { get; private set; }
    public string? ThresholdSource { get; private set; }
}
