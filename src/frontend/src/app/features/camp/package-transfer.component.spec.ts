import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { API_BASE_URL } from '../../core/api-base-url';
import { PackageTransferComponent } from './package-transfer.component';
import '../../core/desktop-runtime';
const nativeInvoke = vi.fn();

describe('Camp return import', () => {
  beforeEach(() => {
    nativeInvoke.mockReset();
    window.__SCP_DESKTOP__ = { apiUrl: 'http://localhost:5180', token: 'test' };
    window.__TAURI__ = { core: { invoke: nativeInvoke } };
  });
  afterEach(() => { TestBed.inject(HttpTestingController).verify(); vi.restoreAllMocks(); delete window.__SCP_DESKTOP__; delete window.__TAURI__; });
  function setup() {
    TestBed.configureTestingModule({ imports: [PackageTransferComponent], providers: [
      provideHttpClient(), provideHttpClientTesting(), { provide: API_BASE_URL, useValue: 'http://localhost:5180' },
    ] });
    const fixture = TestBed.createComponent(PackageTransferComponent);
    fixture.componentRef.setInput('campId', 'selected-camp');
    const event = { target: { files: [new File(['package'], 'return.scoutcamp')], value: 'file' } } as unknown as Event;
    return { component: fixture.componentInstance, event, http: TestBed.inject(HttpTestingController) };
  }
  it('does not import when confirmation is cancelled', async () => {
    const { component, event } = setup();
    vi.spyOn(window, 'confirm').mockReturnValue(false);
    await component.openReturn(event);
    expect(component.busy()).toBe(false);
  });
  it('does not contact the backend if native file transfer fails', async () => {
    const { component } = setup();
    nativeInvoke.mockRejectedValueOnce(new TypeError('Failed to fetch'));
    await component.openLocal();
    expect(component.message()).toContain('Es wurde kein Import gestartet');
    expect(component.busy()).toBe(false);
  });
  it('does not contact the backend if the native dialog is cancelled', async () => {
    const { component } = setup();
    nativeInvoke.mockResolvedValueOnce(null);
    await component.openLocal();
    expect(component.message()).toBe('');
  });
  it('imports the completed native file exactly once', async () => {
    const { component, http } = setup();
    nativeInvoke.mockResolvedValueOnce({ id: 'file', size: 3 }).mockResolvedValueOnce([1, 2, 3]).mockResolvedValueOnce(undefined);
    const pending = component.openLocal();
    for (let tick = 0; tick < 10; tick++) await Promise.resolve();
    const request = http.expectOne('http://localhost:5180/api/packages/import-initial');
    expect(request.request.body).toBeInstanceOf(Blob);
    expect((request.request.body as Blob).size).toBe(3);
    request.flush(null); await pending;
    expect(component.message()).toContain('erfolgreich');
  });
  it('binds the import to the selected card and refreshes after success', async () => {
    const { component, event, http } = setup();
    vi.spyOn(window, 'confirm').mockReturnValue(true);
    const completed = vi.fn(); component.completed.subscribe(completed);
    const pending = component.openReturn(event);
    const request = http.expectOne('http://localhost:5180/api/packages/import-return?expectedCampId=selected-camp');
    expect(request.request.method).toBe('POST');
    expect(request.request.withCredentials).toBe(true);
    request.flush(null);
    await pending;
    expect(completed).toHaveBeenCalledOnce();
    expect(component.busy()).toBe(false);
  });
});
