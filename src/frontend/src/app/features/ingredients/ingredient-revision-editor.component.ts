import { HttpErrorResponse } from '@angular/common/http';
import { Component, effect, inject, input, signal } from '@angular/core';
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
  IngredientPropertyReviewState,
  IngredientRevisionApiService,
  IngredientRevisionDetails,
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
              @for (unit of referenceData()?.units ?? []; track unit.id) {
                <mat-option [value]="unit.id">{{ unit.name }} ({{ unit.symbol }})</mat-option>
              }
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
            <mat-select name="revisionUnit" [(ngModel)]="revision.baseUnitId"
              [disabled]="revision.state === publishedState || disabled()">
              @for (unit of referenceData()?.units ?? []; track unit.id) {
                <mat-option [value]="unit.id">{{ unit.name }} ({{ unit.symbol }})</mat-option>
              }
            </mat-select>
          </mat-form-field>
          @if (revision.state === draftState) {
            <fieldset class="review-checks" [disabled]="disabled()">
              <legend>Fachliche Prüfung</legend>
              <mat-checkbox [checked]="isReviewed(revision.allergenReviewState)"
                (change)="revision.allergenReviewState = reviewState($event.checked)">Allergenangaben geprüft</mat-checkbox>
              <mat-checkbox [checked]="isReviewed(revision.intoleranceReviewState)"
                (change)="revision.intoleranceReviewState = reviewState($event.checked)">Unverträglichkeiten geprüft</mat-checkbox>
              <mat-checkbox [checked]="isReviewed(revision.originReviewState)"
                (change)="revision.originReviewState = reviewState($event.checked)">Herkunftsangaben geprüft</mat-checkbox>
            </fieldset>
            <p class="revision-hint">„Geprüft“ bedeutet: Auch fehlende Einträge wurden bewusst kontrolliert.</p>
            <div class="revision-actions">
              <button matButton type="submit" [disabled]="submitting() || disabled() || !isDirty(revision)">
                <scp-action-icon name="save"/>Entwurf speichern</button>
              <button matButton="filled" type="button" (click)="publish()"
                [disabled]="submitting() || disabled() || isDirty(revision) || !allReviewed(revision)">Veröffentlichen</button>
            </div>
            @if (isDirty(revision) && allReviewed(revision)) {
              <p class="revision-hint revision-unsaved">Vor dem Veröffentlichen muss der aktuelle Entwurf gespeichert werden.</p>
            }
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
    .revision-form h4, .revision-form .revision-editor-heading, .review-checks, .revision-hint, .revision-actions { grid-column: 1 / -1; }
    .revision-form mat-form-field:first-of-type { grid-column: 1 / -1; }
    .review-checks { display: flex; flex-wrap: wrap; gap: .4rem 1rem; border: 1px solid #d7e1d5; border-radius: .65rem; }
    .review-checks legend { color: #465249; font-weight: 600; }
    .revision-hint { color: #58635a; font-size: .85rem; }
    .revision-actions { display: flex; justify-content: flex-end; gap: .4rem; }
    .revision-state { padding: .25rem .55rem; border-radius: 999px; background: #f1e6d5; color: #674a1d; font-size: .78rem; font-weight: 700; }
    .revision-state.published { background: #dcebdd; color: #214b28; }
    .revision-empty { padding: 1rem; border: 1px dashed #b8c4b7; border-radius: .7rem; color: #5b665c; }
    @media (max-width: 700px) { .revision-form { grid-template-columns: 1fr; } .revision-form > * { grid-column: 1 !important; } }
  `
})
export class IngredientRevisionEditorComponent {
  readonly campId = input.required<string>();
  readonly disabled = input(false);
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
  createName = '';
  createCategoryId = '';
  createBaseUnitId = '';
  private selectedSnapshot = '';
  private readonly api = inject(IngredientRevisionApiService);

  constructor() {
    effect(() => { const campId = this.campId(); if (campId) this.load(campId); });
  }

  canCreate() { return !!this.createName.trim() && !!this.createCategoryId && !!this.createBaseUnitId; }
  stateLabel(state: IngredientRevisionState) { return state === this.publishedState ? 'Veröffentlicht' : 'Entwurf'; }
  isReviewed(state: IngredientPropertyReviewState) { return state === IngredientPropertyReviewState.Reviewed; }
  reviewState(checked: boolean) { return checked ? IngredientPropertyReviewState.Reviewed : IngredientPropertyReviewState.Unreviewed; }
  allReviewed(value: IngredientRevisionDetails) { return this.isReviewed(value.allergenReviewState) &&
    this.isReviewed(value.intoleranceReviewState) && this.isReviewed(value.originReviewState); }
  isDirty(value: IngredientRevisionDetails) { return this.snapshot(value) !== this.selectedSnapshot; }

  openCreate() {
    this.createName = '';
    this.createCategoryId = this.referenceData()?.categories[0]?.id ?? '';
    this.createBaseUnitId = this.referenceData()?.units[0]?.id ?? '';
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
    this.api.get(revisionId).subscribe({ next: value => { this.selectedSnapshot = this.snapshot(value); this.selected.set(value); },
      error: () => this.error.set('Die Zutatenrevision konnte nicht geladen werden.') });
  }

  save() {
    const revision = this.selected(); if (!revision || revision.state !== this.draftState) return;
    this.submitting.set(true); this.error.set(''); this.notice.set('');
    this.api.save(revision.id, { name: revision.name, categoryId: revision.categoryId, baseUnitId: revision.baseUnitId,
      allergenReviewState: revision.allergenReviewState, intoleranceReviewState: revision.intoleranceReviewState,
      originReviewState: revision.originReviewState, expectedRowVersion: revision.rowVersion }).subscribe({
      next: result => { revision.rowVersion = result.rowVersion; this.selectedSnapshot = this.snapshot(revision);
        this.selected.set({ ...revision }); this.submitting.set(false);
        this.notice.set('Der Entwurf wurde gespeichert.'); this.refreshList(revision.id, false); },
      error: error => this.handleMutationError(error)
    });
  }

  publish() {
    const revision = this.selected(); if (!revision || !this.allReviewed(revision)) return;
    this.submitting.set(true); this.error.set(''); this.notice.set('');
    this.api.publish(revision.id, revision.rowVersion).subscribe({
      next: result => { this.submitting.set(false); revision.rowVersion = result.rowVersion;
        revision.state = this.publishedState; this.selectedSnapshot = this.snapshot(revision);
        this.selected.set({ ...revision }); this.notice.set('Die Zutat wurde veröffentlicht.');
        this.refreshList(revision.id, false); },
      error: error => this.handleMutationError(error)
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
      originReviewState: value.originReviewState });
  }
}
