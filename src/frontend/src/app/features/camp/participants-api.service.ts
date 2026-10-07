import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { API_BASE_URL } from '../../core/api-base-url';

export interface ParticipantIntolerance { substanceId: string; thresholdGramsPerPortion: number | null; thresholdSource: string | null; }
export interface ParticipantData {
  id?: string; campId?: string; displayName: string; dietTypeId: string | null;
  structureNodeId?: string | null;
  absentDays: string[]; absentMealIds: string[]; allergenIds: string[]; intolerances: ParticipantIntolerance[];
}
export interface ParticipantDocument { data: ParticipantData & { id: string }; stateToken: string; }
export interface RequirementOption { id: string; code: string; name: string; isQuantityDependent: boolean; parentId: string | null;
  defaultThresholdGramsPerPortion?: number | null; defaultThresholdSource?: string | null; defaultThresholdVersion?: number; }
export interface ParticipantsOverview {
  dummyDataOnly: boolean; startDate: string | null; endDate: string | null; isFrozen: boolean; canEdit: boolean;
  participants: ParticipantDocument[]; meals: { id: string; date: string; name: string; isActive: boolean }[];
  structureNodes?: { id: string; parentId: string | null; name: string }[];
  participantStructureDepth?: number | null;
  catalog: { allergens: RequirementOption[]; substances: RequirementOption[]; dietTypes: RequirementOption[] };
}
export interface ExplicitPermissionMember { membershipId: string; userId: string; displayName: string; permissions: string[]; }

@Injectable({ providedIn: 'root' })
export class ParticipantsApiService {
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);
  private readonly options = { withCredentials: true } as const;
  private url(camp: string) { return `${this.base}/api/camps/${camp}`; }
  list(camp: string) { return this.http.get<ParticipantsOverview>(`${this.url(camp)}/participants`, this.options); }
  create(camp: string, data: ParticipantData) { return this.http.post(`${this.url(camp)}/participants`, data, this.options); }
  update(camp: string, document: ParticipantDocument, data: ParticipantData) {
    return this.http.put(`${this.url(camp)}/participants/${document.data.id}`, { expectedStateToken: document.stateToken, data }, this.options);
  }
  remove(camp: string, document: ParticipantDocument) {
    return this.http.delete(`${this.url(camp)}/participants/${document.data.id}`,
      { ...this.options, headers: { 'If-Match': `"${document.stateToken}"` } });
  }
  members(camp: string) { return this.http.get<ExplicitPermissionMember[]>(`${this.url(camp)}/explicit-permissions`, this.options); }
  grant(camp: string, membership: string, permission: string, granted: boolean) {
    return this.http.put(`${this.url(camp)}/explicit-permissions/${membership}`, { permission, granted }, this.options);
  }
}
