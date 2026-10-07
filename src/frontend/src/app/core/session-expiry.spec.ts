import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from './api-base-url';
import { SessionExpiry, sessionExpiryInterceptor } from './session-expiry';

describe('sessionExpiryInterceptor', () => {
  let http: HttpClient;
  let requests: HttpTestingController;
  let session: SessionExpiry;
  beforeEach(() => {
    delete window.__SCP_DESKTOP__;
    TestBed.configureTestingModule({ providers: [
      provideHttpClient(withInterceptors([sessionExpiryInterceptor])), provideHttpClientTesting(),
      { provide: API_BASE_URL, useValue: 'http://localhost:5180' }
    ] });
    http = TestBed.inject(HttpClient);
    requests = TestBed.inject(HttpTestingController);
    session = TestBed.inject(SessionExpiry);
    session.signedIn();
  });
  afterEach(() => { requests.verify(); delete window.__SCP_DESKTOP__; });

  function fail(url: string, status: number) {
    let receivedStatus = 0;
    http.post(url, {}).subscribe({ error: error => receivedStatus = error.status });
    requests.expectOne(url).flush({}, { status, statusText: 'Failure' });
    expect(receivedStatus).toBe(status);
  }

  it('expires the active session on an API 401 without retrying the write', () => {
    fail('http://localhost:5180/api/packages/import-return', 401);
    expect(session.expired()).toBe(true);
    requests.expectNone('http://localhost:5180/api/packages/import-return');
    session.signedIn();
    expect(session.expired()).toBe(false);
  });
  it('leaves permission, missing-resource, network and server errors unchanged', () => {
    for (const status of [403, 404, 0, 500]) fail('http://localhost:5180/api/camps', status);
    expect(session.expired()).toBe(false);
  });
  it('does not treat a rejected login or external response as session expiry', () => {
    fail('http://localhost:5180/api/session', 401);
    fail('http://localhost:51800/api/camps', 401);
    expect(session.expired()).toBe(false);
  });
  it('leaves desktop device authentication unchanged', () => {
    window.__SCP_DESKTOP__ = { apiUrl: 'http://localhost:5180', token: 'test' };
    fail('http://localhost:5180/api/camps', 401);
    expect(session.expired()).toBe(false);
  });
  it('ignores a late failure belonging to the previous session', () => {
    const url = 'http://localhost:5180/api/camps';
    http.get(url).subscribe({ error: () => {} });
    session.signedIn();
    requests.expectOne(url).flush({}, { status: 401, statusText: 'Unauthorized' });
    expect(session.expired()).toBe(false);
  });
  it('does not show expiry before login or after explicit logout', () => {
    session.signedOut();
    fail('http://localhost:5180/api/camps', 401);
    expect(session.expired()).toBe(false);
  });
});
