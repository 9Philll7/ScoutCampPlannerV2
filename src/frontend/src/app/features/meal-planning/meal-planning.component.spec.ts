import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import {
  MealPlanDocument,
  MealPlanningApiService,
  MealPlanningOverview,
} from './meal-planning-api.service';
import { MealPlanningComponent } from './meal-planning.component';

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
      plan: vi.fn(() => of(structuredClone(plan))),
      savePlan: vi.fn(() => of({ id: 'plan-1', version: 4 })),
      configureMeal: vi.fn(() => of({ id: 'state-1', version: null })),
      resetStructure: vi.fn(() => of({ id: 'state-1', version: null })),
      deletePlan: vi.fn(() => of({ id: 'plan-1', version: null })),
    };
    TestBed.configureTestingModule({
      imports: [MealPlanningComponent],
      providers: [{ provide: MealPlanningApiService, useValue: api }],
    });
    fixture = TestBed.createComponent(MealPlanningComponent);
    fixture.componentRef.setInput('campId', 'camp-1');
    fixture.detectChanges();
    component = fixture.componentInstance;
  });

  it('edits a detached copy and discards it on cancel', () => {
    component.openPlan('plan-1');
    component.editingPlan()!.name = 'Nur lokal geändert';

    expect(component.overview()!.mealPlanDocuments[0].name).toBe('Standard');
    component.cancelPlan();
    expect(component.editingPlan()).toBeNull();
  });

  it('saves one explicit edit and closes the editor', () => {
    component.openPlan('plan-1');
    component.editingPlan()!.name = 'Sommerlager';

    component.savePlan();

    expect(api['savePlan']).toHaveBeenCalledOnce();
    expect(api['savePlan']).toHaveBeenCalledWith('camp-1', expect.objectContaining({ name: 'Sommerlager' }));
    expect(component.editingPlan()).toBeNull();
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
});
