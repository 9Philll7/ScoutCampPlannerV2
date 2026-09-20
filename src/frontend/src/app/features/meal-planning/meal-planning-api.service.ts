import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { API_BASE_URL } from '../../core/api-base-url';

export type MealSubscriptionState = 0 | 1 | 2;
export type OperationalStatus = 0 | 1 | 2;
export type MealEntryRole = 0 | 1 | 2 | 3 | 4 | 5;

export interface MealSlot { id: string; mealTypeId: string; mealTypeName: string; date: string; isActive: boolean; isInsideCampPeriod: boolean; changeVersion: number; }
export interface StructureNodeOption { id: string; parentId: string | null; name: string; }
export interface MealPlanSummary { id: string; name: string; sortOrder: number; version: number; missingActiveMealCount: number; }
export interface RecipeOption { revisionId: string; recipeId: string; name: string; revisionNumber: number; isPortionBased: boolean; suggestedRole: MealEntryRole | null; }
export interface MealPlanEntryDocument { id: string; recipeRevisionId: string; isStandard: boolean; displayName: string | null; role: MealEntryRole | null; note: string | null; sortOrder: number; }
export interface MealPlanOfferGroupDocument { id: string; campMealId: string; name: string | null; sortOrder: number; entries: MealPlanEntryDocument[]; }
export interface MealPlanDocument { id: string; campId: string; name: string; sortOrder: number; version: number; offerGroups: MealPlanOfferGroupDocument[]; }
export interface CookingUnitGroup { id: string; campId: string; name: string; sortOrder: number; }
export interface CookingUnit { id: string; campId: string; name: string; sortOrder: number; groupId: string | null; standardMealPlanId: string | null; defaultStructureNodeIds: string[]; }
export interface OfferTarget { id: string; offerGroupId: string; targetOverride: number | null; }
export interface RecipeChoice { id: string; recipeRevisionId: string; offerGroupId: string | null; mealPlanEntryId: string | null; sortOrder: number; }
export interface CookingUnitMeal {
  id: string; cookingUnitId: string; campMealId: string; subscriptionState: MealSubscriptionState;
  demandOverride: number | null; calculatedDemand: number | null; effectiveDemand: number | null;
  status: OperationalStatus; mealPlanId: string | null; mealPlanVersion: number | null;
  calculatedAtUtc: string | null; structureOverrideNodeIds: string[]; offerTargets: OfferTarget[];
  recipeChoices: RecipeChoice[]; warnings: string[];
}
export interface MealPlanningOverview {
  campId: string; meals: MealSlot[]; structureNodes: StructureNodeOption[]; mealPlans: MealPlanSummary[];
  mealPlanDocuments: MealPlanDocument[];
  cookingUnitGroups: CookingUnitGroup[]; cookingUnits: CookingUnit[]; operationalMeals: CookingUnitMeal[];
  recipeOptions: RecipeOption[]; coverageWarnings: string[];
}
export interface MutationResult { id: string | null; version: number | null; }

@Injectable({ providedIn: 'root' })
export class MealPlanningApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);
  private options = { withCredentials: true } as const;

  overview(campId: string) { return this.http.get<MealPlanningOverview>(`${this.baseUrl}/api/camps/${campId}/meal-planning`, this.options); }
  plan(campId: string, planId: string) { return this.http.get<MealPlanDocument>(`${this.baseUrl}/api/camps/${campId}/meal-plans/${planId}`, this.options); }
  createPlan(campId: string, name: string) { return this.http.post<MutationResult>(`${this.baseUrl}/api/camps/${campId}/meal-plans`, { name }, this.options); }
  reorderPlans(campId: string, mealPlanIds: string[]) { return this.http.put<MutationResult>(`${this.baseUrl}/api/camps/${campId}/meal-plans/order`, { mealPlanIds }, this.options); }
  savePlan(campId: string, plan: MealPlanDocument) { return this.http.put<MutationResult>(`${this.baseUrl}/api/camps/${campId}/meal-plans/${plan.id}`, {
    expectedVersion: plan.version, name: plan.name, sortOrder: plan.sortOrder, offerGroups: plan.offerGroups,
  }, this.options); }
  deletePlan(campId: string, planId: string) { return this.http.delete<MutationResult>(`${this.baseUrl}/api/camps/${campId}/meal-plans/${planId}`, this.options); }
  createGroup(campId: string, value: { name: string; sortOrder: number }) { return this.http.post<MutationResult>(`${this.baseUrl}/api/camps/${campId}/cooking-unit-groups`, value, this.options); }
  saveGroup(campId: string, group: CookingUnitGroup) { return this.http.put<MutationResult>(`${this.baseUrl}/api/camps/${campId}/cooking-unit-groups/${group.id}`, { name: group.name, sortOrder: group.sortOrder }, this.options); }
  deleteGroup(campId: string, groupId: string) { return this.http.delete<MutationResult>(`${this.baseUrl}/api/camps/${campId}/cooking-unit-groups/${groupId}`, this.options); }
  createUnit(campId: string, value: Partial<CookingUnit> & { name: string; sortOrder: number; initialStructureNodeId?: string | null }) { return this.http.post<MutationResult>(`${this.baseUrl}/api/camps/${campId}/cooking-units`, value, this.options); }
  saveUnit(campId: string, unit: CookingUnit) { return this.http.put<MutationResult>(`${this.baseUrl}/api/camps/${campId}/cooking-units/${unit.id}`, unit, this.options); }
  deleteUnit(campId: string, unitId: string) { return this.http.delete<MutationResult>(`${this.baseUrl}/api/camps/${campId}/cooking-units/${unitId}`, this.options); }
  configureMeal(campId: string, unitId: string, mealId: string, value: {
    subscriptionState: MealSubscriptionState; demandOverride: number | null; structureOverrideNodeIds: string[] | null;
    offerTargets: OfferTarget[]; recipeChoices: RecipeChoice[];
  }) { return this.http.put<MutationResult>(`${this.baseUrl}/api/camps/${campId}/cooking-units/${unitId}/meals/${mealId}`, value, this.options); }
  resetStructure(campId: string, unitId: string, mealId: string) { return this.http.delete<MutationResult>(`${this.baseUrl}/api/camps/${campId}/cooking-units/${unitId}/meals/${mealId}/structure-override`, this.options); }
  calculate(campId: string, unitId: string, mealId: string) { return this.http.post<MutationResult>(`${this.baseUrl}/api/camps/${campId}/cooking-units/${unitId}/meals/${mealId}/calculate`, {}, this.options); }
}
