import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { API_BASE_URL } from '../../core/api-base-url';

export enum IngredientRevisionState { Draft = 0, Published = 1 }
export enum IngredientPropertyReviewState { Unreviewed = 0, Reviewed = 1 }
export enum IngredientPropertyState { Contains = 0, DoesNotContain = 1, MayContain = 2, Unknown = 3 }
export enum IngredientPropertySource { Inherent = 0, Derived = 1, ManuallyVerified = 2, ArticleDependent = 3 }
export enum IngredientConversionPrecision { Exact = 0, Average = 1, Estimated = 2 }
export enum IngredientNutritionReviewState { Unreviewed = 0, Reviewed = 1 }
export enum IngredientNutritionSourceType { Manufacturer = 0, OfficialDatabase = 1, ManualEstimate = 2 }
export enum IngredientSubstanceContentSourceType { Manufacturer = 0, OfficialDatabase = 1, ManualEstimate = 2 }
export enum IngredientSubstanceContentReviewState { Unreviewed = 0, Reviewed = 1 }

export interface IngredientCategoryReference {
  id: string;
  parentCategoryId: string | null;
  code: string;
  name: string;
}

export interface MeasurementUnitReference {
  id: string;
  name: string;
  symbol: string;
  dimension: number;
  baseUnitFactor: number;
}

export interface IngredientAllergenReference {
  id: string;
  parentAllergenId: string | null;
  code: string;
  name: string;
  isEuMajorAllergen: boolean;
}

export interface IngredientIntoleranceReference {
  id: string;
  code: string;
  name: string;
  isQuantityDependent: boolean;
}

export interface IngredientOriginReference {
  id: string;
  code: string;
  name: string;
  isAnimalOrigin: boolean;
}

export interface IngredientEditorReferenceData {
  categories: IngredientCategoryReference[];
  units: MeasurementUnitReference[];
  allergens: IngredientAllergenReference[];
  intolerances: IngredientIntoleranceReference[];
  origins: IngredientOriginReference[];
}

export interface IngredientRevisionPropertyItem {
  propertyId: string;
  state: IngredientPropertyState;
  source: IngredientPropertySource;
}

export interface IngredientRevisionUnitConversionItem {
  sourceUnitId: string;
  factorToBaseUnit: number;
  precision: IngredientConversionPrecision;
  factorInput?: string;
}

export interface IngredientNutritionProfileItem {
  referenceQuantity: number;
  referenceUnitId: string;
  energyKilojoules: number | null;
  fatGrams: number | null;
  saturatedFatGrams: number | null;
  carbohydrateGrams: number | null;
  sugarsGrams: number | null;
  proteinGrams: number | null;
  saltGrams: number | null;
  fiberGrams: number | null;
  sourceType: IngredientNutritionSourceType;
  sourceReference: string;
  reviewState: IngredientNutritionReviewState;
  referenceDate: string | null;
}

export interface IngredientSubstanceContentItem {
  substanceId: string;
  amount: number;
  amountUnitId: string;
  referenceQuantity: number;
  referenceUnitId: string;
  sourceType: IngredientSubstanceContentSourceType;
  sourceReference: string;
  reviewState: IngredientSubstanceContentReviewState;
}

export interface IngredientVariantRevisionItem {
  id: string;
  variantKey: string;
  name: string;
  isActive: boolean;
  sortOrder: number;
  allergenOverrides: IngredientRevisionPropertyItem[];
  intoleranceOverrides: IngredientRevisionPropertyItem[];
  originOverrides: IngredientRevisionPropertyItem[];
  unitConversionOverrides: IngredientRevisionUnitConversionItem[];
  nutritionProfile: IngredientNutritionProfileItem | null;
  substanceContentOverrides: IngredientSubstanceContentItem[];
  isNew?: boolean;
}

export interface IngredientRevisionSummary {
  ingredientId: string;
  revisionId: string;
  name: string;
  state: IngredientRevisionState;
  rowVersion: number;
}

export interface IngredientRevisionDetails {
  id: string;
  ingredientId: string;
  scopeType: number;
  scopeId: string | null;
  name: string;
  categoryId: string;
  baseUnitId: string;
  state: IngredientRevisionState;
  rowVersion: number;
  allergenReviewState: IngredientPropertyReviewState;
  intoleranceReviewState: IngredientPropertyReviewState;
  originReviewState: IngredientPropertyReviewState;
  allergens: IngredientRevisionPropertyItem[];
  intolerances: IngredientRevisionPropertyItem[];
  origins: IngredientRevisionPropertyItem[];
  unitConversions: IngredientRevisionUnitConversionItem[];
  variants: IngredientVariantRevisionItem[];
  nutritionProfile: IngredientNutritionProfileItem | null;
  substanceContents: IngredientSubstanceContentItem[];
  sourceSummary: string;
}

export interface CentralIngredientCandidate {
  ingredientId: string;
  revisionId: string;
  name: string;
  categoryId: string;
  baseUnitId: string;
}

export interface IngredientCentralContribution {
  id: string;
  submittedRevisionId: string;
  localIngredientId: string;
  sourceScopeType: number;
  sourceScopeId: string;
  name: string;
  categoryId: string;
  baseUnitId: string;
  suggestedCentralIngredientId: string | null;
  suggestedCentralIngredientName: string | null;
  submittedAtUtc: string;
  submittedBy: string;
}

export interface IngredientContributionMutationResponse {
  contributionId?: string;
  centralIngredientId?: string;
  centralRevisionId?: string;
}

export interface IngredientRevisionMutationResponse {
  ingredientId?: string;
  revisionId?: string;
  rowVersion: number;
}

export interface IngredientSuggestionNutrition {
  energyKilojoules: number | null;
  fatGrams: number | null;
  saturatedFatGrams: number | null;
  carbohydrateGrams: number | null;
  sugarsGrams: number | null;
  proteinGrams: number | null;
  saltGrams: number | null;
  fiberGrams: number | null;
}

export interface IngredientSuggestionSubstance {
  code: string;
  amountGrams: number;
}

export interface IngredientDataSuggestion {
  provider: string;
  sourceKey: string;
  name: string;
  referenceQuantity: number;
  referenceUnitSymbol: string;
  sourceSummary: string;
  nutrition: IngredientSuggestionNutrition;
  substanceContents: IngredientSuggestionSubstance[];
}

export interface IngredientSuggestionSearchResult {
  isAvailable: boolean;
  unavailableReason: string | null;
  suggestions: IngredientDataSuggestion[];
}

@Injectable({ providedIn: 'root' })
export class IngredientRevisionApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);
  private readonly options = { withCredentials: true } as const;

  getReferenceData() {
    return this.http.get<IngredientEditorReferenceData>(
      `${this.baseUrl}/api/ingredient-reference-data`, this.options);
  }

  searchSuggestions(provider: string, query: string) {
    return this.http.get<IngredientSuggestionSearchResult>(
      `${this.baseUrl}/api/ingredient-suggestions/${encodeURIComponent(provider)}`,
      { ...this.options, params: { query } });
  }

  listCamp(campId: string) {
    return this.http.get<IngredientRevisionSummary[]>(
      `${this.baseUrl}/api/camps/${campId}/ingredient-revisions`, this.options);
  }

  listCentral() {
    return this.http.get<IngredientRevisionSummary[]>(
      `${this.baseUrl}/api/ingredients/central/revisions`, this.options);
  }

  listTenant(tenantId: string) {
    return this.http.get<IngredientRevisionSummary[]>(
      `${this.baseUrl}/api/tenants/${tenantId}/ingredient-revisions`, this.options);
  }

  getCampForkPreview(campId: string, sourceRevisionId: string) {
    return this.http.get<IngredientRevisionDetails>(
      `${this.baseUrl}/api/camps/${campId}/ingredient-revisions/${sourceRevisionId}/fork-preview`, this.options);
  }

  createCamp(campId: string, request: { name: string; categoryId: string; baseUnitId: string }) {
    return this.http.post<IngredientRevisionMutationResponse>(
      `${this.baseUrl}/api/camps/${campId}/ingredient-revisions`, request, this.options);
  }

  createCentral(request: { name: string; categoryId: string; baseUnitId: string }) {
    return this.http.post<IngredientRevisionMutationResponse>(
      `${this.baseUrl}/api/ingredients/central/revisions`, request, this.options);
  }

  createTenant(tenantId: string, request: { name: string; categoryId: string; baseUnitId: string }) {
    return this.http.post<IngredientRevisionMutationResponse>(
      `${this.baseUrl}/api/tenants/${tenantId}/ingredient-revisions`, request, this.options);
  }

  get(revisionId: string) {
    return this.http.get<IngredientRevisionDetails>(
      `${this.baseUrl}/api/ingredient-revisions/${revisionId}`, this.options);
  }

  save(revisionId: string, request: {
    name: string;
    categoryId: string;
    baseUnitId: string;
    allergenReviewState: IngredientPropertyReviewState;
    intoleranceReviewState: IngredientPropertyReviewState;
    originReviewState: IngredientPropertyReviewState;
    expectedRowVersion: number;
    allergens: IngredientRevisionPropertyItem[];
    intolerances: IngredientRevisionPropertyItem[];
    origins: IngredientRevisionPropertyItem[];
    unitConversions: IngredientRevisionUnitConversionItem[];
    nutritionProfile: IngredientNutritionProfileItem | null;
    substanceContents: IngredientSubstanceContentItem[];
    sourceSummary: string;
    variants: {
      id: string;
      variantKey: string;
      name: string;
      isActive: boolean;
      sortOrder: number;
      allergenOverrides: IngredientRevisionPropertyItem[];
      intoleranceOverrides: IngredientRevisionPropertyItem[];
      originOverrides: IngredientRevisionPropertyItem[];
      unitConversionOverrides: IngredientRevisionUnitConversionItem[];
      nutritionProfile: IngredientNutritionProfileItem | null;
      substanceContentOverrides: IngredientSubstanceContentItem[];
    }[];
  }) {
    return this.http.put<IngredientRevisionMutationResponse>(
      `${this.baseUrl}/api/ingredient-revisions/${revisionId}`, request, this.options);
  }

  createCampFork(campId: string, sourceRevisionId: string, request: {
    name: string;
    categoryId: string;
    baseUnitId: string;
    allergenReviewState: IngredientPropertyReviewState;
    intoleranceReviewState: IngredientPropertyReviewState;
    originReviewState: IngredientPropertyReviewState;
    expectedSourceRowVersion: number;
    allergens: IngredientRevisionPropertyItem[];
    intolerances: IngredientRevisionPropertyItem[];
    origins: IngredientRevisionPropertyItem[];
    unitConversions: IngredientRevisionUnitConversionItem[];
    nutritionProfile: IngredientNutritionProfileItem | null;
    substanceContents: IngredientSubstanceContentItem[];
    sourceSummary: string;
    variants: {
      id: string;
      variantKey: string;
      name: string;
      isActive: boolean;
      sortOrder: number;
      allergenOverrides: IngredientRevisionPropertyItem[];
      intoleranceOverrides: IngredientRevisionPropertyItem[];
      originOverrides: IngredientRevisionPropertyItem[];
      unitConversionOverrides: IngredientRevisionUnitConversionItem[];
      nutritionProfile: IngredientNutritionProfileItem | null;
      substanceContentOverrides: IngredientSubstanceContentItem[];
    }[];
  }) {
    return this.http.post<IngredientRevisionMutationResponse>(
      `${this.baseUrl}/api/camps/${campId}/ingredient-revisions/${sourceRevisionId}/fork`, request, this.options);
  }

  publish(revisionId: string, expectedRowVersion: number) {
    return this.http.post<IngredientRevisionMutationResponse>(
      `${this.baseUrl}/api/ingredient-revisions/${revisionId}/publish`,
      { expectedRowVersion }, this.options);
  }

  createDraftFromPublished(revisionId: string) {
    return this.http.post<IngredientRevisionMutationResponse>(
      `${this.baseUrl}/api/ingredient-revisions/${revisionId}/draft`, {}, this.options);
  }

  findCentralCandidates(revisionId: string) {
    return this.http.get<CentralIngredientCandidate[]>(
      `${this.baseUrl}/api/ingredient-revisions/${revisionId}/central-candidates`, this.options);
  }

  submitToCentral(revisionId: string) {
    return this.http.post<IngredientContributionMutationResponse>(
      `${this.baseUrl}/api/ingredient-revisions/${revisionId}/central-contributions`, {}, this.options);
  }

  replaceWithCentral(revisionId: string, centralRevisionId: string) {
    return this.http.post<IngredientContributionMutationResponse>(
      `${this.baseUrl}/api/ingredient-revisions/${revisionId}/replace-with-central`,
      { centralRevisionId }, this.options);
  }

  listCentralContributions() {
    return this.http.get<IngredientCentralContribution[]>(
      `${this.baseUrl}/api/ingredient-central-contributions`, this.options);
  }

  acceptCentralContribution(contributionId: string, targetCentralIngredientId: string | null) {
    return this.http.post<IngredientContributionMutationResponse>(
      `${this.baseUrl}/api/ingredient-central-contributions/${contributionId}/accept`,
      { targetCentralIngredientId }, this.options);
  }

  rejectCentralContribution(contributionId: string) {
    return this.http.post<IngredientContributionMutationResponse>(
      `${this.baseUrl}/api/ingredient-central-contributions/${contributionId}/reject`, {}, this.options);
  }
}
