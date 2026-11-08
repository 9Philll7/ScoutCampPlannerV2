import { Component, DestroyRef, ElementRef, computed, effect, inject, input, output, signal, untracked, viewChild } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { firstValueFrom, Observable, switchMap, tap } from 'rxjs';
import { ActionIconComponent } from '../../shared/action-icon.component';
import { NotificationBannerComponent } from '../../shared/notification-banner.component';
import { CateringTarget } from './catering-overview.component';
import { CookingUnitMeal, MealPlanningApiService, MealPlanningOverview, MealSubscriptionState, OfferTarget, RecipeChoice } from './meal-planning-api.service';
import { SupplySolutionsComponent } from './supply-solutions.component';
import { CookingUnitStructure } from './cooking-unit-structure';
import { emptyRequirementCatalog, hasNamedRequirements, hasOwnRecipeSolutions, operativeMealStatus, operativeVerificationLabel, isPlanRecipeSolution } from './operative-meal-view';

interface Settings {
  subscriptionState: MealSubscriptionState; demandOverride: number | null; useStructureOverride: boolean;
  structureOverrideNodeIds: string[]; offerTargets: OfferTarget[]; recipeChoices: RecipeChoice[];
  manualDemand: boolean; participantFilter: string; expectedVersion: number; preference: string; resetOwnRecipes: boolean; resetAll: boolean;
}

@Component({
  selector: 'scp-operative-meal-workspace', standalone: true,
  imports: [DatePipe, FormsModule, MatButtonModule, ActionIconComponent, NotificationBannerComponent, SupplySolutionsComponent],
  templateUrl: './operative-meal-workspace.component.html', styleUrl: './operative-meal-workspace.component.scss',
})
export class OperativeMealWorkspaceComponent {
  readonly data = input.required<MealPlanningOverview>();
  readonly unitId = input<string>(); readonly mealId = input<string>();
  readonly disabled = input(false); readonly frozen = input(false);
  readonly navigate = output<CateringTarget>();
  readonly changed = output<{ data: MealPlanningOverview; message: string }>();
  readonly unit = computed(() => this.data().cookingUnits.find(unit => unit.id === this.unitId()));
  readonly meals = computed(() => this.data().meals.filter(meal => meal.isActive && meal.isInsideCampPeriod)
    .sort((a, b) => a.date.localeCompare(b.date) || (a.sortOrder ?? 0) - (b.sortOrder ?? 0)));
  readonly days = computed(() => [...new Set(this.meals().map(meal => meal.date))].map(date => ({ date, meals: this.meals().filter(meal => meal.date === date) })));
  readonly meal = computed(() => this.meals().find(meal => meal.id === this.mealId()));
  readonly state = computed(() => this.stateFor(this.mealId()));
  readonly groups = computed(() => this.data().mealPlanDocuments.find(plan => plan.id === this.unit()?.standardMealPlanId)?.offerGroups
    .filter(group => group.campMealId === this.mealId()).sort((a, b) => a.sortOrder - b.sortOrder) ?? []);
  readonly plannedGroups = computed(() => this.groups().map(group => ({ ...group,
    standards: group.entries.filter(entry => entry.isStandard).sort((a, b) => a.sortOrder - b.sortOrder),
    alternatives: group.entries.filter(entry => !entry.isStandard).sort((a, b) => a.sortOrder - b.sortOrder)
  })));
  readonly planName = computed(() => this.data().mealPlans.find(plan => plan.id === this.unit()?.standardMealPlanId)?.name ?? 'Kein Standardplan');
  readonly catalog = signal(emptyRequirementCatalog);
  readonly collapsed = new Set<string>();
  readonly busy = signal(false); readonly error = signal('');
  readonly batchProgress = signal<{ done: number; total: number } | null>(null);
  readonly supply = viewChild(SupplySolutionsComponent);
  private readonly dialog = viewChild<ElementRef<HTMLDialogElement>>('settingsDialog');
  private readonly api = inject(MealPlanningApiService); private readonly destroyRef = inject(DestroyRef);
  private baseline = ''; private loadedKey = ''; private catalogCamp = '';
  draft: Settings = this.settings();
  constructor() {
    effect(() => {
      const data = this.data(), key = `${data.campId}:${this.unitId()}:${this.mealId()}`, state = this.state();
      untracked(() => {
        if (key !== this.loadedKey || !this.hasPendingEdits()) { this.loadedKey = key; this.restore(); }
        if (this.catalogCamp !== data.campId) { this.catalogCamp = data.campId; this.catalog.set(emptyRequirementCatalog); }
        if ((state?.supply?.requirements ?? state?.requirementGroups ?? []).some(group => hasNamedRequirements(group.requirements)) && this.catalog() === emptyRequirementCatalog) {
          // This existing endpoint enforces Health.Read; unavailable names stay generic.
          this.catalog.set({ dietTypes: [], allergens: [], substances: [] });
          this.api.requirementCatalog(data.campId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: catalog => this.catalog.set(catalog), error: () => {} });
        }
      });
    });
  }
  private settings(state?: CookingUnitMeal): Settings { return { subscriptionState: state?.subscriptionState ?? 0,
    demandOverride: state?.demandOverride ?? null, useStructureOverride: !!state?.structureOverrideNodeIds.length,
    structureOverrideNodeIds: [...(state?.structureOverrideNodeIds ?? [])], offerTargets: structuredClone(state?.offerTargets ?? []), recipeChoices: structuredClone(state?.recipeChoices ?? []), manualDemand: state?.demandOverride != null || (state?.offerTargets ?? []).some(value => value.targetOverride !== null), participantFilter: state?.participantFilterOverride == null ? 'standard' : String(state.participantFilterOverride), expectedVersion: state?.supplyVersion ?? 0, preference: this.savedPreference(state), resetOwnRecipes: false, resetAll: false }; }
  private savedPreference(state?: CookingUnitMeal) {
    const selections = state?.manualScenarioSelections ?? [];
    if (!selections.length) return 'standard';
    const ids: (string | null)[] = this.groups().length ? this.groups().map(value => value.id) : [null];
    const values = ids.map(id => selections.find(value => value.offerGroupId === id)?.preference);
    return values.every(value => value !== undefined && value === values[0]) ? String(values[0]) : 'mixed';
  }
  preferenceLabel(value = this.draft.preference) {
    return value === 'standard' ? 'Standard der Kocheinheit' : value === 'mixed' ? 'Unterschiedlich je Komponente' : ['Gemeinsam', 'Nachfragen', 'Getrennt'][Number(value)];
  }
  standardPreferenceLabel() { return ['Gemeinsam bevorzugen', 'Nachfragen', 'Getrennt bevorzugen'][this.unit()?.togetherPreference ?? 1]; }
  structureNames() {
    const ids = this.draft.useStructureOverride ? this.draft.structureOverrideNodeIds : this.unit()?.defaultStructureNodeIds ?? [];
    return ids.map(id => this.data().structureNodes.find(value => value.id === id)?.name ?? 'Strukturbereich');
  }
  setManualDemand(manual: boolean) {
    this.draft.manualDemand = manual;
    if (manual) this.draft.demandOverride ??= this.state()?.calculatedDemand ?? 0;
    else { this.draft.demandOverride = null; this.draft.offerTargets = []; }
  }
  setPreferenceOverride(override: boolean) { this.draft.preference = override ? String(this.unit()?.togetherPreference ?? 1) : 'standard'; }
  participantFilterLabel() { return this.draft.participantFilter === 'standard' ? 'Standard der Kocheinheit' : ['Alle Teilnehmer', 'Nur Sonderverpflegung', 'Ohne Sonderverpflegung'][Number(this.draft.participantFilter)]; }
  hasTargetOverrides() { return this.draft.offerTargets.some(value => value.targetOverride !== null); }
  useCustomStructure() { this.draft.useStructureOverride = true; if (!this.draft.structureOverrideNodeIds.length) this.draft.structureOverrideNodeIds = [...(this.unit()?.defaultStructureNodeIds ?? [])]; }
  stageRecipeReset() { if (this.writable()) { this.draft.resetOwnRecipes = true; this.error.set(''); } }
  resetAllAdjustments() {
    if (!this.writable()) return;
    this.resetPlan(); this.resetStructure(); this.draft.preference = 'standard'; this.draft.participantFilter = 'standard'; this.draft.resetOwnRecipes = true; this.draft.resetAll = true;
  }
  private restore() { this.draft = this.settings(this.state()); this.baseline = JSON.stringify(this.draft); }
  stateFor(mealId?: string) { return this.data().operationalMeals.find(state => state.cookingUnitId === this.unitId() && state.campMealId === mealId); }
  status = operativeMealStatus; verificationLabel = operativeVerificationLabel;
  calculationLabel() { return !this.state()?.calculatedAtUtc ? 'Nicht berechnet' : this.state()?.status === 1 ? 'Veraltet' : this.state()?.status === 2 ? 'Unvollständig' : 'Aktuell'; }
  verificationTone() {
    const state = this.state();
    return !state?.verification ? 'neutral' : state.verificationStale || state.status === 1 || state.verification.kind === 1 ? 'warning' : 'success';
  }
  mode() { return this.state()?.subscriptionState === 2 ? 'Keine Versorgung' : this.state()?.completeOwnSupply ? 'Eigene Versorgungslösung' : hasOwnRecipeSolutions(this.state(), this.unit()?.togetherPreference ?? 1) ? 'Eigene Versorgungslösung' : this.state()?.subscriptionState === 1 ? 'Individuelle Rezeptwahl (Bestand)' : 'Folgt dem Plan'; }
  recipeName(id: string) { return this.data().recipeOptions.find(recipe => recipe.revisionId === id)?.name ?? 'Gespeicherte Rezeptrevision'; }
  offers() { return this.groups().map(group => ({ id: group.id, name: group.name,
    alternativeRevisionIds: group.entries.filter(entry => !entry.isStandard).map(entry => entry.recipeRevisionId) })); }
  hasSettingsChanges() { return JSON.stringify(this.draft) !== this.baseline; }
  hasPendingEdits() { return this.hasSettingsChanges() || !!this.supply()?.hasUnsavedChanges(); }
  isBusy() { return this.busy() || !!this.supply()?.busy(); }
  discardChanges() { if (this.isBusy()) return; this.restore(); this.supply()?.discardChanges(); this.dialog()?.nativeElement.close(); }
  selectMeal(mealId?: string) {
    if (this.isBusy() || (this.hasPendingEdits() && !window.confirm('Ungespeicherte Mahlzeitenänderungen verwerfen?'))) return;
    this.discardChanges(); this.navigate.emit({ section: 'units', unitId: this.unitId(), mealId });
  }

  openSettings() { if (!this.isBusy()) this.dialog()?.nativeElement.showModal(); }
  changeSupplyMode(reset = false) {
    if (!this.writable()) return;
    if (this.hasPendingEdits()) { this.error.set('Ungespeicherte Änderungen zuerst speichern oder verwerfen.'); return; }
    this.error.set('');
    this.dialog()?.nativeElement.close();
    if (reset) this.supply()?.reset(true); else this.supply()?.openScenarioEditor();
  }
  editSupply() {
    if (!this.writable()) return;
    if (this.hasPendingEdits()) { this.error.set('Ungespeicherte Änderungen zuerst speichern oder verwerfen.'); return; }
    this.dialog()?.nativeElement.close(); this.supply()?.openEditor();
  }
  closeSettings() {
    if (this.isBusy() || (this.hasSettingsChanges() && !window.confirm('Ungespeicherte Anpassungen verwerfen?'))) return;
    this.restore(); this.dialog()?.nativeElement.close();
  }
  target(groupId: string) { return this.draft.offerTargets.find(target => target.offerGroupId === groupId)?.targetOverride ?? null; }
  setTarget(groupId: string, value: number | null) {
    const target = this.draft.offerTargets.find(target => target.offerGroupId === groupId);
    if (target) target.targetOverride = value; else this.draft.offerTargets.push({ id: crypto.randomUUID(), offerGroupId: groupId, targetOverride: value });
  }
  structure() { return new CookingUnitStructure(this.data().structureNodes); }
  toggleNode(id: string, checked: boolean) { this.draft.structureOverrideNodeIds = this.structure().toggle(id, this.draft.structureOverrideNodeIds, checked); }
  addRecipe() { const recipe = this.data().recipeOptions[0]; if (recipe) this.draft.recipeChoices.push({ id: crypto.randomUUID(), recipeRevisionId: recipe.revisionId, offerGroupId: null, mealPlanEntryId: null, sortOrder: this.draft.recipeChoices.length }); }
  private writable() { return !this.disabled() && !this.frozen() && !this.isBusy() && !!this.unit() && !!this.meal(); }
  saveSettings() {
    if (!this.writable()) return;
    if (this.supply()?.hasUnsavedChanges()) { this.error.set('Bitte zuerst die Versorgungsentscheidung speichern oder verwerfen.'); return; }
    if (this.draft.useStructureOverride && !this.draft.structureOverrideNodeIds.length) { this.error.set('Für die eigene Strukturzuordnung mindestens einen Bereich auswählen.'); return; }
    if ((this.draft.demandOverride !== null && (!Number.isFinite(this.draft.demandOverride) || this.draft.demandOverride < 0)) || this.draft.offerTargets.some(target => target.targetOverride !== null && (!Number.isFinite(target.targetOverride) || target.targetOverride < 0))) { this.error.set('Personen müssen eine gültige, nicht negative Zahl sein.'); return; }
    const draft = this.draft;
    let configured = false;
    const request = this.api.configureMeal(this.data().campId, this.unitId()!, this.mealId()!, { subscriptionState: draft.subscriptionState,
      demandOverride: draft.demandOverride, structureOverrideNodeIds: draft.useStructureOverride ? draft.structureOverrideNodeIds : [],
      offerTargets: draft.offerTargets, recipeChoices: draft.recipeChoices,
      supplyAdjustments: { expectedVersion: draft.expectedVersion,
        updatePreference: draft.resetAll || draft.preference !== (JSON.parse(this.baseline) as Settings).preference,
        preferenceOverride: ['standard', 'mixed'].includes(draft.preference) ? null : Number(draft.preference), resetRecipes: draft.resetOwnRecipes,
        retainRecipeSolutionIds: draft.resetAll ? [] : (this.state()?.manualSupplySolutions ?? []).filter(value =>
          isPlanRecipeSolution(value, this.state()!, this.unit()?.togetherPreference ?? 1, false)).map(value => value.id), updateParticipantFilter: draft.resetAll || draft.participantFilter !== (JSON.parse(this.baseline) as Settings).participantFilter,
        participantFilterOverride: draft.participantFilter === 'standard' ? null : Number(draft.participantFilter) }
    });
    this.mutate(draft.resetOwnRecipes ? request.pipe(tap(() => configured = true), switchMap(() => this.api.calculate(this.data().campId, this.unitId()!, this.mealId()!))) : request,
      draft.resetOwnRecipes ? 'Anpassungen gespeichert und automatische Versorgung neu berechnet.' : 'Anpassungen gespeichert. Bitte neu berechnen.', true,
      () => configured ? 'Anpassungen sind gespeichert. Neuberechnung fehlgeschlagen; bitte erneut berechnen.' : 'Anpassungen konnten nicht gespeichert werden.');
  }
  resetPlan() { this.draft.manualDemand = false; this.draft.subscriptionState = 0; this.draft.demandOverride = null; this.draft.offerTargets = []; this.draft.recipeChoices = []; }
  resetStructure() {
    if (!this.writable()) return;
    this.draft.useStructureOverride = false; this.draft.structureOverrideNodeIds = [];
  }
  calculate() {
    if (!this.writable()) return;
    if (this.hasPendingEdits()) { this.error.set('Ungespeicherte Änderungen zuerst speichern oder verwerfen. Es wurde nichts verworfen.'); return; }
    this.mutate(this.api.calculate(this.data().campId, this.unitId()!, this.mealId()!), 'Versorgung neu berechnet.');
  }
  async calculateAllOutdated() {
    const unitId = this.unit()?.id, campId = this.data().campId;
    if (!unitId || this.disabled() || this.frozen() || this.isBusy()) return;
    if (this.hasPendingEdits()) { this.error.set('Ungespeicherte Änderungen zuerst speichern oder verwerfen. Es wurde nichts verworfen.'); return; }
    this.busy.set(true); this.error.set('');
    let succeeded = 0; const failures: string[] = [];
    try {
      const fresh = await firstValueFrom(this.api.overview(campId).pipe(takeUntilDestroyed(this.destroyRef)));
      if (this.destroyRef.destroyed) return;
      if (!fresh.cookingUnits.some(unit => unit.id === unitId)) { this.error.set('Die Kocheinheit ist nicht mehr verfügbar. Bitte die Ansicht neu laden.'); return; }
      const queue = fresh.meals.filter(meal => meal.isActive && meal.isInsideCampPeriod).filter(meal => {
        const state = fresh.operationalMeals.find(value => value.cookingUnitId === unitId && value.campMealId === meal.id);
        return !state?.calculatedAtUtc || state.status === 1;
      }).sort((a, b) => a.date.localeCompare(b.date) || (a.sortOrder ?? 0) - (b.sortOrder ?? 0));
      this.batchProgress.set({ done: 0, total: queue.length });
      for (const meal of queue) {
        try {
          await firstValueFrom(this.api.calculate(campId, unitId, meal.id).pipe(takeUntilDestroyed(this.destroyRef)));
          succeeded++;
        } catch (error) {
          if (this.destroyRef.destroyed) return;
          failures.push(`${meal.date} · ${meal.mealTypeName}`);
          const failure = error as { status?: number; error?: { code?: string } };
          // Stop if authorization or freeze changed; do not repeatedly submit rejected writes.
          if (failure.status === 0 || failure.status === 401 || failure.status === 403 || failure.error?.code === 'camp_frozen') break;
        }
        if (this.destroyRef.destroyed) return;
        this.batchProgress.set({ done: succeeded + failures.length, total: queue.length });
      }
      const data = queue.length ? await firstValueFrom(this.api.overview(campId).pipe(takeUntilDestroyed(this.destroyRef))) : fresh;
      if (this.destroyRef.destroyed) return;
      const state = data.operationalMeals.find(value => value.cookingUnitId === unitId && value.campMealId === this.mealId());
      this.draft = this.settings(state); this.baseline = JSON.stringify(this.draft);
      this.changed.emit({ data, message: queue.length ? `${succeeded} von ${queue.length} Mahlzeiten neu berechnet.` : 'Keine veralteten oder unberechneten Mahlzeiten in dieser Kocheinheit.' });
      if (failures.length) this.error.set(`${succeeded} von ${queue.length} Mahlzeiten neu berechnet. Fehler bei: ${failures.join('; ')}. Bitte prüfen; es wurde kein automatischer Wiederholungsversuch gestartet.`);
    } catch {
      if (!this.destroyRef.destroyed) this.error.set(succeeded
        ? `${succeeded} Mahlzeiten wurden berechnet. Der aktualisierte Stand konnte nicht geladen werden. Bitte die Ansicht neu laden.`
        : 'Die Sammelberechnung konnte nicht vorbereitet oder aktualisiert werden. Bitte den Stand neu laden.');
    } finally {
      this.busy.set(false); this.batchProgress.set(null);
    }
  }
  refresh(message: string) { this.mutate(this.api.overview(this.data().campId), message, false); }
  supplySaved(recalculate: boolean) {
    if (!recalculate) { this.refresh('Versorgungsentscheidung gespeichert. Bitte neu berechnen.'); return; }
    if (!this.writable()) return;
    if (this.hasPendingEdits()) { this.error.set('Auswahl gespeichert. Weitere ungespeicherte Änderungen vor der Neuberechnung speichern oder verwerfen.'); return; }
    this.mutate(this.api.calculate(this.data().campId, this.unitId()!, this.mealId()!), 'Auswahl gespeichert und Versorgung neu berechnet.', true,
      'Die Auswahl ist gespeichert. Neuberechnung oder Laden fehlgeschlagen; bitte erneut berechnen.');
  }
  private mutate(request: Observable<unknown>, message: string, reload = true, failureMessage?: string | (() => string)) {
    if (this.isBusy()) return;
    this.busy.set(true); this.error.set('');
    (reload ? request.pipe(switchMap(() => this.api.overview(this.data().campId))) : request as Observable<MealPlanningOverview>)
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: data => {
        this.busy.set(false); this.dialog()?.nativeElement.close();
        const state = data.operationalMeals.find(state => state.cookingUnitId === this.unitId() && state.campMealId === this.mealId());
        this.draft = this.settings(state); this.baseline = JSON.stringify(this.draft); this.changed.emit({ data, message });
      }, error: error => { this.busy.set(false); this.error.set(failureMessage ? [typeof failureMessage === 'function' ? failureMessage() : failureMessage, error.error?.message].filter(Boolean).join(' ') : error.error?.message ?? 'Der Vorgang konnte nicht abgeschlossen werden. Bitte den gespeicherten Stand prüfen; der Entwurf bleibt erhalten.'); } });
  }
}
