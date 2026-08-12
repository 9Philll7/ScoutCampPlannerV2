using ScoutCampPlanner.Platform.Application.Authorization;
using Xunit;

namespace ScoutCampPlanner.PlatformTests;

public sealed class AuthorizationCatalogueTests
{
    [Fact]
    public void CatalogueContainsTheDocumentedStableIdentifiers()
    {
        Assert.Equal(1, AuthorizationCatalogue.DefinitionVersion);
        Assert.Equal(10, AuthorizationCatalogue.AllTenantPermissions.Count);
        Assert.Equal(8, AuthorizationCatalogue.AllCampPermissions.Count);
        Assert.Equal(7, AuthorizationCatalogue.AllRoles.Count);
        Assert.Contains(Permissions.Tenant.ManageAuditLegalHold, AuthorizationCatalogue.AllTenantPermissions);
        Assert.Contains(Permissions.Camp.PrepareOfflineAccess, AuthorizationCatalogue.AllCampPermissions);
    }

    [Fact]
    public void RoleMappingsMatchAdr011()
    {
        AssertRole(Roles.TenantOwner, AuthorizationScope.Tenant, AuthorizationCatalogue.AllTenantPermissions);
        AssertRole(Roles.TenantAdmin, AuthorizationScope.Tenant,
            Permissions.Tenant.View,
            Permissions.Tenant.ManageSettings,
            Permissions.Tenant.ViewMembers,
            Permissions.Tenant.ManageMembers,
            Permissions.Tenant.CreateCamps,
            Permissions.Tenant.AssignCampMembers);
        AssertRole(Roles.TenantMember, AuthorizationScope.Tenant, Permissions.Tenant.View);
        AssertRole(Roles.TenantAuditor, AuthorizationScope.Tenant,
            Permissions.Tenant.ViewAudit,
            Permissions.Tenant.ExportAudit);
        AssertRole(Roles.CampAdmin, AuthorizationScope.Camp, AuthorizationCatalogue.AllCampPermissions);
        AssertRole(Roles.CampEditor, AuthorizationScope.Camp, Permissions.Camp.View, Permissions.Camp.Edit);
        AssertRole(Roles.CampViewer, AuthorizationScope.Camp, Permissions.Camp.View);
    }

    [Fact]
    public void MultipleRolesAreCombinedOnlyWithinRequestedScope()
    {
        var tenantPermissions = AuthorizationCatalogue.ResolvePermissions(
            AuthorizationScope.Tenant,
            [Roles.TenantMember, Roles.TenantAuditor, Roles.CampAdmin, "UnknownRole"]);

        Assert.Equal(
            new[] { Permissions.Tenant.ExportAudit, Permissions.Tenant.ViewAudit, Permissions.Tenant.View },
            tenantPermissions.Order(StringComparer.Ordinal));
        Assert.DoesNotContain(Permissions.Camp.View, tenantPermissions);
    }

    [Fact]
    public void NoInitialRoleGrantsFutureHealthPermission()
    {
        Assert.All(AuthorizationCatalogue.AllRoles.Values, role =>
            Assert.DoesNotContain(role.Permissions, permission =>
                permission.StartsWith("health.", StringComparison.Ordinal)));
    }

    [Theory]
    [InlineData(Roles.TenantOwner)]
    [InlineData(Roles.TenantAdmin)]
    [InlineData(Roles.TenantMember)]
    public void TenantRoleSetAcceptsExactlyOneBaseRoleWithOptionalAuditor(string baseRole)
    {
        Assert.True(TenantRoleSetValidator.Validate([baseRole]).IsValid);
        Assert.True(TenantRoleSetValidator.Validate([baseRole, Roles.TenantAuditor]).IsValid);
    }

    [Theory]
    [InlineData(TenantRoleSetFailure.MissingBaseRole, Roles.TenantAuditor)]
    [InlineData(TenantRoleSetFailure.MultipleBaseRoles, Roles.TenantOwner, Roles.TenantAdmin)]
    [InlineData(TenantRoleSetFailure.DuplicateRole, Roles.TenantMember, Roles.TenantMember)]
    [InlineData(TenantRoleSetFailure.UnknownOrWrongScopeRole, Roles.TenantMember, Roles.CampAdmin)]
    [InlineData(TenantRoleSetFailure.UnknownOrWrongScopeRole, Roles.TenantMember, "UnknownRole")]
    public void TenantRoleSetRejectsInvalidCombinations(
        TenantRoleSetFailure expectedFailure,
        params string[] roles)
    {
        var result = TenantRoleSetValidator.Validate(roles);

        Assert.False(result.IsValid);
        Assert.Equal(expectedFailure, result.Failure);
    }

    private static void AssertRole(
        string identifier,
        AuthorizationScope expectedScope,
        params string[] expectedPermissions)
    {
        Assert.True(AuthorizationCatalogue.TryGetRole(identifier, out var role));
        Assert.NotNull(role);
        Assert.Equal(expectedScope, role.Scope);
        Assert.Equal(
            expectedPermissions.Order(StringComparer.Ordinal),
            role.Permissions.Order(StringComparer.Ordinal));
    }

    private static void AssertRole(
        string identifier,
        AuthorizationScope expectedScope,
        IReadOnlySet<string> expectedPermissions) =>
        AssertRole(identifier, expectedScope, expectedPermissions.ToArray());
}
