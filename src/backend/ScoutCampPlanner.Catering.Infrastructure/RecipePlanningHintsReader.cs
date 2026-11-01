using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Application.MealPlanning;
using ScoutCampPlanner.Catering.Infrastructure.Ingredients;
using ScoutCampPlanner.Catering.Infrastructure.Recipes;

namespace ScoutCampPlanner.Catering.Infrastructure;

public sealed class RecipePlanningHintsReader(CateringDbContext database, IMealPlanningStore planning, TimeProvider time) : IRecipePlanningHintsReader
{
    public async Task<RecipePlanningHints?> ReadAsync(Guid campId, Guid tenantId, Guid revisionId, CancellationToken ct)
    {
        // Same scope/publication/portion-based visibility as the existing selection, before resolving any contents.
        var accessible = await planning.ListAccessibleRecipesAsync(campId, tenantId, ct);
        if (!accessible.Any(recipe => recipe.RevisionId == revisionId)) return null;
        var allergens = await database.Set<IngredientAllergenDefinitionRecord>().AsNoTracking()
            .OrderBy(value => value.Code).Select(value => new RecipeHintDefinition(value.Id, value.Code, value.Name)).ToArrayAsync(ct);
        var substances = await database.Set<IngredientIntoleranceDefinitionRecord>().AsNoTracking().Where(value => value.Status == 0)
            .OrderBy(value => value.Code).Select(value => new RecipeHintDefinition(value.Id, value.Code, value.Name)).ToArrayAsync(ct);
        var diets = (await new DietaryCatalogStore(database).ListAsync(tenantId, ct))
            .Select(value => new RecipeHintDefinition(value.Id, "", value.Name)).ToArray();
        return new RecipePlanningHintsProjector(new EfSupplySuitabilityReferences(database))
            .Build(revisionId, allergens, diets, substances, time.GetUtcNow(), ct);
    }
}
