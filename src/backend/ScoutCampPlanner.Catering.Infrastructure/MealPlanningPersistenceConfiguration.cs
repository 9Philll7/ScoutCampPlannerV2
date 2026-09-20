using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Infrastructure;

internal static class MealPlanningPersistenceConfiguration
{
    public static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MealPlanSnapshot>(entity =>
        {
            entity.ToTable("MealPlanSnapshots");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.ContentJson).HasColumnType("text");
            entity.HasIndex(value => new { value.MealPlanId, value.Version }).IsUnique();
            entity.HasIndex(value => value.CampId);
            entity.HasOne<MealPlan>().WithMany().HasForeignKey(value => value.MealPlanId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<MealPlanOfferGroup>(entity =>
        {
            entity.ToTable("MealPlanOfferGroups");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Name).HasMaxLength(200);
            entity.HasIndex(value => new { value.MealPlanId, value.CampMealId, value.SortOrder }).IsUnique();
            entity.HasOne<MealPlan>().WithMany().HasForeignKey(value => value.MealPlanId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<CampMeal>().WithMany().HasForeignKey(value => value.CampMealId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<MealPlanEntry>(entity =>
        {
            entity.ToTable("MealPlanEntries");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.DisplayName).HasMaxLength(200);
            entity.Property(value => value.Note).HasMaxLength(2_000);
            entity.HasIndex(value => new { value.OfferGroupId, value.RecipeRevisionId }).IsUnique();
            entity.HasIndex(value => new { value.OfferGroupId, value.SortOrder }).IsUnique();
            entity.HasIndex(value => value.RecipeRevisionId);
            entity.HasOne<MealPlanOfferGroup>().WithMany().HasForeignKey(value => value.OfferGroupId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<CookingUnitGroup>(entity =>
        {
            entity.ToTable("CookingUnitGroups");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Name).HasMaxLength(200);
            entity.HasIndex(value => new { value.CampId, value.Name }).IsUnique();
            entity.HasIndex(value => new { value.CampId, value.SortOrder });
        });
        modelBuilder.Entity<CookingUnit>(entity =>
        {
            entity.ToTable("CookingUnits");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.Name).HasMaxLength(200);
            entity.HasIndex(value => new { value.CampId, value.Name }).IsUnique();
            entity.HasIndex(value => new { value.CampId, value.SortOrder });
            entity.HasOne<CookingUnitGroup>().WithMany().HasForeignKey(value => value.GroupId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<MealPlan>().WithMany().HasForeignKey(value => value.StandardMealPlanId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CookingUnitStructureAssignment>(entity =>
        {
            entity.ToTable("CookingUnitStructureAssignments");
            entity.HasKey(value => value.Id);
            entity.HasIndex(value => new { value.CookingUnitId, value.CampMealId, value.StructureNodeId }).IsUnique();
            entity.HasIndex(value => value.CampId);
            entity.HasOne<CookingUnit>().WithMany().HasForeignKey(value => value.CookingUnitId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<CampMeal>().WithMany().HasForeignKey(value => value.CampMealId)
                .OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<CookingUnitMealState>(entity =>
        {
            entity.ToTable("CookingUnitMealStates");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.DemandOverride).HasPrecision(18, 4);
            entity.Property(value => value.CalculatedDemand).HasPrecision(18, 4);
            entity.Property(value => value.EffectiveDemand).HasPrecision(18, 4);
            entity.Property(value => value.CalculationSnapshotJson).HasColumnType("text");
            entity.Property(value => value.SourceFingerprint).HasMaxLength(64);
            entity.Property(value => value.WarningsJson).HasColumnType("text");
            entity.HasIndex(value => new { value.CookingUnitId, value.CampMealId }).IsUnique();
            entity.HasIndex(value => value.CampId);
            entity.HasOne<CookingUnit>().WithMany().HasForeignKey(value => value.CookingUnitId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<CampMeal>().WithMany().HasForeignKey(value => value.CampMealId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<MealPlan>().WithMany().HasForeignKey(value => value.MealPlanId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<MealPlanSnapshot>().WithMany().HasForeignKey(value => value.MealPlanSnapshotId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CookingUnitMealOfferTarget>(entity =>
        {
            entity.ToTable("CookingUnitMealOfferTargets");
            entity.HasKey(value => value.Id);
            entity.Property(value => value.TargetOverride).HasPrecision(18, 4);
            entity.HasIndex(value => new { value.CookingUnitMealStateId, value.OfferGroupId }).IsUnique();
            entity.HasOne<CookingUnitMealState>().WithMany().HasForeignKey(value => value.CookingUnitMealStateId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<MealPlanOfferGroup>().WithMany().HasForeignKey(value => value.OfferGroupId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CookingUnitMealRecipeChoice>(entity =>
        {
            entity.ToTable("CookingUnitMealRecipeChoices");
            entity.HasKey(value => value.Id);
            entity.HasIndex(value => new { value.CookingUnitMealStateId, value.SortOrder }).IsUnique();
            entity.HasIndex(value => value.RecipeRevisionId);
            entity.HasOne<CookingUnitMealState>().WithMany().HasForeignKey(value => value.CookingUnitMealStateId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<MealPlanOfferGroup>().WithMany().HasForeignKey(value => value.OfferGroupId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<MealPlanEntry>().WithMany().HasForeignKey(value => value.MealPlanEntryId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
