using ScoutCampPlanner.Platform.Application.Authorization;
using ScoutCampPlanner.Platform.Domain;
using Xunit;

namespace ScoutCampPlanner.PlatformTests;

public sealed class LocalCampAccessTests
{
    [Fact]
    public void Local_access_is_bound_to_device_tenant_camp_and_transfer()
    {
        var grant = new LocalCampAccess(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        bool Allowed(bool local, Guid device, Guid tenant, Guid camp, Guid transfer) =>
            LocalCampAccessPolicy.Allows(local, grant, device, tenant, camp, transfer,
                AuthorizationScope.Camp, Permissions.Camp.Edit);
        Assert.True(Allowed(true, grant.DeviceIdentityId, grant.TenantId, grant.CampId, grant.TransferId));
        Assert.False(Allowed(false, grant.DeviceIdentityId, grant.TenantId, grant.CampId, grant.TransferId));
        Assert.False(Allowed(true, Guid.NewGuid(), grant.TenantId, grant.CampId, grant.TransferId));
        Assert.False(Allowed(true, grant.DeviceIdentityId, Guid.NewGuid(), grant.CampId, grant.TransferId));
        Assert.False(Allowed(true, grant.DeviceIdentityId, grant.TenantId, Guid.NewGuid(), grant.TransferId));
        Assert.False(Allowed(true, grant.DeviceIdentityId, grant.TenantId, grant.CampId, Guid.NewGuid()));
    }

    [Fact]
    public void Local_access_never_grants_administration_or_unknown_permissions()
    {
        var grant = new LocalCampAccess(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        foreach (var scope in Enum.GetValues<AuthorizationScope>())
        foreach (var permission in new[] { Permissions.Camp.ManageMembers, Permissions.Camp.PrepareOfflineAccess,
            Permissions.Camp.ImportPackage, Permissions.Tenant.ManageSettings,
            Permissions.Platform.ManageCentralIngredients, "future.permission" })
            Assert.False(LocalCampAccessPolicy.Allows(true, grant, grant.DeviceIdentityId, grant.TenantId,
                grant.CampId, grant.TransferId, scope, permission));
        Assert.False(LocalCampAccessPolicy.Allows(true, null, grant.DeviceIdentityId, grant.TenantId,
            grant.CampId, grant.TransferId, AuthorizationScope.Camp, Permissions.Camp.Edit));
        Assert.False(LocalCampAccessPolicy.Allows(true, grant, grant.DeviceIdentityId, grant.TenantId,
            grant.CampId, grant.TransferId, AuthorizationScope.Tenant, Permissions.Recipes.Edit));
    }
}
