using System.Collections.Frozen;

namespace ScoutCampPlanner.Platform.Application.Authorization;

public enum AuthorizationScope
{
    Platform,
    Tenant,
    Camp,
}

public static class Permissions
{
    public static class Platform
    {
        public const string ReadCentralRecipes = "recipes.central.read";
        public const string ReviewCentralRecipeChanges = "recipes.central.changes.review";
        public const string PermanentlyDeleteCentralRecipes = "recipes.central.delete";
    }

    public static class Tenant
    {
        public const string View = "tenant.view";
        public const string ManageSettings = "tenant.settings.manage";
        public const string ViewMembers = "tenant.members.view";
        public const string ManageMembers = "tenant.members.manage";
        public const string TransferOwnership = "tenant.ownership.transfer";
        public const string CreateCamps = "tenant.camps.create";
        public const string AssignCampMembers = "tenant.camps.assign-members";
        public const string ViewAudit = "tenant.audit.view";
        public const string ExportAudit = "tenant.audit.export";
        public const string ManageAuditLegalHold = "tenant.audit.legal-hold.manage";
    }

    public static class Camp
    {
        public const string View = "camp.view";
        public const string Edit = "camp.edit";
        public const string ViewMembers = "camp.members.view";
        public const string ManageMembers = "camp.members.manage";
        public const string PrepareOfflineAccess = "camp.offline-access.prepare";
        public const string ExportPackage = "camp.package.export";
        public const string ImportPackage = "camp.package.import";
        public const string ViewAudit = "camp.audit.view";
    }
}

public static class Roles
{
    public const string PlatformAdmin = "PlatformAdmin";
    public const string TenantOwner = "TenantOwner";
    public const string TenantAdmin = "TenantAdmin";
    public const string TenantMember = "TenantMember";
    public const string TenantAuditor = "TenantAuditor";
    public const string CampAdmin = "CampAdmin";
    public const string CampEditor = "CampEditor";
    public const string CampViewer = "CampViewer";
}

public sealed record RoleDefinition(
    string Identifier,
    AuthorizationScope Scope,
    IReadOnlySet<string> Permissions);

public static class AuthorizationCatalogue
{
    public const int DefinitionVersion = 2;

    private static readonly FrozenSet<string> PlatformPermissions = new[]
    {
        Permissions.Platform.ReadCentralRecipes,
        Permissions.Platform.ReviewCentralRecipeChanges,
        Permissions.Platform.PermanentlyDeleteCentralRecipes,
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> TenantPermissions = new[]
    {
        Permissions.Tenant.View,
        Permissions.Tenant.ManageSettings,
        Permissions.Tenant.ViewMembers,
        Permissions.Tenant.ManageMembers,
        Permissions.Tenant.TransferOwnership,
        Permissions.Tenant.CreateCamps,
        Permissions.Tenant.AssignCampMembers,
        Permissions.Tenant.ViewAudit,
        Permissions.Tenant.ExportAudit,
        Permissions.Tenant.ManageAuditLegalHold,
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> CampPermissions = new[]
    {
        Permissions.Camp.View,
        Permissions.Camp.Edit,
        Permissions.Camp.ViewMembers,
        Permissions.Camp.ManageMembers,
        Permissions.Camp.PrepareOfflineAccess,
        Permissions.Camp.ExportPackage,
        Permissions.Camp.ImportPackage,
        Permissions.Camp.ViewAudit,
    }.ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenDictionary<string, RoleDefinition> Definitions =
        new RoleDefinition[]
        {
            Define(Roles.PlatformAdmin, AuthorizationScope.Platform, PlatformPermissions),
            Define(Roles.TenantOwner, AuthorizationScope.Tenant, TenantPermissions),
            Define(Roles.TenantAdmin, AuthorizationScope.Tenant,
                Permissions.Tenant.View,
                Permissions.Tenant.ManageSettings,
                Permissions.Tenant.ViewMembers,
                Permissions.Tenant.ManageMembers,
                Permissions.Tenant.CreateCamps,
                Permissions.Tenant.AssignCampMembers),
            Define(Roles.TenantMember, AuthorizationScope.Tenant, Permissions.Tenant.View),
            Define(Roles.TenantAuditor, AuthorizationScope.Tenant,
                Permissions.Tenant.ViewAudit,
                Permissions.Tenant.ExportAudit),
            Define(Roles.CampAdmin, AuthorizationScope.Camp, CampPermissions),
            Define(Roles.CampEditor, AuthorizationScope.Camp, Permissions.Camp.View, Permissions.Camp.Edit),
            Define(Roles.CampViewer, AuthorizationScope.Camp, Permissions.Camp.View),
        }.ToFrozenDictionary(definition => definition.Identifier, StringComparer.Ordinal);

    public static IReadOnlySet<string> AllTenantPermissions => TenantPermissions;

    public static IReadOnlySet<string> AllPlatformPermissions => PlatformPermissions;

    public static IReadOnlySet<string> AllCampPermissions => CampPermissions;

    public static IReadOnlyDictionary<string, RoleDefinition> AllRoles => Definitions;

    public static bool TryGetRole(string identifier, out RoleDefinition? definition)
    {
        ArgumentNullException.ThrowIfNull(identifier);
        return Definitions.TryGetValue(identifier, out definition);
    }

    public static IReadOnlySet<string> ResolvePermissions(
        AuthorizationScope scope,
        IEnumerable<string> roleIdentifiers)
    {
        ArgumentNullException.ThrowIfNull(roleIdentifiers);
        var permissions = new HashSet<string>(StringComparer.Ordinal);
        foreach (string roleIdentifier in roleIdentifiers)
        {
            if (!Definitions.TryGetValue(roleIdentifier, out var definition) || definition.Scope != scope)
                continue;

            permissions.UnionWith(definition.Permissions);
        }

        return permissions.ToFrozenSet(StringComparer.Ordinal);
    }

    private static RoleDefinition Define(
        string identifier,
        AuthorizationScope scope,
        params string[] permissions) =>
        new(identifier, scope, permissions.ToFrozenSet(StringComparer.Ordinal));

    private static RoleDefinition Define(
        string identifier,
        AuthorizationScope scope,
        IReadOnlySet<string> permissions) =>
        new(identifier, scope, permissions);
}
