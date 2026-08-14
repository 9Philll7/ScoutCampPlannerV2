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
}

export interface TenantOption { id: string; name: string; }
export interface CampAdministratorOption { membershipId: string; userId: string; email: string; }
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

  create(tenantId: string, request: CreateCampRequest) {
    return this.http.post<CampSummary>(`${this.baseUrl}/api/tenants/${tenantId}/camps`, request,
      { withCredentials: true });
  }

  startOfflineTransfer(campId: string) {
    return this.http.post(`${this.baseUrl}/api/camps/${campId}/offline-package`, null, {
      responseType: 'blob', withCredentials: true
    });
  }
}
