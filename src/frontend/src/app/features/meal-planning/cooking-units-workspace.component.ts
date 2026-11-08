import { Component, DestroyRef, effect, inject, input, output, signal, untracked, viewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { NgTemplateOutlet } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Observable, switchMap } from 'rxjs';
import { MatButtonModule } from '@angular/material/button';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActionIconComponent } from '../../shared/action-icon.component';
import { NotificationBannerComponent } from '../../shared/notification-banner.component';
import { ParticipantPlanningComponent } from './participant-planning.component';
import { CookingUnit, MealPlanningApiService, MealPlanningOverview, MutationResult } from './meal-planning-api.service';
import { CateringTarget } from './catering-overview.component';
import { CookingUnitStructure } from './cooking-unit-structure';
import { CookingUnitParticipantSummaryComponent } from './cooking-unit-participant-summary.component';
import { CookingUnitRequirementsComponent } from './cooking-unit-requirements.component';

@Component({
  selector: 'scp-cooking-units-workspace', standalone: true,
  imports: [FormsModule, NgTemplateOutlet, MatButtonModule, MatTooltipModule, ActionIconComponent, NotificationBannerComponent, ParticipantPlanningComponent, CookingUnitParticipantSummaryComponent, CookingUnitRequirementsComponent],
  templateUrl: './cooking-units-workspace.component.html', styleUrl: './cooking-units-workspace.component.scss',
})
export class CookingUnitsWorkspaceComponent {
  readonly data = input.required<MealPlanningOverview>();
  readonly disabled = input(false);
  readonly targetUnitId = input<string | undefined>();
  readonly changed = output<{ data: MealPlanningOverview; message: string }>();
  readonly navigate = output<CateringTarget>();
  readonly draft = signal<CookingUnit | null>(null);
  readonly isNew = signal(false);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly tab = signal<'general' | 'structure' | 'catering'>('general');
  readonly collapsed = signal(new Set<string>());
  readonly settingsOpen = signal(false);
  private readonly basisEditor = viewChild(ParticipantPlanningComponent);
  search = ''; structureSearch = ''; sort = 'name'; groupName = '';
  groupFilter: string | null = '';
  readonly filters = [
    { value: 0, name: 'Alle Teilnehmer', summary: 'Alle anwesenden Teilnehmer der ausgewählten Strukturbereiche.' },
    { value: 1, name: 'Nur Sonderverpflegung', summary: 'Anwesende Teilnehmer mit Ernährungsform, Allergen- oder Unverträglichkeitsanforderung.' },
    { value: 2, name: 'Ohne Sonderverpflegung', summary: 'Anwesende Teilnehmer ohne erfasste Sonderverpflegungsanforderung.' },
  ];
  readonly preferences = [
    { value: 0, name: 'Gemeinsam', summary: 'Primär eine gemeinsame Lösung suchen.' },
    { value: 2, name: 'Getrennt', summary: 'Getrennte Lösungen für Anforderungsgruppen bevorzugen.' },
    { value: 1, name: 'Nachfragen', summary: 'Bei Konflikten bewusst zwischen gemeinsam und getrennt entscheiden.' },
  ];
  private readonly api = inject(MealPlanningApiService);
  private readonly destroyRef = inject(DestroyRef);
  private baseline = '';
  private currentCamp = '';
  private lastTarget?: string;
  private treeSource?: MealPlanningOverview['structureNodes'];
  private treeCache?: CookingUnitStructure;

  constructor() {
    effect(() => {
      const data = this.data(), target = this.targetUnitId();
      untracked(() => {
        if (this.currentCamp !== data.campId) {
          this.currentCamp = data.campId; this.draft.set(null); this.baseline = ''; this.isNew.set(false);
          this.lastTarget = undefined;
          this.search = ''; this.structureSearch = ''; this.groupFilter = ''; this.collapsed.set(new Set()); this.error.set('');
        }
        if (this.groupFilter && !data.cookingUnitGroups.some(group => group.id === this.groupFilter)) this.groupFilter = '';
        const targetChanged = target !== this.lastTarget; this.lastTarget = target;
        if (this.hasPendingChanges() || this.isNew()) return;
        const id = targetChanged ? target : this.draft()?.id;
        const unit = data.cookingUnits.find(value => value.id === id) ?? this.sortedUnits()[0];
        this.edit(unit ?? null);
      });
    });
  }
  tree() {
    if (this.treeSource !== this.data().structureNodes) {
      this.treeSource = this.data().structureNodes; this.treeCache = new CookingUnitStructure(this.treeSource);
    }
    return this.treeCache!;
  }
  managementBusy() { return !!this.basisEditor()?.busy(); }
  basisDirty() { return !!this.basisEditor()?.hasPendingChanges(); }
  locked() { return this.disabled() || this.busy() || this.managementBusy(); }
  hasPendingChanges() { return !!this.draft() && JSON.stringify(this.draft()) !== this.baseline; }
  hasPendingEdits() { return this.hasPendingChanges() || !!this.groupName.trim() || !!this.basisEditor()?.hasPendingChanges(); }
  private mayDiscard() {
    if (!this.hasPendingEdits()) return true;
    if (!window.confirm('Ungespeicherte Änderungen dieser Kocheinheitenverwaltung verwerfen?')) return false;
    this.groupName = ''; this.basisEditor()?.discardChanges(); return true;
  }
  openSettings(dialog: HTMLDialogElement) { if (!this.busy() && !this.hasPendingChanges()) { this.settingsOpen.set(true); dialog.showModal(); } }
  closeSettings(dialog: HTMLDialogElement) {
    if (this.busy() || this.managementBusy() || !this.mayDiscard()) return;
    dialog.close(); this.settingsOpen.set(false);
  }
  private edit(unit: CookingUnit | null) {
    const draft = unit ? { ...structuredClone(unit), participantFilter: unit.participantFilter ?? 0, togetherPreference: unit.togetherPreference ?? 1 } : null;
    this.draft.set(draft); this.isNew.set(false); this.baseline = draft ? JSON.stringify(draft) : '';
  }
  select(unit: CookingUnit) {
    if (this.busy() || unit.id === this.draft()?.id || !this.mayDiscard()) return;
    this.edit(unit); this.error.set('');
  }
  create() {
    if (this.locked() || !this.mayDiscard()) return;
    this.edit({ id: crypto.randomUUID(), campId: this.data().campId, name: '',
      sortOrder: Math.max(-1, ...this.data().cookingUnits.map(unit => unit.sortOrder)) + 1,
      groupId: null, standardMealPlanId: null, defaultStructureNodeIds: [], participantFilter: 0, togetherPreference: 1 });
    this.isNew.set(true); this.tab.set('general'); this.error.set('');
  }
  discardChanges() {
    if (this.busy()) return;
    this.groupName = ''; this.basisEditor()?.discardChanges();
    const unit = this.data().cookingUnits.find(unit => unit.id === this.draft()?.id) ?? (this.isNew() ? this.sortedUnits()[0] : null);
    this.edit(unit ?? null); this.error.set('');
  }
  discard() { if (!this.busy() && this.mayDiscard()) this.discardChanges(); }
  sortedUnits() {
    return this.data().cookingUnits.slice().sort((a, b) => this.sort === 'name'
      ? a.name.localeCompare(b.name, 'de', { numeric: true }) : a.sortOrder - b.sortOrder);
  }
  listedUnits() { const term = this.search.trim().toLocaleLowerCase('de'); return this.sortedUnits().filter(unit =>
    (this.groupFilter === '' || unit.groupId === this.groupFilter) &&
    `${unit.name} ${this.groupLabel(unit.groupId)}`.toLocaleLowerCase('de').includes(term)); }
  groupLabel(id: string | null) { return id ? this.data().cookingUnitGroups.find(group => group.id === id)?.name ?? 'Gruppe nicht verfügbar' : 'Keine Gruppe'; }
  planLabel(id: string | null) { return id ? this.data().mealPlans.find(plan => plan.id === id)?.name ?? 'Menüplan nicht verfügbar' : 'Kein Standard-Menüplan'; }
  filterLabel(value = 0) { return this.filters.find(filter => filter.value === value)?.name ?? 'Filter nicht verfügbar'; }
  filterSummary() { return this.filters.find(filter => filter.value === this.draft()?.participantFilter)?.summary ?? 'Filterauswahl prüfen.'; }
  preferenceLabel(value = 1) { return this.preferences.find(preference => preference.value === value)?.name ?? 'Präferenz nicht verfügbar'; }
  selectedNodes() { return (this.draft()?.defaultStructureNodeIds ?? []).map(id => ({ id, name: this.tree().byId.get(id)?.name ?? 'Strukturbereich nicht verfügbar' })); }
  missingNodes() { return this.selectedNodes().filter(node => !this.tree().byId.has(node.id)); }
  rows() { return this.tree().rows(this.structureSearch, this.collapsed()); }
  nodeState(id: string) { return this.tree().state(id, this.draft()?.defaultStructureNodeIds ?? []); }
  toggleNode(id: string, checked: boolean) {
    if (this.locked() || !this.draft()) return;
    const ids = this.tree().toggle(id, this.draft()!.defaultStructureNodeIds, checked);
    this.draft.update(unit => unit ? { ...unit, defaultStructureNodeIds: ids } : null);
  }
  removeNode(id: string) {
    if (this.locked() || !this.draft()) return;
    if (this.tree().byId.has(id)) this.toggleNode(id, false);
    else this.draft.update(unit => unit ? { ...unit, defaultStructureNodeIds: unit.defaultStructureNodeIds.filter(value => value !== id) } : null);
  }
  toggleExpanded(id: string) { const values = new Set(this.collapsed()); values.has(id) ? values.delete(id) : values.add(id); this.collapsed.set(values); }
  valid() {
    const draft = this.draft(); return !!draft?.name.trim() && draft.name.trim().length <= 200 && this.tree().valid && !this.missingNodes().length &&
      (!draft.groupId || this.data().cookingUnitGroups.some(group => group.id === draft.groupId)) &&
      (!draft.standardMealPlanId || this.data().mealPlans.some(plan => plan.id === draft.standardMealPlanId)) &&
      this.filters.some(filter => filter.value === draft.participantFilter) && this.preferences.some(preference => preference.value === draft.togetherPreference);
  }
  save() {
    const draft = this.draft(); if (this.locked() || !draft || !this.valid() || (!this.isNew() && !this.hasPendingChanges())) return;
    const value = structuredClone(draft); value.name = value.name.trim();
    const creating = this.isNew();
    this.mutate(creating ? this.api.createUnit(value.campId, value) : this.api.saveUnit(value.campId, value),
      value.id, 'Kocheinheit gespeichert.', creating);
  }
  remove() {
    const draft = this.draft(); if (!draft || this.locked() || this.isNew()) return;
    if (!window.confirm(`Kocheinheit „${draft.name}“ und ihre operativen Teilstände löschen?`)) return;
    this.mutate(this.api.deleteUnit(this.data().campId, draft.id), null, 'Kocheinheit gelöscht.');
  }
  createGroup() {
    if (this.locked() || this.hasPendingChanges() || this.basisEditor()?.hasPendingChanges() || !this.groupName.trim()) return;
    this.mutate(this.api.createGroup(this.data().campId, { name: this.groupName.trim(),
      sortOrder: Math.max(-1, ...this.data().cookingUnitGroups.map(group => group.sortOrder)) + 1 }), this.draft()?.id ?? null,
      'Kocheinheitengruppe angelegt.');
  }
  removeGroup(id: string) {
    if (this.locked() || this.hasPendingChanges() || this.basisEditor()?.hasPendingChanges() || !window.confirm('Gruppe entfernen? Die Kocheinheiten bleiben erhalten.')) return;
    this.mutate(this.api.deleteGroup(this.data().campId, id), this.draft()?.id ?? null, 'Gruppe entfernt; Kocheinheiten bleiben erhalten.');
  }
  refresh() {
    if (this.busy() || !this.mayDiscard()) return;
    const camp = this.data().campId; this.busy.set(true);
    this.api.overview(camp).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: data => {
      this.busy.set(false); this.edit(data.cookingUnits.find(unit => unit.id === this.draft()?.id) ?? data.cookingUnits[0] ?? null);
      this.error.set(''); this.changed.emit({ data, message: '' });
    }, error: () => { this.busy.set(false); this.error.set('Die Kocheinheiten konnten nicht neu geladen werden. Der Entwurf bleibt erhalten.'); } });
  }
  private mutate(request: Observable<MutationResult>, selectedId: string | null, message: string, creating = false) {
    const camp = this.data().campId; let committed = false;
    this.busy.set(true); this.error.set('');
    request.pipe(switchMap(result => {
      committed = true;
      if (creating && result.id) { selectedId = result.id; this.draft()!.id = result.id; this.isNew.set(false); }
      return this.api.overview(camp);
    }), takeUntilDestroyed(this.destroyRef)).subscribe({ next: data => {
      this.busy.set(false); this.groupName = '';
      this.edit(data.cookingUnits.find(unit => unit.id === selectedId) ?? data.cookingUnits[0] ?? null);
      this.changed.emit({ data, message });
    }, error: error => {
      this.busy.set(false);
      this.error.set(committed ? 'Die Änderung wurde gespeichert, der aktuelle Stand konnte aber nicht geladen werden. Bitte neu laden.'
        : [error.error?.message ?? 'Nicht gespeichert. Der Entwurf bleibt erhalten.', error.error?.references?.join(' · ')].filter(Boolean).join(' '));
    } });
  }
  firstMeal() { return this.data().meals.find(meal => meal.isActive && meal.isInsideCampPeriod); }
  openSupply() {
    const draft = this.draft(), meal = this.firstMeal();
    if (!draft || this.isNew() || this.busy() || !meal || !this.mayDiscard()) return;
    const unitId = draft.id; this.discardChanges(); this.navigate.emit({ section: 'units', unitId, mealId: meal.id });
  }
}
