namespace ScoutCampPlanner.Catering.Domain;

public sealed class TenantStageFoodFactor
{
    private TenantStageFoodFactor() { }
    public TenantStageFoodFactor(Guid id, Guid tenantId, string stageName, decimal factor)
    {
        if (id == Guid.Empty || tenantId == Guid.Empty) throw new ArgumentException("Factor identifiers are required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(stageName);
        string trimmed = stageName.Trim();
        if (trimmed.Length > 100) throw new ArgumentException("Stage name must not exceed 100 characters.");
        SetFactor(factor);
        Id = id; TenantId = tenantId; StageName = trimmed; NormalizedStageName = trimmed.ToUpperInvariant();
    }
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string StageName { get; private set; } = string.Empty;
    public string NormalizedStageName { get; private set; } = string.Empty;
    public decimal Factor { get; private set; }
    public void SetFactor(decimal factor)
    {
        if (factor < 0.1m || factor > 3m || decimal.Round(factor, 2) != factor)
            throw new ArgumentOutOfRangeException(nameof(factor));
        Factor = factor;
    }
}
