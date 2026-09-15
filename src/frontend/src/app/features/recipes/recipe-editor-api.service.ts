import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { API_BASE_URL } from '../../core/api-base-url';
import { IngredientCatalogEntry } from '../camp/camp-api.service';

export interface RecipeCatalogEntry {
  libraryEntryId: string | null;
  recipeId: string;
  revisionId: string | null;
  revisionNumber: number | null;
  name: string;
  scope: number;
  status: number;
  isLocal: boolean;
  updatedAtUtc: string;
}

export interface RecipeEditorConflict { type: number; id: string; }
export interface RecipeEditorGroup { id: string; name: string | null; sortOrder: number; }
export interface RecipeEditorIngredientReplacement {
  id: string; ingredientRevisionId: string | null; quantity: number | null; unitId: string | null;
  conflicts: RecipeEditorConflict[];
}
export interface RecipeEditorIngredientPosition {
  id: string; groupId: string | null; ingredientRevisionId: string | null;
  quantity: number | null; unitId: string | null; sortOrder: number;
  scalingMode: number; ageGroupScaling: number; stepSize: number | null; quantityPerStep: number | null;
  replacements: RecipeEditorIngredientReplacement[];
}
export interface RecipeEditorSubrecipeReplacement {
  id: string; recipeRevisionId: string | null; servings: number | null; quantity: number | null;
  unitId: string | null; conflicts: RecipeEditorConflict[];
}
export interface RecipeEditorSubrecipePosition {
  id: string; groupId: string | null; recipeRevisionId: string | null; servings: number | null;
  quantity: number | null; unitId: string | null; sortOrder: number;
  replacements: RecipeEditorSubrecipeReplacement[];
}
export interface RecipeEditorContent {
  name: string | null; description: string | null; source: string | null; internalNotes: string | null;
  recipeType: number; referenceServings: number | null; referenceQuantity: number | null;
  referenceUnitId: string | null; defaultAgeGroupScalingApplies: boolean | null;
  authoringStage: { stageId: string; stageName: string; factor: number } | null;
  tags: string[]; groups: RecipeEditorGroup[]; ingredientPositions: RecipeEditorIngredientPosition[];
  subrecipePositions: RecipeEditorSubrecipePosition[];
}
export interface RecipeEditorDraft {
  id: string; campId: string; status: number; draftVersion: number; content: RecipeEditorContent;
  ingredientReferences: RecipeEditorIngredientReference[];
}

export interface RecipeEditorIngredientReference {
  revisionId: string;
  ingredientId: string;
  revisionNumber: number;
  name: string;
  scope: number;
  units: { unitId: string; name: string; symbol: string; dimension: number;
    baseUnitFactor: number; referenceQuantityPerUnit: number }[];
  conflicts: { type: number; id: string; name: string; preventableByVariants: string[] }[];
  availableUpdate: RecipeEditorIngredientUpdate | null;
}

export interface RecipeEditorIngredientUpdate {
  revisionId: string;
  revisionNumber: number;
  name: string;
  units: RecipeEditorIngredientReference['units'];
  conflicts: RecipeEditorIngredientReference['conflicts'];
}

export interface RecipeNutritionValues {
  energyKilojoules: number;
  energyKilocalories: number;
  fatGrams: number;
  saturatedFatGrams: number;
  carbohydrateGrams: number;
  sugarsGrams: number;
  proteinGrams: number;
  saltGrams: number;
  fiberGrams: number | null;
}

export interface MissingNutritionContribution {
  ingredientRevisionId: string;
  ingredientName: string;
  recipeRevisionPath: string[];
  positionId: string;
  reason: number;
}

export interface RecipeNutritionCalculation {
  isComplete: boolean;
  total: RecipeNutritionValues | null;
  perStandardPortion: RecipeNutritionValues | null;
  missingContributions: MissingNutritionContribution[];
}

export interface RecipeValidationIssue {
  code: string;
  severity: number;
  message: string;
  context: Record<string, string>;
}

export interface RecipePublicationResponse {
  status: number;
  validation: { issues: RecipeValidationIssue[]; errors: RecipeValidationIssue[];
    warnings: RecipeValidationIssue[] } | null;
  revisionId: string | null;
  revisionNumber: number | null;
  draftVersion: number | null;
}

@Injectable({ providedIn: 'root' })
export class RecipeEditorApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  list(campId: string) {
    return this.http.get<RecipeCatalogEntry[]>(`${this.baseUrl}/api/camps/${campId}/recipes`,
      { withCredentials: true });
  }

  ingredients(campId: string) {
    return this.http.get<IngredientCatalogEntry[]>(`${this.baseUrl}/api/camps/${campId}/ingredients`,
      { withCredentials: true });
  }

  get(campId: string, recipeId: string) {
    return this.http.get<RecipeEditorDraft>(`${this.baseUrl}/api/camps/${campId}/recipes/${recipeId}/draft`,
      { withCredentials: true });
  }

  nutrition(campId: string, recipeId: string) {
    return this.http.get<RecipeNutritionCalculation>(
      `${this.baseUrl}/api/camps/${campId}/recipes/${recipeId}/draft/nutrition`,
      { withCredentials: true });
  }

  create(campId: string, content: RecipeEditorContent) {
    return this.http.post<RecipeEditorDraft>(`${this.baseUrl}/api/camps/${campId}/recipes/drafts`, content,
      { withCredentials: true });
  }

  save(campId: string, draft: RecipeEditorDraft) {
    return this.http.put<RecipeEditorDraft>(`${this.baseUrl}/api/camps/${campId}/recipes/${draft.id}/draft`,
      { expectedVersion: draft.draftVersion, content: draft.content }, { withCredentials: true });
  }

  publish(campId: string, draft: RecipeEditorDraft, acknowledgeWarnings: boolean) {
    return this.http.post<RecipePublicationResponse>(
      `${this.baseUrl}/api/camps/${campId}/recipes/${draft.id}/publish`,
      { expectedVersion: draft.draftVersion, acknowledgeWarnings },
      { withCredentials: true });
  }
}
