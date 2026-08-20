namespace ScoutCampPlanner.Catering.Domain;

public enum MeasurementDimension
{
    Mass,
    Volume,
    Count,
}

public sealed class MeasurementUnit
{
    private MeasurementUnit() { }

    public MeasurementUnit(
        Guid id,
        string name,
        string symbol,
        MeasurementDimension dimension,
        decimal baseUnitFactor)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Unit ID is required.", nameof(id)) : id;
        if (!Enum.IsDefined(dimension))
            throw new ArgumentOutOfRangeException(nameof(dimension));
        Rename(name, symbol);
        Dimension = dimension;
        BaseUnitFactor = baseUnitFactor > 0
            ? baseUnitFactor
            : throw new ArgumentOutOfRangeException(nameof(baseUnitFactor), "Base-unit factor must be positive.");
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public string Symbol { get; private set; } = string.Empty;
    public MeasurementDimension Dimension { get; private set; }
    public decimal BaseUnitFactor { get; private set; }

    public decimal ConvertTo(decimal quantity, MeasurementUnit target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (Dimension != target.Dimension)
            throw new InvalidOperationException("Units of different dimensions cannot be converted directly.");

        return quantity * BaseUnitFactor / target.BaseUnitFactor;
    }

    private void Rename(string name, string symbol)
    {
        (Name, NormalizedName) = CatalogName.Normalize(name, nameof(name), 100);
        ArgumentException.ThrowIfNullOrWhiteSpace(symbol);
        Symbol = symbol.Trim();
        if (Symbol.Length > 20)
            throw new ArgumentException("Unit symbol must not exceed 20 characters.", nameof(symbol));
    }
}
