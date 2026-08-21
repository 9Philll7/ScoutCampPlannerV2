using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Infrastructure.Recipes;

internal static class RecipePersistenceConfiguration
{
    public static void Configure(ModelBuilder modelBuilder, bool isNpgsql)
    {
        ConfigureRecipe(modelBuilder.Entity<RecipeRecord>());
        ConfigureDraftGraph(modelBuilder);
        ConfigureRevisions(modelBuilder, isNpgsql);
        ConfigureLibraries(modelBuilder);
        ConfigureCampRecipeNotes(modelBuilder);
        ConfigureCentralChangeSubmissions(modelBuilder);
    }

    private static void ConfigureCampRecipeNotes(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CampRecipeNoteRecord>(entity =>
        {
            entity.ToTable("CampRecipeNotes");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Text).IsRequired();
            entity.HasIndex(value => new { value.CampRecipeEntryId, value.CreatedAtUtc });
            entity.HasOne<CampRecipeEntryRecord>().WithMany().HasForeignKey(value => value.CampRecipeEntryId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigureCentralChangeSubmissions(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CentralRecipeChangeSubmissionRecord>(entity =>
        {
            entity.ToTable("CentralRecipeChangeSubmissions");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Status).IsConcurrencyToken();
            entity.HasIndex(value => value.SubmittedLocalRecipeRevisionId).IsUnique();
            entity.HasIndex(value => new { value.Status, value.SubmittedAtUtc });
            entity.HasOne<RecipeRecord>().WithMany().HasForeignKey(value => value.CentralRecipeId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<RecipeRevisionRecord>().WithMany().HasForeignKey(value => value.SourceCentralRevisionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<RecipeRevisionRecord>().WithMany().HasForeignKey(value => value.SubmittedLocalRecipeRevisionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<RecipeRevisionRecord>().WithMany().HasForeignKey(value => value.ResultingCentralRevisionId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureLibraries(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<TenantRecipeEntryRecord>(entity =>
        {
            entity.ToTable("TenantRecipeEntries", table => table.HasCheckConstraint(
                "CK_TenantRecipeEntries_Source",
                "(\"CentralRecipeRevisionId\" IS NOT NULL AND \"TenantRecipeId\" IS NULL) OR " +
                "(\"CentralRecipeRevisionId\" IS NULL AND \"TenantRecipeId\" IS NOT NULL)"));
            entity.HasKey(value => value.Id);
            entity.HasIndex(value => new { value.TenantId, value.CentralRecipeRevisionId }).IsUnique()
                .HasFilter("\"CentralRecipeRevisionId\" IS NOT NULL");
            entity.HasIndex(value => new { value.TenantId, value.TenantRecipeId }).IsUnique()
                .HasFilter("\"TenantRecipeId\" IS NOT NULL");
            entity.HasOne<RecipeRevisionRecord>().WithMany().HasForeignKey(value => value.CentralRecipeRevisionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<RecipeRecord>().WithMany().HasForeignKey(value => value.TenantRecipeId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CampRecipeEntryRecord>(entity =>
        {
            entity.ToTable("CampRecipeEntries", table => table.HasCheckConstraint(
                "CK_CampRecipeEntries_Source",
                "(\"UpstreamRecipeRevisionId\" IS NOT NULL AND \"CampRecipeId\" IS NULL) OR " +
                "(\"UpstreamRecipeRevisionId\" IS NULL AND \"CampRecipeId\" IS NOT NULL)"));
            entity.HasKey(value => value.Id);
            entity.HasIndex(value => new { value.CampId, value.UpstreamRecipeRevisionId }).IsUnique()
                .HasFilter("\"UpstreamRecipeRevisionId\" IS NOT NULL");
            entity.HasIndex(value => new { value.CampId, value.CampRecipeId }).IsUnique()
                .HasFilter("\"CampRecipeId\" IS NOT NULL");
            entity.HasOne<RecipeRevisionRecord>().WithMany().HasForeignKey(value => value.UpstreamRecipeRevisionId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<RecipeRecord>().WithMany().HasForeignKey(value => value.CampRecipeId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureRecipe(EntityTypeBuilder<RecipeRecord> entity)
    {
        entity.ToTable("Recipes", table => table.HasCheckConstraint(
            "CK_Recipes_ScopeOwner",
            "(\"ScopeType\" = 0 AND \"ScopeId\" IS NULL) OR (\"ScopeType\" IN (1, 2) AND \"ScopeId\" IS NOT NULL)"));
        entity.HasKey(x => x.Id);
        entity.Property(x => x.Name).HasMaxLength(200);
        entity.Property(x => x.NormalizedName).HasMaxLength(200);
        entity.Property(x => x.AuthoringStageName).HasMaxLength(100);
        entity.Property(x => x.ReferenceServings).HasPrecision(18, 6);
        entity.Property(x => x.ReferenceQuantity).HasPrecision(18, 6);
        entity.Property(x => x.AuthoringStageFactor).HasPrecision(18, 6);
        entity.Property(x => x.DraftVersion).IsConcurrencyToken();
        entity.HasIndex(x => new { x.ScopeType, x.NormalizedName }).IsUnique().HasFilter("\"ScopeId\" IS NULL");
        entity.HasIndex(x => new { x.ScopeType, x.ScopeId, x.NormalizedName }).IsUnique().HasFilter("\"ScopeId\" IS NOT NULL");
        entity.HasIndex(x => x.ScopeId);
        entity.HasOne<MeasurementUnit>().WithMany().HasForeignKey(x => x.ReferenceUnitId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<RecipeRecord>().WithMany().HasForeignKey(x => x.DerivedFromRecipeId).OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureDraftGraph(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RecipeDraftTagRecord>(entity =>
        {
            entity.ToTable("RecipeDraftTags");
            entity.HasKey(x => new { x.RecipeId, x.Value });
            entity.Property(x => x.Value).HasMaxLength(100);
            entity.HasOne<RecipeRecord>().WithMany().HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<RecipeGroupRecord>(entity =>
        {
            entity.ToTable("RecipeGroups");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(200);
            entity.HasIndex(x => new { x.RecipeId, x.SortOrder }).IsUnique();
            entity.HasOne<RecipeRecord>().WithMany().HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<RecipeIngredientPositionRecord>(entity =>
        {
            entity.ToTable("RecipeIngredientPositions");
            entity.HasKey(x => x.Id);
            ConfigurePositionQuantities(entity);
            entity.HasIndex(x => new { x.RecipeId, x.GroupId, x.SortOrder }).IsUnique()
                .HasFilter("\"GroupId\" IS NOT NULL");
            entity.HasIndex(x => new { x.RecipeId, x.SortOrder }).IsUnique()
                .HasDatabaseName("IX_RecipeIngredientPositions_Ungrouped_SortOrder")
                .HasFilter("\"GroupId\" IS NULL");
            entity.HasIndex(x => new { x.RecipeId, x.GroupId, x.BaseIngredientId }).IsUnique()
                .HasFilter("\"GroupId\" IS NOT NULL");
            entity.HasIndex(x => new { x.RecipeId, x.BaseIngredientId }).IsUnique()
                .HasDatabaseName("IX_RecipeIngredientPositions_Ungrouped_BaseIngredientId")
                .HasFilter("\"GroupId\" IS NULL");
            entity.HasOne<RecipeRecord>().WithMany().HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<RecipeGroupRecord>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<BaseIngredient>().WithMany().HasForeignKey(x => x.BaseIngredientId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<MeasurementUnit>().WithMany().HasForeignKey(x => x.UnitId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<RecipeIngredientReplacementRecord>(entity =>
        {
            entity.ToTable("RecipeIngredientReplacements");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ReplacementQuantity).HasPrecision(18, 6);
            entity.HasOne<RecipeIngredientPositionRecord>().WithMany().HasForeignKey(x => x.IngredientPositionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<BaseIngredient>().WithMany().HasForeignKey(x => x.ReplacementBaseIngredientId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<MeasurementUnit>().WithMany().HasForeignKey(x => x.ReplacementUnitId).OnDelete(DeleteBehavior.Restrict);
        });
        ConfigureIngredientReplacementConflicts(modelBuilder);
        modelBuilder.Entity<RecipeSubrecipePositionRecord>(entity =>
        {
            entity.ToTable("RecipeSubrecipePositions");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.RequiredServings).HasPrecision(18, 6);
            entity.Property(x => x.RequiredQuantity).HasPrecision(18, 6);
            entity.HasIndex(x => new { x.RecipeId, x.GroupId, x.SortOrder }).IsUnique()
                .HasFilter("\"GroupId\" IS NOT NULL");
            entity.HasIndex(x => new { x.RecipeId, x.SortOrder }).IsUnique()
                .HasDatabaseName("IX_RecipeSubrecipePositions_Ungrouped_SortOrder")
                .HasFilter("\"GroupId\" IS NULL");
            entity.HasIndex(x => new { x.RecipeId, x.GroupId, x.RecipeRevisionId }).IsUnique()
                .HasFilter("\"GroupId\" IS NOT NULL");
            entity.HasIndex(x => new { x.RecipeId, x.RecipeRevisionId }).IsUnique()
                .HasDatabaseName("IX_RecipeSubrecipePositions_Ungrouped_RecipeRevisionId")
                .HasFilter("\"GroupId\" IS NULL");
            entity.HasOne<RecipeRecord>().WithMany().HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<RecipeGroupRecord>().WithMany().HasForeignKey(x => x.GroupId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<RecipeRevisionRecord>().WithMany().HasForeignKey(x => x.RecipeRevisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<MeasurementUnit>().WithMany().HasForeignKey(x => x.RequiredUnitId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<RecipeSubrecipeReplacementRecord>(entity =>
        {
            entity.ToTable("RecipeSubrecipeReplacements");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ReplacementServings).HasPrecision(18, 6);
            entity.Property(x => x.ReplacementQuantity).HasPrecision(18, 6);
            entity.HasOne<RecipeSubrecipePositionRecord>().WithMany().HasForeignKey(x => x.SubrecipePositionId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<RecipeRevisionRecord>().WithMany().HasForeignKey(x => x.ReplacementRecipeRevisionId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<MeasurementUnit>().WithMany().HasForeignKey(x => x.ReplacementUnitId).OnDelete(DeleteBehavior.Restrict);
        });
        ConfigureSubrecipeReplacementConflicts(modelBuilder);
    }

    private static void ConfigureRevisions(ModelBuilder modelBuilder, bool isNpgsql)
    {
        modelBuilder.Entity<RecipeRevisionRecord>(entity =>
        {
            entity.ToTable("RecipeRevisions", table =>
            {
                table.HasCheckConstraint("CK_RecipeRevisions_Number_Positive", "\"RevisionNumber\" > 0");
                table.HasCheckConstraint("CK_RecipeRevisions_SchemaVersion_Positive", "\"SnapshotSchemaVersion\" > 0");
            });
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ChangeNote).HasMaxLength(2_000);
            entity.Property(x => x.SnapshotJson).HasColumnType(isNpgsql ? "jsonb" : "TEXT");
            entity.HasIndex(x => new { x.RecipeId, x.RevisionNumber }).IsUnique();
            entity.HasOne<RecipeRecord>().WithMany().HasForeignKey(x => x.RecipeId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<RecipeRevisionRecord>().WithMany().HasForeignKey(x => x.RestoredFromRevisionId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<RecipeRevisionWarningRecord>(entity =>
        {
            entity.ToTable("RecipeRevisionWarnings");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.WarningCode).HasMaxLength(150);
            entity.Property(x => x.Message).HasMaxLength(1_000);
            entity.Property(x => x.ContextJson).HasColumnType(isNpgsql ? "jsonb" : "TEXT");
            entity.HasIndex(x => x.RecipeRevisionId);
            entity.HasOne<RecipeRevisionRecord>().WithMany().HasForeignKey(x => x.RecipeRevisionId).OnDelete(DeleteBehavior.Cascade);
        });
    }

    private static void ConfigurePositionQuantities(EntityTypeBuilder<RecipeIngredientPositionRecord> entity)
    {
        entity.Property(x => x.Quantity).HasPrecision(18, 6);
        entity.Property(x => x.StepSize).HasPrecision(18, 6);
        entity.Property(x => x.QuantityPerStep).HasPrecision(18, 6);
    }

    private static void ConfigureIngredientReplacementConflicts(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RecipeIngredientReplacementAllergenRecord>(entity =>
        {
            entity.ToTable("RecipeIngredientReplacementAllergens");
            entity.HasKey(x => new { x.ReplacementId, x.AllergenId });
            entity.HasOne<RecipeIngredientReplacementRecord>().WithMany().HasForeignKey(x => x.ReplacementId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Allergen>().WithMany().HasForeignKey(x => x.AllergenId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<RecipeIngredientReplacementIntoleranceRecord>(entity =>
        {
            entity.ToTable("RecipeIngredientReplacementIntolerances");
            entity.HasKey(x => new { x.ReplacementId, x.IntoleranceId });
            entity.HasOne<RecipeIngredientReplacementRecord>().WithMany().HasForeignKey(x => x.ReplacementId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Intolerance>().WithMany().HasForeignKey(x => x.IntoleranceId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<RecipeIngredientReplacementDietaryRequirementRecord>(entity =>
        {
            entity.ToTable("RecipeIngredientReplacementDietaryRequirements");
            entity.HasKey(x => new { x.ReplacementId, x.DietaryRequirementId });
            entity.HasOne<RecipeIngredientReplacementRecord>().WithMany().HasForeignKey(x => x.ReplacementId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<DietaryRequirement>().WithMany().HasForeignKey(x => x.DietaryRequirementId).OnDelete(DeleteBehavior.Restrict);
        });
    }

    private static void ConfigureSubrecipeReplacementConflicts(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<RecipeSubrecipeReplacementAllergenRecord>(entity =>
        {
            entity.ToTable("RecipeSubrecipeReplacementAllergens");
            entity.HasKey(x => new { x.ReplacementId, x.AllergenId });
            entity.HasOne<RecipeSubrecipeReplacementRecord>().WithMany().HasForeignKey(x => x.ReplacementId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Allergen>().WithMany().HasForeignKey(x => x.AllergenId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<RecipeSubrecipeReplacementIntoleranceRecord>(entity =>
        {
            entity.ToTable("RecipeSubrecipeReplacementIntolerances");
            entity.HasKey(x => new { x.ReplacementId, x.IntoleranceId });
            entity.HasOne<RecipeSubrecipeReplacementRecord>().WithMany().HasForeignKey(x => x.ReplacementId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Intolerance>().WithMany().HasForeignKey(x => x.IntoleranceId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<RecipeSubrecipeReplacementDietaryRequirementRecord>(entity =>
        {
            entity.ToTable("RecipeSubrecipeReplacementDietaryRequirements");
            entity.HasKey(x => new { x.ReplacementId, x.DietaryRequirementId });
            entity.HasOne<RecipeSubrecipeReplacementRecord>().WithMany().HasForeignKey(x => x.ReplacementId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<DietaryRequirement>().WithMany().HasForeignKey(x => x.DietaryRequirementId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
