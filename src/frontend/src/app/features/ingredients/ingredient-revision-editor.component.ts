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
  IngredientVariantRevisionItem,
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
          <section class="ingredient-variants">
            <div class="unit-conversion-heading">
              <div>
                <h4>Varianten</h4>
                <p>Varianten sind Ausprägungen derselben Zutat, zum Beispiel „laktosefrei“ oder „geräuchert“.</p>
              </div>
              @if (revision.state === draftState && !disabled()) {
                <button matButton type="button" (click)="addVariant(revision)">
                  <scp-action-icon name="add"/>Variante hinzufügen
                </button>
              }
            </div>
            @for (variant of revision.variants; track variant.id; let index = $index) {
              <article class="variant-row" [class.inactive]="!variant.isActive">
                <div class="variant-header">
                  <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Bezeichnung</mat-label>
                    <input matInput [ngModel]="variant.name" (ngModelChange)="setVariantName(revision, variant, $event)"
                      [name]="'variantName' + index" maxlength="200" required
                      [disabled]="revision.state === publishedState || disabled()">
                  </mat-form-field>
                  <mat-checkbox [(ngModel)]="variant.isActive" [name]="'variantActive' + index"
                    [disabled]="revision.state === publishedState || disabled()">Aktiv</mat-checkbox>
                  @if (revision.state === draftState && !disabled()) {
                    <div class="variant-actions">
                      @if (variant.isNew) {
                        <button matIconButton type="button" aria-label="Neue Variante verwerfen"
                          (click)="removeNewVariant(revision, index)"><scp-action-icon name="remove"/></button>
                      }
                    </div>
                  }
                </div>
                <details class="variant-overrides">
                  <summary>Abweichungen zur Basiszutat <small>{{ variantOverrideCount(variant) }} festgelegt</small></summary>
                  <p class="property-info">Ohne abweichende Auswahl gilt automatisch der Wert der Basiszutat.</p>
                  <div class="variant-override-groups">
                    <section>
                      <h5>Allergene</h5>
                      <div class="variant-override-grid">
                        @for (property of variantVisibleAllergens(revision, variant); track property.id) {
                          <div class="property-row">
                            <span [class.variant-child-property]="!!property.parentAllergenId">{{ property.isEuMajorAllergen ? allergenLetter(property.code) + ' · ' : '' }}{{ property.name }}</span>
                            <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Abweichung</mat-label>
                              <mat-select [value]="propertyState(variant.allergenOverrides, property.id)"
                                (selectionChange)="setVariantPropertyState(revision, variant, 'allergens', property.id, $event.value)"
                                [disabled]="revision.state === publishedState || disabled()">
                                <mat-option [value]="null">Wie Basis ({{ propertyStateLabel(propertyState(revision.allergens, property.id)) }})</mat-option>
                                @for (state of propertyStates; track state.value) {
                                  <mat-option [value]="state.value">{{ state.label }}</mat-option>
                                }
                              </mat-select>
                            </mat-form-field>
                          </div>
                        }
                      </div>
                    </section>
                    <section>
                      <h5>Unverträglichkeiten</h5>
                      <div class="variant-override-grid">
                        @for (property of visibleIntolerances(); track property.id) {
                          <div class="property-row">
                            <span>{{ property.name }}</span>
                            <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Abweichung</mat-label>
                              <mat-select [value]="propertyState(variant.intoleranceOverrides, property.id)"
                                (selectionChange)="setVariantPropertyState(revision, variant, 'intolerances', property.id, $event.value)"
                                [disabled]="revision.state === publishedState || disabled()">
                                <mat-option [value]="null">Wie Basis ({{ propertyStateLabel(propertyState(revision.intolerances, property.id)) }})</mat-option>
                                @for (state of propertyStates; track state.value) {
                                  <mat-option [value]="state.value">{{ state.label }}</mat-option>
                                }
                              </mat-select>
                            </mat-form-field>
                          </div>
                        }
                      </div>
                    </section>
                    <section>
                      <h5>Herkunft</h5>
                      <div class="variant-primary-origin">
                        <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Hauptherkunft</mat-label>
                          <mat-select [value]="variantPrimaryOriginOverrideId(variant)"
                            (selectionChange)="setVariantPrimaryOrigin(revision, variant, $event.value)"
                            [disabled]="revision.state === publishedState || disabled()">
                            <mat-option [value]="null">Wie Basis ({{ primaryOriginLabel(revision) }})</mat-option>
                            @for (property of primaryOrigins(); track property.id) {
                              <mat-option [value]="property.id">{{ property.name }}</mat-option>
                            }
                          </mat-select>
                        </mat-form-field>
                      </div>
                      <div class="variant-override-grid">
                        @for (property of additionalOrigins(); track property.id) {
                          <div class="property-row">
                            <span>{{ property.name }}</span>
                            <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Abweichung</mat-label>
                              <mat-select [value]="propertyState(variant.originOverrides, property.id)"
                                (selectionChange)="setVariantPropertyState(revision, variant, 'origins', property.id, $event.value)"
                                [disabled]="revision.state === publishedState || disabled()">
                                <mat-option [value]="null">Wie Basis ({{ propertyStateLabel(propertyState(revision.origins, property.id)) }})</mat-option>
                                @for (state of propertyStates; track state.value) {
                                  <mat-option [value]="state.value">{{ state.label }}</mat-option>
                                }
                              </mat-select>
                            </mat-form-field>
                          </div>
                        }
                      </div>
                    </section>
                    <section>
                      <h5>Weitere Einheiten</h5>
                      <div class="variant-conversion-list">
                        @for (baseConversion of revision.unitConversions; track baseConversion.sourceUnitId) {
                          <div class="variant-conversion-row">
                            <mat-checkbox [checked]="hasVariantConversionOverride(variant, baseConversion.sourceUnitId)"
                              (change)="setVariantConversionOverride(variant, baseConversion, $event.checked)"
                              [disabled]="revision.state === publishedState || disabled()">
                              1 {{ unitSymbol(baseConversion.sourceUnitId) }} abweichend berechnen
                            </mat-checkbox>
                            @if (variantConversionOverride(variant, baseConversion.sourceUnitId); as conversion) {
                              <span class="conversion-formula">=</span>
                              <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Menge</mat-label>
                                <input matInput type="text" inputmode="decimal" [ngModel]="conversion.factorInput"
                                  (ngModelChange)="setConversionFactor(conversion, $event)"
                                  (blur)="normalizeConversionFactorInput(conversion)"
                                  [name]="'variantConversionFactor' + index + baseConversion.sourceUnitId"
                                  [disabled]="revision.state === publishedState || disabled()" required>
                              </mat-form-field>
                              <span class="conversion-formula">{{ unitSymbol(revision.baseUnitId) }}</span>
                              <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Genauigkeit</mat-label>
                                <mat-select [(ngModel)]="conversion.precision"
                                  [name]="'variantConversionPrecision' + index + baseConversion.sourceUnitId"
                                  [disabled]="revision.state === publishedState || disabled()">
                                  @for (precision of conversionPrecisions; track precision.value) {
                                    <mat-option [value]="precision.value">{{ precision.label }}</mat-option>
                                  }
                                </mat-select>
                              </mat-form-field>
                            } @else {
                              <span class="variant-inherited-value">Wie Basis: {{ baseConversion.factorInput }} {{ unitSymbol(revision.baseUnitId) }}</span>
                            }
                          </div>
                        }
                        @if (!revision.unitConversions.length) {
                          <p class="unit-conversion-empty">Lege weitere Einheiten zuerst bei der Basiszutat an. Die Variante kann anschließend deren Faktor oder Genauigkeit überschreiben.</p>
                        }
                      </div>
                    </section>
                  </div>
                </details>
              </article>
            } @empty {
              <p class="unit-conversion-empty">Für diese Zutat sind noch keine Varianten angelegt.</p>
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
                [disabled]="submitting() || disabled() || !isDirty(revision) || !unitConversionsValid(revision) || !variantsValid(revision)">
                <scp-action-icon name="save"/>Entwurf speichern</button>
              <button matButton="filled" type="button" (click)="publish()"
                [disabled]="submitting() || disabled() || isDirty(revision) || !allReviewed(revision) || !unitConversionsValid(revision) || !variantsValid(revision)">Veröffentlichen</button>
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
    .revision-form h4, .revision-form .revision-editor-heading, .unit-conversions, .ingredient-variants, .property-groups, .revision-hint, .revision-actions { grid-column: 1 / -1; }
    .revision-form mat-form-field:first-of-type { grid-column: 1 / -1; }
    .property-groups { display: grid; gap: .65rem; }
    .unit-conversions { display: grid; gap: .65rem; padding: .85rem; border: 1px solid #d7e1d5;
      border-radius: .65rem; background: #fff; }
    .ingredient-variants { display: grid; gap: .65rem; padding: .85rem; border: 1px solid #d7e1d5;
      border-radius: .65rem; background: #fff; }
    .unit-conversion-heading { display: flex; align-items: center; justify-content: space-between; gap: 1rem; }
    .unit-conversion-heading p, .unit-conversion-empty { color: #667168; font-size: .84rem; }
    .unit-conversion-row { display: grid; grid-template-columns: minmax(10rem, 1.2fr) auto minmax(7rem, .7fr) auto minmax(10rem, 1fr) auto;
      align-items: center; gap: .55rem; padding: .65rem; border: 1px solid #e0e7de; border-radius: .6rem; background: #f8faf7; }
    .conversion-formula { white-space: nowrap; color: #536056; font-weight: 600; }
    .variant-row { display: grid; gap: .65rem; padding: .65rem; border: 1px solid #e0e7de;
      border-radius: .6rem; background: #f8faf7; }
    .variant-row.inactive { background: #f3f3f1; color: #69706a; }
    .variant-header { display: grid; grid-template-columns: minmax(12rem, 1fr) auto auto; align-items: center; gap: .7rem; }
    .variant-actions { display: flex; align-items: center; }
    .variant-overrides { border-top: 1px solid #dde5db; }
    .variant-overrides > summary { display: flex; justify-content: space-between; gap: .75rem; padding: .65rem .2rem .1rem;
      color: #425247; font-weight: 650; cursor: pointer; }
    .variant-overrides > summary small { color: #68736a; font-weight: 500; }
    .variant-override-groups { display: grid; gap: .65rem; padding-top: .7rem; }
    .variant-override-groups section { overflow: hidden; border: 1px solid #e0e7de; border-radius: .55rem; background: #fff; }
    .variant-override-groups h5 { margin: 0; padding: .6rem .75rem; background: #f0f5ef; font-size: .9rem; }
    .variant-override-grid { display: grid; grid-template-columns: repeat(2, minmax(0, 1fr)); gap: .5rem .8rem; padding: .7rem; }
    .variant-child-property { padding-left: 1.35rem; color: #5f6b61; }
    .variant-primary-origin { padding: .7rem .7rem 0; }
    .variant-primary-origin mat-form-field { width: min(100%, 28rem); }
    .variant-conversion-list { display: grid; gap: .55rem; padding: .7rem; }
    .variant-conversion-row { display: grid; grid-template-columns: minmax(14rem, 1fr) auto minmax(7rem, .7fr) auto minmax(10rem, 1fr);
      align-items: center; gap: .55rem; }
    .variant-inherited-value { color: #68736a; font-size: .84rem; }
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
      .unit-conversion-row mat-form-field { grid-column: 1 / -1; }
      .variant-header, .variant-override-grid { grid-template-columns: 1fr auto; }
      .variant-header mat-form-field, .variant-override-grid .property-row { grid-column: 1 / -1; }
      .variant-conversion-row { grid-template-columns: 1fr auto; }
      .variant-conversion-row mat-checkbox, .variant-conversion-row .variant-inherited-value { grid-column: 1 / -1; } }
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
  visibleIntolerances() { return [...this.commonIntolerances(), ...this.advancedIntolerances()]; }
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

  private effectiveVariantPropertyState(
    baseValues: IngredientRevisionPropertyItem[],
    overrides: IngredientRevisionPropertyItem[],
    propertyId: string)
  {
    return this.propertyState(overrides, propertyId) ?? this.propertyState(baseValues, propertyId);
  }

  variantVisibleAllergens(revision: IngredientRevisionDetails, variant: IngredientVariantRevisionItem) {
    return this.mainAllergens().flatMap(parent => {
      const children = this.allergenChildren(parent.id);
      const parentState = this.effectiveVariantPropertyState(
        revision.allergens, variant.allergenOverrides, parent.id);
      const visibleChildren = parentState === IngredientPropertyState.Contains ||
        children.some(child => this.propertyState(variant.allergenOverrides, child.id) !== null)
        ? children : [];
      return [parent, ...visibleChildren];
    });
  }

  variantOverrideCount(variant: IngredientVariantRevisionItem) {
    return variant.allergenOverrides.length + variant.intoleranceOverrides.length +
      variant.originOverrides.length + variant.unitConversionOverrides.length;
  }

  setVariantPropertyState(
    revision: IngredientRevisionDetails,
    variant: IngredientVariantRevisionItem,
    group: 'allergens' | 'intolerances' | 'origins',
    propertyId: string,
    state: IngredientPropertyState | null)
  {
    const values = group === 'allergens' ? variant.allergenOverrides :
      group === 'intolerances' ? variant.intoleranceOverrides : variant.originOverrides;
    this.setPropertyValue(values, propertyId, state, IngredientPropertySource.ManuallyVerified);
    if (group === 'allergens') {
      const children = this.allergenChildren(propertyId);
      if (children.length && state === null) {
        for (const child of children) {
          const inherited = variant.allergenOverrides.find(value => value.propertyId === child.id);
          if (inherited?.source === IngredientPropertySource.Derived)
            this.setPropertyValue(values, child.id, null, IngredientPropertySource.Derived);
        }
      } else if (children.length && state !== IngredientPropertyState.Contains) {
        for (const child of children)
          this.setPropertyValue(values, child.id, state, IngredientPropertySource.Derived);
      } else if (children.length) {
        for (const child of children) {
          const existing = variant.allergenOverrides.find(value => value.propertyId === child.id);
          if (!existing || existing.source === IngredientPropertySource.Derived)
            this.setPropertyValue(values, child.id,
              IngredientPropertyState.Unknown, IngredientPropertySource.Derived);
        }
      }
      revision.allergenReviewState = IngredientPropertyReviewState.Unreviewed;
    }
    else if (group === 'intolerances') revision.intoleranceReviewState = IngredientPropertyReviewState.Unreviewed;
    else revision.originReviewState = IngredientPropertyReviewState.Unreviewed;
  }

  variantPrimaryOriginOverrideId(variant: IngredientVariantRevisionItem) {
    const primaryIds = new Set(this.primaryOrigins().map(value => value.id));
    return variant.originOverrides.find(value => primaryIds.has(value.propertyId) &&
      value.state === IngredientPropertyState.Contains)?.propertyId ?? null;
  }

  setVariantPrimaryOrigin(
    revision: IngredientRevisionDetails,
    variant: IngredientVariantRevisionItem,
    propertyId: string | null)
  {
    for (const property of this.primaryOrigins()) {
      if (propertyId === null)
        this.setPropertyValue(variant.originOverrides, property.id, null, IngredientPropertySource.Derived);
      else
        this.setPropertyValue(variant.originOverrides, property.id,
          property.id === propertyId ? IngredientPropertyState.Contains : IngredientPropertyState.DoesNotContain,
          property.id === propertyId ? IngredientPropertySource.ManuallyVerified : IngredientPropertySource.Derived);
    }
    revision.originReviewState = IngredientPropertyReviewState.Unreviewed;
  }

  variantConversionOverride(variant: IngredientVariantRevisionItem, sourceUnitId: string) {
    return variant.unitConversionOverrides.find(value => value.sourceUnitId === sourceUnitId) ?? null;
  }

  hasVariantConversionOverride(variant: IngredientVariantRevisionItem, sourceUnitId: string) {
    return this.variantConversionOverride(variant, sourceUnitId) !== null;
  }

  setVariantConversionOverride(
    variant: IngredientVariantRevisionItem,
    baseConversion: IngredientRevisionUnitConversionItem,
    enabled: boolean)
  {
    const index = variant.unitConversionOverrides.findIndex(
      value => value.sourceUnitId === baseConversion.sourceUnitId);
    if (!enabled) {
      if (index >= 0) variant.unitConversionOverrides.splice(index, 1);
      return;
    }
    if (index >= 0) return;
    variant.unitConversionOverrides.push({
      sourceUnitId: baseConversion.sourceUnitId,
      factorToBaseUnit: baseConversion.factorToBaseUnit,
      precision: baseConversion.precision,
      factorInput: baseConversion.factorInput ?? String(baseConversion.factorToBaseUnit).replace('.', ',')
    });
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
    this.removeOrphanedVariantConversions(revision);
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
    this.removeOrphanedVariantConversions(revision);
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

  variantsValid(revision: IngredientRevisionDetails) {
    const names = revision.variants.map(value => value.name.trim().toLocaleUpperCase('de'));
    const keys = revision.variants.map(value => value.variantKey);
    const baseConversionIds = new Set(revision.unitConversions.map(value => value.sourceUnitId));
    return revision.variants.every(value => !!value.name.trim() && !!value.variantKey) &&
      revision.variants.every(value => value.unitConversionOverrides.every(conversion =>
        baseConversionIds.has(conversion.sourceUnitId) && Number.isFinite(conversion.factorToBaseUnit) &&
        conversion.factorToBaseUnit > 0)) &&
      new Set(names).size === names.length && new Set(keys).size === keys.length;
  }

  addVariant(revision: IngredientRevisionDetails) {
    revision.variants.push({
      id: crypto.randomUUID(),
      variantKey: '',
      name: '',
      isActive: true,
      sortOrder: Math.max(-1, ...revision.variants.map(value => value.sortOrder)) + 1,
      allergenOverrides: [],
      intoleranceOverrides: [],
      originOverrides: [],
      unitConversionOverrides: [],
      isNew: true
    });
  }

  setVariantName(
    revision: IngredientRevisionDetails,
    variant: IngredientVariantRevisionItem,
    name: string)
  {
    variant.name = name;
    if (variant.isNew)
      variant.variantKey = this.uniqueVariantKey(revision, variant, name);
  }

  removeNewVariant(revision: IngredientRevisionDetails, index: number) {
    if (!revision.variants[index]?.isNew) return;
    revision.variants.splice(index, 1);
  }

  private isAllowedConversionUnit(baseUnitId: string, sourceUnitId: string) {
    const units = this.referenceData()?.units ?? [];
    const baseUnit = units.find(value => value.id === baseUnitId);
    const sourceUnit = units.find(value => value.id === sourceUnitId);
    if (!baseUnit || !sourceUnit || baseUnit.id === sourceUnit.id) return false;
    const kitchenMeasureSymbols = new Set(['TL', 'EL', 'Prise', 'Bund']);
    return kitchenMeasureSymbols.has(sourceUnit.symbol) || sourceUnit.dimension !== baseUnit.dimension;
  }

  private removeOrphanedVariantConversions(revision: IngredientRevisionDetails) {
    const available = new Set(revision.unitConversions.map(value => value.sourceUnitId));
    for (const variant of revision.variants)
      variant.unitConversionOverrides = variant.unitConversionOverrides
        .filter(value => available.has(value.sourceUnitId));
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
      })), variants: revision.variants.map(value => ({
        id: value.id,
        variantKey: value.variantKey,
        name: value.name,
        isActive: value.isActive,
        sortOrder: value.sortOrder,
        allergenOverrides: value.allergenOverrides,
        intoleranceOverrides: value.intoleranceOverrides,
        originOverrides: value.originOverrides,
        unitConversionOverrides: value.unitConversionOverrides.map(conversion => ({
          sourceUnitId: conversion.sourceUnitId,
          factorToBaseUnit: conversion.factorToBaseUnit,
          precision: conversion.precision
        }))
      })) }).subscribe({
      next: result => { revision.rowVersion = result.rowVersion; this.selectedSnapshot = this.snapshot(revision);
        revision.variants.forEach(value => value.isNew = false);
        this.selected.set({ ...revision }); this.submitting.set(false);
        this.notice.set('Der Entwurf wurde gespeichert.'); this.refreshList(revision.id, false); },
      error: error => this.handleMutationError(error)
    });
  }

  publish() {
    const revision = this.selected();
    if (!revision || this.isDirty(revision) || !this.allReviewed(revision) ||
        !this.unitConversionsValid(revision) || !this.variantsValid(revision)) return;
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
      unitConversions: this.sortedUnitConversions(value.unitConversions),
      variants: value.variants.map(variant => ({ id: variant.id, variantKey: variant.variantKey,
        name: variant.name.trim(), isActive: variant.isActive, sortOrder: variant.sortOrder,
        allergenOverrides: this.sortedProperties(variant.allergenOverrides),
        intoleranceOverrides: this.sortedProperties(variant.intoleranceOverrides),
        originOverrides: this.sortedProperties(variant.originOverrides),
        unitConversionOverrides: this.sortedUnitConversions(variant.unitConversionOverrides) })) });
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
    for (const variant of revision.variants)
      for (const conversion of variant.unitConversionOverrides)
        conversion.factorInput = String(conversion.factorToBaseUnit).replace('.', ',');
  }

  private uniqueVariantKey(
    revision: IngredientRevisionDetails,
    current: IngredientVariantRevisionItem,
    name: string)
  {
    const normalized = name.trim().toLocaleLowerCase('de').replaceAll('ß', 'ss')
      .normalize('NFD').replace(/[\u0300-\u036f]/g, '')
      .replace(/[^a-z0-9]+/g, '_').replace(/^_+|_+$/g, '') || 'variante';
    const used = new Set(revision.variants.filter(value => value !== current).map(value => value.variantKey));
    let candidate = normalized;
    for (let suffix = 2; used.has(candidate); suffix++) candidate = `${normalized}_${suffix}`;
    return candidate;
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
