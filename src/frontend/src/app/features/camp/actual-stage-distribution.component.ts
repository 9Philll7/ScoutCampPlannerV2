import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { ParticipantCounts, ParticipantsApiService } from './participants-api.service';

@Component({
  selector: 'scp-actual-stage-distribution',
  template: `
    <section aria-label="Erfasste Teilnehmer">
      @if (error()) { <p role="alert">Erfasste Teilnehmerzahlen konnten nicht geladen werden.</p> }
      @else if (data()) {
        <h3>Erfasste Teilnehmer: {{ total() }}</h3>
        <p>KiJu: {{ count('ChildYouth') }} · Leiter/Erwachsene: {{ count('LeaderAdult') }} · Kategorie nicht zugeordnet: {{ count(null) }}</p>
        @if (wholeCamp()) { <p>Ohne Strukturzuordnung: {{ unassignedStructure() }}</p> }
        <details><summary>Stufenverteilung (erfasst)</summary>
          <div class="scroll"><table><thead><tr><th>Stufe</th><th>KiJu</th><th>Leiter/Erwachsene</th><th>Kategorie offen</th></tr></thead>
            <tbody>@for (stage of stages(); track stage.id) { <tr><th>{{ stage.name }}</th><td>{{ count('ChildYouth', stage.id) }}</td><td>{{ count('LeaderAdult', stage.id) }}</td><td>{{ count(null, stage.id) }}</td></tr> }</tbody>
          </table></div>
        </details>
      } @else { <p role="status">Erfasste Teilnehmerzahlen werden geladen …</p> }
    </section>`,
  styles: `:host { display:block; margin-top:24px; border-top:1px solid var(--color-border); padding-top:16px; }
    h3 { font-size:20px; } p { color:var(--color-text-muted); } summary { cursor:pointer; padding:8px 0; }
    .scroll { overflow:auto; } table { width:100%; border-collapse:collapse; } th,td { padding:12px; border-bottom:1px solid var(--color-border); text-align:right; } th:first-child { text-align:left; }`
})
export class ActualStageDistributionComponent {
  readonly campId = input.required<string>();
  readonly nodeIds = input<readonly string[]>([]);
  readonly wholeCamp = input(false);
  readonly data = signal<ParticipantCounts | null>(null);
  readonly error = signal(false);
  private readonly api = inject(ParticipantsApiService);
  readonly selected = computed(() => this.data()?.counts.filter(value => this.wholeCamp() ||
    value.structureNodeId !== null && this.nodeIds().includes(value.structureNodeId)) ?? []);
  readonly total = computed(() => this.selected().reduce((sum, row) => sum + row.count, 0));
  readonly unassignedStructure = computed(() => this.data()?.counts.filter(row => row.structureNodeId === null).reduce((sum, row) => sum + row.count, 0) ?? 0);
  readonly stages = computed(() => [...(this.data()?.stages ?? []), { id: null, name: 'Stufe nicht zugeordnet' }]);
  constructor() {
    effect(cleanup => {
      this.data.set(null); this.error.set(false);
      const request = this.api.counts(this.campId()).subscribe({ next: value => this.data.set(value), error: () => this.error.set(true) });
      cleanup(() => request.unsubscribe());
    });
  }
  count(category: string | null, stageId?: string | null) {
    return this.selected().filter(row => row.category === category && (stageId === undefined || row.campStageId === stageId))
      .reduce((sum, row) => sum + row.count, 0);
  }
}
