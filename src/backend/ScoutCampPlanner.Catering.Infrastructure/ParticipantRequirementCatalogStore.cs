using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Application.MealPlanning;
using ScoutCampPlanner.Catering.Infrastructure.Ingredients;

namespace ScoutCampPlanner.Catering.Infrastructure;

/// <summary>Participant requirements use the same stable catalog IDs as revisioned ingredients.</summary>
public sealed class ParticipantRequirementCatalogStore(CateringDbContext database)
{
    public async Task<ParticipantRequirementCatalog> ReadAsync(CancellationToken ct = default, Guid? tenantId = null) => new(
        await database.Set<IngredientAllergenDefinitionRecord>().AsNoTracking().OrderBy(value => value.Name)
            .Select(value => new ParticipantRequirementOption(value.Id, value.Code, value.Name, false, value.ParentAllergenId)).ToArrayAsync(ct),
        await database.Set<IngredientIntoleranceDefinitionRecord>().AsNoTracking().OrderBy(value => value.Name)
            .Select(value => new ParticipantRequirementOption(value.Id, value.Code, value.Name, value.IsQuantityDependent, null,
                value.DefaultThresholdGramsPerPortion, value.DefaultThresholdSource, value.DefaultThresholdVersion)).ToArrayAsync(ct),
        await database.DietaryRequirements.AsNoTracking().Where(value => value.TenantId == null || value.TenantId == tenantId).OrderBy(value => value.Name)
            .Select(value => new ParticipantRequirementOption(value.Id, value.NormalizedName, value.Name, false, null)).ToArrayAsync(ct));

    public async Task<bool> ContainsAsync(Guid? dietTypeId, IReadOnlyCollection<Guid> allergens,
        IReadOnlyCollection<Guid> substances, CancellationToken ct = default, Guid? tenantId = null) =>
        await database.Set<IngredientAllergenDefinitionRecord>().CountAsync(value => allergens.Contains(value.Id), ct) == allergens.Count &&
        await database.Set<IngredientIntoleranceDefinitionRecord>().CountAsync(value => substances.Contains(value.Id), ct) == substances.Count &&
        (!dietTypeId.HasValue || await database.DietaryRequirements.AnyAsync(value => value.Id == dietTypeId &&
            (value.TenantId == null || value.TenantId == tenantId), ct));
}
