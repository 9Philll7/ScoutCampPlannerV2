import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { CommonSupplyChoice, CookingUnitMeal, MealPlanningApiService, SupplyPositionOption, SupplySolution } from './meal-planning-api.service';
import { SupplySolutionsComponent } from './supply-solutions.component';

describe('SupplySolutionsComponent', () => {
  const state: CookingUnitMeal = {
    id: 'state', cookingUnitId: 'unit', campMealId: 'meal', subscriptionState: 0,
    demandOverride: null, calculatedDemand: 2, effectiveDemand: 2, status: 0,
    mealPlanId: null, mealPlanVersion: null, calculatedAtUtc: null,
    structureOverrideNodeIds: [], offerTargets: [], recipeChoices: [], warnings: [], supplyVersion: 4,
    supply: { candidates: [], problems: [], solutions: [{ id: 's', recipeRevisionId: 'r', offerGroupId: null,
      portions: 2, source: 0, allocations: [{ requirementGroupKey: 'g', portions: 2 }], positionSelections: {} }] }
  };
  function create() {
    const api = { saveSupply: vi.fn(() => of({ id: 'state', version: null })),
      verifySupply: vi.fn(() => of({ id: 'state', version: null })),
      supplyPositions: vi.fn(() => of([] as SupplyPositionOption[])) };
    TestBed.configureTestingModule({ imports: [SupplySolutionsComponent], providers: [{ provide: MealPlanningApiService, useValue: api }] });
    const fixture = TestBed.createComponent(SupplySolutionsComponent);
    fixture.componentRef.setInput('campId', 'camp'); fixture.componentRef.setInput('state', structuredClone(state));
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, api };
  }
  function askChoice(): CommonSupplyChoice {
    const base: SupplySolution = { id: 'standard', recipeRevisionId: 'r', offerGroupId: 'offer', portions: 2,
      source: 0, allocations: [{ requirementGroupKey: 'standard', portions: 2 }], positionSelections: {} };
    const adapted: SupplySolution = { ...structuredClone(base), id: 'adapted',
      allocations: [{ requirementGroupKey: 'allergy', portions: 2 }], positionSelections: { path: { variantKey: 'free', replacementRuleId: null } } };
    return { offerGroupId: 'offer', requirementGroupKeys: ['standard', 'allergy'], separateSolutions: [base, adapted],
      togetherOptions: [{ ...structuredClone(adapted), id: 'together', portions: 4, allocations: [...base.allocations, ...adapted.allocations] }] };
  }
  it('offers two explicit Ask paths without materializing either before selection', () => {
    const { fixture, component } = create();
    fixture.componentRef.setInput('state', { ...state, supply: { solutions: [], candidates: [], problems: [], commonSupplyChoices: [askChoice()] } });
    fixture.detectChanges();
    expect(component.drafts()).toEqual([]);
    expect(fixture.nativeElement.textContent).toContain('Gemeinsam versorgen');
    expect(fixture.nativeElement.textContent).toContain('Getrennt versorgen');
  });
  for (const initiallyTogether of [true, false]) {
    it(`switches Ask exclusively and retains provenance after reload (together first: ${initiallyTogether})`, () => {
      const { fixture, component, api } = create();
      const choice = askChoice();
      const unrelated = structuredClone(state.supply!.solutions[0]);
      // Also repair the displayed overlap when switching from an older calculated snapshot.
      const current = structuredClone(state);
      current.supply = { solutions: [unrelated, ...choice.separateSolutions], candidates: [], problems: [], commonSupplyChoices: [choice] };
      fixture.componentRef.setInput('state', current); fixture.detectChanges();
      for (const together of [initiallyTogether, !initiallyTogether, initiallyTogether]) {
        component.chooseCommon(choice, together ? choice.togetherOptions : choice.separateSolutions);
        const selected = component.drafts().filter(value => value.offerGroupId === 'offer');
        expect(selected).toHaveLength(together ? 1 : 2);
        expect(selected.every(value => value.source === 1)).toBe(true);
        expect(selected.reduce((sum, value) => sum + value.portions, 0)).toBe(4);
        expect(selected.flatMap(value => value.allocations).map(value => value.requirementGroupKey).sort()).toEqual(['allergy', 'standard']);
        expect(component.drafts().find(value => value.id === 's')!.source).toBe(0);
        component.save();
        expect(api.saveSupply).toHaveBeenLastCalledWith('camp', expect.anything(), selected);
        const reloaded = { ...structuredClone(current), manualSupplySolutions: structuredClone(selected) };
        reloaded.supply!.solutions = [unrelated, ...structuredClone(selected)];
        fixture.componentRef.setInput('state', reloaded); fixture.detectChanges();
        expect(component.drafts()).toHaveLength(1 + selected.length);
        expect(component.hasUnsavedChanges()).toBe(false);
      }
    });
  }
  it('edits detached values and submits manual provenance with the current version', () => {
    const { component, api } = create();
    component.drafts()[0].portions = 3;
    expect(component.state().supply!.solutions[0].portions).toBe(2);
    component.save();
    expect(api.saveSupply).toHaveBeenCalledWith('camp', expect.objectContaining({ supplyVersion: 4 }),
      [expect.objectContaining({ source: 1, portions: 3 })]);
  });
  it('clears positional choices when the recipe revision changes and resets explicitly', () => {
    const { component, api } = create();
    component.drafts()[0].positionSelections = { path: { replacementRuleId: 'replacement', variantKey: null } };
    component.changeRecipe(component.drafts()[0], 'other');
    expect(component.drafts()[0].positionSelections).toEqual({});
    component.reset();
    expect(api.saveSupply).toHaveBeenCalledWith('camp', expect.anything(), []);
  });
  it('preserves drafts and reports failed saves without retrying', () => {
    const { component, api } = create();
    api.saveSupply.mockReturnValue(throwError(() => new Error('conflict')));
    component.save();
    expect(api.saveSupply).toHaveBeenCalledOnce(); expect(component.error()).not.toBe('');
    expect(component.drafts()).toHaveLength(1); expect(component.busy()).toBe(false);
  });
  it('loads occurrence choices and removes hidden child decisions when a subrecipe is replaced', () => {
    const { component, api } = create();
    const position: SupplyPositionOption = { path: 'root/sub', name: 'Beilage', choices: [
      { label: 'Original', selection: null }, { label: 'Ersatz', selection: { replacementRuleId: 'replacement', variantKey: null } }
    ] };
    api.supplyPositions.mockReturnValue(of([position]));
    component.loadPositions('r');
    expect(component.positions()['r']).toEqual([position]);
    const solution = component.drafts()[0];
    solution.positionSelections['root/sub/child/ingredient'] = { replacementRuleId: null, variantKey: 'variant' };
    component.selectPosition(solution, position, 1);
    expect(component.hiddenPosition(solution, 'root/sub/child/ingredient')).toBe(true);
    expect(solution.positionSelections['root/sub/child/ingredient']).toBeUndefined();
    component.selectPosition(solution, position, 0);
    expect(solution.positionSelections).toEqual({});
  });
  it('displays pending manual decisions instead of the old calculated values', () => {
    const { fixture, component } = create();
    const updated = structuredClone(state);
    updated.manualSupplySolutions = [{ ...structuredClone(state.supply!.solutions[0]), portions: 5, source: 1 }];
    fixture.componentRef.setInput('state', updated); fixture.detectChanges();
    expect(component.drafts()).toHaveLength(1);
    expect(component.drafts()[0].portions).toBe(5);
  });
  it('adds group allocations explicitly without duplicates or implicit portions', () => {
    const { component } = create();
    const solution = component.drafts()[0];
    component.addAllocation(solution, 'other'); component.addAllocation(solution, 'other');
    expect(solution.allocations.filter(value => value.requirementGroupKey === 'other')).toEqual([{ requirementGroupKey: 'other', portions: 0 }]);
  });
  it('can verify NoSupplyRequired and emits a distinct verification event', () => {
    const { fixture, component, api } = create();
    fixture.componentRef.setInput('state', { ...state, subscriptionState: 2, supply: { solutions: [], candidates: [], problems: [] } });
    fixture.detectChanges();
    const verified = vi.fn(); component.verified.subscribe(verified);
    component.verify(0);
    expect(api.verifySupply).toHaveBeenCalledWith('camp', expect.objectContaining({ subscriptionState: 2 }), 0, '');
    expect(verified).toHaveBeenCalledOnce();
    expect(fixture.nativeElement.textContent).toContain('keine Versorgung erforderlich');
    expect(fixture.nativeElement.textContent).not.toContain('Eigene Lösung hinzufügen');
  });
  it('does not convert unchanged automatic solutions when another proposal is selected', () => {
    const { component, api } = create();
    const candidate = { ...structuredClone(state.supply!.solutions[0]), recipeRevisionId: 'other', id: 'candidate' };
    component.choose(candidate); component.save();
    expect(api.saveSupply).toHaveBeenCalledWith('camp', expect.anything(), [expect.objectContaining({ recipeRevisionId: 'other', source: 1 })]);
    expect(component.isManual(component.drafts()[0])).toBe(false);
  });
  it('displays calculated ingredient quantities and stepwise parameters', () => {
    const { fixture } = create();
    const updated = structuredClone(state);
    updated.supply!.quantities = [{ solutionId: 's', positionPath: 'path', ingredientName: 'TEST Ingredient', quantity: 90,
      unit: 'g', scalingMode: 2, stepwiseScaling: { stepSize: 3, quantityPerStep: 30 } }];
    fixture.componentRef.setInput('state', updated); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('90 g');
    expect(fixture.nativeElement.textContent).toContain('Stufenweise');
    expect(fixture.nativeElement.textContent).toContain('30 g je 3 Portionen');
  });
  it('rejects duplicate proposals and guards verification of unsaved drafts', () => {
    const { fixture, component, api } = create();
    const candidate = { ...structuredClone(state.supply!.solutions[0]), recipeRevisionId: 'other' };
    component.choose(candidate); component.choose(candidate);
    fixture.detectChanges();
    expect(component.drafts()).toHaveLength(2);
    expect(component.hasUnsavedChanges()).toBe(true);
    component.verify(0); expect(api.verifySupply).not.toHaveBeenCalled();
  });
});
