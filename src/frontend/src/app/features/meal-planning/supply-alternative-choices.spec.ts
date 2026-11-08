import { CookingUnitMeal, SupplySolution } from './meal-planning-api.service';
import { alternativeDecisions, mergeSupplySolutions } from './supply-alternative-choices';

describe('recipe-level supply choices', () => {
  it('uses explicit API decisions including variants instead of reconstructing candidates', () => {
    const current = state([]); const first = option('a', 'g1');
    current.supply!.decisions = [{ id: 'api-choice', offerGroupId: 'main', requirementGroupKeys: ['g1'], options: [first, { ...first, positionSelections: { path: { variantKey: 'free', replacementRuleId: null } } }], together: false }];
    const choices = alternativeDecisions(current, [], 2);
    expect(choices).toHaveLength(1); expect(choices[0].candidates).toHaveLength(2);
  });
  const option = (recipe: string, key: string, portions = 2): SupplySolution => ({ id: recipe + key, recipeRevisionId: recipe,
    offerGroupId: 'main', portions, source: 0, positionSelections: {}, allocations: [{ requirementGroupKey: key, portions }] });
  const offers = [{ id: 'main', name: 'Hauptspeise', alternativeRevisionIds: ['a', 'b', 'c', 'd'] }];
  function state(candidates: SupplySolution[]): CookingUnitMeal { return { id: 'state', cookingUnitId: 'unit', campMealId: 'meal', subscriptionState: 0,
    demandOverride: null, calculatedDemand: 5, effectiveDemand: 5, status: 0, mealPlanId: null, mealPlanVersion: null, calculatedAtUtc: null,
    structureOverrideNodeIds: [], offerTargets: [], recipeChoices: [], warnings: [],
    supply: { candidates, solutions: [], problems: ['g1', 'g2'].map(requirementGroupKey => ({ code: 4, category: 0, requirementGroupKey, referenceId: null, positionPath: null })) } }; }
  it('combines the same alternatives for multiple requirements into one decision', () => {
    const choices = alternativeDecisions(state([option('a', 'g1'), option('b', 'g1'), option('a', 'g2', 3), option('b', 'g2', 3)]), offers, 1);
    expect(choices).toHaveLength(1); expect(choices[0].keys).toEqual(['g1', 'g2']);
    expect(choices[0].candidates.map(value => value.portions)).toEqual([5, 5]);
    expect(choices[0].candidates[0].allocations).toHaveLength(2);
  });
  it('uses a shared intersection before splitting genuinely incompatible choices', () => {
    const shared = alternativeDecisions(state([option('a', 'g1'), option('b', 'g1'), option('b', 'g2'), option('c', 'g2')]), offers, 1);
    expect(shared).toHaveLength(1); expect(shared[0].candidates.map(value => value.recipeRevisionId)).toEqual(['b']);
    const split = alternativeDecisions(state([option('a', 'g1'), option('b', 'g1'), option('c', 'g2'), option('d', 'g2')]), offers, 1);
    expect(split).toHaveLength(2);
  });
  it('keeps recipe choices per requirement for the separate preference', () => {
    const choices = alternativeDecisions(state([option('a', 'g1'), option('b', 'g1'), option('a', 'g2'), option('b', 'g2')]), offers, 2);
    expect(choices).toHaveLength(2); expect(choices.map(choice => choice.keys)).toEqual([['g1'], ['g2']]);
  });
  it('prioritizes backend-confirmed whole-component alternatives for AlwaysTogether', () => {
    const candidates = ['a', 'b'].map(recipe => mergeSupplySolutions([option(recipe, 'standard', 10), option(recipe, 'g1', 2), option(recipe, 'g2', 3)])[0]);
    const choices = alternativeDecisions(state([...candidates, option('a', 'g1'), option('b', 'g1'), option('a', 'g2'), option('b', 'g2')]), offers, 0);
    expect(choices).toHaveLength(1); expect(choices[0].together).toBe(true);
    expect(choices[0].keys).toEqual(['g1', 'g2', 'standard']); expect(choices[0].candidates[0].portions).toBe(15);
  });
  it('merges by recipe, component, provenance and positional decisions, without mutating snapshots', () => {
    const first = { ...option('a', 'g1'), source: 1 }, second = { ...option('a', 'g2', 3), source: 1 };
    const changed = { ...second, positionSelections: { path: { variantKey: 'free', replacementRuleId: null } } };
    const merged = mergeSupplySolutions([first, second, option('a', 'g1'), { ...second, offerGroupId: 'side' }, changed]);
    expect(merged).toHaveLength(4); expect(merged[0].portions).toBe(5); expect(merged[0].allocations).toHaveLength(2);
    expect(first.portions).toBe(2); expect(first.allocations).toHaveLength(1);
  });
});
