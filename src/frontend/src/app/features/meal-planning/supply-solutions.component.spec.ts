import { TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { vi } from 'vitest';
import { CommonSupplyChoice, CookingUnitMeal, MealPlanningApiService, SupplyPositionOption, SupplySolution, SupplySuggestionDocument } from './meal-planning-api.service';
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
    const api = { previewSupply: vi.fn(() => of([] as { solutionId: string; problems: import('./meal-planning-api.service').SupplyProblem[]; error: string | null }[])), supplySuggestions: vi.fn(() => of<SupplySuggestionDocument>({ expectedVersion: 4, offers: [], unavailableReason: null })), applySupplySuggestion: vi.fn(() => of({})), saveSupply: vi.fn(() => of({ id: 'state', version: null })),
      verifySupply: vi.fn(() => of({ id: 'state', version: null })),
      supplyPositions: vi.fn(() => of([] as SupplyPositionOption[])) };
    TestBed.configureTestingModule({ imports: [SupplySolutionsComponent], providers: [{ provide: MealPlanningApiService, useValue: api }] });
    const fixture = TestBed.createComponent(SupplySolutionsComponent);
    fixture.componentRef.setInput('campId', 'camp'); fixture.componentRef.setInput('state', structuredClone(state));
    fixture.detectChanges();
    for (const dialog of fixture.nativeElement.querySelectorAll('dialog') as NodeListOf<HTMLDialogElement>) {
      dialog.close = vi.fn(() => dialog.removeAttribute('open'));
      dialog.showModal = vi.fn(() => dialog.setAttribute('open', ''));
    }
    return { fixture, component: fixture.componentInstance, api };
  }
  it('does not persist automatic recipes just by opening and saving an unchanged own editor', () => {
    const { component, api } = create(); component.openEditor(); component.saveOwnSolution();
    expect(api.saveSupply).not.toHaveBeenCalled(); expect(component.hasUnsavedDecisions()).toBe(false);
  });
  it('opens all current recipes for editing without a scope selector or an automatic write', () => {
    const { fixture, component, api } = create(); component.openEditor(); fixture.detectChanges();
    expect(component.ownRows()).toHaveLength(1); expect(component.planRows()).toEqual([]);
    expect(fixture.nativeElement.querySelector('.own-scope')).toBeNull();
    expect(component.hasUnsavedDecisions()).toBe(false); expect(api.saveSupply).not.toHaveBeenCalled();
    component.changeRecipe(component.ownRows()[0], 'changed'); component.saveOwnSolution();
    expect(api.saveSupply).toHaveBeenCalledWith('camp', expect.anything(), [expect.objectContaining({ recipeRevisionId: 'changed', source: 1 })], [], true);
  });
  it('lets existing and new recipes use any requirement group', () => {
    const { fixture, component } = create(); fixture.componentRef.setInput('state', partialState());
    fixture.componentRef.setInput('recipes', [{ revisionId: 'new', name: 'New' }]); fixture.detectChanges(); component.openEditor();
    expect(component.ownAvailableGroups(component.ownRows()[0])).toHaveLength(2);
    component.addOwnRecipe('new'); const added = component.ownRows().find(value => value.recipeRevisionId === 'new')!;
    component.assignAllOwnGroups(added); expect(added.allocations).toHaveLength(2);
    component.remove(component.ownRows()[0].id); expect(component.ownRows()).toEqual([added]);
  });
  it('shows recipe conflicts in a read-only preview and still saves the chosen solution', () => {
    const { fixture, component, api } = create(); component.openEditor();
    api.previewSupply.mockReturnValue(of([{ solutionId: 's', problems: [{ category: 0, code: 10, requirementGroupKey: 'g', referenceId: null, positionPath: null }], error: null }]));
    component.changeRecipe(component.ownRows()[0], 'unsuitable'); component.refreshOwnPreview(); fixture.detectChanges();
    expect(component.ownConflictCount()).toBe(1); expect(component.ownRecipeWarnings('s')[0]).toContain('Allergenkonflikt');
    expect(api.saveSupply).not.toHaveBeenCalled(); component.saveOwnSolution();
    expect(api.saveSupply).toHaveBeenCalledWith('camp', expect.anything(), [expect.objectContaining({ recipeRevisionId: 'unsuitable' })], [], true);
  });
  it('discards out-of-order preview responses and reports unavailable checking', () => {
    const { component, api } = create(); component.openEditor(); const old = new Subject<any[]>();
    api.previewSupply.mockReturnValueOnce(old); component.refreshOwnPreview();
    component.changeRecipe(component.ownRows()[0], 'changed'); component.refreshOwnPreview();
    old.next([{ solutionId: 's', problems: [], error: 'Old failure' }]); expect(component.ownConflictCount()).toBe(0);
    api.previewSupply.mockReturnValueOnce(throwError(() => ({}))); component.refreshOwnPreview();
    expect(component.ownPreviewError()).toContain('nicht verfügbar'); expect(component.ownPreviewLoading()).toBe(false);
  });
  it('counts coverage once per person across components and flags mismatched allocations', () => {
    const { fixture, component } = create(); const current = structuredClone(state);
    current.supply!.requirements = [{ requirements: { key: 'g' }, portions: 2 }];
    fixture.componentRef.setInput('state', current); fixture.componentRef.setInput('offers', [{ id: 'main', name: 'Main' }, { id: 'side', name: 'Side' }]); fixture.detectChanges();
    component.completeOwnSupply.set(true);
    component.drafts.set(['main', 'side'].map((offerGroupId,index) => ({ ...structuredClone(state.supply!.solutions[0]), id: String(index), offerGroupId })));
    expect(component.ownCoverage()).toEqual(expect.objectContaining({ total: 2, missing: 0, excess: 0, invalid: false }));
    component.drafts()[0].portions = 3; expect(component.ownCoverage().invalid).toBe(true);
    component.remove('1'); expect(component.ownCoverage().missing).toBe(2);
  });
  it('keeps the normal view read-only and opens decisions explicitly', () => {
    const { fixture, component } = create();
    expect(fixture.nativeElement.querySelector('.supply-normal input')).toBeNull();
    expect(fixture.nativeElement.querySelector('dialog[open]')).toBeNull();
    component.openEditor();
    expect(fixture.nativeElement.querySelector('.supply-dialog[open]')).not.toBeNull();
  });
  it('shows only total portions rather than requirement allocations on a combined recipe card', () => {
    const { fixture, component } = create(); const current = structuredClone(state);
    current.supply!.solutions[0].portions = 5;
    current.supply!.solutions[0].allocations = [{ requirementGroupKey: 'group-one', portions: 2 }, { requirementGroupKey: 'group-two', portions: 3 }];
    fixture.componentRef.setInput('state', current); fixture.detectChanges();
    const card = fixture.nativeElement.querySelector('.solution-card') as HTMLElement;
    expect(card.textContent).toContain('5 Personen'); expect(card.querySelector('.allocation-summary')).toBeNull();
    expect(card.textContent).not.toContain('Anforderungen');
    expect(component.drafts()[0].allocations).toHaveLength(2); expect(component.hasUnsavedChanges()).toBe(false);
  });
  it('summarizes unmet groups in one card without counting multiple components twice', () => {
    const { fixture, component, api } = create();
    const current = structuredClone(state);
    current.supply = { solutions: [], candidates: [], requirements: [
      { requirements: { key: 'milk' }, portions: 2 },
      { requirements: { key: 'plant' }, portions: 1 }
    ], problems: [
      ...['main', 'side'].map(referenceId => ({ code: 15, category: 1, positionPath: null, referenceId, requirementGroupKey: 'milk' })),
      { code: 10, category: 0, positionPath: null, referenceId: null, requirementGroupKey: 'milk' },
      { code: 15, category: 1, positionPath: null, referenceId: 'main', requirementGroupKey: 'plant' },
      { code: 12, category: 0, positionPath: null, referenceId: null, requirementGroupKey: 'plant' }
    ] };
    fixture.componentRef.setInput('state', current); fixture.detectChanges();
    const cards = fixture.nativeElement.querySelectorAll('.conflict-summary');
    expect(cards).toHaveLength(1);
    expect(cards[0].querySelector('header').textContent).toContain('3 Personen nicht abgedeckt');
    expect(cards[0].querySelectorAll('.conflict-reasons>li')).toHaveLength(2);
    expect(cards[0].textContent).toContain('Allergenkonflikt');
    expect(cards[0].textContent).toContain('Herkunft für Ernährungsform ausgeschlossen');
    expect(cards[0].querySelectorAll('button')).toHaveLength(1);
    cards[0].querySelector('button').click();
    expect(component.focusedRequirement()).toBeUndefined();
    expect(fixture.nativeElement.querySelector('.conflict-dialog[open]')).not.toBeNull();
    expect(api.saveSupply).not.toHaveBeenCalled();
  });
  it('does not invent a portion total for unknown coverage or show an empty conflict card', () => {
    const { fixture } = create();
    expect(fixture.nativeElement.querySelector('.conflict-summary')).toBeNull();
    const current = structuredClone(state);
    current.supply!.problems = [{ code: 15, category: 1, positionPath: null, referenceId: 'main', requirementGroupKey: 'unknown' }];
    fixture.componentRef.setInput('state', current); fixture.componentRef.setInput('disabled', true); fixture.detectChanges();
    const card = fixture.nativeElement.querySelector('.conflict-summary');
    expect(card.querySelector('header').textContent).toContain('Versorgung klären');
    expect(card.querySelector('.conflict-reason-heading strong')).toBeNull();
    expect(card.querySelector('button')).toBeNull();
  });
  function partialState(): CookingUnitMeal {
    return { ...structuredClone(state), supply: { solutions: [{ ...structuredClone(state.supply!.solutions[0]), portions: 3,
      offerGroupId: 'main', allocations: [{ requirementGroupKey: 'g', portions: 1 }, { requirementGroupKey: 'covered', portions: 2 }] }],
      candidates: [], requirements: [{ requirements: { key: 'g' }, portions: 3 }, { requirements: { key: 'covered' }, portions: 2 }],
      problems: [{ code: 15, category: 1, requirementGroupKey: 'g', referenceId: 'main', positionPath: null }] } };
  }
  it('loads assessed proposals without preselection and saves only on explicit confirmation', () => {
    const { fixture, component, api } = create();
    const document: SupplySuggestionDocument = { expectedVersion: 4, unavailableReason: null, offers: [{ offerGroupId: 'main', name: 'Main', preference: 0, missingPersons: 1, coveredPersons: 9,
      options: [{ id: 'safe', scope: 'whole', label: 'Gemeinsame Versorgung', recipeName: 'Suitable recipe', preferred: true,
        solution: { ...structuredClone(state.supply!.solutions[0]), portions: 10 } }] }] };
    api.supplySuggestions.mockReturnValue(of(document)); component.openConflictEditor(); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.conflict-dialog[open]')).not.toBeNull();
    expect(component.selectedSuggestion()).toBeUndefined(); expect(api.applySupplySuggestion).not.toHaveBeenCalled();
    fixture.nativeElement.querySelector('.suggestion-card input').click(); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.suggestion-impact').textContent).toContain('ersetzt');
    expect(api.applySupplySuggestion).not.toHaveBeenCalled();
    const saved = vi.fn(); component.saved.subscribe(saved); component.applySuggestion();
    expect(api.applySupplySuggestion).toHaveBeenCalledWith('camp', expect.anything(), 4, 'safe'); expect(saved).toHaveBeenCalledWith(true);
  });
  it('clears an invalidated suggestion after a failed apply without retrying a write', () => {
    const { component, api } = create();
    const solution = structuredClone(state.supply!.solutions[0]);
    component.suggestions.set({ expectedVersion: 4, unavailableReason: null, offers: [{ offerGroupId: null, name: 'Main', preference: 2, missingPersons: 2, coveredPersons: 0,
      options: [{ id: 'stale', scope: 'missing', label: 'Open', recipeName: 'Recipe', preferred: true, solution }] }] });
    component.suggestionSelection.set('stale'); api.applySupplySuggestion.mockReturnValue(throwError(() => ({ status: 409 })));
    component.applySuggestion(); expect(component.selectedSuggestion()).toBeUndefined(); expect(component.error()).toContain('geändert');
    expect(api.applySupplySuggestion).toHaveBeenCalledTimes(1);
  });
  it('resolves missing portions without changing or dropping existing partial automatic coverage', () => {
    const { fixture, component, api } = create();
    fixture.componentRef.setInput('recipes', [{ revisionId: 'new', name: 'Alternative' }]);
    fixture.componentRef.setInput('state', partialState()); fixture.detectChanges();
    component.openConflictEditor(); fixture.detectChanges();
    expect(component.editableDrafts()).toHaveLength(0);
    expect(component.protectedSolutions().reduce((sum,value) => sum + value.portions,0)).toBe(3);
    component.addConflictSolution(); fixture.detectChanges();
    expect(component.editableDrafts()).toHaveLength(1);
    expect(component.protectedSolutions().map(value => value.recipeRevisionId)).not.toContain('new');
    expect(component.editableDrafts()[0].portions).toBe(2);
    expect(component.editableDrafts()[0].allocations).toEqual([{ requirementGroupKey: 'g', portions: 2 }]);
    component.saveConflictResolution();
    expect(api.saveSupply).toHaveBeenCalledWith('camp', expect.anything(), expect.arrayContaining([
      expect.objectContaining({ recipeRevisionId: 'r', source: 1, portions: 3, allocations: [{ requirementGroupKey: 'g', portions: 1 }, { requirementGroupKey: 'covered', portions: 2 }] }),
      expect.objectContaining({ recipeRevisionId: 'new', source: 1, portions: 2, offerGroupId: 'main', allocations: [{ requirementGroupKey: 'g', portions: 2 }] })
    ]));
  });
  it('rejects edits to protected solutions and excessive conflict portions, and allows discarding additions', () => {
    const { fixture, component, api } = create();
    fixture.componentRef.setInput('recipes', [{ revisionId: 'new', name: 'Alternative' }]);
    fixture.componentRef.setInput('state', partialState()); fixture.detectChanges(); component.openConflictEditor();
    component.addConflictSolution(); component.changePortions(component.editableDrafts()[0], 4); component.saveConflictResolution();
    expect(api.saveSupply).not.toHaveBeenCalled();
    component.changePortions(component.editableDrafts()[0], 2); component.drafts()[0].portions = 4; component.saveConflictResolution();
    expect(api.saveSupply).not.toHaveBeenCalled();
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(true);
    component.closeEditor(); expect(component.drafts()).toEqual(partialState().supply!.solutions); confirm.mockRestore();
  });
  it('bundles uncovered groups for separate supply and still offers individual scopes', () => {
    const { fixture, component, api } = create(); const current = partialState();
    current.supply!.requirements!.push({ requirements: { key: 'other' }, portions: 4 });
    current.supply!.problems.push({ code: 15, category: 1, requirementGroupKey: 'other', referenceId: 'main', positionPath: null });
    fixture.componentRef.setInput('recipes', [{ revisionId: 'new', name: 'Alternative' }]);
    fixture.componentRef.setInput('togetherPreference', 2); fixture.componentRef.setInput('state', current); fixture.detectChanges();
    component.openConflictEditor();
    expect(component.conflictScopes().map(value => value.kind)).toEqual(['missing', 'single:g', 'single:other']);
    component.addConflictSolution();
    expect(component.editableDrafts()[0].portions).toBe(6);
    expect(component.editableDrafts()[0].allocations).toEqual([{ requirementGroupKey: 'g', portions: 2 }, { requirementGroupKey: 'other', portions: 4 }]);
    component.saveConflictResolution();
    expect(api.saveSupply).toHaveBeenCalledWith('camp', expect.anything(), expect.arrayContaining([
      expect.objectContaining({ recipeRevisionId: 'new', portions: 6, allocations: expect.arrayContaining([{ requirementGroupKey: 'other', portions: 4 }]) }),
      expect.objectContaining({ recipeRevisionId: 'r', portions: 3 })
    ]));
  });
  it('explicitly includes already served people for together supply while preserving another component', () => {
    const { fixture, component, api } = create(); const current = partialState();
    current.supply!.solutions.push({ ...structuredClone(current.supply!.solutions[0]), id: 'side', source: 1, offerGroupId: 'side' });
    fixture.componentRef.setInput('recipes', [{ revisionId: 'new', name: 'Alternative' }]);
    fixture.componentRef.setInput('togetherPreference', 0); fixture.componentRef.setInput('state', current); fixture.detectChanges();
    component.openConflictEditor();
    expect(component.conflictScopes()[0].kind).toBe('whole');
    expect(component.protectedSolutions()).toHaveLength(2);
    component.addConflictSolution();
    expect(component.editableDrafts()[0].portions).toBe(5);
    expect(component.protectedSolutions().map(value => value.offerGroupId)).toEqual(['side']);
    component.saveConflictResolution();
    expect(api.saveSupply).toHaveBeenCalledWith('camp', expect.anything(), [
      expect.objectContaining({ offerGroupId: 'side', recipeRevisionId: 'r' }),
      expect.objectContaining({ offerGroupId: 'main', recipeRevisionId: 'new', portions: 5 })
    ]);
  });
  it('uses a confirmed per-meal preference and restores protection when a whole-group draft is removed', () => {
    const { fixture, component } = create(); const current = partialState();
    current.manualScenarioSelections = [{ offerGroupId: 'main', preference: 0 }];
    fixture.componentRef.setInput('recipes', [{ revisionId: 'new', name: 'Alternative' }]);
    fixture.componentRef.setInput('togetherPreference', 2); fixture.componentRef.setInput('state', current); fixture.detectChanges();
    component.openConflictEditor(); expect(component.conflictScopes()[0].kind).toBe('whole');
    component.addConflictSolution(); expect(component.protectedSolutions()).toHaveLength(0);
    component.remove(component.editableDrafts()[0].id); expect(component.protectedSolutions()).toHaveLength(1);
  });
  it('restores plan-following for a changed automatic card and removes a persisted own decision', () => {
    const { fixture, component, api } = create();
    component.changeRecipe(component.drafts()[0], 'changed'); component.followPlan(component.drafts()[0]);
    expect(component.drafts()[0].recipeRevisionId).toBe('r'); expect(component.hasUnsavedChanges()).toBe(false);
    const current = structuredClone(state); current.supply!.solutions[0].source = 1; current.manualSupplySolutions = structuredClone(current.supply!.solutions);
    fixture.componentRef.setInput('state', current); fixture.detectChanges();
    component.followPlan(component.drafts()[0]); expect(component.drafts()).toHaveLength(0);
    component.saveOwnSolution(); expect(api.saveSupply).toHaveBeenCalledWith('camp', expect.anything(), [], [], true);
    component.reset(); expect(api.saveSupply).toHaveBeenCalledWith('camp', expect.anything(), [], [], false);
  });
  it('resets own recipes while preserving the saved preference and retries with the same preference after failure', () => {
    const { fixture, component, api } = create();
    const current = { ...structuredClone(state), completeOwnSupply: true, manualScenarioSelections: [{ offerGroupId: 'main', preference: 2 as const }] };
    fixture.componentRef.setInput('state', current); fixture.detectChanges();
    api.saveSupply.mockReturnValueOnce(throwError(() => ({ status: 409 })));
    component.reset(true);
    expect(api.saveSupply).toHaveBeenLastCalledWith('camp', expect.anything(), [], [{ offerGroupId: 'main', preference: 2 }], false);
    expect(component.scenarioSelections()).toEqual(current.manualScenarioSelections);
    component.reset(true);
    expect(api.saveSupply).toHaveBeenLastCalledWith('camp', expect.anything(), [], [{ offerGroupId: 'main', preference: 2 }], false);
  });
  it('persists a direction without persisting its deterministic recipe preview as an own solution', () => {
    const { fixture, component, api } = create();
    const recipe = structuredClone(state.supply!.solutions[0]);
    const choice: CommonSupplyChoice = { offerGroupId: null, requirementGroupKeys: ['g'], togetherOptions: [recipe], separateSolutions: [recipe],
      togetherScenario: { solutions: [recipe], decisions: [], problems: [] }, separateScenario: { solutions: [recipe], decisions: [], problems: [] } };
    fixture.componentRef.setInput('state', { ...structuredClone(state), supply: { solutions: [], candidates: [], problems: [], preferenceOptions: [choice] } }); fixture.detectChanges();
    component.selectScenario(choice, 2); expect(component.drafts()).toHaveLength(1);
    expect(component.isManual(component.drafts()[0])).toBe(false);
    component.save(true);
    expect(api.saveSupply).toHaveBeenCalledWith('camp', expect.anything(), [], [{ offerGroupId: null, preference: 2 }]);
  });
  it('keeps a confirmed nested plan alternative while resetting genuinely own recipes', () => {
    const { fixture, component, api } = create();
    const chosen: SupplySolution = { ...structuredClone(state.supply!.solutions[0]), source: 1, recipeRevisionId: 'plan-alternative' };
    const own: SupplySolution = { ...structuredClone(chosen), id: 'own', recipeRevisionId: 'own-recipe', offerGroupId: 'other-offer' };
    const current = { ...structuredClone(state), manualScenarioSelections: [{ offerGroupId: null, preference: 0 as const }],
      manualSupplySolutions: [chosen, own], supply: { solutions: [chosen, own], candidates: [], problems: [],
        preferenceOptions: [{ offerGroupId: null, requirementGroupKeys: ['g'], togetherOptions: [], separateSolutions: [],
          togetherScenario: { solutions: [], decisions: [{ id: 'nested', offerGroupId: null, requirementGroupKeys: ['g'], together: true,
            options: [{ ...chosen, source: 0 }, { ...chosen, recipeRevisionId: 'another-plan-alternative', source: 0 }] }], problems: [] } }] } };
    fixture.componentRef.setInput('state', current); fixture.detectChanges(); component.reset(true);
    expect(api.saveSupply).toHaveBeenCalledWith('camp', expect.anything(), [chosen], [{ offerGroupId: null, preference: 0 }], false);
  });
  it('preserves an unsaved decision across an overview refresh and blocks concurrent writes', () => {
    const { fixture, component, api } = create(); component.drafts()[0].portions = 8;
    fixture.componentRef.setInput('state', { ...structuredClone(state), supplyVersion: 5 }); fixture.detectChanges();
    expect(component.drafts()[0].portions).toBe(8);
    fixture.componentRef.setInput('externalBusy', true); fixture.detectChanges(); component.save(); component.verify(0);
    expect(api.saveSupply).not.toHaveBeenCalled(); expect(api.verifySupply).not.toHaveBeenCalled();
    fixture.componentRef.setInput('externalBusy', false); fixture.detectChanges(); component.save();
    expect(api.saveSupply).toHaveBeenCalledWith('camp', expect.objectContaining({ supplyVersion: 4 }), expect.anything());
  });
  it('preserves decisions when closing is rejected and restores them on accepted discard', () => {
    const { component } = create(); component.drafts()[0].portions = 9;
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    component.closeEditor(); expect(component.drafts()[0].portions).toBe(9);
    confirm.mockReturnValue(true); component.closeEditor();
    expect(component.drafts()[0].portions).toBe(2); expect(component.hasUnsavedChanges()).toBe(false);
    confirm.mockRestore();
  });
  it('confirms complete supply without an input or a deviation reason', () => {
    const { fixture, component, api } = create(); component.openVerification(); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.verification-dialog textarea')).toBeNull();
    expect(fixture.nativeElement.querySelector('.verification-dialog[open]')).toBeNull();
    expect(api.verifySupply).toHaveBeenCalledTimes(1);
    expect(api.verifySupply).toHaveBeenCalledWith('camp', expect.anything(), 0, '');
  });
  it('verifies independently of edit permission and allows an explicit deviation reason', () => {
    const { fixture, component, api } = create(); fixture.componentRef.setInput('disabled', true); fixture.detectChanges();
    fixture.componentRef.setInput('state', partialState()); fixture.detectChanges();
    component.openVerification(); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.verification-dialog[open]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.verification-dialog textarea')).not.toBeNull();
    component.verify(1); expect(api.verifySupply).not.toHaveBeenCalled();
    component.reason = 'Manuell geprüft'; component.verify(1);
    expect(api.verifySupply).toHaveBeenCalledWith('camp', expect.anything(), 1, 'Manuell geprüft');
    fixture.componentRef.setInput('frozen', true); fixture.detectChanges(); component.verify(0);
    expect(api.verifySupply).toHaveBeenCalledTimes(1);
    component.save(); expect(api.saveSupply).not.toHaveBeenCalled();
  });
  function askChoice(): CommonSupplyChoice {
    const base: SupplySolution = { id: 'standard', recipeRevisionId: 'r', offerGroupId: 'offer', portions: 2,
      source: 0, allocations: [{ requirementGroupKey: 'standard', portions: 2 }], positionSelections: {} };
    const adapted: SupplySolution = { ...structuredClone(base), id: 'adapted',
      allocations: [{ requirementGroupKey: 'allergy', portions: 2 }], positionSelections: { path: { variantKey: 'free', replacementRuleId: null } } };
    return { offerGroupId: 'offer', requirementGroupKeys: ['standard', 'allergy'], separateSolutions: [base, adapted],
      togetherOptions: [{ ...structuredClone(adapted), id: 'together', portions: 4, allocations: [...base.allocations, ...adapted.allocations] }] };
  }
  it('offers a compact exclusive choice of confirmed recipe alternatives and preserves other allocations', () => {
    const { fixture, component, api } = create();
    const candidate = (recipe: string): SupplySolution => ({ id: recipe, recipeRevisionId: recipe, offerGroupId: 'main', portions: 2, source: 0,
      allocations: [{ requirementGroupKey: 'g', portions: 2 }], positionSelections: {} });
    const first = candidate('alternative-a'), second = candidate('alternative-b'), partial = candidate('standard');
    const shared = { ...candidate('old'), id: 'shared', portions: 6, source: 1, allocations: [{ requirementGroupKey: 'g', portions: 2 }, { requirementGroupKey: 'other', portions: 4 }] };
    fixture.componentRef.setInput('offers', [{ id: 'main', name: 'Hauptspeise', alternativeRevisionIds: ['alternative-a', 'alternative-b'] }]);
    fixture.componentRef.setInput('recipes', ['alternative-a', 'alternative-b'].map(revisionId => ({ revisionId, name: revisionId })));
    fixture.componentRef.setInput('state', { ...structuredClone(state), supply: { solutions: [structuredClone(state.supply!.solutions[0]), shared], candidates: [first, second, partial],
      problems: [{ code: 4, category: 0, requirementGroupKey: 'g', referenceId: null, positionPath: null }] } }); fixture.detectChanges();
    const group = component.conflicts()[0];
    expect(component.alternativeChoices(group)[0].candidates).toEqual([first, second]);
    expect(fixture.nativeElement.querySelectorAll('.alternative-choice input[type=radio]')).toHaveLength(2);
    expect(fixture.nativeElement.querySelector('dialog[open]')).toBeNull();
    component.selectAlternative('g', first); component.selectAlternative('g', second);
    expect(component.drafts().filter(value => value.offerGroupId === 'main' && value.allocations.some(value => value.requirementGroupKey === 'g'))).toHaveLength(1);
    expect(component.drafts().find(value => value.id === 'shared')!.allocations).toEqual([{ requirementGroupKey: 'other', portions: 4 }]);
    expect(component.drafts().find(value => value.id === 'shared')!.portions).toBe(4);
    expect(component.drafts().find(value => value.id === 's')!.source).toBe(0);
    expect(api.saveSupply).not.toHaveBeenCalled(); component.save();
    expect(api.saveSupply).toHaveBeenCalledWith('camp', expect.anything(), expect.arrayContaining([expect.objectContaining({ recipeRevisionId: 'alternative-b', source: 1 })]));
    expect(api.saveSupply).not.toHaveBeenCalledWith('camp', expect.anything(), expect.arrayContaining([expect.objectContaining({ recipeRevisionId: 'alternative-a' })]));
  });
  it('does not classify partial proposals or stale choices as an alternative selection', () => {
    const { fixture, component } = create();
    fixture.componentRef.setInput('offers', [{ id: 'main', name: 'Hauptspeise', alternativeRevisionIds: ['a', 'b'] }]);
    const a = { ...structuredClone(state.supply!.solutions[0]), offerGroupId: 'main', recipeRevisionId: 'a' };
    const b = { ...structuredClone(a), recipeRevisionId: 'b' };
    fixture.componentRef.setInput('state', { ...structuredClone(state), status: 1, supply: { solutions: [], candidates: [a, b], problems: [{ code: 4, requirementGroupKey: 'g' }] } }); fixture.detectChanges();
    component.selectAlternative('g', a); expect(component.drafts()).toEqual([]);
    fixture.componentRef.setInput('state', { ...structuredClone(state), supply: { solutions: [], candidates: [a, b], problems: [{ code: 2, requirementGroupKey: 'g' }] } }); fixture.detectChanges();
    expect(component.alternativeChoices(component.conflicts()[0])).toEqual([]);
  });
  for (const together of [false, true]) {
    it(`uses one decision and one stored recipe for shared coverage (AlwaysTogether: ${together})`, () => {
      const { fixture, component, api } = create();
      const single = (recipe: string, key: string, portions: number): SupplySolution => ({ id: recipe + key, recipeRevisionId: recipe, offerGroupId: 'main', portions, source: 0, positionSelections: {}, allocations: [{ requirementGroupKey: key, portions }] });
      const candidates = ['a', 'b'].flatMap(recipe => [single(recipe, 'g1', 2), single(recipe, 'g2', 3)]);
      if (together) for (const recipe of ['a', 'b']) candidates.push({ ...single(recipe, 'g1', 15), allocations: [{ requirementGroupKey: 'standard', portions: 10 }, { requirementGroupKey: 'g1', portions: 2 }, { requirementGroupKey: 'g2', portions: 3 }] });
      fixture.componentRef.setInput('offers', [{ id: 'main', name: 'Hauptspeise', alternativeRevisionIds: ['a', 'b'] }]);
      fixture.componentRef.setInput('togetherPreference', together ? 0 : 1);
      fixture.componentRef.setInput('state', { ...structuredClone(state), supply: { candidates, solutions: [single('standard-recipe', 'standard', 10)],
        problems: ['g1', 'g2'].map(requirementGroupKey => ({ code: 4, category: 0, requirementGroupKey, referenceId: null, positionPath: null })) } }); fixture.detectChanges();
      expect(fixture.nativeElement.querySelectorAll('.alternative-choice')).toHaveLength(1);
      expect(fixture.nativeElement.querySelectorAll('.solution-list .solution-card')).toHaveLength(together ? 0 : 1);
      expect(fixture.nativeElement.querySelector('.empty-solutions')).toBeNull();
      expect(!!fixture.nativeElement.querySelector('.replacement-hint')).toBe(together);
      const decision = component.decisions()[0]; component.selectDecision(decision, decision.candidates[0]); component.save();
      expect(api.saveSupply).toHaveBeenCalledWith('camp', expect.anything(), [expect.objectContaining({ recipeRevisionId: 'a', portions: together ? 15 : 5,
        allocations: expect.arrayContaining([{ requirementGroupKey: 'g1', portions: 2 }, { requirementGroupKey: 'g2', portions: 3 }]) })]);
      expect(component.drafts().some(value => value.recipeRevisionId === 'standard-recipe')).toBe(!together);
    });
  }
  it('displays and saves old split manual entries as one recipe decision', () => {
    const { fixture, component, api } = create();
    const first = { ...structuredClone(state.supply!.solutions[0]), source: 1 };
    const second = { ...structuredClone(first), id: 'second', portions: 3, allocations: [{ requirementGroupKey: 'other', portions: 3 }] };
    fixture.componentRef.setInput('state', { ...structuredClone(state), manualSupplySolutions: [first, second] }); fixture.detectChanges();
    expect(component.drafts()).toHaveLength(1); expect(component.hasUnsavedChanges()).toBe(false); component.save();
    expect(api.saveSupply).toHaveBeenCalledWith('camp', expect.anything(), [expect.objectContaining({ portions: 5, source: 1 })]);
  });
  it('requests recalculation only after the selected alternative has been saved successfully', () => {
    const { fixture, component, api } = create();
    const candidate = (recipe: string): SupplySolution => ({ id: recipe, recipeRevisionId: recipe, offerGroupId: 'main', portions: 2, source: 0, positionSelections: {}, allocations: [{ requirementGroupKey: 'g', portions: 2 }] });
    fixture.componentRef.setInput('offers', [{ id: 'main', name: 'Hauptspeise', alternativeRevisionIds: ['a', 'b'] }]);
    fixture.componentRef.setInput('state', { ...structuredClone(state), supply: { solutions: [], candidates: [candidate('a'), candidate('b')], problems: [{ code: 4, requirementGroupKey: 'g' }] } }); fixture.detectChanges();
    const request = new Subject<{ id: string; version: null }>(); api.saveSupply.mockReturnValue(request);
    const saved = vi.spyOn(component.saved, 'emit'); const decision = component.decisions()[0];
    component.selectDecision(decision, decision.candidates[0]); fixture.detectChanges();
    expect(api.saveSupply).not.toHaveBeenCalled(); expect(saved).not.toHaveBeenCalled();
    const confirmButton = Array.from(fixture.nativeElement.querySelectorAll('.alternative-actions button') as NodeListOf<HTMLButtonElement>).find(button => button.textContent?.includes('Auswahl bestätigen'))!;
    confirmButton.click();
    expect(api.saveSupply).toHaveBeenCalledOnce(); expect(saved).not.toHaveBeenCalled(); expect(component.busy()).toBe(true);
    request.next({ id: 'state', version: null }); request.complete();
    expect(saved).toHaveBeenCalledWith(true); expect(component.hasUnsavedDecisions()).toBe(false);
    api.saveSupply.mockReturnValue(throwError(() => ({ status: 409 })));
    saved.mockClear(); component.selectDecision(decision, decision.candidates[1]); component.save(true);
    expect(saved).not.toHaveBeenCalled(); expect(component.hasUnsavedDecisions()).toBe(true);
  });
  it('offers two explicit Ask paths without materializing either before selection', () => {
    const { fixture, component } = create();
    fixture.componentRef.setInput('state', { ...state, supply: { solutions: [], candidates: [], problems: [], commonSupplyChoices: [askChoice()] } });
    fixture.detectChanges();
    expect(component.drafts()).toEqual([]);
    expect(fixture.nativeElement.textContent).toContain('Gemeinsam versorgen');
    expect(fixture.nativeElement.textContent).toContain('Getrennt versorgen');
    const preview = fixture.nativeElement.querySelector('.common-choice') as HTMLElement;
    expect(preview.querySelectorAll('input[type=radio]')).toHaveLength(2);
    expect(preview.querySelector('input:checked')).toBeNull();
    expect(fixture.nativeElement.querySelector('dialog[open]')).toBeNull();
    expect(preview.textContent).toContain('4 Personen'); expect(preview.textContent).toContain('2 Personen');
  });
  it('confirms an inline Ask choice before saving and requesting recalculation', () => {
    const { fixture, component, api } = create(); const choice = askChoice();
    fixture.componentRef.setInput('state', { ...structuredClone(state), supply: { solutions: [], candidates: [], problems: [], commonSupplyChoices: [choice] } }); fixture.detectChanges();
    const saved = vi.spyOn(component.saved, 'emit');
    const radios = fixture.nativeElement.querySelectorAll('.common-choice input') as NodeListOf<HTMLInputElement>;
    radios[0].click(); fixture.detectChanges();
    expect(api.saveSupply).not.toHaveBeenCalled(); expect(component.drafts()).toHaveLength(1);
    radios[1].click(); fixture.detectChanges(); expect(component.drafts()).toHaveLength(2);
    const button = fixture.nativeElement.querySelector('.common-choice .alternative-actions button') as HTMLButtonElement;
    button.click(); expect(api.saveSupply).toHaveBeenCalledOnce(); expect(saved).toHaveBeenCalledWith(true);
  });
  it('persists the selected API scenario and exposes its pending variant choices without guessing suitability', () => {
    const { fixture, component, api } = create();
    const option = (variant: string): SupplySolution => ({ ...structuredClone(state.supply!.solutions[0]), offerGroupId: 'offer', positionSelections: { path: { variantKey: variant, replacementRuleId: null } } });
    const choice: CommonSupplyChoice = { offerGroupId: 'offer', requirementGroupKeys: ['g'], togetherOptions: [], separateSolutions: [],
      togetherScenario: { solutions: [], decisions: [{ id: 'together', offerGroupId: 'offer', requirementGroupKeys: ['g'], options: [option('a'), option('b')], together: true }], problems: [] },
      separateScenario: { solutions: [option('standard')], decisions: [], problems: [] } };
    fixture.componentRef.setInput('state', { ...structuredClone(state), supply: { solutions: [], candidates: [], problems: [], decisions: [], commonSupplyChoices: [choice] } }); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.common-choice input:checked')).toBeNull();
    component.selectScenario(choice, 0); fixture.detectChanges();
    expect(component.decisions()).toHaveLength(1); expect(fixture.nativeElement.querySelectorAll('.alternative-choice input[type=radio]')).toHaveLength(4);
    const decision = component.decisions()[0]; component.selectDecision(decision, decision.candidates[1]); component.save(true);
    expect(api.saveSupply).toHaveBeenCalledWith('camp', expect.anything(), [expect.objectContaining({ positionSelections: { path: { variantKey: 'b', replacementRuleId: null } } })], [{ offerGroupId: 'offer', preference: 0 }]);
    fixture.componentRef.setInput('state', { ...structuredClone(state), manualScenarioSelections: [{ offerGroupId: 'offer', preference: 0 }],
      manualSupplySolutions: structuredClone(component.drafts()), supply: { solutions: structuredClone(component.drafts()), candidates: [], problems: [], decisions: [], commonSupplyChoices: [choice] } }); fixture.detectChanges();
    expect(component.hasUnsavedChanges()).toBe(false); expect(component.scenarioSelected(choice, 0)).toBe(true);
    expect(component.visibleSolutions()).toHaveLength(1);
    expect(fixture.nativeElement.querySelector('.supply-normal .common-choice')).toBeNull();
    component.openScenarioEditor(); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('dialog[open] .common-choice')).not.toBeNull();
    component.closeScenarioEditor();
    component.reset(); expect(api.saveSupply).toHaveBeenLastCalledWith('camp', expect.anything(), [expect.objectContaining({ positionSelections: { path: { variantKey: 'b', replacementRuleId: null } } })], [{ offerGroupId: 'offer', preference: 0 }], false);
  });
  it('removes old scenario decisions when switching to a direction with a unique solution', () => {
    const { fixture, component } = create(); const solution = structuredClone(state.supply!.solutions[0]);
    const choice: CommonSupplyChoice = { offerGroupId: null, requirementGroupKeys: ['g'], togetherOptions: [], separateSolutions: [solution],
      togetherScenario: { solutions: [], decisions: [], problems: [] }, separateScenario: { solutions: [solution], decisions: [], problems: [] } };
    fixture.componentRef.setInput('state', { ...structuredClone(state), manualScenarioSelections: [{ offerGroupId: null, preference: 0 }], supply: {
      solutions: [], candidates: [], problems: [], decisions: [{ id: 'old', offerGroupId: null, requirementGroupKeys: ['g'], options: [solution], together: true }], commonSupplyChoices: [choice] } }); fixture.detectChanges();
    expect(component.decisions()).toHaveLength(1); component.selectScenario(choice, 2);
    expect(component.decisions()).toHaveLength(0); expect(component.hasUnsavedDecisions()).toBe(true);
  });
  for (const initial of [0, 2]) {
    it(`allows all preferences under further adjustments for a non-Ask default (${initial})`, () => {
      const { fixture, component, api } = create(); const solution = structuredClone(state.supply!.solutions[0]);
      const choice: CommonSupplyChoice = { offerGroupId: null, requirementGroupKeys: ['g'], togetherOptions: [solution], separateSolutions: [solution],
        togetherScenario: { solutions: [solution], decisions: [], problems: [] }, separateScenario: { solutions: [solution], decisions: [], problems: [] } };
      fixture.componentRef.setInput('togetherPreference', initial);
      fixture.componentRef.setInput('state', { ...structuredClone(state), supply: { solutions: [solution], candidates: [], problems: [], decisions: [], commonSupplyChoices: [], preferenceOptions: [choice] } }); fixture.detectChanges();
      expect(fixture.nativeElement.querySelector('.supply-normal .common-choice')).toBeNull();
      component.openScenarioEditor(); fixture.detectChanges();
      expect(fixture.nativeElement.querySelectorAll('dialog[open] .common-choice input[type=radio]')).toHaveLength(3);
      expect(component.scenarioSelected(choice, initial as 0 | 2)).toBe(true);
      component.selectScenario(choice, 1); expect(component.preferenceLabel()).toBe('Fragen'); component.save(true);
      expect(api.saveSupply).toHaveBeenCalledWith('camp', expect.anything(), [], [{ offerGroupId: null, preference: 1 }]);
    });
  }
  it('keeps separate choices independent while merging the same selected recipe on save', () => {
    const { fixture, component, api } = create();
    const candidates = ['g1', 'g2'].flatMap(key => ['a', 'b'].map(recipe => ({ id: key + recipe, recipeRevisionId: recipe, offerGroupId: 'main', portions: 2, source: 0,
      allocations: [{ requirementGroupKey: key, portions: 2 }], positionSelections: {} })));
    fixture.componentRef.setInput('offers', [{ id: 'main', name: 'Hauptspeise', alternativeRevisionIds: ['a', 'b'] }]); fixture.componentRef.setInput('togetherPreference', 2);
    fixture.componentRef.setInput('state', { ...structuredClone(state), supply: { solutions: [], candidates, problems: ['g1', 'g2'].map(requirementGroupKey => ({ code: 4, requirementGroupKey })) } }); fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('.alternative-choice')).toHaveLength(2);
    const decisions = component.decisions(); component.selectDecision(decisions[0], decisions[0].candidates[0]);
    expect(component.hasSelectedAlternative(decisions[1].candidates)).toBe(false);
    component.selectDecision(decisions[1], decisions[1].candidates[0]); component.save(true);
    expect(api.saveSupply).toHaveBeenCalledWith('camp', expect.anything(), [expect.objectContaining({ recipeRevisionId: 'a', portions: 4, allocations: expect.any(Array) })]);
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
    expect(api.saveSupply).toHaveBeenCalledWith('camp', expect.anything(), [], [], false);
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
    expect(fixture.nativeElement.textContent).toContain('30 g je 3 Personen');
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
