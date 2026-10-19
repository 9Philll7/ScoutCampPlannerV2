import { Component, computed, effect, inject, input, output, signal } from '@angular/core';
import { ParticipantCounts, ParticipantsApiService } from './participants-api.service';
import { StageChartComponent } from './stage-chart.component';

@Component({
  selector: 'scp-actual-stage-distribution',
  imports: [StageChartComponent],
  template: `
    <section aria-label="Erfasste Teilnehmer">
      @if (error()) { <p role="alert">Erfasste Teilnehmerzahlen konnten nicht geladen werden.</p> }
      @else if (data()) {
        @if (view() === 'chart') {
          <scp-stage-chart title="Erfasste Teilnehmer" [values]="chartValues()" [scale]="scale()"/>
        } @else {
          <h3>Erfasste Teilnehmer</h3>
          <div class="scroll"><table aria-label="Erfasste Stufenverteilung"><thead><tr><th scope="col">Stufe</th><th scope="col">KiJu</th><th scope="col">Leiter/Erwachsene</th>@if (count(null) > 0) { <th scope="col">Kategorie offen</th> }<th scope="col">Gesamt</th></tr></thead>
            <tbody>@for (stage of stages(); track stage.id) { <tr><th scope="row">{{ stage.name }}</th><td>{{ count('ChildYouth', stage.id) }}</td><td>{{ count('LeaderAdult', stage.id) }}</td>@if (count(null) > 0) { <td>{{ count(null, stage.id) }}</td> }<td>{{ count('ChildYouth', stage.id) + count('LeaderAdult', stage.id) + count(null, stage.id) }}</td></tr> }
            @empty { <tr><td [attr.colspan]="count(null) > 0 ? 5 : 4">Keine Stufen mit Teilnehmerzahlen in diesem Bereich.</td></tr> }</tbody>
            @if (stages().length) { <tfoot><tr><th scope="row">Gesamt</th><td>{{ count('ChildYouth') }}</td><td>{{ count('LeaderAdult') }}</td>@if (count(null) > 0) { <td>{{ count(null) }}</td> }<td>{{ total() }}</td></tr></tfoot> }
          </table></div>
        }
        @if (wholeCamp()) { <p>Ohne Strukturzuordnung: {{ unassignedStructure() }}</p> }
      } @else { <p role="status">Erfasste Teilnehmerzahlen werden geladen …</p> }
    </section>`,
  styles: `:host { display:block; min-width:0; }
    h3 { font-size:20px; } p { color:var(--color-text-muted); } summary { cursor:pointer; padding:8px 0; }
    .scroll { overflow:auto; } table { width:100%; border-collapse:collapse; font-size:14px; } th,td { padding:12px; border-bottom:1px solid var(--color-border); text-align:right; font-variant-numeric:tabular-nums; } th:first-child { text-align:left; } thead,tfoot { background:var(--color-surface-muted); } tbody th { font-weight:500; }`
})
export class ActualStageDistributionComponent {
  readonly campId = input.required<string>();
  readonly nodeIds = input<readonly string[]>([]);
  readonly wholeCamp = input(false);
  readonly scale = input(0);
  readonly view = input<'chart' | 'table'>('chart');
  readonly visibleStageIds = input<readonly (string | null)[] | null>(null);
  readonly activeStagesChange = output<(string | null)[]>();
  readonly maximumChange = output<number>();
  readonly data = signal<ParticipantCounts | null>(null);
  readonly error = signal(false);
  private readonly api = inject(ParticipantsApiService);
  readonly selected = computed(() => this.data()?.counts.filter(value => this.wholeCamp() ||
    value.structureNodeId !== null && this.nodeIds().includes(value.structureNodeId)) ?? []);
  readonly total = computed(() => this.selected().reduce((sum, row) => sum + row.count, 0));
  readonly unassignedStructure = computed(() => this.data()?.counts.filter(row => row.structureNodeId === null).reduce((sum, row) => sum + row.count, 0) ?? 0);
  readonly activeStageIds = computed(() => [...new Set(this.selected().filter(row => row.count > 0).map(row => row.campStageId))]);
  readonly stages = computed(() => {
    const visible = this.visibleStageIds() ?? this.activeStageIds();
    return [...(this.data()?.stages ?? []), { id: null, name: 'Stufe nicht zugeordnet' }]
      .filter(stage => visible.includes(stage.id));
  });
  readonly chartValues = computed(() => this.stages().map(stage => ({
    id: stage.id, name: stage.name, youth: this.count('ChildYouth', stage.id),
    adults: this.count('LeaderAdult', stage.id), unknown: this.count(null, stage.id)
  })));
  constructor() {
    effect(() => this.activeStagesChange.emit(this.activeStageIds()));
    effect(() => this.maximumChange.emit(Math.max(this.count('ChildYouth'), this.count('LeaderAdult'), this.count(null))));
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
