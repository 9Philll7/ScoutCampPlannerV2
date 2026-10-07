import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { API_BASE_URL } from './api-base-url';
import { isDesktop } from './desktop-runtime';

@Injectable({ providedIn: 'root' })
export class SessionExpiry {
  readonly expired = signal(false);
  private active = false;
  generation = 0;

  signedIn() { this.generation++; this.active = true; this.expired.set(false); }
  signedOut() { this.generation++; this.active = false; this.expired.set(false); }
  expire(generation: number) {
    if (!this.active || generation !== this.generation) return;
    this.active = false;
    this.expired.set(true);
  }
}

export const sessionExpiryInterceptor: HttpInterceptorFn = (request, next) => {
  const session = inject(SessionExpiry);
  const base = inject(API_BASE_URL).replace(/\/$/, '');
  const path = request.url.split('?')[0];
  if (isDesktop() || !path.startsWith(base + '/api/') || path === base + '/api/session')
    return next(request);
  const generation = session.generation;
  return next(request).pipe(catchError(error => {
    if (error instanceof HttpErrorResponse && error.status === 401) session.expire(generation);
    // Preserve the original failure; never retry a potentially mutating operation.
    return throwError(() => error);
  }));
};
