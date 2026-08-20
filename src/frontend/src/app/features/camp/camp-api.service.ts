import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { API_BASE_URL } from '../../core/api-base-url';

export interface CampSummary {
  id: string;
  tenantId: string;
  name: string;
  startDate: string | null;
  endDate: string | null;
  isFrozen: boolean;
  canEdit: boolean;
  canExport: boolean;
}

export interface TenantOption { id: string; name: string; }
export interface CampAdministratorOption { membershipId: string; userId: string; email: string; }
export interface StructureNodeSummary { id: string; campId: string; parentId: string | null; name: string; }
export interface StructureConfiguration { mode: 'Free' | 'Fixed'; levelNames: string[]; }
export interface StageTemplateEntry { id: string; name: string; sortOrder: number; }
export interface ParticipantEstimate { campStageId: string; stageName: string; childYouthCount: number; leaderCount: number; }
export interface CampPlanningSummary {
  stageTotals: { campStageId: string; stageName: string; childYouthCount: number; leaderCount: number }[];
  structureTotals: { structureNodeId: string; childYouthCount: number; leaderCount: number }[];
}
export interface TenantStageFoodFactor { stageName: string; factor: number; }
export interface CampStageFoodFactor { campStageId: string; stageName: string; factor: number; }
export interface WeightedStageTotal extends CampStageFoodFactor {
  childYouthCount: number; leaderCount: number; foodUnits: number;
}
export interface CampMealType { id: string; name: string; sortOrder: number; }
export interface CampMeal { id: string; mealTypeId: string; mealTypeName: string; date: string; isActive: boolean; }
export interface CampMealPlan { mealTypes: CampMealType[]; meals: CampMeal[]; }
export interface CreateCampRequest {
  name: string;
  startDate: string;
  endDate: string;
  initialAdministratorMembershipIds: string[];
}

@Injectable({ providedIn: 'root' })
export class CampApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  listTenants() {
    return this.http.get<TenantOption[]>(`${this.baseUrl}/api/tenants`, { withCredentials: true });
  }

  list(tenantId: string) {
    return this.http.get<CampSummary[]>(`${this.baseUrl}/api/tenants/${tenantId}/camps`, { withCredentials: true });
  }

  listAdministratorCandidates(tenantId: string) {
    return this.http.get<CampAdministratorOption[]>(
      `${this.baseUrl}/api/tenants/${tenantId}/camp-administrator-candidates`, { withCredentials: true });
  }

  getStageTemplate(tenantId: string) {
    return this.http.get<StageTemplateEntry[]>(`${this.baseUrl}/api/tenants/${tenantId}/stage-template`,
      { withCredentials: true });
  }

  updateStageTemplate(tenantId: string, stageNames: string[]) {
    return this.http.put<void>(`${this.baseUrl}/api/tenants/${tenantId}/stage-template`,
      { stageNames }, { withCredentials: true });
  }

  getTenantStageFoodFactors(tenantId: string) {
    return this.http.get<TenantStageFoodFactor[]>(`${this.baseUrl}/api/tenants/${tenantId}/catering-stage-factors`,
      { withCredentials: true });
  }

  updateTenantStageFoodFactors(tenantId: string, factors: TenantStageFoodFactor[]) {
    return this.http.put<void>(`${this.baseUrl}/api/tenants/${tenantId}/catering-stage-factors`,
      { factors }, { withCredentials: true });
  }

  getCampStages(campId: string) {
    return this.http.get<StageTemplateEntry[]>(`${this.baseUrl}/api/camps/${campId}/stages`, { withCredentials: true });
  }

  updateCampStages(campId: string, stageNames: string[]) {
    return this.http.put<void>(`${this.baseUrl}/api/camps/${campId}/stages`, { stageNames }, { withCredentials: true });
  }

  getParticipantEstimates(campId: string, nodeId: string) {
    return this.http.get<ParticipantEstimate[]>(`${this.baseUrl}/api/camps/${campId}/structure/${nodeId}/participant-estimates`, { withCredentials: true });
  }

  updateParticipantEstimates(campId: string, nodeId: string, estimates: ParticipantEstimate[]) {
    return this.http.put<void>(`${this.baseUrl}/api/camps/${campId}/structure/${nodeId}/participant-estimates`,
      { estimates: estimates.map(value => ({ campStageId: value.campStageId,
        childYouthCount: value.childYouthCount, leaderCount: value.leaderCount })) }, { withCredentials: true });
  }

  getPlanningSummary(campId: string) {
    return this.http.get<CampPlanningSummary>(`${this.baseUrl}/api/camps/${campId}/planning-summary`,
      { withCredentials: true });
  }

  getCampStageFoodFactors(campId: string) {
    return this.http.get<CampStageFoodFactor[]>(`${this.baseUrl}/api/camps/${campId}/catering-stage-factors`,
      { withCredentials: true });
  }

  updateCampStageFoodFactors(campId: string, factors: CampStageFoodFactor[]) {
    return this.http.put<void>(`${this.baseUrl}/api/camps/${campId}/catering-stage-factors`,
      { factors }, { withCredentials: true });
  }

  getWeightedFoodSummary(campId: string) {
    return this.http.get<WeightedStageTotal[]>(`${this.baseUrl}/api/camps/${campId}/weighted-food-summary`,
      { withCredentials: true });
  }

  getMealPlan(campId: string) {
    return this.http.get<CampMealPlan>(`${this.baseUrl}/api/camps/${campId}/meal-plan`, { withCredentials: true });
  }

  updateMealTypes(campId: string, names: string[]) {
    return this.http.put<void>(`${this.baseUrl}/api/camps/${campId}/meal-types`, { names }, { withCredentials: true });
  }

  updateMealActivity(campId: string, mealId: string, isActive: boolean) {
    return this.http.put<void>(`${this.baseUrl}/api/camps/${campId}/meals/${mealId}/activity`,
      { isActive }, { withCredentials: true });
  }

  create(tenantId: string, request: CreateCampRequest) {
    return this.http.post<CampSummary>(`${this.baseUrl}/api/tenants/${tenantId}/camps`, request,
      { withCredentials: true });
  }

  update(campId: string, request: Omit<CreateCampRequest, 'initialAdministratorMembershipIds'>) {
    return this.http.put<CampSummary>(`${this.baseUrl}/api/camps/${campId}`, request,
      { withCredentials: true });
  }

  listStructure(campId: string) {
    return this.http.get<StructureNodeSummary[]>(`${this.baseUrl}/api/camps/${campId}/structure`,
      { withCredentials: true });
  }

  createStructureNode(campId: string, parentId: string | null, name: string) {
    return this.http.post<StructureNodeSummary>(`${this.baseUrl}/api/camps/${campId}/structure`,
      { parentId, name }, { withCredentials: true });
  }

  deleteStructureNode(campId: string, nodeId: string) {
    return this.http.delete<void>(`${this.baseUrl}/api/camps/${campId}/structure/${nodeId}`,
      { withCredentials: true });
  }

  renameStructureNode(campId: string, nodeId: string, name: string) {
    return this.http.put<void>(`${this.baseUrl}/api/camps/${campId}/structure/${nodeId}`,
      { name }, { withCredentials: true });
  }

  moveStructureNode(campId: string, nodeId: string, parentId: string | null) {
    return this.http.put<void>(`${this.baseUrl}/api/camps/${campId}/structure/${nodeId}/parent`,
      { parentId }, { withCredentials: true });
  }

  getStructureConfiguration(campId: string) {
    return this.http.get<StructureConfiguration>(`${this.baseUrl}/api/camps/${campId}/structure/configuration`,
      { withCredentials: true });
  }

  updateStructureConfiguration(campId: string, levelNames: string[]) {
    return this.http.put<void>(`${this.baseUrl}/api/camps/${campId}/structure/configuration`,
      { levelNames }, { withCredentials: true });
  }

  startOfflineTransfer(campId: string) {
    return this.http.post(`${this.baseUrl}/api/camps/${campId}/offline-package`, null, {
      responseType: 'blob', withCredentials: true
    });
  }
}
