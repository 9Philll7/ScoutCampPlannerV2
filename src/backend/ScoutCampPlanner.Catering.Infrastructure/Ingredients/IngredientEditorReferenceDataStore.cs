using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Application.Ingredients;
using ScoutCampPlanner.Catering.Domain;

namespace ScoutCampPlanner.Catering.Infrastructure.Ingredients;

public sealed class IngredientEditorReferenceDataStore(CateringDbContext database)
    : IIngredientEditorReferenceDataStore
{
    public async Task<IngredientEditorReferenceData> GetAsync(
        CancellationToken cancellationToken = default)
    {
        IngredientCategoryReference[] categories = await database.Set<IngredientCategoryRecord>()
            .AsNoTracking()
            .Where(value => value.Status == (int)IngredientCatalogEntryStatus.Active)
            .OrderBy(value => value.NormalizedName)
            .Select(value => new IngredientCategoryReference(
                value.Id, value.ParentCategoryId, value.Code, value.Name))
            .ToArrayAsync(cancellationToken);
        MeasurementUnitReference[] units = await database.MeasurementUnits
            .AsNoTracking()
            .OrderBy(value => value.NormalizedName)
            .Select(value => new MeasurementUnitReference(
                value.Id, value.Name, value.Symbol, value.Dimension, value.BaseUnitFactor))
            .ToArrayAsync(cancellationToken);
        IngredientAllergenReference[] allergens = await database.Set<IngredientAllergenDefinitionRecord>()
            .AsNoTracking()
            .Where(value => value.Status == (int)IngredientCatalogEntryStatus.Active)
            .OrderByDescending(value => value.IsEuMajorAllergen)
            .ThenBy(value => value.Name)
            .Select(value => new IngredientAllergenReference(
                value.Id, value.ParentAllergenId, value.Code, value.Name, value.IsEuMajorAllergen))
            .ToArrayAsync(cancellationToken);
        IngredientIntoleranceReference[] intolerances = await database
            .Set<IngredientIntoleranceDefinitionRecord>()
            .AsNoTracking()
            .Where(value => value.Status == (int)IngredientCatalogEntryStatus.Active)
            .OrderBy(value => value.Name)
            .Select(value => new IngredientIntoleranceReference(
                value.Id, value.Code, value.Name, value.IsQuantityDependent))
            .ToArrayAsync(cancellationToken);
        IngredientOriginReference[] origins = await database.Set<IngredientOriginPropertyRecord>()
            .AsNoTracking()
            .Where(value => value.Status == (int)IngredientCatalogEntryStatus.Active)
            .OrderBy(value => value.Name)
            .Select(value => new IngredientOriginReference(
                value.Id, value.Code, value.Name, value.IsAnimalOrigin))
            .ToArrayAsync(cancellationToken);

        return new IngredientEditorReferenceData(categories, units, allergens, intolerances, origins);
    }
}
