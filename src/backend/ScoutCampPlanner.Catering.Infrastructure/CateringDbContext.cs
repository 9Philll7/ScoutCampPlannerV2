using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Infrastructure;

public sealed class CateringDbContext(DbContextOptions<CateringDbContext> options) : DbContext(options)
{
    public DbSet<MealPlan> MealPlans => Set<MealPlan>();

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
    }
}
