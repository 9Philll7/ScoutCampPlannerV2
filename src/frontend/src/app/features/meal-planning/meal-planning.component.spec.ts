import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import {
  MealPlanDocument,
  MealPlanningApiService,
  MealPlanningOverview,
} from './meal-planning-api.service';
import { MealPlanningComponent } from './meal-planning.component';
import { SupplySolutionsComponent } from './supply-solutions.component';
import { By } from '@angular/platform-browser';

describe('MealPlanningComponent', () => {
  const plan: MealPlanDocument = {
    id: 'plan-1', campId: 'camp-1', name: 'Standard', sortOrder: 0, version: 3,
    offerGroups: [{
      id: 'group-1', campMealId: 'meal-1', name: 'Hauptangebot', sortOrder: 0,
      entries: [{
        id: 'entry-1', recipeRevisionId: 'revision-1', isStandard: true,
        displayName: null, role: 0, note: null, sortOrder: 0,
      }],
    }],
  };
  const overview: MealPlanningOverview = {
    campId: 'camp-1',
    meals: [{ id: 'meal-1', mealTypeId: 'type-1', mealTypeName: 'Mittagessen', date: '2027-07-02', isActive: true, isInsideCampPeriod: true, changeVersion: 0 }],
    structureNodes: [{ id: 'node-1', parentId: null, name: 'Lager' }],
    mealPlans: [{ id: 'plan-1', name: 'Standard', sortOrder: 0, version: 3, missingActiveMealCount: 0 }],
    mealPlanDocuments: [plan], cookingUnitGroups: [],
    cookingUnits: [{ id: 'unit-1', campId: 'camp-1', name: 'Küche', sortOrder: 0, groupId: null, standardMealPlanId: 'plan-1', defaultStructureNodeIds: ['node-1'] }],
    operationalMeals: [{
      id: 'state-1', cookingUnitId: 'unit-1', campMealId: 'meal-1', subscriptionState: 1,
      demandOverride: 12, calculatedDemand: 10, effectiveDemand: 12, status: 1,
      mealPlanId: null, mealPlanVersion: null, calculatedAtUtc: null,
      structureOverrideNodeIds: ['node-1'], offerTargets: [],
      recipeChoices: [{ id: 'choice-1', recipeRevisionId: 'revision-1', offerGroupId: 'group-1', mealPlanEntryId: 'entry-1', sortOrder: 0 }],
      warnings: ['Neu berechnen.'],
    }],
    recipeOptions: [{ revisionId: 'revision-1', recipeId: 'recipe-1', name: 'Reis', revisionNumber: 1, isPortionBased: true, suggestedRole: 0 }],
    coverageWarnings: [],
  };

  let fixture: ComponentFixture<MealPlanningComponent>;
  let component: MealPlanningComponent;
  let api: Record<string, ReturnType<typeof vi.fn>>;

  beforeEach(() => {
    api = {
      overview: vi.fn(() => of(structuredClone(overview))),
      recipeHints: vi.fn((_camp: string, revisionId: string) => of({ recipeRevisionId: revisionId, basis: 'PublishedStandard', evaluatedAtUtc: '2026-11-01T00:00:00Z', hasUnknownData: true, hints: [] })),
      plan: vi.fn(() => of(structuredClone(plan))),
      savePlan: vi.fn(() => of({ id: 'plan-1', version: 4 })),
      configureMeal: vi.fn(() => of({ id: 'state-1', version: null })),
      resetStructure: vi.fn(() => of({ id: 'state-1', version: null })),
      deletePlan: vi.fn(() => of({ id: 'plan-1', version: null })),
    };
    TestBed.configureTestingModule({
      imports: [MealPlanningComponent],
      providers: [provideHttpClient(), provideHttpClientTesting(), { provide: MealPlanningApiService, useValue: api }],
    });
    fixture = TestBed.createComponent(MealPlanningComponent);
    fixture.componentRef.setInput('campId', 'camp-1');
    fixture.detectChanges();
    component = fixture.componentInstance;
    vi.spyOn(window, 'confirm').mockReturnValue(true);
  });
  afterEach(() => vi.restoreAllMocks());
  it('separates unit management from existing targeted operative planning', () => {
    fixture.componentRef.setInput('section', 'units'); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('scp-cooking-units-workspace')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.slot-card')).toBeNull();
    fixture.componentRef.setInput('target', { section: 'units', unitId: 'unit-1', mealId: 'meal-1' }); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('scp-cooking-units-workspace')).toBeNull();
    expect(fixture.nativeElement.querySelectorAll('scp-operative-meal-workspace')).toHaveLength(1);
  });

  it('edits a detached copy and discards it on cancel', () => {
    component.openPlan('plan-1');
    component.editingPlan()!.name = 'Nur lokal geändert';

    expect(component.overview()!.mealPlanDocuments[0].name).toBe('Standard');
    component.cancelPlan();
    expect(component.editingPlan()).toBeNull();
  });
  it('includes operative drafts in the application navigation guard', () => {
    fixture.componentRef.setInput('section', 'units');
    fixture.componentRef.setInput('target', { section: 'units', unitId: 'unit-1', mealId: 'meal-1' }); fixture.detectChanges();
    component.operativeWorkspace()!.draft.demandOverride = 25;
    fixture.nativeElement.querySelector('dialog').close = vi.fn();
    expect(component.hasPendingUnitChanges()).toBe(true);
    component.discardUnitChanges(); expect(component.hasPendingUnitChanges()).toBe(false);
  });

  it('blocks recalculation when a visible supply editor has unsaved decisions', () => {
    fixture.componentRef.setInput('section', 'units');
    fixture.componentRef.setInput('target', { section: 'units', unitId: 'unit-1', mealId: 'meal-1' });
    component.overview.update(value => value ? { ...value, operationalMeals: value.operationalMeals.map(state => ({ ...state, supply: { solutions: [], candidates: [], problems: [] } })) } : value);
    fixture.detectChanges();
    const editor = fixture.debugElement.query(By.directive(SupplySolutionsComponent));
    expect(editor).toBeTruthy();
    vi.spyOn(editor.componentInstance, 'hasUnsavedChanges').mockReturnValue(true);
    const alert = vi.spyOn(window, 'alert').mockImplementation(() => {});
    api['calculate'] = vi.fn();
    component.calculate('unit-1', 'meal-1');
    expect(api['calculate']).not.toHaveBeenCalled();
    expect(component.operativeWorkspace()!.error()).toContain('Es wurde nichts verworfen.');
    alert.mockRestore();
  });

  it('saves one explicit edit and closes the editor', () => {
    component.openPlan('plan-1');
    component.editingPlan()!.name = 'Sommerlager';

    component.savePlan();

    expect(api['savePlan']).toHaveBeenCalledOnce();
    expect(api['savePlan']).toHaveBeenCalledWith('camp-1', expect.objectContaining({ name: 'Sommerlager' }));
    expect(component.editingPlan()).toBeNull();
  });

  it('retains a changed draft on rejected navigation and a version conflict', () => {
    component.openPlan('plan-1'); component.editingPlan()!.name = 'Ungespeichert';
    vi.mocked(window.confirm).mockReturnValue(false);
    component.cancelPlan(); component.openPlan('another');
    expect(component.editingPlan()!.name).toBe('Ungespeichert');
    expect(api['plan']).toHaveBeenCalledTimes(1);
    api['savePlan'].mockReturnValue(throwError(() => ({ error: { message: 'Versionskonflikt' } })));
    component.savePlan();
    expect(component.editingPlan()!.name).toBe('Ungespeichert');
    expect(component.hasPendingPlanChanges()).toBe(true); expect(component.planBusy()).toBe(false);
  });

  it('opens a dashboard target once without reloading its draft on later edits', () => {
    fixture.componentRef.setInput('section', 'plans');
    fixture.componentRef.setInput('target', { section: 'plans', planId: 'plan-1' }); fixture.detectChanges();
    expect(api['plan']).toHaveBeenCalledTimes(1);
    component.editingPlan()!.name = 'Entwurf aus dem Dashboard'; fixture.detectChanges();
    expect(api['plan']).toHaveBeenCalledTimes(1);
    expect(component.hasPendingPlanChanges()).toBe(true);
  });

  it('saves an incomplete plan without serializing empty component placeholders', () => {
    component.openPlan('plan-1');
    component.editingPlan()!.offerGroups = [{ id: 'empty', campMealId: 'meal-1', name: 'Noch leer', entries: [], sortOrder: 0 }];
    component.savePlan();
    expect(api['savePlan']).toHaveBeenCalledWith('camp-1', expect.objectContaining({ offerGroups: [], version: 3 }));
    expect(component.notice()).toContain('ausgelassen');
  });

  it('does not save, create, delete or reorder a read-only plan', () => {
    component.openPlan('plan-1'); fixture.componentRef.setInput('disabled', true); fixture.detectChanges();
    component.savePlan(); component.deletePlan('plan-1'); component.createPlan('Test'); component.movePlan(0, 1);
    expect(api['savePlan']).not.toHaveBeenCalled(); expect(api['deletePlan']).not.toHaveBeenCalled();
  });

  it('maps all operational states and resets custom decisions to standard', () => {
    expect(component.statusLabel(0)).toBe('Aktuell');
    expect(component.statusLabel(1)).toBe('Veraltet');
    expect(component.statusLabel(2)).toBe('Unvollständig');
    const draft = component.mealDraft('unit-1', 'meal-1')!;

    component.resetPlan(overview.cookingUnits[0], overview.meals[0], draft);

    expect(api['configureMeal']).toHaveBeenCalledWith('camp-1', 'unit-1', 'meal-1', expect.objectContaining({
      subscriptionState: 0, demandOverride: null, offerTargets: [], recipeChoices: [],
    }));
  });

  it('explicitly clears a saved structure override when its toggle is disabled', () => {
    const draft = component.mealDraft('unit-1', 'meal-1')!;
    expect(draft.structureOverrideNodeIds).toEqual(['node-1']);
    draft.useStructureOverride = false;
    const reloaded = structuredClone(overview);
    reloaded.operationalMeals[0].structureOverrideNodeIds = [];
    api['overview'].mockReturnValue(of(reloaded));

    component.saveMeal(overview.cookingUnits[0], overview.meals[0], draft);

    expect(api['configureMeal']).toHaveBeenCalledWith('camp-1', 'unit-1', 'meal-1', expect.objectContaining({
      structureOverrideNodeIds: [],
    }));
    expect(component.mealDraft('unit-1', 'meal-1')!.useStructureOverride).toBe(false);
  });

  it('uses the dedicated structure reset and exposes blocking references', () => {
    component.resetStructure('unit-1', 'meal-1');
    expect(api['resetStructure']).toHaveBeenCalledWith('camp-1', 'unit-1', 'meal-1');

    api['deletePlan'].mockReturnValue(throwError(() => ({
      error: { message: 'Der Mahlzeitenplan wird noch verwendet.', references: ['Kocheinheit: Küche'] },
    })));
    component.deletePlan('plan-1');
    expect(component.error()).toContain('Kocheinheit: Küche');
  });

  it('blocks schedule changes while a menu plan has unsaved changes', () => {
    component.overview.set({ ...structuredClone(overview), scheduleVersion: 4 });
    api['addAdditionalMeal'] = vi.fn(() => of({ scheduleVersion: 5 }));
    component.openPlan('plan-1'); component.editingPlan()!.name = 'Draft';
    component.changeSchedule({ kind: 'add', date: '2027-07-02', name: 'Snack' });
    expect(api['addAdditionalMeal']).not.toHaveBeenCalled();
    expect(component.editingPlan()!.name).toBe('Draft');
  });
  it('saves the shared schedule immediately and retains the unchanged open plan', () => {
    component.overview.set({ ...structuredClone(overview), scheduleVersion: 4 });
    component.openPlan('plan-1');
    const open = component.editingPlan();
    api['addAdditionalMeal'] = vi.fn(() => of({ scheduleVersion: 5 }));
    api['overview'].mockReturnValue(of({ ...structuredClone(overview), scheduleVersion: 5 }));
    component.changeSchedule({ kind: 'add', date: '2027-07-02', name: 'Snack' });
    expect(api['addAdditionalMeal']).toHaveBeenCalledWith('camp-1', '2027-07-02', 'Snack', 4);
    expect(component.overview()!.scheduleVersion).toBe(5);
    expect(component.editingPlan()).toBe(open);
    expect(api['savePlan']).not.toHaveBeenCalled();
    expect(component.planBusy()).toBe(false);
  });
  it('reorders meals with an unsaved component without saving or discarding the plan draft', () => {
    component.overview.set({ ...structuredClone(overview), scheduleVersion: 4 });
    component.openPlan('plan-1');
    const draft = component.editingPlan()!;
    draft.offerGroups.push({ id: 'new-component', campMealId: 'meal-1', name: 'Beilage', sortOrder: 1, entries: [] });
    api['orderDayMeals'] = vi.fn(() => of({ scheduleVersion: 5 }));
    api['overview'].mockReturnValue(of({ ...structuredClone(overview), scheduleVersion: 5 }));
    component.changeSchedule({ kind: 'order', date: '2027-07-02', mealIds: ['meal-1'] });
    expect(api['orderDayMeals']).toHaveBeenCalledWith('camp-1', '2027-07-02', ['meal-1'], 4);
    expect(component.editingPlan()).toBe(draft);
    expect(component.editingPlan()!.offerGroups.some(group => group.id === 'new-component')).toBe(true);
    expect(component.hasPendingPlanChanges()).toBe(true);
    expect(api['savePlan']).not.toHaveBeenCalled();
    expect(component.overview()!.scheduleVersion).toBe(5);
  });
  it('keeps the plan on schedule conflicts and never automatically retries the mutation', () => {
    component.overview.set({ ...structuredClone(overview), scheduleVersion: 4 });
    component.openPlan('plan-1');
    api['removeAdditionalMeal'] = vi.fn(() => throwError(() => ({ error: { code: 'meal_schedule_changed' } })));
    component.changeSchedule({ kind: 'remove', mealId: 'meal-1' });
    expect(api['removeAdditionalMeal']).toHaveBeenCalledOnce();
    expect(component.editingPlan()!.id).toBe('plan-1');
    expect(component.error()).toContain('inzwischen geändert');
    expect(component.planBusy()).toBe(false);
  });
  it.each([false, true])('changes meal activity to %s with recipe edits still pending', isActive => {
    const data = structuredClone(overview);
    data.scheduleVersion = 4; data.meals[0].kind = 1; data.meals[0].isActive = !isActive;
    component.overview.set(data);
    component.openPlan('plan-1');
    const draft = component.editingPlan()!;
    draft.offerGroups[0].entries[0].note = 'Ungespeicherter Hinweis';
    api['setMealActivity'] = vi.fn(() => of(undefined));
    api['overview'].mockReturnValue(of({ ...data, scheduleVersion: 5,
      meals: data.meals.map(meal => ({ ...meal, isActive })) }));
    component.changeSchedule({ kind: 'activity', mealId: 'meal-1', isActive });
    expect(api['setMealActivity']).toHaveBeenCalledWith('camp-1', 'meal-1', isActive, 4);
    expect(component.editingPlan()).toBe(draft);
    expect(draft.offerGroups[0].entries[0].note).toBe('Ungespeicherter Hinweis');
    expect(component.hasPendingPlanChanges()).toBe(true);
    expect(component.overview()!.meals[0].isActive).toBe(isActive);
    expect(api['savePlan']).not.toHaveBeenCalled();
  });
});
