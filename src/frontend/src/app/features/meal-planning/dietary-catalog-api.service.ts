import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { API_BASE_URL } from '../../core/api-base-url';
import { RequirementOption } from '../camp/participants-api.service';

export interface DietaryType { id: string; tenantId: string | null; name: string; description: string | null;
  sortOrder: number; version: number; rules: { originId: string; decision: number }[]; }
export interface DietaryCatalog { types: DietaryType[]; origins: { id: string; name: string; code: string }[]; substances: RequirementOption[]; }
export interface DietaryContribution { id: string; submitted: DietaryType; submittedBy: string; submittedAtUtc: string; status: number; centralId: string | null; }

@Injectable({ providedIn: 'root' })
export class DietaryCatalogApiService {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);
  private readonly options = { withCredentials: true } as const;
  private path(tenantId: string | null) { return `${this.base}/api/${tenantId ? `tenants/${tenantId}` : 'central'}/diet-types`; }
  list(tenantId: string | null) { return this.http.get<DietaryCatalog>(this.path(tenantId), this.options); }
  save(tenantId: string | null, value: DietaryType) {
    return this.http.put(this.path(tenantId) + '/' + value.id,
      { ...value, expectedVersion: value.version }, this.options);
  }
  submit(tenantId: string, value: DietaryType) {
    return this.http.post(`${this.path(tenantId)}/${value.id}/contributions`, { version: value.version }, this.options);
  }
  contributions() { return this.http.get<DietaryContribution[]>(`${this.base}/api/central/diet-type-contributions`, this.options); }
  review(id: string, accept: boolean, targetCentralId: string | null) {
    return this.http.post(`${this.base}/api/central/diet-type-contributions/${id}/review`, { accept, targetCentralId }, this.options);
  }
  saveThreshold(value: RequirementOption) {
    return this.http.put(`${this.base}/api/central/substances/${value.id}/threshold-default`, {
      gramsPerPortion: value.defaultThresholdGramsPerPortion ?? null, source: value.defaultThresholdSource ?? null,
      expectedVersion: value.defaultThresholdVersion ?? 0 }, this.options);
  }
}
