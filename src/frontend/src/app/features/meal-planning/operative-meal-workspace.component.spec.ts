import { TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { vi } from 'vitest';
import { CookingUnitMeal, MealPlanningApiService, MealPlanningOverview } from './meal-planning-api.service';
import { OperativeMealWorkspaceComponent } from './operative-meal-workspace.component';

describe('OperativeMealWorkspaceComponent', () => {
  const state: CookingUnitMeal = { id: 'state', cookingUnitId: 'unit', campMealId: 'meal', subscriptionState: 0, demandOverride: null,
    calculatedDemand: 12, effectiveDemand: 12, calculatedAtUtc: '2026-11-01T10:00:00Z', status: 0, mealPlanId: 'plan', mealPlanVersion: 1,
    structureOverrideNodeIds: [], offerTargets: [], recipeChoices: [], warnings: [], demandBasis: 2,
    supply: { solutions: [], candidates: [], problems: [] } };
  const data: MealPlanningOverview = { campId: 'camp', cookingUnits: [{ id: 'unit', campId: 'camp', name: 'Küche', sortOrder: 0, groupId: null, standardMealPlanId: 'plan', defaultStructureNodeIds: ['root'] }], cookingUnitGroups: [],
    meals: [{ id: 'later', mealTypeId: 'type', mealTypeName: 'Abendessen', date: '2027-07-01', sortOrder: 1, isActive: true, isInsideCampPeriod: true, changeVersion: 0 },
      { id: 'meal', mealTypeId: 'type', mealTypeName: 'Mittagessen', date: '2027-07-01', sortOrder: 0, isActive: true, isInsideCampPeriod: true, changeVersion: 0 },
      { id: 'inactive', mealTypeId: 'type', mealTypeName: 'Entfällt', date: '2027-07-02', isActive: false, isInsideCampPeriod: true, changeVersion: 0 }],
    mealPlans: [{ id: 'plan', name: 'Standardplan', sortOrder: 0, version: 1, missingActiveMealCount: 0 }], mealPlanDocuments: [{ id: 'plan', campId: 'camp', name: 'Standardplan', sortOrder: 0, version: 1,
      offerGroups: ['main', 'side'].map((id, sortOrder) => ({ id, campMealId: 'meal', name: id, sortOrder, entries: [{ id: id + '-recipe', recipeRevisionId: 'rice', isStandard: true, displayName: null, role: null, note: null, sortOrder: 0 }] })) }],
    operationalMeals: [state], structureNodes: [{ id: 'root', parentId: null, name: 'Lager' }, { id: 'leaf', parentId: 'root', name: 'Gruppe' }],
    recipeOptions: [{ revisionId: 'rice', recipeId: 'recipe', name: 'Reis', revisionNumber: 1, isPortionBased: true, suggestedRole: null }], coverageWarnings: [] };
  function create() {
    const api = { overview: vi.fn(() => of(structuredClone(data))), calculate: vi.fn(() => of({})), configureMeal: vi.fn(() => of({})), resetStructure: vi.fn(() => of({})), requirementCatalog: vi.fn(() => throwError(() => ({ status: 403 }))) };
    TestBed.configureTestingModule({ imports: [OperativeMealWorkspaceComponent], providers: [{ provide: MealPlanningApiService, useValue: api }] });
    const fixture = TestBed.createComponent(OperativeMealWorkspaceComponent);
    fixture.componentRef.setInput('data', structuredClone(data)); fixture.componentRef.setInput('unitId', 'unit'); fixture.componentRef.setInput('mealId', 'meal'); fixture.detectChanges();
    for (const dialog of fixture.nativeElement.querySelectorAll('dialog') as NodeListOf<HTMLDialogElement>) {
      dialog.close = vi.fn(() => dialog.removeAttribute('open')); dialog.showModal = vi.fn(() => dialog.setAttribute('open', ''));
    }
    return { fixture, component: fixture.componentInstance, api };
  }
  afterEach(() => vi.restoreAllMocks());
  it('shows one unit with ordered meals, read-only offers without a redundant demand strip', () => {
    const { fixture, component, api } = create();
    expect(component.meals().map(meal => meal.id)).toEqual(['meal', 'later']);
    expect(fixture.nativeElement.querySelectorAll('.recipe-card')).toHaveLength(2);
    expect(fixture.nativeElement.querySelector('.meal-detail input')).toBeNull();
    expect(fixture.nativeElement.querySelector('.demand-strip')).toBeNull();
    expect(api.requirementCatalog).not.toHaveBeenCalled();
    expect(api.calculate).not.toHaveBeenCalled();
  });
  it('opens unrestricted editing only from further adjustments and protects pending settings', () => {
    const { fixture, component } = create();
    const open = vi.spyOn(component.supply()!, 'openEditor').mockImplementation(() => {});
    expect(fixture.nativeElement.querySelector('.supply-normal').textContent).not.toContain('Versorgungsentscheidungen bearbeiten');
    component.draft.demandOverride = 20; component.editSupply(); expect(open).not.toHaveBeenCalled();
    component.discardChanges(); component.openSettings(); component.editSupply(); expect(open).toHaveBeenCalledOnce();
    expect(fixture.nativeElement.querySelector('dialog[open]')).toBeNull();
  });
  it('shows calculation, supply and verification before the offers and opens verification from the header', () => {
    const { fixture, component } = create();
    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelector('[aria-label="Vorherige Mahlzeit"]')).toBeNull();
    expect(host.querySelector('[aria-label="Nächste Mahlzeit"]')).toBeNull();
    const summary = host.querySelector('.meal-status-overview')!;
    expect(summary.textContent).toContain('Berechnung'); expect(summary.textContent).toContain('Verifikation');
    expect(summary.compareDocumentPosition(host.querySelector('.planned-offers')!) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
    expect(host.querySelector('.verification-strip')).toBeNull();
    expect(host.querySelector('.meal-heading button')).toBeNull();
    expect(host.querySelectorAll('.status-actions button')).toHaveLength(3);
    expect(host.querySelector('.meal-status-row button')).toBeNull();
    expect(host.querySelector('.status-actions')?.textContent).toContain('Neu berechnen');
    const open = vi.spyOn(component.supply()!, 'openVerification').mockImplementation(() => {});
    const button = [...host.querySelectorAll<HTMLButtonElement>('.status-actions button')].find(value => value.textContent?.includes('Verifizieren'))!;
    button.click(); expect(open).toHaveBeenCalledOnce();
    const updated = structuredClone(data);
    updated.operationalMeals[0].verification = { kind: 0, verifiedAt: '2026-11-01T11:00:00Z' } as NonNullable<CookingUnitMeal['verification']>;
    fixture.componentRef.setInput('data', updated); fixture.detectChanges();
    expect(host.querySelector('.verification-status.success')).not.toBeNull();
    updated.operationalMeals[0] = { ...updated.operationalMeals[0], verificationStale: true };
    fixture.componentRef.setInput('data', structuredClone(updated)); fixture.detectChanges();
    expect(host.querySelector('.verification-status.warning')?.textContent).toContain('Veraltet');
    fixture.componentRef.setInput('frozen', true); fixture.detectChanges(); expect(button.disabled).toBe(true);
  });
  it('shows standard recipes first and keeps each component alternative list collapsed until opened', () => {
    const { fixture, api } = create(); const updated = structuredClone(data);
    const main = updated.mealPlanDocuments[0].offerGroups[0];
    main.entries.push({ ...main.entries[0], id: 'alternative', recipeRevisionId: 'alternative-recipe', isStandard: false, sortOrder: 1, note: 'Alternative note' });
    updated.recipeOptions.push({ ...updated.recipeOptions[0], revisionId: 'alternative-recipe', name: 'Alternative recipe' });
    fixture.componentRef.setInput('data', updated); fixture.detectChanges();
    const host = fixture.nativeElement as HTMLElement;
    expect(host.querySelectorAll('.standard-recipe')).toHaveLength(2);
    const disclosure = host.querySelector<HTMLDetailsElement>('.offer-alternatives')!;
    expect(disclosure.open).toBe(false);
    expect(disclosure.querySelector('summary')!.textContent).toContain('1 Alternative');
    expect(disclosure.querySelector('.alternative-recipes')!.textContent).toContain('Alternative recipe');
    expect(disclosure.querySelector('.alternative-recipes')!.textContent).toContain('Alternative note');
    disclosure.open = true; fixture.detectChanges();
    expect(host.querySelectorAll('.standard-recipe')).toHaveLength(2);
    expect(api.configureMeal).not.toHaveBeenCalled(); expect(api.calculate).not.toHaveBeenCalled();
    fixture.componentRef.setInput('mealId', 'later'); fixture.detectChanges();
    expect(host.querySelector('.offer-alternatives')).toBeNull();
  });
  it('protects pending settings on navigation and recalculation, then discards explicitly', () => {
    const { component, api } = create(); component.draft.demandOverride = 20;
    const navigate = vi.spyOn(component.navigate, 'emit'); const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    component.selectMeal('later'); component.calculate();
    expect(navigate).not.toHaveBeenCalled(); expect(api.calculate).not.toHaveBeenCalled();
    expect(component.draft.demandOverride).toBe(20);
    confirm.mockReturnValue(true); component.selectMeal('later');
    expect(navigate).toHaveBeenCalledWith({ section: 'units', unitId: 'unit', mealId: 'later' }); expect(component.hasPendingEdits()).toBe(false);
  });
  it('keeps settings on errors and sends versioned supply adjustments with the configuration', () => {
    const { component, api } = create(); api.configureMeal.mockReturnValue(throwError(() => ({ error: { message: 'Versionskonflikt' } })));
    component.draft.demandOverride = 20; component.saveSettings();
    expect(component.error()).toContain('Versionskonflikt'); expect(component.draft.demandOverride).toBe(20);
    expect(api.configureMeal).toHaveBeenCalledWith('camp', 'unit', 'meal', { subscriptionState: 0, demandOverride: 20, structureOverrideNodeIds: [], offerTargets: [], recipeChoices: [], supplyAdjustments: { expectedVersion: 0, updatePreference: false, preferenceOverride: null, resetRecipes: false, retainRecipeSolutionIds: [], updateParticipantFilter: false, participantFilterOverride: null } });
    expect(api.calculate).not.toHaveBeenCalled();
  });
  it('stages resets without writing and cancels all changes including preference and structure', () => {
    const { fixture, component, api } = create(); component.openSettings();
    expect(fixture.nativeElement.querySelectorAll('.adjustment-section')).toHaveLength(4);
    component.draft.demandOverride = 14; component.draft.preference = '2'; component.useCustomStructure();
    component.stageRecipeReset(); expect(api.configureMeal).not.toHaveBeenCalled(); expect(api.resetStructure).not.toHaveBeenCalled();
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(true); component.closeSettings();
    expect(component.draft.demandOverride).toBeNull(); expect(component.draft.preference).toBe('standard');
    expect(component.draft.resetOwnRecipes).toBe(false); expect(component.draft.useStructureOverride).toBe(false);
    expect(api.configureMeal).not.toHaveBeenCalled(); confirm.mockRestore();
  });
  it('sends an all-reset only on save and keeps mixed component preferences unless explicitly changed', () => {
    const { fixture, component, api } = create(); const updated = structuredClone(data);
    updated.operationalMeals[0].manualScenarioSelections = [{ offerGroupId: 'main', preference: 0 }, { offerGroupId: 'side', preference: 2 }];
    updated.operationalMeals[0].supplyVersion = 9;
    fixture.componentRef.setInput('data', updated); fixture.detectChanges(); expect(component.draft.preference).toBe('mixed');
    component.resetAllAdjustments(); expect(api.configureMeal).not.toHaveBeenCalled(); component.saveSettings();
    expect(api.configureMeal).toHaveBeenCalledWith('camp', 'unit', 'meal', expect.objectContaining({ structureOverrideNodeIds: [], demandOverride: null,
      supplyAdjustments: { expectedVersion: 9, updatePreference: true, preferenceOverride: null, resetRecipes: true, retainRecipeSolutionIds: [], updateParticipantFilter: true, participantFilterOverride: null } }));
  });
  it('disables component overrides in automatic mode and accepts manually typed fractions', async () => {
    const { fixture, component, api } = create();
    const inputs = () => [...fixture.nativeElement.querySelectorAll('.component-targets input')] as HTMLInputElement[];
    await fixture.whenStable(); expect(inputs()[0].disabled).toBe(true);
    component.setManualDemand(true); fixture.changeDetectorRef.markForCheck(); fixture.detectChanges(); await fixture.whenStable(); expect(inputs()[0].disabled).toBe(false); expect(inputs()[0].step).toBe('1');
    component.draft.demandOverride = 12.5; component.setTarget('main', 4.5); component.saveSettings();
    expect(api.configureMeal).toHaveBeenCalledWith('camp', 'unit', 'meal', expect.objectContaining({ demandOverride: 12.5 }));
    component.setManualDemand(false); expect(component.draft.offerTargets).toEqual([]); expect(component.draft.demandOverride).toBeNull();
  });
  it('shows preference choices only after override and recalculates after a saved recipe reset', async () => {
    const { fixture, component, api } = create();
    await fixture.whenStable(); expect(fixture.nativeElement.querySelector('.preference-settings')).toBeNull(); component.setPreferenceOverride(true); fixture.changeDetectorRef.markForCheck(); fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.nativeElement.querySelectorAll('.preference-settings input')).toHaveLength(3);
    component.stageRecipeReset(); component.saveSettings(); expect(api.configureMeal).toHaveBeenCalledOnce(); expect(api.calculate).toHaveBeenCalledWith('camp', 'unit', 'meal');
  });
  it('guards asynchronous calculation and refreshes without automatically verifying', () => {
    const { component, api } = create(); const request = new Subject<object>(); api.calculate.mockReturnValue(request);
    const navigate = vi.spyOn(component.navigate, 'emit'); const changed = vi.spyOn(component.changed, 'emit');
    component.calculate(); component.selectMeal('later'); component.calculate();
    expect(api.calculate).toHaveBeenCalledTimes(1); expect(navigate).not.toHaveBeenCalled();
    request.next({}); request.complete(); expect(changed).toHaveBeenCalledOnce(); expect(component.isBusy()).toBe(false);
  });
  it('recalculates stale and uncalculated active meals sequentially using a fresh overview', async () => {
    const { component, api } = create(); const fresh = structuredClone(data);
    fresh.operationalMeals[0].status = 1;
    fresh.operationalMeals.push({ ...structuredClone(state), id: 'later-state', campMealId: 'later', status: 1 });
    fresh.meals.push({ ...fresh.meals[0], id: 'new', date: '2027-07-02' }, { ...fresh.meals[0], id: 'outside', isInsideCampPeriod: false });
    api.overview.mockReturnValue(of(fresh));
    const first = new Subject<object>(); api.calculate.mockReturnValueOnce(first);
    const changed = vi.spyOn(component.changed, 'emit'), navigate = vi.spyOn(component.navigate, 'emit');
    const run = component.calculateAllOutdated(); await Promise.resolve();
    expect(api.calculate).toHaveBeenCalledTimes(1); expect(api.calculate).toHaveBeenNthCalledWith(1, 'camp', 'unit', 'meal');
    expect(component.batchProgress()).toEqual({ done: 0, total: 3 });
    component.selectMeal('later'); expect(navigate).not.toHaveBeenCalled();
    first.next({}); first.complete(); await run;
    expect(api.calculate).toHaveBeenCalledTimes(3);
    expect(api.calculate).toHaveBeenNthCalledWith(2, 'camp', 'unit', 'later'); expect(api.calculate).toHaveBeenNthCalledWith(3, 'camp', 'unit', 'new');
    expect(api.overview).toHaveBeenCalledTimes(2); expect(changed).toHaveBeenCalledWith({ data: fresh, message: '3 von 3 Mahlzeiten neu berechnet.' });
    expect(component.isBusy()).toBe(false); expect(component.batchProgress()).toBeNull();
    expect(api.configureMeal).not.toHaveBeenCalled();
  });
  it('skips current and freshly incomplete meals and does not touch another unit', async () => {
    const { component, api } = create(); const fresh = structuredClone(data);
    fresh.operationalMeals.push({ ...structuredClone(state), id: 'later-state', campMealId: 'later', status: 2 },
      { ...structuredClone(state), id: 'other-unit', cookingUnitId: 'other', status: 1 });
    api.overview.mockReturnValue(of(fresh)); const changed = vi.spyOn(component.changed, 'emit');
    await component.calculateAllOutdated(); expect(api.calculate).not.toHaveBeenCalled();
    expect(changed).toHaveBeenCalledWith({ data: fresh, message: 'Keine veralteten oder unberechneten Mahlzeiten in dieser Kocheinheit.' });
    expect(api.overview).toHaveBeenCalledOnce();
  });
  it('reports partial failure, continues other meals and never retries a failed calculation', async () => {
    const { component, api } = create(); const fresh = structuredClone(data); fresh.operationalMeals[0].status = 1;
    api.overview.mockReturnValue(of(fresh)); api.calculate.mockReturnValueOnce(throwError(() => ({ status: 422 })));
    const changed = vi.spyOn(component.changed, 'emit'); await component.calculateAllOutdated();
    expect(api.calculate).toHaveBeenCalledTimes(2); expect(component.error()).toContain('Mittagessen');
    expect(changed).toHaveBeenCalledWith({ data: fresh, message: '1 von 2 Mahlzeiten neu berechnet.' });
    expect(component.isBusy()).toBe(false);
  });
  it('guards bulk calculation for drafts, freeze and changed permissions', async () => {
    const { fixture, component, api } = create(); component.draft.demandOverride = 25;
    await component.calculateAllOutdated(); expect(api.overview).not.toHaveBeenCalled(); component.discardChanges();
    fixture.componentRef.setInput('frozen', true); fixture.detectChanges(); await component.calculateAllOutdated(); expect(api.overview).not.toHaveBeenCalled();
    fixture.componentRef.setInput('frozen', false); fixture.detectChanges();
    fixture.componentRef.setInput('disabled', true); fixture.detectChanges(); await component.calculateAllOutdated(); expect(api.overview).not.toHaveBeenCalled();
    fixture.componentRef.setInput('disabled', false); fixture.detectChanges();
    const fresh = structuredClone(data); fresh.operationalMeals[0].status = 1; api.overview.mockReturnValue(of(fresh));
    api.calculate.mockReturnValueOnce(throwError(() => ({ status: 403 }))); await component.calculateAllOutdated();
    expect(api.calculate).toHaveBeenCalledOnce(); expect(component.isBusy()).toBe(false);
    api.calculate.mockClear(); fresh.cookingUnits = []; await component.calculateAllOutdated();
    expect(api.calculate).not.toHaveBeenCalled(); expect(component.error()).toContain('nicht mehr verfügbar');
  });
  it('retains successful calculations when the final overview refresh fails', async () => {
    const { component, api } = create(); const fresh = structuredClone(data); fresh.operationalMeals[0].status = 1;
    api.overview.mockReturnValueOnce(of(fresh)).mockReturnValueOnce(throwError(() => ({ status: 500 })));
    await component.calculateAllOutdated(); expect(api.calculate).toHaveBeenCalledTimes(2);
    expect(component.error()).toContain('2 Mahlzeiten wurden berechnet.'); expect(component.isBusy()).toBe(false);
  });
  it('recalculates a saved selection before refreshing and reports a calculation failure as already saved', () => {
    const { component, api } = create(); const request = new Subject<object>(); api.calculate.mockReturnValue(request);
    component.supplySaved(true); expect(api.calculate).toHaveBeenCalledWith('camp', 'unit', 'meal');
    expect(api.overview).not.toHaveBeenCalled(); expect(component.isBusy()).toBe(true);
    request.next({}); request.complete(); expect(api.overview).toHaveBeenCalledOnce();
    api.calculate.mockReturnValue(throwError(() => ({ status: 500 }))); component.supplySaved(true);
    expect(component.error()).toContain('Die Auswahl ist gespeichert.'); expect(component.isBusy()).toBe(false);
    expect(api.calculate).toHaveBeenCalledTimes(2);
  });
  it('blocks writes while frozen and supports a draft reset without writing immediately', () => {
    const { fixture, component, api } = create(); component.draft.subscriptionState = 1; component.addRecipe(); component.resetPlan();
    expect(component.draft.recipeChoices).toEqual([]); expect(api.configureMeal).not.toHaveBeenCalled();
    fixture.componentRef.setInput('frozen', true); fixture.detectChanges(); component.calculate(); component.saveSettings();
    expect(api.configureMeal).not.toHaveBeenCalled(); expect(api.calculate).not.toHaveBeenCalled();
  });
  it('opens saved supply preferences or resets from further adjustments only with a clean settings draft', () => {
    const { component } = create();
    const open = vi.spyOn(component.supply()!, 'openScenarioEditor').mockImplementation(() => {});
    const reset = vi.spyOn(component.supply()!, 'reset').mockImplementation(() => {});
    component.draft.demandOverride = 25; component.changeSupplyMode(); component.changeSupplyMode(true);
    expect(open).not.toHaveBeenCalled(); expect(reset).not.toHaveBeenCalled();
    component.discardChanges(); component.changeSupplyMode(); expect(open).toHaveBeenCalledOnce();
    component.changeSupplyMode(true); expect(reset).toHaveBeenCalledWith(true);
  });
  it('normalizes parent/child structure choices and rejects an empty explicit override', () => {
    const { component, api } = create(); component.draft.useStructureOverride = true; component.saveSettings();
    expect(api.configureMeal).not.toHaveBeenCalled(); component.toggleNode('leaf', true);
    expect(component.draft.structureOverrideNodeIds).toEqual(['root']);
  });
  it('uses only the protected reference catalogue and degrades without exposing group keys', () => {
    const { fixture, api } = create(); const updated = structuredClone(data);
    updated.operationalMeals[0].supply!.requirements = [{ portions: 12, requirements: { key: 'internal-key', dietTypeId: 'diet-id', allergenIds: [], intolerances: [] } }];
    fixture.componentRef.setInput('data', updated); fixture.detectChanges();
    expect(api.requirementCatalog).toHaveBeenCalledWith('camp');
    expect(fixture.nativeElement.querySelector('.meal-detail').textContent).not.toContain('internal-key');
    expect(fixture.componentInstance.catalog().dietTypes).toEqual([]);
  });
});
