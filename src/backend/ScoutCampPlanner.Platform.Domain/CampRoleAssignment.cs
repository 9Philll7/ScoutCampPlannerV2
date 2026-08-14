namespace ScoutCampPlanner.Platform.Domain;

public sealed class CampRoleAssignment
{
    private CampRoleAssignment() { }

    public CampRoleAssignment(Guid membershipId, string roleIdentifier)
    {
        if (membershipId == Guid.Empty) throw new ArgumentException("Membership ID is required.", nameof(membershipId));
        if (string.IsNullOrWhiteSpace(roleIdentifier))
            throw new ArgumentException("Role identifier is required.", nameof(roleIdentifier));
        MembershipId = membershipId;
        RoleIdentifier = roleIdentifier.Trim();
    }

    public Guid MembershipId { get; private set; }
    public string RoleIdentifier { get; private set; } = string.Empty;
}
