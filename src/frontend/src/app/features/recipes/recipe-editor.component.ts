import { HttpErrorResponse } from '@angular/common/http';
import { DecimalPipe } from '@angular/common';
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
  RecipeEditorDraft, RecipeEditorGroup, RecipeEditorIngredientPosition,
  RecipeNutritionCalculation, RecipeNutritionValues, RecipePublicationResponse,
  RecipeValidationIssue, RecipeEditorIngredientUpdate } from './recipe-editor-api.service';

interface IngredientSection {
  id: string | null;
  group: RecipeEditorGroup | null;
  positions: RecipeEditorIngredientPosition[];
}

@Component({
  selector: 'scp-recipe-editor',
  standalone: true,
  imports: [DecimalPipe, FormsModule, MatButtonModule, MatCardModule, MatCheckboxModule, MatFormFieldModule,
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
          <div class="section-title">
            <div><h4>Zutaten</h4><p>Mengen gelten für die angegebenen Standardportionen.</p></div>
            @if (!disabled()) {
              <button matButton type="button" (click)="addGroup()"><scp-action-icon name="add"/>Gruppe</button>
            }
          </div>
          <div class="conflict-legend" aria-label="Legende für Zutatenkonflikte">
            <span class="preventable">Gelb: durch eine Variante vermeidbar</span>
            <span class="unavoidable">Rot: durch keine Variante vermeidbar</span>
          </div>
          <div class="group-list">
            @for (section of ingredientSections(); track section.id ?? 'ungrouped') {
              <article class="ingredient-group" [class.ungrouped]="!section.group">
                <div class="group-heading">
                  @if (section.group; as group) {
                    <mat-form-field appearance="outline" subscriptSizing="dynamic" class="group-name">
                      <mat-label>Gruppenname</mat-label>
                      <input matInput [name]="'group-' + group.id" [(ngModel)]="group.name"
                        maxlength="200" [disabled]="disabled()">
                    </mat-form-field>
                    <div class="icon-actions">
                      <button matIconButton type="button" aria-label="Gruppe nach oben verschieben"
                        title="Gruppe nach oben verschieben" (click)="moveGroup(group, -1)"
                        [disabled]="disabled() || !canMoveGroup(group, -1)"><scp-action-icon name="up"/></button>
                      <button matIconButton type="button" aria-label="Gruppe nach unten verschieben"
                        title="Gruppe nach unten verschieben" (click)="moveGroup(group, 1)"
                        [disabled]="disabled() || !canMoveGroup(group, 1)"><scp-action-icon name="down"/></button>
                      <button matIconButton type="button" aria-label="Gruppe löschen"
                        [title]="groupIsEmpty(group.id) ? 'Gruppe löschen' : 'Positionen zuerst aus der Gruppe verschieben'"
                        (click)="removeGroup(group)" [disabled]="disabled() || !groupIsEmpty(group.id)">
                        <scp-action-icon name="remove"/></button>
                    </div>
                  } @else {
                    <div><strong>Ohne Gruppe</strong><small>Zutaten, die keiner Rezeptgruppe zugeordnet sind.</small></div>
                  }
                </div>

                <div class="position-list">
                  @for (position of section.positions; track position.id; let first = $first; let last = $last) {
                    <div class="position-card">
                      <div class="position-name"><strong>{{ ingredient(position)?.name ?? 'Unbekannte Zutat' }}</strong>
                        <small>{{ scopeLabel(ingredient(position)?.scope) }}
                          @if (ingredientReference(position); as reference) { · Revision {{ reference.revisionNumber }} }
                        </small>
                        @if ((ingredient(position)?.conflicts ?? []).length) {
                          <div class="conflict-list" aria-label="Konflikte der Zutat">
                            @for (conflict of ingredient(position)!.conflicts; track conflict.type + '-' + conflict.id) {
                              <span
                                [class.preventable]="conflictVariantNames(position, conflict.id)?.length"
                                [class.unavoidable]="conflictVariantNames(position, conflict.id)?.length === 0"
                                [class.unclassified]="conflictVariantNames(position, conflict.id) === null"
                                [title]="conflictTitle(position, conflict.id, conflict.name)">{{ conflict.name }}</span>
                            }
                          </div>
                        }
                        @if (ingredientReference(position)?.availableUpdate; as update) {
                          <button matButton type="button" class="ingredient-update"
                            (click)="adoptIngredientUpdate(position, update)" [disabled]="disabled()">
                            Auf Revision {{ update.revisionNumber }} aktualisieren
                          </button>
                        }
                      </div>
                      <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Gruppe</mat-label>
                        <mat-select [name]="'position-group-' + position.id" [ngModel]="position.groupId"
                          (ngModelChange)="movePositionToGroup(position, $event)" [disabled]="disabled()">
                          <mat-option [value]="null">Ohne Gruppe</mat-option>
                          @for (group of orderedGroups(); track group.id) {
                            <mat-option [value]="group.id">{{ group.name || 'Unbenannte Gruppe' }}</mat-option>
                          }
                        </mat-select>
                      </mat-form-field>
                      <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Menge</mat-label>
                        <input matInput type="number" min="0.000001" step="any" [name]="'quantity-' + position.id"
                          [(ngModel)]="position.quantity" [disabled]="disabled()">
                      </mat-form-field>
                      <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Einheit</mat-label>
                        <mat-select [name]="'unit-' + position.id" [(ngModel)]="position.unitId" [disabled]="disabled()">
                          @for (unit of ingredient(position)?.units ?? []; track unit.unitId) {
                            <mat-option [value]="unit.unitId">{{ unit.name }} ({{ unit.symbol }})</mat-option>
                          }
                        </mat-select>
                      </mat-form-field>
                      <div class="icon-actions">
                        <button matIconButton type="button" aria-label="Zutat nach oben verschieben"
                          title="Zutat nach oben verschieben" (click)="movePosition(position, -1)"
                          [disabled]="disabled() || first"><scp-action-icon name="up"/></button>
                        <button matIconButton type="button" aria-label="Zutat nach unten verschieben"
                          title="Zutat nach unten verschieben" (click)="movePosition(position, 1)"
                          [disabled]="disabled() || last"><scp-action-icon name="down"/></button>
                        <button matIconButton type="button" aria-label="Zutat entfernen" title="Zutat entfernen"
                          (click)="removePosition(position.id)" [disabled]="disabled()"><scp-action-icon name="remove"/></button>
                      </div>
                    </div>
                  } @empty { <p class="empty">Noch keine Zutaten in dieser Gruppe.</p> }
                </div>
              </article>
            }
          </div>

          @if (!disabled()) {
            <div class="ingredient-picker">
              <div class="picker-fields">
                <mat-form-field appearance="outline"><mat-label>Zutat suchen</mat-label>
                  <input matInput name="recipeIngredientSearch" [ngModel]="ingredientSearch()"
                    (ngModelChange)="ingredientSearch.set($event)" autocomplete="off">
                </mat-form-field>
                <mat-form-field appearance="outline"><mat-label>In Gruppe einfügen</mat-label>
                  <mat-select name="newIngredientGroup" [ngModel]="targetGroupId()"
                    (ngModelChange)="targetGroupId.set($event)">
                    <mat-option [value]="null">Ohne Gruppe</mat-option>
                    @for (group of orderedGroups(); track group.id) {
                      <mat-option [value]="group.id">{{ group.name || 'Unbenannte Gruppe' }}</mat-option>
                    }
                  </mat-select>
                </mat-form-field>
              </div>
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

        <section class="recipe-section nutrition-section">
          <div class="section-title">
            <div><h4>Nährwertschätzung</h4><p>Berechnet aus dem zuletzt gespeicherten Rezeptstand.</p></div>
            <button matButton type="button" (click)="loadNutrition()"
              [disabled]="changed() || nutritionLoading()">
              Neu berechnen
            </button>
          </div>
          @if (changed()) {
            <p class="nutrition-hint">Speichere die Änderungen, um die Nährwerte neu zu berechnen.</p>
          } @else if (nutritionLoading()) {
            <div class="recipe-loading"><mat-spinner diameter="24"/><span>Nährwerte werden berechnet …</span></div>
          } @else if (nutrition(); as calculation) {
            @if (calculation.isComplete && calculation.perStandardPortion && calculation.total) {
              <p class="nutrition-state complete">Vollständige Schätzung auf Basis geprüfter Zutatenprofile.</p>
              <h5>Pro Standardportion</h5>
              <div class="nutrition-grid">
                <span><strong>{{ calculation.perStandardPortion.energyKilojoules | number:'1.0-1' }} kJ</strong><small>{{ calculation.perStandardPortion.energyKilocalories | number:'1.0-1' }} kcal</small></span>
                <span><strong>{{ calculation.perStandardPortion.fatGrams | number:'1.0-2' }} g</strong><small>Fett</small></span>
                <span><strong>{{ calculation.perStandardPortion.saturatedFatGrams | number:'1.0-2' }} g</strong><small>davon gesättigt</small></span>
                <span><strong>{{ calculation.perStandardPortion.carbohydrateGrams | number:'1.0-2' }} g</strong><small>Kohlenhydrate</small></span>
                <span><strong>{{ calculation.perStandardPortion.sugarsGrams | number:'1.0-2' }} g</strong><small>davon Zucker</small></span>
                <span><strong>{{ calculation.perStandardPortion.proteinGrams | number:'1.0-2' }} g</strong><small>Eiweiß</small></span>
                <span><strong>{{ calculation.perStandardPortion.saltGrams | number:'1.0-3' }} g</strong><small>Salz</small></span>
                <span><strong>{{ optionalGrams(calculation.perStandardPortion.fiberGrams) }}</strong><small>Ballaststoffe</small></span>
              </div>
              <details class="nutrition-total">
                <summary>Gesamtwerte des Rezepts</summary>
                <div class="nutrition-grid">
                  <span><strong>{{ calculation.total.energyKilojoules | number:'1.0-1' }} kJ</strong><small>{{ calculation.total.energyKilocalories | number:'1.0-1' }} kcal</small></span>
                  <span><strong>{{ calculation.total.fatGrams | number:'1.0-2' }} g</strong><small>Fett</small></span>
                  <span><strong>{{ calculation.total.saturatedFatGrams | number:'1.0-2' }} g</strong><small>davon gesättigt</small></span>
                  <span><strong>{{ calculation.total.carbohydrateGrams | number:'1.0-2' }} g</strong><small>Kohlenhydrate</small></span>
                  <span><strong>{{ calculation.total.sugarsGrams | number:'1.0-2' }} g</strong><small>davon Zucker</small></span>
                  <span><strong>{{ calculation.total.proteinGrams | number:'1.0-2' }} g</strong><small>Eiweiß</small></span>
                  <span><strong>{{ calculation.total.saltGrams | number:'1.0-3' }} g</strong><small>Salz</small></span>
                  <span><strong>{{ optionalGrams(calculation.total.fiberGrams) }}</strong><small>Ballaststoffe</small></span>
                </div>
              </details>
            } @else {
              <p class="nutrition-state incomplete">Die Nährwertschätzung ist noch unvollständig.</p>
              <ul class="nutrition-missing">
                @for (missing of calculation.missingContributions; track missing.positionId + missing.ingredientRevisionId) {
                  <li><strong>{{ missing.ingredientName }}</strong>: {{ nutritionReason(missing.reason) }}</li>
                }
              </ul>
            }
          } @else {
            <p class="nutrition-hint">{{ nutritionMessage() || 'Für dieses Rezept sind noch keine Nährwerte berechenbar.' }}</p>
          }
        </section>

        <div class="recipe-actions">
          @if (publicationIssues().length) {
            <div class="publication-validation" role="alert">
              <strong>{{ publicationHasErrors() ? 'Das Rezept kann noch nicht veröffentlicht werden.' :
                'Bitte prüfe diese Hinweise vor der Veröffentlichung.' }}</strong>
              <ul>
                @for (issue of publicationIssues(); track issue.code + ($index)) {
                  <li>{{ validationIssueText(issue) }}</li>
                }
              </ul>
              @if (!publicationHasErrors()) {
                <button matButton="filled" type="button" (click)="publish(true)" [disabled]="publishing()">
                  Hinweise bestätigen und veröffentlichen
                </button>
              }
            </div>
          }
          <button matButton="filled" type="submit" [disabled]="disabled() || saving() || !changed()">
            <scp-action-icon name="save"/>Speichern</button>
          <button matButton type="button" (click)="publish(false)"
            [disabled]="disabled() || saving() || publishing() || changed() || draft.status === 2">
            {{ draft.status === 1 ? 'Neue Revision veröffentlichen' : 'Veröffentlichen' }}
          </button>
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
    .full { width: 100%; } .group-list, .position-list, .ingredient-picker { display: grid; gap: .65rem; }
    .ingredient-group { display: grid; gap: .7rem; padding: .8rem; border: 1px solid #dce5df; border-radius: .7rem; background: #fbfcfb; }
    .ingredient-group.ungrouped { border-style: dashed; }
    .group-heading, .icon-actions { display: flex; align-items: center; justify-content: space-between; gap: .35rem; }
    .group-heading > div:first-child { display: grid; gap: .15rem; }
    .group-name { width: min(24rem, 100%); }
    .position-card { display: grid; grid-template-columns: minmax(10rem, 1.5fr) minmax(9rem, 1fr) minmax(7rem, .7fr) minmax(9rem, 1fr) auto; gap: .7rem; align-items: center; padding: .7rem; border-radius: .65rem; background: #f1f6f3; }
    .position-name { display: grid; gap: .25rem; }
    .conflict-list, .conflict-legend { display: flex; flex-wrap: wrap; gap: .3rem; }
    .conflict-list span, .conflict-legend span { padding: .15rem .45rem; border-radius: 999px; font-size: .76rem; }
    .conflict-list .preventable, .conflict-legend .preventable { color: #72500b; background: #fff0b8; }
    .conflict-list .unavoidable, .conflict-legend .unavoidable { color: #8b2525; background: #ffe2df; }
    .conflict-list .unclassified { color: #4f5d55; background: #e8eeea; }
    .ingredient-update { justify-self: start; margin-left: -.75rem; }
    .picker-fields { display: grid; grid-template-columns: 2fr 1fr; gap: .7rem; }
    .candidate-list { display: flex; flex-wrap: wrap; gap: .5rem; }
    .candidate-list button span { display: grid; text-align: left; }
    .nutrition-section h5 { margin: 0; }
    .nutrition-state { padding: .65rem .8rem; border-radius: .6rem; }
    .nutrition-state.complete { color: #205b3b; background: #edf8f1; }
    .nutrition-state.incomplete { color: #7b5414; background: #fff7e8; }
    .nutrition-hint, .nutrition-missing { color: #657269; }
    .publication-validation { flex: 1 1 100%; padding: .8rem 1rem; border: 1px solid #e2bc70; border-radius: .65rem; background: #fff8e8; }
    .publication-validation ul { margin: .5rem 0; padding-left: 1.25rem; }
    .recipe-actions { flex-wrap: wrap; }
    .nutrition-grid { display: grid; grid-template-columns: repeat(4, minmax(8rem, 1fr)); gap: .6rem; }
    .nutrition-grid span { display: grid; gap: .15rem; padding: .65rem; border-radius: .6rem; background: #f1f6f3; }
    .nutrition-grid small { display: block; }
    .nutrition-total { display: grid; gap: .7rem; }
    .nutrition-total summary { cursor: pointer; color: #315d48; font-weight: 600; }
    .nutrition-missing { margin: 0; padding-left: 1.25rem; }
    .recipe-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(15rem, 1fr)); gap: .8rem; }
    @media (max-width: 900px) { .position-card, .nutrition-grid { grid-template-columns: 1fr 1fr; } }
    @media (max-width: 720px) { .field-grid, .picker-fields, .position-card, .nutrition-grid { grid-template-columns: 1fr; } }
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
  readonly targetGroupId = signal<string | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly nutrition = signal<RecipeNutritionCalculation | null>(null);
  readonly nutritionLoading = signal(false);
  readonly nutritionMessage = signal('');
  readonly publishing = signal(false);
  readonly publicationIssues = signal<RecipeValidationIssue[]>([]);
  readonly publicationHasErrors = computed(() => this.publicationIssues().some(issue => issue.severity === 0));
  private snapshot = '';

  readonly ingredientCandidates = computed(() => {
    const search = this.ingredientSearch().trim().toLocaleLowerCase('de');
    const used = new Set(this.selected()?.content.ingredientPositions
      .filter(value => value.groupId === this.targetGroupId()).map(value => value.ingredientRevisionId));
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

  closeEditor() {
    this.selected.set(null); this.ingredientSearch.set(''); this.targetGroupId.set(null); this.notice.set(''); this.load();
  }

  ingredient(position: RecipeEditorIngredientPosition) {
    return this.ingredientReference(position) ??
      this.ingredients().find(value => value.revisionId === position.ingredientRevisionId);
  }

  ingredientReference(position: RecipeEditorIngredientPosition) {
    return this.selected()?.ingredientReferences.find(value => value.revisionId === position.ingredientRevisionId);
  }

  adoptIngredientUpdate(position: RecipeEditorIngredientPosition, update: RecipeEditorIngredientUpdate) {
    const draft = this.selected();
    if (!draft) return;
    const duplicate = draft.content.ingredientPositions.some(value => value.id !== position.id &&
      value.groupId === position.groupId && value.ingredientRevisionId === update.revisionId);
    if (duplicate) {
      this.error.set('Die aktuelle Revision dieser Zutat ist in der Gruppe bereits vorhanden.');
      return;
    }
    position.ingredientRevisionId = update.revisionId;
    if (!update.units.some(unit => unit.unitId === position.unitId))
      position.unitId = update.units[0]?.unitId ?? null;
    this.error.set('');
    this.markContentChanged();
  }

  conflictVariantNames(position: RecipeEditorIngredientPosition, conflictId: string): string[] | null {
    const reference = this.ingredientReference(position);
    if (!reference) return null;
    return reference.conflicts.find(conflict => conflict.id === conflictId)?.preventableByVariants ?? [];
  }

  conflictTitle(position: RecipeEditorIngredientPosition, conflictId: string, conflictName: string) {
    const variants = this.conflictVariantNames(position, conflictId);
    if (variants === null) return `${conflictName}: Variantenprüfung nach dem Speichern verfügbar.`;
    return variants.length
      ? `${conflictName}: vermeidbar durch ${variants.join(', ')}.`
      : `${conflictName}: durch keine aktive Variante vermeidbar.`;
  }

  orderedGroups() {
    return [...(this.selected()?.content.groups ?? [])].sort((first, second) => first.sortOrder - second.sortOrder);
  }

  ingredientSections(): IngredientSection[] {
    const draft = this.selected();
    if (!draft) return [];
    return [
      { id: null, group: null, positions: this.positionsForGroup(null) },
      ...this.orderedGroups().map(group => ({ id: group.id, group, positions: this.positionsForGroup(group.id) })),
    ];
  }

  addGroup() {
    const draft = this.selected(); if (!draft) return;
    const group: RecipeEditorGroup = {
      id: crypto.randomUUID(), name: this.nextGroupName(), sortOrder: draft.content.groups.length,
    };
    draft.content.groups.push(group);
    this.targetGroupId.set(group.id);
    this.markContentChanged();
  }

  removeGroup(group: RecipeEditorGroup) {
    const draft = this.selected();
    if (!draft || !this.groupIsEmpty(group.id)) return;
    draft.content.groups = draft.content.groups.filter(value => value.id !== group.id);
    this.normalizeGroups();
    if (this.targetGroupId() === group.id) this.targetGroupId.set(null);
    this.markContentChanged();
  }

  groupIsEmpty(groupId: string) {
    const content = this.selected()?.content;
    return !!content && !content.ingredientPositions.some(value => value.groupId === groupId) &&
      !content.subrecipePositions.some(value => value.groupId === groupId);
  }

  canMoveGroup(group: RecipeEditorGroup, direction: -1 | 1) {
    const groups = this.orderedGroups();
    const index = groups.findIndex(value => value.id === group.id);
    return index >= 0 && index + direction >= 0 && index + direction < groups.length;
  }

  moveGroup(group: RecipeEditorGroup, direction: -1 | 1) {
    const groups = this.orderedGroups();
    const index = groups.findIndex(value => value.id === group.id);
    const targetIndex = index + direction;
    if (index < 0 || targetIndex < 0 || targetIndex >= groups.length) return;
    [groups[index], groups[targetIndex]] = [groups[targetIndex], groups[index]];
    groups.forEach((value, sortOrder) => value.sortOrder = sortOrder);
    this.markContentChanged();
  }

  addIngredient(ingredient: IngredientCatalogEntry) {
    const draft = this.selected();
    if (!draft || !ingredient.revisionId || !ingredient.units.length) return;
    const groupId = this.targetGroupId();
    const sortOrder = this.positionsForGroup(groupId).length;
    draft.content.ingredientPositions.push({ id: crypto.randomUUID(), groupId,
      ingredientRevisionId: ingredient.revisionId, quantity: 1, unitId: ingredient.units[0].unitId,
      sortOrder, scalingMode: 0, ageGroupScaling: 0,
      stepSize: null, quantityPerStep: null, replacements: [] });
    this.markContentChanged();
    this.ingredientSearch.set('');
  }

  removePosition(positionId: string) {
    const draft = this.selected(); if (!draft) return;
    const position = draft.content.ingredientPositions.find(value => value.id === positionId);
    if (!position) return;
    draft.content.ingredientPositions = draft.content.ingredientPositions.filter(value => value.id !== positionId);
    this.normalizePositions(position.groupId);
    this.markContentChanged();
  }

  movePosition(position: RecipeEditorIngredientPosition, direction: -1 | 1) {
    const positions = this.positionsForGroup(position.groupId);
    const index = positions.findIndex(value => value.id === position.id);
    const targetIndex = index + direction;
    if (index < 0 || targetIndex < 0 || targetIndex >= positions.length) return;
    [positions[index], positions[targetIndex]] = [positions[targetIndex], positions[index]];
    positions.forEach((value, sortOrder) => value.sortOrder = sortOrder);
    this.markContentChanged();
  }

  movePositionToGroup(position: RecipeEditorIngredientPosition, groupId: string | null) {
    if (position.groupId === groupId) return;
    const draft = this.selected(); if (!draft) return;
    const duplicate = draft.content.ingredientPositions.some(value => value.id !== position.id &&
      value.groupId === groupId && value.ingredientRevisionId === position.ingredientRevisionId);
    if (duplicate) {
      this.error.set('Diese Zutat ist in der gewählten Gruppe bereits vorhanden.');
      return;
    }
    const previousGroupId = position.groupId;
    position.groupId = groupId;
    position.sortOrder = this.positionsForGroup(groupId).filter(value => value.id !== position.id).length;
    this.normalizePositions(previousGroupId);
    this.normalizePositions(groupId);
    this.error.set('');
    this.markContentChanged();
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

  publish(acknowledgeWarnings: boolean) {
    const draft = this.selected();
    if (!draft || this.changed() || this.publishing()) return;
    this.publishing.set(true); this.error.set(''); this.notice.set('');
    this.api.publish(this.campId(), draft, acknowledgeWarnings).subscribe({
      next: result => {
        this.publicationIssues.set([]);
        this.api.get(this.campId(), draft.id).subscribe({
          next: current => {
            this.publishing.set(false); this.setDraft(current);
            this.notice.set(`Das Rezept wurde als Revision ${result.revisionNumber} veröffentlicht.`);
            this.loadCatalogOnly();
          },
          error: () => {
            this.publishing.set(false);
            this.error.set('Das veröffentlichte Rezept konnte nicht neu geladen werden.');
          }
        });
      },
      error: (response: HttpErrorResponse) => {
        this.publishing.set(false);
        const result = response.error as RecipePublicationResponse | undefined;
        if (result?.status === 4 || result?.status === 5) {
          this.publicationIssues.set(result.validation?.issues ?? []);
          return;
        }
        this.publicationIssues.set([]);
        this.error.set(response.status === 403 ? 'Du darfst Lagerrezepte nicht veröffentlichen.' :
          result?.status === 3 ? 'Das Rezept wurde zwischenzeitlich geändert. Bitte öffne es erneut.' :
          result?.status === 6 ? 'Ein archiviertes Rezept kann nicht veröffentlicht werden.' :
          'Das Rezept konnte nicht veröffentlicht werden.');
      }
    });
  }

  loadNutrition() {
    const draft = this.selected();
    if (!draft || this.changed() || this.nutritionLoading()) return;
    this.nutritionLoading.set(true); this.nutritionMessage.set('');
    this.api.nutrition(this.campId(), draft.id).subscribe({
      next: value => {
        this.nutrition.set(value); this.nutritionLoading.set(false);
      },
      error: (response: HttpErrorResponse) => {
        this.nutrition.set(null); this.nutritionLoading.set(false);
        this.nutritionMessage.set(response.status === 422
          ? 'Ergänze zuerst Standardportionen und mindestens eine vollständig konfigurierte Zutat.'
          : 'Die Nährwerte konnten nicht berechnet werden.');
      }
    });
  }

  optionalGrams(value: number | null) {
    return value === null ? 'Unbekannt' : `${this.formatNumber(value, 2)} g`;
  }

  nutritionReason(reason: number) {
    return reason === 0 ? 'kein Nährwertprofil hinterlegt' :
      reason === 1 ? 'Nährwertprofil noch nicht vollständig geprüft' :
      'Bezugsmenge oder Umrechnung ist ungültig';
  }

  scopeLabel(scope: IngredientCatalogEntry['scope'] | number | undefined) {
    return scope === 'Camp' || scope === 2 ? 'Lager' : scope === 'Tenant' || scope === 1 ? 'Organisation' :
      scope === 'Central' || scope === 0 ? 'Zentral' : '';
  }
  statusLabel(status: number) { return status === 0 ? 'Entwurf' : status === 1 ? 'Aktiv' : 'Archiviert'; }

  validationIssueText(issue: RecipeValidationIssue) {
    const conflictName = issue.context?.['conflictName'];
    if (conflictName) {
      if (issue.code === 'recipe.conflict.unresolved')
        return `Für „${conflictName}“ ist noch kein Ersatz definiert.`;
      if (issue.code === 'recipe.replacement.conflict.remains')
        return `Die Ersatzzutat enthält weiterhin „${conflictName}“ bzw. kann es enthalten.`;
      if (issue.code === 'recipe.replacement.conflict.created')
        return `Die Ersatzzutat bringt zusätzlich den Konflikt „${conflictName}“ mit.`;
    }
    const messages: Record<string, string> = {
      'recipe.name.missing': 'Ein Rezeptname ist erforderlich.',
      'recipe.reference.servings.invalid': 'Die Anzahl der Standardportionen muss größer als null sein.',
      'recipe.positions.empty': 'Das Rezept benötigt mindestens eine Zutat.',
      'recipe.group.name.missing': 'Jede Gruppe benötigt einen Namen.',
      'recipe.group.empty': 'Leere Gruppen müssen entfernt oder mit Zutaten befüllt werden.',
      'recipe.position.sort-order.invalid': 'Die Reihenfolge der Zutaten ist ungültig.',
      'recipe.ingredient.missing': 'Eine verwendete Zutatenrevision ist nicht mehr verfügbar.',
      'recipe.ingredient.scope.forbidden': 'Eine Zutat ist für dieses Lagerrezept nicht zulässig.',
      'recipe.ingredient.quantity.invalid': 'Jede Zutat benötigt eine Menge größer als null.',
      'recipe.ingredient.unit.invalid': 'Für jede Zutat muss eine gültige Einheit gewählt werden.',
      'recipe.ingredient.duplicate': 'Eine Zutat darf innerhalb derselben Gruppe nur einmal vorkommen.',
      'recipe.description.missing': 'Es fehlt eine Beschreibung oder Zubereitungsanleitung.',
      'recipe.source.missing': 'Es fehlt eine Quellenangabe.',
      'recipe.conflict.unresolved': 'Für einen bekannten Konflikt ist noch kein Ersatz definiert.',
    };
    return messages[issue.code] ?? `Prüfhinweis: ${issue.code}`;
  }

  private setDraft(draft: RecipeEditorDraft) {
    this.selected.set(draft); this.targetGroupId.set(null); this.snapshot = JSON.stringify(draft.content);
    this.nutrition.set(null); this.nutritionMessage.set(''); this.publicationIssues.set([]); this.loadNutrition();
  }
  private positionsForGroup(groupId: string | null) {
    return [...(this.selected()?.content.ingredientPositions ?? [])]
      .filter(value => value.groupId === groupId)
      .sort((first, second) => first.sortOrder - second.sortOrder);
  }
  private normalizePositions(groupId: string | null) {
    this.positionsForGroup(groupId).forEach((value, sortOrder) => value.sortOrder = sortOrder);
  }
  private normalizeGroups() {
    this.orderedGroups().forEach((value, sortOrder) => value.sortOrder = sortOrder);
  }
  private markContentChanged() {
    const draft = this.selected(); if (!draft) return;
    this.selected.set({ ...draft, content: { ...draft.content,
      groups: [...draft.content.groups], ingredientPositions: [...draft.content.ingredientPositions] } });
  }
  private nextDraftName() {
    const baseName = 'Neues Rezept';
    const existingNames = new Set(this.recipes().map(recipe => recipe.name.trim().toLocaleLowerCase('de')));
    if (!existingNames.has(baseName.toLocaleLowerCase('de'))) return baseName;
    let suffix = 2;
    while (existingNames.has(`${baseName} ${suffix}`.toLocaleLowerCase('de'))) suffix++;
    return `${baseName} ${suffix}`;
  }
  private nextGroupName() {
    const existingNames = new Set((this.selected()?.content.groups ?? [])
      .map(group => (group.name ?? '').trim().toLocaleLowerCase('de')));
    let suffix = 1;
    while (existingNames.has(`Gruppe ${suffix}`.toLocaleLowerCase('de'))) suffix++;
    return `Gruppe ${suffix}`;
  }
  private loadCatalogOnly() { this.api.list(this.campId()).subscribe({ next: values => this.recipes.set(values) }); }
  private formatNumber(value: number, maximumFractionDigits: number) {
    return new Intl.NumberFormat('de-DE', { maximumFractionDigits }).format(value);
  }
}
