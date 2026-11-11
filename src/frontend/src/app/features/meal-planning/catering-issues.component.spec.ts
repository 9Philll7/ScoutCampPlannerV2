import { TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { vi } from 'vitest';
import { cateringSummary } from './catering-overview.component';
import { CateringIssuesComponent } from './catering-issues.component';
import { MealPlanningApiService, MealPlanningOverview, StructureConflictDocument, StructureResolutionPreview } from './meal-planning-api.service';

describe('CateringIssuesComponent', () => {
  function create(disabled = false) {
    const data: MealPlanningOverview = { campId: 'camp', meals: [
      { id: 'm', mealTypeId: 't', mealTypeName: 'Mittagessen', date: '2027-07-02', isActive: true, isInsideCampPeriod: true, changeVersion: 0 },
      { id: 'm2', mealTypeId: 't', mealTypeName: 'Mittagessen', date: '2027-07-03', isActive: true, isInsideCampPeriod: true, changeVersion: 0 }],
      structureNodes: [{ id: 'node', parentId: null, name: 'Wölflinge' }], cookingUnitGroups: [{ id: 'g', campId: 'camp', name: 'Gruppe 1', sortOrder: 0 }],
      cookingUnits: ['u','v'].map((id, sortOrder) => ({ id, campId: 'camp', name: id === 'u' ? 'Wölflingsküche' : 'Zentral', sortOrder, groupId: 'g', standardMealPlanId: null, defaultStructureNodeIds: ['node'] })),
      operationalMeals: [{ id: 's', cookingUnitId: 'u', campMealId: 'm', subscriptionState: 0, demandOverride: null, calculatedDemand: 10, effectiveDemand: 10,
        status: 0, mealPlanId: null, mealPlanVersion: null, calculatedAtUtc: '2027-07-01', structureOverrideNodeIds: [], offerTargets: [], recipeChoices: [], warnings: [],
        supply: { solutions: [], candidates: [], problems: [{ category: 0, code: 10, requirementGroupKey: null, referenceId: null, positionPath: null }] } }],
      mealPlans: [], mealPlanDocuments: [], recipeOptions: [], coverageWarnings: [] };
    const document: StructureConflictDocument = { version: 'version', actualParticipants: true, conflicts: [{ id: 'c', nodeId: 'node', nodeName: 'Wölflinge', kind: 'overlap', cookingUnitIds: ['u','v'], mealIds: ['m','m2'], persons: 10 }] };
    const preview: StructureResolutionPreview = { version: 'version', resolved: 2, remaining: 0, newConflicts: 0, changes: [{ cookingUnitId: 'v', mealId: null, nodeIds: [], noSupply: false }], conflicts: [] };
    const api = { structureConflicts: vi.fn(() => of(document)), previewStructureResolution: vi.fn(() => of(preview)), applyStructureResolution: vi.fn(() => of({})), overview: vi.fn(() => of(data)) };
    TestBed.configureTestingModule({ imports: [CateringIssuesComponent], providers: [{ provide: MealPlanningApiService, useValue: api }] });
    const fixture = TestBed.createComponent(CateringIssuesComponent); fixture.componentRef.setInput('data', data); fixture.componentRef.setInput('disabled', disabled); fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, api, document, data, preview };
  }
  it('groups structure cases and navigates nutrition conflicts directly to the meal', () => {
    const { fixture, component, api } = create(); const navigate = vi.fn(); component.navigate.subscribe(navigate);
    expect(component.rows()).toHaveLength(2);
    const row = Array.from(fixture.nativeElement.querySelectorAll('.issue-row') as NodeListOf<HTMLButtonElement>).find(value => value.textContent?.includes('Allergenkonflikt'))!;
    row.click(); expect(navigate).toHaveBeenCalledWith({ section: 'units', unitId: 'u', mealId: 'm' });
    expect(api.applyStructureResolution).not.toHaveBeenCalled();
    component.selectCategory('structure'); expect(component.rows()).toHaveLength(1);
    component.search = 'does not match'; expect(component.rows()).toEqual([]);
  });
  it.each([16,17])('includes supply coverage problem %s with a direct meal target', code => {
    const { fixture, component, data } = create();
    const current = structuredClone(data); current.operationalMeals[0].supply!.problems[0].code = code;
    fixture.componentRef.setInput('data', current); fixture.detectChanges();
    expect(component.nutritionIssues()).toHaveLength(1);
    expect(component.nutritionIssues()[0].title).toBe(code === 16 ? 'Überbelegung der Versorgung' : 'Personenzuordnung der Rezeptlösung offen');
    const action = cateringSummary(current).actions.find(value => value.id === 'conflict-s')!;
    expect(action.target).toEqual({ section: 'units', unitId: 'u', mealId: 'm' });
  });
  it('uses the same structure conflicts as the overview and ignores superseded warnings', () => {
    const { fixture, component, api, data, document } = create();
    const shared = { ...data, structureConflicts: document, coverageWarnings: ['Old unfiltered overlap'] };
    fixture.componentRef.setInput('data', shared); fixture.detectChanges();
    expect(component.document()).toEqual(document); expect(api.structureConflicts).toHaveBeenCalledTimes(1);
    const structural = cateringSummary(shared).actions.filter(value => value.id.startsWith('coverage-'));
    expect(structural).toHaveLength(1); expect(structural[0].target).toEqual({ section: 'issues' });
    const excluded = structuredClone(shared); excluded.structureConflicts.conflicts = []; excluded.operationalMeals[0].subscriptionState = 2;
    fixture.componentRef.setInput('data', excluded); fixture.detectChanges();
    expect(component.rows()).toEqual([]); expect(cateringSummary(excluded).actions.filter(value => value.kind === 'conflict')).toEqual([]);
  });
  it('reloads the complete shared overview when refreshing conflicts', () => {
    const { component, api, data, document } = create(); const changed = vi.fn(); component.changed.subscribe(changed);
    const current = { ...data, structureConflicts: { ...document, conflicts: [] } };
    api.overview.mockReturnValueOnce(of(current)); component.reload();
    expect(changed).toHaveBeenCalledWith({ data: current, message: '' });
    expect(component.document()?.conflicts).toEqual([]); expect(component.loading()).toBe(false);
  });
  it('opens missing supply in the assistant and offers cooking units directly', () => {
    const { fixture, component, api, document } = create(); const navigate = vi.fn(); component.navigate.subscribe(navigate);
    const missing = { ...document.conflicts[0], kind: 'missing' as const, cookingUnitIds: [] };
    component.document.set({ ...document, conflicts: [missing] }); fixture.detectChanges();
    const row = Array.from(fixture.nativeElement.querySelectorAll('.issue-row') as NodeListOf<HTMLButtonElement>).find(value => value.textContent?.includes('Fehlende Versorgung'))!;
    row.click(); fixture.detectChanges();
    expect(component.selected()?.kind).toBe('missing'); expect(navigate).not.toHaveBeenCalled();
    expect(component.useDefaults).toBe(false); expect(component.selectedUnitIds).toEqual([]);
    expect(api.previewStructureResolution).not.toHaveBeenCalled(); component.next(); expect(component.step()).toBe(1);
    expect(component.candidateUnitIds(missing)).toEqual(['u','v']);
    component.toggleUnit('v', true); component.next(); component.next(); component.apply();
    expect(api.applyStructureResolution).toHaveBeenCalledWith('camp', expect.objectContaining({ keepCookingUnitId: 'v', useDefaults: false, mealIds: ['m','m2'] }));
  });
  it('does not apply a proposal that resolves no affected meal', () => {
    const { component, api, document, preview } = create();
    api.previewStructureResolution.mockReturnValue(of({ ...preview, resolved: 0 }));
    component.open(document.conflicts[0]); component.step.set(2); component.apply();
    expect(component.canApply()).toBe(false); expect(api.applyStructureResolution).not.toHaveBeenCalled();
  });
  it('selects multiple cooking units and submits them together with their visible filters', () => {
    const { fixture, component, api, data, document } = create(); const updated = structuredClone(data);
    updated.cookingUnits[0].participantFilter = 1; updated.cookingUnits[1].participantFilter = 2;
    fixture.componentRef.setInput('data', updated); fixture.detectChanges();
    const missing = { ...document.conflicts[0], kind: 'missing' as const, cookingUnitIds: [] };
    component.open(missing); component.next(); fixture.detectChanges();
    component.toggleUnit('u', true); component.toggleUnit('v', true); fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('.unit-selection input[type="checkbox"]:checked')).toHaveLength(2);
    expect(fixture.nativeElement.textContent).toContain('Nur Sonderverpflegung'); expect(fixture.nativeElement.textContent).toContain('Nur Standardverpflegung');
    component.next(); component.next(); component.apply();
    expect(api.applyStructureResolution).toHaveBeenCalledWith('camp', expect.objectContaining({ cookingUnitIds: ['u','v'] }));
  });
  it('blocks overlapping filters even without currently duplicated participants', () => {
    const { fixture, component, api, document, preview } = create();
    api.previewStructureResolution.mockReturnValue(of({ ...preview, overlappingMeals: 0, overlappingFilters: true }));
    component.open(document.conflicts[0]); component.step.set(2); fixture.detectChanges(); component.apply();
    expect(component.canApply()).toBe(false);
    expect(api.applyStructureResolution).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Verpflegungsfilter dieser Kocheinheiten');
  });
  it('blocks overlapping assignments even when some conflicts would be resolved and no new ones appear', () => {
    const { component, api, document, preview } = create();
    api.previewStructureResolution.mockReturnValue(of({ ...preview, overlappingMeals: 1 }));
    component.open(document.conflicts[0]); component.step.set(2); component.apply();
    expect(component.canApply()).toBe(false); expect(api.applyStructureResolution).not.toHaveBeenCalled();
    component.step.set(1); component.toggleUnit('u', false); component.step.set(0);
    expect(component.preview()).toBeNull(); expect(component.canAdvance()).toBe(true);
  });
  it('protects changed assignment options and becomes clean after reverting them', () => {
    const { component, document } = create();
    component.open(document.conflicts[0]);
    expect(component.hasPendingEdits()).toBe(false);
    component.toggleUnit('v', true);
    expect(component.hasPendingEdits()).toBe(true);
    component.toggleUnit('v', false);
    expect(component.hasPendingEdits()).toBe(false);
    component.useDefaults = false;
    expect(component.hasPendingEdits()).toBe(true);
    component.close();
    expect(component.hasPendingEdits()).toBe(false);
  });
  it('opens the guided flow and writes only after the final confirmation', () => {
    const { component, api, document } = create(); component.open(document.conflicts[0]);
    expect(component.step()).toBe(0); expect(component.hasPendingEdits()).toBe(false); expect(api.previewStructureResolution).toHaveBeenCalled();
    component.apply(); expect(api.applyStructureResolution).not.toHaveBeenCalled();
    component.next(); component.next(); expect(component.step()).toBe(2); component.apply();
    expect(api.applyStructureResolution).toHaveBeenCalledWith('camp', expect.objectContaining({ expectedVersion: 'version', conflictId: 'c', keepCookingUnitId: 'u', useDefaults: true }));
    expect(component.selected()).toBeNull();
  });
  it('previews selected meals, blocks new conflicts and ignores old preview responses', () => {
    const { component, api, document, preview } = create(); component.open(document.conflicts[0]); const pending = new Subject<StructureResolutionPreview>();
    api.previewStructureResolution.mockReturnValueOnce(pending); component.selectedUnitIds = ['v']; component.updatePreview();
    component.useDefaults = false; component.toggleMeal('m2', false);
    expect(api.previewStructureResolution).toHaveBeenLastCalledWith('camp', expect.objectContaining({ useDefaults: false, mealIds: ['m'], keepCookingUnitId: 'v' }));
    pending.next({ ...preview, newConflicts: 99 }); expect(component.preview()?.newConflicts).toBe(0);
    api.previewStructureResolution.mockReturnValueOnce(of({ ...preview, newConflicts: 1 })); component.updatePreview();
    component.step.set(2); component.apply(); expect(api.applyStructureResolution).not.toHaveBeenCalled();
    component.toggleMeal('m', false); expect(component.preview()).toBeNull();
  });
  it('cancels without writing and respects edit permissions', () => {
    const { component, api, document } = create(true); component.open(document.conflicts[0]); expect(component.selected()).toBeNull(); expect(api.previewStructureResolution).not.toHaveBeenCalled();
  });
  it('does not repeat an already applied change if refreshing fails', () => {
    const { component, api, document } = create(); api.overview.mockReturnValueOnce(throwError(() => ({})));
    component.open(document.conflicts[0]); component.step.set(2); component.apply();
    expect(component.applied()).toBe(true); expect(component.error()).toContain('gespeichert'); component.apply();
    expect(api.applyStructureResolution).toHaveBeenCalledTimes(1); expect(component.hasPendingEdits()).toBe(false);
  });
});
