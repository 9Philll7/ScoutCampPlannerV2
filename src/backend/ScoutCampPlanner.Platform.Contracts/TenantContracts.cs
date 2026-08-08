namespace ScoutCampPlanner.Platform.Contracts;

public sealed record TenantReference(Guid Id, string Name);

public interface ITenantLookup
{
    Task<TenantReference?> FindAsync(Guid tenantId, CancellationToken cancellationToken = default);
}
