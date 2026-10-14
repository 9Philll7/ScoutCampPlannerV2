import { MealPlanningOverview } from '../meal-planning/meal-planning-api.service';

/** Display projection only. The recipe picker retains the full accessible catalogue. */
export function usedRecipeRevisions(overview: MealPlanningOverview): ReadonlySet<string> {
  return new Set([
    ...overview.mealPlanDocuments.flatMap(plan => plan.offerGroups.flatMap(group => group.entries.map(entry => entry.recipeRevisionId))),
    ...overview.operationalMeals.flatMap(meal => [
      ...meal.recipeChoices.map(choice => choice.recipeRevisionId),
      ...(meal.manualSupplySolutions ?? []).map(solution => solution.recipeRevisionId),
      ...(meal.supply?.solutions ?? []).map(solution => solution.recipeRevisionId),
    ]),
  ]);
}
