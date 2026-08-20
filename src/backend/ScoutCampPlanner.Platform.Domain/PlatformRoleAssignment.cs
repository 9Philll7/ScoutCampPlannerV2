namespace ScoutCampPlanner.Platform.Domain;

public sealed class PlatformRoleAssignment
{
    private PlatformRoleAssignment() { }

    public PlatformRoleAssignment(Guid userId, string roleIdentifier)
    {
        UserId = userId == Guid.Empty ? throw new ArgumentException("User ID is required.", nameof(userId)) : userId;
        RoleIdentifier = string.IsNullOrWhiteSpace(roleIdentifier)
            ? throw new ArgumentException("Role identifier is required.", nameof(roleIdentifier))
            : roleIdentifier.Trim();
    }

    public Guid UserId { get; private set; }
    public string RoleIdentifier { get; private set; } = string.Empty;
}
