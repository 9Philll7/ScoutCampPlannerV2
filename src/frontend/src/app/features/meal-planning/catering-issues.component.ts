import { Component, DestroyRef, effect, inject, input, output, signal, untracked } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActionIconComponent } from '../../shared/action-icon.component';
import { ListPaginationComponent } from '../../shared/list-pagination.component';
import { CateringTarget } from './catering-overview.component';
import { MealPlanningApiService, MealPlanningOverview, StructureConflict, StructureConflictDocument, StructureResolutionPreview, StructureResolutionRequest } from './meal-planning-api.service';

interface NutritionIssue { id: string; title: string; unitId: string; mealId: string; date: string; description: string; stale: boolean; }

@Component({
  selector: 'scp-catering-issues', standalone: true,
  imports: [DatePipe, FormsModule, MatButtonModule, ActionIconComponent, ListPaginationComponent],
  templateUrl: './catering-issues.component.html', styleUrl: './catering-issues.component.scss',
})
export class CateringIssuesComponent {
  readonly data = input.required<MealPlanningOverview>();
  readonly disabled = input(false);
  readonly frozen = input(false);
  readonly navigate = output<CateringTarget>();
  readonly changed = output<{ data: MealPlanningOverview; message: string }>();
  readonly document = signal<StructureConflictDocument | null>(null);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly selected = signal<StructureConflict | null>(null);
  readonly step = signal(0);
  readonly busy = signal(false);
  readonly previewLoading = signal(false);
  readonly preview = signal<StructureResolutionPreview | null>(null);
  readonly applied = signal(false);
  readonly page = signal(0);
  category = ''; groupId = ''; conflictKind = ''; date = ''; search = '';
  useDefaults = true;
  selectedUnitIds: string[] = [];
  selectedMeals: string[] = [];
  readonly previousStep = (step: number) => Math.max(0, step - 1);
  readonly steps = ['Konflikt und Ursache', 'Lösung anpassen', 'Auswirkungen prüfen'];
  private readonly api = inject(MealPlanningApiService);
  private readonly destroyRef = inject(DestroyRef);
  private loadRequest = 0;
  private previewRequest = 0;

  constructor() { effect(() => { this.data(); untracked(() => this.load()); }); }
  load() {
    const request = ++this.loadRequest; this.error.set('');
    const shared = this.data().structureConflicts;
    if (shared) { this.document.set(shared); this.loading.set(false); return; }
    this.loading.set(true);
    this.api.structureConflicts(this.data().campId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: value => { if (request === this.loadRequest) { this.document.set(value); this.loading.set(false); } },
      error: () => { if (request === this.loadRequest) { this.document.set(null); this.loading.set(false); this.error.set('Strukturkonflikte konnten nicht geladen werden. Bitte erneut laden.'); } }
    });
  }
  reload() {
    if (this.loading()) return;
    const request = ++this.loadRequest; this.loading.set(true); this.error.set('');
    this.api.overview(this.data().campId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: data => {
        if (request !== this.loadRequest) return;
        this.loading.set(false);
        if (data.structureConflicts) this.document.set(data.structureConflicts);
        this.changed.emit({ data, message: '' });
      },
      error: () => {
        if (request !== this.loadRequest) return;
        this.loading.set(false); this.error.set('Konflikte konnten nicht neu geladen werden.');
      }
    });
  }
  unitName(id: string) { return this.data().cookingUnits.find(value => value.id === id)?.name ?? 'Entfernte Kocheinheit'; }
  groupName(unitId: string) { const unit = this.data().cookingUnits.find(value => value.id === unitId); return this.data().cookingUnitGroups.find(value => value.id === unit?.groupId)?.name ?? 'Ohne organisatorische Gruppe'; }
  mealName(id: string) { const meal = this.data().meals.find(value => value.id === id); return meal ? `${meal.mealTypeName} · ${meal.date}` : 'Entfernte Mahlzeit'; }
  nodeNames(ids: string[]) { return ids.map(id => this.data().structureNodes.find(value => value.id === id)?.name ?? 'Entfernter Strukturknoten').join(', ') || 'Keine Strukturzuordnung'; }
  assignedNames(unitId: string) { return this.nodeNames(this.data().cookingUnits.find(value => value.id === unitId)?.defaultStructureNodeIds ?? []); }
  dates() { return [...new Set(this.data().meals.filter(value => value.isActive && value.isInsideCampPeriod).map(value => value.date))].sort(); }
  nutritionIssues(): NutritionIssue[] {
    return this.data().operationalMeals.flatMap(state => {
      const meal = this.data().meals.find(value => value.id === state.campMealId);
      if (!meal?.isActive || !meal.isInsideCampPeriod || state.subscriptionState === 2) return [];
      const problems = state.supply?.problems.filter(value => value.code !== 18) ?? [];
      const decision = problems.some(value => value.code === 19) || !!state.supply?.commonSupplyChoices?.length || !!state.supply?.decisions?.length;
      if (!problems.length && !decision) return [];
      const title = problems.some(value => value.code === 16) ? 'Überbelegung der Versorgung' : problems.some(value => value.code === 17) ? 'Personenzuordnung der Rezeptlösung offen' : problems.some(value => value.code === 10) ? 'Allergenkonflikt' : problems.some(value => value.code === 12) ? 'Ernährungsanforderung nicht abgedeckt' :
        problems.some(value => [5,6,7,8,9].includes(value.code)) ? 'Unverträglichkeit prüfen' : decision ? 'Versorgungsentscheidung offen' : problems.some(value => value.code === 15) ? 'Versorgung nicht vollständig abgedeckt' : 'Versorgungslösung prüfen';
      return [{ id: state.id, title, unitId: state.cookingUnitId, mealId: meal.id, date: meal.date,
        description: this.unitName(state.cookingUnitId) + ' · ' + meal.mealTypeName, stale: state.status === 1 }];
    });
  }
  affectedMealIds(conflict: StructureConflict) {
    return this.data().meals.filter(value => conflict.mealIds.includes(value.id))
      .sort((a, b) => a.date.localeCompare(b.date) || (a.sortOrder ?? 0) - (b.sortOrder ?? 0) || a.mealTypeName.localeCompare(b.mealTypeName, 'de'))
      .map(value => value.id);
  }
  orderedChanges() {
    return [...(this.preview()?.changes ?? [])].sort((a, b) => {
      const first = this.data().meals.find(value => value.id === a.mealId);
      const second = this.data().meals.find(value => value.id === b.mealId);
      return (first?.date ?? '').localeCompare(second?.date ?? '') || (first?.sortOrder ?? 0) - (second?.sortOrder ?? 0);
    });
  }
  private unitMatches(ids: string[]) { return !this.groupId || ids.some(id => this.data().cookingUnits.find(value => value.id === id)?.groupId === this.groupId); }
  private searchMatches(value: string) { return value.toLocaleLowerCase('de').includes(this.search.trim().toLocaleLowerCase('de')); }
  structureIssues() {
    return (this.document()?.conflicts ?? []).filter(value => this.unitMatches(value.cookingUnitIds) && (!this.conflictKind || value.kind === this.conflictKind) &&
      (!this.date || value.mealIds.some(id => this.data().meals.find(meal => meal.id === id)?.date === this.date)) &&
      this.searchMatches(value.nodeName + ' ' + value.cookingUnitIds.map(id => this.unitName(id) + ' ' + this.groupName(id)).join(' ')));
  }
  filteredNutrition() { return this.nutritionIssues().filter(value => this.unitMatches([value.unitId]) && (!this.date || value.date === this.date) && this.searchMatches(value.title + ' ' + value.description + ' ' + this.groupName(value.unitId))); }
  rows() {
    return [...(this.category === 'nutrition' || this.conflictKind === 'nutrition' ? [] : this.structureIssues().map(value => ({ id: value.id, structure: value, nutrition: null as NutritionIssue | null }))),
      ...(this.category === 'structure' || ['overlap','missing'].includes(this.conflictKind) ? [] : this.filteredNutrition().map(value => ({ id: value.id, structure: null as StructureConflict | null, nutrition: value })))];
  }
  visibleRows() { return this.rows().slice(this.page() * 7, (this.page() + 1) * 7); }
  resetPage() { this.page.set(0); }
  selectCategory(value: string) { this.category = value; this.page.set(0); }
  open(conflict: StructureConflict) {
    if (this.disabled() || this.frozen() || this.busy()) return;
    this.selected.set(conflict); this.step.set(0); this.useDefaults = conflict.kind === 'overlap'; this.selectedUnitIds = conflict.cookingUnitIds.slice(0, 1);
    this.selectedMeals = this.affectedMealIds(conflict); this.preview.set(null); this.applied.set(false); this.error.set(''); this.updatePreview();
  }
  hasStructureNode(conflict: StructureConflict) { return this.data().structureNodes.some(node => node.id === conflict.nodeId); }
  candidateUnitIds(_conflict: StructureConflict) { return this.data().cookingUnits.map(unit => unit.id); }
  toggleUnit(id: string, selected: boolean) {
    this.selectedUnitIds = selected ? [...new Set([...this.selectedUnitIds, id])] : this.selectedUnitIds.filter(value => value !== id);
    this.updatePreview();
  }
  selectedUnitNames() { return this.selectedUnitIds.map(id => this.unitName(id)).join(', '); }
  unitFilterLabel(id: string) {
    const filter = this.data().cookingUnits.find(unit => unit.id === id)?.participantFilter ?? 0;
    return filter === 1 ? 'Nur Sonderverpflegung' : filter === 2 ? 'Nur Standardverpflegung' : 'Alle Teilnehmer';
  }
  canAdvance() {
    if (this.busy() || this.previewLoading()) return false;
    const conflict = this.selected();
    if (this.step() === 0 && conflict) return this.hasStructureNode(conflict) && this.candidateUnitIds(conflict).length > 0;
    return this.preview() !== null;
  }
  close() { if (!this.busy()) { ++this.previewRequest; this.selected.set(null); this.preview.set(null); this.error.set(''); } }
  hasPendingEdits() {
    const conflict = this.selected();
    if (!conflict || this.applied()) return false;
    return this.useDefaults !== (conflict.kind === 'overlap') ||
      JSON.stringify([...this.selectedUnitIds].sort()) !== JSON.stringify(conflict.cookingUnitIds.slice(0, 1).sort()) ||
      !this.useDefaults && JSON.stringify([...this.selectedMeals].sort()) !== JSON.stringify(this.affectedMealIds(conflict).sort());
  }
  discardChanges() { this.close(); }
  request(): StructureResolutionRequest {
    return { expectedVersion: this.document()!.version, conflictId: this.selected()!.id, keepCookingUnitId: this.selectedUnitIds[0] ?? '', cookingUnitIds: [...this.selectedUnitIds], useDefaults: this.useDefaults, mealIds: [...this.selectedMeals] };
  }
  toggleMeal(id: string, checked: boolean) { this.selectedMeals = checked ? [...this.selectedMeals, id] : this.selectedMeals.filter(value => value !== id); this.updatePreview(); }
  updatePreview() {
    const request = ++this.previewRequest; this.preview.set(null); this.error.set('');
    if (!this.document() || !this.selected() || !this.selectedUnitIds.length || !this.hasStructureNode(this.selected()!) || !this.useDefaults && !this.selectedMeals.length) { this.previewLoading.set(false); return; }
    this.previewLoading.set(true);
    this.api.previewStructureResolution(this.data().campId, this.request()).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: value => { if (request === this.previewRequest) { this.preview.set(value); this.previewLoading.set(false); } },
      error: () => { if (request === this.previewRequest) { this.previewLoading.set(false); this.error.set('Die Vorschau ist nicht mehr aktuell oder konnte nicht geladen werden. Zurück zur Übersicht und neu laden.'); } }
    });
  }
  canApply() { return !!this.preview()?.changes.length && this.preview()!.newConflicts === 0 && !this.preview()!.overlappingMeals && !this.preview()!.overlappingFilters && this.preview()!.resolved > 0 && !this.previewLoading() && !this.busy() && !this.disabled() && !this.frozen() && !this.applied(); }
  next() { if (this.step() < this.steps.length - 1 && this.canAdvance()) this.step.update(value => value + 1); }
  apply() {
    if (this.step() !== this.steps.length - 1 || !this.canApply()) return;
    this.busy.set(true); this.error.set('');
    this.api.applyStructureResolution(this.data().campId, this.request()).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.applied.set(true);
        this.api.overview(this.data().campId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
          next: data => { this.busy.set(false); this.selected.set(null); this.changed.emit({ data, message: 'Strukturzuordnung gespeichert. Betroffene Versorgungen bitte neu berechnen.' }); },
          error: () => { this.busy.set(false); this.error.set('Zuordnung gespeichert. Bitte die Seite neu laden, um den aktuellen Stand anzuzeigen.'); }
        });
      },
      error: error => { this.busy.set(false); this.preview.set(null); this.error.set(error.error?.message ?? 'Die Zuordnung wurde inzwischen geändert oder konnte nicht gespeichert werden. Bitte neu laden.'); }
    });
  }
}
