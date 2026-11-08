import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { map } from 'rxjs';
import { ParticipantDocument, ParticipantsApiService } from '../camp/participants-api.service';
import { RequirementLabelCatalog, RequirementSignature, StructureNodeOption } from './meal-planning-api.service';
import { emptyRequirementCatalog, requirementDescription } from './operative-meal-view';
import { unitParticipantTotals } from './cooking-unit-participant-summary.component';
import { CookingUnitStructure } from './cooking-unit-structure';
import { ActionIconComponent } from '../../shared/action-icon.component';

interface UnitRequirementCount { nodeId: string | null; requirements: RequirementSignature; count: number; }
const normalize = (id: string) => id.replaceAll('-', '').toLowerCase();

/** Aggregate immediately: names, individual IDs, absence data and medical sources are not retained. */
export function unitRequirementCounts(documents: ParticipantDocument[]): UnitRequirementCount[] {
  const seen = new Set<string>(), rows = new Map<string, UnitRequirementCount>();
  for (const { data } of documents) {
    if (seen.has(data.id)) continue; seen.add(data.id);
    const signature = { dietTypeId: data.dietTypeId ? normalize(data.dietTypeId) : null,
      allergenIds: [...new Set(data.allergenIds.map(normalize))].sort(),
      intolerances: data.intolerances.map(value => ({ substanceId: normalize(value.substanceId), thresholdGramsPerPortion: value.thresholdGramsPerPortion }))
        .sort((a, b) => a.substanceId.localeCompare(b.substanceId)) };
    const requirements = { ...signature, key: JSON.stringify(signature) }, nodeId = data.structureNodeId ?? null;
    const key = JSON.stringify([nodeId, requirements.key]);
    const row = rows.get(key) ?? { nodeId, requirements, count: 0 }; row.count++; rows.set(key, row);
  }
  return [...rows.values()];
}

export function scopedRequirementCounts(rows: UnitRequirementCount[], nodes: StructureNodeOption[], selectedIds: string[], filter: number) {
  if (!new CookingUnitStructure(nodes).valid || selectedIds.some(id => !nodes.some(node => node.id === id)) || ![0, 1, 2].includes(filter)) return null;
  const groups = new Map<string, { requirements: RequirementSignature; count: number }>();
  for (const row of rows) {
    const special = !!row.requirements.dietTypeId || !!row.requirements.allergenIds?.length || !!row.requirements.intolerances?.length;
    if ((filter === 1 && !special) || (filter === 2 && special)) continue;
    const count = unitParticipantTotals([{ structureNodeId: row.nodeId, category: null, count: row.count }], nodes, selectedIds)!.total;
    if (!count) continue;
    const group = groups.get(row.requirements.key) ?? { requirements: row.requirements, count: 0 }; group.count += count; groups.set(row.requirements.key, group);
  }
  return [...groups.values()];
}

@Component({
  selector: 'scp-cooking-unit-requirements', standalone: true, imports: [ActionIconComponent],
  template: `
    <section aria-label="Anforderungsgruppen der Kocheinheit">
      <header><h4><scp-action-icon name="cooking"/>Anforderungsgruppen</h4><button type="button" aria-label="Anforderungsgruppen aktualisieren" [disabled]="loading()" (click)="reload.update(value => value + 1)"><scp-action-icon name="refresh"/></button></header>
      @if (loading()) { <p role="status">Anforderungsgruppen werden geladen …</p> }
      @else if (error()) { <p class="unavailable">{{ error() }}</p> }
      @else if (groups(); as values) {
        @if (values.length) { <table><thead><tr><th>Anforderungen</th><th>Teilnehmer</th></tr></thead><tbody>
          @for (group of values; track group.requirements.key) { <tr><td>{{ label(group.requirements) }}</td><td>{{ group.count }}</td></tr> }
        </tbody><tfoot><tr><th>Gesamt</th><td>{{ total() }}</td></tr></tfoot></table> }
        @else { <p>Keine erfassten Teilnehmer für diese Struktur- und Filterauswahl.</p> }
      } @else { <p class="unavailable">Strukturzuordnung oder Verpflegungsfilter bitte prüfen.</p> }
      <p class="note">Erfasster Bestand für die aktuelle Struktur- und Filterauswahl, einschließlich ungespeicherter Änderungen. Anwesenheit und Bedarf können je Mahlzeit abweichen.</p>
    </section>
  `,
  styles: `
    :host { display:block; min-width:0; } h4,p { margin:0; } header,h4 { display:flex; align-items:center; gap:10px; } header { justify-content:space-between; margin-bottom:12px; } h4 { font-size:16px; } h4 scp-action-icon { color:var(--color-primary); }
    button { display:grid; place-items:center; width:36px; height:36px; border:0; border-radius:6px; background:transparent; color:var(--color-text-muted); cursor:pointer; } button:focus-visible { outline:2px solid var(--color-focus); outline-offset:2px; }
    table { width:100%; border-collapse:collapse; font-size:13px; } th,td { padding:12px 8px; text-align:left; border-bottom:1px solid var(--color-border); } th { color:var(--color-text-muted); font-weight:500; } td:first-child { overflow-wrap:anywhere; } th:last-child,td:last-child { text-align:right; width:90px; font-variant-numeric:tabular-nums; } tfoot td,tfoot th { font-weight:650; border-bottom:0; }
    .note,.unavailable,[role=status] { font-size:12px; line-height:1.5; color:var(--color-text-muted); } .note { margin-top:14px; }
  `,
})
export class CookingUnitRequirementsComponent {
  readonly campId = input.required<string>(); readonly nodes = input.required<StructureNodeOption[]>();
  readonly selectedIds = input.required<string[]>(); readonly filter = input(0);
  readonly rows = signal<UnitRequirementCount[] | null>(null); readonly catalog = signal<RequirementLabelCatalog>(emptyRequirementCatalog);
  readonly loading = signal(false); readonly error = signal(''); readonly reload = signal(0);
  private readonly api = inject(ParticipantsApiService);
  constructor() {
    effect(onCleanup => {
      const camp = this.campId(); this.reload(); this.rows.set(null); this.catalog.set(emptyRequirementCatalog); this.error.set(''); this.loading.set(true);
      const subscription = this.api.list(camp).pipe(map(value => {
        const catalog = value.catalog ?? emptyRequirementCatalog;
        return { rows: unitRequirementCounts(value.participants), catalog: {
          dietTypes: catalog.dietTypes.map(value => ({ id: value.id, name: value.name })),
          allergens: catalog.allergens.map(value => ({ id: value.id, name: value.name, code: value.code })),
          substances: catalog.substances.map(value => ({ id: value.id, name: value.name }))
        } };
      })).subscribe({
        next: value => { this.rows.set(value.rows); this.catalog.set(value.catalog); this.loading.set(false); },
        error: () => { this.loading.set(false); this.error.set('Anforderungsgruppen sind nicht verfügbar. Bitte das Teilnehmer-Leserecht prüfen.'); }
      });
      onCleanup(() => subscription.unsubscribe());
    });
  }
  readonly groups = computed(() => { const rows = this.rows(); return rows ? scopedRequirementCounts(rows, this.nodes(), this.selectedIds(), this.filter()) : null; });
  total() { return this.groups()?.reduce((sum, group) => sum + group.count, 0) ?? 0; }
  label(requirements: RequirementSignature) { return requirementDescription(requirements, this.catalog()); }
}
