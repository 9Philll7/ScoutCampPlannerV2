import { CookingUnitMeal, SupplyProblem } from './meal-planning-api.service';
import { operativeMealStatus, operativeVerificationLabel, requirementDescription, supplyConflictGroups, missingPortions, hasOwnRecipeSolutions } from './operative-meal-view';

describe('operative meal presentation', () => {
  const state = { calculatedAtUtc: '2026-11-01T10:00:00Z', status: 0, subscriptionState: 0,
    supply: { solutions: [{ id: 'solution' }], problems: [], candidates: [] } } as unknown as CookingUnitMeal;
  it('recognizes persisted preference previews as plan-following but preserves genuine recipe deviations', () => {
    const solution = { id: 'old-preview', recipeRevisionId: 'recipe', offerGroupId: null, portions: 2, source: 1,
      positionSelections: {}, allocations: [{ requirementGroupKey: 'g', portions: 2 }] };
    const current = { ...state, manualSupplySolutions: [solution], manualScenarioSelections: [{ offerGroupId: null, preference: 2 as const }],
      supply: { ...state.supply!, preferenceOptions: [{ offerGroupId: null, requirementGroupKeys: ['g'], togetherOptions: [], separateSolutions: [solution],
        separateScenario: { solutions: [{ ...solution, id: 'calculated', source: 0 }], decisions: [], problems: [] } }] } };
    expect(hasOwnRecipeSolutions(current)).toBe(false);
    expect(hasOwnRecipeSolutions({ ...current, manualSupplySolutions: [{ ...solution, recipeRevisionId: 'own-recipe' }] })).toBe(true);
    expect(hasOwnRecipeSolutions({ ...current, manualSupplySolutions: [{ ...solution, portions: 3 }] })).toBe(true);
    expect(hasOwnRecipeSolutions({ ...current, completeOwnSupply: true })).toBe(true);
    expect(hasOwnRecipeSolutions({ ...current, manualSupplySolutions: [] })).toBe(false);
    expect(hasOwnRecipeSolutions({ ...current, manualScenarioSelections: [] })).toBe(true);
  });
  it('counts a nested choice from the selected plan scenario as plan-following, including merged requirement groups', () => {
    const first = { id: 'choice', recipeRevisionId: 'plan-recipe', offerGroupId: null, portions: 2, source: 1,
      positionSelections: {}, allocations: [{ requirementGroupKey: 'g1', portions: 2 }] };
    const second = { ...first, id: 'second', portions: 3, allocations: [{ requirementGroupKey: 'g2', portions: 3 }] };
    const current = { ...state, manualSupplySolutions: [{ ...first, portions: 5, allocations: [...first.allocations, ...second.allocations] }],
      manualScenarioSelections: [{ offerGroupId: null, preference: 0 as const }], supply: { ...state.supply!, preferenceOptions: [
        { offerGroupId: null, requirementGroupKeys: ['g1', 'g2'], togetherOptions: [], separateSolutions: [], togetherScenario: {
          solutions: [], problems: [], decisions: [first, second].map((value, index) => ({ id: String(index), offerGroupId: null, requirementGroupKeys: [value.allocations[0].requirementGroupKey],
            together: false, options: [value, { ...value, recipeRevisionId: 'other' }] })) } }] } };
    expect(hasOwnRecipeSolutions(current)).toBe(false);
    expect(hasOwnRecipeSolutions({ ...current, manualSupplySolutions: [{ ...first, portions: 1, allocations: [{ requirementGroupKey: 'g1', portions: 1 }] }] })).toBe(true);
    expect(hasOwnRecipeSolutions({ ...current, manualSupplySolutions: [{ ...first, positionSelections: { path: { variantKey: 'custom', replacementRuleId: null } } }] })).toBe(true);
  });
  it('does not infer solved supply or verification from an uncalculated state', () => {
    expect(operativeMealStatus().kind).toBe('unknown');
    expect(operativeMealStatus({ ...state, calculatedAtUtc: null }).kind).toBe('unknown');
    expect(operativeMealStatus(state).kind).toBe('success');
    expect(operativeVerificationLabel(state)).toBe('Offen');
    expect(operativeMealStatus({ ...state, status: 1 }).kind).toBe('stale');
  });
  it('counts Ask once and keeps conflict, calculation and verification distinct', () => {
    const current = { ...state, supply: { ...state.supply!, problems: [{ code: 19 }], commonSupplyChoices: [{}] } } as CookingUnitMeal;
    expect(operativeMealStatus(current).label).toBe('1 Klärung erforderlich');
    expect(operativeMealStatus({ ...state, subscriptionState: 2 }).label).toBe('Keine Versorgung');
    expect(operativeVerificationLabel({ ...state, verification: { kind: 1 }, verificationStale: true } as CookingUnitMeal)).toBe('Veraltet');
  });
  it('groups ten diagnoses for three missing portions without multiplying additive components', () => {
    const current = { ...state, supply: { solutions: [], candidates: [], requirements: [{ requirements: { key: 'allergy' }, portions: 3 }],
      problems: [0, 3, 10, 11, 5, 6, 7, 8].map((code): SupplyProblem => ({ code, category: 0, requirementGroupKey: 'allergy', referenceId: null, positionPath: null }))
        .concat(['main', 'side'].map(referenceId => ({ code: 15, category: 1, requirementGroupKey: 'allergy', referenceId, positionPath: null }))) } };
    expect(supplyConflictGroups(current)).toHaveLength(1);
    expect(missingPortions(supplyConflictGroups(current))).toBe(3);
    expect(operativeMealStatus(current).label).toBe('3 Personen offen');
    expect(supplyConflictGroups(current)[0].problems).toHaveLength(10);
  });
  it('does not invent missing portions when the requirement target is unavailable', () => {
    const current = { ...state, supply: { solutions: [], candidates: [], problems: [{ code: 15, category: 1, requirementGroupKey: 'unknown', referenceId: 'main', positionPath: null }] } };
    expect(missingPortions(supplyConflictGroups(current))).toBeNull();
    expect(operativeMealStatus(current).label).toBe('1 Klärung erforderlich');
  });
  it('does not count a confirmed scenario as a new unanswered question', () => {
    const current = { ...state, manualScenarioSelections: [{ offerGroupId: null, preference: 0 as const }], supply: { ...state.supply!, commonSupplyChoices: [{ offerGroupId: null, requirementGroupKeys: ['g'], togetherOptions: [], separateSolutions: [] }] } };
    expect(operativeMealStatus(current).kind).toBe('success');
  });
  it('resolves structured and legacy requirements without displaying internal identifiers', () => {
    const id = '11223344-5566-7788-9900-aabbccddeeff';
    const catalog = { dietTypes: [], substances: [], allergens: [{ id, name: 'Milch', code: 'G' }] };
    expect(requirementDescription({ key: `diet:-|allergens:${id.replaceAll('-', '')}|substances:` }, catalog)).toBe('Allergene meiden: Milch (G)');
    expect(requirementDescription({ key: 'opaque-internal-key' })).not.toContain('opaque');
    expect(requirementDescription({ key: '', dietTypeId: null, allergenIds: [id], intolerances: [] })).not.toContain(id);
    expect(requirementDescription({ key: 'diet:-|allergens:|substances:' })).toBe('Keine erfassten Sonderanforderungen');
  });
});
