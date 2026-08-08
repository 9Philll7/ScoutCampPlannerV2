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
    }

    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsFrozen { get; private set; }
    public Guid? ActiveTransferId { get; private set; }
    public long BaselineVersion { get; private set; }

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

public sealed class CookingUnit
{
    private CookingUnit() { }
    public CookingUnit(Guid id, Guid campId, string name)
    {
        Id = id == Guid.Empty ? throw new ArgumentException("Cooking unit ID is required.", nameof(id)) : id;
        CampId = campId == Guid.Empty ? throw new ArgumentException("Camp ID is required.", nameof(campId)) : campId;
        Name = string.IsNullOrWhiteSpace(name) ? throw new ArgumentException("Name is required.", nameof(name)) : name.Trim();
    }
    public Guid Id { get; private set; }
    public Guid CampId { get; private set; }
    public string Name { get; private set; } = string.Empty;
}
