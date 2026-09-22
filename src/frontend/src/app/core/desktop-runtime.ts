import { HttpInterceptorFn } from '@angular/common/http';
import { retry, timer } from 'rxjs';

declare global {
  interface Window {
    __SCP_DESKTOP__?: { apiUrl: string; token: string };
    __TAURI__?: { core: { invoke<T>(command: string, args?: Record<string, unknown>): Promise<T> } };
  }
}

export const isDesktop = () => !!window.__SCP_DESKTOP__;

export const desktopInterceptor: HttpInterceptorFn = (request, next) => {
  const runtime = window.__SCP_DESKTOP__;
  if (!runtime || !request.url.startsWith(runtime.apiUrl + '/')) return next(request);
  const response = next(request.clone({ setHeaders: { 'X-ScoutCampPlanner-Device': runtime.token } }));
  // Startup migrations can take a few seconds. Never retry writes.
  return request.method === 'GET' && request.url.endsWith('/api/setup/status')
    ? response.pipe(retry({ count: 30, delay: (error) => {
        if (error.status !== 0 && error.status !== 503) throw error;
        return timer(1000);
      } })) : response;
};

export function desktopCommand<T>(name: string, args?: Record<string, unknown>): Promise<T> {
  if (!isDesktop() || !window.__TAURI__) return Promise.reject(new Error('Desktopfunktion nicht verfügbar.'));
  return window.__TAURI__.core.invoke<T>(name, args);
}
