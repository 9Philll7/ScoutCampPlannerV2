import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { desktopCommand, desktopInterceptor } from './desktop-runtime';

describe('Desktop runtime', () => {
  let http: HttpTestingController;
  let client: HttpClient;
  beforeEach(() => {
    window.__SCP_DESKTOP__ = { apiUrl: 'http://127.0.0.1:5199', token: 'private-test-token' };
    TestBed.configureTestingModule({ providers: [
      provideHttpClient(withInterceptors([desktopInterceptor])), provideHttpClientTesting(),
    ] });
    http = TestBed.inject(HttpTestingController);
    client = TestBed.inject(HttpClient);
  });
  afterEach(() => {
    http.verify();
    delete window.__SCP_DESKTOP__;
    delete window.__TAURI__;
  });
  it('only attaches the launch token to its own sidecar', () => {
    for (const url of ['http://127.0.0.1:5199/api/session', 'http://localhost:5180/api/session',
      'http://127.0.0.1:51990/api/session']) {
      client.get(url).subscribe();
      const request = http.expectOne(url);
      expect(request.request.headers.get('X-ScoutCampPlanner-Device'))
        .toBe(url === 'http://127.0.0.1:5199/api/session' ? 'private-test-token' : null);
      request.flush({});
    }
  });
  it('does not retry failed package writes', () => {
    let failed = false;
    client.post('http://127.0.0.1:5199/api/packages/import-initial', {}).subscribe({ error: () => failed = true });
    http.expectOne('http://127.0.0.1:5199/api/packages/import-initial')
      .flush({}, { status: 503, statusText: 'Unavailable' });
    expect(failed).toBe(true);
  });
  it('does not enable native commands in the browser', async () => {
    delete window.__SCP_DESKTOP__;
    await expect(desktopCommand('open_camp_package')).rejects.toThrow('Desktopfunktion');
  });
});
