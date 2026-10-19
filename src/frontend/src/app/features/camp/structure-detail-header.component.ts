import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { ParticipantCounts, ParticipantsApiService } from './participants-api.service';
import { ActionIconComponent } from '../../shared/action-icon.component';

/** Presentation only: values and eligibility remain owned by the existing camp screen. */
@Component({
  selector: 'scp-structure-detail-header',
  imports: [ActionIconComponent],
  template: `
    <header class="detail-heading" [attr.aria-description]="description()">
      <span class="detail-symbol"><scp-action-icon [name]="children() ? 'structure' : 'people'"/></span>
      <div class="detail-heading-text">
        @if (renaming()) { <ng-content select="[titleEditor]"/> }
        @else { <h3 class="structure-detail-title">{{ title() }}<ng-content select="[nameAction]"/></h3> }
        <p class="structure-path">{{ path() }}</p></div>
      <div class="detail-heading-actions"><ng-content select="[detailActions]"/></div>
      @if (editing()) { <span class="mode-label">Bearbeitungsmodus</span> }
    </header>
    @if (!editing()) {
    <div class="detail-statistics">
      <section class="statistic" aria-label="Erfasste Teilnehmer">
        <div class="statistic-heading"><scp-action-icon name="people"/><span>Erfasste Teilnehmer</span></div>
        @if (counts()) {
          <dl class="structure-metrics"><div><dt>KiJu</dt><dd>{{ actual('ChildYouth') }}</dd></div><div><dt>Leiter/Erwachsene</dt><dd>{{ actual('LeaderAdult') }}</dd></div></dl>
          @if (actual(null)) { <p class="statistic-footnote">{{ actual(null) }} Kategorie nicht zugeordnet</p> }
        } @else { <p class="unavailable">{{ countsError() ? 'Nicht verfügbar' : 'Wird geladen …' }}</p> }
      </section>
      <section class="statistic" aria-label="Teilnehmerschätzung">
        <div class="statistic-heading"><scp-action-icon name="people"/><span>Geschätzte Teilnehmer</span></div>
        @if (youth() !== null && adults() !== null) {
          <dl class="structure-metrics"><div><dt>KiJu</dt><dd>{{ youth() }}</dd></div><div><dt>Leiter/Erwachsene</dt><dd>{{ adults() }}</dd></div></dl>
        } @else { <p class="unavailable">Noch nicht verfügbar</p> }
      </section>
      @if (children() > 0) {
        <section class="statistic statistic-children"><div class="statistic-heading"><scp-action-icon name="structure"/><span>Untergruppen</span></div>
          <p class="child-count">{{ children() }}</p><span class="statistic-footnote">Direkt untergeordnet</span></section>
      }
    </div>
    }
  `,
  styles: `
    :host { display: block; }
    .detail-heading { display: grid; grid-template-columns: 56px minmax(0, 1fr) auto; align-items: start; gap: 12px; padding-bottom: 16px; }
    .detail-heading-actions:empty { display:none; }
    @media(max-width:1400px) { .detail-heading-actions { grid-column:2 / -1; justify-self:start; } }
    .detail-symbol { display: grid; place-items: center; width: 56px; height: 56px; border-radius: 10px; background: var(--color-primary-soft); color: var(--color-primary); }
    .detail-symbol scp-action-icon { width: 32px; height: 32px; }
    .structure-detail-title { display:flex; align-items:center; gap:8px; margin: 0 0 8px; font-size: 28px; font-weight: 700; letter-spacing: -.025em; line-height: 1.2; overflow-wrap: anywhere; }
    .structure-path, .detail-description { margin: 6px 0 0; font-size: 14px; color: var(--color-text-muted); overflow-wrap: anywhere; }
    .structure-path { font-weight: 600; }
    .mode-label { grid-column: 2; justify-self: start; font-size: 13px; font-weight: 600; background: var(--color-warning-soft); color: var(--color-warning-text); padding: 4px 10px; border-radius: 6px; }
    .detail-statistics { display: grid; grid-template-columns: repeat(auto-fit,minmax(210px, 1fr)); gap: 10px; padding: 12px 0 16px; border-top: 1px solid var(--color-border); }
    .statistic { padding: 12px 16px; background: var(--color-canvas); border-radius: 8px; min-width: 0; }
    .statistic:only-child { grid-column: 1 / -1; }
    .statistic-heading { display: flex; gap: 8px; align-items: center; font-size: 14px; color: var(--color-text-muted); }
    .statistic-heading scp-action-icon { width: 20px; height: 20px; color: var(--color-primary); }
    .structure-metrics { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; margin: 8px 0 0; }
    .structure-metrics > div { display: flex; flex-direction: column-reverse; }
    .structure-metrics div + div { border-left: 1px solid var(--color-border); padding-left: 20px; }
    .structure-metrics dd, .child-count { margin: 0; font-size: 28px; font-weight: 700; line-height: 1.2; font-variant-numeric: tabular-nums; }
    .structure-metrics dt, .statistic-footnote, .unavailable { margin-top: 6px; font-size: 13px; color: var(--color-text-muted); }
    .child-count { margin-top: 8px; }
    .statistic-footnote { display: block; }
    @media(max-width: 600px) { .detail-heading { grid-template-columns: 48px minmax(0, 1fr); } .detail-symbol { width: 48px; height: 48px; } .structure-detail-title { font-size: 24px; } .detail-statistics { grid-template-columns: minmax(0, 1fr); } .structure-metrics { gap: 12px; } .statistic { padding: 16px; } }

  `
})
export class StructureDetailHeaderComponent {
  readonly campId = input.required<string>();
  readonly nodeIds = input<readonly string[]>([]);
  readonly wholeCamp = input(false);
  readonly revision = input(0);
  readonly counts = signal<ParticipantCounts | null>(null);
  readonly countsError = signal(false);
  private readonly api = inject(ParticipantsApiService);
  readonly selectedCounts = computed(() => this.counts()?.counts.filter(row => this.wholeCamp() || row.structureNodeId !== null && this.nodeIds().includes(row.structureNodeId)) ?? []);
  actual(category: string | null) { return this.selectedCounts().filter(row => row.category === category).reduce((total, row) => total + row.count, 0); }
  constructor() {
    effect(cleanup => {
      this.revision(); this.counts.set(null); this.countsError.set(false);
      const request = this.api.counts(this.campId()).subscribe({ next: value => this.counts.set(value), error: () => this.countsError.set(true) });
      cleanup(() => request.unsubscribe());
    });
  }
  readonly title = input.required<string>();
  readonly renaming = input(false);
  readonly path = input.required<string>();
  readonly description = input.required<string>();
  readonly youth = input<number | null>(null);
  readonly adults = input<number | null>(null);
  readonly children = input(0);
  readonly editing = input(false);
}
