using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure.Ingredients;

namespace ScoutCampPlanner.Catering.Infrastructure;

internal static class DietaryRequirementPersistenceConfiguration
{
    public static void Configure(ModelBuilder model)
    {
        model.Entity<IngredientIntoleranceDefinitionRecord>(entity =>
        {
            entity.Property(value => value.DefaultThresholdGramsPerPortion).HasPrecision(18, 6);
            entity.Property(value => value.DefaultThresholdSource).HasMaxLength(500);
            entity.Property(value => value.DefaultThresholdVersion).IsConcurrencyToken();
        });
        model.Entity<DietaryRequirement>(entity =>
        {
            entity.ToTable("DietaryRequirements"); entity.HasKey(value => value.Id);
            entity.Property(value => value.Name).HasMaxLength(100);
            entity.Property(value => value.NormalizedName).HasMaxLength(100);
            entity.Property(value => value.Description).HasMaxLength(2000);
            entity.Property(value => value.Version).IsConcurrencyToken();
            entity.HasIndex(value => value.NormalizedName).IsUnique().HasFilter("\"TenantId\" IS NULL");
            entity.HasIndex(value => new { value.TenantId, value.NormalizedName }).IsUnique().HasFilter("\"TenantId\" IS NOT NULL");
            entity.OwnsMany(value => value.OriginRules, rules =>
            {
                rules.ToTable("DietaryOriginRules"); rules.WithOwner().HasForeignKey("DietaryRequirementId");
                rules.HasKey("DietaryRequirementId", nameof(DietaryOriginRule.OriginId));
                rules.HasOne<IngredientOriginPropertyRecord>().WithMany().HasForeignKey(value => value.OriginId).OnDelete(DeleteBehavior.Restrict);
            });
            entity.Navigation(value => value.OriginRules).UsePropertyAccessMode(PropertyAccessMode.Field);
        });
        model.Entity<DietaryRequirementRevisionRecord>(entity =>
        {
            entity.ToTable("DietaryRequirementRevisions"); entity.HasKey(value => new { value.DietaryRequirementId, value.Version });
            entity.Property(value => value.SnapshotJson).IsRequired();
            entity.HasOne<DietaryRequirement>().WithMany().HasForeignKey(value => value.DietaryRequirementId).OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<DietaryRequirementContributionRecord>(entity =>
        {
            entity.ToTable("DietaryRequirementContributions"); entity.HasKey(value => value.Id);
            entity.Property(value => value.Status).IsConcurrencyToken();
            entity.HasIndex(value => new { value.DietaryRequirementId, value.Version }).IsUnique();
            entity.HasOne<DietaryRequirementRevisionRecord>().WithMany()
                .HasForeignKey(value => new { value.DietaryRequirementId, value.Version }).OnDelete(DeleteBehavior.Restrict);
        });
    }
}

internal sealed class DietaryRequirementRevisionRecord
{
    public Guid DietaryRequirementId { get; set; }
    public int Version { get; set; }
    public string SnapshotJson { get; set; } = "";
}

internal sealed class DietaryRequirementContributionRecord
{
    public Guid Id { get; set; }
    public Guid DietaryRequirementId { get; set; }
    public int Version { get; set; }
    public Guid SubmittedBy { get; set; }
    public DateTimeOffset SubmittedAtUtc { get; set; }
    public int Status { get; set; }
    public Guid? ReviewedBy { get; set; }
    public DateTimeOffset? ReviewedAtUtc { get; set; }
    public Guid? CentralDietaryRequirementId { get; set; }
}
