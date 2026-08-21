using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Camp.Infrastructure;
using ScoutCampPlanner.Catering.Application.Ingredients;

namespace ScoutCampPlanner.Api.Catering;

public sealed class CampTenantResolver(CampDbContext database) : ICampTenantResolver
{
    public Task<Guid?> FindTenantIdAsync(Guid campId, CancellationToken cancellationToken = default) =>
        database.Camps.AsNoTracking().Where(value => value.Id == campId)
            .Select(value => (Guid?)value.TenantId).SingleOrDefaultAsync(cancellationToken);
}
