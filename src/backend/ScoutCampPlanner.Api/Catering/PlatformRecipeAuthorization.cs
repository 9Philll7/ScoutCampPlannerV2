using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Platform.Application.Authorization;
using ScoutCampPlanner.Platform.Infrastructure;
using ScoutCampPlanner.Platform.Domain;

namespace ScoutCampPlanner.Api.Catering;

public sealed class PlatformRecipeAuthorization(PlatformDbContext database) :
    IRecipePermanentDeleteAuthorization,
    IRecipeChangeSubmissionAuthorization
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
}
