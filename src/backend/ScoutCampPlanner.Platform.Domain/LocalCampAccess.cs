namespace ScoutCampPlanner.Platform.Domain;

/// <summary>Local access granted by a successful deliberate camp-package import.</summary>
public sealed class LocalCampAccess
{
    private LocalCampAccess() { }

    public LocalCampAccess(Guid deviceIdentityId, Guid tenantId, Guid campId, Guid transferId)
    {
        if (deviceIdentityId == Guid.Empty || tenantId == Guid.Empty || campId == Guid.Empty || transferId == Guid.Empty)
            throw new ArgumentException("Identity, tenant, camp and transfer IDs are required.");
        DeviceIdentityId = deviceIdentityId;
        TenantId = tenantId;
        CampId = campId;
        TransferId = transferId;
    }

    public Guid DeviceIdentityId { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid CampId { get; private set; }
    public Guid TransferId { get; private set; }
}
