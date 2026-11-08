import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { By } from '@angular/platform-browser';
import { of, Subject, throwError } from 'rxjs';
import { vi } from 'vitest';
import { CookingUnitsWorkspaceComponent } from './cooking-units-workspace.component';
import { CookingUnit, MealPlanningApiService, MealPlanningOverview, MutationResult } from './meal-planning-api.service';
import { ParticipantPlanningComponent } from './participant-planning.component';
import { ParticipantsApiService } from '../camp/participants-api.service';

describe('CookingUnitsWorkspaceComponent', () => {
  let fixture: ComponentFixture<CookingUnitsWorkspaceComponent>;
  let component: CookingUnitsWorkspaceComponent;
  let data: MealPlanningOverview;
  let api: Record<string, ReturnType<typeof vi.fn>>;
  beforeEach(() => {
    const unit: CookingUnit = { id: 'a', campId: 'camp', name: 'Gruppe A', sortOrder: 0, groupId: 'g', standardMealPlanId: 'p',
      defaultStructureNodeIds: ['north'], participantFilter: 0, togetherPreference: 1 };
    data = { campId: 'camp', meals: [{ id: 'lunch', mealTypeId: 'type', mealTypeName: 'Mittagessen', date: '2027-07-01', isActive: true, isInsideCampPeriod: true, changeVersion: 0 }],
      cookingUnits: [unit, { ...unit, id: 'b', name: 'Gruppe B', sortOrder: 1, defaultStructureNodeIds: ['south'] }],
      cookingUnitGroups: [{ id: 'g', campId: 'camp', name: 'Unterlager', sortOrder: 0 }],
      mealPlans: [{ id: 'p', name: 'Standardplan', sortOrder: 0, version: 1, missingActiveMealCount: 1 }], mealPlanDocuments: [],
      structureNodes: [{ id: 'root', parentId: null, name: 'Lager' }, { id: 'north', parentId: 'root', name: 'Nord' },
        { id: 'fox', parentId: 'north', name: 'Fuchs' }, { id: 'owl', parentId: 'north', name: 'Eule' }, { id: 'south', parentId: 'root', name: 'Süd' }],
      operationalMeals: [], recipeOptions: [], coverageWarnings: [] };
    api = { saveUnit: vi.fn(() => of({ id: 'a', version: null })), createUnit: vi.fn(() => of({ id: 'created', version: null })),
      deleteUnit: vi.fn(() => of({ id: 'a', version: null })), overview: vi.fn(() => of(structuredClone(data))),
      createGroup: vi.fn(() => of({ id: 'new-group', version: null })), deleteGroup: vi.fn(() => of({ id: 'g', version: null })) };
    TestBed.configureTestingModule({ imports: [CookingUnitsWorkspaceComponent], providers: [provideHttpClient(), provideHttpClientTesting(),
      { provide: MealPlanningApiService, useValue: api },
      { provide: ParticipantsApiService, useValue: { counts: () => of({ counts: [], stages: [] }), list: () => of({ participants: [] }) } }] });
    fixture = TestBed.createComponent(CookingUnitsWorkspaceComponent);
    fixture.componentRef.setInput('data', data); fixture.detectChanges(); component = fixture.componentInstance;
    component.changed.subscribe(event => fixture.componentRef.setInput('data', event.data));
    vi.spyOn(window, 'confirm').mockReturnValue(true);
  });
  afterEach(() => vi.restoreAllMocks());
  it('combines group filtering, ungrouped units, search and sorting without changing the draft', () => {
    fixture.componentRef.setInput('data', { ...data, cookingUnitGroups: [...data.cookingUnitGroups, { id: 'other', campId: 'camp', name: 'Andere', sortOrder: 1 }],
      cookingUnits: [...data.cookingUnits, { ...data.cookingUnits[0], id: 'c', name: 'Gruppe C', groupId: null },
        { ...data.cookingUnits[0], id: 'd', name: 'Andere Küche', groupId: 'other' }] }); fixture.detectChanges();
    component.draft()!.name = 'Unsaved'; const draft = JSON.stringify(component.draft());
    component.groupFilter = 'g'; expect(component.listedUnits().map(unit => unit.id)).toEqual(['a', 'b']);
    component.search = 'Unterlager'; expect(component.listedUnits().map(unit => unit.id)).toEqual(['a', 'b']);
    component.search = 'Gruppe B'; expect(component.listedUnits().map(unit => unit.id)).toEqual(['b']);
    component.search = ''; component.groupFilter = null; expect(component.listedUnits().map(unit => unit.id)).toEqual(['c']);
    component.groupFilter = 'other'; expect(component.listedUnits().map(unit => unit.id)).toEqual(['d']);
    component.groupFilter = ''; component.sort = 'name'; expect(component.listedUnits()).toHaveLength(4);
    expect(JSON.stringify(component.draft())).toBe(draft); expect(window.confirm).not.toHaveBeenCalled();
    expect(api['saveUnit']).not.toHaveBeenCalled();
  });
  it('resets a deleted group filter on refresh without discarding unit edits', () => {
    component.groupFilter = 'g'; component.draft()!.name = 'Unsaved';
    fixture.componentRef.setInput('data', { ...data, cookingUnitGroups: [], cookingUnits: data.cookingUnits.map(unit => ({ ...unit, groupId: null })) }); fixture.detectChanges();
    expect(component.groupFilter).toBe(''); expect(component.draft()!.name).toBe('Unsaved');
    expect(component.listedUnits()).toHaveLength(2);
  });
  it('shows only the organisational editor with one actual filter selection and no invented counts', () => {
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelectorAll('.unit-row')).toHaveLength(2);
    expect(element.querySelectorAll('[role=tab]')).toHaveLength(3);
    expect(element.querySelector('[name=cookingUnitFilter]')).toBeNull();
    expect(element.querySelector('.slot-card')).toBeNull();
    expect(element.querySelector('.unit-summary')!.textContent).not.toContain('Teilnehmerzahl');
    expect(component.hasPendingChanges()).toBe(false);
    component.tab.set('structure'); fixture.detectChanges();
    expect(element.querySelectorAll('[name=cookingUnitFilter] option')).toHaveLength(3);
    expect(element.querySelector('.selected-nodes')).toBeNull();
    expect(element.querySelector('scp-cooking-unit-participant-summary')).not.toBeNull();
    component.tab.set('catering'); fixture.detectChanges();
    expect(element.querySelector('scp-cooking-unit-requirements')).not.toBeNull();
    expect(component.hasPendingChanges()).toBe(false);
  });
  it('edits detached data and protects switching or creating a new unit', () => {
    component.draft()!.name = 'Unsaved';
    expect(data.cookingUnits[0].name).toBe('Gruppe A');
    vi.mocked(window.confirm).mockReturnValue(false);
    component.select(data.cookingUnits[1]); component.create();
    expect(component.draft()!.name).toBe('Unsaved'); expect(component.isNew()).toBe(false);
    vi.mocked(window.confirm).mockReturnValue(true); component.select(data.cookingUnits[1]);
    expect(component.draft()!.id).toBe('b'); expect(component.hasPendingChanges()).toBe(false);
  });
  it('renders recursively indeterminate parents and saves only canonical subtree roots', () => {
    component.tab.set('structure'); fixture.detectChanges();
    (fixture.nativeElement.querySelector('input[aria-label=Eule]') as HTMLInputElement).click(); fixture.detectChanges();
    const root = fixture.nativeElement.querySelector('input[aria-label=Lager]') as HTMLInputElement;
    const north = fixture.nativeElement.querySelector('input[aria-label=Nord]') as HTMLInputElement;
    expect(component.draft()!.defaultStructureNodeIds).toEqual(['fox']);
    expect(component.nodeState('north')).toBe('partial');
    expect(root.indeterminate).toBe(true); expect(north.indeterminate).toBe(true); expect(north.checked).toBe(false);
    component.toggleNode('north', true);
    expect(component.draft()!.defaultStructureNodeIds).toEqual(['north']);
    expect(component.nodeState('owl')).toBe('full');
  });
  it('sends one complete unit edit through the existing API and resets its dirty state on success', () => {
    component.draft()!.name = 'Updated'; component.draft()!.participantFilter = 2; component.draft()!.togetherPreference = 0;
    const saved = structuredClone(component.draft()!);
    api['overview'].mockReturnValue(of({ ...data, cookingUnits: [saved, data.cookingUnits[1]] }));
    component.save(); fixture.detectChanges();
    expect(api['saveUnit']).toHaveBeenCalledWith('camp', expect.objectContaining({ id: 'a', name: 'Updated', participantFilter: 2, togetherPreference: 0, defaultStructureNodeIds: ['north'] }));
    expect(component.hasPendingChanges()).toBe(false); expect(component.draft()!.name).toBe('Updated');
  });
  it('retains rejected edits and server references for overlapping configurations', () => {
    component.draft()!.name = 'Updated';
    api['saveUnit'].mockReturnValue(throwError(() => ({ error: { message: 'Überschneidung', references: ['Gruppe B'] } })));
    component.save();
    expect(component.error()).toContain('Überschneidung'); expect(component.error()).toContain('Gruppe B');
    expect(component.draft()!.name).toBe('Updated'); expect(component.hasPendingChanges()).toBe(true); expect(component.busy()).toBe(false);
  });
  it('preserves a dirty draft when overview data refreshes', () => {
    component.draft()!.name = 'Local';
    fixture.componentRef.setInput('data', { ...data, cookingUnits: data.cookingUnits.map(unit => ({ ...unit, name: 'Server' })) }); fixture.detectChanges();
    expect(component.draft()!.name).toBe('Local');
    component.discardChanges(); expect(component.draft()!.name).toBe('Server');
  });
  it('creates a unit with the saved server ID and supports explicit discard', () => {
    component.create(); expect(component.isNew()).toBe(true); component.draft()!.name = 'New unit';
    const saved = { ...structuredClone(component.draft()!), id: 'created' };
    api['overview'].mockReturnValue(of({ ...data, cookingUnits: [...data.cookingUnits, saved] }));
    component.save(); fixture.detectChanges();
    expect(api['createUnit']).toHaveBeenCalledWith('camp', expect.objectContaining({ name: 'New unit' }));
    expect(component.draft()!.id).toBe('created'); expect(component.isNew()).toBe(false); expect(component.hasPendingChanges()).toBe(false);
    component.draft()!.name = 'Unsaved'; component.discard();
    expect(component.draft()!.name).toBe('New unit');
  });
  it('blocks changes and API mutations in read-only and busy states', () => {
    fixture.componentRef.setInput('disabled', true); fixture.detectChanges();
    const before = JSON.stringify(component.draft()); component.toggleNode('root', true); component.create(); component.save(); component.remove();
    expect(JSON.stringify(component.draft())).toBe(before); expect(api['saveUnit']).not.toHaveBeenCalled(); expect(api['deleteUnit']).not.toHaveBeenCalled();
    fixture.componentRef.setInput('disabled', false); fixture.detectChanges();
    component.draft()!.name = 'Saving'; const request = new Subject<MutationResult>(); api['saveUnit'].mockReturnValue(request);
    component.save(); component.select(data.cookingUnits[1]);
    expect(component.busy()).toBe(true); expect(component.draft()!.id).toBe('a'); request.error({}); expect(component.busy()).toBe(false);
  });
  it('does not silently drop missing structure references or reinterpret unknown filters', () => {
    component.draft()!.defaultStructureNodeIds = ['north', 'missing']; component.toggleNode('owl', false);
    expect(component.draft()!.defaultStructureNodeIds).toContain('missing'); expect(component.valid()).toBe(false);
    component.removeNode('missing'); expect(component.valid()).toBe(true);
    component.draft()!.participantFilter = 99; component.save(); expect(api['saveUnit']).not.toHaveBeenCalled();
  });
  it('protects navigation to existing operative planning and respects the selected unit', () => {
    const navigate = vi.fn(); component.navigate.subscribe(navigate);
    component.draft()!.name = 'Local'; vi.mocked(window.confirm).mockReturnValue(false); component.openSupply();
    expect(navigate).not.toHaveBeenCalled();
    vi.mocked(window.confirm).mockReturnValue(true); component.openSupply();
    expect(navigate).toHaveBeenCalledWith({ section: 'units', unitId: 'a', mealId: 'lunch' }); expect(component.hasPendingChanges()).toBe(false);
  });
  it('protects an unfinished group name from reload and reports it to the navigation guard', () => {
    component.groupName = 'New group'; expect(component.hasPendingEdits()).toBe(true);
    vi.mocked(window.confirm).mockReturnValue(false); component.refresh(); expect(api['overview']).not.toHaveBeenCalled();
    component.discardChanges(); expect(component.hasPendingEdits()).toBe(false);
  });
  it('does not jump back to an old target after saving another selected unit', () => {
    fixture.componentRef.setInput('targetUnitId', 'a'); fixture.detectChanges();
    component.select(data.cookingUnits[1]); component.draft()!.name = 'Updated B';
    const updated = { ...data, cookingUnits: [data.cookingUnits[0], structuredClone(component.draft()!)] };
    api['saveUnit'].mockReturnValue(of({ id: 'b', version: null })); api['overview'].mockReturnValue(of(updated));
    component.save(); fixture.detectChanges();
    expect(component.draft()!.id).toBe('b'); expect(component.draft()!.name).toBe('Updated B');
  });
  it('does not create a duplicate when the post-save refresh fails', () => {
    component.create(); component.draft()!.name = 'Created';
    api['overview'].mockReturnValue(throwError(() => ({ status: 500 })));
    component.save();
    expect(component.isNew()).toBe(false); expect(component.draft()!.id).toBe('created');
    expect(component.error()).toContain('wurde gespeichert');
    component.save();
    expect(api['createUnit']).toHaveBeenCalledOnce();
    expect(api['saveUnit']).toHaveBeenCalledWith('camp', expect.objectContaining({ id: 'created' }));
  });
  it('protects an unsaved demand basis when closing management and blocks conflicting group changes', () => {
    component.settingsOpen.set(true); fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(request => request.url.endsWith('/meal-planning/participants')).flush({ version: 2, demandMode: 0, requiresStructureMigration: false });
    const basis = fixture.debugElement.query(By.directive(ParticipantPlanningComponent)).componentInstance as ParticipantPlanningComponent;
    basis.configuration()!.demandMode = 1;
    expect(component.hasPendingEdits()).toBe(true);
    const close = vi.fn(), dialog = { close } as unknown as HTMLDialogElement;
    vi.mocked(window.confirm).mockReturnValue(false); component.closeSettings(dialog);
    expect(close).not.toHaveBeenCalled(); expect(basis.configuration()!.demandMode).toBe(1);
    component.groupName = 'New group'; component.createGroup();
    expect(api['createGroup']).not.toHaveBeenCalled();
    vi.mocked(window.confirm).mockReturnValue(true); component.closeSettings(dialog);
    expect(close).toHaveBeenCalledOnce(); expect(component.settingsOpen()).toBe(false);
    expect(basis.configuration()!.demandMode).toBe(0); expect(component.groupName).toBe(''); http.verify();
  });
});
