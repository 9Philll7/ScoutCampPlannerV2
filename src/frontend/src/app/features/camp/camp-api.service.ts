import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { API_BASE_URL } from '../../core/api-base-url';

export interface CampSummary {
  id: string;
  tenantId: string;
  name: string;
  isFrozen: boolean;
}

@Injectable({ providedIn: 'root' })
export class CampApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  list() {
    return this.http.get<CampSummary[]>(`${this.baseUrl}/api/camps`, { withCredentials: true });
  }

  startOfflineTransfer(campId: string) {
    return this.http.post(`${this.baseUrl}/api/camps/${campId}/offline-package`, null, {
      responseType: 'blob', withCredentials: true
    });
  }
}
