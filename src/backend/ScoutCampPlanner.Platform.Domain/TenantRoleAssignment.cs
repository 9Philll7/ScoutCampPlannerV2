namespace ScoutCampPlanner.Platform.Domain;

public sealed class TenantRoleAssignment
{
    private TenantRoleAssignment() { }

    public TenantRoleAssignment(Guid membershipId, string roleIdentifier)
    {
        if (membershipId == Guid.Empty)
            throw new ArgumentException("Membership ID is required.", nameof(membershipId));
        ArgumentException.ThrowIfNullOrWhiteSpace(roleIdentifier);
        if (roleIdentifier.Length > 100)
            throw new ArgumentException("Role identifier must not exceed 100 characters.", nameof(roleIdentifier));

        MembershipId = membershipId;
        RoleIdentifier = roleIdentifier;
    }

    public Guid MembershipId { get; private set; }

    public string RoleIdentifier { get; private set; } = string.Empty;
}
