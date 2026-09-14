import { Component, input } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import {
  IngredientNutritionProfileItem,
  IngredientNutritionReviewState,
  IngredientNutritionSourceType,
  MeasurementUnitReference
} from './ingredient-revision-api.service';

@Component({
  selector: 'scp-ingredient-nutrition-editor',
  standalone: true,
  imports: [FormsModule, MatCheckboxModule, MatFormFieldModule, MatInputModule, MatSelectModule],
  template: `
    <div class="nutrition-reference">
      <strong>Angaben je {{ profile().referenceQuantity }} {{ unitSymbol() }}</strong>
      @if (profile().energyKilojoules !== null) {
        <span>entspricht ca. {{ kilocalories() }} kcal</span>
      }
    </div>
    <div class="nutrition-values">
      <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Energie (kJ)</mat-label>
        <input matInput type="number" min="0" step="0.1" [ngModel]="profile().energyKilojoules"
          (ngModelChange)="setNumber('energyKilojoules', $event)" [ngModelOptions]="{ standalone: true }"
          [disabled]="disabled()">
      </mat-form-field>
      <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Fett (g)</mat-label>
        <input matInput type="number" min="0" step="0.01" [ngModel]="profile().fatGrams"
          (ngModelChange)="setNumber('fatGrams', $event)" [ngModelOptions]="{ standalone: true }"
          [disabled]="disabled()">
      </mat-form-field>
      <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>davon gesättigt (g)</mat-label>
        <input matInput type="number" min="0" step="0.01" [ngModel]="profile().saturatedFatGrams"
          (ngModelChange)="setNumber('saturatedFatGrams', $event)" [ngModelOptions]="{ standalone: true }"
          [disabled]="disabled()">
      </mat-form-field>
      <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Kohlenhydrate (g)</mat-label>
        <input matInput type="number" min="0" step="0.01" [ngModel]="profile().carbohydrateGrams"
          (ngModelChange)="setNumber('carbohydrateGrams', $event)" [ngModelOptions]="{ standalone: true }"
          [disabled]="disabled()">
      </mat-form-field>
      <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>davon Zucker (g)</mat-label>
        <input matInput type="number" min="0" step="0.01" [ngModel]="profile().sugarsGrams"
          (ngModelChange)="setNumber('sugarsGrams', $event)" [ngModelOptions]="{ standalone: true }"
          [disabled]="disabled()">
      </mat-form-field>
      <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Eiweiß (g)</mat-label>
        <input matInput type="number" min="0" step="0.01" [ngModel]="profile().proteinGrams"
          (ngModelChange)="setNumber('proteinGrams', $event)" [ngModelOptions]="{ standalone: true }"
          [disabled]="disabled()">
      </mat-form-field>
      <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Salz (g)</mat-label>
        <input matInput type="number" min="0" step="0.001" [ngModel]="profile().saltGrams"
          (ngModelChange)="setNumber('saltGrams', $event)" [ngModelOptions]="{ standalone: true }"
          [disabled]="disabled()">
      </mat-form-field>
      <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Ballaststoffe (g, optional)</mat-label>
        <input matInput type="number" min="0" step="0.01" [ngModel]="profile().fiberGrams"
          (ngModelChange)="setNumber('fiberGrams', $event)" [ngModelOptions]="{ standalone: true }"
          [disabled]="disabled()">
      </mat-form-field>
    </div>
    <div class="nutrition-source">
      <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Quelle</mat-label>
        <mat-select [ngModel]="profile().sourceType" (ngModelChange)="setSourceType($event)"
          [ngModelOptions]="{ standalone: true }" [disabled]="disabled()">
          @for (source of sourceTypes; track source.value) {
            <mat-option [value]="source.value">{{ source.label }}</mat-option>
          }
        </mat-select>
      </mat-form-field>
      <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Quellenangabe</mat-label>
        <input matInput [ngModel]="profile().sourceReference" (ngModelChange)="setSourceReference($event)"
          [ngModelOptions]="{ standalone: true }"
          maxlength="500" [disabled]="disabled()" placeholder="z. B. Herstelleretikett">
      </mat-form-field>
      <mat-form-field appearance="outline" subscriptSizing="dynamic"><mat-label>Stand der Angaben</mat-label>
        <input matInput type="date" [ngModel]="profile().referenceDate" (ngModelChange)="setReferenceDate($event)"
          [ngModelOptions]="{ standalone: true }"
          [disabled]="disabled()">
      </mat-form-field>
    </div>
    <mat-checkbox [checked]="profile().reviewState === reviewedState"
      (change)="profile().reviewState = $event.checked ? reviewedState : unreviewedState"
      [disabled]="disabled() || !coreValuesComplete() || !profile().sourceReference.trim()">
      Nährwertangaben vollständig geprüft
    </mat-checkbox>
    @if (!isPlausible()) {
      <p class="nutrition-warning">Alle Werte müssen mindestens 0 sein. Gesättigte Fettsäuren dürfen Fett und Zucker darf Kohlenhydrate nicht überschreiten.</p>
    } @else if (!coreValuesComplete()) {
      <p class="nutrition-info">Fehlende Werte bleiben unbekannt. Für den Prüfstatus müssen alle Kernwerte ausgefüllt sein.</p>
    } @else if (!profile().sourceReference.trim()) {
      <p class="nutrition-info">Bitte eine nachvollziehbare Quellenangabe ergänzen.</p>
    }
  `,
  styles: `
    :host { display: grid; gap: .7rem; }
    .nutrition-reference { display: flex; flex-wrap: wrap; align-items: baseline; gap: .4rem .8rem; }
    .nutrition-reference span, .nutrition-info { color: #667168; font-size: .84rem; }
    .nutrition-values { display: grid; grid-template-columns: repeat(4, minmax(8rem, 1fr)); gap: .6rem; }
    .nutrition-source { display: grid; grid-template-columns: minmax(10rem, .8fr) minmax(14rem, 1.5fr) minmax(10rem, .8fr); gap: .6rem; }
    p { margin: 0; }
    .nutrition-warning { color: #8b2525; font-size: .84rem; }
    @media (max-width: 900px) { .nutrition-values { grid-template-columns: repeat(2, minmax(8rem, 1fr)); }
      .nutrition-source { grid-template-columns: 1fr; } }
    @media (max-width: 480px) { .nutrition-values { grid-template-columns: 1fr; } }
  `
})
export class IngredientNutritionEditorComponent {
  readonly profile = input.required<IngredientNutritionProfileItem>();
  readonly units = input.required<MeasurementUnitReference[]>();
  readonly disabled = input(false);
  readonly reviewedState = IngredientNutritionReviewState.Reviewed;
  readonly unreviewedState = IngredientNutritionReviewState.Unreviewed;
  readonly sourceTypes = [
    { value: IngredientNutritionSourceType.Manufacturer, label: 'Herstellerangabe' },
    { value: IngredientNutritionSourceType.OfficialDatabase, label: 'Offizielle Lebensmitteldatenbank' },
    { value: IngredientNutritionSourceType.ManualEstimate, label: 'Manuelle Schätzung' }
  ] as const;

  unitSymbol() {
    return this.units().find(value => value.id === this.profile().referenceUnitId)?.symbol ?? '?';
  }

  kilocalories() {
    return Math.round((this.profile().energyKilojoules ?? 0) / 4.184);
  }

  numberOrNull(value: number | string | null) {
    if (value === null || value === '') return null;
    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : null;
  }

  setNumber(
    field: 'energyKilojoules' | 'fatGrams' | 'saturatedFatGrams' | 'carbohydrateGrams' |
      'sugarsGrams' | 'proteinGrams' | 'saltGrams' | 'fiberGrams',
    value: number | string | null)
  {
    const parsed = this.numberOrNull(value);
    if (this.profile()[field] === parsed) return;
    this.profile()[field] = parsed;
    this.markUnreviewed();
  }

  setSourceType(value: IngredientNutritionSourceType) {
    if (this.profile().sourceType === value) return;
    this.profile().sourceType = value;
    this.markUnreviewed();
  }

  setSourceReference(value: string) {
    if (this.profile().sourceReference === value) return;
    this.profile().sourceReference = value;
    this.markUnreviewed();
  }

  setReferenceDate(value: string | null) {
    if (this.profile().referenceDate === value) return;
    this.profile().referenceDate = value;
    this.markUnreviewed();
  }

  coreValuesComplete() {
    const value = this.profile();
    return [value.energyKilojoules, value.fatGrams, value.saturatedFatGrams,
      value.carbohydrateGrams, value.sugarsGrams, value.proteinGrams, value.saltGrams]
      .every(item => item !== null && Number.isFinite(item));
  }

  isPlausible() {
    const value = this.profile();
    const numbers = [value.energyKilojoules, value.fatGrams, value.saturatedFatGrams,
      value.carbohydrateGrams, value.sugarsGrams, value.proteinGrams, value.saltGrams, value.fiberGrams];
    return numbers.every(item => item === null || Number.isFinite(item) && item >= 0) &&
      (value.saturatedFatGrams === null || value.fatGrams === null || value.saturatedFatGrams <= value.fatGrams) &&
      (value.sugarsGrams === null || value.carbohydrateGrams === null || value.sugarsGrams <= value.carbohydrateGrams);
  }

  private markUnreviewed() {
    this.profile().reviewState = IngredientNutritionReviewState.Unreviewed;
  }
}
