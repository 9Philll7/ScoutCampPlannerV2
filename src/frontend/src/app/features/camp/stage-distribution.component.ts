import { Component, computed, effect, inject, input, output, signal, untracked } from '@angular/core';
import { forkJoin, Subscription } from 'rxjs';
import { CampApiService, ParticipantEstimate } from './camp-api.service';
import { StageChartComponent } from './stage-chart.component';

/** Read-only projection of existing estimates; never mixes estimates and named participants. */
@Component({
  selector: 'scp-stage-distribution',
  imports: [StageChartComponent],
  template: `
    @if (view() === 'chart' && rows()) { <scp-stage-chart title="Geschätzte Teilnehmer" [values]="chartValues()" [scale]="scale()"/> }
    @if (busy()) { <p role="status">Geschätzte Stufenverteilung wird geladen …</p> }
    @if (error()) { <p role="alert">{{ error() }}</p><button type="button" (click)="load()">Erneut versuchen</button> }
      @if (view() === 'table') {
        <h3>Geschätzte Teilnehmer</h3>
        @if (!busy() && !error() && displayRows(); as values) {
          <div class="table-scroll"><table aria-label="Geschätzte Stufenverteilung">
            <thead><tr><th scope="col">Stufe</th><th scope="col">KiJu</th><th scope="col">Leiter/Erwachsene</th><th scope="col">Gesamt</th></tr></thead>
            <tbody>@for (row of values; track row.campStageId) { <tr><th scope="row">{{ row.stageName }}</th><td>{{ row.childYouthCount }}</td><td>{{ row.leaderCount }}</td><td>{{ row.childYouthCount + row.leaderCount }}</td></tr> }
              @empty { <tr><td colspan="4">Keine Stufen mit Teilnehmerzahlen in diesem Bereich.</td></tr> }
            </tbody>
            @if (values.length) { <tfoot><tr><th scope="row">Gesamt</th><td>{{ total('childYouthCount') }}</td><td>{{ total('leaderCount') }}</td><td>{{ total('childYouthCount') + total('leaderCount') }}</td></tr></tfoot> }
          </table></div>
          @if (values.length && total('childYouthCount') + total('leaderCount') === 0) { <p>Stufen sind vorhanden, aber für diesen Bereich sind noch keine Schätzwerte eingetragen.</p> }
        }
      }
  `,
  styles: `
    :host { display: block; min-width:0; }
    summary { cursor: pointer; font-size: 18px; font-weight: 600; padding: 8px 0; } summary span { color: var(--color-text-muted); font-size: 14px; font-weight: 400; }
    p { font-size: 14px; color: var(--color-text-muted); } [role=alert] { color: var(--color-danger); }
    .table-scroll { overflow-x: auto; } table { border-collapse: collapse; width: 100%; min-width: 400px; font-size: 14px; }
    th, td { padding: 12px; border-bottom: 1px solid var(--color-border); text-align: right; font-variant-numeric: tabular-nums; }
    th:first-child { text-align: left; } thead, tfoot { background: var(--color-surface-muted); } tbody th { font-weight: 500; }
    button { min-height: 40px; padding: 8px 12px; border: 1px solid var(--color-border); border-radius: 8px; background: var(--color-surface); color: var(--color-text); font: inherit; cursor: pointer; }
  `
})
export class StageDistributionComponent {
  readonly campId = input.required<string>();
  readonly context = input.required<string>();
  readonly nodeIds = input<readonly string[]>([]);
  readonly stageTotals = input<ParticipantEstimate[] | null>(null);
  readonly scale = input(0);
  readonly maximumChange = output<number>();
  readonly view = input<'chart' | 'table'>('chart');
  readonly visibleStageIds = input<readonly (string | null)[] | null>(null);
  readonly activeStagesChange = output<(string | null)[]>();
  readonly rows = signal<ParticipantEstimate[] | null>(null);
  readonly busy = signal(false);
  readonly error = signal('');
  readonly activeStageIds = computed(() => (this.rows() ?? []).filter(row => row.childYouthCount + row.leaderCount > 0).map(row => row.campStageId));
  readonly displayRows = computed(() => {
    const visible = this.visibleStageIds() ?? this.activeStageIds();
    return this.rows()?.filter(row => visible.includes(row.campStageId)) ?? null;
  });
  readonly chartValues = computed(() => (this.displayRows() ?? []).map(row => ({
    id: row.campStageId, name: row.stageName, youth: row.childYouthCount, adults: row.leaderCount
  })));
  private readonly api = inject(CampApiService);
  private request?: Subscription;
  constructor() {
    effect(() => this.activeStagesChange.emit(this.activeStageIds()));
    effect(() => this.maximumChange.emit(Math.max(this.total('childYouthCount'), this.total('leaderCount'))));
    effect(onCleanup => {
      this.campId(); this.nodeIds(); const totals = this.stageTotals();
      this.rows.set(totals); this.error.set(''); this.busy.set(false);
      if (totals === null && this.nodeIds().length) untracked(() => this.load());
      onCleanup(() => this.request?.unsubscribe());
    });
  }
  load() {
    this.request?.unsubscribe(); this.error.set('');
    const ids = [...new Set(this.nodeIds())];
    if (!ids.length) {
      this.rows.set(this.stageTotals());
      if (this.rows() === null) this.error.set('Die Planungsübersicht ist noch nicht verfügbar.');
      return;
    }
    this.busy.set(true); this.rows.set(null);
    this.request = forkJoin(ids.map(id => this.api.getParticipantEstimates(this.campId(), id))).subscribe({
      next: groups => {
        const totals = new Map<string, ParticipantEstimate>();
        for (const group of groups) for (const row of group) {
          const total = totals.get(row.campStageId) ?? { ...row, childYouthCount: 0, leaderCount: 0 };
          total.childYouthCount += row.childYouthCount; total.leaderCount += row.leaderCount;
          totals.set(row.campStageId, total);
        }
        this.rows.set([...totals.values()]); this.busy.set(false);
      },
      error: () => { this.busy.set(false); this.error.set('Die vollständige Stufenverteilung konnte nicht geladen werden.'); }
    });
  }
  total(key: 'childYouthCount' | 'leaderCount') { return (this.rows() ?? []).reduce((sum, row) => sum + row[key], 0); }
}
