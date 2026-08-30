namespace ScoutCampPlanner.Catering.Domain;

public enum IngredientConversionPrecision
{
    Exact,
    Average,
    Estimated,
}

public sealed record IngredientRevisionUnitConversion
{
    public IngredientRevisionUnitConversion(
        Guid sourceUnitId,
        decimal factorToBaseUnit,
        IngredientConversionPrecision precision)
    {
        SourceUnitId = sourceUnitId == Guid.Empty
            ? throw new ArgumentException("Source unit ID is required.", nameof(sourceUnitId))
            : sourceUnitId;
        FactorToBaseUnit = factorToBaseUnit > 0
            ? factorToBaseUnit
            : throw new ArgumentOutOfRangeException(nameof(factorToBaseUnit), "Conversion factor must be positive.");
        if (!Enum.IsDefined(precision))
            throw new ArgumentOutOfRangeException(nameof(precision));
        Precision = precision;
    }

    public Guid SourceUnitId { get; }
    public decimal FactorToBaseUnit { get; }
    public IngredientConversionPrecision Precision { get; }

    public decimal ConvertToBaseUnit(decimal quantity) => quantity * FactorToBaseUnit;
}


public static class IngredientUnitConversionResolver
{
    public static IngredientRevisionUnitConversion? EffectiveConversion(
        IEnumerable<IngredientRevisionUnitConversion> baseConversions,
        IEnumerable<IngredientRevisionUnitConversion>? overrides,
        Guid sourceUnitId)
    {
        if (sourceUnitId == Guid.Empty)
            throw new ArgumentException("Source unit ID is required.", nameof(sourceUnitId));
        IngredientRevisionUnitConversion? overridden = overrides?
            .SingleOrDefault(value => value.SourceUnitId == sourceUnitId);
        return overridden ?? baseConversions.SingleOrDefault(value => value.SourceUnitId == sourceUnitId);
    }
}
