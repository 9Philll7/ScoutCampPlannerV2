import { Component, effect, inject, input, output, signal } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { Observable, Subscription } from 'rxjs';
import { ActionIconComponent } from '../../shared/action-icon.component';
import { LIST_PAGE_SIZE, ListPage, ListPaginationComponent } from '../../shared/list-pagination.component';
import { FitListPageDirective } from '../../shared/fit-list-page.directive';
import { ExplicitPermissionMember, ParticipantData, ParticipantDocument, ParticipantsApiService, ParticipantsOverview } from './participants-api.service';

@Component({
  selector: 'scp-participants', standalone: true,
  host: { '[class.embedded]': 'scopedNodeId() !== null' },
  imports: [FormsModule, NgTemplateOutlet, MatButtonModule, ActionIconComponent, ListPaginationComponent, FitListPageDirective],
  templateUrl: './participants.component.html',
  styleUrl: './participants.component.scss'
})
export class ParticipantsComponent {
  readonly campId = input.required<string>();
  readonly initialStructureNodeId = input<string | null>(null);
  readonly scopedNodeId = input<string | null>(null);
  readonly changed = output<void>();
  readonly local = input(false);
  private readonly api = inject(ParticipantsApiService);
  readonly overview = signal<ParticipantsOverview | null>(null);
  readonly members = signal<ExplicitPermissionMember[]>([]);
  readonly error = signal('');
  readonly busy = signal(false);
  search = '';
  groupFilter = '';
  stageFilter = '';
  categoryFilter = '';
  dietFilter = '';
  resetFilters() { this.search = ''; this.groupFilter = ''; this.stageFilter = ''; this.categoryFilter = ''; this.dietFilter = ''; this.clearHiddenSelection(); }
  dietName(id: string | null) { return this.overview()?.catalog.dietTypes.find(diet => diet.id === id)?.name ?? (id ? 'Nicht verfügbar' : 'Keine Angabe'); }
  groupName(id?: string | null) { return this.overview()?.structureNodes?.find(node => node.id === id)?.name ?? 'Nicht zugeordnet'; }
  summaryCount(category: 'ChildYouth' | 'LeaderAdult' | 'diet') { return (this.overview()?.participants ?? []).filter(row => category === 'diet' ? row.data.dietTypeId !== null : row.data.category === category).length; }
  sortAscending = true;
  readonly pageIndex = signal(0);
  readonly pageSize = signal(LIST_PAGE_SIZE);
  fitPage(size: number) { this.pageSize.set(size); this.pageIndex.set(0); }
  visiblePageIndex() { return Math.min(this.pageIndex(), Math.max(0, Math.ceil(this.filteredParticipants().length / this.pageSize()) - 1)); }
  pagedParticipants() { const start = this.visiblePageIndex() * this.pageSize(); return this.filteredParticipants().slice(start, start + this.pageSize()); }
  changePage(page: ListPage) { this.pageIndex.set(page.pageIndex); this.selectedId = null; }
  scopedCount() { return this.overview()?.participants.filter(row => row.data.structureNodeId === this.scopedNodeId()).length ?? 0; }
  selectedId: string | null = null;
  readonly detailId = signal<string | null>(null);
  readonly editorSection = signal('general');
  readonly participantSections = [
    { id: 'general', label: 'Allgemein' }, { id: 'structure', label: 'Struktur & Gruppe' },
    { id: 'diet', label: 'Ernährung' },
    { id: 'attendance', label: 'Anwesenheit' }
  ];
  detailDocument() { return this.overview()?.participants.find(row => row.data.id === this.detailId()) ?? null; }
  openDetails(document: ParticipantDocument) { if (!this.closeEditor()) return; if (this.detailId() !== document.data.id) this.editorSection.set('general'); this.detailId.set(document.data.id); }
  backToList() { if (this.closeEditor()) this.detailId.set(null); }
  allergenName(id: string) {
    const catalog = this.overview()?.catalog.allergens ?? [];
    const item = catalog.find(row => row.id === id);
    if (!item) return 'Nicht verfügbar';
    const root = item.parentId ? catalog.find(row => row.id === item.parentId) : item;
    const letter = root ? this.allergenLetters[root.code] : null;
    return letter ? `${letter} · ${item.name}` : item.name;
  }
  substanceName(id: string) { return this.overview()?.catalog.substances.find(row => row.id === id)?.name ?? 'Nicht verfügbar'; }
  groupedAllergens(ids: string[]) {
    const catalog = this.overview()?.catalog.allergens ?? [];
    const groups = new Map<string, { id: string; name: string; subgroups: string[] }>();
    for (const id of new Set(ids)) {
      const item = catalog.find(row => row.id === id);
      const groupId = item?.parentId ?? id;
      let group = groups.get(groupId);
      if (!group) {
        group = { id: groupId, name: this.allergenName(groupId), subgroups: [] };
        groups.set(groupId, group);
      }
      if (item?.parentId) group.subgroups.push(item.name);
    }
    return [...groups.values()];
  }
  readonly allergenEditor = signal<string | null>(null);
  readonly addingIntolerance = signal(false);
  readonly editingIntolerance = signal<string | null>(null);
  allergenGroup = '';
  readonly allergenLetters: Record<string, string> = { GLUTEN_CEREALS: 'A', CRUSTACEANS: 'B', EGGS: 'C', FISH: 'D', PEANUTS: 'E', SOYBEANS: 'F', MILK: 'G', TREE_NUTS: 'H', CELERY: 'L', MUSTARD: 'M', SESAME: 'N', SULPHUR_DIOXIDE_AND_SULPHITES: 'O', LUPIN: 'P', MOLLUSCS: 'R' };
  intoleranceThreshold: number | null = null;
  intoleranceSource: string | null = null;
  allergenChoice = '';
  allergenSubgroups: string[] = [];
  substanceChoice = '';
  private canChangeNutrition() { return this.draft !== null && this.overview()?.canEdit && !this.overview()?.isFrozen && !this.busy(); }
  availableAllergens() { return (this.overview()?.catalog.allergens ?? []).filter(row => !this.draft?.allergenIds.includes(row.id) || row.id === this.allergenEditor()); }
  availableSubstances() { return (this.overview()?.catalog.substances ?? []).filter(row => !this.requirement(row.id)); }
  allergenGroups() { return (this.overview()?.catalog.allergens ?? []).filter(row => !row.parentId && this.allergenLetters[row.code]).sort((a, b) => this.allergenLetters[a.code].localeCompare(this.allergenLetters[b.code])); }
  allergenChildren() { return (this.overview()?.catalog.allergens ?? []).filter(row => row.parentId === this.allergenGroup); }
  chooseAllergenGroup() {
    this.allergenChoice = this.allergenGroup;
    this.allergenSubgroups = this.allergenChildren().filter(row => this.draft?.allergenIds.includes(row.id)).map(row => row.id);
  }
  toggleAllergenSubgroup(id: string) {
    this.allergenSubgroups = this.allergenSubgroups.includes(id) ? this.allergenSubgroups.filter(value => value !== id) : [...this.allergenSubgroups, id];
  }
  validAllergenChoice() {
    return this.allergenGroups().some(row => row.id === this.allergenGroup) &&
      this.allergenSubgroups.every(id => this.allergenChildren().some(row => row.id === id));
  }
  beginAllergen(id: string | null = null) {
    if (!this.canChangeNutrition()) return;
    const item = this.overview()?.catalog.allergens.find(row => row.id === id);
    this.allergenEditor.set(id ?? ''); this.allergenGroup = item?.parentId ?? id ?? '';
    this.chooseAllergenGroup();
  }
  beginIntolerance(id: string | null = null) {
    if (!this.canChangeNutrition()) return;
    this.editingIntolerance.set(id); this.addingIntolerance.set(id === null); this.substanceChoice = id ?? '';
    this.prepareIntolerance();
  }
  prepareIntolerance() {
    const existing = this.requirement(this.substanceChoice);
    const catalog = this.overview()?.catalog.substances.find(row => row.id === this.substanceChoice);
    this.intoleranceThreshold = existing ? existing.thresholdGramsPerPortion : catalog?.defaultThresholdGramsPerPortion ?? null;
    this.intoleranceSource = existing ? existing.thresholdSource : this.intoleranceThreshold === null ? null : `Katalogübernahme (Stand ${catalog?.defaultThresholdVersion ?? 0}): ${catalog?.defaultThresholdSource ?? ''}`.slice(0, 500);
  }
  validIntolerance() { return !!this.substanceChoice && (this.substanceChoice === this.editingIntolerance() || this.availableSubstances().some(row => row.id === this.substanceChoice)) && (this.intoleranceThreshold === null || (Number.isFinite(this.intoleranceThreshold) && this.intoleranceThreshold >= 0 && this.intoleranceThreshold <= 999999999999.999999)); }
  applyIntolerance() {
    if (!this.canChangeNutrition() || !this.validIntolerance()) return;
    if (!this.requirement(this.substanceChoice)) this.toggleRequirement(this.substanceChoice, true);
    Object.assign(this.requirement(this.substanceChoice)!, { thresholdGramsPerPortion: this.intoleranceThreshold, thresholdSource: this.intoleranceSource });
    this.editingIntolerance.set(null); this.addingIntolerance.set(false);
  }
  applyAllergen() {
    if (!this.canChangeNutrition() || this.allergenEditor() === null || !this.validAllergenChoice()) return;
    const current = this.overview()?.catalog.allergens.find(row => row.id === this.allergenEditor());
    const replacedGroups = new Set([this.allergenGroup, ...(current ? [current.parentId ?? current.id] : [])]);
    const replacedIds = new Set((this.overview()?.catalog.allergens ?? []).filter(row => replacedGroups.has(row.parentId ?? row.id)).map(row => row.id));
    const selected = this.allergenSubgroups.length ? this.allergenSubgroups : [this.allergenGroup];
    this.draft!.allergenIds = [...new Set([...this.draft!.allergenIds.filter(id => !replacedIds.has(id)), ...selected])];
    this.allergenEditor.set(null); this.allergenChoice = '';
  }
  removeAllergen(id: string) {
    if (!this.canChangeNutrition()) return;
    const catalog = this.overview()?.catalog.allergens ?? [];
    const groupId = catalog.find(row => row.id === id)?.parentId ?? id;
    this.draft!.allergenIds = this.draft!.allergenIds.filter(value => value !== groupId && catalog.find(row => row.id === value)?.parentId !== groupId);
    this.allergenEditor.set(null);
  }
  addIntolerance() {
    if (!this.canChangeNutrition() || !this.availableSubstances().some(row => row.id === this.substanceChoice)) return;
    this.toggleRequirement(this.substanceChoice, true); this.editingIntolerance.set(this.substanceChoice);
    this.addingIntolerance.set(false); this.substanceChoice = '';
  }
  removeIntolerance(id: string) { if (!this.canChangeNutrition()) return; this.toggleRequirement(id, false); if (this.editingIntolerance() === id) this.editingIntolerance.set(null); }
  quantityDependent(id: string) { return this.overview()?.catalog.substances.find(row => row.id === id)?.isQuantityDependent ?? false; }
  private draftBaseline = '';
  filteredParticipants() {
    const term = this.search.trim().toLocaleLowerCase('de');
    return (this.overview()?.participants ?? []).filter(document =>
      (!this.scopedNodeId() || document.data.structureNodeId === this.scopedNodeId()) &&
      (!term || document.data.displayName.toLocaleLowerCase('de').includes(term)) &&
      (!this.stageFilter || (this.stageFilter === 'unassigned' ? !document.data.campStageId : document.data.campStageId === this.stageFilter)) &&
      (!this.categoryFilter || (this.categoryFilter === 'unassigned' ? !document.data.category : document.data.category === this.categoryFilter)) &&
      (!this.dietFilter || (this.dietFilter === 'unassigned' ? !document.data.dietTypeId : document.data.dietTypeId === this.dietFilter)) &&
      (!this.groupFilter || (this.groupFilter === 'unassigned' ? !document.data.structureNodeId : this.belongsToBranch(document.data.structureNodeId, this.groupFilter))))
      .slice().sort((a, b) => a.data.displayName.localeCompare(b.data.displayName, 'de') * (this.sortAscending ? 1 : -1));
  }
  selectedDocument() { return this.overview()?.participants.find(document => document.data.id === this.selectedId) ?? null; }
  private belongsToBranch(id: string | null | undefined, branch: string) {
    const nodes = this.overview()?.structureNodes ?? []; const visited = new Set<string>();
    while (id && !visited.has(id)) { if (id === branch) return true; visited.add(id); id = nodes.find(node => node.id === id)?.parentId; }
    return false;
  }
  select(document: ParticipantDocument) {
    if (this.scopedNodeId()) { this.openDetails(document); return; }
    if (!this.closeEditor()) return;
    this.selectedId = this.selectedId === document.data.id ? null : document.data.id;
  }
  clearHiddenSelection() { this.pageIndex.set(0); if (!this.pagedParticipants().some(document => document.data.id === this.selectedId)) this.selectedId = null; }
  initials(name: string) { return name.trim().split(/\s+/).slice(0, 2).map(word => word[0]).join('').toLocaleUpperCase('de'); }
  structurePath(id: string | null | undefined) {
    if (!id) return 'Noch nicht zugeordnet';
    const nodes = this.overview()?.structureNodes ?? [];
    const parts: string[] = []; const visited = new Set<string>();
    let current: string | null = id;
    while (current && !visited.has(current)) {
      visited.add(current); const node = nodes.find(value => value.id === current);
      if (!node) { parts.unshift('Zuordnung nicht verfügbar'); break; }
      parts.unshift(node.name); current = node.parentId;
    }
    return parts.join(' › ');
  }
  hasPendingChanges() { return this.draft !== null && JSON.stringify(this.draft) !== this.draftBaseline; }
  closeEditor() {
    if (this.hasPendingChanges() && !window.confirm('Nicht gespeicherte Änderungen verwerfen?')) return false;
    this.allergenEditor.set(null); this.addingIntolerance.set(false); this.editingIntolerance.set(null); this.allergenChoice = ''; this.substanceChoice = '';
    this.draft = null; this.original = null; return true;
  }
  reload() { if (this.closeEditor()) this.load(); }
  draft: ParticipantData | null = null;
  participantNodes() {
    const view = this.overview();
    const nodes = view?.structureNodes ?? [];
    const requiredDepth = view?.participantStructureDepth;
    const byId = new Map(nodes.map(node => [node.id, node]));
    const parents = new Set(nodes.map(node => node.parentId));
    return nodes.filter(node => {
      if (parents.has(node.id)) return false;
      if (requiredDepth == null) return true;
      let depth = 1;
      let parentId = node.parentId;
      const visited = new Set([node.id]);
      while (parentId !== null) {
        if (visited.has(parentId)) return false;
        visited.add(parentId);
        const parent = byId.get(parentId);
        if (!parent) return false;
        depth++;
        parentId = parent.parentId;
      }
      return depth === requiredDepth;
    });
  }
  original: ParticipantDocument | null = null;
  private requests = new Subscription();
  readonly rights = [
    { code: 'health.participant-requirements.read', label: 'Teilnehmeranforderungen lesen' },
    { code: 'health.participant-requirements.edit', label: 'Teilnehmeranforderungen bearbeiten' },
    { code: 'catering.meal-planning.verify', label: 'Verpflegung verifizieren' },
  ];
  constructor() { effect(onCleanup => { this.campId(); this.scopedNodeId(); this.search = ''; this.groupFilter = ''; this.stageFilter = ''; this.categoryFilter = ''; this.dietFilter = ''; this.sortAscending = true; this.load(); onCleanup(() => this.requests.unsubscribe()); }); }
  load() {
    this.detailId.set(null); this.editorSection.set('general');
    this.pageIndex.set(0);
    this.requests.unsubscribe(); this.requests = new Subscription();
    this.draft = null; this.original = null; this.selectedId = null; this.overview.set(null); this.members.set([]); this.error.set(''); this.busy.set(false);
    this.requests.add(this.api.list(this.campId()).subscribe({ next: data => this.overview.set(data),
      error: () => this.error.set('Teilnehmer konnten nicht geladen werden. Ein explizites Leserecht ist erforderlich.') }));
    if (!this.local() && !this.scopedNodeId()) this.requests.add(this.api.members(this.campId()).subscribe({ next: data => this.members.set(data), error: () => {} }));
  }
  create() { if (!this.closeEditor()) return; this.detailId.set(null); this.editorSection.set('general'); this.original = null; this.draft = { displayName: '', campStageId: null, category: null, structureNodeId: this.participantNodes().some(node => node.id === this.initialStructureNodeId()) ? this.initialStructureNodeId() : null, dietTypeId: null, absentDays: [], absentMealIds: [], allergenIds: [], intolerances: [] }; this.draftBaseline = JSON.stringify(this.draft); }
  edit(document: ParticipantDocument) { if (!this.closeEditor()) return; if (this.detailId() !== document.data.id) this.editorSection.set('general'); this.original = document; this.draft = { ...structuredClone(document.data), campStageId: document.data.campStageId ?? null, category: document.data.category ?? null }; this.draftBaseline = JSON.stringify(this.draft); }
  stageName(id?: string | null) { return this.overview()?.stages?.find(stage => stage.id === id)?.name ?? 'Nicht zugeordnet'; }
  categoryName(category?: string | null) { return category === 'ChildYouth' ? 'KiJu' : category === 'LeaderAdult' ? 'Leiter/Erwachsene' : 'Nicht zugeordnet'; }
  days(): string[] {
    const view = this.overview(); if (!view?.startDate || !view.endDate) return [];
    const days: string[] = []; const current = new Date(view.startDate + 'T00:00:00Z'); const end = new Date(view.endDate + 'T00:00:00Z');
    while (current <= end) { days.push(current.toISOString().slice(0, 10)); current.setUTCDate(current.getUTCDate() + 1); }
    return days;
  }
  toggle(values: string[], id: string, enabled: boolean) { const index = values.indexOf(id); if (enabled && index < 0) values.push(id); if (!enabled && index >= 0) values.splice(index, 1); }
  requirement(id: string) { return this.draft?.intolerances.find(value => value.substanceId === id); }
  toggleRequirement(id: string, enabled: boolean) {
    if (!this.draft) return;
    if (enabled && !this.requirement(id)) {
      const catalog = this.overview()?.catalog.substances.find(value => value.id === id);
      const threshold = catalog?.defaultThresholdGramsPerPortion ?? null;
      this.draft.intolerances.push({ substanceId: id, thresholdGramsPerPortion: threshold,
        thresholdSource: threshold === null ? null : `Katalogübernahme (Stand ${catalog?.defaultThresholdVersion ?? 0}): ${catalog?.defaultThresholdSource ?? ''}`.slice(0, 500) });
    }
    if (!enabled) this.draft.intolerances = this.draft.intolerances.filter(value => value.substanceId !== id);
  }
  save() { if (!this.draft || !this.overview()?.canEdit || this.overview()?.isFrozen || this.busy()) return;
    this.mutate(this.original ? this.api.update(this.campId(), this.original, this.draft) : this.api.create(this.campId(), this.draft)); }
  remove(document: ParticipantDocument) { if (!this.overview()?.canEdit || this.overview()?.isFrozen || this.busy() || !window.confirm('Diesen Testteilnehmer löschen?')) return;
    this.mutate(this.api.remove(this.campId(), document)); }
  grant(member: ExplicitPermissionMember, permission: string, enabled: boolean) { this.mutate(this.api.grant(this.campId(), member.membershipId, permission, enabled)); }
  private mutate(request: Observable<unknown>) {
    this.busy.set(true); this.error.set('');
    this.requests.add(request.subscribe({ next: () => { this.changed.emit(); this.load(); }, error: failure => { this.busy.set(false);
      this.error.set(failure.status === 409 ? 'Der Stand hat sich geändert oder das Lager ist gesperrt. Eingaben bleiben erhalten; bitte vor erneutem Bearbeiten neu laden.' :
        'Änderung nicht gespeichert. Berechtigung und Eingaben prüfen.'); } }));
  }
}
