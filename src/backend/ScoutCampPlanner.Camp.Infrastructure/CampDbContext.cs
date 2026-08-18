using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Camp.Contracts;
using ScoutCampPlanner.Camp.Domain;

namespace ScoutCampPlanner.Camp.Infrastructure;

public sealed class CampDbContext(DbContextOptions<CampDbContext> options) : DbContext(options), ICampLookup
{
    public DbSet<Camp.Domain.Camp> Camps => Set<Camp.Domain.Camp>();
    public DbSet<StructureNode> StructureNodes => Set<StructureNode>();
    public DbSet<TenantStageTemplateEntry> TenantStageTemplateEntries => Set<TenantStageTemplateEntry>();
    public DbSet<CampStage> CampStages => Set<CampStage>();

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
            entity.Property(x => x.StructureLevelNamesJson).HasMaxLength(4000);
            entity.HasIndex(x => new { x.TenantId, x.Name });
            entity.HasIndex(x => new { x.TenantId, x.NormalizedName, x.StartDate, x.EndDate }).IsUnique();
        });
        modelBuilder.Entity<StructureNode>(entity =>
        {
            entity.ToTable("StructureNodes");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.NormalizedName).HasMaxLength(200);
            entity.HasIndex(x => x.CampId);
            entity.HasIndex(x => new { x.CampId, x.NormalizedName }).IsUnique()
                .HasFilter("\"ParentId\" IS NULL");
            entity.HasIndex(x => new { x.CampId, x.ParentId, x.NormalizedName }).IsUnique()
                .HasFilter("\"ParentId\" IS NOT NULL");
            entity.HasOne<Camp.Domain.Camp>().WithMany().HasForeignKey(x => x.CampId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<StructureNode>().WithMany().HasForeignKey(x => x.ParentId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<TenantStageTemplateEntry>(entity =>
        {
            entity.ToTable("TenantStageTemplateEntries");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100);
            entity.Property(x => x.NormalizedName).HasMaxLength(100);
            entity.HasIndex(x => new { x.TenantId, x.NormalizedName }).IsUnique();
            entity.HasIndex(x => new { x.TenantId, x.SortOrder }).IsUnique();
        });
        modelBuilder.Entity<CampStage>(entity =>
        {
            entity.ToTable("CampStages"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100); entity.Property(x => x.NormalizedName).HasMaxLength(100);
            entity.HasIndex(x => new { x.CampId, x.NormalizedName }).IsUnique();
            entity.HasIndex(x => new { x.CampId, x.SortOrder }).IsUnique();
            entity.HasOne<Camp.Domain.Camp>().WithMany().HasForeignKey(x => x.CampId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    public async Task<CampReference?> FindAsync(Guid campId, CancellationToken cancellationToken = default) =>
        await Camps.Where(x => x.Id == campId)
            .Select(x => new CampReference(x.Id, x.TenantId, x.Name, x.IsFrozen))
            .SingleOrDefaultAsync(cancellationToken);
}
