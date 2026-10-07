namespace ScoutCampPlanner.Platform.Domain;

/// <summary>Explicit permission, separate from role bundles. Provision only through an authorized audited operation.</summary>
public sealed class CampPermissionGrant
{
    private CampPermissionGrant() { }
    public CampPermissionGrant(Guid membershipId, string permission)
    {
        if (membershipId == Guid.Empty) throw new ArgumentException("Membership is required.", nameof(membershipId));
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        if (permission.Length > 100) throw new ArgumentException("Permission is too long.", nameof(permission));
        MembershipId = membershipId;
        Permission = permission;
    }
    public Guid MembershipId { get; private set; }
    public string Permission { get; private set; } = string.Empty;
}

/// <summary>Device-local explicit grant. Not part of camp-package replacement or the cloud authorization state.</summary>
public sealed class LocalCampPermissionGrant
{
    private LocalCampPermissionGrant() { }
    public LocalCampPermissionGrant(Guid deviceIdentityId, Guid tenantId, Guid campId,
        Guid transferId, string permission)
    {
        if (deviceIdentityId == Guid.Empty || tenantId == Guid.Empty || campId == Guid.Empty || transferId == Guid.Empty)
            throw new ArgumentException("Device, tenant, camp and transfer are required.");
        ArgumentException.ThrowIfNullOrWhiteSpace(permission);
        if (permission.Length > 100) throw new ArgumentException("Permission is too long.", nameof(permission));
        DeviceIdentityId = deviceIdentityId;
        TenantId = tenantId;
        CampId = campId;
        TransferId = transferId;
        Permission = permission;
    }
    public Guid DeviceIdentityId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CampId { get; private set; }
    public Guid TransferId { get; private set; }
    public string Permission { get; private set; } = string.Empty;
}
