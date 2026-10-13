import { Component, effect, inject, input, output, signal, untracked } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { CommonSupplyChoice, CookingUnitMeal, MealPlanningApiService, RecipeOption, SupplySolution, SupplyPositionOption } from './meal-planning-api.service';

@Component({
  selector: 'scp-supply-solutions', standalone: true,
  imports: [FormsModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule],
  template: `
    <section aria-label="Versorgungslösungen">
      <h4>Versorgungslösungen</h4>
      @if (state().status === 1) { <p>Der berechnete Stand ist veraltet. Bitte neu berechnen.</p> }
      @if (state().subscriptionState !== 2) {
      @for (choice of state().supply?.commonSupplyChoices ?? []; track $index) {
        <fieldset>
          <legend>Gemeinsame oder getrennte Versorgung</legend>
          <p>Die Auswahl ersetzt die Versorgung der betroffenen Gruppen. Danach speichern und neu berechnen.</p>
          @for (together of choice.togetherOptions; track $index) {
            <button matButton type="button" (click)="chooseCommon(choice, [together])" [disabled]="disabled() || busy() || state().status === 1">
              Gemeinsam versorgen: {{ recipeName(together.recipeRevisionId) }} ({{ together.portions }} Portionen)
            </button>
          }
          <button matButton type="button" (click)="chooseCommon(choice, choice.separateSolutions)" [disabled]="disabled() || busy() || state().status === 1">Getrennt versorgen</button>
        </fieldset>
      }
      @for (solution of drafts(); track solution.id) {
        <article>
          <p>{{ isManual(solution) ? 'Manuelle Entscheidung' : 'Automatische Lösung' }}</p>
          @for (quantity of state().supply?.quantities ?? []; track $index) {
            @if (quantity.solutionId === solution.id) {
              <p>{{ quantity.ingredientName }}: {{ quantity.quantity }} {{ quantity.unit }} · {{ ['Linear', 'Fixe Menge', 'Stufenweise'][quantity.scalingMode] }}
                @if (quantity.stepwiseScaling; as step) { · {{ step.quantityPerStep }} {{ quantity.unit }} je {{ step.stepSize }} Portionen }
                <small> (letzter Berechnungsstand)</small>
              </p>
            }
          }
          <mat-form-field><mat-label>Rezeptrevision</mat-label>
            <mat-select [ngModel]="solution.recipeRevisionId" (ngModelChange)="changeRecipe(solution, $event)" [disabled]="disabled() || busy()">
              @for (recipe of recipes(); track recipe.revisionId) { <mat-option [value]="recipe.revisionId">{{ recipe.name }} · Rev. {{ recipe.revisionNumber }}</mat-option> }
            </mat-select>
          </mat-form-field>
          <mat-form-field><mat-label>Portionen</mat-label><input matInput type="number" min="0.01" step="0.01" [(ngModel)]="solution.portions" [disabled]="disabled() || busy()"></mat-form-field>
          <mat-form-field><mat-label>Angebotsgruppe</mat-label>
            <mat-select [(ngModel)]="solution.offerGroupId" [disabled]="disabled() || busy()">
              <mat-option [value]="null">Eigenes Angebot</mat-option>
              @for (offer of offerIds(); track offer; let index = $index) { <mat-option [value]="offer">Angebot {{ index + 1 }}</mat-option> }
            </mat-select>
          </mat-form-field>
          <button matButton type="button" (click)="loadPositions(solution.recipeRevisionId)" [disabled]="busy()">Zutaten und Ersatz bearbeiten</button>
          @for (position of positions()[solution.recipeRevisionId] ?? []; track position.path) {
            @if (!hiddenPosition(solution, position.path)) {
              <mat-form-field><mat-label>{{ position.name }} · Position {{ $index + 1 }}</mat-label>
                <mat-select [ngModel]="selectedPosition(solution, position)" (ngModelChange)="selectPosition(solution, position, $event)" [disabled]="disabled() || busy()">
                  @if (selectedPosition(solution, position) < 0) { <mat-option [value]="-1" disabled>Gespeicherte Auswahl nicht mehr verfügbar</mat-option> }
                  @for (choice of position.choices; track $index; let index = $index) { <mat-option [value]="index">{{ choice.label }}</mat-option> }
                </mat-select>
              </mat-form-field>
            }
          }
          @for (allocation of solution.allocations; track allocation.requirementGroupKey; let index = $index) {
            <mat-form-field><mat-label>Zuordnung Gruppe {{ groupNumber(allocation.requirementGroupKey) }}</mat-label>
              <input matInput type="number" min="0" step="0.01" [(ngModel)]="allocation.portions" [disabled]="disabled() || busy()">
            </mat-form-field>
            <button matButton type="button" (click)="solution.allocations.splice(index, 1)" [disabled]="disabled() || busy()">Zuordnung entfernen</button>
          }
          @for (group of availableGroups(solution); track group.requirements.key) {
            <button matButton type="button" (click)="addAllocation(solution, group.requirements.key)" [disabled]="disabled() || busy()">Gruppe {{ groupNumber(group.requirements.key) }} zuordnen (Bedarf {{ group.portions }})</button>
          }
          <button matButton type="button" (click)="remove(solution.id)" [disabled]="disabled() || busy()">Entfernen</button>
        </article>
      } @empty { <p>Noch keine Lösung. Bitte berechnen oder eine vorgeschlagene Alternative wählen.</p> }
      <button matButton type="button" (click)="add()" [disabled]="disabled() || busy() || !recipes().length">Eigene Lösung hinzufügen</button>
      @for (candidate of state().supply?.candidates ?? []; track $index) {
        <button matButton type="button" (click)="choose(candidate)" [disabled]="disabled() || busy()">Vorschlag: {{ recipeName(candidate.recipeRevisionId) }} ({{ candidate.portions }} Portionen)</button>
      }
      @for (problem of state().supply?.problems ?? []; track $index) {
        <p role="status">{{ problemLabel(problem.code) }} @if (problem.requirementGroupKey) { · Gruppe {{ groupNumber(problem.requirementGroupKey) }} }
          @if (problem.positionPath) { <small> · Position {{ problem.positionPath }}</small> }
        </p>
      }
      @if (error()) { <p role="alert">{{ error() }}</p> }
      <button matButton type="button" (click)="save()" [disabled]="disabled() || busy() || !state().supply">Als manuelle Entscheidungen speichern</button>
      <button matButton type="button" (click)="reset()" [disabled]="disabled() || busy()">Manuelle Entscheidungen zurücksetzen</button>
      } @else { <p>Für diese Mahlzeit ist keine Versorgung erforderlich.</p> }
      @if (state().subscriptionState === 2 && error()) { <p role="alert">{{ error() }}</p> }
      <h4>Verifikation</h4>
      @if (state().verification; as verification) {
        <p>{{ state().verificationStale ? 'Verifikation veraltet' : verification.kind === 0 ? 'Ohne Abweichung verifiziert' : 'Mit Abweichung verifiziert' }} · {{ verification.verifiedAt }}</p>
        @if (verification.reason) { <p>{{ verification.reason }}</p> }
      }
      <p>Erfordert eine separat vergebene Verifikationsberechtigung.</p>
      <mat-form-field><mat-label>Begründung der Abweichung</mat-label><textarea matInput [(ngModel)]="reason" maxlength="2000" [disabled]="busy()"></textarea></mat-form-field>
      <button matButton type="button" (click)="verify(0)" [disabled]="busy() || state().status !== 0 || !!state().supply?.problems?.length">Ohne Abweichung verifizieren</button>
      <button matButton type="button" (click)="verify(1)" [disabled]="busy() || state().status === 1 || !reason.trim()">Mit Abweichung verifizieren</button>
    </section>`,
  styles: [`article { border: 1px solid var(--mat-sys-outline-variant); border-radius: .5rem; padding: .5rem; margin: .5rem 0; } mat-form-field { width: 100%; } small { overflow-wrap: anywhere; }`]
})
export class SupplySolutionsComponent {
  readonly campId = input.required<string>();
  readonly state = input.required<CookingUnitMeal>();
  readonly recipes = input<RecipeOption[]>([]);
  readonly disabled = input(false);
  readonly saved = output<void>();
  readonly verified = output<void>();
  readonly drafts = signal<SupplySolution[]>([]);
  readonly positions = signal<Partial<Record<string, SupplyPositionOption[]>>>({});
  readonly busy = signal(false);
  readonly error = signal('');
  reason = '';
  private baseline = '';
  private readonly api = inject(MealPlanningApiService);
  constructor() { effect(() => {
    const state = this.state();
    const manual = state.manualSupplySolutions ?? [];
    const calculated = state.supply?.solutions ?? [];
    // A pending manual edit must not be replaced by the previous calculation on reload.
    this.drafts.set(structuredClone(manual.length ? [ ...manual, ...calculated.filter(value =>
      !manual.some(item => item.offerGroupId === value.offerGroupId && item.allocations.some(allocation =>
        value.allocations.some(previous => previous.requirementGroupKey === allocation.requirementGroupKey)))) ] : calculated));
    this.baseline = untracked(() => this.draftKey());
  }); }
  add() {
    const recipe = this.recipes()[0];
    if (!recipe) return;
    this.drafts.update(values => [...values, { id: crypto.randomUUID(), recipeRevisionId: recipe.revisionId,
      offerGroupId: null, portions: this.state().effectiveDemand || 1, source: 1, allocations: [], positionSelections: {} }]);
  }
  offerIds() { return [...new Set([...this.drafts(), ...(this.state().supply?.candidates ?? [])].map(value => value.offerGroupId).filter((value): value is string => !!value))]; }
  groups() { return this.state().supply?.requirements ?? this.state().requirementGroups ?? []; }
  availableGroups(solution: SupplySolution) { return this.groups().filter(group => !solution.allocations.some(value => value.requirementGroupKey === group.requirements.key)); }
  addAllocation(solution: SupplySolution, key: string) {
    if (!solution.allocations.some(value => value.requirementGroupKey === key)) solution.allocations.push({ requirementGroupKey: key, portions: 0 });
  }
  loadPositions(revision: string) {
    this.api.supplyPositions(this.campId(), revision).subscribe({
      next: values => this.positions.update(current => ({ ...current, [revision]: values })),
      error: () => this.error.set('Rezeptpositionen konnten nicht geladen werden. Gespeicherte Entscheidungen bleiben erhalten.')
    });
  }
  selectedPosition(solution: SupplySolution, position: SupplyPositionOption) {
    const selected = solution.positionSelections[position.path];
    return position.choices.findIndex(choice => (choice.selection?.replacementRuleId ?? null) === (selected?.replacementRuleId ?? null)
      && (choice.selection?.variantKey ?? null) === (selected?.variantKey ?? null));
  }
  hiddenPosition(solution: SupplySolution, path: string) {
    return Object.entries(solution.positionSelections).some(([parent, selection]) => !!selection.replacementRuleId && path.startsWith(parent + '/'));
  }
  selectPosition(solution: SupplySolution, position: SupplyPositionOption, index: number) {
    const choice = position.choices[index];
    if (!choice) return;
    if (choice.selection) solution.positionSelections[position.path] = { ...choice.selection };
    else delete solution.positionSelections[position.path];
    if (choice.selection?.replacementRuleId) for (const path of Object.keys(solution.positionSelections))
      if (path.startsWith(position.path + '/')) delete solution.positionSelections[path];
  }
  changeRecipe(solution: SupplySolution, recipe: string) { solution.recipeRevisionId = recipe; solution.positionSelections = {}; }
  remove(id: string) { this.drafts.update(values => values.filter(value => value.id !== id)); }
  choose(candidate: SupplySolution) {
    if (this.drafts().some(value => this.decisionKey(value) === this.decisionKey(candidate))) {
      this.error.set('Diese Entscheidung ist bereits vorhanden.'); return;
    }
    this.drafts.update(values => [...values, { ...structuredClone(candidate), id: crypto.randomUUID(), source: 1 }]);
  }
  chooseCommon(choice: CommonSupplyChoice, selected: SupplySolution[]) {
    this.error.set('');
    this.drafts.update(values => [
      ...values.filter(value => value.offerGroupId !== choice.offerGroupId ||
        !value.allocations.some(allocation => choice.requirementGroupKeys.includes(allocation.requirementGroupKey))),
      ...structuredClone(selected).map(value => ({ ...value, id: crypto.randomUUID(), source: 1 }))
    ]);
  }
  private decisionKey(value: SupplySolution) { return JSON.stringify({
    recipe: value.recipeRevisionId, offer: value.offerGroupId, portions: value.portions,
    positions: Object.entries(value.positionSelections).sort(([a], [b]) => a.localeCompare(b)),
    allocations: [...value.allocations].sort((a, b) => a.requirementGroupKey.localeCompare(b.requirementGroupKey))
  }); }
  isManual(solution: SupplySolution) {
    const original = this.state().supply?.solutions.find(value => value.id === solution.id);
    return solution.source === 1 || !original || this.decisionKey(original) !== this.decisionKey(solution);
  }
  hasUnsavedChanges() {
    return this.draftKey() !== this.baseline;
  }
  private draftKey() { return JSON.stringify(this.drafts().map(value => [value.id, this.decisionKey(value)])); }
  groupNumber(key: string) { const index = this.groups().findIndex(value => value.requirements.key === key); return index < 0 ? 'Standard/gespeichert' : index + 1; }
  recipeName(id: string) { return this.recipes().find(value => value.revisionId === id)?.name ?? id; }
  problemLabel(code: number) { return [
    'Keine passende Direktlösung', 'Mehrere Direktlösungen – bitte entscheiden', 'Nur teilweise aufgelöst',
    'Keine passende Angebotsalternative', 'Mehrere passende Angebotsalternativen', 'Stoffdaten unbekannt',
    'Nur qualitative Stoffdaten vorhanden', 'Grenzwert fehlt', 'Grenzwert überschritten', 'Stoff enthalten, Grenzwert fehlt',
    'Allergenkonflikt', 'Allergenangaben unbekannt', 'Herkunft für Ernährungsform ausgeschlossen', 'Herkunftsregel unbekannt',
    'Manuelle Entscheidung nicht mehr geeignet', 'Anforderungsgruppe unterversorgt', 'Anforderungsgruppe überversorgt',
    'Geplante Portionen nicht eindeutig zugeordnet', 'Teilnehmer nicht zugeordnet', 'Gemeinsame oder getrennte Versorgung: Entscheidung erforderlich'
  ][code] ?? 'Versorgungsproblem'; }
  save() { this.submit(this.drafts().filter(value => this.isManual(value)).map(value => ({ ...value, source: 1 }))); }
  reset() { this.submit([]); }
  verify(kind: number) {
    if (this.hasUnsavedChanges()) { this.error.set('Bitte Entscheidungen vor der Verifikation speichern.'); return; }
    this.busy.set(true); this.error.set('');
    this.api.verifySupply(this.campId(), this.state(), kind, this.reason).subscribe({
      next: () => { this.busy.set(false); this.verified.emit(); },
      error: () => { this.busy.set(false); this.error.set('Verifikation fehlgeschlagen. Berechtigung, offene Probleme und aktuellen Berechnungsstand prüfen.'); }
    });
  }
  private submit(solutions: SupplySolution[]) {
    this.busy.set(true); this.error.set('');
    this.api.saveSupply(this.campId(), this.state(), solutions).subscribe({
      next: () => { this.busy.set(false); this.saved.emit(); },
      error: () => { this.busy.set(false); this.error.set('Speichern fehlgeschlagen. Berechtigung und aktuellen Stand prüfen; Änderungen wurden nicht automatisch erneut gesendet.'); }
    });
  }
}
