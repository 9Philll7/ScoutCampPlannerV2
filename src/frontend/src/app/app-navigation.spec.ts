import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, Subject, throwError } from 'rxjs';
import { vi } from 'vitest';
import { AppComponent } from './app.component';
import { SetupApiService } from './features/setup/setup-api.service';
import { AuthenticationApiService } from './features/authentication/authentication-api.service';
import { CampApiService, CampSummary } from './features/camp/camp-api.service';

describe('Application navigation', () => {
  const camp: CampSummary = { id: 'camp-a', tenantId: 'tenant-a', name: 'Sommerlager',
    startDate: '2027-07-01', endDate: '2027-07-10', isFrozen: false, canEdit: true, canExport: true };
  const tenants = [
    { id: 'tenant-a', name: 'Organisation A', canManageIngredients: true, canManageSettings: true, canCreateCamps: true },
    { id: 'tenant-b', name: 'Organisation B', canManageIngredients: false, canManageSettings: false, canCreateCamps: false },
  ];
  let api: Record<string, ReturnType<typeof vi.fn>>;
  beforeEach(() => {
    window.history.replaceState(null, '', '/'); sessionStorage.clear(); delete window.__SCP_DESKTOP__;
    api = {
      listTenants: vi.fn(() => of(tenants)),
      list: vi.fn((id: string) => of(id === 'tenant-a' ? [camp] : [])),
      getStageTemplate: vi.fn(() => of([])), getTenantStageFoodFactors: vi.fn(() => of([])),
      listAdministratorCandidates: vi.fn(() => of([])),
      getCampStageFoodFactors: vi.fn(() => of([])), getCampStages: vi.fn(() => of([])),
      getStructureConfiguration: vi.fn(() => of({ mode: 'Free', levelNames: [] })),
    };
    TestBed.configureTestingModule({ imports: [AppComponent], providers: [
      provideHttpClient(), provideHttpClientTesting(),
      { provide: SetupApiService, useValue: { status: () => of({ isRequired: false }) } },
      { provide: AuthenticationApiService, useValue: { current: () => of({ userId: 'user', email: 'test@example.test', canManageCentralIngredients: false }) } },
      { provide: CampApiService, useValue: api },
    ] });
    vi.spyOn(window, 'confirm').mockReturnValue(true);
  });
  afterEach(() => { vi.restoreAllMocks(); delete window.__SCP_DESKTOP__; });
  function setup() {
    const fixture = TestBed.createComponent(AppComponent); fixture.detectChanges();
    return { fixture, app: fixture.componentInstance, element: fixture.nativeElement as HTMLElement };
  }
  it('shows the active organisation, accessible camps and real recent history on Start', () => {
    const { app, fixture, element } = setup();
    expect(app.applicationSection()).toBe('home');
    expect(element.textContent).toContain('Organisation A');
    expect(element.textContent).toContain('Sommerlager');
    expect(app.recentCamps()).toEqual([]);
    app.openCamp(camp); app.closeCamp(); app.showSection('home'); fixture.detectChanges();
    expect(app.recentCamps()).toEqual([camp]);
  });
  it('marks exactly one active navigation item semantically and opens a named camp action', () => {
    const { app, fixture, element } = setup();
    expect(element.querySelector('[aria-current="page"]')?.textContent).toBe('Start');
    const open = element.querySelector<HTMLButtonElement>('button[aria-label="Sommerlager öffnen"]')!;
    expect(open).not.toBeNull(); open.click(); fixture.detectChanges();
    expect(app.openedCampId()).toBe(camp.id);
    expect(element.querySelectorAll('[aria-current="page"]').length).toBe(1);
    expect(element.querySelector('[aria-current="page"]')?.textContent).toBe('Übersicht');
    app.closeCamp(); fixture.detectChanges();
    expect(element.querySelector('[aria-current="page"]')?.textContent).toContain('Lager');
  });
  it('replaces global navigation with camp navigation and returns explicitly to camps', () => {
    const { app, fixture, element } = setup();
    app.openCamp(camp); fixture.detectChanges();
    expect(element.querySelector('[aria-label="Organisationsnavigation"]')).toBeNull();
    expect(element.querySelector('[aria-label="Lagernavigation"]')).not.toBeNull();
    expect(element.querySelector('.tenant-select')).toBeNull();
    expect(element.textContent).toContain('Organisation A');
    expect(app.campSection()).toBe('overview');
    app.closeCamp(); fixture.detectChanges();
    expect(app.applicationSection()).toBe('camps');
    expect(element.querySelector('[aria-label="Lagernavigation"]')).toBeNull();
  });
  it('isolates organisation switching and hides unavailable management links', () => {
    const { app, fixture, element } = setup();
    app.openCamp(camp); app.closeCamp(); app.selectTenant('tenant-b'); fixture.detectChanges();
    expect(app.openedCampId()).toBeNull(); expect(app.camps()).toEqual([]);
    expect(app.recentCamps()).toEqual([]);
    const navigation = element.querySelector('[aria-label="Organisationsnavigation"]')!;
    expect(navigation.textContent).not.toContain('Zutaten');
    expect(navigation.textContent).not.toContain('Organisation');
    app.showSection('organization'); expect(app.applicationSection()).toBe('home');
    app.showSection('centralIngredients'); expect(app.applicationSection()).toBe('home');
  });
  it('restores a camp deep link and handles back navigation without a camp switcher', () => {
    window.history.replaceState(null, '', '#/organization/tenant-a/camps/camp-a/overview');
    const { app } = setup();
    expect(app.openedCampId()).toBe('camp-a');
    window.history.replaceState(null, '', '#/organization/tenant-a/home'); app.restoreNavigation();
    expect(app.openedCampId()).toBeNull(); expect(app.applicationSection()).toBe('home');
  });
  it('rejects unavailable camp deep links without requesting foreign camp data', () => {
    window.history.replaceState(null, '', '#/organization/tenant-b/camps/camp-a/overview');
    const { app } = setup();
    expect(app.selectedTenant()?.id).toBe('tenant-b'); expect(app.openedCampId()).toBeNull();
    expect(api['getCampStageFoodFactors']).not.toHaveBeenCalled();
    expect(app.error()).toContain('nicht verfügbar');
  });
  it('allows cancelling navigation with pending edits and protects browser unload', () => {
    const { app } = setup(); app.openCamp(camp);
    app.trackEdit({ target: document.createElement('input') } as unknown as Event);
    vi.mocked(window.confirm).mockReturnValue(false);
    app.closeCamp(); expect(app.openedCampId()).toBe(camp.id);
    const event = new Event('beforeunload', { cancelable: true });
    app.beforeUnload(event as BeforeUnloadEvent); expect(event.defaultPrevented).toBe(true);
    window.history.replaceState(null, '', '#/organization/tenant-a/home'); app.restoreNavigation();
    expect(window.location.hash).toContain('/camps/camp-a/overview');
  });
  it('keeps desktop package access and hides cloud administration', () => {
    window.__SCP_DESKTOP__ = { apiUrl: 'http://localhost:5180', token: 'test' };
    const { app, fixture, element } = setup(); app.showSection('camps'); fixture.detectChanges();
    expect(element.querySelector('scp-package-transfer')).not.toBeNull();
    expect(element.querySelector('[aria-label="Organisationsnavigation"]')?.textContent).not.toContain('Rezepte');
    app.showSection('organization'); expect(app.applicationSection()).toBe('home');
  });
  it('renders each accessible camp once across recent and other camps', () => {
    const { app, fixture, element } = setup();
    const second = { ...camp, id: 'camp-b', name: 'Zweites Lager' };
    app.camps.set([camp, second]); app.openCamp(camp); app.closeCamp(); app.showSection('home'); fixture.detectChanges();
    expect(element.querySelectorAll('button[aria-label="Sommerlager öffnen"]')).toHaveLength(1);
    expect(element.querySelectorAll('button[aria-label="Zweites Lager öffnen"]')).toHaveLength(1);
    expect(app.otherCamps()).toEqual([second]);
    app.camps.set([]); fixture.detectChanges();
    expect(app.recentCamps()).toEqual([]); expect(app.otherCamps()).toEqual([]);
    expect(element.textContent).toContain('keine Lager verfügbar');
  });
  it('places camp navigation only in the sidebar and keeps real dashboard information', () => {
    const { app, fixture, element } = setup(); app.openCamp(camp); fixture.detectChanges();
    expect(element.querySelector('#workspace-sidebar [aria-label="Lagernavigation"]')).not.toBeNull();
    expect(element.querySelector('main [aria-label="Lagernavigation"]')).toBeNull();
    expect(element.querySelector('[aria-label="Organisationsnavigation"]')).toBeNull();
    expect(element.textContent).toContain('Alle Lager');
    expect(element.textContent).toContain('2027-07-01 bis 2027-07-10');
    expect(element.textContent).toContain('Lagerweite Kennzahlen und eine Aufgabenübersicht sind noch nicht verfügbar');
    expect(element.querySelectorAll('.dashboard-panel')).toHaveLength(0);
    expect(element.querySelectorAll('.dashboard-actions button')).toHaveLength(5);
    expect(element.querySelector('[role="progressbar"]')).toBeNull();
  });
  it('toggles navigation accessibly, Escape returns focus, cancelled navigation leaves it open', () => {
    const { app, fixture, element } = setup();
    const toggle = element.querySelector<HTMLButtonElement>('#navigation-toggle')!;
    expect(toggle.getAttribute('aria-expanded')).toBe('false');
    toggle.click(); fixture.detectChanges(); expect(toggle.getAttribute('aria-expanded')).toBe('true');
    app.closeSidebar(); fixture.detectChanges(); expect(toggle.getAttribute('aria-expanded')).toBe('false');
    expect(document.activeElement).toBe(toggle);
    app.openCamp(camp); app.sidebarOpen.set(true);
    app.trackEdit({ target: document.createElement('input') } as unknown as Event);
    vi.mocked(window.confirm).mockReturnValue(false); app.closeCamp();
    expect(app.sidebarOpen()).toBe(true); expect(app.openedCampId()).toBe(camp.id);
    vi.mocked(window.confirm).mockReturnValue(true); app.closeCamp();
    expect(app.sidebarOpen()).toBe(false); expect(app.openedCampId()).toBeNull();
  });
  it('hides the entire additional section after the only camp was opened and keeps a named filled action', () => {
    const { app, fixture, element } = setup();
    app.openCamp(camp); app.closeCamp(); app.showSection('home'); fixture.detectChanges();
    expect(element.querySelector('#accessible-camps-title')).toBeNull();
    expect(element.textContent).not.toContain('Alle zugänglichen Lager stehen bereits');
    const action = element.querySelector<HTMLButtonElement>('.primary-action')!;
    expect(action.textContent).toContain('Zur Lagerübersicht');
    expect(action.classList.contains('mat-mdc-unelevated-button')).toBe(true);
    for (const button of element.querySelectorAll('.start-page button')) {
      expect(button.textContent?.trim()).not.toBe('');
    }
    action.click(); fixture.detectChanges(); expect(app.applicationSection()).toBe('camps');
  });
  it('renders one honest empty state without a misleading recent-camp instruction', () => {
    const { app, fixture, element } = setup(); app.camps.set([]); fixture.detectChanges();
    expect(element.querySelector('#recent-camps-title')).toBeNull();
    expect(element.querySelectorAll('.start-empty')).toHaveLength(1);
    expect(element.textContent).not.toContain('Wähle unten ein Lager');
  });
  it('keeps a current factor failure visible including its HTTP status', () => {
    const { app } = setup();
    api['getCampStageFoodFactors'].mockReturnValue(throwError(() => ({ status: 403 })));
    app.openCamp(camp);
    expect(app.error()).toContain('HTTP 403');
    expect(app.campStagesLoading()).toBe(false);
  });
  it('does not apply a late factor failure to another camp', () => {
    const { app } = setup();
    const pending = new Subject<never>();
    api['getCampStageFoodFactors'].mockReturnValueOnce(pending);
    app.openCamp(camp); app.closeCamp();
    app.openCamp({ ...camp, id: 'camp-b' });
    pending.error({ status: 500 });
    expect(app.error()).toBeNull();
  });
  it('moves focus into the menu and closes it when focus reaches the content', async () => {
    const { app, fixture, element } = setup();
    app.toggleSidebar(); fixture.detectChanges();
    await new Promise(resolve => setTimeout(resolve, 0));
    expect(document.activeElement).toBe(element.querySelector('[aria-current="page"]'));
    element.querySelector<HTMLElement>('main')!.focus(); fixture.detectChanges();
    expect(app.sidebarOpen()).toBe(false);
  });
});
