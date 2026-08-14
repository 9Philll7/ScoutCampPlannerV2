import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { API_BASE_URL } from '../../core/api-base-url';

export interface AuthenticatedUser {
  userId: string;
  email: string;
}

@Injectable({ providedIn: 'root' })
export class AuthenticationApiService {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  current() {
    return this.http.get<AuthenticatedUser>(`${this.baseUrl}/api/session`, { withCredentials: true });
  }

  signIn(email: string, password: string) {
    return this.http.post<AuthenticatedUser>(
      `${this.baseUrl}/api/session`, { email, password }, { withCredentials: true });
  }

  signOut() {
    return this.http.delete(`${this.baseUrl}/api/session`, { withCredentials: true });
  }
}
