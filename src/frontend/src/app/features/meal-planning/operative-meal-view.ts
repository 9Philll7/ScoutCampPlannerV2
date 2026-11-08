import { mergeSupplySolutions } from './supply-alternative-choices';
import { CookingUnitMeal, RequirementLabelCatalog, RequirementSignature, SupplyProblem, SupplySolution } from './meal-planning-api.service';

export const emptyRequirementCatalog: RequirementLabelCatalog = { dietTypes: [], allergens: [], substances: [] };
const normalizeId = (value: string) => value.replaceAll('-', '').toLowerCase();

/** Legacy snapshots can contain only the stable signature, newer DTOs include its structured fields. */
export function requirementParts(requirement: RequirementSignature): RequirementSignature | null {
  if (requirement.allergenIds !== undefined && requirement.intolerances !== undefined && requirement.dietTypeId !== undefined) return requirement;
  const match = /^diet:([a-f0-9]{32}|-)\|allergens:([^|]*)\|substances:(.*)$/i.exec(requirement.key);
  if (!match) return null;
  const allergens = match[2] ? match[2].split(',') : [];
  const substances = match[3] ? match[3].split(',') : [];
  if (allergens.some(id => !/^[a-f0-9]{32}$/i.test(id)) || substances.some(value => !/^[a-f0-9]{32}=(\?|\d+(\.\d+)?([eE][+-]?\d+)?)$/.test(value))) return null;
  return { key: requirement.key, dietTypeId: match[1] === '-' ? null : match[1], allergenIds: allergens,
    intolerances: substances.map(value => ({ substanceId: value.split('=')[0], thresholdGramsPerPortion: value.split('=')[1] === '?' ? null : Number(value.split('=')[1]) })) };
}
export function requirementDescription(signature: RequirementSignature, catalog: RequirementLabelCatalog = emptyRequirementCatalog) {
  const requirement = requirementParts(signature);
  if (!requirement) return 'Gespeicherte Anforderungen (Details nicht verfügbar)';
  const name = (options: { id: string; name: string; code?: string }[], id: string, fallback: string) => {
    const option = options.find(option => normalizeId(option.id) === normalizeId(id));
    return option ? option.name + (option.code ? ` (${option.code})` : '') : fallback;
  };
  const labels: string[] = [];
  if (requirement.dietTypeId) labels.push(name(catalog.dietTypes, requirement.dietTypeId, 'Ernährungsform hinterlegt'));
  if (requirement.allergenIds?.length) labels.push('Allergene meiden: ' + requirement.allergenIds.map(id => name(catalog.allergens, id, 'Allergenanforderung')).join(', '));
  if (requirement.intolerances?.length) labels.push('Unverträglichkeit: ' + requirement.intolerances.map(value => name(catalog.substances, value.substanceId, 'Stoffanforderung') +
    (value.thresholdGramsPerPortion === null ? ' (Grenzwert unbekannt)' : ` (Grenzwert ${value.thresholdGramsPerPortion.toLocaleString('de')} g/Portion)`)).join(', '));
  return labels.join(' · ') || 'Keine erfassten Sonderanforderungen';
}
export function hasNamedRequirements(signature: RequirementSignature) {
  const parts = requirementParts(signature); return !!parts && (!!parts.dietTypeId || !!parts.allergenIds?.length || !!parts.intolerances?.length);
}
/** Several resolver diagnostics can describe one unmet requirement, not additional portions. */
export function supplyConflictGroups(state?: CookingUnitMeal | null) {
  const groups = new Map<string | null, { key: string | null; problems: SupplyProblem[]; coverage: { offerGroupId: string | null; missing: number | null }[] }>();
  for (const problem of state?.supply?.problems ?? []) {
    if (problem.code === 19 && state?.supply?.commonSupplyChoices?.length) continue;
    const key = problem.requirementGroupKey ?? null;
    const group = groups.get(key) ?? { key, problems: [], coverage: [] };
    group.problems.push(problem); groups.set(key, group);
    if (problem.code !== 15 || group.coverage.some(value => value.offerGroupId === problem.referenceId)) continue;
    const target = (state?.supply?.requirements ?? state?.requirementGroups ?? []).find(value => value.requirements.key === key)?.portions;
    const supplied = (state?.supply?.solutions ?? []).filter(value => value.offerGroupId === problem.referenceId)
      .flatMap(value => value.allocations).filter(value => value.requirementGroupKey === key).reduce((sum, value) => sum + value.portions, 0);
    group.coverage.push({ offerGroupId: problem.referenceId, missing: target === undefined ? null : Math.max(0, target - supplied) });
  }
  return [...groups.values()];
}
export function missingPortions(groups: ReturnType<typeof supplyConflictGroups>) {
  const coverage = groups.flatMap(group => group.coverage);
  if (!coverage.length || coverage.some(value => value.missing === null)) return null;
  // The same requirement can miss a main dish AND a side; never count those portions twice.
  return groups.reduce((sum, group) => sum + Math.max(0, ...group.coverage.map(value => value.missing ?? 0)), 0);
}
export function operativeMealStatus(state?: CookingUnitMeal | null) {
  if (!state?.calculatedAtUtc || !state.supply) return { kind: 'unknown', label: 'Nicht berechnet' };
  if (state.status === 1) return { kind: 'stale', label: 'Veraltet' };
  const groups = supplyConflictGroups(state), missing = missingPortions(groups);
  const unanswered = (state.supply.commonSupplyChoices ?? []).filter(choice => !state.manualScenarioSelections?.some(value => value.offerGroupId === choice.offerGroupId && value.preference !== 1) &&
    !choice.requirementGroupKeys?.every(key => state.manualSupplySolutions?.some(solution => solution.offerGroupId === choice.offerGroupId && solution.allocations.some(allocation => allocation.requirementGroupKey === key)))).length;
  const conflicts = groups.length + (state.participantProblems?.length ? 1 : 0) + unanswered;
  if (conflicts) return { kind: 'warning', label: missing !== null && missing > 0
    ? `${missing.toLocaleString('de')} ${missing === 1 ? 'Person offen' : 'Personen offen'}`
    : `${conflicts} ${conflicts === 1 ? 'Klärung erforderlich' : 'Klärungen erforderlich'}` };
  if (state.status !== 0) return { kind: 'warning', label: 'Unvollständig' };
  if (state.subscriptionState === 2) return { kind: 'neutral', label: 'Keine Versorgung' };
  return state.supply.solutions.length ? { kind: 'success', label: 'Versorgung gelöst' } : { kind: 'warning', label: 'Keine Lösung vorhanden' };
}
export function operativeVerificationLabel(state?: CookingUnitMeal | null) {
  if (!state?.verification) return 'Offen';
  if (state.verificationStale || state.status === 1) return 'Veraltet';
  return state.verification.kind === 0 ? 'Ohne Abweichung verifiziert' : 'Mit Abweichung verifiziert';
}

/** Plan choices include recipe/variant selections requested by the calculated plan. */
export function isPlanRecipeSolution(solution: SupplySolution, state: CookingUnitMeal, defaultPreference = 1, includeAutomatic = true) {
  const positionKey = (value: SupplySolution) => JSON.stringify(Object.entries(value.positionSelections)
    .map(([path, selection]) => [path, selection.replacementRuleId ?? null, selection.variantKey ?? null])
    .sort(([a], [b]) => String(a).localeCompare(String(b))));
  const options = (state.supply?.preferenceOptions ?? state.supply?.commonSupplyChoices ?? []).flatMap(choice => {
    const preference = state.manualScenarioSelections?.find(value => value.offerGroupId === choice.offerGroupId)?.preference ?? defaultPreference;
    const scenario = preference === 0 ? choice.togetherScenario : preference === 2 ? choice.separateScenario : undefined;
    return [...(includeAutomatic ? scenario?.solutions ?? [] : []), ...(scenario?.decisions.flatMap(decision => decision.options) ?? [])];
  });
  options.push(...(state.supply?.decisions?.flatMap(decision => decision.options) ?? []));
  const capacities = new Map<string, number>();
  for (const candidate of options.filter(value => value.offerGroupId === solution.offerGroupId &&
    value.recipeRevisionId === solution.recipeRevisionId && positionKey(value) === positionKey(solution)))
    for (const allocation of candidate.allocations)
      capacities.set(allocation.requirementGroupKey, Math.max(capacities.get(allocation.requirementGroupKey) ?? 0, allocation.portions));
  const allocations = solution.allocations.filter(value => value.portions > 0);
  const equal = (a: number, b: number) => Math.abs(a - b) < 1e-8;
  return allocations.length > 0 && allocations.every(value => equal(value.portions, capacities.get(value.requirementGroupKey) ?? -1)) &&
    equal(solution.portions, allocations.reduce((sum, value) => sum + value.portions, 0));
}
export function hasOwnRecipeSolutions(state?: CookingUnitMeal | null, defaultPreference = 1) {
  if (state?.completeOwnSupply) return true;
  return !!state && mergeSupplySolutions((state.manualSupplySolutions ?? []).map(value => ({ ...value, source: 0 })))
    .some(value => !isPlanRecipeSolution(value, state, defaultPreference));
}
