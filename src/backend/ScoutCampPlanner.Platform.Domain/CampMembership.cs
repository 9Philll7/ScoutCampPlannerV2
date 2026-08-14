namespace ScoutCampPlanner.Platform.Domain;

public enum CampMembershipState
{
    Active = 0,
    Suspended = 1,
    Removed = 2,
}

public sealed class CampMembership
{
    private CampMembership() { }

    public CampMembership(Guid id, Guid tenantMembershipId, Guid campId)
    {
        if (id == Guid.Empty) throw new ArgumentException("Camp membership ID is required.", nameof(id));
        if (tenantMembershipId == Guid.Empty)
            throw new ArgumentException("Tenant membership ID is required.", nameof(tenantMembershipId));
        if (campId == Guid.Empty) throw new ArgumentException("Camp ID is required.", nameof(campId));
        Id = id;
        TenantMembershipId = tenantMembershipId;
        CampId = campId;
        State = CampMembershipState.Active;
    }

    public Guid Id { get; private set; }
    public Guid TenantMembershipId { get; private set; }
    public Guid CampId { get; private set; }
    public CampMembershipState State { get; private set; }

    public void Suspend() { EnsureNotRemoved(); State = CampMembershipState.Suspended; }
    public void Restore() { EnsureNotRemoved(); State = CampMembershipState.Active; }
    public void Remove() => State = CampMembershipState.Removed;

    private void EnsureNotRemoved()
    {
        if (State == CampMembershipState.Removed)
            throw new InvalidOperationException("A removed camp membership cannot be restored or suspended.");
    }
}
