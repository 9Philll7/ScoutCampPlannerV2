import { Component, computed, input, output, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { ActionIconComponent } from '../../shared/action-icon.component';
import { ListPaginationComponent } from '../../shared/list-pagination.component';
import { CookingUnitMeal, MealPlanningOverview } from './meal-planning-api.service';

export type CateringSection = 'overview' | 'plans' | 'units' | 'issues';
export interface CateringTarget { section: CateringSection; planId?: string; unitId?: string; mealId?: string; }
type ActionKind = 'conflict' | 'decision' | 'planning' | 'calculation' | 'verification';
interface CateringAction {
  id: string; kind: ActionKind; title: string; description: string; context: string;
  date?: string; stale?: boolean; target: CateringTarget;
}

/** Read-only projection of the existing planning contract. No suitability or demand calculation. */
export function cateringSummary(data: MealPlanningOverview) {
  const meals = data.meals.filter(meal => meal.isActive && meal.isInsideCampPeriod);
  const states = data.operationalMeals.filter(state => meals.some(meal => meal.id === state.campMealId) &&
    data.cookingUnits.some(unit => unit.id === state.cookingUnitId) && state.subscriptionState !== 2);
  const actions: CateringAction[] = [];
  const conflicts = (state: CookingUnitMeal) => (state.supply?.problems ?? []).filter(problem => problem.code !== 19);
  const decisions = (state: CookingUnitMeal) => Math.max(state.supply?.commonSupplyChoices?.length ?? 0,
    state.supply?.problems.filter(problem => problem.code === 19).length ?? 0);
  for (const state of states) {
    const meal = meals.find(value => value.id === state.campMealId)!;
    const unit = data.cookingUnits.find(value => value.id === state.cookingUnitId)!;
    const target: CateringTarget = { section: 'units', unitId: unit.id, mealId: meal.id };
    const base = { context: `${unit.name} · ${meal.mealTypeName}`, date: meal.date,
      stale: state.status === 1, target };
    if (conflicts(state).length || state.participantProblems?.length || state.unassignedParticipantIds?.length) {
      actions.push({ ...base, id: `conflict-${state.id}`, kind: 'conflict', title: 'Versorgung prüfen',
        description: state.participantProblems?.length || state.unassignedParticipantIds?.length
          ? 'Die Teilnehmerzuordnung ist nicht eindeutig oder unvollständig.'
          : `${conflicts(state).length} offene Versorgungsprobleme im gespeicherten Teilstand.` });
    }
    if (decisions(state)) actions.push({ ...base, id: `decision-${state.id}`, kind: 'decision',
      title: 'Gemeinsam oder getrennt?', description: `${decisions(state)} ausstehende Entscheidung(en) zur gemeinsamen Versorgung.` });
    if (!state.calculatedAtUtc || state.status !== 0) actions.push({ ...base, id: `calculation-${state.id}`, kind: 'calculation',
      title: !state.calculatedAtUtc ? 'Berechnung ausstehend' : state.status === 1 ? 'Berechnung veraltet' : 'Berechnung unvollständig',
      description: 'Mahlzeit öffnen und den Bedarf sowie den Berechnungsstand prüfen.' });
    if (state.supply && state.calculatedAtUtc && (!state.verification || state.verificationStale)) {
      actions.push({ ...base, id: `verification-${state.id}`, kind: 'verification',
        title: state.verificationStale ? 'Verifikation veraltet' : 'Verifikation ausstehend',
        description: 'Die fachliche Prüfung ist separat von der Berechnung erforderlich.' });
    }
  }
  for (const plan of data.mealPlans.filter(plan => plan.missingActiveMealCount > 0)) {
    actions.push({ id: `plan-${plan.id}`, kind: 'planning', title: 'Standardangebot fehlt', context: plan.name,
      description: `${plan.missingActiveMealCount} aktive Mahlzeit(en) ohne Standardangebot.`, target: { section: 'plans', planId: plan.id } });
  }
  for (const warning of new Set(data.coverageWarnings)) actions.push({ id: `coverage-${warning}`, kind: 'conflict',
    title: 'Strukturabdeckung prüfen', description: warning, context: 'Kocheinheiten', target: { section: 'units' } });
  // Missing states are unknown, never implicitly current or verified.
  for (const unit of data.cookingUnits) {
    const missing = meals.filter(meal => !data.operationalMeals.some(state => state.cookingUnitId === unit.id && state.campMealId === meal.id));
    if (missing.length) actions.push({ id: `missing-${unit.id}`, kind: 'calculation', title: 'Berechnungsstand fehlt',
      context: unit.name, description: `${missing.length} aktive Mahlzeit(en) ohne gespeicherten Teilstand.`,
      target: { section: 'units', unitId: unit.id, mealId: missing[0].id } });
  }
  const priority: Record<ActionKind, number> = { conflict: 0, decision: 1, planning: 2, calculation: 3, verification: 4 };
  actions.sort((a, b) => priority[a.kind] - priority[b.kind] || (a.date ?? '').localeCompare(b.date ?? '') || a.context.localeCompare(b.context, 'de'));
  return {
    actions, activeMeals: meals.length,
    missingOffers: data.mealPlans.reduce((sum, plan) => sum + plan.missingActiveMealCount, 0),
    subscribedUnits: data.cookingUnits.filter(unit => !!unit.standardMealPlanId).length,
    conflictMeals: states.filter(state => conflicts(state).length || state.participantProblems?.length || state.unassignedParticipantIds?.length).length,
    decisions: states.reduce((sum, state) => sum + decisions(state), 0),
    pendingVerification: actions.filter(action => action.kind === 'verification').length,
    current: states.filter(state => state.status === 0 && !!state.calculatedAtUtc).length,
    stale: states.filter(state => state.status === 1).length,
    incomplete: states.filter(state => state.status === 2 && !!state.calculatedAtUtc).length,
    verified: states.filter(state => !!state.verification && !state.verificationStale).length,
    noSupply: data.operationalMeals.filter(state => state.subscriptionState === 2 && meals.some(meal => meal.id === state.campMealId) && data.cookingUnits.some(unit => unit.id === state.cookingUnitId)).length,
    uncalculated: data.cookingUnits.length * meals.length - data.operationalMeals.filter(state => meals.some(meal => meal.id === state.campMealId) && data.cookingUnits.some(unit => unit.id === state.cookingUnitId) && (state.subscriptionState === 2 || !!state.calculatedAtUtc)).length,
  };
}

@Component({
  selector: 'scp-catering-overview', standalone: true,
  imports: [DatePipe, FormsModule, MatButtonModule, ActionIconComponent, ListPaginationComponent],
  templateUrl: './catering-overview.component.html', styleUrl: './catering-overview.component.scss',
})
export class CateringOverviewComponent {
  readonly data = input.required<MealPlanningOverview>();
  readonly issuesOnly = input(false);
  readonly navigate = output<CateringTarget>();
  readonly summary = computed(() => cateringSummary(this.data()));
  readonly page = signal(0);
  filter = '';
  readonly categories = [
    { id: 'conflict', label: 'Konflikte' }, { id: 'decision', label: 'Entscheidungen' },
    { id: 'planning', label: 'Menüplanung' }, { id: 'calculation', label: 'Berechnung' },
    { id: 'verification', label: 'Verifikation' },
  ];
  actions() { return this.summary().actions.filter(action => !this.filter || action.kind === this.filter); }
  visibleActions() { return this.actions().slice(this.issuesOnly() ? this.page() * 8 : 0, this.issuesOnly() ? (this.page() + 1) * 8 : 5); }
  label(kind: ActionKind) { return this.categories.find(category => category.id === kind)!.label; }
}
