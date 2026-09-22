using Microsoft.EntityFrameworkCore;
using ScoutCampPlanner.Catering.Domain;
using ScoutCampPlanner.Catering.Infrastructure.Recipes;
using ScoutCampPlanner.Catering.Infrastructure.Ingredients;

namespace ScoutCampPlanner.Catering.Infrastructure.Offline;

public sealed class LocalCampRemovalStore(CateringDbContext database)
{
    // Caller owns the cross-module transaction. Shared upstream catalogues stay intact.
    public async Task DeleteAsync(Guid campId, CancellationToken ct)
    {
        await new CampMealPlanningPackageStore(database).DeleteCampDataAsync(campId, ct);
        await database.CampMeals.Where(x => x.CampId == campId).ExecuteDeleteAsync(ct);
        await database.CampMealTypes.Where(x => x.CampId == campId).ExecuteDeleteAsync(ct);
        await database.CampStageFoodFactors.Where(x => x.CampId == campId).ExecuteDeleteAsync(ct);
        await database.Set<CampRecipeEntryRecord>().Where(x => x.CampId == campId).ExecuteDeleteAsync(ct);
        var recipes = database.Set<RecipeRecord>().Where(x => x.ScopeType == (int)RecipeScopeType.Camp && x.ScopeId == campId);
        var ids = recipes.Select(x => x.Id);
        await database.Set<RecipeSubrecipePositionRecord>().Where(x => ids.Contains(x.RecipeId)).ExecuteDeleteAsync(ct);
        await recipes.ExecuteDeleteAsync(ct);
        await database.Set<IngredientIdentityRecord>().Where(x => x.ScopeType == (int)IngredientScopeType.Camp && x.ScopeId == campId).ExecuteDeleteAsync(ct);
        await database.BaseIngredients.Where(x => x.ScopeType == IngredientScopeType.Camp && x.ScopeId == campId).ExecuteDeleteAsync(ct);
    }
}
