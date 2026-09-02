import { HttpErrorResponse } from '@angular/common/http';
import { Component, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { forkJoin } from 'rxjs';
import { ActionIconComponent } from '../../shared/action-icon.component';
import {
  IngredientEditorReferenceData,
  IngredientAllergenReference,
  IngredientConversionPrecision,
  IngredientPropertyReviewState,
  IngredientPropertySource,
  IngredientPropertyState,
  IngredientRevisionApiService,
  IngredientRevisionDetails,
  IngredientRevisionPropertyItem,
  IngredientRevisionUnitConversionItem,
  IngredientRevisionState,
  IngredientRevisionSummary
} from './ingredient-revision-api.service';

@Component({
  selector: 'scp-ingredient-revision-editor',
  standalone: true,
  imports: [FormsModule, MatButtonModule, MatCardModule, MatCheckboxModule, MatFormFieldModule,
    MatInputModule, MatProgressSpinnerModule, MatSelectModule, ActionIconComponent],
  template: `
    <div class="revision-editor-heading">
      <div><h4>Lagerzutaten verwalten</h4><p>Entwürfe explizit speichern und nach der Prüfung veröffentlichen.</p></div>
      @if (!disabled() && !createOpen()) {
        <button matButton type="button" (click)="openCreate()"><scp-action-icon name="add"/>Neue Lagerzutat</button>
      }
    </div>
    @if (error()) { <p class="revision-message revision-error" role="alert">{{ error() }}</p> }
    @if (notice()) { <p class="revision-message revision-notice" role="status">{{ notice() }}</p> }
    @if (loading()) {
      <div class="revision-loading"><mat-spinner diameter="28"/><span>Zutatenentwürfe werden geladen …</span></div>
    } @else {
      @if (createOpen()) {
        <form class="revision-form" (ngSubmit)="create()">
          <h4>Neue Lagerzutat</h4>
          <mat-form-field appearance="outline"><mat-label>Name</mat-label>
            <input matInput name="createIngredientName" [(ngModel)]="createName" maxlength="200" required>
          </mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Kategorie</mat-label>
            <mat-select name="createIngredientCategory" [(ngModel)]="createCategoryId" required>
              @for (category of referenceData()?.categories ?? []; track category.id) {
                <mat-option [value]="category.id">{{ category.name }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Basiseinheit</mat-label>
            <mat-select name="createIngredientUnit" [(ngModel)]="createBaseUnitId" required>
              <mat-optgroup label="Gewicht">
                @for (unit of baseUnitsByDimension(0); track unit.id) {
                  <mat-option [value]="unit.id">{{ unit.name }} ({{ unit.symbol }})</mat-option>
                }
              </mat-optgroup>
              <mat-optgroup label="Volumen">
                @for (unit of baseUnitsByDimension(1); track unit.id) {
                  <mat-option [value]="unit.id">{{ unit.name }} ({{ unit.symbol }})</mat-option>
                }
              </mat-optgroup>
              <mat-optgroup label="Anzahl">
                @for (unit of baseUnitsByDimension(2); track unit.id) {
                  <mat-option [value]="unit.id">{{ unit.name }} ({{ unit.symbol }})</mat-option>
                }
              </mat-optgroup>
            </mat-select>
          </mat-form-field>
          <div class="revision-actions">
            <button matButton type="button" (click)="createOpen.set(false)">Abbrechen</button>
            <button matButton="filled" type="submit" [disabled]="submitting() || !canCreate()">
              <scp-action-icon name="save"/>Entwurf anlegen</button>
          </div>
        </form>
      }

      <div class="revision-list">
        @for (revision of revisions(); track revision.revisionId) {
          <button type="button" class="revision-list-item" [class.active]="selected()?.id === revision.revisionId"
            (click)="open(revision.revisionId)">
            <span><strong>{{ revision.name }}</strong><small>{{ stateLabel(revision.state) }}</small></span>
            <scp-action-icon name="edit"/>
          </button>
        } @empty { <p class="revision-empty">Noch keine revisionsfähigen Lagerzutaten vorhanden.</p> }
      </div>

      @if (selected(); as revision) {
        <form class="revision-form revision-details" (ngSubmit)="save()">
          <div class="revision-editor-heading"><div><h4>{{ revision.name }}</h4><p>Version {{ revision.rowVersion }}</p></div>
            <span class="revision-state" [class.published]="revision.state === publishedState">{{ stateLabel(revision.state) }}</span>
          </div>
          <mat-form-field appearance="outline"><mat-label>Name</mat-label>
            <input matInput name="revisionName" [(ngModel)]="revision.name" maxlength="200" required
              [disabled]="revision.state === publishedState || disabled()">
          </mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Kategorie</mat-label>
            <mat-select name="revisionCategory" [(ngModel)]="revision.categoryId"
              [disabled]="revision.state === publishedState || disabled()">
              @for (category of referenceData()?.categories ?? []; track category.id) {
                <mat-option [value]="category.id">{{ category.name }}</mat-option>
              }
            </mat-select>
          </mat-form-field>
          <mat-form-field appearance="outline"><mat-label>Basiseinheit</mat-label>
            <mat-select name="revisionUnit" [ngModel]="revision.baseUnitId"
              (ngModelChange)="setBaseUnit(revision, $event)"
              [disabled]="revision.state === publishedState || disabled()">
              <mat-optgroup label="Gewicht">
                @for (unit of baseUnitsByDimension(0); track unit.id) {
                  <mat-option [value]="unit.id">{{ unit.name }} ({{ unit.symbol }})</mat-option>
                }
              </mat-optgroup>
              <mat-optgroup label="Volumen">
                @for (unit of baseUnitsByDimension(1); track unit.id) {
                  <mat-option [value]="unit.id">{{ unit.name }} ({{ unit.symbol }})</mat-option>
                }
              </mat-optgroup>
              <mat-optgroup label="Anzahl">
                @for (unit of baseUnitsByDimension(2); track unit.id) {
                  <mat-option [value]="unit.id">{{ unit.name }} ({{ unit.symbol }})</mat-option>
                }
              </mat-optgroup>
            </mat-select>
          </mat-form-field>
          <section class="unit-conversions">
            <div class="unit-conversion-heading">
              <div>
                <h4>Weitere Einheiten</h4>
                <p>Lege Umrechnungen zu Einheiten anderer Klassen oder zu Küchenmaßen fest. Umrechnungen innerhalb der Basiseinheitenklasse erfolgen automatisch.</p>
              </div>
              @if (revision.state === draftState && !disabled()) {
                <button matButton type="button" (click)="addUnitConversion(revision)"
                  [disabled]="!availableConversionUnits(revision).length">
                  <scp-action-icon name="add"/>Einheit ergÃ¤nzen
                </button>
              }
            </div>
            @for (conversion of revision.unitConversions; track conversion.sourceUnitId; let index = $index) {
              <div class="unit-conversion-row">
                <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Einheit</mat-label>
                  <mat-select [(ngModel)]="conversion.sourceUnitId" [name]="'conversionUnit' + index"
                    [disabled]="revision.state === publishedState || disabled()">
                    @for (unit of availableConversionUnits(revision, conversion.sourceUnitId); track unit.id) {
                      <mat-option [value]="unit.id">{{ unit.name }} ({{ unit.symbol }})</mat-option>
                    }
                  </mat-select>
                </mat-form-field>
                <span class="conversion-formula">1 {{ unitSymbol(conversion.sourceUnitId) }} =</span>
                <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Menge</mat-label>
                  <input matInput type="text" inputmode="decimal"
                    [ngModel]="conversion.factorInput"
                    (ngModelChange)="setConversionFactor(conversion, $event)"
                    (blur)="normalizeConversionFactorInput(conversion)" [name]="'conversionFactor' + index"
                    [disabled]="revision.state === publishedState || disabled()" required>
                </mat-form-field>
                <span class="conversion-formula">{{ unitSymbol(revision.baseUnitId) }}</span>
                <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Genauigkeit</mat-label>
                  <mat-select [(ngModel)]="conversion.precision" [name]="'conversionPrecision' + index"
                    [disabled]="revision.state === publishedState || disabled()">
                    @for (precision of conversionPrecisions; track precision.value) {
                      <mat-option [value]="precision.value">{{ precision.label }}</mat-option>
                    }
                  </mat-select>
                </mat-form-field>
                @if (revision.state === draftState && !disabled()) {
                  <button matIconButton type="button" aria-label="Einheit entfernen"
                    (click)="removeUnitConversion(revision, index)"><scp-action-icon name="remove"/></button>
                }
              </div>
            } @empty {
              <p class="unit-conversion-empty">Neben der Basiseinheit sind noch keine weiteren Einheiten hinterlegt.</p>
            }
          </section>
          <div class="property-groups">
            <details class="property-group" open>
              <summary><span>Allergene</span><small>{{ specifiedMainAllergenCount(revision) }} von 14 angegeben</small></summary>
              <div class="property-review">
                <mat-checkbox [checked]="isReviewed(revision.allergenReviewState)"
                  (change)="revision.allergenReviewState = reviewState($event.checked)"
                  [disabled]="revision.state === publishedState || disabled()">Allergenangaben vollständig geprüft</mat-checkbox>
              </div>
              <div class="allergen-grid">
                @for (property of mainAllergens(); track property.id) {
                  <article class="allergen-card">
                    <div class="property-row allergen-main">
                      <span class="allergen-name"><strong>{{ allergenLetter(property.code) }}</strong>{{ property.name }}</span>
                      <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Zustand</mat-label>
                        <mat-select [value]="propertyState(revision.allergens, property.id)"
                          (selectionChange)="setPropertyState(revision, 'allergens', property.id, $event.value)"
                          [disabled]="revision.state === publishedState || disabled()">
                          <mat-option [value]="null">Nicht angegeben</mat-option>
                          @for (state of propertyStates; track state.value) {
                            <mat-option [value]="state.value">{{ state.label }}</mat-option>
                          }
                        </mat-select>
                      </mat-form-field>
                    </div>
                    @if (allergenChildren(property.id); as children) {
                      @if (children.length && propertyState(revision.allergens, property.id) === containsState) {
                        <details class="allergen-details">
                          <summary>Enthaltene Untertypen auswählen</summary>
                          <div class="allergen-children">
                            @for (child of children; track child.id) {
                              <div class="property-row property-child">
                                <span>{{ child.name }}</span>
                                <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Zustand</mat-label>
                                  <mat-select [value]="propertyState(revision.allergens, child.id)"
                                    (selectionChange)="setPropertyState(revision, 'allergens', child.id, $event.value)"
                                    [disabled]="revision.state === publishedState || disabled()">
                                    @for (state of propertyStates; track state.value) {
                                      <mat-option [value]="state.value">{{ state.label }}</mat-option>
                                    }
                                  </mat-select>
                                </mat-form-field>
                              </div>
                            }
                          </div>
                        </details>
                      } @else if (children.length && propertyState(revision.allergens, property.id) !== null) {
                        <p class="inherited-details">Alle Untertypen übernehmen „{{ propertyStateLabel(propertyState(revision.allergens, property.id)) }}“.</p>
                      }
                    }
                  </article>
                }
              </div>
            </details>
            <details class="property-group">
              <summary><span>Unverträglichkeiten</span><small>{{ specifiedVisibleIntoleranceCount(revision) }} angegeben</small></summary>
              <div class="property-review">
                <mat-checkbox [checked]="isReviewed(revision.intoleranceReviewState)"
                  (change)="setIntoleranceReviewState(revision, $event.checked)"
                  [disabled]="revision.state === publishedState || disabled()">Unverträglichkeiten vollständig geprüft</mat-checkbox>
              </div>
              <p class="property-info">Gluten wird nicht doppelt erfasst, sondern über Allergen A ausgewertet. Laktose bleibt von der Milchallergie getrennt.</p>
              <div class="property-grid">
                @for (property of commonIntolerances(); track property.id) {
                  <div class="property-row">
                    <span>{{ intoleranceLabel(property.code, property.name) }}</span>
                    <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Zustand</mat-label>
                      <mat-select [value]="propertyState(revision.intolerances, property.id)"
                        (selectionChange)="setPropertyState(revision, 'intolerances', property.id, $event.value)"
                        [disabled]="revision.state === publishedState || disabled()">
                        <mat-option [value]="null">Nicht angegeben</mat-option>
                        @for (state of propertyStates; track state.value) {
                          <mat-option [value]="state.value">{{ state.label }}</mat-option>
                        }
                      </mat-select>
                    </mat-form-field>
                  </div>
                }
              </div>
              <details class="secondary-details">
                <summary>Weitere Unverträglichkeiten</summary>
                <div class="property-grid">
                  @for (property of advancedIntolerances(); track property.id) {
                    <div class="property-row">
                      <span>{{ property.name }}</span>
                      <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Zustand</mat-label>
                        <mat-select [value]="propertyState(revision.intolerances, property.id)"
                          (selectionChange)="setPropertyState(revision, 'intolerances', property.id, $event.value)"
                          [disabled]="revision.state === publishedState || disabled()">
                          <mat-option [value]="null">Nicht angegeben</mat-option>
                          @for (state of propertyStates; track state.value) {
                            <mat-option [value]="state.value">{{ state.label }}</mat-option>
                          }
                        </mat-select>
                      </mat-form-field>
                    </div>
                  }
                </div>
              </details>
            </details>
            <details class="property-group">
              <summary><span>Herkunft</span><small>{{ primaryOriginLabel(revision) }}</small></summary>
              <div class="property-review">
                <mat-checkbox [checked]="isReviewed(revision.originReviewState)"
                  (change)="setOriginReviewState(revision, $event.checked)"
                  [disabled]="revision.state === publishedState || disabled() || !primaryOriginId(revision)">Herkunftsangaben vollständig geprüft</mat-checkbox>
              </div>
              <p class="property-info">Wähle genau eine Hauptherkunft. Besondere tierische Bestandteile können danach zusätzlich angegeben werden.</p>
              <div class="origin-main-selection">
                <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Hauptherkunft</mat-label>
                  <mat-select [value]="primaryOriginId(revision)"
                    (selectionChange)="setPrimaryOrigin(revision, $event.value)"
                    [disabled]="revision.state === publishedState || disabled()">
                    <mat-option [value]="null" disabled>Bitte auswählen</mat-option>
                    @for (property of primaryOrigins(); track property.id) {
                      <mat-option [value]="property.id">{{ property.name }}</mat-option>
                    }
                  </mat-select>
                </mat-form-field>
              </div>
              <details class="secondary-details">
                <summary>Zusätzliche tierische Merkmale</summary>
                <div class="property-grid">
                @for (property of additionalOrigins(); track property.id) {
                  <div class="property-row">
                    <span>{{ property.name }}</span>
                    <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Zustand</mat-label>
                      <mat-select [value]="propertyState(revision.origins, property.id)"
                        (selectionChange)="setPropertyState(revision, 'origins', property.id, $event.value)"
                        [disabled]="revision.state === publishedState || disabled()">
                        <mat-option [value]="null">Nicht angegeben</mat-option>
                        @for (state of propertyStates; track state.value) {
                          <mat-option [value]="state.value">{{ state.label }}</mat-option>
                        }
                      </mat-select>
                    </mat-form-field>
                  </div>
                }
                </div>
              </details>
            </details>
          </div>
          @if (revision.state === draftState) {
            <p class="revision-hint">„Geprüft“ bedeutet: Auch fehlende Einträge wurden bewusst kontrolliert.</p>
            <div class="revision-actions">
              <button matButton type="submit"
                [disabled]="submitting() || disabled() || !isDirty(revision) || !unitConversionsValid(revision)">
                <scp-action-icon name="save"/>Entwurf speichern</button>
              <button matButton="filled" type="button" (click)="publish()"
                [disabled]="submitting() || disabled() || isDirty(revision) || !allReviewed(revision) || !unitConversionsValid(revision)">Veröffentlichen</button>
            </div>
            @if (isDirty(revision) && allReviewed(revision)) {
              <p class="revision-hint revision-unsaved">Vor dem Veröffentlichen muss der aktuelle Entwurf gespeichert werden.</p>
            }
          } @else if (!disabled()) {
            <p class="revision-hint">Veröffentlichte Versionen bleiben unveränderlich. Änderungen erfolgen in einem neuen Entwurf.</p>
            <div class="revision-actions">
              <button matButton="filled" type="button" (click)="createNextDraft()" [disabled]="submitting()">
                <scp-action-icon name="edit"/>Neue Bearbeitung beginnen</button>
            </div>
          }
        </form>
      }
    }
  `,
  styles: `
    :host { display: grid; gap: 1rem; }
    .revision-editor-heading { display: flex; align-items: center; justify-content: space-between; gap: 1rem; }
    h4, p { margin: 0; } h4 { font-size: 1.05rem; } .revision-editor-heading p { color: #667168; font-size: .88rem; }
    .revision-loading { display: flex; align-items: center; gap: .75rem; min-height: 3rem; }
    .revision-message { margin: 0; padding: .75rem .9rem; border: 1px solid; border-radius: .65rem; }
    .revision-error { color: #8b2525; border-color: #efb8b8; background: #fff1f0; }
    .revision-notice { color: #214b28; border-color: #b8d9bd; background: #edf8ee; }
    .revision-list { display: grid; grid-template-columns: repeat(auto-fit, minmax(14rem, 1fr)); gap: .6rem; }
    .revision-list-item { display: flex; align-items: center; justify-content: space-between; gap: .75rem; padding: .8rem;
      border: 1px solid #dce4da; border-radius: .7rem; background: #f8faf7; color: inherit; text-align: left; cursor: pointer; }
    .revision-list-item.active { border-color: #6d9975; background: #edf5ed; }
    .revision-list-item span:first-child { display: grid; gap: .15rem; } .revision-list-item small { color: #667168; }
    .revision-form { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .75rem; padding: 1rem;
      border: 1px solid #cddbcc; border-radius: .8rem; background: #f5faf4; }
    .revision-form h4, .revision-form .revision-editor-heading, .unit-conversions, .property-groups, .revision-hint, .revision-actions { grid-column: 1 / -1; }
    .revision-form mat-form-field:first-of-type { grid-column: 1 / -1; }
    .property-groups { display: grid; gap: .65rem; }
    .unit-conversions { display: grid; gap: .65rem; padding: .85rem; border: 1px solid #d7e1d5;
      border-radius: .65rem; background: #fff; }
    .unit-conversion-heading { display: flex; align-items: center; justify-content: space-between; gap: 1rem; }
    .unit-conversion-heading p, .unit-conversion-empty { color: #667168; font-size: .84rem; }
    .unit-conversion-row { display: grid; grid-template-columns: minmax(10rem, 1.2fr) auto minmax(7rem, .7fr) auto minmax(10rem, 1fr) auto;
      align-items: center; gap: .55rem; padding: .65rem; border: 1px solid #e0e7de; border-radius: .6rem; background: #f8faf7; }
    .conversion-formula { white-space: nowrap; color: #536056; font-weight: 600; }
    .property-group { overflow: hidden; border: 1px solid #d7e1d5; border-radius: .65rem; background: #fff; }
    .property-group summary { display: flex; justify-content: space-between; gap: .75rem; padding: .8rem .9rem;
      background: #eef4ed; color: #334737; font-weight: 700; cursor: pointer; }
    .property-group summary small { color: #68736a; font-weight: 500; }
    .property-review { padding: .65rem .85rem; border-bottom: 1px solid #e2e8e0; }
    .property-info { padding: .7rem .85rem 0; color: #58635a; font-size: .84rem; }
    .allergen-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .65rem; padding: .8rem; }
    .allergen-card { align-self: start; overflow: hidden; border: 1px solid #dfe7dc; border-radius: .65rem; background: #fbfcfa; }
    .allergen-main { padding: .65rem; }
    .allergen-name { display: flex; align-items: center; gap: .55rem; font-weight: 600; }
    .allergen-name strong { display: inline-grid; place-items: center; flex: 0 0 1.7rem; height: 1.7rem; border-radius: .4rem;
      background: #3f7048; color: #fff; font-size: .85rem; }
    .allergen-details { border-top: 1px solid #e1e8df; }
    .allergen-details summary { padding: .6rem .75rem; background: #f2f6f1; font-size: .86rem; }
    .allergen-children { display: grid; gap: .45rem; padding: .65rem; }
    .inherited-details { padding: 0 .7rem .65rem; color: #667168; font-size: .8rem; }
    .secondary-details { margin: 0 .8rem .8rem; border: 1px solid #dfe7dc; border-radius: .6rem; }
    .secondary-details > summary { padding: .65rem .75rem; background: #f2f6f1; font-size: .88rem; }
    .origin-main-selection { padding: .8rem; }
    .origin-main-selection mat-form-field { width: min(100%, 28rem); }
    .property-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .55rem .8rem; padding: .8rem; }
    .property-row { display: grid; grid-template-columns: minmax(7rem, 1fr) minmax(9rem, 12rem); align-items: center; gap: .6rem; }
    .property-row > span { line-height: 1.25; }
    .property-child > span { padding-left: 1rem; color: #58635a; }
    .revision-hint { color: #58635a; font-size: .85rem; }
    .revision-actions { display: flex; justify-content: flex-end; gap: .4rem; }
    .revision-state { padding: .25rem .55rem; border-radius: 999px; background: #f1e6d5; color: #674a1d; font-size: .78rem; font-weight: 700; }
    .revision-state.published { background: #dcebdd; color: #214b28; }
    .revision-empty { padding: 1rem; border: 1px dashed #b8c4b7; border-radius: .7rem; color: #5b665c; }
    @media (max-width: 800px) { .revision-form, .property-grid, .allergen-grid { grid-template-columns: 1fr; }
      .revision-form > * { grid-column: 1 !important; } }
    @media (max-width: 680px) { .unit-conversion-heading { align-items: flex-start; flex-direction: column; }
      .unit-conversion-row { grid-template-columns: 1fr auto; }
      .unit-conversion-row mat-form-field { grid-column: 1 / -1; } }
    @media (max-width: 480px) { .property-row { grid-template-columns: 1fr; } }
  `
})
export class IngredientRevisionEditorComponent {
  readonly campId = input.required<string>();
  readonly disabled = input(false);
  readonly published = output<void>();
  readonly revisions = signal<IngredientRevisionSummary[]>([]);
  readonly selected = signal<IngredientRevisionDetails | null>(null);
  readonly referenceData = signal<IngredientEditorReferenceData | null>(null);
  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly createOpen = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly draftState = IngredientRevisionState.Draft;
  readonly publishedState = IngredientRevisionState.Published;
  readonly containsState = IngredientPropertyState.Contains;
  readonly propertyStates = [
    { value: IngredientPropertyState.Contains, label: 'Enthalten' },
    { value: IngredientPropertyState.DoesNotContain, label: 'Nicht enthalten' },
    { value: IngredientPropertyState.MayContain, label: 'Kann enthalten' },
    { value: IngredientPropertyState.Unknown, label: 'Unbekannt' }
  ] as const;
  readonly conversionPrecisions = [
    { value: IngredientConversionPrecision.Exact, label: 'Exakt' },
    { value: IngredientConversionPrecision.Average, label: 'Durchschnitt' },
    { value: IngredientConversionPrecision.Estimated, label: 'GeschÃ¤tzt' }
  ] as const;
  private readonly allergenLetters: Readonly<Record<string, string>> = {
    GLUTEN_CEREALS: 'A', CRUSTACEANS: 'B', EGGS: 'C', FISH: 'D', PEANUTS: 'E', SOYBEANS: 'F',
    MILK: 'G', TREE_NUTS: 'H', CELERY: 'L', MUSTARD: 'M', SESAME: 'N',
    SULPHUR_DIOXIDE_AND_SULPHITES: 'O', LUPIN: 'P', MOLLUSCS: 'R'
  };
  private readonly commonIntoleranceCodes = ['LACTOSE', 'FRUCTOSE', 'HISTAMINE'];
  private readonly primaryOriginCodes = [
    'PLANT', 'FUNGI', 'MICROBIAL', 'MINERAL', 'SYNTHETIC',
    'MEAT', 'POULTRY', 'FISH', 'CRUSTACEAN', 'MOLLUSC', 'DAIRY', 'EGG', 'HONEY', 'INSECT',
    'UNKNOWN_ORIGIN'
  ];
  private readonly additionalOriginCodes = [
    'ANIMAL_FAT', 'GELATIN', 'ANIMAL_RENNET', 'OTHER_ANIMAL_DERIVED'
  ];
  createName = '';
  createCategoryId = '';
  createBaseUnitId = '';
  private selectedSnapshot = '';
  private readonly api = inject(IngredientRevisionApiService);

  constructor() {
    effect(() => { const campId = this.campId(); if (campId) this.load(campId); });
  }

  canCreate() { return !!this.createName.trim() && !!this.createCategoryId && !!this.createBaseUnitId; }
  baseUnits() {
    const allowedSymbols = new Set(['g', 'kg', 'ml', 'l', 'Stk.']);
    return (this.referenceData()?.units ?? []).filter(value => allowedSymbols.has(value.symbol));
  }
  baseUnitsByDimension(dimension: number) {
    return this.baseUnits().filter(value => value.dimension === dimension);
  }
  stateLabel(state: IngredientRevisionState) { return state === this.publishedState ? 'Veröffentlicht' : 'Entwurf'; }
  isReviewed(state: IngredientPropertyReviewState) { return state === IngredientPropertyReviewState.Reviewed; }
  reviewState(checked: boolean) { return checked ? IngredientPropertyReviewState.Reviewed : IngredientPropertyReviewState.Unreviewed; }
  allReviewed(value: IngredientRevisionDetails) { return this.isReviewed(value.allergenReviewState) &&
    this.isReviewed(value.intoleranceReviewState) && this.isReviewed(value.originReviewState); }
  isDirty(value: IngredientRevisionDetails) { return this.snapshot(value) !== this.selectedSnapshot; }
  specifiedCount(values: IngredientRevisionPropertyItem[]) { return values.length; }
  specifiedMainAllergenCount(revision: IngredientRevisionDetails) {
    return this.mainAllergens().filter(value => this.propertyState(revision.allergens, value.id) !== null).length;
  }
  specifiedVisibleIntoleranceCount(revision: IngredientRevisionDetails) {
    const visibleIds = new Set([...this.commonIntolerances(), ...this.advancedIntolerances()].map(value => value.id));
    return revision.intolerances.filter(value => visibleIds.has(value.propertyId)).length;
  }
  commonIntolerances() {
    return (this.referenceData()?.intolerances ?? [])
      .filter(value => this.commonIntoleranceCodes.includes(value.code))
      .sort((left, right) => this.commonIntoleranceCodes.indexOf(left.code) - this.commonIntoleranceCodes.indexOf(right.code));
  }
  advancedIntolerances() {
    return (this.referenceData()?.intolerances ?? [])
      .filter(value => value.code !== 'GLUTEN' && !this.commonIntoleranceCodes.includes(value.code));
  }
  intoleranceLabel(code: string, name: string) {
    return code === 'LACTOSE' ? `${name} (nicht Milchallergie)` : name;
  }
  primaryOrigins() {
    return (this.referenceData()?.origins ?? [])
      .filter(value => this.primaryOriginCodes.includes(value.code))
      .sort((left, right) => this.primaryOriginCodes.indexOf(left.code) - this.primaryOriginCodes.indexOf(right.code));
  }
  additionalOrigins() {
    return (this.referenceData()?.origins ?? [])
      .filter(value => this.additionalOriginCodes.includes(value.code))
      .sort((left, right) => this.additionalOriginCodes.indexOf(left.code) - this.additionalOriginCodes.indexOf(right.code));
  }
  primaryOriginId(revision: IngredientRevisionDetails) {
    const contained = this.primaryOrigins()
      .filter(value => this.propertyState(revision.origins, value.id) === IngredientPropertyState.Contains);
    return contained.length === 1 ? contained[0].id : null;
  }
  primaryOriginLabel(revision: IngredientRevisionDetails) {
    const selectedId = this.primaryOriginId(revision);
    return this.primaryOrigins().find(value => value.id === selectedId)?.name ?? 'Bitte auswählen';
  }
  mainAllergens() {
    return (this.referenceData()?.allergens ?? []).filter(value => value.isEuMajorAllergen)
      .sort((left, right) => this.allergenLetter(left.code).localeCompare(this.allergenLetter(right.code)));
  }
  allergenChildren(parentId: string) {
    return (this.referenceData()?.allergens ?? []).filter(value => value.parentAllergenId === parentId);
  }
  allergenLetter(code: string) { return this.allergenLetters[code] ?? '?'; }
  propertyStateLabel(state: IngredientPropertyState | null) {
    return this.propertyStates.find(value => value.value === state)?.label ?? 'Nicht angegeben';
  }
  propertyState(values: IngredientRevisionPropertyItem[], propertyId: string) {
    return values.find(value => value.propertyId === propertyId)?.state ?? null;
  }

  unitSymbol(unitId: string) {
    return this.referenceData()?.units.find(value => value.id === unitId)?.symbol ?? '?';
  }

  availableConversionUnits(revision: IngredientRevisionDetails, currentSourceUnitId?: string) {
    const selectedIds = new Set(revision.unitConversions
      .filter(value => value.sourceUnitId !== currentSourceUnitId)
      .map(value => value.sourceUnitId));
    return (this.referenceData()?.units ?? [])
      .filter(value => this.isAllowedConversionUnit(revision.baseUnitId, value.id) && !selectedIds.has(value.id));
  }

  setBaseUnit(revision: IngredientRevisionDetails, baseUnitId: string) {
    revision.baseUnitId = baseUnitId;
    revision.unitConversions = revision.unitConversions
      .filter(value => this.isAllowedConversionUnit(baseUnitId, value.sourceUnitId));
  }

  addUnitConversion(revision: IngredientRevisionDetails) {
    const unit = this.availableConversionUnits(revision)[0];
    if (!unit) return;
    revision.unitConversions.push({
      sourceUnitId: unit.id,
      factorToBaseUnit: 1,
      precision: IngredientConversionPrecision.Average,
      factorInput: '1'
    });
  }

  removeUnitConversion(revision: IngredientRevisionDetails, index: number) {
    revision.unitConversions.splice(index, 1);
  }

  setConversionFactor(conversion: IngredientRevisionUnitConversionItem, input: string) {
    conversion.factorInput = input;
    const parsed = Number(input.trim().replace(',', '.'));
    conversion.factorToBaseUnit = Number.isFinite(parsed) && parsed > 0 ? parsed : Number.NaN;
  }

  normalizeConversionFactorInput(conversion: IngredientRevisionUnitConversionItem) {
    if (Number.isFinite(conversion.factorToBaseUnit) && conversion.factorToBaseUnit > 0)
      conversion.factorInput = String(conversion.factorToBaseUnit).replace('.', ',');
  }

  unitConversionsValid(revision: IngredientRevisionDetails) {
    return revision.unitConversions.every(value =>
      Number.isFinite(value.factorToBaseUnit) && value.factorToBaseUnit > 0 &&
      this.isAllowedConversionUnit(revision.baseUnitId, value.sourceUnitId));
  }

  private isAllowedConversionUnit(baseUnitId: string, sourceUnitId: string) {
    const units = this.referenceData()?.units ?? [];
    const baseUnit = units.find(value => value.id === baseUnitId);
    const sourceUnit = units.find(value => value.id === sourceUnitId);
    if (!baseUnit || !sourceUnit || baseUnit.id === sourceUnit.id) return false;
    const kitchenMeasureSymbols = new Set(['TL', 'EL', 'Prise', 'Bund']);
    return kitchenMeasureSymbols.has(sourceUnit.symbol) || sourceUnit.dimension !== baseUnit.dimension;
  }

  setPropertyState(
    revision: IngredientRevisionDetails,
    group: 'allergens' | 'intolerances' | 'origins',
    propertyId: string,
    state: IngredientPropertyState | null)
  {
    const values = revision[group];
    const previousState = this.propertyState(values, propertyId);
    this.setPropertyValue(values, propertyId, state, IngredientPropertySource.ManuallyVerified);
    if (group === 'allergens') {
      const definition = this.referenceData()?.allergens.find(value => value.id === propertyId);
      const children = definition?.parentAllergenId ? [] : this.allergenChildren(propertyId);
      if (children.length && state !== IngredientPropertyState.Contains) {
        for (const child of children)
          this.setPropertyValue(values, child.id, state, IngredientPropertySource.Derived);
      } else if (children.length && previousState !== IngredientPropertyState.Contains) {
        for (const child of children)
          this.setPropertyValue(values, child.id, IngredientPropertyState.Unknown, IngredientPropertySource.Derived);
      }
    }
    if (group === 'allergens') revision.allergenReviewState = IngredientPropertyReviewState.Unreviewed;
    else if (group === 'intolerances') revision.intoleranceReviewState = IngredientPropertyReviewState.Unreviewed;
    else revision.originReviewState = IngredientPropertyReviewState.Unreviewed;
  }

  setIntoleranceReviewState(revision: IngredientRevisionDetails, reviewed: boolean) {
    if (reviewed) {
      for (const property of this.commonIntolerances()) {
        if (this.propertyState(revision.intolerances, property.id) === null)
          this.setPropertyValue(revision.intolerances, property.id,
            IngredientPropertyState.Unknown, IngredientPropertySource.Derived);
      }
      for (const property of this.advancedIntolerances()) {
        if (this.propertyState(revision.intolerances, property.id) === null)
          this.setPropertyValue(revision.intolerances, property.id,
            IngredientPropertyState.DoesNotContain, IngredientPropertySource.Derived);
      }
    }
    revision.intoleranceReviewState = this.reviewState(reviewed);
  }

  setPrimaryOrigin(revision: IngredientRevisionDetails, propertyId: string | null) {
    if (!propertyId) return;
    this.applyPrimaryOrigin(revision, propertyId, IngredientPropertySource.ManuallyVerified);
    revision.originReviewState = IngredientPropertyReviewState.Unreviewed;
  }

  setOriginReviewState(revision: IngredientRevisionDetails, reviewed: boolean) {
    if (reviewed) {
      if (!this.primaryOriginId(revision)) return;
      for (const property of this.additionalOrigins()) {
        if (this.propertyState(revision.origins, property.id) === null)
          this.setPropertyValue(revision.origins, property.id,
            IngredientPropertyState.DoesNotContain, IngredientPropertySource.Derived);
      }
    }
    revision.originReviewState = this.reviewState(reviewed);
  }

  openCreate() {
    this.createName = '';
    this.createCategoryId = this.referenceData()?.categories[0]?.id ?? '';
    this.createBaseUnitId = this.baseUnits()[0]?.id ?? '';
    this.createOpen.set(true); this.error.set(''); this.notice.set('');
  }

  create() {
    if (!this.canCreate() || this.submitting()) return;
    this.submitting.set(true); this.error.set('');
    this.api.createCamp(this.campId(), { name: this.createName.trim(), categoryId: this.createCategoryId,
      baseUnitId: this.createBaseUnitId }).subscribe({
      next: result => { this.submitting.set(false); this.createOpen.set(false); this.notice.set('Der Zutatenentwurf wurde angelegt.');
        this.refreshList(result.revisionId); },
      error: () => { this.submitting.set(false); this.error.set('Der Zutatenentwurf konnte nicht angelegt werden.'); }
    });
  }

  open(revisionId: string) {
    this.error.set(''); this.notice.set('');
    this.api.get(revisionId).subscribe({ next: value => { this.initializeConversionFactorInputs(value);
      this.selectedSnapshot = this.snapshot(value);
      if (value.state === this.draftState) {
        this.normalizeAllergenDetails(value);
        this.normalizeCommonIntolerances(value);
        this.normalizePrimaryOrigin(value);
      }
      this.selected.set(value); },
      error: () => this.error.set('Die Zutatenrevision konnte nicht geladen werden.') });
  }

  save() {
    const revision = this.selected(); if (!revision || revision.state !== this.draftState) return;
    this.submitting.set(true); this.error.set(''); this.notice.set('');
    this.api.save(revision.id, { name: revision.name, categoryId: revision.categoryId, baseUnitId: revision.baseUnitId,
      allergenReviewState: revision.allergenReviewState, intoleranceReviewState: revision.intoleranceReviewState,
      originReviewState: revision.originReviewState, expectedRowVersion: revision.rowVersion,
      allergens: revision.allergens, intolerances: revision.intolerances, origins: revision.origins,
      unitConversions: revision.unitConversions.map(value => ({
        sourceUnitId: value.sourceUnitId,
        factorToBaseUnit: value.factorToBaseUnit,
        precision: value.precision
      })) }).subscribe({
      next: result => { revision.rowVersion = result.rowVersion; this.selectedSnapshot = this.snapshot(revision);
        this.selected.set({ ...revision }); this.submitting.set(false);
        this.notice.set('Der Entwurf wurde gespeichert.'); this.refreshList(revision.id, false); },
      error: error => this.handleMutationError(error)
    });
  }

  publish() {
    const revision = this.selected();
    if (!revision || this.isDirty(revision) || !this.allReviewed(revision) || !this.unitConversionsValid(revision)) return;
    this.submitting.set(true); this.error.set(''); this.notice.set('');
    this.api.publish(revision.id, revision.rowVersion).subscribe({
      next: result => { this.submitting.set(false); revision.rowVersion = result.rowVersion;
        revision.state = this.publishedState; this.selectedSnapshot = this.snapshot(revision);
        this.selected.set({ ...revision }); this.notice.set('Die Zutat wurde veröffentlicht.');
        this.published.emit();
        this.refreshList(revision.id, false); },
      error: error => this.handleMutationError(error)
    });
  }

  createNextDraft() {
    const revision = this.selected();
    if (!revision || revision.state !== this.publishedState || this.submitting()) return;
    this.submitting.set(true); this.error.set(''); this.notice.set('');
    this.api.createDraftFromPublished(revision.id).subscribe({
      next: result => { this.submitting.set(false); this.notice.set('Ein neuer Entwurf wurde aus der veröffentlichten Version erstellt.');
        if (result.revisionId) this.refreshList(result.revisionId); },
      error: (error: HttpErrorResponse) => {
        this.submitting.set(false);
        if (error.status === 409 && error.error?.code === 'ingredient_revision_draft_exists') {
          this.notice.set('Für diese Zutat besteht bereits ein Entwurf.');
          const revisionId = error.error?.revisionId as string | undefined;
          this.refreshList(revisionId);
        } else this.error.set('Der neue Zutatenentwurf konnte nicht erstellt werden.');
      }
    });
  }

  private load(campId: string) {
    this.loading.set(true); this.selected.set(null); this.selectedSnapshot = ''; this.error.set('');
    forkJoin({ referenceData: this.api.getReferenceData(), revisions: this.api.listCamp(campId) }).subscribe({
      next: result => { this.referenceData.set(result.referenceData); this.revisions.set(result.revisions); this.loading.set(false); },
      error: () => { this.loading.set(false); this.error.set('Die Zutatenverwaltung konnte nicht geladen werden.'); }
    });
  }

  private refreshList(openRevisionId?: string, openAfter = true) {
    this.api.listCamp(this.campId()).subscribe({ next: values => { this.revisions.set(values);
      if (openRevisionId && openAfter) this.open(openRevisionId); },
      error: () => this.error.set('Die Zutatenliste konnte nicht aktualisiert werden.') });
  }

  private handleMutationError(error: HttpErrorResponse) {
    this.submitting.set(false);
    if (error.status === 409) {
      this.error.set('Die Revision wurde zwischenzeitlich geändert und wird neu geladen.');
      const revision = this.selected(); if (revision) this.open(revision.id);
    } else this.error.set('Die Zutatenrevision konnte nicht gespeichert werden.');
  }

  private snapshot(value: IngredientRevisionDetails) {
    return JSON.stringify({ name: value.name.trim(), categoryId: value.categoryId, baseUnitId: value.baseUnitId,
      allergenReviewState: value.allergenReviewState, intoleranceReviewState: value.intoleranceReviewState,
      originReviewState: value.originReviewState,
      allergens: this.sortedProperties(value.allergens), intolerances: this.sortedProperties(value.intolerances),
      origins: this.sortedProperties(value.origins),
      unitConversions: this.sortedUnitConversions(value.unitConversions) });
  }

  private sortedProperties(values: IngredientRevisionPropertyItem[]) {
    return [...values].sort((left, right) => left.propertyId.localeCompare(right.propertyId));
  }

  private sortedUnitConversions(values: IngredientRevisionUnitConversionItem[]) {
    return values.map(value => ({
      sourceUnitId: value.sourceUnitId,
      factorToBaseUnit: value.factorToBaseUnit,
      precision: value.precision,
      factorInput: value.factorInput
    })).sort((left, right) => left.sourceUnitId.localeCompare(right.sourceUnitId));
  }

  private initializeConversionFactorInputs(revision: IngredientRevisionDetails) {
    for (const conversion of revision.unitConversions)
      conversion.factorInput = String(conversion.factorToBaseUnit).replace('.', ',');
  }

  private normalizeAllergenDetails(revision: IngredientRevisionDetails) {
    let changed = false;
    for (const parent of this.mainAllergens()) {
      const children = this.allergenChildren(parent.id);
      const parentState = this.propertyState(revision.allergens, parent.id);
      if (!children.length || parentState === null) continue;
      const inheritedState = parentState === IngredientPropertyState.Contains
        ? IngredientPropertyState.Unknown : parentState;
      for (const child of children) {
        const childState = this.propertyState(revision.allergens, child.id);
        if ((parentState === IngredientPropertyState.Contains && childState !== null) || childState === inheritedState)
          continue;
        this.setPropertyValue(revision.allergens, child.id, inheritedState, IngredientPropertySource.Derived);
        changed = true;
      }
    }
    if (changed) revision.allergenReviewState = IngredientPropertyReviewState.Unreviewed;
  }

  private normalizeCommonIntolerances(revision: IngredientRevisionDetails) {
    let changed = false;
    for (const property of this.commonIntolerances()) {
      if (this.propertyState(revision.intolerances, property.id) !== null) continue;
      this.setPropertyValue(revision.intolerances, property.id,
        IngredientPropertyState.Unknown, IngredientPropertySource.Derived);
      changed = true;
    }
    if (changed) revision.intoleranceReviewState = IngredientPropertyReviewState.Unreviewed;
  }

  private normalizePrimaryOrigin(revision: IngredientRevisionDetails) {
    const primaryOrigins = this.primaryOrigins();
    if (primaryOrigins.some(value => this.propertyState(revision.origins, value.id) !== null)) return;
    const unknownOrigin = primaryOrigins.find(value => value.code === 'UNKNOWN_ORIGIN');
    if (!unknownOrigin) return;
    this.applyPrimaryOrigin(revision, unknownOrigin.id, IngredientPropertySource.Derived);
    revision.originReviewState = IngredientPropertyReviewState.Unreviewed;
  }

  private applyPrimaryOrigin(
    revision: IngredientRevisionDetails,
    selectedPropertyId: string,
    selectedSource: IngredientPropertySource)
  {
    for (const property of this.primaryOrigins()) {
      const selected = property.id === selectedPropertyId;
      this.setPropertyValue(revision.origins, property.id,
        selected ? IngredientPropertyState.Contains : IngredientPropertyState.DoesNotContain,
        selected ? selectedSource : IngredientPropertySource.Derived);
    }
  }

  private setPropertyValue(
    values: IngredientRevisionPropertyItem[],
    propertyId: string,
    state: IngredientPropertyState | null,
    source: IngredientPropertySource)
  {
    const index = values.findIndex(value => value.propertyId === propertyId);
    if (state === null) {
      if (index >= 0) values.splice(index, 1);
      return;
    }
    const item = { propertyId, state, source };
    if (index >= 0) values[index] = item;
    else values.push(item);
  }
}
