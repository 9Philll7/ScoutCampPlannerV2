using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Infrastructure.Ingredients;

internal static class RevisionedIngredientPersistenceConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        ConfigureMasterData(modelBuilder);
        ConfigureIdentityAndRevision(modelBuilder);
        ConfigureRevisionProperties(modelBuilder);
        ConfigureVariants(modelBuilder);
    }

    private static void ConfigureMasterData(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IngredientCategoryRecord>(entity =>
        {
            entity.ToTable("IngredientCategories");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Code).HasMaxLength(80);
            entity.Property(value => value.Name).HasMaxLength(150);
            entity.Property(value => value.NormalizedName).HasMaxLength(150);
            entity.HasIndex(value => value.Code).IsUnique();
            entity.HasIndex(value => value.NormalizedName).IsUnique();
            entity.HasOne<IngredientCategoryRecord>().WithMany().HasForeignKey(value => value.ParentCategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<IngredientAllergenDefinitionRecord>(entity =>
        {
            entity.ToTable("IngredientAllergenDefinitions");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Code).HasMaxLength(100);
            entity.Property(value => value.Name).HasMaxLength(150);
            entity.HasIndex(value => value.Code).IsUnique();
            entity.HasOne<IngredientAllergenDefinitionRecord>().WithMany().HasForeignKey(value => value.ParentAllergenId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<IngredientIntoleranceDefinitionRecord>(entity =>
        {
            entity.ToTable("IngredientIntoleranceDefinitions");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Code).HasMaxLength(100);
            entity.Property(value => value.Name).HasMaxLength(150);
            entity.HasIndex(value => value.Code).IsUnique();
        });
        modelBuilder.Entity<IngredientOriginPropertyRecord>(entity =>
        {
            entity.ToTable("IngredientOriginProperties");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Code).HasMaxLength(100);
            entity.Property(value => value.Name).HasMaxLength(150);
            entity.HasIndex(value => value.Code).IsUnique();
        });
        IngredientMasterDataSeed.Configure(modelBuilder);
    }

    private static void ConfigureIdentityAndRevision(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IngredientIdentityRecord>(entity =>
        {
            entity.ToTable("IngredientIdentities", table => table.HasCheckConstraint(
                "CK_IngredientIdentities_ScopeOwner",
                "(\"ScopeType\" = 0 AND \"ScopeId\" IS NULL) OR (\"ScopeType\" IN (1, 2) AND \"ScopeId\" IS NOT NULL)"));
            entity.HasKey(value => value.Id);
            entity.HasIndex(value => new { value.ScopeType, value.ScopeId });
            entity.HasIndex(value => new { value.SourceIngredientId, value.SourceRevisionId });
            entity.HasOne<IngredientIdentityRecord>().WithMany().HasForeignKey(value => value.SourceIngredientId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<IngredientRevisionRecord>().WithMany().HasForeignKey(value => value.SourceRevisionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<IngredientRevisionRecord>().WithMany().HasForeignKey(value => value.CurrentPublishedRevisionId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<IngredientRevisionRecord>(entity =>
        {
            entity.ToTable("IngredientRevisions", table => table.HasCheckConstraint(
                "CK_IngredientRevisions_Number_Positive", "\"RevisionNumber\" > 0"));
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Name).HasMaxLength(200);
            entity.Property(value => value.NormalizedName).HasMaxLength(200);
            entity.Property(value => value.RowVersion).IsConcurrencyToken();
            entity.HasIndex(value => new { value.IngredientId, value.RevisionNumber }).IsUnique();
            entity.HasIndex(value => value.IngredientId).IsUnique().HasFilter("\"State\" = 0");
            entity.HasOne<IngredientIdentityRecord>().WithMany().HasForeignKey(value => value.IngredientId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<IngredientRevisionRecord>().WithMany().HasForeignKey(value => value.BasedOnRevisionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<IngredientRevisionRecord>().WithMany().HasForeignKey(value => value.MergedCentralRevisionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<IngredientCategoryRecord>().WithMany().HasForeignKey(value => value.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<MeasurementUnit>().WithMany().HasForeignKey(value => value.BaseUnitId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureRevisionProperties(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IngredientRevisionAllergenRecord>(entity =>
        {
            entity.ToTable("IngredientRevisionAllergens");
            entity.HasKey(value => new { value.IngredientRevisionId, value.AllergenId });
            entity.HasOne<IngredientRevisionRecord>().WithMany().HasForeignKey(value => value.IngredientRevisionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<IngredientAllergenDefinitionRecord>().WithMany().HasForeignKey(value => value.AllergenId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<IngredientRevisionIntoleranceRecord>(entity =>
        {
            entity.ToTable("IngredientRevisionIntolerances");
            entity.HasKey(value => new { value.IngredientRevisionId, value.IntoleranceId });
            entity.HasOne<IngredientRevisionRecord>().WithMany().HasForeignKey(value => value.IngredientRevisionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<IngredientIntoleranceDefinitionRecord>().WithMany().HasForeignKey(value => value.IntoleranceId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<IngredientRevisionOriginRecord>(entity =>
        {
            entity.ToTable("IngredientRevisionOrigins");
            entity.HasKey(value => new { value.IngredientRevisionId, value.OriginPropertyId });
            entity.HasOne<IngredientRevisionRecord>().WithMany().HasForeignKey(value => value.IngredientRevisionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<IngredientOriginPropertyRecord>().WithMany().HasForeignKey(value => value.OriginPropertyId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<IngredientRevisionUnitConversionRecord>(entity =>
        {
            entity.ToTable("IngredientRevisionUnitConversions", table => table.HasCheckConstraint(
                "CK_IngredientRevisionUnitConversions_Factor_Positive", "\"FactorToBaseUnit\" > 0"));
            entity.HasKey(value => new { value.IngredientRevisionId, value.SourceUnitId });
            entity.Property(value => value.FactorToBaseUnit).HasPrecision(18, 8);
            entity.HasOne<IngredientRevisionRecord>().WithMany().HasForeignKey(value => value.IngredientRevisionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<MeasurementUnit>().WithMany().HasForeignKey(value => value.SourceUnitId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureVariants(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<IngredientVariantRevisionRecord>(entity =>
        {
            entity.ToTable("IngredientVariantRevisions");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.VariantKey).HasMaxLength(100);
            entity.Property(value => value.Name).HasMaxLength(200);
            entity.Property(value => value.NormalizedName).HasMaxLength(200);
            entity.HasIndex(value => new { value.IngredientRevisionId, value.VariantKey }).IsUnique();
            entity.HasIndex(value => new { value.IngredientRevisionId, value.NormalizedName }).IsUnique();
            entity.HasOne<IngredientRevisionRecord>().WithMany().HasForeignKey(value => value.IngredientRevisionId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<IngredientVariantAllergenOverrideRecord>(entity =>
        {
            entity.ToTable("IngredientVariantAllergenOverrides");
            entity.HasKey(value => new { value.VariantRevisionId, value.AllergenId });
            entity.HasOne<IngredientVariantRevisionRecord>().WithMany().HasForeignKey(value => value.VariantRevisionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<IngredientAllergenDefinitionRecord>().WithMany().HasForeignKey(value => value.AllergenId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<IngredientVariantIntoleranceOverrideRecord>(entity =>
        {
            entity.ToTable("IngredientVariantIntoleranceOverrides");
            entity.HasKey(value => new { value.VariantRevisionId, value.IntoleranceId });
            entity.HasOne<IngredientVariantRevisionRecord>().WithMany().HasForeignKey(value => value.VariantRevisionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<IngredientIntoleranceDefinitionRecord>().WithMany().HasForeignKey(value => value.IntoleranceId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<IngredientVariantOriginOverrideRecord>(entity =>
        {
            entity.ToTable("IngredientVariantOriginOverrides");
            entity.HasKey(value => new { value.VariantRevisionId, value.OriginPropertyId });
            entity.HasOne<IngredientVariantRevisionRecord>().WithMany().HasForeignKey(value => value.VariantRevisionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<IngredientOriginPropertyRecord>().WithMany().HasForeignKey(value => value.OriginPropertyId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<IngredientVariantUnitConversionOverrideRecord>(entity =>
        {
            entity.ToTable("IngredientVariantUnitConversionOverrides", table => table.HasCheckConstraint(
                "CK_IngredientVariantUnitConversionOverrides_Factor_Positive", "\"FactorToBaseUnit\" > 0"));
            entity.HasKey(value => new { value.VariantRevisionId, value.SourceUnitId });
            entity.Property(value => value.FactorToBaseUnit).HasPrecision(18, 8);
            entity.HasOne<IngredientVariantRevisionRecord>().WithMany().HasForeignKey(value => value.VariantRevisionId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<MeasurementUnit>().WithMany().HasForeignKey(value => value.SourceUnitId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
