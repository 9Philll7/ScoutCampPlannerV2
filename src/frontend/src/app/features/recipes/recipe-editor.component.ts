import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { IngredientCatalogEntry } from '../camp/camp-api.service';
import { ActionIconComponent } from '../../shared/action-icon.component';
import { RecipeCatalogEntry, RecipeEditorApiService, RecipeEditorContent,
  RecipeEditorDraft, RecipeEditorIngredientPosition } from './recipe-editor-api.service';

@Component({
  selector: 'scp-recipe-editor',
  standalone: true,
  imports: [FormsModule, MatButtonModule, MatCardModule, MatCheckboxModule, MatFormFieldModule,
    MatInputModule, MatProgressSpinnerModule, MatSelectModule, ActionIconComponent],
  template: `
    <div class="recipe-heading">
      <div><p class="eyebrow">Rezepte</p><h3>{{ selected() ? selected()!.content.name || 'Neues Rezept' : 'Lagerrezepte' }}</h3></div>
      @if (selected()) {
        <button matButton type="button" (click)="closeEditor()"><scp-action-icon name="back"/>Rezeptliste</button>
      } @else {
        <button matButton="filled" type="button" (click)="createDraft()" [disabled]="disabled() || loading()">
          <scp-action-icon name="add"/>Rezept anlegen</button>
      }
    </div>
    @if (error()) { <p class="recipe-message error" role="alert">{{ error() }}</p> }
    @if (notice()) { <p class="recipe-message notice" role="status">{{ notice() }}</p> }
    @if (loading()) {
      <div class="recipe-loading"><mat-spinner diameter="28"/><span>Rezepte werden geladen …</span></div>
    } @else if (selected(); as draft) {
      <form (ngSubmit)="save()" class="recipe-form">
        <section class="recipe-section">
          <div class="section-title"><h4>Grunddaten</h4></div>
          <div class="field-grid">
            <mat-form-field appearance="outline"><mat-label>Name</mat-label>
              <input matInput name="recipeName" [(ngModel)]="draft.content.name" maxlength="200" [disabled]="disabled()">
            </mat-form-field>
            <mat-form-field appearance="outline"><mat-label>Standardportionen</mat-label>
              <input matInput name="referenceServings" type="number" min="0.01" step="0.01"
                [(ngModel)]="draft.content.referenceServings" [disabled]="disabled()">
            </mat-form-field>
          </div>
          <mat-form-field appearance="outline" class="full"><mat-label>Beschreibung und Zubereitung</mat-label>
            <textarea matInput name="description" rows="4" [(ngModel)]="draft.content.description" [disabled]="disabled()"></textarea>
          </mat-form-field>
          <mat-form-field appearance="outline" class="full"><mat-label>Quelle</mat-label>
            <input matInput name="source" [(ngModel)]="draft.content.source" [disabled]="disabled()">
          </mat-form-field>
          <mat-checkbox name="ageScaling" [(ngModel)]="draft.content.defaultAgeGroupScalingApplies" [disabled]="disabled()">
            Stufenfaktoren standardmäßig anwenden
          </mat-checkbox>
        </section>

        <section class="recipe-section">
          <div class="section-title"><div><h4>Zutaten</h4><p>Mengen gelten für die angegebenen Standardportionen.</p></div></div>
          <div class="position-list">
            @for (position of draft.content.ingredientPositions; track position.id; let index = $index) {
              <article class="position-card">
                <div class="position-name"><strong>{{ ingredient(position)?.name ?? 'Unbekannte Zutat' }}</strong>
                  <small>{{ scopeLabel(ingredient(position)?.scope) }}</small></div>
                <mat-form-field appearance="outline"><mat-label>Menge</mat-label>
                  <input matInput type="number" min="0.000001" step="any" [name]="'quantity-' + position.id"
                    [(ngModel)]="position.quantity" [disabled]="disabled()">
                </mat-form-field>
                <mat-form-field appearance="outline"><mat-label>Einheit</mat-label>
                  <mat-select [name]="'unit-' + position.id" [(ngModel)]="position.unitId" [disabled]="disabled()">
                    @for (unit of ingredient(position)?.units ?? []; track unit.unitId) {
                      <mat-option [value]="unit.unitId">{{ unit.name }} ({{ unit.symbol }})</mat-option>
                    }
                  </mat-select>
                </mat-form-field>
                <button matIconButton type="button" aria-label="Zutat entfernen" (click)="removePosition(index)"
                  [disabled]="disabled()"><scp-action-icon name="remove"/></button>
              </article>
            } @empty { <p class="empty">Dem Rezept wurden noch keine Zutaten hinzugefügt.</p> }
          </div>

          @if (!disabled()) {
            <div class="ingredient-picker">
              <mat-form-field appearance="outline"><mat-label>Zutat suchen</mat-label>
                <input matInput name="recipeIngredientSearch" [ngModel]="ingredientSearch()"
                  (ngModelChange)="ingredientSearch.set($event)" autocomplete="off">
              </mat-form-field>
              @if (ingredientSearch().trim()) {
                <div class="candidate-list">
                  @for (candidate of ingredientCandidates(); track candidate.revisionId) {
                    <button matButton type="button" (click)="addIngredient(candidate)">
                      <scp-action-icon name="add"/><span>{{ candidate.name }}<small>{{ scopeLabel(candidate.scope) }}</small></span>
                    </button>
                  } @empty { <p class="empty">Keine passende veröffentlichte Zutat gefunden.</p> }
                </div>
              }
            </div>
          }
        </section>

        <div class="recipe-actions">
          <button matButton="filled" type="submit" [disabled]="disabled() || saving() || !changed()">
            <scp-action-icon name="save"/>Speichern</button>
        </div>
      </form>
    } @else {
      <div class="recipe-grid">
        @for (recipe of recipes(); track recipe.libraryEntryId ?? recipe.recipeId) {
          <mat-card><mat-card-header><mat-card-title>{{ recipe.name }}</mat-card-title>
            <mat-card-subtitle>{{ recipe.isLocal ? statusLabel(recipe.status) : 'Übernommenes Rezept' }}</mat-card-subtitle>
          </mat-card-header><mat-card-actions align="end">
            @if (recipe.isLocal) {
              <button matButton type="button" (click)="open(recipe)"><scp-action-icon name="edit"/>Öffnen</button>
            }
          </mat-card-actions></mat-card>
        } @empty { <p class="empty">Noch keine Lagerrezepte vorhanden.</p> }
      </div>
    }
  `,
  styles: `
    :host { display: grid; gap: 1rem; }
    h3, h4, p { margin: 0; } .eyebrow { color: #557064; font-size: .75rem; font-weight: 700; text-transform: uppercase; }
    .recipe-heading, .section-title, .recipe-actions { display: flex; align-items: center; justify-content: space-between; gap: 1rem; }
    .recipe-message { padding: .75rem 1rem; border-radius: .65rem; border: 1px solid; }
    .recipe-message.error { color: #8b2525; background: #fff1f0; border-color: #efb8b8; }
    .recipe-message.notice { color: #205b3b; background: #edf8f1; border-color: #aed5bd; }
    .recipe-loading { display: flex; gap: .75rem; align-items: center; min-height: 4rem; }
    .recipe-form { display: grid; gap: 1rem; }
    .recipe-section { display: grid; gap: 1rem; padding: 1rem; border: 1px solid #d9e2dc; border-radius: .8rem; background: #fff; }
    .section-title p, .empty, small { color: #657269; font-size: .84rem; }
    .field-grid { display: grid; grid-template-columns: minmax(0, 2fr) minmax(10rem, 1fr); gap: .8rem; }
    .full { width: 100%; } .position-list, .ingredient-picker { display: grid; gap: .65rem; }
    .position-card { display: grid; grid-template-columns: minmax(12rem, 2fr) minmax(8rem, 1fr) minmax(10rem, 1fr) auto; gap: .7rem; align-items: center; padding: .7rem; border-radius: .65rem; background: #f5f8f6; }
    .position-name { display: grid; gap: .15rem; }
    .candidate-list { display: flex; flex-wrap: wrap; gap: .5rem; }
    .candidate-list button span { display: grid; text-align: left; }
    .recipe-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(15rem, 1fr)); gap: .8rem; }
    @media (max-width: 720px) { .field-grid, .position-card { grid-template-columns: 1fr; } }
  `
})
export class RecipeEditorComponent {
  private readonly api = inject(RecipeEditorApiService);
  readonly campId = input.required<string>();
  readonly disabled = input(false);
  readonly recipes = signal<RecipeCatalogEntry[]>([]);
  readonly ingredients = signal<IngredientCatalogEntry[]>([]);
  readonly selected = signal<RecipeEditorDraft | null>(null);
  readonly ingredientSearch = signal('');
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  private snapshot = '';

  readonly ingredientCandidates = computed(() => {
    const search = this.ingredientSearch().trim().toLocaleLowerCase('de');
    const used = new Set(this.selected()?.content.ingredientPositions.map(value => value.ingredientRevisionId));
    const matches = this.ingredients().filter(value => value.revisionId && !used.has(value.revisionId) &&
      value.name.toLocaleLowerCase('de').includes(search));
    const camp = matches.filter(value => value.scope === 'Camp');
    if (camp.length) return camp;
    const tenant = matches.filter(value => value.scope === 'Tenant');
    return tenant.length ? tenant : matches.filter(value => value.scope === 'Central');
  });

  constructor() { effect(() => { const campId = this.campId(); if (campId) this.load(); }); }

  load() {
    this.loading.set(true); this.error.set('');
    this.api.list(this.campId()).subscribe({ next: recipes => {
      this.recipes.set(recipes); this.api.ingredients(this.campId()).subscribe({
        next: ingredients => { this.ingredients.set(ingredients); this.loading.set(false); },
        error: () => { this.loading.set(false); this.error.set('Die Zutaten konnten nicht geladen werden.'); }
      });
    }, error: () => { this.loading.set(false); this.error.set('Die Rezepte konnten nicht geladen werden.'); } });
  }

  createDraft() {
    const content: RecipeEditorContent = { name: this.nextDraftName(), description: null, source: null,
      internalNotes: null, recipeType: 0, referenceServings: 10, referenceQuantity: null,
      referenceUnitId: null, defaultAgeGroupScalingApplies: true, authoringStage: null,
      tags: [], groups: [], ingredientPositions: [], subrecipePositions: [] };
    this.saving.set(true); this.error.set('');
    this.api.create(this.campId(), content).subscribe({ next: draft => {
      this.saving.set(false); this.setDraft(draft); this.loadCatalogOnly();
    }, error: (response: HttpErrorResponse) => { this.saving.set(false);
      this.error.set(response.status === 403 ? 'Du darfst keine Lagerrezepte bearbeiten.' :
        'Das Rezept konnte nicht angelegt werden.'); } });
  }

  open(recipe: RecipeCatalogEntry) {
    this.loading.set(true); this.error.set(''); this.notice.set('');
    this.api.get(this.campId(), recipe.recipeId).subscribe({ next: draft => {
      this.loading.set(false); this.setDraft(draft);
    }, error: () => { this.loading.set(false); this.error.set('Das Rezept konnte nicht geladen werden.'); } });
  }

  closeEditor() { this.selected.set(null); this.ingredientSearch.set(''); this.notice.set(''); this.load(); }

  ingredient(position: RecipeEditorIngredientPosition) {
    return this.ingredients().find(value => value.revisionId === position.ingredientRevisionId);
  }

  addIngredient(ingredient: IngredientCatalogEntry) {
    const draft = this.selected();
    if (!draft || !ingredient.revisionId || !ingredient.units.length) return;
    draft.content.ingredientPositions.push({ id: crypto.randomUUID(), groupId: null,
      ingredientRevisionId: ingredient.revisionId, quantity: 1, unitId: ingredient.units[0].unitId,
      sortOrder: draft.content.ingredientPositions.length, scalingMode: 0, ageGroupScaling: 0,
      stepSize: null, quantityPerStep: null, replacements: [] });
    this.selected.set({ ...draft, content: { ...draft.content,
      ingredientPositions: [...draft.content.ingredientPositions] } });
    this.ingredientSearch.set('');
  }

  removePosition(index: number) {
    const draft = this.selected(); if (!draft) return;
    draft.content.ingredientPositions.splice(index, 1);
    draft.content.ingredientPositions.forEach((value, sortOrder) => value.sortOrder = sortOrder);
    this.selected.set({ ...draft, content: { ...draft.content,
      ingredientPositions: [...draft.content.ingredientPositions] } });
  }

  changed() { return !!this.selected() && JSON.stringify(this.selected()!.content) !== this.snapshot; }

  save() {
    const draft = this.selected(); if (!draft || !this.changed() || this.saving()) return;
    this.saving.set(true); this.error.set(''); this.notice.set('');
    this.api.save(this.campId(), draft).subscribe({ next: saved => {
      this.saving.set(false); this.setDraft(saved); this.notice.set('Das Rezept wurde gespeichert.');
      this.loadCatalogOnly();
    }, error: (response: HttpErrorResponse) => { this.saving.set(false);
      this.error.set(response.status === 409 ?
        'Das Rezept wurde zwischenzeitlich geändert. Bitte öffne es erneut.' : 'Das Rezept konnte nicht gespeichert werden.');
    } });
  }

  scopeLabel(scope: IngredientCatalogEntry['scope'] | undefined) {
    return scope === 'Camp' ? 'Lager' : scope === 'Tenant' ? 'Organisation' : scope === 'Central' ? 'Zentral' : '';
  }
  statusLabel(status: number) { return status === 0 ? 'Entwurf' : status === 1 ? 'Aktiv' : 'Archiviert'; }

  private setDraft(draft: RecipeEditorDraft) {
    this.selected.set(draft); this.snapshot = JSON.stringify(draft.content);
  }
  private nextDraftName() {
    const baseName = 'Neues Rezept';
    const existingNames = new Set(this.recipes().map(recipe => recipe.name.trim().toLocaleLowerCase('de')));
    if (!existingNames.has(baseName.toLocaleLowerCase('de'))) return baseName;
    let suffix = 2;
    while (existingNames.has(`${baseName} ${suffix}`.toLocaleLowerCase('de'))) suffix++;
    return `${baseName} ${suffix}`;
  }
  private loadCatalogOnly() { this.api.list(this.campId()).subscribe({ next: values => this.recipes.set(values) }); }
}
