namespace ScoutCampPlanner.Platform.Application.Authorization;

public enum TenantRoleSetFailure
{
    MissingBaseRole,
    MultipleBaseRoles,
    DuplicateRole,
    UnknownOrWrongScopeRole,
}

public sealed record TenantRoleSetValidation(bool IsValid, TenantRoleSetFailure? Failure)
{
    public static TenantRoleSetValidation Valid { get; } = new(true, null);

    public static TenantRoleSetValidation Invalid(TenantRoleSetFailure failure) => new(false, failure);
}

public static class TenantRoleSetValidator
{
    private static readonly HashSet<string> BaseRoles =
    [
        Roles.TenantOwner,
        Roles.TenantAdmin,
        Roles.TenantMember,
    ];

    public static TenantRoleSetValidation Validate(IEnumerable<string> roleIdentifiers)
    {
        ArgumentNullException.ThrowIfNull(roleIdentifiers);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var baseRoleCount = 0;

        foreach (string roleIdentifier in roleIdentifiers)
        {
            if (!seen.Add(roleIdentifier))
                return TenantRoleSetValidation.Invalid(TenantRoleSetFailure.DuplicateRole);

            if (!AuthorizationCatalogue.TryGetRole(roleIdentifier, out var definition) ||
                definition?.Scope != AuthorizationScope.Tenant)
                return TenantRoleSetValidation.Invalid(TenantRoleSetFailure.UnknownOrWrongScopeRole);

            if (BaseRoles.Contains(roleIdentifier))
                baseRoleCount++;
        }

        return baseRoleCount switch
        {
            0 => TenantRoleSetValidation.Invalid(TenantRoleSetFailure.MissingBaseRole),
            > 1 => TenantRoleSetValidation.Invalid(TenantRoleSetFailure.MultipleBaseRoles),
            _ => TenantRoleSetValidation.Valid,
        };
    }
}
