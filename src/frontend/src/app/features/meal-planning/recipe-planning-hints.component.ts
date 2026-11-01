import { DatePipe } from '@angular/common';
import { Component, effect, inject, input, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { ActionIconComponent, ActionIconName } from '../../shared/action-icon.component';
import { MealPlanningApiService, RecipePlanningHint, RecipePlanningHints } from './meal-planning-api.service';

@Component({
  selector: 'scp-recipe-planning-hints', standalone: true,
  imports: [DatePipe, MatButtonModule, ActionIconComponent],
  template: `
    <div class="hint-chips" aria-label="Rezeptmerkmale und potenzieller Anpassungsbedarf">
      @if (loading()) { <span class="hint-status" role="status">Hinweise werden geladen …</span> }
      @else if (error()) { <button type="button" class="hint-chip incomplete" (click)="openDetails(details)"><scp-action-icon name="warning"/>Hinweise nicht verfügbar</button> }
      @else if (data(); as result) {
        @for (hint of visibleHints().slice(0, 3); track hint.referenceId) {
          @if (hint.state === 'Compatible') { <span class="hint-chip compatible passive" [title]="hint.explanation"><scp-action-icon [name]="icon(hint)"/>{{ label(hint) }}</span> }
          @else { <button type="button" class="hint-chip" [class.adjustable]="hint.adjustment === 'Available'" [class.uncovered]="hint.adjustment === 'Unavailable'" [title]="hint.explanation + ' ' + adjustmentLabel(hint)" [attr.aria-label]="label(hint) + ' – ' + stateLabel(hint) + '. ' + adjustmentLabel(hint)" (click)="openDetails(details)"><scp-action-icon [name]="icon(hint)"/>{{ label(hint) }}</button> }
        }
        @if (visibleHints().length > 3) { @if (hasDetailHints()) { <button type="button" class="hint-chip" (click)="openDetails(details)">+{{ visibleHints().length - 3 }} Merkmale</button> } @else { <span class="hint-chip passive">+{{ visibleHints().length - 3 }} Merkmale</span> } }
        @if (result.hasUnknownData) { <button type="button" class="hint-chip incomplete" (click)="openDetails(details)"><scp-action-icon name="question"/>Angaben unvollständig</button> }
      }
    </div>
    <dialog #details class="hints-dialog" aria-labelledby="recipe-hints-title">
      <header><div><h3 id="recipe-hints-title">Rezepthinweise</h3><p>{{ name() }}</p></div><button matButton type="button" aria-label="Rezepthinweise schließen" (click)="details.close()">×</button></header>
      <p class="color-key"><span class="adjustable">Gelb: Zutatenlösung möglich</span><span class="uncovered">Rot: nicht abgedeckt</span><span>Grau: unbewertet</span></p>
      <p class="context">Standardrezept ohne Anpassungen. Die gemeinsame Eignung mehrerer Anforderungen bleibt separat zu prüfen.</p>
      @if (loading()) { <p role="status">Hinweise werden geladen …</p> }
      @else if (error()) { <p role="alert">Die Rezeptmerkmale konnten nicht geladen werden. Daraus lässt sich keine Eignung ableiten.</p> }
      @else if (data(); as result) {
        @if (result.hasUnknownData && !hasUnknownDetails()) { <p class="data-warning">Einige Zutatenangaben sind ungeprüft.</p> }
        <div class="hint-details">@for (kind of kinds; track kind.id) { @if (hintsFor(kind.id).length) {
          <section><h4>{{ kind.label }}</h4>
            @for (hint of hintsFor(kind.id); track hint.referenceId) {
              <article class="hint-detail">
                <div class="hint-title"><scp-action-icon [name]="icon(hint)"/><strong>{{ label(hint) }}</strong><span class="state" [class.adjustable]="hint.adjustment === 'Available'" [class.uncovered]="hint.adjustment === 'Unavailable'" [title]="adjustmentLabel(hint)">{{ detailStatus(hint) }}</span></div>
                @if (hint.evidence.length) { <div class="ingredients">@for (ingredient of evidenceNames(hint); track ingredient) { <span class="ingredient">{{ ingredient }}</span> }</div> }
                <details class="hint-explanation"><summary>Begründung</summary><p>{{ hint.explanation }}</p></details>
              </article>
            }
          </section>
        } } @if (!hasDetailHints()) { <p>Keine enthaltenen Stoffe oder offenen Hinweise in den verfügbaren Angaben.</p> }</div>
      }
      <footer>@if (data(); as result) { <span class="evaluated">Stand {{ result.evaluatedAtUtc | date:'dd.MM.yyyy HH:mm' }}</span> }<button matButton="outlined" type="button" [disabled]="loading()" (click)="refresh()">Erneut prüfen</button><button matButton="filled" type="button" (click)="details.close()">Schließen</button></footer>
    </dialog>
  `,
  styles: `
    :host { display:block; margin-top:3px; }
    .hint-chips { display:flex; flex-wrap:wrap; gap:5px; }
    .hint-chip { display:inline-flex; align-items:center; gap:5px; min-height:28px; padding:4px 7px; border:1px solid var(--color-border); border-radius:5px; background:var(--color-canvas); color:var(--color-text-muted); font:inherit; font-size:11px; text-align:left; cursor:pointer; }
    .hint-chip scp-action-icon { width:13px; height:13px; }
    .adjustable,.state.adjustable { color:var(--color-warning-text); background:var(--color-warning-soft); border-color:var(--color-warning-border); }
    .uncovered,.state.uncovered { color:var(--color-danger); background:color-mix(in srgb,var(--color-danger) 10%,var(--color-surface)); border-color:var(--color-danger); }
    .color-key { display:flex; flex-wrap:wrap; gap:6px; margin:12px 0 8px; font-size:11px; }.color-key>span,.adjustment-note { padding:5px 8px; border-radius:5px; }.adjustment-note { width:fit-content; color:var(--color-text-muted); }.adjustment-note.adjustable { color:var(--color-warning-text); }.adjustment-note.uncovered { color:var(--color-danger); }
    .hint-chip.compatible,.state.compatible { background:var(--color-primary-soft); color:var(--color-primary); }
    .hint-chip.passive { cursor:default; }
    button:focus-visible { outline:2px solid var(--color-focus); outline-offset:2px; }
    .hint-status { font-size:12px; color:var(--color-text-muted); }
    dialog { width:min(720px,calc(100vw - 32px)); max-height:calc(100dvh - 40px); overflow:auto; padding:20px; border:1px solid var(--color-border); border-radius:12px; background:var(--color-surface); color:var(--color-text); box-shadow:0 20px 60px #20302940; box-sizing:border-box; }
    dialog::backdrop { background:#182e4066; backdrop-filter:blur(3px); }header { display:flex; justify-content:space-between; gap:16px; align-items:flex-start; }h3 { margin:0; font-size:20px; }header p { margin:6px 0 0; font-size:14px; }p { font-size:13px; line-height:1.5; }.basis,.data-warning { padding:12px; border-radius:8px; background:var(--color-canvas); }.data-warning { color:var(--color-text-muted); background:var(--color-canvas); border-left:3px solid var(--color-info); }.evaluated { color:var(--color-text-muted); font-size:12px; }
    .hint-details { display:grid; gap:12px; }.hint-details h4 { margin:0 0 6px; font-size:14px; }.hint-detail { padding:8px 0; border-top:1px solid var(--color-border); }.hint-title { display:flex; align-items:center; gap:8px; flex-wrap:wrap; font-size:14px; }.hint-title scp-action-icon { width:16px; height:16px; }.hint-detail p { margin:8px 0; }.state { margin-left:auto; padding:4px 8px; border-radius:5px; background:var(--color-canvas); color:var(--color-text-muted); font-size:12px; }.ingredients { margin-top:6px; display:flex; flex-wrap:wrap; align-items:center; gap:5px; font-size:12px; color:var(--color-text-muted); }.ingredient { background:var(--color-surface-muted); padding:3px 6px; border-radius:4px; }
    footer { display:flex; flex-wrap:wrap; align-items:center; justify-content:flex-end; gap:10px; margin-top:16px; }.context { margin:8px 0 16px; color:var(--color-text-muted); font-size:12px; }.hint-explanation { margin-top:5px; }.hint-explanation summary { width:fit-content; cursor:pointer; font-size:12px; color:var(--color-text-muted); }.hint-explanation summary:focus-visible { outline:2px solid var(--color-focus); outline-offset:2px; }.hint-explanation p { font-size:12px; margin:6px 0 0; }.evaluated { margin-right:auto; font-size:11px; }@media(max-width:600px) { dialog { padding:16px; }.state { margin-left:0; }h3 { font-size:18px; } }
  `,
})
export class RecipePlanningHintsComponent {
  readonly campId = input.required<string>();
  readonly revisionId = input.required<string>();
  readonly name = input('');
  readonly data = signal<RecipePlanningHints | null>(null);
  readonly loading = signal(false);
  readonly error = signal(false);
  readonly reload = signal(0);
  private readonly api = inject(MealPlanningApiService);
  readonly kinds = [{ id: 'Allergen', label: 'Allergene' }, { id: 'Diet', label: 'Ernährungsformen' }, { id: 'Substance', label: 'Stoffe und Unverträglichkeiten' }];
  private readonly letters: Record<string, string> = { GLUTEN_CEREALS: 'A', CRUSTACEANS: 'B', EGGS: 'C', FISH: 'D', PEANUTS: 'E', SOYBEANS: 'F', MILK: 'G', TREE_NUTS: 'H', CELERY: 'L', MUSTARD: 'M', SESAME: 'N', SULPHUR_DIOXIDE_AND_SULPHITES: 'O', LUPIN: 'P', MOLLUSCS: 'R' };
  constructor() {
    effect(cleanup => {
      const campId = this.campId(), revisionId = this.revisionId(); this.reload();
      this.data.set(null); this.error.set(false); this.loading.set(true);
      const request = this.api.recipeHints(campId, revisionId).subscribe({
        next: result => { this.data.set(result); this.loading.set(false); },
        error: () => { this.error.set(true); this.loading.set(false); },
      });
      cleanup(() => request.unsubscribe());
    });
  }
  visibleHints() {
    const priority: Record<string, number> = { Contains: 0, Excluded: 1, QuantityDependent: 2, Compatible: 3 };
    return (this.data()?.hints ?? []).filter(hint => hint.state in priority)
      .slice().sort((a, b) => priority[a.state] - priority[b.state]);
  }
  refresh() { this.reload.update(value => value + 1); }
  hintsFor(kind: string) { return (this.data()?.hints ?? []).filter(hint => hint.kind === kind && ['Contains', 'Excluded', 'Unknown', 'QuantityDependent'].includes(hint.state)); }
  hasDetailHints() { return this.kinds.some(kind => this.hintsFor(kind.id).length > 0); }
  hasUnknownDetails() { return (this.data()?.hints ?? []).some(hint => hint.state === 'Unknown'); }
  detailStatus(hint: RecipePlanningHint) {
    return hint.adjustment === 'Available' ? 'Variante möglich' : hint.adjustment === 'Unavailable' ? 'Nicht abgedeckt'
      : hint.state === 'QuantityDependent' ? 'Mengenabhängig' : hint.state === 'Unknown' ? 'Unbekannt' : 'Unbewertet';
  }
  openDetails(dialog: Pick<HTMLDialogElement, 'showModal'>) {
    if (!this.loading() && (this.error() || this.data()?.hasUnknownData || this.hasDetailHints())) dialog.showModal();
  }
  evidenceNames(hint: RecipePlanningHint) { return [...new Set(hint.evidence.map(item => item.ingredientName))]; }
  icon(hint: RecipePlanningHint): ActionIconName { return hint.state === 'Compatible' ? 'check' : hint.state === 'Unknown' ? 'question' : hint.state === 'NotContained' ? 'info' : 'warning'; }
  label(hint: RecipePlanningHint) {
    if (hint.kind === 'Diet') return hint.name;
    return `${hint.name}${this.letters[hint.code] ? ` (${this.letters[hint.code]})` : ''}`;
  }
  stateLabel(hint: RecipePlanningHint) { return ({ Contains: 'Enthalten', NotContained: 'Laut Angaben nicht enthalten', Compatible: 'Herkunftsregeln erfüllt', Excluded: 'Ausgeschlossen', Unknown: 'Unvollständig / ungeprüft', QuantityDependent: 'Mengenabhängig' })[hint.state]; }
  adjustmentLabel(hint: RecipePlanningHint) {
    return hint.adjustment === 'Available' ? 'Durch eine vollständige Zutatenvariante oder Ersatzlösung abdeckbar.'
      : hint.adjustment === 'Unavailable' ? 'Nicht abgedeckt: keine vollständige Zutatenlösung gefunden.'
      : hint.state === 'QuantityDependent' ? 'Abdeckung nicht bewertet: Ein passender Mengen-/Grenzwertkontext ist erforderlich.'
      : 'Die Abdeckung durch Zutatenanpassungen ist nicht abschließend bewertet.';
  }
}
