import { Component, DestroyRef, effect, inject, input, output, signal, untracked, viewChild } from '@angular/core';
import { Observable, switchMap } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NotificationBannerComponent } from '../../shared/notification-banner.component';
import { CateringOverviewComponent, CateringSection, CateringTarget } from './catering-overview.component';
import { MealPlanWorkspaceComponent, MealScheduleAction } from './meal-plan-workspace.component';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActionIconComponent } from '../../shared/action-icon.component';
import { CateringIssuesComponent } from './catering-issues.component';
import { CookingUnitsWorkspaceComponent } from './cooking-units-workspace.component';
import { OperativeMealWorkspaceComponent } from './operative-meal-workspace.component';
import {
  CookingUnit, CookingUnitMeal, MealPlanDocument,
  MealPlanningApiService, MealPlanningOverview, MealSlot,
  MealSubscriptionState, OfferTarget, RecipeChoice,
} from './meal-planning-api.service';

interface MealDraft {
  subscriptionState: MealSubscriptionState;
  demandOverride: number | null;
  useStructureOverride: boolean;
  structureOverrideNodeIds: string[];
  offerTargets: OfferTarget[];
  recipeChoices: RecipeChoice[];
}

@Component({
  selector: 'scp-meal-planning',
  standalone: true,
  imports: [MatButtonModule, MatTooltipModule, ActionIconComponent, CateringOverviewComponent,
    MealPlanWorkspaceComponent, NotificationBannerComponent, CookingUnitsWorkspaceComponent, OperativeMealWorkspaceComponent, CateringIssuesComponent],
  template: `
    <section class="meal-planning">
      <div class="section-heading" [hidden]="section() === 'plans' || section() === 'units' || section() === 'issues'">
        <div><p class="eyebrow">Verpflegung</p><h2>{{ sectionTitle() }}</h2></div>
        <button matIconButton type="button" matTooltip="Neu laden" aria-label="Neu laden" (click)="reload()">
          <scp-action-icon name="refresh"/>
        </button>
      </div>
      @if (error() || notice()) { <div class="planning-notifications">
        @if (error()) { <scp-notification-banner [message]="error()" kind="error" (dismissed)="error.set('')"/> }
        @if (notice()) { <scp-notification-banner [message]="notice()" (dismissed)="notice.set('')"/> }
      </div> }
      @if (overview(); as data) {
        @if (section() === 'overview') {
          <scp-catering-overview [data]="data" [issuesOnly]="section() === 'issues'" (navigate)="navigate.emit($event)"/>
        }
        @if (section() === 'issues') {
          <scp-catering-issues [data]="data" [disabled]="disabled()" [frozen]="frozen()" (changed)="unitWorkspaceChanged($event)" (navigate)="navigate.emit($event)"/>
        }
        @if (section() === 'units' && !target()?.mealId) {
          <scp-cooking-units-workspace [data]="data" [disabled]="disabled()" [targetUnitId]="target()?.unitId"
            (changed)="unitWorkspaceChanged($event)" (navigate)="navigate.emit($event)"/>
        }
        @if (section() === 'plans') {
          <scp-meal-plan-workspace [data]="data" [plan]="editingPlan()" [disabled]="disabled()" [busy]="planBusy()" [planDirty]="hasPendingPlanChanges()" (schedule)="changeSchedule($event)" (refreshSchedule)="load()"
            (open)="openPlan($event)" (close)="cancelPlan()" (save)="savePlan()" (create)="createPlan($event)"
            (remove)="deletePlan($event)" (reorder)="movePlan($event.index, $event.direction)"/>
        }
        @if (section() === 'units' && target()?.mealId) {
          <scp-operative-meal-workspace [data]="data" [unitId]="target()?.unitId" [mealId]="target()?.mealId"
            [disabled]="disabled()" [frozen]="frozen()" (changed)="unitWorkspaceChanged($event)" (navigate)="navigate.emit($event)"/>
        }
      } @else if (!error()) { <p role="status">Verpflegung wird geladen …</p> }
    </section>
  `,
  styles: `
    .planning-notifications { position:fixed; z-index:1000; top:16px; right:24px; width:min(560px,calc(100vw - 32px)); display:grid; gap:8px; }
    .meal-planning { display:grid; gap:1rem; }
    .section-heading[hidden] { display:none; }
    .section-heading { display:flex; justify-content:space-between; align-items:center; gap:1rem; }
    h2,p { margin:0; } h2 { font-size:28px; line-height:1.2; } .eyebrow { text-transform:uppercase; letter-spacing:.08em; font-size:.72rem; }
  `,
})
export class MealPlanningComponent {
  supplySaved() { this.changed('Versorgungsentscheidungen gespeichert. Bitte neu berechnen.'); }
  supplyVerified() { this.changed('Verifikation gespeichert.'); }
  readonly campId = input.required<string>();
  readonly disabled = input(false);
  readonly frozen = input(false);
  readonly section = input<CateringSection>('overview');
  readonly target = input<CateringTarget | null>(null);
  readonly navigate = output<CateringTarget>();
  readonly scheduleChanged = output<void>();
  sectionTitle() { return { overview: 'Verpflegung – Übersicht', plans: 'Menüpläne', units: 'Kocheinheiten', issues: 'Konflikte & Entscheidungen' }[this.section()]; }
  visibleUnits(data: MealPlanningOverview) { return data.cookingUnits.filter(unit => !this.target()?.unitId || unit.id === this.target()?.unitId); }
  visibleMeals(data: MealPlanningOverview) { return this.activeMeals(data).filter(meal => !this.target()?.mealId || meal.id === this.target()?.mealId); }

  private readonly api = inject(MealPlanningApiService);
  private readonly destroyRef = inject(DestroyRef);
  readonly overview = signal<MealPlanningOverview | null>(null);
  readonly editingPlan = signal<MealPlanDocument | null>(null);
  readonly error = signal(''); readonly notice = signal('');
  readonly planBusy = signal(false);
  private planBaseline = '';
  readonly planWorkspace = viewChild(MealPlanWorkspaceComponent);
  private mealDraftMap = new Map<string, MealDraft>();

  constructor() {
    effect(() => { this.campId(); this.overview.set(null); this.discardPlan(); this.load(); });
    effect(() => { const target = this.target(); untracked(() => { if (target?.planId) this.openPlan(target.planId); else if (target?.section === 'plans') this.discardPlan(); }); });
  }
  load() { this.api.overview(this.campId()).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: value => { this.overview.set(value); this.buildMealDrafts(value); this.error.set(''); }, error: () => this.error.set('Die Mahlzeitenplanung konnte nicht geladen werden.') }); }
  activeMeals(data: MealPlanningOverview) { return data.meals.filter(value => value.isActive && value.isInsideCampPeriod); }
  mealLabel(data: MealPlanningOverview, id: string) { const meal = data.meals.find(value => value.id === id); return meal ? `${meal.date} · ${meal.mealTypeName}` : 'Unbekannte Mahlzeit'; }
  hasPendingPlanChanges() { return this.editingPlan() !== null && JSON.stringify(this.editingPlan()) !== this.planBaseline; }
  discardPlan() { this.editingPlan.set(null); this.planBaseline = ''; }
  private mayDiscardPlan() { return !this.hasPendingPlanChanges() || window.confirm('Nicht gespeicherte Menüplanänderungen verwerfen?'); }
  reload() { if (this.planBusy() || !this.mayDiscardPlan()) return; this.discardPlan(); this.load(); }
  changeSchedule(action: MealScheduleAction) {
    const data = this.overview();
    if (!data || this.disabled() || this.planBusy() || ((action.kind === 'add' || action.kind === 'remove') && this.hasPendingPlanChanges()) || data.scheduleVersion === undefined) return;
    const campId = this.campId(), version = data.scheduleVersion;
    const request: Observable<unknown> = action.kind === 'add' ? this.api.addAdditionalMeal(campId, action.date, action.name, version)
      : action.kind === 'order' ? this.api.orderDayMeals(campId, action.date, action.mealIds, version)
      : action.kind === 'remove' ? this.api.removeAdditionalMeal(campId, action.mealId, version)
      : this.api.setMealActivity(campId, action.mealId, action.isActive, version);
    this.planBusy.set(true);
    let successMessage = 'Mahlzeitenrahmen gespeichert.';
    request.pipe(switchMap(result => {
      if (action.kind === 'remove') successMessage = (result as { outcome: string }).outcome === 'Deactivated'
        ? 'Zusatzmahlzeit deaktiviert. Vorhandene Zuordnungen bleiben erhalten.' : 'Zusatzmahlzeit entfernt.';
      return this.api.overview(campId);
    }), takeUntilDestroyed(this.destroyRef)).subscribe({
      next: value => {
        if (campId !== this.campId()) return;
        this.overview.set(value); this.buildMealDrafts(value); this.planBusy.set(false); this.error.set('');
        if (action.kind === 'add' && this.planWorkspace()) this.planWorkspace()!.additionalMealName = '';
        this.notice.set(successMessage); this.scheduleChanged.emit();
      },
      error: error => {
        this.planBusy.set(false);
        const code = error.error?.code;
        this.error.set(code === 'meal_schedule_changed' ? 'Der Mahlzeitenrahmen wurde inzwischen geändert. Bitte neu laden; dein Menüplanentwurf bleibt erhalten.'
          : code === 'meal_already_exists' ? 'Diese Mahlzeit existiert an diesem Tag bereits. Gegebenenfalls wieder aktivieren.'
          : code === 'meal_name_conflict' ? 'Diese Bezeichnung wird bereits für eine regelmäßige Mahlzeit verwendet.'
          : 'Der Mahlzeitenrahmen konnte nicht gespeichert oder neu geladen werden. Bitte den Stand prüfen.');
      }
    });
  }
  createPlan(name: string) {
    if (this.disabled() || this.planBusy() || !name.trim()) return;
    this.planBusy.set(true);
    this.api.createPlan(this.campId(), name.trim()).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => { this.planBusy.set(false); this.changed('Menüplan angelegt.'); }, error: error => { this.planBusy.set(false); this.failed(error, 'Der Menüplan konnte nicht angelegt werden.'); } });
  }
  movePlan(index: number, direction: number) {
    const data = this.overview(); if (!data || this.disabled() || this.planBusy()) return;
    const plans = [...data.mealPlans]; const target = index + direction;
    if (index < 0 || index >= plans.length || target < 0 || target >= plans.length) return;
    [plans[index], plans[target]] = [plans[target], plans[index]]; this.planBusy.set(true);
    this.api.reorderPlans(this.campId(), plans.map(value => value.id)).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => { this.planBusy.set(false); this.changed('Planreihenfolge gespeichert.'); }, error: error => { this.planBusy.set(false); this.failed(error, 'Die Planreihenfolge konnte nicht gespeichert werden.'); } });
  }
  openPlan(id: string) {
    if (this.planBusy() || !this.mayDiscardPlan()) return;
    this.planBusy.set(true);
    this.api.plan(this.campId(), id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: value => {
      this.editingPlan.set(structuredClone(value)); this.planBaseline = JSON.stringify(value); this.planBusy.set(false);
    }, error: () => { this.planBusy.set(false); this.error.set('Der Menüplan konnte nicht geöffnet werden.'); } });
  }
  cancelPlan() { if (!this.planBusy() && this.mayDiscardPlan()) this.discardPlan(); }
  savePlan() {
    const plan = this.editingPlan(); if (!plan || this.disabled() || this.planBusy() || !plan.name.trim()) return;
    const empty = plan.offerGroups.filter(group => !group.entries.length).length;
    const saved = structuredClone(plan); saved.offerGroups = saved.offerGroups.filter(group => group.entries.length);
    this.planBusy.set(true);
    this.api.savePlan(this.campId(), saved).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => {
      this.planBusy.set(false); this.discardPlan(); this.changed(empty ? `Menüplan gespeichert. ${empty} leere Komponente(n) wurden ausgelassen.` : 'Menüplan gespeichert.');
    }, error: error => { this.planBusy.set(false); this.failed(error, 'Der Menüplan konnte nicht gespeichert werden. Der Entwurf bleibt erhalten.'); } });
  }
  deletePlan(id: string) {
    if (this.disabled() || this.planBusy() || !window.confirm('Diesen Menüplan löschen?')) return;
    this.planBusy.set(true);
    this.api.deletePlan(this.campId(), id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => { this.planBusy.set(false); this.changed('Menüplan gelöscht.'); }, error: error => { this.planBusy.set(false); this.failed(error, 'Der Menüplan konnte nicht gelöscht werden.'); } });
  }
  readonly issuesWorkspace = viewChild(CateringIssuesComponent);
  readonly unitWorkspace = viewChild(CookingUnitsWorkspaceComponent);
  readonly operativeWorkspace = viewChild(OperativeMealWorkspaceComponent);
  hasPendingUnitChanges() { return !!this.issuesWorkspace()?.hasPendingEdits() || !!this.unitWorkspace()?.hasPendingEdits() || !!this.operativeWorkspace()?.hasPendingEdits(); }
  unitBusy() { return !!this.issuesWorkspace()?.busy() || !!this.unitWorkspace()?.busy() || !!this.unitWorkspace()?.managementBusy() || !!this.operativeWorkspace()?.isBusy(); }
  discardUnitChanges() { this.issuesWorkspace()?.discardChanges(); this.unitWorkspace()?.discardChanges(); this.operativeWorkspace()?.discardChanges(); }
  unitWorkspaceChanged(event: { data: MealPlanningOverview; message: string }) {
    this.overview.set(event.data); this.buildMealDrafts(event.data); this.error.set('');
    if (event.message) this.notice.set(event.message);
  }
  mealDraft(unitId: string, mealId: string) { return this.mealDraftMap.get(`${unitId}:${mealId}`); }
  stateFor(data: MealPlanningOverview, unitId: string, mealId: string) { return data.operationalMeals.find(value => value.cookingUnitId === unitId && value.campMealId === mealId); }
  saveMeal(unit: CookingUnit, meal: MealSlot, draft: MealDraft) { this.api.configureMeal(this.campId(), unit.id, meal.id, { subscriptionState: draft.subscriptionState, demandOverride: draft.demandOverride, structureOverrideNodeIds: draft.useStructureOverride ? draft.structureOverrideNodeIds : [], offerTargets: draft.offerTargets, recipeChoices: draft.recipeChoices }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => this.changed('Mahlzeiteneinstellungen gespeichert.'), error: error => this.failed(error, 'Die Mahlzeiteneinstellungen konnten nicht gespeichert werden.') }); }
  resetPlan(unit: CookingUnit, meal: MealSlot, draft: MealDraft) { draft.subscriptionState = 0; draft.demandOverride = null; draft.offerTargets = []; draft.recipeChoices = []; this.saveMeal(unit, meal, draft); }
  resetStructure(unitId: string, mealId: string) { this.api.resetStructure(this.campId(), unitId, mealId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => this.changed('Strukturabweichung zurückgesetzt.'), error: error => this.failed(error, 'Die Strukturabweichung konnte nicht zurückgesetzt werden.') }); }
  calculate(unitId: string, mealId: string) {
    if (this.operativeWorkspace()) { this.operativeWorkspace()!.calculate(); return; }
    this.api.calculate(this.campId(), unitId, mealId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => this.changed('Verpflegungsteilstand berechnet.'), error: error => this.failed(error, 'Der Verpflegungsteilstand konnte nicht berechnet werden.') });
  }
  offerGroupsFor(data: MealPlanningOverview, unit: CookingUnit, mealId: string) { const planId = unit.standardMealPlanId; return data.mealPlanDocuments.find(value => value.id === planId)?.offerGroups.filter(value => value.campMealId === mealId) ?? []; }
  offerTarget(draft: MealDraft, groupId: string) { return draft.offerTargets.find(value => value.offerGroupId === groupId)?.targetOverride ?? null; }
  setOfferTarget(draft: MealDraft, groupId: string, value: number | null) { const existing = draft.offerTargets.find(item => item.offerGroupId === groupId); if (existing) existing.targetOverride = value; else draft.offerTargets.push({ id: crypto.randomUUID(), offerGroupId: groupId, targetOverride: value }); }
  addChoice(draft: MealDraft, data: MealPlanningOverview) { draft.recipeChoices.push({ id: crypto.randomUUID(), recipeRevisionId: data.recipeOptions[0].revisionId, offerGroupId: null, mealPlanEntryId: null, sortOrder: draft.recipeChoices.length }); }
  statusLabel(value: number | undefined) { return value === 0 ? 'Aktuell' : value === 1 ? 'Veraltet' : 'Unvollständig'; }
  statusClass(value: number | undefined) { return value === 0 ? 'current' : value === 1 ? 'stale' : 'incomplete'; }
  private buildMealDrafts(data: MealPlanningOverview) { this.mealDraftMap = new Map(); for (const unit of data.cookingUnits) for (const meal of data.meals.filter(value => value.isActive)) { const state = this.stateFor(data, unit.id, meal.id); this.mealDraftMap.set(`${unit.id}:${meal.id}`, { subscriptionState: state?.subscriptionState ?? 0, demandOverride: state?.demandOverride ?? null, useStructureOverride: !!state?.structureOverrideNodeIds.length, structureOverrideNodeIds: [...(state?.structureOverrideNodeIds ?? [])], offerTargets: structuredClone(state?.offerTargets ?? []), recipeChoices: structuredClone(state?.recipeChoices ?? []) }); } }
  private changed(message: string) { this.notice.set(message); this.error.set(''); this.load(); }
  private failed(error: { error?: { message?: string; references?: string[] } }, fallback: string) { const details = error.error?.references?.join(' · '); this.error.set([error.error?.message || fallback, details].filter(Boolean).join(' ')); }
}
