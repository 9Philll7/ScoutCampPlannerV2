import { InjectionToken } from '@angular/core';

export const API_BASE_URL = new InjectionToken<string>('API_BASE_URL', {
  providedIn: 'root',
  factory: () => window.__SCP_DESKTOP__?.apiUrl ?? `http://${window.location.hostname === 'localhost' ? 'localhost' : '127.0.0.1'}:5180`
});
