using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Infrastructure;

public sealed class CateringDbContext(DbContextOptions<CateringDbContext> options) : DbContext(options)
{
    public DbSet<MealPlan> MealPlans => Set<MealPlan>();
    public DbSet<TenantStageFoodFactor> TenantStageFoodFactors => Set<TenantStageFoodFactor>();
    public DbSet<CampStageFoodFactor> CampStageFoodFactors => Set<CampStageFoodFactor>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        if (Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true)
            modelBuilder.HasDefaultSchema("catering");
        modelBuilder.Entity<MealPlan>(entity =>
        {
            entity.ToTable("MealPlans");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.HasIndex(x => x.CampId);
        });
        modelBuilder.Entity<TenantStageFoodFactor>(entity =>
        {
            entity.ToTable("TenantStageFoodFactors"); entity.HasKey(x => x.Id);
            entity.Property(x => x.StageName).HasMaxLength(100);
            entity.Property(x => x.NormalizedStageName).HasMaxLength(100);
            entity.Property(x => x.Factor).HasPrecision(5, 2);
            entity.HasIndex(x => new { x.TenantId, x.NormalizedStageName }).IsUnique();
        });
        modelBuilder.Entity<CampStageFoodFactor>(entity =>
        {
            entity.ToTable("CampStageFoodFactors"); entity.HasKey(x => x.Id);
            entity.Property(x => x.StageName).HasMaxLength(100);
            entity.Property(x => x.Factor).HasPrecision(5, 2);
            entity.HasIndex(x => new { x.CampId, x.CampStageId }).IsUnique();
            entity.HasIndex(x => x.CampId);
        });
    }
}
