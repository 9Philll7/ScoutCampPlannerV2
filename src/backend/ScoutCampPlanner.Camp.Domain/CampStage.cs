namespace ScoutCampPlanner.Camp.Domain;

public sealed class CampStage
{
    private CampStage() { }
    public CampStage(Guid id, Guid campId, string name, int sortOrder)
    {
        if (id == Guid.Empty || campId == Guid.Empty) throw new ArgumentException("Stage and camp IDs are required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        string trimmed = name.Trim();
        if (trimmed.Length > 100 || sortOrder < 0) throw new ArgumentException("Camp stage is invalid.");
        Id = id; CampId = campId; Name = trimmed; NormalizedName = trimmed.ToUpperInvariant(); SortOrder = sortOrder;
    }
    public Guid Id { get; private set; }
    public Guid CampId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
}
