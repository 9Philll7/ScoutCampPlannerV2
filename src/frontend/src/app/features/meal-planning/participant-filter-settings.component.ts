import { Component, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { API_BASE_URL } from '../../core/api-base-url';

interface Option { id: string | null; name: string; }
interface Condition { field: number; operator: number; values: (string | null)[]; }
interface Definition { schemaVersion: number; groups: { conditions: Condition[] }[]; }
interface Filter { id: string; name: string; version: number; definition: Definition; }
interface Overview { canEdit: boolean; filters: Filter[]; stages: Option[];
  catalog: { dietTypes: Option[]; allergens: Option[]; substances: Option[] }; }

@Component({
  selector: 'scp-participant-filter-settings', imports: [FormsModule],
  template: `
    <section aria-label="Teilnehmerfilter">
      <h3>Teilnehmerfilter</h3>
      <p>Lagerbezogene Filter für Stufe, Kategorie und Verpflegungsanforderungen.</p>
      <p role="note">Die Filterverwaltung ist vorbereitet. Die Verwendung durch Kocheinheiten und der Offline-Transport sind noch nicht angebunden.</p>
      @if (error()) { <p role="alert">{{ error() }}</p> }
      @if (notice()) { <p role="status">{{ notice() }}</p> }
      @if (data(); as overview) {
        <button type="button" (click)="create()" [disabled]="!overview.canEdit || busy()">Filter erstellen</button>
        <ul>@for (filter of overview.filters; track filter.id) {
          <li><strong>{{ filter.name }}</strong> · Version {{ filter.version }}
            <button type="button" (click)="open(filter)" [disabled]="busy()">Öffnen</button>
            <button type="button" (click)="copy(filter)" [disabled]="!overview.canEdit || busy()">Kopieren</button>
          </li>
        } @empty { <li>Noch keine gespeicherten Filter.</li> }</ul>
        @if (draft(); as edit) {
          <fieldset [disabled]="!overview.canEdit || busy()">
            <legend>Filter bearbeiten</legend>
            <label>Name <input [(ngModel)]="edit.name" maxlength="200"></label>
            @for (group of edit.definition.groups; track $index; let gi = $index) {
              @if (gi > 0) { <p><strong>ODER</strong></p> }
              <fieldset><legend>Alle Bedingungen dieser Gruppe müssen zutreffen</legend>
                @for (condition of group.conditions; track $index; let ci = $index) {
                  <div class="condition">
                    <label>Merkmal <select [ngModel]="condition.field" (ngModelChange)="setField(condition, $event)">
                      @for (field of fields; track $index; let fi = $index) { <option [ngValue]="fi">{{ field }}</option> }
                    </select></label>
                    <label>Vergleich <select [(ngModel)]="condition.operator"><option [ngValue]="0">Mindestens einer dieser Werte</option><option [ngValue]="1">Keiner dieser Werte</option></select></label>
                    <label>Werte (Mehrfachauswahl) <select multiple size="5" [(ngModel)]="condition.values">
                      @for (option of options(condition.field); track option.id) { <option [ngValue]="option.id">{{ option.name }}</option> }
                    </select></label>
                    <button type="button" (click)="group.conditions.splice(ci, 1)">Bedingung entfernen</button>
                  </div>
                }
                <button type="button" (click)="group.conditions.push(newCondition())">UND-Bedingung hinzufügen</button>
                <button type="button" (click)="edit.definition.groups.splice(gi, 1)">Gruppe entfernen</button>
              </fieldset>
            }
            <button type="button" (click)="edit.definition.groups.push({ conditions: [newCondition()] })">ODER-Gruppe hinzufügen</button>
            <p>{{ summary(edit) }}</p>
            <p>Ohne Bedingung bleibt ein Merkmal uneingeschränkt. „Nicht zugeordnet“ ist ausdrücklich auswählbar. Ausschlüsse sind keine medizinische Freigabe.</p>
            <button type="button" (click)="save()" [disabled]="!valid(edit)">Speichern</button>
          </fieldset>
          <button type="button" (click)="cancel()" [disabled]="busy()">Schließen / Änderungen verwerfen</button>
        }
      } @else if (!error()) { <p>Filter werden geladen …</p> }
    </section>`,
  styles: `fieldset { margin: 12px 0; padding: 12px; } label { display: grid; gap: 4px; }
    .condition { display:flex; flex-wrap:wrap; gap:12px; margin:12px 0; align-items:end; }
    button { margin: 4px; } select,input { padding:8px; max-width:100%; } [role=alert] { color:#a21; }`
})
export class ParticipantFilterSettingsComponent {
  readonly campId = input.required<string>();
  readonly data = signal<Overview | null>(null);
  readonly draft = signal<Filter | null>(null);
  readonly error = signal(''); readonly notice = signal(''); readonly busy = signal(false);
  readonly fields = ['Stufe', 'Kategorie', 'Ernährungsform', 'Allergenanforderung', 'Unverträglichkeitsstoff', 'Sonderverpflegung'];
  private readonly http = inject(HttpClient); private readonly base = inject(API_BASE_URL);
  private baseline = ''; private generation = 0;
  constructor() { effect(() => { this.campId(); this.draft.set(null); this.load(); }); }
  private url() { return this.base + '/api/camps/' + this.campId() + '/participant-filters'; }
  load() {
    const generation = ++this.generation; this.data.set(null); this.error.set('');
    this.http.get<Overview>(this.url(), { withCredentials: true }).subscribe({ next: value => {
      if (generation === this.generation) this.data.set(value);
    }, error: () => { if (generation === this.generation) this.error.set('Filter nicht verfügbar. Lagerzugriff und explizites Teilnehmer-Leserecht prüfen.'); } });
  }
  newCondition(): Condition { return { field: 0, operator: 0, values: [] }; }
  hasPendingChanges() { return !!this.draft() && JSON.stringify(this.draft()) !== this.baseline; }
  private discardAllowed() { return !this.hasPendingChanges() || window.confirm('Ungespeicherte Filteränderungen verwerfen?'); }
  open(filter: Filter) { if (!this.discardAllowed()) return false; this.draft.set(structuredClone(filter)); this.baseline = JSON.stringify(this.draft()); this.error.set(''); return true; }
  create() { this.open({ id: crypto.randomUUID(), name: '', version: 0, definition: { schemaVersion: 1, groups: [{ conditions: [this.newCondition()] }] } }); }
  copy(filter: Filter) { if (this.open({ ...structuredClone(filter), id: crypto.randomUUID(), name: filter.name + ' (Kopie)', version: 0 })) this.baseline = ''; }
  cancel() { if (this.discardAllowed()) { this.draft.set(null); this.error.set(''); } }
  setField(condition: Condition, field: number) { condition.field = field; condition.values = []; }
  options(field: number): Option[] {
    const data = this.data(); if (!data) return [];
    switch (field) {
      case 0: return [...data.stages, { id: null, name: 'Nicht zugeordnet' }];
      case 1: return [{ id: 'ChildYouth', name: 'KiJu' }, { id: 'LeaderAdult', name: 'Leiter/Erwachsene' }, { id: null, name: 'Nicht zugeordnet' }];
      case 2: return data.catalog.dietTypes;
      case 3: return data.catalog.allergens;
      case 4: return data.catalog.substances;
      case 5: return [{ id: 'true', name: 'Mit Sonderverpflegung' }, { id: 'false', name: 'Ohne Sonderverpflegung' }];
      default: return [];
    }
  }
  valid(filter: Filter) { return !!filter.name.trim() && filter.definition.groups.length > 0 && filter.definition.groups.every(group =>
    group.conditions.length > 0 && group.conditions.every(condition => condition.values.length > 0)); }
  summary(filter: Filter) { return filter.definition.groups.map(group => '(' + group.conditions.map(condition =>
    this.fields[condition.field] + (condition.operator === 1 ? ': keine von ' : ': ') + condition.values.map(value =>
      this.options(condition.field).find(option => option.id === value)?.name ?? 'Referenz nicht verfügbar').join(' / ')).join(' UND ') + ')').join(' ODER '); }
  save() {
    const draft = this.draft(); if (!draft || !this.valid(draft) || !this.data()?.canEdit || this.busy()) return;
    this.busy.set(true); this.error.set(''); const generation = this.generation;
    this.http.put(this.url() + '/' + draft.id, { expectedVersion: draft.version, name: draft.name, definition: draft.definition },
      { withCredentials: true }).subscribe({ next: () => {
        this.busy.set(false); if (generation !== this.generation) return;
        this.draft.set(null); this.notice.set('Filter gespeichert.'); this.load();
      }, error: error => { this.busy.set(false); if (generation === this.generation)
        this.error.set(error.error?.message ?? 'Filter nicht gespeichert. Eingaben bleiben erhalten.'); } });
  }
}
