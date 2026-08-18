namespace ScoutCampPlanner.Camp.Domain;

public sealed class TenantStageTemplateEntry
{
    private TenantStageTemplateEntry() { }

    public TenantStageTemplateEntry(Guid id, Guid tenantId, string name, int sortOrder)
    {
        if (id == Guid.Empty) throw new ArgumentException("Stage ID is required.", nameof(id));
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        string trimmedName = name.Trim();
        if (trimmedName.Length > 100) throw new ArgumentException("Stage name must not exceed 100 characters.", nameof(name));
        if (sortOrder < 0) throw new ArgumentOutOfRangeException(nameof(sortOrder));
        Id = id; TenantId = tenantId; Name = trimmedName;
        NormalizedName = trimmedName.ToUpperInvariant(); SortOrder = sortOrder;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string NormalizedName { get; private set; } = string.Empty;
    public int SortOrder { get; private set; }
}
