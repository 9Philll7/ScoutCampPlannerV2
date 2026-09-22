import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { API_BASE_URL } from '../../core/api-base-url';
import { PackageTransferComponent } from './package-transfer.component';

describe('Camp return import', () => {
  afterEach(() => { TestBed.inject(HttpTestingController).verify(); vi.restoreAllMocks(); });
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
