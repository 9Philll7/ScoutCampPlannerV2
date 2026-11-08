import { CookingUnitMeal, SupplySolution } from './meal-planning-api.service';

export interface SupplyOfferOptions { id: string; name: string | null; alternativeRevisionIds?: string[]; }
export interface AlternativeDecision { id: string; offerId: string | null; keys: string[]; candidates: SupplySolution[]; together: boolean; }
const recipeKey = (value: SupplySolution) => JSON.stringify([value.recipeRevisionId, Object.entries(value.positionSelections).sort(([a], [b]) => a.localeCompare(b))]);

/** Mirrors the existing domain merge identity; different occurrences/provenance never merge. */
export function mergeSupplySolutions(values: SupplySolution[]) {
  const result = new Map<string, SupplySolution>();
  for (const value of values) {
    const key = JSON.stringify([value.offerGroupId, value.source, recipeKey(value)]), existing = result.get(key);
    if (!existing) { result.set(key, structuredClone(value)); continue; }
    existing.portions += value.portions;
    for (const allocation of value.allocations) {
      const found = existing.allocations.find(item => item.requirementGroupKey === allocation.requirementGroupKey);
      if (found) found.portions += allocation.portions; else existing.allocations.push({ ...allocation });
    }
  }
  return [...result.values()];
}

export function alternativeDecisions(state: CookingUnitMeal, offers: SupplyOfferOptions[], preference: number): AlternativeDecision[] {
  if (state.supply?.decisions) return state.supply.decisions.map(value => ({ id: value.id, offerId: value.offerGroupId, keys: value.requirementGroupKeys, candidates: value.options, together: value.together }));
  return offers.flatMap((offer): AlternativeDecision[] => {
    if (state.supply?.commonSupplyChoices?.some(choice => choice.offerGroupId === offer.id)) return [];
    const candidates = (state.supply?.candidates ?? []).filter(value => value.offerGroupId === offer.id &&
      offer.alternativeRevisionIds?.includes(value.recipeRevisionId) && !Object.keys(value.positionSelections).length);
    const unique = (values: SupplySolution[]) => values.filter((value, index) => values.findIndex(other => recipeKey(other) === recipeKey(value)) === index);
    const keys = (values: SupplySolution[]) => [...new Set(values.flatMap(value => value.allocations.map(allocation => allocation.requirementGroupKey)))].sort();
    if (preference === 0) {
      // The backend separately assesses these multi-group candidates against ALL automatic
      // groups, including groups already served by the standard recipe.
      const common = candidates.filter(value => value.allocations.length > 1);
      if (unique(common).length > 1) {
        const required = keys(common);
        const complete = unique(common.filter(value => required.every(key => value.allocations.some(allocation => allocation.requirementGroupKey === key))));
        if (complete.length > 1) return [{ id: offer.id + ':together', offerId: offer.id, keys: required, candidates: complete, together: true }];
      }
    }
    const groupKeys = [...new Set((state.supply?.problems ?? []).filter(problem => problem.code === 4 && problem.requirementGroupKey).map(problem => problem.requirementGroupKey!))];
    const groups = groupKeys.map(key => ({ keys: [key], candidates: unique(candidates.filter(value => value.allocations.length === 1 && value.allocations[0].requirementGroupKey === key)) }))
      .filter(group => group.candidates.length > 1);
    if (preference === 2) return groups.map(group => ({ id: offer.id + ':' + group.keys[0], offerId: offer.id, ...group, together: false }));
    const clusters: typeof groups = [];
    for (const group of groups) {
      const cluster = clusters.find(cluster => cluster.candidates.some(option => group.candidates.some(value => recipeKey(value) === recipeKey(option))));
      if (!cluster) { clusters.push(group); continue; }
      cluster.candidates = cluster.candidates.flatMap(option => {
        const matching = group.candidates.find(value => recipeKey(value) === recipeKey(option));
        return matching ? mergeSupplySolutions([option, matching]) : [];
      });
      cluster.keys.push(...group.keys);
    }
    return clusters.map(group => ({ id: offer.id + ':' + group.keys.join(','), offerId: offer.id, ...group, together: false }));
  });
}
