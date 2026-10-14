import { usedRecipeRevisions } from './camp-recipe-navigation';
import { MealPlanningOverview } from '../meal-planning/meal-planning-api.service';

describe('Camp recipe navigation projection', () => {
  it('includes plans and active manual/automatic choices, not the unselected catalogue', () => {
    const overview = {
      mealPlanDocuments: [{ offerGroups: [{ entries: [{ recipeRevisionId: 'planned' }] }] }],
      operationalMeals: [{ recipeChoices: [{ recipeRevisionId: 'chosen' }],
        manualSupplySolutions: [{ recipeRevisionId: 'manual' }],
        supply: { solutions: [{ recipeRevisionId: 'auto' }], candidates: [{ recipeRevisionId: 'candidate' }] } }],
      recipeOptions: [{ revisionId: 'unused' }],
    } as unknown as MealPlanningOverview;
    expect([...usedRecipeRevisions(overview)]).toEqual(['planned', 'chosen', 'manual', 'auto']);
    expect(overview.recipeOptions).toHaveLength(1);
  });
});
