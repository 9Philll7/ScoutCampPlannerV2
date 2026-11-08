import { Component, effect, inject, input, signal } from '@angular/core';
import { map } from 'rxjs';
import { ParticipantDocument, ParticipantsApiService } from '../camp/participants-api.service';
import { StructureNodeOption } from './meal-planning-api.service';
import { ActionIconComponent } from '../../shared/action-icon.component';

export interface UnitParticipantCount { structureNodeId: string | null; category: string | null; count: number; }

/** Project protected participant data immediately; never retain names or individual profiles in the view. */
export function filteredParticipantCounts(documents: ParticipantDocument[], filter: number): UnitParticipantCount[] {
  const seen = new Set<string>(), rows = new Map<string, UnitParticipantCount>();
  for (const { data } of documents) {
    if (seen.has(data.id)) continue;
    seen.add(data.id);
    const special = data.dietTypeId !== null || data.allergenIds.length > 0 || data.intolerances.length > 0;
    if ((filter === 1 && !special) || (filter === 2 && special)) continue;
    const node = data.structureNodeId ?? null, category = data.category ?? null;
    const key = JSON.stringify([node, category]);
    const row = rows.get(key) ?? { structureNodeId: node, category, count: 0 };
    row.count++; rows.set(key, row);
  }
  return [...rows.values()];
}

export function unitParticipantTotals(rows: UnitParticipantCount[], nodes: StructureNodeOption[], selectedIds: string[]) {
  const parents = new Map(nodes.map(node => [node.id, node.parentId]));
  const selected = new Set(selectedIds);
  if (selectedIds.some(id => !parents.has(id))) return null;
  const covers = (node: string | null) => {
    const visited = new Set<string>();
    while (node !== null && parents.has(node) && !visited.has(node)) {
      if (selected.has(node)) return true;
      visited.add(node); node = parents.get(node) ?? null;
    }
    return false;
  };
  const totals = { total: 0, children: 0, adults: 0, unknown: 0 };
  for (const row of rows.filter(row => covers(row.structureNodeId))) {
    totals.total += row.count;
    if (row.category === 'ChildYouth') totals.children += row.count;
    else if (row.category === 'LeaderAdult') totals.adults += row.count;
    else totals.unknown += row.count;
  }
  return totals;
}

@Component({
  selector: 'scp-cooking-unit-participant-summary', standalone: true, imports: [ActionIconComponent],
  template: `
    <section class="participant-summary" aria-label="Teilnehmerübersicht">
      <header><span class="summary-icon"><scp-action-icon name="people"/></span><h4>Erfasste Teilnehmer</h4><button type="button" [disabled]="loading()" aria-label="Teilnehmerzahlen aktualisieren" (click)="reload.update(value => value + 1)"><scp-action-icon name="refresh"/></button></header>
      <div aria-live="polite">
        @if (loading()) { <p role="status">Teilnehmerzahlen werden geladen …</p> }
        @else if (error()) { <p class="unavailable">{{ error() }}</p> }
        @else if (totals(); as counts) {
          <div class="participant-total"><strong>{{ counts.total }}</strong><span>Teilnehmer</span></div>
          <dl><div><dt>KiJu</dt><dd>{{ counts.children }}</dd></div><div><dt>Leitung / Erwachsene</dt><dd>{{ counts.adults }}</dd></div>@if (counts.unknown) { <div><dt>Kategorie offen</dt><dd>{{ counts.unknown }}</dd></div> }</dl>
        } @else { <p class="unavailable">Die Strukturzuordnung ist unvollständig. Bitte die nicht verfügbaren Zuordnungen korrigieren.</p> }
      </div>
      <p class="summary-note">Vorschau aus Struktur und Filter, einschließlich ungespeicherter Auswahl. Die Anwesenheit kann je Mahlzeit abweichen.</p>
    </section>
  `,
  styles: `
    :host { display:block; } h4,p,dl { margin:0; }
    header { display:flex; align-items:center; gap:10px; margin-bottom:16px; } h4 { flex:1; font-size:16px; line-height:1.3; }
    .summary-icon { display:flex; align-items:center; justify-content:center; width:38px; height:38px; border-radius:50%; background:var(--color-primary-soft); color:var(--color-primary); }
    header button { display:flex; align-items:center; justify-content:center; width:36px; height:36px; border:0; border-radius:6px; background:transparent; color:var(--color-text-muted); cursor:pointer; }
    header button:focus-visible { outline:2px solid var(--color-focus); outline-offset:2px; } header button:disabled { opacity:.5; cursor:default; }
    .participant-total { display:flex; align-items:baseline; gap:10px; margin-bottom:16px; } .participant-total strong { font-size:32px; line-height:1.2; color:var(--color-primary); font-variant-numeric:tabular-nums; } .participant-total span { font-size:14px; }
    dl { display:grid; gap:10px; font-size:13px; } dl>div { display:flex; justify-content:space-between; gap:12px; } dt { color:var(--color-text-muted); } dd { margin:0; font-weight:600; font-variant-numeric:tabular-nums; }
    .summary-note { color:var(--color-text-muted); font-size:12px; line-height:1.5; margin-top:16px; } .unavailable,[role=status] { font-size:13px; line-height:1.5; color:var(--color-text-muted); }
  `,
})
export class CookingUnitParticipantSummaryComponent {
  readonly campId = input.required<string>();
  readonly nodes = input.required<StructureNodeOption[]>();
  readonly selectedIds = input.required<string[]>();
  readonly filter = input(0);
  readonly rows = signal<UnitParticipantCount[] | null>(null);
  readonly loading = signal(false);
  readonly error = signal('');
  readonly reload = signal(0);
  private readonly api = inject(ParticipantsApiService);

  constructor() {
    effect(onCleanup => {
      const camp = this.campId(), filter = this.filter(); this.reload();
      this.rows.set(null); this.error.set('');
      if (![0, 1, 2].includes(filter)) { this.loading.set(false); this.error.set('Der gewählte Verpflegungsfilter ist nicht verfügbar.'); return; }
      this.loading.set(true);
      // Unrestricted counts use the anonymous Camp.View endpoint. Dietary filters retain the existing Health read boundary.
      const request = filter === 0 ? this.api.counts(camp).pipe(map(value => value.counts))
        : this.api.list(camp).pipe(map(value => filteredParticipantCounts(value.participants, filter)));
      const subscription = request.subscribe({ next: rows => { this.rows.set(rows); this.loading.set(false); }, error: () => {
        this.loading.set(false); this.error.set(filter === 0 ? 'Teilnehmerzahlen konnten nicht geladen werden.'
          : 'Gefilterte Teilnehmerzahlen sind nicht verfügbar. Bitte das Teilnehmer-Leserecht prüfen.');
      } });
      onCleanup(() => subscription.unsubscribe());
    });
  }
  totals() { const rows = this.rows(); return rows ? unitParticipantTotals(rows, this.nodes(), this.selectedIds()) : null; }
}
