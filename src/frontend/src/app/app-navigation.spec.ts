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
      listStructure: vi.fn(() => of([])), getPlanningSummary: vi.fn(() => of({ stageTotals: [], structureTotals: [] })),
      getParticipantEstimates: vi.fn(() => of([])), updateParticipantEstimates: vi.fn(() => of(undefined)), renameStructureNode: vi.fn(() => of(undefined)),
      getWeightedFoodSummary: vi.fn(() => of([])),
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
  function structureSetup() {
    const result = setup(); const { app } = result;
    app.openCamp(camp); app.campSection.set('structure'); app.structureCampId.set(camp.id);
    app.structureMode.set('Free'); app.planningSummary.set({ stageTotals: [], structureTotals: [] });
    app.structureNodes.set([
      { id: 'parent', campId: camp.id, parentId: null, name: 'GuSp ist ein Knotenname' },
      { id: 'leaf', campId: camp.id, parentId: 'parent', name: 'Gemischte Gruppe' },
      { id: 'shallow', campId: camp.id, parentId: null, name: 'Kurzer Zweig' }
    ]);
    return result;
  }
  it('keeps the tree visible and only offers estimates on eligible leaves', () => {
    const { app, fixture, element } = structureSetup();
    app.selectStructureNode('parent'); fixture.detectChanges();
    expect(element.querySelector('[aria-label="Strukturknoten"]')).not.toBeNull();
    expect(element.textContent).not.toContain('Schätzung bearbeiten');
    const details = element.querySelector('.structure-tree') as HTMLElement;
    details.scrollTop = 240;
    app.selectStructureNode('leaf'); fixture.detectChanges();
    expect(details.scrollTop).toBe(0);
    expect(element.textContent).toContain('Schätzung bearbeiten');
    expect(element.querySelector('[aria-label="Eintrag umbenennen"]')).toBeNull();
    app.structureMode.set('Fixed'); app.structureLevelNames.set(['Bereich', 'Gruppe']);
    app.selectStructureNode('shallow'); fixture.detectChanges();
    expect(element.textContent).not.toContain('Schätzung bearbeiten');
    expect(element.textContent).not.toContain('Teilnehmerverwaltung öffnen');
  });
  it('shows direct node actions while respecting read-only permissions', () => {
    const { app, fixture, element } = structureSetup(); app.selectStructureNode('leaf');
    fixture.detectChanges();
    expect(element.querySelector('[aria-label="Aktionen für Gemischte Gruppe"]')).not.toBeNull();
    expect(element.querySelector('[aria-label="Name bearbeiten: Gemischte Gruppe"]')).not.toBeNull();
    expect(element.textContent).toContain('Schätzung bearbeiten');
    app.camps.set([{ ...camp, canEdit: false }]); fixture.detectChanges();
    expect(element.querySelector('[aria-label="Aktionen für Gemischte Gruppe"]')).toBeNull();
    expect(element.querySelector('[aria-label="Name bearbeiten: Gemischte Gruppe"]')).toBeNull();
    expect(element.textContent).not.toContain('Bearbeitung beenden');
  });
  it('does not double count parent estimates and selects collapsed descendants independently', () => {
    const { app } = structureSetup();
    app.planningSummary.set({ stageTotals: [{ campStageId: 'stage', stageName: 'Biber', childYouthCount: 3, leaderCount: 2 }], structureTotals: [
      { structureNodeId: 'parent', childYouthCount: 3, leaderCount: 2 }, { structureNodeId: 'leaf', childYouthCount: 3, leaderCount: 2 }
    ] });
    expect(app.campChildYouthTotal()).toBe(3);
    app.selectStructureNode('leaf'); app.toggleStructureNodeCollapsed('parent');
    expect(app.selectedStructureRows()[0].depth).toBe(1);
    expect(app.structureRows().some(row => row.node.id === 'leaf')).toBe(false);
  });
  it('discards estimate drafts only after confirmation and rejects invalid counts', () => {
    const { app } = structureSetup(); app.selectStructureNode('leaf');
    app.estimateNodeId.set('leaf');
    app.participantEstimates.set([{ campStageId: 'a', stageName: 'Biber', childYouthCount: 3, leaderCount: 2 },
      { campStageId: 'b', stageName: 'GuSp', childYouthCount: 4, leaderCount: 1 }]);
    expect(app.estimateTotal()).toBe(10);
    vi.mocked(window.confirm).mockReturnValue(false); app.closeEstimates(); expect(app.estimateNodeId()).toBe('leaf');
    app.participantEstimates()[0].childYouthCount = -1; app.saveEstimates(camp);
    expect(api['updateParticipantEstimates']).not.toHaveBeenCalled();
    expect(app.error()).toContain('nicht negative');
    vi.mocked(window.confirm).mockReturnValue(true); app.closeEstimates(); expect(app.estimateNodeId()).toBeNull();
  });
  it('persists the complete mixed-stage matrix and closes only after success', () => {
    const { app } = structureSetup(); app.selectStructureNode('leaf'); app.estimateNodeId.set('leaf');
    const rows = [{ campStageId: 'a', stageName: 'Biber', childYouthCount: 3, leaderCount: 2 },
      { campStageId: 'b', stageName: 'GuSp', childYouthCount: 4, leaderCount: 1 }];
    app.participantEstimates.set(rows);
    api['updateParticipantEstimates'].mockReturnValueOnce(throwError(() => ({ status: 409, error: { code: 'structure_node_not_participant_level' } })));
    app.saveEstimates(camp); expect(app.error()).toContain('letzten definierten Ebene');
    expect(app.estimateNodeId()).toBe('leaf'); expect(app.participantEstimates()).toEqual(rows);
    app.saveEstimates(camp);
    expect(api['updateParticipantEstimates']).toHaveBeenLastCalledWith(camp.id, 'leaf', rows);
    expect(app.estimateNodeId()).toBeNull(); expect(api['getPlanningSummary']).toHaveBeenCalledWith(camp.id);
  });
  it('aligns leaf disclosure slots and displays only labelled estimate metrics', () => {
    const { app, fixture, element } = structureSetup();
    app.planningSummary.set({ stageTotals: [], structureTotals: [{ structureNodeId: 'leaf', childYouthCount: 7, leaderCount: 2 }] });
    app.selectStructureNode('leaf'); fixture.detectChanges();
    expect(element.querySelectorAll('.structure-disclosure-space')).toHaveLength(2);
    expect(element.querySelector('.structure-select.selected')?.textContent).toContain('7 / 2');
    expect(element.querySelector('.structure-detail-title')?.textContent).toBe('Gemischte Gruppe');
    expect(element.querySelector('.structure-path')?.textContent).toContain('GuSp ist ein Knotenname › Gemischte Gruppe');
    expect(Array.from(element.querySelectorAll('.structure-metrics dd')).map(value => value.textContent)).toEqual(['7', '2']);
    expect(element.querySelector('[aria-label="Teilnehmerschätzung"]')?.textContent).toContain('Geschätzte Teilnehmer');
    expect(element.querySelector('.structure-metrics')?.textContent).toContain('KiJu');
  });
  it('groups estimate dialog totals and actions without changing save semantics', () => {
    const { app, fixture, element } = structureSetup(); app.selectStructureNode('leaf');
    app.estimateNodeId.set('leaf'); app.participantEstimates.set([{ campStageId: 'a', stageName: 'Biber', childYouthCount: 3, leaderCount: 2 }]);
    fixture.detectChanges();
    expect(element.querySelector('dialog tfoot')?.textContent).toContain('Gesamt5');
    expect(element.querySelector('dialog .estimate-actions [aria-label="Schätzung speichern"]')).not.toBeNull();
    expect(element.querySelector('input[aria-label="Biber Leiter/Erwachsene"]')).not.toBeNull();
  });
  it('uses one structure page heading and keeps editing in the tree panel', () => {
    const { app, fixture, element } = structureSetup(); fixture.detectChanges();
    expect(element.querySelector('main')?.classList.contains('structure-page')).toBe(true);
    expect(Array.from(element.querySelectorAll('h1')).map(heading => heading.textContent)).toEqual(['Struktur']);
    expect(element.querySelector('.page-header')).toBeNull();
    expect(element.querySelector('[aria-label="Aktionen für das Lager"]')).not.toBeNull();
    expect(element.querySelector('.camp-navigation-subitems [aria-current="page"]')?.textContent).toBe('Struktur');
    app.navigateCamp('general'); fixture.detectChanges();
    expect(element.querySelector('main')?.classList.contains('structure-page')).toBe(false);
    expect(element.querySelector('h1')?.textContent).toBe(camp.name);
  });
  it('renders parent details as a header and real child table, and leaf details separately', async () => {
    const { app, fixture, element } = structureSetup(); app.selectStructureNode('parent'); fixture.detectChanges();
    expect(Array.from(element.querySelectorAll('[role="tab"]')).map(tab => tab.textContent?.trim())).toEqual(['Untergruppen', 'Stufenverteilung']);
    expect(element.querySelector('scp-structure-detail-header .detail-symbol')).not.toBeNull();
    expect(element.querySelector('.structure-children-table tbody th')?.textContent).toContain('Gemischte Gruppe');
    expect(element.querySelector('.structure-leaf-content')).toBeNull();
    expect(element.querySelector('.child-count')?.textContent).toBe('1');
    app.selectStructureNode('leaf'); fixture.detectChanges();
    expect(element.querySelector('.structure-children-table')).toBeNull();
    expect(element.querySelector('.structure-leaf-content')).not.toBeNull();
    expect(element.querySelector('[role="tablist"]')).toBeNull();
    (element.querySelector('[aria-label="Name bearbeiten: Gemischte Gruppe"]') as HTMLButtonElement).click(); fixture.detectChanges();
    await fixture.whenStable(); fixture.detectChanges();
    expect(element.querySelector('scp-structure-detail-header .structure-title-editor')).not.toBeNull();
    expect(element.querySelector('.structure-editor-section')).toBeNull();
    expect(element.querySelector('.mode-label')).toBeNull();
    expect(element.querySelector('.detail-statistics')).not.toBeNull();
    expect((element.querySelector('.structure-node-name-field input') as HTMLInputElement)?.value).toBe('Gemischte Gruppe');
    app.toggleRenameStructureNode(app.structureNodes()[1]); fixture.detectChanges();
    expect(element.querySelector('.detail-statistics')).not.toBeNull();
    expect(element.querySelector('.structure-column-caption')?.textContent).toContain('Geschätzt');
  });
  it('paginates subgroups without changing full hierarchy totals and resets on node change', () => {
    const { app, fixture, element } = structureSetup();
    const parent = app.structureNodes()[0];
    app.structureNodes.set([parent, ...Array.from({ length: 12 }, (_, index) => ({
      ...parent, id: `child-${index}`, parentId: parent.id, name: `Gruppe ${index}`
    }))]);
    app.selectStructureNode(parent.id); fixture.detectChanges();
    expect(element.querySelectorAll('.structure-children-table tbody tr').length).toBe(5);
    expect(app.selectedStructureChildren().length).toBe(12);
    app.changeChildrenPage({ pageIndex: 2, pageSize: 5 }); fixture.detectChanges();
    expect(app.pagedStructureChildren().map(row => row.id)).toEqual(['child-10', 'child-11']);
    app.selectStructureNode(null); fixture.detectChanges();
    expect(app.childrenPageIndex()).toBe(0);
  });
  it('switches tree counts to actual subtree totals without mixing estimates or unassigned people', () => {
    const { app, fixture } = structureSetup();
    app.treeCountMode.set('actual'); fixture.detectChanges();
    expect(app.treeParticipantLabel('parent')).toBe('—');
    app.treeActualCounts.set({ stages: [], counts: [
      { structureNodeId: 'leaf', campStageId: null, category: 'ChildYouth', count: 3 },
      { structureNodeId: 'parent', campStageId: null, category: 'LeaderAdult', count: 2 },
      { structureNodeId: 'leaf', campStageId: null, category: null, count: 1 },
      { structureNodeId: null, campStageId: null, category: 'ChildYouth', count: 4 }
    ] });
    expect(app.treeParticipantLabel('parent')).toBe('3 / 2 · 1 offen');
    expect(app.treeParticipantLabel('leaf')).toBe('3 / 0 · 1 offen');
    expect(app.treeParticipantLabel(null)).toBe('7 / 2 · 1 offen');
    app.treeCountMode.set('estimated');
    expect(app.treeParticipantLabel('leaf')).not.toContain('offen');
  });
  it('keeps the local view switch in the shared page header during editing and participant navigation', () => {
    const { app, fixture, element } = structureSetup();
    app.selectStructureNode('leaf'); app.toggleRenameStructureNode(app.structureNodes()[1]); fixture.detectChanges();
    expect(element.querySelectorAll('.workspace-sidebar [aria-label="Struktur und Teilnehmer"]')).toHaveLength(1);
    expect(element.querySelector('.page-header')).toBeNull();
    expect(element.querySelector('.structure-edit-notice')).toBeNull();
    app.navigateCamp('participants'); fixture.detectChanges();
    expect(element.querySelector('h1')?.textContent).toBe('Teilnehmerübersicht');
    expect(element.querySelector('.camp-navigation-subitems [aria-current="page"]')?.textContent).toBe('Teilnehmerübersicht');
    expect(element.querySelectorAll('.workspace-sidebar [aria-label="Struktur und Teilnehmer"]')).toHaveLength(1);
  });
  it('keeps renaming in the title instead of the node menu without persisting a change', async () => {
    const { app, fixture, element } = structureSetup(); app.selectStructureNode('leaf'); fixture.detectChanges();
    (element.querySelector('[aria-label="Aktionen für Gemischte Gruppe"]') as HTMLButtonElement).click(); fixture.detectChanges(); await fixture.whenStable();
    const rename = Array.from(document.querySelectorAll<HTMLButtonElement>('[role="menuitem"]')).find(button => button.textContent?.trim() === 'Umbenennen');
    expect(rename).toBeUndefined();
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true })); fixture.detectChanges();
    (element.querySelector('[aria-label="Name bearbeiten: Gemischte Gruppe"]') as HTMLButtonElement).click(); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    expect(app.editingStructureNodeId()).toBe('leaf');
    const input = element.querySelector('.structure-node-name-field input') as HTMLInputElement;
    expect(input.value).toBe('Gemischte Gruppe');
    expect(element.querySelector('[aria-label="Namen speichern"]')).not.toBeNull();
    expect(api['renameStructureNode']).not.toHaveBeenCalled();
  });
  it('does not warn for an unchanged or cancelled structure name but protects a changed draft', () => {
    const { app, fixture } = structureSetup(); app.selectStructureNode('leaf'); fixture.detectChanges();
    vi.mocked(window.confirm).mockClear();
    app.toggleRenameStructureNode(app.structureNodes()[1]); fixture.detectChanges();
    app.toggleRenameStructureNode(app.structureNodes()[1]); fixture.detectChanges();
    expect(window.confirm).not.toHaveBeenCalled();
    app.toggleRenameStructureNode(app.structureNodes()[1]); app.editStructureNodeName = 'Changed'; fixture.detectChanges();
    vi.mocked(window.confirm).mockReturnValue(false); app.toggleMoveStructureNode(app.structureNodes()[1]);
    expect(app.editingStructureNodeId()).toBe('leaf'); expect(window.confirm).toHaveBeenCalled();
  });
});
