using System.Collections.Frozen;
using ScoutCampPlanner.Platform.Domain;

namespace ScoutCampPlanner.Platform.Application.Authorization;

public static class LocalCampAccessPolicy
{
    // Explicit allowlist: additions to cloud roles must never expand local device access.
    private static readonly FrozenSet<string> AllowedPermissions = new[]
    {
        Permissions.Camp.View,
        Permissions.Camp.Edit,
        Permissions.Camp.ExportPackage,
        Permissions.Catering.EditMealPlanning,
        Permissions.Recipes.Read,
        Permissions.Recipes.Edit,
        Permissions.Recipes.Publish,
        Permissions.Recipes.Archive,
        Permissions.Recipes.ResetToDraft,
        Permissions.Recipes.ManageLibrary,
        Permissions.Recipes.ManageCampNotes,
        Permissions.Ingredients.Manage,
    }.ToFrozenSet(StringComparer.Ordinal);

    public static bool Allows(bool isSingleDevice, LocalCampAccess? grant,
        Guid deviceIdentityId, Guid tenantId, Guid campId, Guid activeTransferId,
        AuthorizationScope scope, string permission,
        IEnumerable<LocalCampPermissionGrant>? explicitGrants = null) =>
        isSingleDevice && grant is not null &&
        deviceIdentityId != Guid.Empty && activeTransferId != Guid.Empty &&
        grant.DeviceIdentityId == deviceIdentityId && grant.TenantId == tenantId &&
        grant.CampId == campId && grant.TransferId == activeTransferId &&
        scope == AuthorizationScope.Camp && (AllowedPermissions.Contains(permission) ||
            IsExplicitPermission(permission) && explicitGrants?.Any(value =>
                value.DeviceIdentityId == deviceIdentityId && value.TenantId == tenantId &&
                value.CampId == campId && value.TransferId == activeTransferId &&
                value.Permission == permission) == true);

    public static bool IsExplicitPermission(string permission) => permission is
        Permissions.Health.ReadParticipantRequirements or
        Permissions.Health.EditParticipantRequirements or
        Permissions.Catering.VerifyMealPlanning;
}
