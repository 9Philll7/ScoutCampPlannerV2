import { TestBed } from '@angular/core/testing';
import { IngredientRevisionEditorComponent } from './ingredient-revision-editor.component';
import { IngredientRevisionApiService, IngredientRevisionDetails, IngredientPropertyState } from './ingredient-revision-api.service';

describe('Ingredient substance modes', () => {
  let component: IngredientRevisionEditorComponent;
  let revision: IngredientRevisionDetails;
  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [IngredientRevisionEditorComponent],
      providers: [{ provide: IngredientRevisionApiService, useValue: {} }] });
    component = TestBed.createComponent(IngredientRevisionEditorComponent).componentInstance;
    component.referenceData.set({ categories: [], allergens: [], origins: [], intolerances: [],
      units: [{ id: 'g', name: 'Gramm', symbol: 'g', dimension: 0, baseUnitFactor: 1 }] });
    revision = { id: 'revision', ingredientId: 'ingredient', scopeType: 0, scopeId: null,
      name: 'Test', categoryId: 'category', baseUnitId: 'g', state: 0, rowVersion: 0,
      allergenReviewState: 0, intoleranceReviewState: 0, originReviewState: 0,
      allergens: [], intolerances: [], origins: [], unitConversions: [], variants: [],
      nutritionProfile: null, substanceContents: [], sourceSummary: 'Testquelle' };
  });
  it('does not treat missing data as absence', () => {
    expect(component.substanceMode(revision, 'lactose')).toBe('unknown');
    expect(revision.substanceContents).toEqual([]);
  });
  it('replaces rather than combines quantitative and qualitative input', () => {
    component.setSubstanceMode(revision, 'lactose', 'quantitative');
    expect(Number.isNaN(revision.substanceContents[0].amount)).toBe(true);
    revision.substanceContents[0].amount = 4.8;
    component.setSubstanceMode(revision, 'lactose', 'absent');
    expect(revision.substanceContents).toEqual([]);
    expect(revision.intolerances[0].state).toBe(IngredientPropertyState.DoesNotContain);
    component.setSubstanceMode(revision, 'lactose', 'quantitative');
    expect(revision.intolerances).toEqual([]);
    expect(revision.substanceContents).toHaveLength(1);
    component.setSubstanceMode(revision, 'lactose', 'unknown');
    expect(revision.substanceContents).toEqual([]);
    expect(component.substanceMode(revision, 'lactose')).toBe('unknown');
  });
  it('distinguishes an explicit unknown variant from inheritance and does not change the base', () => {
    const variant = { id: 'variant', variantKey: 'test', name: 'Test', isActive: true, sortOrder: 0,
      allergenOverrides: [], intoleranceOverrides: [], originOverrides: [], unitConversionOverrides: [],
      nutritionProfile: null, substanceContentOverrides: [] };
    component.setSubstanceMode(revision, 'lactose', 'quantitative');
    revision.substanceContents[0].amount = 4.8;
    expect(component.variantSubstanceMode(variant, 'lactose')).toBe('inherit');
    component.setVariantSubstanceMode(revision, variant, 'lactose', 'unknown');
    expect(component.variantSubstanceMode(variant, 'lactose')).toBe('unknown');
    component.setVariantSubstanceMode(revision, variant, 'lactose', 'absent');
    expect(component.variantSubstanceMode(variant, 'lactose')).toBe('absent');
    expect(revision.substanceContents[0].amount).toBe(4.8);
    component.setVariantSubstanceMode(revision, variant, 'lactose', 'quantitative');
    expect(variant.intoleranceOverrides).toEqual([]);
    expect(variant.substanceContentOverrides).toHaveLength(1);
    component.setVariantSubstanceMode(revision, variant, 'lactose', 'inherit');
    expect(variant.intoleranceOverrides).toEqual([]);
    expect(variant.substanceContentOverrides).toEqual([]);
  });
});
