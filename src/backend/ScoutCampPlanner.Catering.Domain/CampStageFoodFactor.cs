namespace ScoutCampPlanner.Catering.Domain;

public sealed class CampStageFoodFactor
{
    private CampStageFoodFactor() { }
    public CampStageFoodFactor(Guid id, Guid campId, Guid campStageId, string stageName, decimal factor)
    {
        if (id == Guid.Empty || campId == Guid.Empty || campStageId == Guid.Empty)
            throw new ArgumentException("Factor identifiers are required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(stageName);
        string trimmed = stageName.Trim();
        if (trimmed.Length > 100 || factor < 0.1m || factor > 3m || decimal.Round(factor, 2) != factor)
            throw new ArgumentException("Camp stage food factor is invalid.");
        Id = id; CampId = campId; CampStageId = campStageId; StageName = trimmed; Factor = factor;
    }
    public Guid Id { get; private set; }
    public Guid CampId { get; private set; }
    public Guid CampStageId { get; private set; }
    public string StageName { get; private set; } = string.Empty;
    public decimal Factor { get; private set; }
}
