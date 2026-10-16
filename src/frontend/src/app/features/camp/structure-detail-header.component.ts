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
        <scp-action-icon class="statistic-icon" name="people"/>
        <span class="statistic-label">Geschätzte Teilnehmer</span>
        @if (youth() !== null && adults() !== null) {
          <p class="estimate-pair" aria-hidden="true">{{ youth() }} <span>/</span> {{ adults() }}</p>
          <span class="statistic-footnote" aria-hidden="true">KiJu / Leiter-Erwachsene</span>
          <dl class="structure-metrics"><div><dt>KiJu</dt><dd>{{ youth() }}</dd></div><div><dt>Leiter/Erwachsene</dt><dd>{{ adults() }}</dd></div></dl>
        } @else { <p class="unavailable">Noch nicht verfügbar</p> }
      </section>
      @if (children() > 0) {
        <section class="statistic"><scp-action-icon class="statistic-icon" name="structure"/><span class="statistic-label">Untergruppen</span>
          <p class="child-count">{{ children() }}</p><span class="statistic-footnote">Direkt untergeordnet</span></section>
      }
    </div>
    }
  `,
  styles: `
    :host { display: block; }
    .detail-heading { display: flex; align-items: flex-start; gap: var(--space-4); padding-bottom: var(--space-4); }
    .detail-symbol { display: grid; place-items: center; flex: 0 0 64px; height: 64px; border-radius: var(--radius-panel); background: var(--color-primary-soft); color: var(--color-primary); }
    .detail-symbol scp-action-icon { width: 32px; height: 32px; }
    .detail-heading-text { flex: 1; min-width: 0; }
    :host .structure-detail-title { margin: 0 0 var(--space-2); font-size: 24px; font-weight: 700; letter-spacing: -.025em; line-height: 1.3; overflow-wrap: anywhere; }
    .structure-path, .detail-description { margin: var(--space-1) 0; font-size: var(--font-small); color: var(--color-text-muted); overflow-wrap: anywhere; }
    .structure-path { font-weight: 600; }
    .mode-label { font-size: var(--font-small); background: var(--color-primary-soft); padding: var(--space-1) var(--space-2); border-radius: var(--radius-small); }
    .detail-statistics { display: grid; grid-template-columns: minmax(0, 3fr) minmax(0, 2fr); gap: var(--space-3); margin-bottom: var(--space-3); }
    .statistic { position: relative; border: 1px solid var(--color-border); border-radius: var(--radius-small); padding: var(--space-3) var(--space-3) var(--space-3) 56px; background: var(--color-surface); min-width: 0; }
    .statistic-icon { position: absolute; left: var(--space-3); top: 50%; transform: translateY(-50%); width: 28px; height: 28px; color: var(--color-primary); }
    .statistic:only-child { grid-column: 1 / -1; }
    .statistic-label { display: flex; gap: var(--space-2); align-items: center; color: var(--color-text-muted); font-size: var(--font-small); }
    :host .structure-metrics { position: absolute; width: 1px; height: 1px; overflow: hidden; clip-path: inset(50%); margin: 0; padding: 0; border: 0; }
    .estimate-pair { margin: var(--space-1) 0; font-size: 28px; line-height: 1.3; font-weight: 700; font-variant-numeric: tabular-nums; }
    .estimate-pair span { font-weight: 400; color: var(--color-text-muted); }
    .structure-metrics > div { display: flex; flex-direction: column-reverse; }
    .structure-metrics dt, .statistic-footnote, .unavailable { font-size: var(--font-small); color: var(--color-text-muted); }
    :host .structure-metrics dd, .child-count { margin: 0; font-size: 28px; line-height: 1.3; font-weight: 700; font-variant-numeric: tabular-nums; }
    .child-count { margin-top: var(--space-3); }
    @media(max-width: 600px) { .detail-heading { flex-wrap: wrap; } .detail-symbol { flex-basis: 48px; height: 48px; } .detail-statistics { grid-template-columns: minmax(0, 1fr); } }
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
