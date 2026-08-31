import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { API_BASE_URL } from '../../core/api-base-url';

export enum IngredientRevisionState { Draft = 0, Published = 1 }
export enum IngredientPropertyReviewState { Unreviewed = 0, Reviewed = 1 }
export enum IngredientPropertyState { Contains = 0, DoesNotContain = 1, MayContain = 2, Unknown = 3 }
export enum IngredientPropertySource { Inherent = 0, Derived = 1, ManuallyVerified = 2, ArticleDependent = 3 }

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
}

export interface IngredientRevisionMutationResponse {
  ingredientId?: string;
  revisionId?: string;
  rowVersion: number;
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

  listCamp(campId: string) {
    return this.http.get<IngredientRevisionSummary[]>(
      `${this.baseUrl}/api/camps/${campId}/ingredient-revisions`, this.options);
  }

  createCamp(campId: string, request: { name: string; categoryId: string; baseUnitId: string }) {
    return this.http.post<IngredientRevisionMutationResponse>(
      `${this.baseUrl}/api/camps/${campId}/ingredient-revisions`, request, this.options);
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
  }) {
    return this.http.put<IngredientRevisionMutationResponse>(
      `${this.baseUrl}/api/ingredient-revisions/${revisionId}`, request, this.options);
  }

  publish(revisionId: string, expectedRowVersion: number) {
    return this.http.post<IngredientRevisionMutationResponse>(
      `${this.baseUrl}/api/ingredient-revisions/${revisionId}/publish`,
      { expectedRowVersion }, this.options);
  }
}
