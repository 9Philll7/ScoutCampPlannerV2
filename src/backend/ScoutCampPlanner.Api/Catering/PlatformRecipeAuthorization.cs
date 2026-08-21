using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Application.Recipes;
using ScoutCampPlanner.Platform.Application.Authorization;
using ScoutCampPlanner.Platform.Infrastructure;

namespace ScoutCampPlanner.Api.Catering;

public sealed class PlatformRecipeAuthorization(PlatformDbContext database) : IRecipePermanentDeleteAuthorization
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
}
