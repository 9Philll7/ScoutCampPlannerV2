import { Component, DestroyRef, ElementRef, effect, inject, input, output, signal, untracked, viewChild } from '@angular/core';
import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { CommonSupplyChoice, CookingUnitMeal, MealPlanningApiService, RecipeOption, SupplySolution, SupplyPositionOption, RequirementLabelCatalog, SupplyScenario, SupplyScenarioSelection, SupplySuggestionDocument, SupplySuggestion } from './meal-planning-api.service';
import { alternativeDecisions, AlternativeDecision, mergeSupplySolutions, SupplyOfferOptions } from './supply-alternative-choices';
import { ActionIconComponent } from '../../shared/action-icon.component';
import { emptyRequirementCatalog, operativeMealStatus, operativeVerificationLabel, requirementDescription, supplyConflictGroups, missingPortions, isPlanRecipeSolution } from './operative-meal-view';

@Component({
  selector: 'scp-supply-solutions', standalone: true,
  imports: [FormsModule, DatePipe, NgTemplateOutlet, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule, ActionIconComponent],
  templateUrl: './supply-solutions.component.html',
  styleUrl: './supply-solutions.component.scss',
})
export class SupplySolutionsComponent {
  readonly campId = input.required<string>();
  readonly state = input.required<CookingUnitMeal>();
  readonly recipes = input<RecipeOption[]>([]);
  readonly disabled = input(false);
  readonly showVerification = input(true);
  readonly contextLabel = input('');
  readonly frozen = input(false);
  readonly externalBusy = input(false);
  readonly catalog = input<RequirementLabelCatalog>(emptyRequirementCatalog);
  readonly offers = input<SupplyOfferOptions[]>([]);
  readonly togetherPreference = input(1);
  readonly saved = output<boolean>();
  readonly verified = output<void>();
  readonly drafts = signal<SupplySolution[]>([]);
  readonly positions = signal<Partial<Record<string, SupplyPositionOption[]>>>({});
  readonly scenarioSelections = signal<SupplyScenarioSelection[]>([]);
  private originalScenarioSelections: SupplyScenarioSelection[] = [];
  readonly busy = signal(false);
  readonly error = signal('');
  reason = '';
  private baseline = '';
  private readonly api = inject(MealPlanningApiService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly solutionDialog = viewChild<ElementRef<HTMLDialogElement>>('solutionDialog');
  private readonly verificationDialog = viewChild<ElementRef<HTMLDialogElement>>('verificationDialog');
  private readonly conflictDialog = viewChild<ElementRef<HTMLDialogElement>>('conflictDialog');
  private readonly scenarioDialog = viewChild<ElementRef<HTMLDialogElement>>('scenarioDialog');
  readonly editingScenarios = signal(false);
  readonly completeOwnSupply = signal(false);
  private loadedStateId = '';
  private loadedState?: CookingUnitMeal;
  constructor() { effect(() => {
    const state = this.state();
    if (untracked(() => this.loadedStateId === state.id && this.hasUnsavedChanges())) return;
    this.loadedStateId = state.id;
    this.loadedState = state;
    this.scenarioPreviewIds.clear();
    this.completeOwnSupply.set(state.completeOwnSupply ?? false);
    this.scenarioSelections.set(structuredClone(state.manualScenarioSelections ?? []));
    this.originalScenarioSelections = structuredClone(state.manualScenarioSelections ?? []);
    const manual = state.manualSupplySolutions ?? [];
    const calculated = state.supply?.solutions ?? [];
    // A pending manual edit must not be replaced by the previous calculation on reload.
    this.drafts.set(mergeSupplySolutions(manual.length ? [ ...manual, ...calculated.filter(value =>
      !manual.some(item => item.offerGroupId === value.offerGroupId && item.allocations.some(allocation =>
        value.allocations.some(previous => previous.requirementGroupKey === allocation.requirementGroupKey)))) ] : calculated));
    this.baseline = untracked(() => this.draftKey());
    this.originalDrafts = untracked(() => structuredClone(this.drafts()));
    this.reason = '';
  });
    effect(() => { this.drafts(); this.editingSolution(); this.editorMode(); untracked(() => this.queueOwnPreview()); });
    this.destroyRef.onDestroy(() => clearTimeout(this.ownPreviewTimer));
  }
  status() { return operativeMealStatus(this.state()); }
  verificationLabel() { return operativeVerificationLabel(this.state()); }
  hasUnsavedDecisions() { return this.draftKey() !== this.baseline; }
  discardChanges() {
    if (this.isBusy()) return;
    // Keep IDs, provenance and positional choices when restoring the loaded state.
    this.drafts.set(structuredClone(this.originalDrafts)); this.completeOwnSupply.set(this.loadedState?.completeOwnSupply ?? false); this.reason = '';
    this.scenarioSelections.set(structuredClone(this.originalScenarioSelections));
    this.solutionDialog()?.nativeElement.close(); this.conflictDialog()?.nativeElement.close(); this.editingSolution.set(false); this.verificationDialog()?.nativeElement.close();
    this.scenarioDialog()?.nativeElement.close(); this.editingScenarios.set(false);
  }
  private originalDrafts: SupplySolution[] = [];
  private scenarioPreviewIds = new Set<string>();
  readonly focusedRequirement = signal<string | null | undefined>(undefined);
  conflicts() { return supplyConflictGroups(this.state()); }
  visibleConflicts() {
    const key = this.editorMode() === 'conflicts'
      ? undefined : this.focusedRequirement();
    return this.conflicts().filter(group => key === undefined || group.key === key);
  }
  conflictTitle(group: ReturnType<typeof supplyConflictGroups>[number]) {
    if (this.alternativeChoices(group).length) return 'Passende Alternative auswählen';
    const missing = missingPortions([group]);
    return missing !== null && missing > 0 ? `${missing.toLocaleString('de')} ${missing === 1 ? 'Person nicht abgedeckt' : 'Personen nicht abgedeckt'}` : 'Versorgung klären';
  }
  conflictSummaryTitle() {
    const missing = missingPortions(this.remainingConflicts());
    return missing !== null && missing > 0
      ? `${missing.toLocaleString('de')} ${missing === 1 ? 'Person nicht abgedeckt' : 'Personen nicht abgedeckt'}`
      : 'Versorgung klären';
  }
  conflictPortions(group: ReturnType<typeof supplyConflictGroups>[number]) {
    const missing = missingPortions([group]);
    return missing === null || missing <= 0 ? null : `${missing.toLocaleString('de')} ${missing === 1 ? 'Person' : 'Personen'}`;
  }
  conflictReasons(group: ReturnType<typeof supplyConflictGroups>[number]) { return [...new Set(group.problems.map(problem => this.problemLabel(problem.code)))]; }
  isFocused(solution: SupplySolution) { return this.focusedRequirement() !== undefined && solution.allocations.some(value => value.requirementGroupKey === this.focusedRequirement()); }
  visibleCandidates() { return (this.state().supply?.candidates ?? []).filter(value => this.focusedRequirement() === undefined || this.isFocused(value)); }
  decisions() {
    const active = alternativeDecisions(this.state(), this.offers(), this.togetherPreference());
    const changedOffers: (string | null)[] = [];
    const preview = this.preferenceOptions().flatMap(choice => {
      const mode = this.scenarioSelections().find(value => value.offerGroupId === choice.offerGroupId)?.preference;
      const savedMode = this.state().manualScenarioSelections?.find(value => value.offerGroupId === choice.offerGroupId)?.preference;
      if (mode === undefined || mode === savedMode) return [];
      changedOffers.push(choice.offerGroupId);
      if (mode === 1) return [];
      return (mode === 0 ? choice.togetherScenario : choice.separateScenario)?.decisions.map(value => ({ id: value.id, offerId: value.offerGroupId, keys: value.requirementGroupKeys, candidates: value.options, together: value.together })) ?? [];
    });
    return [...active.filter(value => !changedOffers.includes(value.offerId)), ...preview];
  }
  preferenceOptions() { return this.state().supply?.preferenceOptions ?? this.state().supply?.commonSupplyChoices ?? []; }
  scenarioSelected(choice: CommonSupplyChoice, mode: 0 | 1 | 2) { return (this.scenarioSelections().find(value => value.offerGroupId === choice.offerGroupId)?.preference ?? this.togetherPreference()) === mode; }
  selectScenario(choice: CommonSupplyChoice, mode: 0 | 1 | 2) {
    if (this.disabled() || this.frozen() || this.isBusy() || this.state().status === 1) return;
    const scenario = mode === 0 ? choice.togetherScenario : choice.separateScenario;
    if (!scenario) return;
    this.scenarioSelections.update(values => [...values.filter(value => value.offerGroupId !== choice.offerGroupId), { offerGroupId: choice.offerGroupId, preference: mode }]);
    this.chooseCommon(choice, mode === 1 ? [] : scenario.solutions, true);
  }
  scenarioSummary(scenario: SupplyScenario) {
    const parts = [];
    if (scenario.decisions.length) parts.push(`${scenario.decisions.length} offene Rezeptauswahl(en)`);
    if (scenario.problems.some(problem => ![1, 4, 19].includes(problem.code))) parts.push('Noch nicht vollständig abgedeckt');
    return parts.join(' · ');
  }
  candidateKey(candidate: SupplySolution) { return this.decisionKey(candidate); }
  visibleSolutions() {
    const replacements = this.decisions().filter(choice => choice.together);
    return this.drafts().filter(solution => !replacements.some(choice => solution.offerGroupId === choice.offerId &&
      solution.allocations.length > 0 && solution.allocations.every(allocation => choice.keys.includes(allocation.requirementGroupKey))) &&
      !(this.state().supply?.commonSupplyChoices ?? []).some(choice => solution.offerGroupId === choice.offerGroupId &&
        (!this.persistedScenario(choice) || this.scenarioSelections().find(value => value.offerGroupId === choice.offerGroupId)?.preference !== this.state().manualScenarioSelections?.find(value => value.offerGroupId === choice.offerGroupId)?.preference) &&
        solution.allocations.length > 0 && solution.allocations.every(allocation => choice.requirementGroupKeys.includes(allocation.requirementGroupKey))));
  }
  persistedScenario(choice: CommonSupplyChoice) { return !!this.state().manualScenarioSelections?.some(value => value.offerGroupId === choice.offerGroupId && value.preference !== 1) ||
    choice.requirementGroupKeys.every(key => this.state().manualSupplySolutions?.some(solution => solution.offerGroupId === choice.offerGroupId && solution.allocations.some(allocation => allocation.requirementGroupKey === key))); }
  commonSelected(choice: CommonSupplyChoice, selected: SupplySolution[]) {
    const relevant = this.drafts().filter(solution => solution.offerGroupId === choice.offerGroupId &&
      solution.allocations.some(allocation => choice.requirementGroupKeys.includes(allocation.requirementGroupKey)));
    const key = (values: SupplySolution[]) => mergeSupplySolutions(values.map(value => ({ ...value, source: 1 }))).map(value => this.decisionKey(value)).sort().join('|');
    return selected.length > 0 && relevant.every(value => value.source === 1) && key(relevant) === key(selected);
  }
  hasCommonSelection(choice: CommonSupplyChoice) { return choice.togetherScenario ? this.scenarioSelections().some(value => value.offerGroupId === choice.offerGroupId) : this.commonSelected(choice, choice.separateSolutions) || choice.togetherOptions.some(option => this.commonSelected(choice, [option])); }
  preferenceLabel() {
    const preferences = new Set(this.preferenceOptions().map(choice => this.scenarioSelections().find(value => value.offerGroupId === choice.offerGroupId)?.preference ?? this.togetherPreference()));
    if (preferences.size > 1) return 'Je Komponente festgelegt';
    return ['Gemeinsam', 'Fragen', 'Getrennt'][preferences.values().next().value ?? this.togetherPreference()] ?? 'Fragen';
  }
  alternativeChoices(group: ReturnType<typeof supplyConflictGroups>[number]) { return this.decisions().filter(choice => !!group.key && choice.keys.includes(group.key)); }
  remainingConflicts() { const decisions = this.decisions(); return this.conflicts().filter(group => {
    if (!group.key) return !group.problems.every(problem => [1, 4, 19].includes(problem.code) && decisions.some(choice => choice.offerId === problem.referenceId));
    const choices = decisions.filter(choice => choice.keys.includes(group.key!));
    return !choices.length || group.coverage.some(coverage => !choices.some(choice => choice.offerId === coverage.offerGroupId)) ||
      group.problems.some(problem => [14, 16, 17, 18].includes(problem.code));
  }); }
  alternativeSelected(candidate: SupplySolution) { return this.drafts().some(value => value.source === 1 &&
    this.decisionKey({ ...value, portions: candidate.portions, allocations: candidate.allocations }) === this.decisionKey(candidate) &&
    candidate.allocations.every(allocation => value.allocations.some(item => item.requirementGroupKey === allocation.requirementGroupKey && item.portions === allocation.portions))); }
  hasSelectedAlternative(candidates: SupplySolution[]) { return candidates.some(candidate => this.alternativeSelected(candidate)); }
  selectAlternative(key: string, candidate: SupplySolution) {
    const decision = this.decisions().find(choice => choice.keys.includes(key) && choice.candidates.some(value => this.decisionKey(value) === this.decisionKey(candidate)));
    if (decision) this.selectDecision(decision, candidate);
  }
  selectDecision(decision: AlternativeDecision, candidate: SupplySolution) {
    if (this.disabled() || this.frozen() || this.isBusy() || this.state().status === 1) return;
    if (!this.decisions().some(choice => choice.id === decision.id && choice.candidates.some(value => this.decisionKey(value) === this.decisionKey(candidate)))) return;
    this.error.set('');
    this.drafts.update(values => mergeSupplySolutions([...values.flatMap(value => {
      if (value.offerGroupId !== candidate.offerGroupId || !value.allocations.some(allocation => decision.keys.includes(allocation.requirementGroupKey))) return [value];
      const copy = structuredClone(value), removed = copy.allocations.filter(allocation => decision.keys.includes(allocation.requirementGroupKey)).reduce((sum, allocation) => sum + allocation.portions, 0);
      copy.allocations = copy.allocations.filter(allocation => !decision.keys.includes(allocation.requirementGroupKey)); copy.portions -= removed;
      return copy.portions > 0 && copy.allocations.length ? [copy] : [];
    }), { ...structuredClone(candidate), id: crypto.randomUUID(), source: 1 }]));
  }
  isBusy() { return this.busy() || this.externalBusy(); }
  readonly editorMode = signal<'conflicts' | 'custom'>('custom');
  readonly editingSolution = signal(false);
  readonly ownTab = signal<'solutions' | 'impact' | 'review'>('solutions');
  readonly ownExpanded = signal<string | null>(null);
  readonly addingOwnRecipe = signal(false);
  ownRecipeSearch = '';
  ownRecipes() { return this.recipes().filter(value => value.isPortionBased !== false && value.name.toLocaleLowerCase('de').includes(this.ownRecipeSearch.toLocaleLowerCase('de'))); }
  ownRows() { return this.drafts(); }
  planRows(): SupplySolution[] { return []; }
  ownAvailableGroups(_solution: Pick<SupplySolution, 'id' | 'offerGroupId' | 'allocations'>) { return this.groups(); }
  ownOfferIds(_solution: SupplySolution) { return this.offerIds(); }
  changeOwnOffer(solution: SupplySolution, offerGroupId: string | null) { solution.offerGroupId = offerGroupId; }
  readonly ownPreview = signal<{ solutionId: string; problems: import('./meal-planning-api.service').SupplyProblem[]; error: string | null }[] | null>(null);
  readonly ownPreviewLoading = signal(false);
  readonly ownPreviewError = signal('');
  private ownPreviewTimer?: ReturnType<typeof setTimeout>;
  private ownPreviewRequest = 0;
  private ownPreviewKey = '';
  private previewKey() { return JSON.stringify([this.state().id, this.state().supplyVersion, this.draftKey()]); }
  queueOwnPreview() {
    if (!this.editingSolution() || this.editorMode() !== 'custom') {
      clearTimeout(this.ownPreviewTimer); ++this.ownPreviewRequest; this.ownPreviewKey = ''; return;
    }
    const key = this.previewKey(); if (key === this.ownPreviewKey) return;
    this.ownPreviewKey = key; clearTimeout(this.ownPreviewTimer); ++this.ownPreviewRequest;
    this.ownPreview.set(null); this.ownPreviewError.set(''); this.ownPreviewLoading.set(true);
    this.ownPreviewTimer = setTimeout(() => this.refreshOwnPreview(), 250);
  }
  refreshOwnPreview() {
    clearTimeout(this.ownPreviewTimer);
    const request = ++this.ownPreviewRequest;
    const key = this.previewKey(); this.ownPreviewKey = key; this.ownPreviewLoading.set(true); this.ownPreviewError.set('');
    this.api.previewSupply(this.campId(), this.state(), structuredClone(this.drafts())).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: values => { if (request !== this.ownPreviewRequest || key !== this.previewKey()) return; this.ownPreview.set(values); this.ownPreviewLoading.set(false); },
      error: () => { if (request !== this.ownPreviewRequest) return; this.ownPreview.set(null); this.ownPreviewLoading.set(false); this.ownPreviewError.set('Konfliktprüfung derzeit nicht verfügbar. Nach dem Speichern neu berechnen.'); }
    });
  }
  ownRecipeWarnings(id: string) {
    const item = this.ownPreview()?.find(value => value.solutionId === id);
    return [...new Set([...(item?.error ? [item.error] : []), ...(item?.problems.map(value => this.problemLabel(value.code) + (value.requirementGroupKey ? ': ' + this.requirementLabel(value.requirementGroupKey) : '')) ?? [])])];
  }
  ownConflictCount() { return this.ownPreview()?.filter(value => value.error || value.problems.length).length ?? 0; }
  ownCoverage() {
    const requirements = this.groups(), rows = [...this.ownRows(), ...this.planRows()];
    const offers: (string | null)[] = this.offerIds().length ? this.offerIds() : [null];
    let missing = 0, excess = 0;
    for (const group of requirements) {
      const supplied = offers.map(offer => rows.filter(value => value.offerGroupId === offer).flatMap(value => value.allocations)
        .filter(value => value.requirementGroupKey === group.requirements.key).reduce((sum, value) => sum + value.portions, 0));
      missing += Math.max(0, ...supplied.map(value => group.portions - value));
      excess += Math.max(0, ...supplied.map(value => value - group.portions));
    }
    return { known: requirements.length > 0, total: requirements.reduce((sum,value) => sum + value.portions, 0), missing, excess,
      invalid: this.ownRows().some(value => !Number.isFinite(value.portions) || value.portions <= 0 || !value.allocations.length ||
        value.allocations.some(item => !Number.isFinite(item.portions) || item.portions < 0 || !requirements.some(group => group.requirements.key === item.requirementGroupKey)) ||
        Math.abs(value.allocations.reduce((sum,item) => sum + item.portions, 0) - value.portions) > 0.000001) };
  }
  ownGroupLabel(solution: SupplySolution) {
    if (!solution.allocations.length) return 'Gruppen zuordnen';
    if (this.groups().length && this.groups().every(group => solution.allocations.some(value => value.requirementGroupKey === group.requirements.key))) return 'Alle Gruppen';
    return solution.allocations.length > 1 ? solution.allocations.length + ' Anforderungsgruppen' : this.requirementLabel(solution.allocations[0].requirementGroupKey);
  }
  addOwnRecipe(recipeId: string) {
    if (this.disabled() || this.frozen() || this.isBusy() || !this.recipes().some(value => value.revisionId === recipeId)) return;
    const id = crypto.randomUUID();
    this.drafts.update(values => [...values, { id, recipeRevisionId: recipeId, offerGroupId: this.offers()[0]?.id ?? null,
      portions: this.state().effectiveDemand || 1, source: 1, allocations: [], positionSelections: {} }]);
    this.ownExpanded.set(id); this.addingOwnRecipe.set(false);
  }
  toggleOwnRow(solution: SupplySolution, positions = false) {
    this.ownExpanded.set(this.ownExpanded() === solution.id && !positions ? null : solution.id);
    if (positions) this.loadPositions(solution.recipeRevisionId);
  }
  setOwnGroup(solution: SupplySolution, key: string, selected: boolean) {
    const available = this.ownAvailableGroups(solution).find(value => value.requirements.key === key);
    if (selected && (!available || available.portions <= 0)) return;
    if (selected && !solution.allocations.some(value => value.requirementGroupKey === key)) solution.allocations.push({ requirementGroupKey: key, portions: available!.portions });
    if (!selected) solution.allocations = solution.allocations.filter(value => value.requirementGroupKey !== key);
    solution.portions = solution.allocations.reduce((sum,value) => sum + value.portions, 0);
  }
  ownGroupSelected(solution: SupplySolution, key: string) { return solution.allocations.some(value => value.requirementGroupKey === key); }
  ownAllocation(solution: SupplySolution, key: string) { return solution.allocations.find(value => value.requirementGroupKey === key)?.portions ?? 0; }
  setOwnAllocation(solution: SupplySolution, key: string, portions: number) {
    const allocation = solution.allocations.find(value => value.requirementGroupKey === key); if (allocation) allocation.portions = portions;
    solution.portions = solution.allocations.reduce((sum,value) => sum + value.portions, 0);
  }
  assignAllOwnGroups(solution: SupplySolution) {
    solution.allocations = this.ownAvailableGroups(solution).filter(value => value.portions > 0).map(value => ({ requirementGroupKey: value.requirements.key, portions: value.portions }));
    solution.portions = solution.allocations.reduce((sum,value) => sum + value.portions, 0);
  }
  selectedConflict = '';
  private conflictWholeDrafts = new Set<string>();
  openEditor(key?: string | null) {
    if (this.isBusy() || this.disabled() || this.frozen()) return;
    this.ownTab.set('solutions'); this.ownExpanded.set(null); this.addingOwnRecipe.set(false);
    this.editingSolution.set(true); this.editorMode.set('custom'); this.focusedRequirement.set(key); this.solutionDialog()?.nativeElement.showModal();
  }
  openConflictEditor() {
    if (this.isBusy() || this.disabled() || this.frozen() || this.state().status === 1) return;
    this.editingSolution.set(true); this.editorMode.set('conflicts'); this.focusedRequirement.set(undefined);
    this.conflictWholeDrafts.clear();
    this.selectedConflict = this.conflictScopes()[0]?.id ?? '';
    this.suggestionSelection.set(''); this.suggestionPages.set({}); this.suggestions.set(null);
    this.conflictDialog()?.nativeElement.showModal(); this.loadSuggestions();
  }
  readonly suggestions = signal<SupplySuggestionDocument | null>(null);
  readonly suggestionsLoading = signal(false);
  readonly suggestionSelection = signal('');
  readonly suggestionOffer = signal<string | null>(null);
  suggestionSort = 'suitability';
  readonly suggestionPages = signal<Record<string, number>>({});
  suggestionPage(scope: string) { return this.suggestionPages()[scope] ?? 0; }
  changeSuggestionPage(scope: string, page: number) { this.suggestionPages.update(values => ({ ...values, [scope]: Math.max(0, page) })); }
  private suggestionRequest = 0;
  loadSuggestions() {
    const request = ++this.suggestionRequest; this.error.set(''); this.suggestionsLoading.set(true);
    this.suggestionSelection.set('');
    this.api.supplySuggestions(this.campId(), this.state()).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: value => { if (request !== this.suggestionRequest || !this.editingSolution()) return;
        this.suggestions.set(value); this.suggestionOffer.set(value.offers[0]?.offerGroupId ?? null); this.suggestionsLoading.set(false); },
      error: () => { if (request !== this.suggestionRequest) return; this.suggestionsLoading.set(false); this.error.set('Rezeptvorschläge konnten nicht geladen werden.'); }
    });
  }
  activeSuggestionOffer() { return this.suggestions()?.offers.find(value => value.offerGroupId === this.suggestionOffer()); }
  suggestionGroups() {
    const options = this.activeSuggestionOffer()?.options ?? [];
    return [...new Set(options.map(value => value.scope))].map(scope => ({ scope, label: options.find(value => value.scope === scope)!.label,
      preferred: options.some(value => value.scope === scope && value.preferred),
      options: options.filter(value => value.scope === scope).sort((a,b) => this.suggestionSort === 'name' ? a.recipeName.localeCompare(b.recipeName, 'de') :
        Object.keys(a.solution.positionSelections).length - Object.keys(b.solution.positionSelections).length || a.recipeName.localeCompare(b.recipeName, 'de')) })).map(group => ({ ...group, count: group.options.length, pages: Math.ceil(group.options.length / 3),
        options: group.options.slice(this.suggestionPage(group.scope) * 3, (this.suggestionPage(group.scope) + 1) * 3) }));
  }
  selectedSuggestion() { return this.activeSuggestionOffer()?.options.find(value => value.id === this.suggestionSelection()); }
  selectSuggestion(option: SupplySuggestion) { if (!this.isBusy()) this.suggestionSelection.set(option.id); }
  applySuggestion() {
    const selected = this.selectedSuggestion(), document = this.suggestions();
    if (!selected || !document || this.isBusy() || this.disabled() || this.frozen() || this.state().status === 1) return;
    this.busy.set(true); this.error.set('');
    this.api.applySupplySuggestion(this.campId(), this.state(), document.expectedVersion, selected.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.busy.set(false); this.conflictDialog()?.nativeElement.close(); this.editingSolution.set(false); this.saved.emit(true); },
      error: error => { this.busy.set(false); this.suggestionSelection.set(''); if (error.status === 409) this.suggestions.set(null);
        this.error.set(error.status === 409 ? 'Der Versorgungsstand hat sich geändert. Bitte neu berechnen und Vorschläge erneut laden.' : 'Die Lösung konnte nicht gespeichert werden.'); }
    });
  }
  conflictTargets() {
    return this.remainingConflicts().flatMap(group => group.key === null ? [] : group.coverage
      .filter(coverage => coverage.missing !== null && coverage.missing > 0)
      .map(coverage => ({ id: JSON.stringify([group.key, coverage.offerGroupId]), key: group.key!,
        offerId: coverage.offerGroupId, portions: coverage.missing! })));
  }
  conflictScopes() {
    const targets = this.conflictTargets();
    const offers = [...new Set(targets.map(value => value.offerId))];
    return offers.flatMap(offerId => {
      const missing = targets.filter(value => value.offerId === offerId);
      const allocations = missing.map(value => ({ requirementGroupKey: value.key, portions: value.portions }));
      const preference = this.scenarioSelections().find(value => value.offerGroupId === offerId)?.preference ?? this.togetherPreference();
      const scope = (kind: string, label: string, values: SupplySolution['allocations']) => ({
        id: JSON.stringify([offerId, kind]), offerId, kind, label, allocations: values,
        portions: values.reduce((sum, value) => sum + value.portions, 0)
      });
      const result = [scope('missing', 'Alle offenen Personen', allocations)];
      if (preference === 0 && this.originalDrafts.some(value => value.offerGroupId === offerId)) {
        // Include existing allocations as well as uncovered people, never scale to an
        // unrelated component or sum people across components.
        const all = new Map(allocations.map(value => [value.requirementGroupKey, value.portions]));
        for (const value of this.originalDrafts.filter(value => value.offerGroupId === offerId).flatMap(value => value.allocations))
          all.set(value.requirementGroupKey, (all.get(value.requirementGroupKey) ?? 0) + value.portions);
        result.unshift(scope('whole', 'Gesamte Gruppe gemeinsam versorgen', [...all].map(([requirementGroupKey, portions]) => ({ requirementGroupKey, portions }))));
      }
      if (preference !== 0 && missing.length > 1) result.push(...missing.map(value => scope('single:' + value.key, this.requirementLabel(value.key), [{ requirementGroupKey: value.key, portions: value.portions }])));
      return result;
    });
  }
  private replacedConflictOffers() {
    return new Set(this.editableDrafts().filter(value => this.conflictWholeDrafts.has(value.id)).map(value => value.offerGroupId));
  }
  protectedSolutions() { const replaced = this.replacedConflictOffers(); return this.originalDrafts.filter(value => !replaced.has(value.offerGroupId)); }
  isWholeConflictSolution(solution: SupplySolution) { return this.conflictWholeDrafts.has(solution.id); }
  editableDrafts() {
    return this.editorMode() === 'custom' ? this.drafts() : this.drafts().filter(value => !this.originalDrafts.some(original => original.id === value.id));
  }
  addConflictSolution() {
    const target = this.conflictScopes().find(value => value.id === this.selectedConflict), recipe = this.recipes()[0];
    if (!target || !recipe || this.isBusy() || this.disabled() || this.frozen()) return;
    const existing = this.editableDrafts().filter(value => value.offerGroupId === target.offerId);
    if (existing.some(value => this.conflictWholeDrafts.has(value.id)) || target.kind === 'whole' && existing.length) {
      this.error.set('Entferne zuerst die neue Rezeptzuordnung dieser Komponente, bevor du den Umfang wechselst.'); return;
    }
    const allocations = target.allocations.map(value => ({ ...value, portions: Math.max(0, value.portions - existing.flatMap(item => item.allocations)
      .filter(item => item.requirementGroupKey === value.requirementGroupKey).reduce((sum, item) => sum + item.portions, 0)) })).filter(value => value.portions > 0);
    const portions = allocations.reduce((sum, value) => sum + value.portions, 0);
    if (!portions) { this.error.set('Diese Personen sind bereits im Entwurf zugeordnet.'); return; }
    const id = crypto.randomUUID();
    if (target.kind === 'whole') this.conflictWholeDrafts.add(id);
    this.drafts.update(values => [...values, { id, recipeRevisionId: recipe.revisionId,
      offerGroupId: target.offerId, portions, source: 1, allocations, positionSelections: {} }]);
  }
  changePortions(solution: SupplySolution, portions: number) {
    solution.portions = portions;
    if (solution.allocations.length === 1) solution.allocations[0].portions = portions;
  }
  saveConflictResolution() {
    if (this.state().status === 1 || this.editorMode() !== 'conflicts') return;
    const additions = this.editableDrafts(), replaced = this.replacedConflictOffers();
    const targets = this.conflictTargets().filter(value => !replaced.has(value.offerId));
    for (const scope of this.conflictScopes().filter(value => value.kind === 'whole' && replaced.has(value.offerId)))
      targets.push(...scope.allocations.map(value => ({ id: '', offerId: scope.offerId, key: value.requirementGroupKey, portions: value.portions })));
    const originalIntact = this.originalDrafts.every(original => this.drafts().some(value => value.id === original.id && this.decisionKey(value) === this.decisionKey(original)));
    if (!originalIntact || !additions.length || additions.some(value => !value.allocations.length ||
      !Number.isFinite(value.portions) || value.portions <= 0 || Math.abs(value.allocations.reduce((sum, item) => sum + item.portions, 0) - value.portions) > 0.000001 ||
      value.allocations.some(item => !Number.isFinite(item.portions) || item.portions <= 0 || !targets.some(target => target.offerId === value.offerGroupId && target.key === item.requirementGroupKey))) ||
      targets.some(target => additions.filter(value => value.offerGroupId === target.offerId).flatMap(value => value.allocations)
        .filter(value => value.requirementGroupKey === target.key).reduce((sum, value) => sum + value.portions, 0) > target.portions)) {
      this.error.set('Die Zuordnung muss innerhalb des gewählten Personenkreises bleiben. Bestehende Lösungen außerhalb bleiben unverändert.'); return;
    }
    const preserved = this.originalDrafts.filter(value => !replaced.has(value.offerGroupId) && (this.isManual(value) || additions.some(addition =>
      addition.offerGroupId === value.offerGroupId && addition.allocations.some(allocation =>
        value.allocations.some(previous => previous.requirementGroupKey === allocation.requirementGroupKey)))));
    this.submit(mergeSupplySolutions([...preserved, ...additions].map(value => ({ ...structuredClone(value), source: 1 }))), true);
  }
  closeEditor() {
    if (this.isBusy() || (this.hasUnsavedDecisions() && !window.confirm('Ungespeicherte Versorgungsentscheidungen verwerfen?'))) return;
    ++this.suggestionRequest; this.suggestionsLoading.set(false);
    this.completeOwnSupply.set(this.loadedState?.completeOwnSupply ?? false);
    this.drafts.set(structuredClone(this.originalDrafts)); this.scenarioSelections.set(structuredClone(this.originalScenarioSelections)); this.solutionDialog()?.nativeElement.close(); this.conflictDialog()?.nativeElement.close(); this.editingSolution.set(false); this.error.set('');
  }
  openVerification() {
    if (this.isBusy() || this.frozen()) return;
    if (!this.verificationNeedsReason() && this.state().status !== 1) { this.verify(0); return; }
    this.verificationDialog()?.nativeElement.showModal();
  }
  openScenarioEditor() { if (!this.isBusy()) { this.editingScenarios.set(true); this.scenarioDialog()?.nativeElement.showModal(); } }
  closeScenarioEditor() {
    if (this.isBusy() || (this.hasUnsavedDecisions() && !window.confirm('Ungespeicherte Auswahl der Versorgungsart verwerfen?'))) return;
    this.completeOwnSupply.set(this.loadedState?.completeOwnSupply ?? false);
    this.drafts.set(structuredClone(this.originalDrafts)); this.scenarioSelections.set(structuredClone(this.originalScenarioSelections));
    this.scenarioDialog()?.nativeElement.close(); this.editingScenarios.set(false); this.error.set('');
  }
  closeVerification() {
    if (this.isBusy() || (this.reason.trim() && !window.confirm('Ungespeicherte Verifikationsbegründung verwerfen?'))) return;
    this.reason = ''; this.verificationDialog()?.nativeElement.close(); this.error.set('');
  }
  requirementLabel(key: string) {
    const requirement = this.groups().find(group => group.requirements.key === key)?.requirements ?? { key };
    return requirementDescription(requirement, this.catalog());
  }
  offerLabel(id: string | null) { return this.offers().find(offer => offer.id === id)?.name || (id ? 'Gespeicherte Komponente' : 'Eigenes Angebot'); }
  quantities(solution: SupplySolution) { return this.state().supply?.quantities?.filter(quantity => quantity.solutionId === solution.id) ?? []; }
  adjustments(solution: SupplySolution) { return Object.entries(solution.positionSelections).map(([path, selection]) => {
    const position = this.positions()[solution.recipeRevisionId]?.find(position => position.path === path);
    const choice = position?.choices.find(choice => (choice.selection?.replacementRuleId ?? null) === selection.replacementRuleId &&
      (choice.selection?.variantKey ?? null) === selection.variantKey);
    return position && choice ? `${position.name}: ${choice.label}` : selection.replacementRuleId ? 'Zutatenersatz gewählt' : 'Zutatenvariante gewählt';
  }); }
  add() {
    const recipe = this.recipes()[0];
    if (!recipe) return;
    this.drafts.update(values => [...values, { id: crypto.randomUUID(), recipeRevisionId: recipe.revisionId,
      offerGroupId: null, portions: this.state().effectiveDemand || 1, source: 1, allocations: [], positionSelections: {} }]);
  }
  offerIds() { return [...new Set([...this.offers().map(offer => offer.id), ...[...this.drafts(), ...(this.state().supply?.candidates ?? [])].map(value => value.offerGroupId).filter((value): value is string => !!value)])]; }
  groups() { return this.state().supply?.requirements ?? this.state().requirementGroups ?? []; }
  availableGroups(solution: SupplySolution) { return this.groups().filter(group => !solution.allocations.some(value => value.requirementGroupKey === group.requirements.key)); }
  addAllocation(solution: SupplySolution, key: string) {
    if (!solution.allocations.some(value => value.requirementGroupKey === key)) solution.allocations.push({ requirementGroupKey: key, portions: 0 });
  }
  loadPositions(revision: string) {
    this.api.supplyPositions(this.campId(), revision).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: values => this.positions.update(current => ({ ...current, [revision]: values })),
      error: () => this.error.set('Rezeptpositionen konnten nicht geladen werden. Gespeicherte Entscheidungen bleiben erhalten.')
    });
  }
  selectedPosition(solution: SupplySolution, position: SupplyPositionOption) {
    const selected = solution.positionSelections[position.path];
    return position.choices.findIndex(choice => (choice.selection?.replacementRuleId ?? null) === (selected?.replacementRuleId ?? null)
      && (choice.selection?.variantKey ?? null) === (selected?.variantKey ?? null));
  }
  hiddenPosition(solution: SupplySolution, path: string) {
    return Object.entries(solution.positionSelections).some(([parent, selection]) => !!selection.replacementRuleId && path.startsWith(parent + '/'));
  }
  selectPosition(solution: SupplySolution, position: SupplyPositionOption, index: number) {
    const choice = position.choices[index];
    if (!choice) return;
    if (choice.selection) solution.positionSelections[position.path] = { ...choice.selection };
    else delete solution.positionSelections[position.path];
    if (choice.selection?.replacementRuleId) for (const path of Object.keys(solution.positionSelections))
      if (path.startsWith(position.path + '/')) delete solution.positionSelections[path];
  }
  changeRecipe(solution: SupplySolution, recipe: string) { solution.recipeRevisionId = recipe; solution.positionSelections = {}; }
  remove(id: string) { if (this.isBusy() || this.disabled() || this.frozen() || this.editorMode() === 'conflicts' && this.originalDrafts.some(value => value.id === id)) return; this.drafts.update(values => values.filter(value => value.id !== id)); }
  choose(candidate: SupplySolution) {
    if (this.drafts().some(value => this.decisionKey(value) === this.decisionKey(candidate))) {
      this.error.set('Diese Entscheidung ist bereits vorhanden.'); return;
    }
    this.drafts.update(values => [...values, { ...structuredClone(candidate), id: crypto.randomUUID(), source: 1 }]);
  }
  chooseCommon(choice: CommonSupplyChoice, selected: SupplySolution[], preview = false) {
    if (this.disabled() || this.frozen() || this.isBusy() || this.state().status === 1) return;
    this.error.set('');
    const chosen = structuredClone(selected).map(value => ({ ...value, id: crypto.randomUUID(), source: preview ? 0 : 1 }));
    if (preview) for (const value of chosen) this.scenarioPreviewIds.add(value.id);
    this.drafts.update(values => [
      ...values.filter(value => value.offerGroupId !== choice.offerGroupId ||
        !value.allocations.some(allocation => choice.requirementGroupKeys.includes(allocation.requirementGroupKey))),
      ...chosen
    ]);
  }
  private decisionKey(value: SupplySolution) { return JSON.stringify({
    recipe: value.recipeRevisionId, offer: value.offerGroupId, portions: value.portions,
    positions: Object.entries(value.positionSelections).sort(([a], [b]) => a.localeCompare(b)),
    allocations: [...value.allocations].sort((a, b) => a.requirementGroupKey.localeCompare(b.requirementGroupKey))
  }); }
  isManual(solution: SupplySolution) {
    if (this.scenarioPreviewIds.has(solution.id)) return false;
    const original = mergeSupplySolutions(this.state().supply?.solutions ?? []).find(value => value.id === solution.id);
    return solution.source === 1 || !original || this.decisionKey(original) !== this.decisionKey(solution);
  }
  hasUnsavedChanges() {
    return this.hasUnsavedDecisions() || !!this.reason.trim();
  }
  private draftKey() { return JSON.stringify([this.drafts().map(value => [value.id, value.source, this.decisionKey(value)]), this.scenarioSelections(), this.completeOwnSupply()]); }
  recipeName(id: string) { return this.recipes().find(value => value.revisionId === id)?.name ?? 'Gespeicherte Rezeptrevision'; }
  problemLabel(code: number) { return [
    'Keine passende Direktlösung', 'Mehrere Direktlösungen – bitte entscheiden', 'Nur teilweise aufgelöst',
    'Keine passende Angebotsalternative', 'Mehrere passende Angebotsalternativen', 'Stoffdaten unbekannt',
    'Nur qualitative Stoffdaten vorhanden', 'Grenzwert fehlt', 'Grenzwert überschritten', 'Stoff enthalten, Grenzwert fehlt',
    'Allergenkonflikt', 'Allergenangaben unbekannt', 'Herkunft für Ernährungsform ausgeschlossen', 'Herkunftsregel unbekannt',
    'Manuelle Entscheidung nicht mehr geeignet', 'Anforderungsgruppe unterversorgt', 'Anforderungsgruppe überversorgt',
    'Geplante Personen nicht eindeutig zugeordnet', 'Teilnehmer nicht zugeordnet', 'Gemeinsame oder getrennte Versorgung: Entscheidung erforderlich'
  ][code] ?? 'Versorgungsproblem'; }
  saveOwnSolution() {
    if (!this.hasUnsavedDecisions()) return;
    // Editing the full current solution is an explicit manual snapshot. Merely opening
    // it keeps the plan state unchanged; reset restores plan-following separately.
    this.submit(mergeSupplySolutions(this.drafts().map(value => ({ ...structuredClone(value), source: 1 }))), true, true);
  }
  followPlan(solution: SupplySolution) {
    if (this.completeOwnSupply() || this.isBusy() || this.disabled() || this.frozen()) return;
    const original = this.originalDrafts.find(value => value.id === solution.id && value.source === 0);
    if (original) this.drafts.update(values => values.map(value => value.id === solution.id ? structuredClone(original) : value));
    else this.remove(solution.id);
  }
  save(recalculate = false) { this.submit(mergeSupplySolutions(this.drafts().filter(value => this.isManual(value)).map(value => ({ ...value, source: 1 }))), recalculate); }
  reset(recalculate = false) {
    if (this.disabled() || this.frozen() || this.isBusy()) return;
    this.scenarioSelections.set(structuredClone(this.originalScenarioSelections));
    this.completeOwnSupply.set(false);
    // Keep confirmed choices requested by the plan, including decisions inside a chosen scenario.
    const retained = (this.loadedState?.manualSupplySolutions ?? []).filter(solution =>
      isPlanRecipeSolution(solution, this.loadedState!, this.togetherPreference(), false));
    this.submit(mergeSupplySolutions(retained), recalculate, false);
  }
  verificationNeedsReason() {
    const state = this.state();
    return state.status === 2 || !!state.supply?.problems.length || !!state.participantProblems?.length || !!state.unassignedParticipantIds?.length;
  }
  verify(kind: number) {
    if (this.isBusy() || this.frozen()) return;
    if (this.hasUnsavedDecisions()) { this.error.set('Bitte Entscheidungen vor der Verifikation speichern.'); return; }
    if (this.state().status === 1 || (this.verificationNeedsReason() ? kind !== 1 || !this.reason.trim() : kind !== 0)) return;
    this.busy.set(true); this.error.set('');
    this.api.verifySupply(this.campId(), this.loadedState ?? this.state(), kind, this.verificationNeedsReason() ? this.reason.trim() : '').pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.busy.set(false); this.reason = ''; this.verificationDialog()?.nativeElement.close(); this.verified.emit(); },
      error: () => { this.busy.set(false); this.error.set('Verifikation fehlgeschlagen. Berechtigung, offene Probleme und aktuellen Berechnungsstand prüfen.'); }
    });
  }
  private submit(solutions: SupplySolution[], recalculate = false, completeOwnSupply?: boolean) {
    if (this.disabled() || this.frozen() || this.isBusy()) return;
    this.busy.set(true); this.error.set('');
    const request = completeOwnSupply !== undefined
      ? this.api.saveSupply(this.campId(), this.loadedState ?? this.state(), solutions, this.scenarioSelections(), completeOwnSupply)
      : this.scenarioSelections().length || this.state().manualScenarioSelections?.length
      ? this.api.saveSupply(this.campId(), this.loadedState ?? this.state(), solutions, this.scenarioSelections())
      : this.api.saveSupply(this.campId(), this.loadedState ?? this.state(), solutions);
    request.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => { this.busy.set(false); this.baseline = this.draftKey(); this.originalDrafts = structuredClone(this.drafts()); this.originalScenarioSelections = structuredClone(this.scenarioSelections()); this.solutionDialog()?.nativeElement.close(); this.conflictDialog()?.nativeElement.close(); this.editingSolution.set(false); this.scenarioDialog()?.nativeElement.close(); this.editingScenarios.set(false); this.saved.emit(recalculate); },
      error: error => { this.busy.set(false); this.error.set(error.error?.code === 'unsuitable_supply' ? error.error.message : 'Speichern fehlgeschlagen. Berechtigung und aktuellen Stand prüfen; Änderungen wurden nicht automatisch erneut gesendet.'); }
    });
  }
}
