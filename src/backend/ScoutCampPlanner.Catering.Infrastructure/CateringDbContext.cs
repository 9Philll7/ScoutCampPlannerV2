using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Infrastructure;

public sealed class CateringDbContext(DbContextOptions<CateringDbContext> options) : DbContext(options)
{
    public DbSet<MealPlan> MealPlans => Set<MealPlan>();
    public DbSet<CampMealType> CampMealTypes => Set<CampMealType>();
    public DbSet<CampMeal> CampMeals => Set<CampMeal>();
    public DbSet<TenantStageFoodFactor> TenantStageFoodFactors => Set<TenantStageFoodFactor>();
    public DbSet<CampStageFoodFactor> CampStageFoodFactors => Set<CampStageFoodFactor>();
    public DbSet<MeasurementUnit> MeasurementUnits => Set<MeasurementUnit>();
    public DbSet<BaseIngredient> BaseIngredients => Set<BaseIngredient>();
    public DbSet<IngredientVariant> IngredientVariants => Set<IngredientVariant>();
    public DbSet<IngredientUnitConversion> IngredientUnitConversions => Set<IngredientUnitConversion>();
    public DbSet<Allergen> Allergens => Set<Allergen>();
    public DbSet<Intolerance> Intolerances => Set<Intolerance>();
    public DbSet<DietaryRequirement> DietaryRequirements => Set<DietaryRequirement>();
    public DbSet<BaseIngredientAllergen> BaseIngredientAllergens => Set<BaseIngredientAllergen>();
    public DbSet<BaseIngredientIntolerance> BaseIngredientIntolerances => Set<BaseIngredientIntolerance>();
    public DbSet<BaseIngredientDietaryRequirement> BaseIngredientDietaryRequirements => Set<BaseIngredientDietaryRequirement>();

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
        modelBuilder.Entity<CampMealType>(entity =>
        {
            entity.ToTable("CampMealTypes"); entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100);
            entity.Property(x => x.NormalizedName).HasMaxLength(100);
            entity.HasIndex(x => new { x.CampId, x.NormalizedName }).IsUnique();
            entity.HasIndex(x => new { x.CampId, x.SortOrder }).IsUnique();
        });
        modelBuilder.Entity<CampMeal>(entity =>
        {
            entity.ToTable("CampMeals"); entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.CampId, x.Date, x.MealTypeId }).IsUnique();
            entity.HasIndex(x => x.MealTypeId);
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
        modelBuilder.Entity<MeasurementUnit>(entity =>
        {
            entity.ToTable("MeasurementUnits", table =>
                table.HasCheckConstraint("CK_MeasurementUnits_BaseUnitFactor_Positive", "\"BaseUnitFactor\" > 0"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(100);
            entity.Property(x => x.NormalizedName).HasMaxLength(100);
            entity.Property(x => x.Symbol).HasMaxLength(20);
            entity.Property(x => x.BaseUnitFactor).HasPrecision(18, 6);
            entity.HasIndex(x => x.NormalizedName).IsUnique();
            entity.HasIndex(x => new { x.Dimension, x.Symbol }).IsUnique();
        });
        modelBuilder.Entity<BaseIngredient>(entity =>
        {
            entity.ToTable("BaseIngredients", table =>
                table.HasCheckConstraint(
                    "CK_BaseIngredients_ScopeOwner",
                    "(\"ScopeType\" = 0 AND \"ScopeId\" IS NULL) OR (\"ScopeType\" IN (1, 2) AND \"ScopeId\" IS NOT NULL)"));
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.NormalizedName).HasMaxLength(200);
            entity.Property(x => x.OriginInformation).HasMaxLength(2_000);
            entity.HasIndex(x => new { x.ScopeType, x.NormalizedName })
                .IsUnique().HasFilter("\"ScopeId\" IS NULL");
            entity.HasIndex(x => new { x.ScopeType, x.ScopeId, x.NormalizedName })
                .IsUnique().HasFilter("\"ScopeId\" IS NOT NULL");
            entity.HasIndex(x => x.ScopeId);
        });
        modelBuilder.Entity<IngredientVariant>(entity =>
        {
            entity.ToTable("IngredientVariants");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.Property(x => x.NormalizedName).HasMaxLength(200);
            entity.HasIndex(x => new { x.BaseIngredientId, x.NormalizedName }).IsUnique();
            entity.HasOne<BaseIngredient>().WithMany().HasForeignKey(x => x.BaseIngredientId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<IngredientUnitConversion>(entity =>
        {
            entity.ToTable("IngredientUnitConversions", table =>
                table.HasCheckConstraint(
                    "CK_IngredientUnitConversions_Factor_Positive",
                    "\"ReferenceQuantityPerUnit\" > 0"));
            entity.HasKey(x => new { x.BaseIngredientId, x.UnitId });
            entity.Property(x => x.ReferenceQuantityPerUnit).HasPrecision(18, 6);
            entity.HasOne<BaseIngredient>().WithMany().HasForeignKey(x => x.BaseIngredientId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<MeasurementUnit>().WithMany().HasForeignKey(x => x.UnitId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        ConfigureConflictCatalog(modelBuilder.Entity<Allergen>(), "Allergens");
        ConfigureConflictCatalog(modelBuilder.Entity<Intolerance>(), "Intolerances");
        ConfigureConflictCatalog(modelBuilder.Entity<DietaryRequirement>(), "DietaryRequirements");
        modelBuilder.Entity<BaseIngredientAllergen>(entity =>
        {
            entity.ToTable("BaseIngredientAllergens");
            entity.HasKey(x => new { x.BaseIngredientId, x.AllergenId });
            entity.HasOne<BaseIngredient>().WithMany().HasForeignKey(x => x.BaseIngredientId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Allergen>().WithMany().HasForeignKey(x => x.AllergenId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<BaseIngredientIntolerance>(entity =>
        {
            entity.ToTable("BaseIngredientIntolerances");
            entity.HasKey(x => new { x.BaseIngredientId, x.IntoleranceId });
            entity.HasOne<BaseIngredient>().WithMany().HasForeignKey(x => x.BaseIngredientId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Intolerance>().WithMany().HasForeignKey(x => x.IntoleranceId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<BaseIngredientDietaryRequirement>(entity =>
        {
            entity.ToTable("BaseIngredientDietaryRequirements");
            entity.HasKey(x => new { x.BaseIngredientId, x.DietaryRequirementId });
            entity.HasOne<BaseIngredient>().WithMany().HasForeignKey(x => x.BaseIngredientId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<DietaryRequirement>().WithMany().HasForeignKey(x => x.DietaryRequirementId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureConflictCatalog<TEntity>(
        Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<TEntity> entity,
        string tableName)
        where TEntity : class
    {
        entity.ToTable(tableName);
        entity.HasKey("Id");
        entity.Property<string>("Name").HasMaxLength(100);
        entity.Property<string>("NormalizedName").HasMaxLength(100);
        entity.HasIndex("NormalizedName").IsUnique();
    }
}
