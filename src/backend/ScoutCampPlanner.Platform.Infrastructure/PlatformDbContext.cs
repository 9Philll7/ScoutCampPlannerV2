using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Platform.Contracts;
using ScoutCampPlanner.Platform.Domain;

namespace ScoutCampPlanner.Platform.Infrastructure;

public sealed class PlatformDbContext(DbContextOptions<PlatformDbContext> options) : DbContext(options), ITenantLookup
{
    public DbSet<Tenant> Tenants => Set<Tenant>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        if (Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true)
            modelBuilder.HasDefaultSchema("platform");
        modelBuilder.Entity<Tenant>(entity =>
        {
            entity.ToTable("Tenants");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200);
        });
    }

    public async Task<TenantReference?> FindAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
        await Tenants.Where(x => x.Id == tenantId)
            .Select(x => new TenantReference(x.Id, x.Name))
            .SingleOrDefaultAsync(cancellationToken);
}
