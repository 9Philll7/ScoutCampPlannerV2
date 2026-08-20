namespace ScoutCampPlanner.Catering.Domain;

public sealed class BaseIngredientAllergen
{
    private BaseIngredientAllergen() { }
    public BaseIngredientAllergen(Guid baseIngredientId, Guid allergenId)
    {
        BaseIngredientId = Required(baseIngredientId, nameof(baseIngredientId));
        AllergenId = Required(allergenId, nameof(allergenId));
    }

    public Guid BaseIngredientId { get; private set; }
    public Guid AllergenId { get; private set; }

    private static Guid Required(Guid value, string parameterName) =>
        value == Guid.Empty ? throw new ArgumentException("ID is required.", parameterName) : value;
}

public sealed class BaseIngredientIntolerance
{
    private BaseIngredientIntolerance() { }
    public BaseIngredientIntolerance(Guid baseIngredientId, Guid intoleranceId)
    {
        BaseIngredientId = Required(baseIngredientId, nameof(baseIngredientId));
        IntoleranceId = Required(intoleranceId, nameof(intoleranceId));
    }

    public Guid BaseIngredientId { get; private set; }
    public Guid IntoleranceId { get; private set; }

    private static Guid Required(Guid value, string parameterName) =>
        value == Guid.Empty ? throw new ArgumentException("ID is required.", parameterName) : value;
}

public sealed class BaseIngredientDietaryRequirement
{
    private BaseIngredientDietaryRequirement() { }
    public BaseIngredientDietaryRequirement(Guid baseIngredientId, Guid dietaryRequirementId)
    {
        BaseIngredientId = Required(baseIngredientId, nameof(baseIngredientId));
        DietaryRequirementId = Required(dietaryRequirementId, nameof(dietaryRequirementId));
    }

    public Guid BaseIngredientId { get; private set; }
    public Guid DietaryRequirementId { get; private set; }

    private static Guid Required(Guid value, string parameterName) =>
        value == Guid.Empty ? throw new ArgumentException("ID is required.", parameterName) : value;
}
