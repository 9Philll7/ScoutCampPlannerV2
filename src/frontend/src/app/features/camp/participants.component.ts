import { Component, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { Observable, Subscription } from 'rxjs';
import { ActionIconComponent } from '../../shared/action-icon.component';
import { ExplicitPermissionMember, ParticipantData, ParticipantDocument, ParticipantsApiService, ParticipantsOverview } from './participants-api.service';

@Component({
  selector: 'scp-participants', standalone: true,
  imports: [FormsModule, MatButtonModule, ActionIconComponent],
  template: `
    <section>
      <div class="heading"><h3>Teilnehmer</h3><button matButton [disabled]="busy()" (click)="load()">Neu laden</button></div>
      <p class="notice">Entwicklungsfunktion: ausschließlich erfundene Dummy-/Testdaten eingeben. Keine Freigabe für echte Gesundheitsdaten oder deren Offline-Transport.</p>
      @if (error()) { <p role="alert">{{ error() }}</p> }
      @if (members().length) {
        <details><summary>Explizite Zugriffsrechte verwalten</summary>
          <p>Diese Rechte werden nicht automatisch durch eine Rolle vergeben. Jede Änderung wird protokolliert.</p>
          @for (member of members(); track member.membershipId) {
            <fieldset [disabled]="busy()"><legend>{{ member.displayName }}</legend>
              @for (right of rights; track right.code) {
                <label class="check"><input type="checkbox" [checked]="member.permissions.includes(right.code)"
                  (change)="grant(member, right.code, $any($event.target).checked)"/>{{ right.label }}</label>
              }
            </fieldset>
          }
        </details>
      }
      @if (local()) { <p class="hint">Lokale Health-/Verify-Rechte werden ausschließlich über den bewusst ausgeführten Verwaltungsbefehl am Gerät vergeben, nicht durch den Paketimport.</p> }
      @if (overview(); as view) {
        @if (view.isFrozen) { <p>Offlinephase aktiv – Teilnehmer sind hier schreibgeschützt.</p> }
        <button matButton [disabled]="!view.canEdit || busy()" (click)="create()">Testteilnehmer anlegen</button>
        <div class="cards">
          @for (document of view.participants; track document.data.id) {
            <article><strong>{{ document.data.displayName }}</strong>
              <div><button matIconButton [attr.aria-label]="'Teilnehmer öffnen: ' + document.data.displayName" [disabled]="busy()" (click)="edit(document)"><scp-action-icon name="edit"/></button>
              <button matIconButton aria-label="Teilnehmer löschen" [disabled]="!view.canEdit || busy()" (click)="remove(document)"><scp-action-icon name="remove"/></button></div>
            </article>
          } @empty { <p>Noch keine Testteilnehmer vorhanden.</p> }
        </div>
        @if (draft; as value) {
          <form (ngSubmit)="save()">
            <h4>{{ original ? 'Teilnehmer bearbeiten' : 'Neuer Testteilnehmer' }}</h4>
            <fieldset [disabled]="!view.canEdit || busy()">
              <label>Anzeigename<input name="displayName" [(ngModel)]="value.displayName" required maxlength="200"/></label>
              <label>Strukturknoten<select name="structureNodeId" [(ngModel)]="value.structureNodeId">
                <option [ngValue]="null">Noch nicht zugeordnet</option>
                @for (node of participantNodes(); track node.id) { <option [ngValue]="node.id">{{ node.name }}</option> }
              </select></label>
              <label>Ernährungsform<select name="diet" [(ngModel)]="value.dietTypeId"><option [ngValue]="null">Keine</option>
                @for (diet of view.catalog.dietTypes; track diet.id) { <option [ngValue]="diet.id">{{ diet.name }}</option> }
              </select></label>
              <details><summary>Anwesenheit (standardmäßig anwesend)</summary>
                @for (day of days(); track day) {
                  <label class="check"><input type="checkbox" [checked]="value.absentDays.includes(day)"
                    (change)="toggle(value.absentDays, day, $any($event.target).checked)"/>{{ day }} ganztägig abwesend</label>
                }
                <h5>Einzelne Mahlzeiten abwesend</h5>
                @for (meal of view.meals; track meal.id) {
                  <label class="check"><input type="checkbox" [checked]="value.absentMealIds.includes(meal.id)"
                    [disabled]="value.absentDays.includes(meal.date) || !meal.isActive"
                    (change)="toggle(value.absentMealIds, meal.id, $any($event.target).checked)"/>
                    {{ meal.date }} · {{ meal.name }} {{ !meal.isActive ? '(inaktiv)' : '' }}</label>
                }
              </details>
              <details><summary>Allergene</summary>
                @for (allergen of view.catalog.allergens; track allergen.id) {
                  <label class="check"><input type="checkbox" [checked]="value.allergenIds.includes(allergen.id)"
                    (change)="toggle(value.allergenIds, allergen.id, $any($event.target).checked)"/>{{ allergen.name }}</label>
                }
              </details>
              <details><summary>Unverträglichkeiten und individuelle Angaben</summary>
                <p>Kein medizinischer Standardwert. Ein leeres Feld bedeutet: Grenzwert unbekannt.</p>
                @for (substance of view.catalog.substances; track substance.id) {
                  <div class="substance"><label class="check"><input type="checkbox" [checked]="!!requirement(substance.id)"
                    (change)="toggleRequirement(substance.id, $any($event.target).checked)"/>{{ substance.name }}</label>
                  @if (requirement(substance.id); as requirement) {
                    @if (substance.isQuantityDependent) {
                      <label>Individueller Grenzwert (g/Portion)<input type="number" min="0" max="999999999999.999999" step="0.000001"
                        [name]="'threshold-' + substance.id" [(ngModel)]="requirement.thresholdGramsPerPortion"/></label>
                    }
                    <label>Herkunft des Grenzwerts<input maxlength="500" [name]="'source-' + substance.id" [(ngModel)]="requirement.thresholdSource"/></label>
                  }</div>
                }
              </details>
              <button matButton type="submit" [disabled]="!value.displayName.trim()">Speichern</button>
            </fieldset>
            <button matButton type="button" [disabled]="busy()" (click)="draft = null; original = null">Schließen</button>
          </form>
        }
      }
    </section>
  `,
  styles: [`
    .heading,article { display:flex;justify-content:space-between;align-items:center;gap:1rem; }
    .notice { background:#fff3d5;padding:1rem;border-radius:.5rem;color:#553800; }
    .hint { color:#53635a; } .cards { display:grid;grid-template-columns:repeat(auto-fit,minmax(240px,1fr));gap:1rem;margin:1rem 0; }
    article,fieldset,details { border:1px solid #ccd9d1;border-radius:.5rem;padding:1rem;margin:.5rem 0; }
    label { display:flex;flex-direction:column;gap:.3rem;margin:.6rem 0; } label.check { flex-direction:row;align-items:center;gap:.6rem; }
    input:not([type=checkbox]),select { padding:.65rem;border:1px solid #7d9485;border-radius:.3rem;max-width:36rem; }
    summary { cursor:pointer;font-weight:600; } [role=alert] { color:#9e2828; } .substance { padding:.5rem;border-bottom:1px solid #ddd; }
  `]
})
export class ParticipantsComponent {
  readonly campId = input.required<string>();
  readonly local = input(false);
  private readonly api = inject(ParticipantsApiService);
  readonly overview = signal<ParticipantsOverview | null>(null);
  readonly members = signal<ExplicitPermissionMember[]>([]);
  readonly error = signal('');
  readonly busy = signal(false);
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
  constructor() { effect(onCleanup => { this.campId(); this.load(); onCleanup(() => this.requests.unsubscribe()); }); }
  load() {
    this.requests.unsubscribe(); this.requests = new Subscription();
    this.draft = null; this.original = null; this.overview.set(null); this.members.set([]); this.error.set(''); this.busy.set(false);
    this.requests.add(this.api.list(this.campId()).subscribe({ next: data => this.overview.set(data),
      error: () => this.error.set('Teilnehmer konnten nicht geladen werden. Ein explizites Leserecht ist erforderlich.') }));
    if (!this.local()) this.requests.add(this.api.members(this.campId()).subscribe({ next: data => this.members.set(data), error: () => {} }));
  }
  create() { this.original = null; this.draft = { displayName: '', structureNodeId: null, dietTypeId: null, absentDays: [], absentMealIds: [], allergenIds: [], intolerances: [] }; }
  edit(document: ParticipantDocument) { this.original = document; this.draft = structuredClone(document.data); }
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
  save() { if (!this.draft || !this.overview()?.canEdit || this.busy()) return;
    this.mutate(this.original ? this.api.update(this.campId(), this.original, this.draft) : this.api.create(this.campId(), this.draft)); }
  remove(document: ParticipantDocument) { if (!this.overview()?.canEdit || this.busy() || !window.confirm('Diesen Testteilnehmer löschen?')) return;
    this.mutate(this.api.remove(this.campId(), document)); }
  grant(member: ExplicitPermissionMember, permission: string, enabled: boolean) { this.mutate(this.api.grant(this.campId(), member.membershipId, permission, enabled)); }
  private mutate(request: Observable<unknown>) {
    this.busy.set(true); this.error.set('');
    this.requests.add(request.subscribe({ next: () => this.load(), error: failure => { this.busy.set(false);
      this.error.set(failure.status === 409 ? 'Der Stand hat sich geändert oder das Lager ist gesperrt. Eingaben bleiben erhalten; bitte vor erneutem Bearbeiten neu laden.' :
        'Änderung nicht gespeichert. Berechtigung und Eingaben prüfen.'); } }));
  }
}
