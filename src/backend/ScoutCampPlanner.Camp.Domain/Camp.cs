namespace ScoutCampPlanner.Camp.Domain;

public sealed class Camp
{
    private Camp() { }

    public Camp(Guid id, Guid tenantId, string name)
    {
        if (id == Guid.Empty) throw new ArgumentException("Camp ID is required.", nameof(id));
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Camp name is required.", nameof(name));
        Id = id;
        TenantId = tenantId;
        Name = name.Trim();
        NormalizedName = Name.ToUpperInvariant();
    }

    public Camp(Guid id, Guid tenantId, string name, DateOnly startDate, DateOnly endDate)
        : this(id, tenantId, name)
    {
        if (endDate < startDate)
            throw new ArgumentException("Camp end date must not precede its start date.", nameof(endDate));
        StartDate = startDate;
        EndDate = endDate;
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? NormalizedName { get; private set; }
    public DateOnly? StartDate { get; private set; }
    public DateOnly? EndDate { get; private set; }
    public bool IsFrozen { get; private set; }
    public Guid? ActiveTransferId { get; private set; }
    public long BaselineVersion { get; private set; }
    public CampStructureMode StructureMode { get; private set; } = CampStructureMode.Free;
    public string StructureLevelNamesJson { get; private set; } = "[]";

    public IReadOnlyList<string> GetStructureLevelNames() =>
        System.Text.Json.JsonSerializer.Deserialize<string[]>(StructureLevelNamesJson) ?? [];

    public void ConfigureStructure(IReadOnlyCollection<string>? levelNames)
    {
        if (IsFrozen) throw new InvalidOperationException("A frozen camp cannot be changed.");
        if (levelNames is null || levelNames.Count == 0)
        {
            StructureMode = CampStructureMode.Free;
            StructureLevelNamesJson = "[]";
            return;
        }

        string[] levels = levelNames.Select(name => name?.Trim() ?? string.Empty).ToArray();
        if (levels.Any(name => name.Length == 0 || name.Length > 100))
            throw new ArgumentException("Structure level names must contain 1 to 100 characters.", nameof(levelNames));
        if (levels.Select(name => name.ToUpperInvariant()).Distinct().Count() != levels.Length)
            throw new ArgumentException("Structure level names must be unique.", nameof(levelNames));
        StructureMode = CampStructureMode.Fixed;
        StructureLevelNamesJson = System.Text.Json.JsonSerializer.Serialize(levels);
    }

    public void UpdateDetails(string name, DateOnly startDate, DateOnly endDate)
    {
        if (IsFrozen) throw new InvalidOperationException("A frozen camp cannot be changed.");
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Camp name is required.", nameof(name));
        string trimmedName = name.Trim();
        if (trimmedName.Length > 200)
            throw new ArgumentException("Camp name must not exceed 200 characters.", nameof(name));
        if (endDate < startDate)
            throw new ArgumentException("Camp end date must not precede its start date.", nameof(endDate));

        Name = trimmedName;
        NormalizedName = trimmedName.ToUpperInvariant();
        StartDate = startDate;
        EndDate = endDate;
    }

    public void Freeze(Guid transferId)
    {
        if (IsFrozen) throw new InvalidOperationException("Camp is already frozen.");
        if (transferId == Guid.Empty) throw new ArgumentException("Transfer ID is required.", nameof(transferId));
        IsFrozen = true;
        ActiveTransferId = transferId;
        BaselineVersion++;
    }

    public void CompleteTransfer(Guid transferId, long baselineVersion)
    {
        if (!IsFrozen || ActiveTransferId != transferId || BaselineVersion != baselineVersion)
            throw new InvalidOperationException("Transfer does not match the active camp baseline.");
        IsFrozen = false;
        ActiveTransferId = null;
        BaselineVersion++;
    }
}
