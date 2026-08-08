namespace ScoutCampPlanner.Camp.Contracts;

public sealed record CampReference(Guid Id, Guid TenantId, string Name, bool IsFrozen);

public interface ICampLookup
{
    Task<CampReference?> FindAsync(Guid campId, CancellationToken cancellationToken = default);
}
