import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { IngredientCatalogComponent } from './ingredient-catalog.component';
import { IngredientRevisionApiService, IngredientRevisionDetails } from './ingredient-revision-api.service';

function ingredient(id: string, name = id): IngredientRevisionDetails {
  return { id, ingredientId: 'ingredient-' + id, scopeType: 2, scopeId: 'camp', name, categoryId: 'category', baseUnitId: 'g', state: 1, rowVersion: 1,
    allergenReviewState: 1, intoleranceReviewState: 1, originReviewState: 1,
    allergens: [{ propertyId: 'milk', state: 0, source: 2 }], intolerances: [], origins: [], unitConversions: [], variants: [], nutritionProfile: null,
    substanceContents: [{ substanceId: 'lactose', amount: 10, amountUnitId: 'g', referenceQuantity: 100, referenceUnitId: 'g', sourceType: 2, sourceReference: 'Test', reviewState: 1 }], sourceSummary: 'Test' };
}
describe('Ingredient catalog', () => {
  async function setup(details: IngredientRevisionDetails[], failingId = '') {
    const api = { get: vi.fn((id: string) => id === failingId ? throwError(() => new Error('Unavailable')) : of(structuredClone(details.find(value => value.id === id)!))) };
    TestBed.configureTestingModule({ imports: [IngredientCatalogComponent], providers: [{ provide: IngredientRevisionApiService, useValue: api }] });
    const fixture = TestBed.createComponent(IngredientCatalogComponent);
    fixture.componentRef.setInput('revisions', details.map(value => ({ ingredientId: value.ingredientId, revisionId: value.id, name: value.name, state: value.state, rowVersion: value.rowVersion })));
    fixture.componentRef.setInput('reference', { categories: [{ id: 'category', name: 'Milchprodukte', code: 'dairy', parentCategoryId: null }],
      allergens: [{ id: 'milk', code: 'MILK', name: 'Milch', parentAllergenId: null, isEuMajorAllergen: true }],
      intolerances: [{ id: 'lactose', code: 'LACTOSE', name: 'Laktose', isQuantityDependent: true }], origins: [],
      units: [{ id: 'g', name: 'Gramm', symbol: 'g', dimension: 0, baseUnitFactor: 1 }] });
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, api };
  }
  it('shows effective variant properties and opens the variant in its base revision', async () => {
    const base = ingredient('butter', 'Butter');
    base.variants = [{ id: 'free', variantKey: 'free', name: 'Butter laktosefrei', isActive: true, sortOrder: 0,
      allergenOverrides: [], intoleranceOverrides: [{ propertyId: 'lactose', state: 1, source: 2 }], originOverrides: [], unitConversionOverrides: [], nutritionProfile: null, substanceContentOverrides: [] }];
    const { fixture, component } = await setup([base]);
    expect(component.rows()).toHaveLength(2); expect(component.variantCount()).toBe(1);
    const variant = component.rows().find(value => value.variant)!;
    expect(component.allergens(variant)).toEqual([{ label: 'Milch', state: 0 }]);
    expect(component.intolerances(variant)).toEqual([{ label: 'Laktose', state: 1 }]);
    component.changeKind('variants'); component.query.set('laktosefrei'); fixture.detectChanges();
    const opened = vi.fn(); component.selected.subscribe(opened);
    (fixture.nativeElement.querySelector('.name-button') as HTMLButtonElement).click();
    expect(opened).toHaveBeenCalledWith({ revisionId: 'butter', variantId: 'free' });
    expect(component.filtered()).toHaveLength(1);
  });
  it('paginates ten entries and resets the page when the type changes', async () => {
    const { fixture, component } = await setup(Array.from({ length: 11 }, (_, index) => ingredient('i' + index)));
    expect(component.visibleRows()).toHaveLength(10);
    (fixture.nativeElement.querySelector('scp-list-pagination button:last-child') as HTMLButtonElement).click(); fixture.detectChanges();
    expect(component.visibleRows()).toHaveLength(1);
    component.changeKind('variants'); expect(component.page()).toBe(0); expect(component.filtered()).toHaveLength(0);
  });
  it('keeps a failed detail request visible without claiming complete data', async () => {
    const { component, fixture, api } = await setup([ingredient('broken')], 'broken');
    expect(component.failed()).toBe(true); expect(component.loading()).toBe(false);
    expect(component.rows()).toHaveLength(1); expect(component.propertySummary(component.rows()[0], 'allergens')).toBe('Nicht geladen');
    api.get.mockImplementation(() => of(ingredient('broken')));
    (fixture.nativeElement.querySelector('.catalog-feedback button') as HTMLButtonElement).click();
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    expect(component.failed()).toBe(false); expect(component.rows()[0].details?.id).toBe('broken');
  });
  it('merges available scopes without duplicating local revisions and limits forks to central revisions', async () => {
    const { fixture, component, api } = await setup([ingredient('local')]);
    const entry = { id: 'central', revisionId: 'published', name: 'Central', scope: 'Central', scopeId: null, originInformation: null,
      variants: [], units: [], conflicts: [{ type: 'Allergen', id: 'milk', name: 'Milch' }] };
    fixture.componentRef.setInput('available', [entry,
      { ...entry, id: 'tenant', scope: 'Tenant' },
      { ...entry, id: 'legacy', revisionId: null },
      { ...entry, id: 'ingredient-local', revisionId: 'local', scope: 'Camp' }]);
    fixture.detectChanges(); await fixture.whenStable();
    expect(component.baseCount()).toBe(4);
    expect(api.get).toHaveBeenCalledTimes(1);
    component.scopeFilter.set('Zentral'); component.allergen.set('milk');
    expect(component.filtered()).toHaveLength(2);
    const central = component.filtered().find(row => row.entry?.revisionId)!;
    expect(component.canFork(central)).toBe(true);
    expect(component.canFork(component.rows().find(row => row.entry?.scope === 'Tenant')!)).toBe(false);
    expect(component.status(component.rows().find(row => row.entry?.id === 'legacy')!)).toBe('Bestand');
    fixture.componentRef.setInput('disabled', true); fixture.detectChanges();
    expect(component.canFork(central)).toBe(false);
    component.resetFilters(); expect(component.filtered()).toHaveLength(4);
  });

});
