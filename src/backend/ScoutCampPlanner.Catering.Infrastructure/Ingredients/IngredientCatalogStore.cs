using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Application.Ingredients;
using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Infrastructure.Ingredients;

public sealed class IngredientCatalogStore(CateringDbContext database) : IIngredientCatalogStore
{
    public Task<IReadOnlyList<IngredientCatalogEntry>> ListCentralAsync(
        CancellationToken cancellationToken = default) =>
        LoadAsync(value => value.ScopeType == IngredientScopeType.Central, cancellationToken);

    public Task<IReadOnlyList<IngredientCatalogEntry>> ListTenantAsync(
        Guid tenantId, CancellationToken cancellationToken = default) =>
        LoadAsync(value => value.ScopeType == IngredientScopeType.Central ||
                           value.ScopeType == IngredientScopeType.Tenant && value.ScopeId == tenantId,
            cancellationToken);

    public Task<IReadOnlyList<IngredientCatalogEntry>> ListCampAsync(
        Guid tenantId, Guid campId, CancellationToken cancellationToken = default) =>
        LoadAsync(value => value.ScopeType == IngredientScopeType.Central ||
                           value.ScopeType == IngredientScopeType.Tenant && value.ScopeId == tenantId ||
                           value.ScopeType == IngredientScopeType.Camp && value.ScopeId == campId,
            cancellationToken);

    private async Task<IReadOnlyList<IngredientCatalogEntry>> LoadAsync(
        System.Linq.Expressions.Expression<Func<BaseIngredient, bool>> predicate,
        CancellationToken cancellationToken)
    {
        BaseIngredient[] ingredients = await database.BaseIngredients.AsNoTracking()
            .Where(predicate).ToArrayAsync(cancellationToken);
        Guid[] ids = ingredients.Select(value => value.Id).ToArray();
        IngredientVariant[] variants = await database.IngredientVariants.AsNoTracking()
            .Where(value => ids.Contains(value.BaseIngredientId)).ToArrayAsync(cancellationToken);
        var units = await (from conversion in database.IngredientUnitConversions.AsNoTracking()
            join unit in database.MeasurementUnits.AsNoTracking() on conversion.UnitId equals unit.Id
            where ids.Contains(conversion.BaseIngredientId)
            select new { conversion.BaseIngredientId, Conversion = conversion, Unit = unit })
            .ToArrayAsync(cancellationToken);
        BaseIngredientAllergen[] allergens = await database.BaseIngredientAllergens.AsNoTracking()
            .Where(value => ids.Contains(value.BaseIngredientId)).ToArrayAsync(cancellationToken);
        BaseIngredientIntolerance[] intolerances = await database.BaseIngredientIntolerances.AsNoTracking()
            .Where(value => ids.Contains(value.BaseIngredientId)).ToArrayAsync(cancellationToken);
        BaseIngredientDietaryRequirement[] requirements = await database.BaseIngredientDietaryRequirements.AsNoTracking()
            .Where(value => ids.Contains(value.BaseIngredientId)).ToArrayAsync(cancellationToken);
        Dictionary<Guid, string> allergenNames = await database.Allergens.AsNoTracking()
            .ToDictionaryAsync(value => value.Id, value => value.Name, cancellationToken);
        Dictionary<Guid, string> intoleranceNames = await database.Intolerances.AsNoTracking()
            .ToDictionaryAsync(value => value.Id, value => value.Name, cancellationToken);
        Dictionary<Guid, string> requirementNames = await database.DietaryRequirements.AsNoTracking()
            .ToDictionaryAsync(value => value.Id, value => value.Name, cancellationToken);

        return ingredients.Select(ingredient => new IngredientCatalogEntry(
                ingredient.Id, ingredient.Name, ingredient.ScopeType, ingredient.ScopeId,
                ingredient.OriginInformation,
                variants.Where(value => value.BaseIngredientId == ingredient.Id)
                    .OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(value => new IngredientVariantItem(value.Id, value.Name)).ToArray(),
                units.Where(value => value.BaseIngredientId == ingredient.Id)
                    .OrderBy(value => value.Unit.Name, StringComparer.OrdinalIgnoreCase)
                    .Select(value => new IngredientUnitItem(
                        value.Unit.Id, value.Unit.Name, value.Unit.Symbol, value.Unit.Dimension,
                        value.Unit.BaseUnitFactor, value.Conversion.ReferenceQuantityPerUnit)).ToArray(),
                BuildConflicts(ingredient.Id, allergens, intolerances, requirements,
                    allergenNames, intoleranceNames, requirementNames)))
            .OrderBy(value => value.Name, StringComparer.OrdinalIgnoreCase).ThenBy(value => value.Id)
            .ToArray();
    }

    private static IngredientConflictItem[] BuildConflicts(
        Guid ingredientId,
        IEnumerable<BaseIngredientAllergen> allergens,
        IEnumerable<BaseIngredientIntolerance> intolerances,
        IEnumerable<BaseIngredientDietaryRequirement> requirements,
        IReadOnlyDictionary<Guid, string> allergenNames,
        IReadOnlyDictionary<Guid, string> intoleranceNames,
        IReadOnlyDictionary<Guid, string> requirementNames) =>
        allergens.Where(value => value.BaseIngredientId == ingredientId)
            .Select(value => new IngredientConflictItem(
                ConflictType.Allergen, value.AllergenId, allergenNames[value.AllergenId]))
            .Concat(intolerances.Where(value => value.BaseIngredientId == ingredientId)
                .Select(value => new IngredientConflictItem(
                    ConflictType.Intolerance, value.IntoleranceId, intoleranceNames[value.IntoleranceId])))
            .Concat(requirements.Where(value => value.BaseIngredientId == ingredientId)
                .Select(value => new IngredientConflictItem(
                    ConflictType.DietaryRequirement, value.DietaryRequirementId,
                    requirementNames[value.DietaryRequirementId])))
            .OrderBy(value => value.Type).ThenBy(value => value.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
