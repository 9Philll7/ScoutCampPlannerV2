namespace ScoutCampPlanner.Catering.Domain;

public sealed class IngredientUnitConversion
{
    private IngredientUnitConversion() { }

    public IngredientUnitConversion(Guid baseIngredientId, Guid unitId, decimal referenceQuantityPerUnit)
    {
        BaseIngredientId = baseIngredientId == Guid.Empty
            ? throw new ArgumentException("Base ingredient ID is required.", nameof(baseIngredientId))
            : baseIngredientId;
        UnitId = unitId == Guid.Empty ? throw new ArgumentException("Unit ID is required.", nameof(unitId)) : unitId;
        ReferenceQuantityPerUnit = referenceQuantityPerUnit > 0
            ? referenceQuantityPerUnit
            : throw new ArgumentOutOfRangeException(nameof(referenceQuantityPerUnit), "Conversion factor must be positive.");
    }

    public Guid BaseIngredientId { get; private set; }
    public Guid UnitId { get; private set; }
    public decimal ReferenceQuantityPerUnit { get; private set; }

    public decimal ConvertTo(decimal quantity, IngredientUnitConversion target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (BaseIngredientId != target.BaseIngredientId)
            throw new InvalidOperationException("Ingredient-specific conversions cannot be used across ingredients.");

        return quantity * ReferenceQuantityPerUnit / target.ReferenceQuantityPerUnit;
    }
}
