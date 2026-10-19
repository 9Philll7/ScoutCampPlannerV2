import { Component, computed, input } from '@angular/core';

export interface StageChartValue { id: string | null; name: string; youth: number; adults: number; unknown?: number; }

/** Organizational counts only; bar lengths represent people, never planning progress. */
@Component({
  selector: 'scp-stage-chart',
  template: `
    <section class="chart" [attr.aria-label]="title()">
      <header><h4>{{ title() }}</h4><span>{{ total() }} Teilnehmer</span></header>
      <ul class="legend">@for (stage of values(); track stage.id) {
        <li><i [style.background]="color(stage.id)"></i>{{ stage.name }}</li>
      }</ul>
      @for (category of categories(); track category.key) {
        <div class="bar-row"><span class="category">{{ category.label }}</span>
          <div class="track" role="img" [attr.aria-label]="description(category.key)">
            @for (stage of values(); track stage.id) {
              @if (value(stage, category.key) > 0) {
                <span class="segment" [style.width.%]="value(stage, category.key) / maximum() * 100"
                  [style.background]="color(stage.id)" [attr.title]="stage.name + ': ' + value(stage, category.key)">
                  @if (value(stage, category.key) / maximum() > .09) { {{ value(stage, category.key) }} }
                </span>
              }
            }
          </div><strong>{{ sum(category.key) }}</strong>
        </div>
      }
      <div class="axis" aria-hidden="true"><span>0</span><span>{{ maximum() }} Personen</span></div>
      @if (total() === 0) { <p class="empty">Für diesen Bereich sind noch keine Teilnehmerzahlen vorhanden.</p> }
    </section>
  `,
  styles: `
    :host { display:block; min-width:0; }
    .chart { padding:20px; border:1px solid var(--color-border); border-radius:12px; background:var(--color-surface, #fff); }
    header { display:flex; align-items:baseline; flex-wrap:wrap; gap:8px 16px; justify-content:space-between; }
    h4 { margin:0; font-size:16px; font-weight:650; } header>span { font-size:13px; color:var(--color-text-muted); }
    .legend { display:flex; flex-wrap:wrap; gap:8px 16px; list-style:none; padding:0; margin:16px 0 24px; }
    li { display:flex; align-items:center; gap:6px; font-size:12px; overflow-wrap:anywhere; }
    i { width:9px; height:9px; border-radius:50%; flex-shrink:0; }
    .bar-row { display:grid; grid-template-columns:112px minmax(0,1fr) 36px; align-items:center; gap:10px; margin:12px 0; }
    .category { font-size:12px; color:var(--color-text-muted); } strong { font-size:14px; text-align:right; font-variant-numeric:tabular-nums; }
    .track { display:flex; height:30px; background:var(--color-canvas,#f4f6f4); border-radius:4px; overflow:hidden; }
    .segment { display:flex; align-items:center; justify-content:center; color:white; font-size:12px; font-weight:600; min-width:0; box-shadow:inset -1px 0 #ffffff50; }
    .axis { display:flex; justify-content:space-between; margin:4px 46px 0 122px; border-top:1px solid var(--color-border); padding-top:6px; font-size:11px; color:var(--color-text-muted); }
    .empty { font-size:13px; color:var(--color-text-muted); margin-bottom:0; }
    @media(max-width:600px) { .chart { padding:14px; } .bar-row { grid-template-columns:82px minmax(0,1fr) 28px; gap:6px; } .axis { margin-left:88px; margin-right:34px; } }
  `
})
export class StageChartComponent {
  readonly title = input.required<string>();
  readonly values = input.required<readonly StageChartValue[]>();
  readonly scale = input(0);
  readonly categories = computed(() => [
    { key: 'youth' as const, label: 'KiJu' }, { key: 'adults' as const, label: 'Leiter/Erw.' },
    ...(this.sum('unknown') ? [{ key: 'unknown' as const, label: 'Kategorie offen' }] : [])
  ]);
  readonly total = computed(() => this.sum('youth') + this.sum('adults') + this.sum('unknown'));
  readonly maximum = computed(() => Math.max(1, this.scale(), this.sum('youth'), this.sum('adults'), this.sum('unknown')));
  value(stage: StageChartValue, key: 'youth' | 'adults' | 'unknown') { return stage[key] ?? 0; }
  sum(key: 'youth' | 'adults' | 'unknown') { return this.values().reduce((sum, stage) => sum + this.value(stage, key), 0); }
  description(key: 'youth' | 'adults' | 'unknown') { return this.values().map(stage => `${stage.name}: ${this.value(stage, key)}`).join(', '); }
  color(id: string | null) {
    if (id === null) return '#667085';
    let hash = 0; for (const character of id) hash = (hash * 31 + character.charCodeAt(0)) | 0;
    return ['#32834b', '#2463b4', '#b65d0c', '#bd3545', '#7750a0', '#167c82', '#786234'][Math.abs(hash) % 7];
  }
}
