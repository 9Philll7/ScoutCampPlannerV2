import { Component, computed, effect, ElementRef, inject, input, output, signal, viewChild } from '@angular/core';
import { IngredientCatalogEntry } from '../camp/camp-api.service';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { catchError, from, mergeMap, of, toArray } from 'rxjs';
import { ActionIconComponent } from '../../shared/action-icon.component';
import { ListPaginationComponent } from '../../shared/list-pagination.component';
import { IngredientEditorReferenceData, IngredientRevisionApiService, IngredientRevisionDetails,
  IngredientRevisionPropertyItem, IngredientRevisionSummary, IngredientVariantRevisionItem } from './ingredient-revision-api.service';

interface IngredientCatalogRow {
  key: string;
  name: string;
  revision: IngredientRevisionSummary | null;
  entry: IngredientCatalogEntry | null;
  scope: string;
  details: IngredientRevisionDetails | null;
  variant: (Pick<IngredientVariantRevisionItem, 'id' | 'name'> & Partial<IngredientVariantRevisionItem>) | null;
}
interface CatalogProperty { label: string; state: number; }

@Component({
  selector: 'scp-ingredient-catalog',
  imports: [FormsModule, MatButtonModule, ActionIconComponent, ListPaginationComponent],
  templateUrl: './ingredient-catalog.component.html',
  styleUrl: './ingredient-catalog.component.scss'
})
export class IngredientCatalogComponent {
  readonly revisions = input.required<IngredientRevisionSummary[]>();
  readonly reference = input<IngredientEditorReferenceData | null>(null);
  readonly disabled = input(false);
  readonly scopeLabel = input('Lager');
  readonly available = input<IngredientCatalogEntry[]>([]);
  readonly fork = output<string>();
  readonly scopeFilter = signal('');
  readonly preview = signal<IngredientCatalogRow | null>(null);
  private readonly previewDialog = viewChild<ElementRef<HTMLDialogElement>>('previewDialog');
  scopeName(scope: string) { return scope === 'Central' ? 'Zentral' : scope === 'Tenant' ? 'Organisation' : scope === 'Camp' ? 'Lager' : scope; }
  canFork(row: IngredientCatalogRow) { return !this.disabled() && row.entry?.scope === 'Central' && !!row.entry.revisionId; }
  status(row: IngredientCatalogRow) { return row.revision ? (row.revision.state === 1 ? 'Veröffentlicht' : 'Entwurf') : row.entry?.revisionId ? 'Veröffentlicht' : 'Bestand'; }
  readonly selected = output<{ revisionId: string; variantId: string | null }>();
  readonly create = output<void>();
  readonly details = signal<IngredientRevisionDetails[]>([]);
  readonly loading = signal(false);
  readonly failed = signal(false);
  readonly query = signal('');
  readonly kind = signal('all');
  readonly category = signal('');
  readonly state = signal('');
  readonly allergen = signal('');
  readonly sort = signal('name');
  readonly grid = signal(false);
  readonly filtersOpen = signal(false);
  readonly page = signal(0);
  private readonly reload = signal(0);
  retryDetails() { this.reload.update(value => value + 1); }
  private readonly api = inject(IngredientRevisionApiService);

  constructor() {
    effect(() => { const dialog = this.previewDialog()?.nativeElement; if (dialog && !dialog.open) dialog.showModal(); });
    effect(onCleanup => {
      const revisions = this.revisions();
      this.reload();
      this.details.set([]); this.failed.set(false); this.loading.set(revisions.length > 0); this.page.set(0);
      const subscription = from(revisions).pipe(
        mergeMap(revision => this.api.get(revision.revisionId).pipe(catchError(() => { this.failed.set(true); return of(null); })), 4),
        toArray()
      ).subscribe(values => { this.details.set(values.filter((value): value is IngredientRevisionDetails => value !== null)); this.loading.set(false); });
      onCleanup(() => subscription.unsubscribe());
    });
  }

  readonly rows = computed<IngredientCatalogRow[]>(() => [...this.revisions().flatMap(revision => {
    const details = this.details().find(value => value.id === revision.revisionId) ?? null;
    const base: IngredientCatalogRow = { key: revision.revisionId, name: revision.name, revision, details, variant: null, entry: null, scope: this.scopeLabel() };
    return [base, ...(details?.variants ?? []).map(variant => ({ ...base, key: revision.revisionId + ':' + variant.id, name: variant.name, variant }))];
  }), ...this.available().filter(entry => !this.revisions().some(revision => revision.ingredientId === entry.id)).flatMap(entry => {
    const base: IngredientCatalogRow = { key: entry.id, name: entry.name, revision: null, details: null, variant: null, entry, scope: this.scopeName(entry.scope) };
    return [base, ...entry.variants.map(variant => ({ ...base, key: entry.id + ':' + variant.id, name: variant.name, variant }))];
  })]);
  readonly baseCount = computed(() => this.rows().filter(row => !row.variant).length);
  readonly variantCount = computed(() => this.rows().length - this.baseCount());
  readonly filtered = computed(() => {
    const query = this.query().trim().toLocaleLowerCase('de');
    return this.rows().filter(row =>
      (this.kind() === 'all' || (this.kind() === 'variants') === !!row.variant) &&
      (!query || (row.name + ' ' + (row.revision?.name ?? row.entry?.name ?? '') + ' ' + this.categoryName(row)).toLocaleLowerCase('de').includes(query)) &&
      (!this.scopeFilter() || row.scope === this.scopeFilter()) &&
      (!this.category() || row.details?.categoryId === this.category()) &&
      (this.state() === '' || (row.revision?.state ?? (row.entry?.revisionId ? 1 : -1)) === Number(this.state())) &&
      (!this.allergen() || (row.entry ? !row.variant && row.entry.conflicts.some(value => value.type === 'Allergen' && value.id === this.allergen()) : this.effectiveProperties(row.details?.allergens ?? [], row.variant?.allergenOverrides ?? [])
        .some(value => value.propertyId === this.allergen() && [0, 2].includes(value.state)))))
      .sort((a, b) => this.compare(a, b));
  });
  readonly visibleRows = computed(() => {
    const start = Math.min(this.page(), Math.max(0, Math.ceil(this.filtered().length / 10) - 1)) * 10;
    return this.filtered().slice(start, start + 10);
  });
  changeKind(value: string) { this.kind.set(value); this.page.set(0); }
  resetFilters() { this.scopeFilter.set(''); this.category.set(''); this.state.set(''); this.allergen.set(''); this.page.set(0); }
  open(row: IngredientCatalogRow) { if (row.revision) this.selected.emit({ revisionId: row.revision.revisionId, variantId: row.variant?.id ?? null }); else this.preview.set(row); }
  categoryName(row: IngredientCatalogRow) { return this.reference()?.categories.find(value => value.id === row.details?.categoryId)?.name ?? (this.loading() ? 'Wird geladen' : '—'); }
  private compare(a: IngredientCatalogRow, b: IngredientCatalogRow) {
    const name = a.name.localeCompare(b.name, 'de', { numeric: true, sensitivity: 'base' });
    if (this.sort() === 'name-desc') return -name;
    if (this.sort() === 'category') return this.categoryName(a).localeCompare(this.categoryName(b), 'de') || name;
    if (this.sort() === 'state') return (a.revision?.state ?? 1) - (b.revision?.state ?? 1) || name;
    return name;
  }
  private effectiveProperties(base: IngredientRevisionPropertyItem[], overrides: IngredientRevisionPropertyItem[]) {
    return [...new Map([...base, ...overrides].map(value => [value.propertyId, value])).values()];
  }
  allergens(row: IngredientCatalogRow): CatalogProperty[] {
    if (row.entry) return row.variant ? [] : row.entry.conflicts.filter(value => value.type === 'Allergen').map(value => ({ label: value.name, state: 2 }));
    const definitions = this.reference()?.allergens.filter(value => value.isEuMajorAllergen) ?? [];
    const values = this.effectiveProperties(row.details?.allergens ?? [], row.variant?.allergenOverrides ?? []);
    return values.filter(value => [0, 2].includes(value.state) && definitions.some(item => item.id === value.propertyId))
      .map(value => ({ label: definitions.find(item => item.id === value.propertyId)!.name, state: value.state }));
  }
  intolerances(row: IngredientCatalogRow): CatalogProperty[] {
    if (row.entry) return row.variant ? [] : row.entry.conflicts.filter(value => value.type === 'Intolerance').map(value => ({ label: value.name, state: 2 }));
    const definitions = this.reference()?.intolerances ?? [];
    const overrides = row.variant?.intoleranceOverrides ?? [];
    const values = this.effectiveProperties(row.details?.intolerances ?? [], overrides);
    const qualitative = values.filter(value => [0, 2].includes(value.state) || value.state === 1 && overrides.some(item => item.propertyId === value.propertyId))
      .map(value => ({ label: definitions.find(item => item.id === value.propertyId)?.name ?? 'Merkmal', state: value.state }));
    const contents = new Map((row.details?.substanceContents ?? []).map(value => [value.substanceId, value]));
    for (const override of row.variant?.substanceContentOverrides ?? []) contents.set(override.substanceId, override);
    for (const value of contents.values()) {
      if (overrides.some(item => item.propertyId === value.substanceId)) continue;
      qualitative.push({ label: (definitions.find(item => item.id === value.substanceId)?.name ?? 'Stoffgehalt') + ' ' +
        new Intl.NumberFormat('de-DE', { maximumFractionDigits: 3 }).format(value.amount) + ' ' +
        (this.reference()?.units.find(item => item.id === value.amountUnitId)?.symbol ?? '') + ' / ' + value.referenceQuantity + ' ' +
        (this.reference()?.units.find(item => item.id === value.referenceUnitId)?.symbol ?? ''), state: value.amount > 0 ? 0 : 1 });
    }
    return qualitative;
  }
  propertyLabels(values: CatalogProperty[]) {
    return values.map(value => value.label + (value.state === 1 ? ' ausgeschlossen' : value.state === 2 ? ' (enthalten oder möglich)' : '')).join(', ');
  }
  origin(row: IngredientCatalogRow) {
    if (row.entry) return row.variant ? '—' : row.entry.originInformation ?? '—';
    const values = this.effectiveProperties(row.details?.origins ?? [], row.variant?.originOverrides ?? []);
    return values.filter(value => value.state === 0).map(value => this.reference()?.origins.find(item => item.id === value.propertyId)?.name).filter(Boolean).join(', ') || '—';
  }
  propertySummary(row: IngredientCatalogRow, kind: 'allergens' | 'intolerances') {
    if (row.entry) return 'Keine Detailangabe';
    if (!row.details) return this.loading() ? 'Wird geladen' : 'Nicht geladen';
    const values = kind === 'allergens' ? this.effectiveProperties(row.details.allergens, row.variant?.allergenOverrides ?? [])
      : this.effectiveProperties(row.details.intolerances, row.variant?.intoleranceOverrides ?? []);
    return values.some(value => value.state === 3) || !values.length ? 'Angaben offen' : 'Keine enthaltenen angegeben';
  }
}
