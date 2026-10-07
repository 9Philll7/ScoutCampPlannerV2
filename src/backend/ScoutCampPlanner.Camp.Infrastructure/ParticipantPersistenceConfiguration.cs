using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ScoutCampPlanner.Camp.Domain;

namespace ScoutCampPlanner.Camp.Infrastructure;

internal sealed class ParticipantPersistenceConfiguration : IEntityTypeConfiguration<Participant>
{
    public void Configure(EntityTypeBuilder<Participant> entity)
    {
        entity.ToTable("CampParticipants");
        entity.HasKey(value => value.Id);
        entity.Property(value => value.DisplayName).HasMaxLength(200);
        entity.HasIndex(value => value.CampId);
        entity.HasOne<StructureNode>().WithMany().HasForeignKey(value => value.StructureNodeId)
            .OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<Domain.Camp>().WithMany().HasForeignKey(value => value.CampId)
            .OnDelete(DeleteBehavior.Cascade);
        // DietType, allergen, substance and meal references cross module boundaries:
        // validate via contracts, never introduce cross-module foreign keys.
        entity.OwnsMany(value => value.AbsentDays, child =>
        {
            child.ToTable("CampParticipantAbsentDays");
            child.WithOwner().HasForeignKey("ParticipantId");
            child.HasKey("ParticipantId", nameof(ParticipantAbsentDay.Date));
        });
        entity.OwnsMany(value => value.AbsentMeals, child =>
        {
            child.ToTable("CampParticipantAbsentMeals");
            child.WithOwner().HasForeignKey("ParticipantId");
            child.HasKey("ParticipantId", nameof(ParticipantAbsentMeal.MealId));
        });
        entity.OwnsMany(value => value.Allergens, child =>
        {
            child.ToTable("CampParticipantAllergens");
            child.WithOwner().HasForeignKey("ParticipantId");
            child.HasKey("ParticipantId", nameof(ParticipantAllergen.AllergenId));
        });
        entity.OwnsMany(value => value.Intolerances, child =>
        {
            child.ToTable("CampParticipantIntolerances", table => table.HasCheckConstraint(
                "CK_CampParticipantIntolerances_Threshold", "\"ThresholdGramsPerPortion\" IS NULL OR \"ThresholdGramsPerPortion\" >= 0"));
            child.WithOwner().HasForeignKey("ParticipantId");
            child.HasKey("ParticipantId", nameof(ParticipantIntolerance.SubstanceId));
            child.Property(value => value.ThresholdGramsPerPortion).HasPrecision(18, 6);
            child.Property(value => value.ThresholdSource).HasMaxLength(500);
        });
        entity.Navigation(value => value.AbsentDays).UsePropertyAccessMode(PropertyAccessMode.Field);
        entity.Navigation(value => value.AbsentMeals).UsePropertyAccessMode(PropertyAccessMode.Field);
        entity.Navigation(value => value.Allergens).UsePropertyAccessMode(PropertyAccessMode.Field);
        entity.Navigation(value => value.Intolerances).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
