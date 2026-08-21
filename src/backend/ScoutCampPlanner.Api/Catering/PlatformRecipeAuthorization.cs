using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Platform.Application.Authorization;
using ScoutCampPlanner.Platform.Infrastructure;
using ScoutCampPlanner.Platform.Domain;

namespace ScoutCampPlanner.Api.Catering;

public sealed class PlatformRecipeAuthorization(PlatformDbContext database) :
    IRecipePermanentDeleteAuthorization,
    IRecipeChangeSubmissionAuthorization,
    ICampRecipeNoteAuthorization,
    IRecipeCatalogAuthorization
{
    public async Task<bool> CanPermanentlyDeleteCentralRecipesAsync(
        Guid actorUserId,
        CancellationToken cancellationToken = default)
    {
        string[] roles = await database.PlatformRoleAssignments.AsNoTracking()
            .Where(value => value.UserId == actorUserId)
            .Select(value => value.RoleIdentifier)
            .ToArrayAsync(cancellationToken);
        return AuthorizationCatalogue.ResolvePermissions(AuthorizationScope.Platform, roles)
            .Contains(Permissions.Platform.PermanentlyDeleteCentralRecipes);
    }

    public async Task<bool> CanSubmitAsync(
        Guid actorUserId,
        ScoutCampPlanner.Catering.Domain.RecipeScopeType scope,
        Guid scopeId,
        CancellationToken cancellationToken = default)
    {
        string[] roles = scope switch
        {
            ScoutCampPlanner.Catering.Domain.RecipeScopeType.Tenant =>
                await (from membership in database.TenantMemberships.AsNoTracking()
                    join assignment in database.TenantRoleAssignments.AsNoTracking()
                        on membership.Id equals assignment.MembershipId
                    where membership.UserId == actorUserId && membership.TenantId == scopeId &&
                          membership.State == TenantMembershipState.Active
                    select assignment.RoleIdentifier).ToArrayAsync(cancellationToken),
            ScoutCampPlanner.Catering.Domain.RecipeScopeType.Camp =>
                await (from campMembership in database.CampMemberships.AsNoTracking()
                    join tenantMembership in database.TenantMemberships.AsNoTracking()
                        on campMembership.TenantMembershipId equals tenantMembership.Id
                    join assignment in database.CampRoleAssignments.AsNoTracking()
                        on campMembership.Id equals assignment.MembershipId
                    where tenantMembership.UserId == actorUserId && campMembership.CampId == scopeId &&
                          tenantMembership.State == TenantMembershipState.Active &&
                          campMembership.State == CampMembershipState.Active
                    select assignment.RoleIdentifier).ToArrayAsync(cancellationToken),
            _ => [],
        };
        AuthorizationScope authorizationScope = scope == ScoutCampPlanner.Catering.Domain.RecipeScopeType.Tenant
            ? AuthorizationScope.Tenant
            : AuthorizationScope.Camp;
        return AuthorizationCatalogue.ResolvePermissions(authorizationScope, roles)
            .Contains(Permissions.Recipes.SubmitCentralChange);
    }

    public async Task<bool> CanReviewAsync(Guid actorUserId, CancellationToken cancellationToken = default)
    {
        string[] roles = await database.PlatformRoleAssignments.AsNoTracking()
            .Where(value => value.UserId == actorUserId)
            .Select(value => value.RoleIdentifier)
            .ToArrayAsync(cancellationToken);
        return AuthorizationCatalogue.ResolvePermissions(AuthorizationScope.Platform, roles)
            .Contains(Permissions.Platform.ReviewCentralRecipeChanges);
    }

    public Task<bool> CanReadAsync(
        Guid actorUserId, Guid campId, CancellationToken cancellationToken = default) =>
        HasCampPermissionAsync(actorUserId, campId, Permissions.Recipes.Read, cancellationToken);

    public Task<bool> CanManageAsync(
        Guid actorUserId, Guid campId, CancellationToken cancellationToken = default) =>
        HasCampPermissionAsync(actorUserId, campId, Permissions.Recipes.ManageCampNotes, cancellationToken);

    public async Task<bool> CanReadCentralAsync(
        Guid actorUserId, CancellationToken cancellationToken = default)
    {
        string[] roles = await database.PlatformRoleAssignments.AsNoTracking()
            .Where(value => value.UserId == actorUserId)
            .Select(value => value.RoleIdentifier).ToArrayAsync(cancellationToken);
        return AuthorizationCatalogue.ResolvePermissions(AuthorizationScope.Platform, roles)
            .Contains(Permissions.Platform.ReadCentralRecipes);
    }

    public Task<bool> CanReadTenantAsync(
        Guid actorUserId, Guid tenantId, CancellationToken cancellationToken = default) =>
        HasTenantPermissionAsync(actorUserId, tenantId, Permissions.Recipes.Read, cancellationToken);

    public Task<bool> CanReadCampAsync(
        Guid actorUserId, Guid campId, CancellationToken cancellationToken = default) =>
        HasCampPermissionAsync(actorUserId, campId, Permissions.Recipes.Read, cancellationToken);

    private async Task<bool> HasTenantPermissionAsync(
        Guid actorUserId, Guid tenantId, string permission, CancellationToken cancellationToken)
    {
        string[] roles = await (from membership in database.TenantMemberships.AsNoTracking()
            join assignment in database.TenantRoleAssignments.AsNoTracking()
                on membership.Id equals assignment.MembershipId
            where membership.UserId == actorUserId && membership.TenantId == tenantId &&
                  membership.State == TenantMembershipState.Active
            select assignment.RoleIdentifier).ToArrayAsync(cancellationToken);
        return AuthorizationCatalogue.ResolvePermissions(AuthorizationScope.Tenant, roles).Contains(permission);
    }

    private async Task<bool> HasCampPermissionAsync(
        Guid actorUserId, Guid campId, string permission, CancellationToken cancellationToken)
    {
        string[] roles = await (from campMembership in database.CampMemberships.AsNoTracking()
            join tenantMembership in database.TenantMemberships.AsNoTracking()
                on campMembership.TenantMembershipId equals tenantMembership.Id
            join assignment in database.CampRoleAssignments.AsNoTracking()
                on campMembership.Id equals assignment.MembershipId
            where tenantMembership.UserId == actorUserId && campMembership.CampId == campId &&
                  tenantMembership.State == TenantMembershipState.Active &&
                  campMembership.State == CampMembershipState.Active
            select assignment.RoleIdentifier).ToArrayAsync(cancellationToken);
        return AuthorizationCatalogue.ResolvePermissions(AuthorizationScope.Camp, roles).Contains(permission);
    }
}
