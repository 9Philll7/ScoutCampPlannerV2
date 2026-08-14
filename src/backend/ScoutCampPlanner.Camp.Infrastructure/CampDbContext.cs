using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Camp.Contracts;
using ScoutCampPlanner.Camp.Domain;

namespace ScoutCampPlanner.Camp.Infrastructure;

public sealed class CampDbContext(DbContextOptions<CampDbContext> options) : DbContext(options), ICampLookup
{
    public DbSet<Camp.Domain.Camp> Camps => Set<Camp.Domain.Camp>();
    public DbSet<CookingUnit> CookingUnits => Set<CookingUnit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        if (Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true)
            modelBuilder.HasDefaultSchema("camp");
        modelBuilder.Entity<Camp.Domain.Camp>(entity =>
        {
            entity.ToTable("Camps");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.NormalizedName).HasMaxLength(200);
            entity.HasIndex(x => new { x.TenantId, x.Name });
            entity.HasIndex(x => new { x.TenantId, x.NormalizedName, x.StartDate, x.EndDate }).IsUnique();
        });
        modelBuilder.Entity<CookingUnit>(entity =>
        {
            entity.ToTable("CookingUnits");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.HasIndex(x => x.CampId);
        });
    }

    public async Task<CampReference?> FindAsync(Guid campId, CancellationToken cancellationToken = default) =>
        await Camps.Where(x => x.Id == campId)
            .Select(x => new CampReference(x.Id, x.TenantId, x.Name, x.IsFrozen))
            .SingleOrDefaultAsync(cancellationToken);
}
