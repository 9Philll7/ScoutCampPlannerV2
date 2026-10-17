import { Component, input } from '@angular/core';
import { ActionIconComponent } from '../../shared/action-icon.component';

/** Presentation only: values and eligibility remain owned by the existing camp screen. */
@Component({
  selector: 'scp-structure-detail-header',
  imports: [ActionIconComponent],
  template: `
    <header class="detail-heading">
      <span class="detail-symbol"><scp-action-icon [name]="children() ? 'structure' : 'people'"/></span>
      <div class="detail-heading-text"><h3 class="structure-detail-title">{{ title() }}</h3>
        <p class="structure-path">{{ path() }}</p><p class="detail-description">{{ description() }}</p></div>
      @if (editing()) { <span class="mode-label">Bearbeitungsmodus</span> }
    </header>
    @if (!editing()) {
    <div class="detail-statistics">
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
    .detail-heading { display: grid; grid-template-columns: 64px minmax(0, 1fr); align-items: start; gap: 16px; padding-bottom: 24px; }
    .detail-symbol { display: grid; place-items: center; width: 64px; height: 64px; border-radius: 12px; background: var(--color-primary-soft); color: var(--color-primary); }
    .detail-symbol scp-action-icon { width: 32px; height: 32px; }
    .structure-detail-title { margin: 0 0 8px; font-size: 28px; font-weight: 700; letter-spacing: -.025em; line-height: 1.2; overflow-wrap: anywhere; }
    .structure-path, .detail-description { margin: 6px 0 0; font-size: 14px; color: var(--color-text-muted); overflow-wrap: anywhere; }
    .structure-path { font-weight: 600; }
    .mode-label { grid-column: 2; justify-self: start; font-size: 13px; font-weight: 600; background: var(--color-warning-soft); color: var(--color-warning-text); padding: 4px 10px; border-radius: 6px; }
    .detail-statistics { display: grid; grid-template-columns: minmax(0, 2fr) minmax(0, 1fr); gap: 16px; padding: 20px 0 24px; border-top: 1px solid var(--color-border); }
    .statistic { padding: 20px; background: var(--color-canvas); border-radius: 10px; min-width: 0; }
    .statistic:only-child { grid-column: 1 / -1; }
    .statistic-heading { display: flex; gap: 8px; align-items: center; font-size: 14px; color: var(--color-text-muted); }
    .statistic-heading scp-action-icon { width: 20px; height: 20px; color: var(--color-primary); }
    .structure-metrics { display: grid; grid-template-columns: 1fr 1fr; gap: 20px; margin: 16px 0 0; }
    .structure-metrics > div { display: flex; flex-direction: column-reverse; }
    .structure-metrics div + div { border-left: 1px solid var(--color-border); padding-left: 20px; }
    .structure-metrics dd, .child-count { margin: 0; font-size: 32px; font-weight: 700; line-height: 1.2; font-variant-numeric: tabular-nums; }
    .structure-metrics dt, .statistic-footnote, .unavailable { margin-top: 6px; font-size: 13px; color: var(--color-text-muted); }
    .child-count { margin-top: 16px; }
    .statistic-footnote { display: block; }
    @media(max-width: 600px) { .detail-heading { grid-template-columns: 48px minmax(0, 1fr); } .detail-symbol { width: 48px; height: 48px; } .structure-detail-title { font-size: 24px; } .detail-statistics { grid-template-columns: minmax(0, 1fr); } .structure-metrics { gap: 12px; } .statistic { padding: 16px; } }

  `
})
export class StructureDetailHeaderComponent {
  readonly title = input.required<string>();
  readonly path = input.required<string>();
  readonly description = input.required<string>();
  readonly youth = input<number | null>(null);
  readonly adults = input<number | null>(null);
  readonly children = input(0);
  readonly editing = input(false);
}
