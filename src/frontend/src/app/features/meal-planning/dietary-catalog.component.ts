import { Component, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { DietaryCatalog, DietaryCatalogApiService, DietaryContribution, DietaryType } from './dietary-catalog-api.service';
import { RequirementOption } from '../camp/participants-api.service';

@Component({
  selector: 'scp-dietary-catalog', standalone: true,
  imports: [FormsModule, MatButtonModule, MatFormFieldModule, MatInputModule, MatSelectModule],
  template: `
    <h2>Ernährungsformen</h2>
    <p>Die Regeln beziehen sich auf die Hauptherkunft der Zutaten. Nicht festgelegte Regeln bleiben prüfbedürftig.</p>
    @if (error()) { <p role="alert">{{ error() }}</p> }
    @if (notice()) { <p role="status">{{ notice() }}</p> }
    @if (catalog(); as data) {
      <button matButton (click)="create()" [disabled]="busy()">Ernährungsform anlegen</button>
      <div class="cards">
        @for (type of data.types; track type.id) {
          <article><strong>{{ type.name }}</strong><p>{{ type.description }}</p>
            <small>{{ type.tenantId ? 'Organisation' : 'Zentral' }} · Version {{ type.version }}</small>
            @if (type.tenantId === tenantId()) {
              <button matButton (click)="edit(type)" [disabled]="busy()">Bearbeiten</button>
              @if (tenantId()) { <button matButton (click)="submit(type)" [disabled]="busy() || type.version === 0">Zentral vorschlagen</button> }
            }
          </article>
        }
      </div>
      @if (draft; as value) {
        <form (ngSubmit)="save()" class="editor">
          <mat-form-field><mat-label>Name</mat-label><input matInput [(ngModel)]="value.name" name="name" maxlength="100" required></mat-form-field>
          <mat-form-field><mat-label>Beschreibung</mat-label><textarea matInput [(ngModel)]="value.description" name="description" maxlength="2000"></textarea></mat-form-field>
          <div class="cards">
            @for (origin of data.origins; track origin.id) {
              <mat-form-field><mat-label>{{ origin.name }}</mat-label>
                <mat-select [value]="rule(value, origin.id)" (selectionChange)="setRule(value, origin.id, $event.value)">
                  <mat-option [value]="null">Nicht festgelegt</mat-option>
                  <mat-option [value]="0">Erlaubt</mat-option><mat-option [value]="1">Ausgeschlossen</mat-option>
                </mat-select>
              </mat-form-field>
            }
          </div>
          <button matButton type="submit" [disabled]="busy() || !value.name.trim()">Speichern</button>
          <button matButton type="button" (click)="draft = null" [disabled]="busy()">Abbrechen</button>
        </form>
      }
      @if (!tenantId()) {
        <h3>Vorgeschlagene Ernährungsformen</h3>
        @for (entry of contributions(); track entry.id) {
          <article><strong>{{ entry.submitted.name }}</strong><p>{{ entry.submitted.description }}</p>
            @for (origin of data.origins; track origin.id) {
              <span>{{ origin.name }}: {{ ruleLabel(rule(entry.submitted, origin.id)) }} · </span>
            }
            <mat-form-field><mat-label>Zentrale Übernahme</mat-label>
              <mat-select [(ngModel)]="targets[entry.id]">
                <mat-option [value]="null">Als neue zentrale Ernährungsform</mat-option>
                @for (type of data.types; track type.id) { <mat-option [value]="type.id">Vorhanden: {{ type.name }}</mat-option> }
              </mat-select>
            </mat-form-field>
            <button matButton (click)="review(entry, true)" [disabled]="busy()">Geprüft übernehmen</button>
            <button matButton (click)="review(entry, false)" [disabled]="busy()">Ablehnen</button>
          </article>
        }
        <details><summary>Optionale Stoff-Vorgabewerte</summary>
          <p>Keine medizinischen Empfehlungen. Nur belegte Vorgaben erfassen. Bestehende Teilnehmerwerte werden nicht geändert.</p>
          @for (substance of data.substances; track substance.id) {
            @if (substance.isQuantityDependent) {
              <article><strong>{{ substance.name }}</strong>
                <mat-form-field><mat-label>g pro Portion (optional)</mat-label>
                  <input matInput type="number" min="0" step="0.000001" [(ngModel)]="substance.defaultThresholdGramsPerPortion">
                </mat-form-field>
                <mat-form-field><mat-label>Quelle der Vorgabe</mat-label><input matInput maxlength="500" [(ngModel)]="substance.defaultThresholdSource"></mat-form-field>
                <button matButton (click)="saveThreshold(substance)" [disabled]="busy()">Vorgabe speichern</button>
              </article>
            }
          }
        </details>
      }
    }
  `,
  styles: [`.cards { display: grid; grid-template-columns: repeat(auto-fit,minmax(220px,1fr)); gap: .7rem; }
    article { padding: .8rem; border: 1px solid #dce5da; border-radius: .7rem; margin: .5rem 0; }
    .editor { padding: 1rem; border: 1px solid #c9d9c7; margin: 1rem 0; } mat-form-field { margin: .3rem; }
    [role=alert] { color: #9e2020; }`]
})
export class DietaryCatalogComponent {
  readonly tenantId = input<string | null>(null);
  readonly catalog = signal<DietaryCatalog | null>(null);
  readonly contributions = signal<DietaryContribution[]>([]);
  readonly error = signal(''); readonly notice = signal(''); readonly busy = signal(false);
  draft: DietaryType | null = null;
  targets: Record<string, string | null> = {};
  private readonly api = inject(DietaryCatalogApiService);
  private generation = 0;
  constructor() { effect(() => { const tenant = this.tenantId(); this.draft = null; this.load(tenant); }); }
  load(tenant = this.tenantId()) {
    const generation = ++this.generation; this.catalog.set(null); this.contributions.set([]); this.error.set('');
    this.api.list(tenant).subscribe({ next: value => { if (generation === this.generation) this.catalog.set(value); },
      error: () => { if (generation === this.generation) this.error.set('Der Katalog konnte nicht geladen werden.'); } });
    if (!tenant) this.api.contributions().subscribe({ next: value => { if (generation === this.generation) this.contributions.set(value); },
      error: () => { if (generation === this.generation) this.error.set('Die Vorschläge konnten nicht geladen werden.'); } });
  }
  create() { this.draft = { id: crypto.randomUUID(), tenantId: this.tenantId(), name: '', description: null, sortOrder: 0, version: 0, rules: [] }; }
  edit(value: DietaryType) { if (value.tenantId === this.tenantId()) this.draft = structuredClone(value); }
  rule(value: DietaryType, id: string) { return value.rules.find(rule => rule.originId === id)?.decision ?? null; }
  ruleLabel(value: number | null) { return value === 0 ? 'Erlaubt' : value === 1 ? 'Ausgeschlossen' : 'Nicht festgelegt'; }
  setRule(value: DietaryType, id: string, decision: number | null) {
    value.rules = value.rules.filter(rule => rule.originId !== id);
    if (decision !== null) value.rules.push({ originId: id, decision });
  }
  save() { if (!this.draft || this.busy()) return; this.mutate(this.api.save(this.tenantId(), this.draft), true); }
  submit(value: DietaryType) { const tenant = this.tenantId(); if (tenant) this.mutate(this.api.submit(tenant, value)); }
  review(value: DietaryContribution, accept: boolean) { this.mutate(this.api.review(value.id, accept, this.targets[value.id] ?? null)); }
  saveThreshold(value: RequirementOption) { this.mutate(this.api.saveThreshold(value)); }
  private mutate(operation: import('rxjs').Observable<unknown>, closeDraft = false) {
    if (this.busy()) return;
    const generation = this.generation; this.busy.set(true); this.error.set(''); this.notice.set('');
    operation.subscribe({ next: () => {
      this.busy.set(false); if (generation !== this.generation) return;
      if (closeDraft) this.draft = null; this.notice.set('Gespeichert.'); this.load();
    }, error: error => { this.busy.set(false); if (generation !== this.generation) return;
      this.error.set(error.status === 409 ? 'Stand geändert oder Name bereits vorhanden. Eingaben bleiben erhalten; bitte den Stand prüfen.' :
        'Nicht gespeichert. Bitte Angaben und Berechtigung prüfen.'); } });
  }
}
