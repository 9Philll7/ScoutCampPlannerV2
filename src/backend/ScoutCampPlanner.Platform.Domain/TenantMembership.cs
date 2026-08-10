namespace ScoutCampPlanner.Platform.Domain;

public enum TenantMembershipState
{
    Active = 0,
    Suspended = 1,
    Removed = 2,
}

public sealed class TenantMembership
{
    private TenantMembership() { }

    public TenantMembership(Guid id, Guid userId, Guid tenantId)
    {
        if (id == Guid.Empty) throw new ArgumentException("Membership ID is required.", nameof(id));
        if (userId == Guid.Empty) throw new ArgumentException("User ID is required.", nameof(userId));
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));

        Id = id;
        UserId = userId;
        TenantId = tenantId;
        State = TenantMembershipState.Active;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public Guid TenantId { get; private set; }

    public TenantMembershipState State { get; private set; }

    public void Suspend()
    {
        EnsureNotRemoved();
        State = TenantMembershipState.Suspended;
    }

    public void Restore()
    {
        EnsureNotRemoved();
        State = TenantMembershipState.Active;
    }

    public void Remove() => State = TenantMembershipState.Removed;

    private void EnsureNotRemoved()
    {
        if (State == TenantMembershipState.Removed)
            throw new InvalidOperationException("A removed tenant membership cannot be restored or suspended.");
    }
}
