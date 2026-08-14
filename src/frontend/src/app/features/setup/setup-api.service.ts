import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { API_BASE_URL } from '../../core/api-base-url';

export interface SetupStatus {
  isRequired: boolean;
}

export interface InitialSetupRequest {
  tenantName: string;
  email: string;
  password: string;
}

@Injectable({ providedIn: 'root' })
export class SetupApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  status() {
    return this.http.get<SetupStatus>(`${this.baseUrl}/api/setup/status`, { withCredentials: true });
  }

  complete(request: InitialSetupRequest) {
    return this.http.post(`${this.baseUrl}/api/setup`, request, { withCredentials: true });
  }
}
