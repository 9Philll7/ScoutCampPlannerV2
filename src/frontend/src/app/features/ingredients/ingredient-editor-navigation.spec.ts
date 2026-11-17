import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { IngredientRevisionEditorComponent } from './ingredient-revision-editor.component';
import { IngredientRevisionApiService, IngredientRevisionDetails } from './ingredient-revision-api.service';

describe('Ingredient editor navigation', () => {
  function setup() {
    const revision: IngredientRevisionDetails = {
      id: 'revision', ingredientId: 'ingredient', scopeType: 2, scopeId: 'camp',
      name: 'Butter', categoryId: 'category', baseUnitId: 'g', state: 0, rowVersion: 0,
      allergenReviewState: 0, intoleranceReviewState: 0, originReviewState: 0,
      allergens: [], intolerances: [], origins: [], unitConversions: [], variants: [],
      nutritionProfile: null, substanceContents: [], sourceSummary: ''
    };
    const api = { get: vi.fn(() => of(revision)) };
    TestBed.configureTestingModule({ imports: [IngredientRevisionEditorComponent],
      providers: [{ provide: IngredientRevisionApiService, useValue: api }] });
    const fixture = TestBed.createComponent(IngredientRevisionEditorComponent);
    const component = fixture.componentInstance;
    component.referenceData.set({ categories: [{ id: 'category', parentCategoryId: null, code: 'dairy', name: 'Milchprodukte' }],
      allergens: [], origins: [], intolerances: [], units: [{ id: 'g', name: 'Gramm', symbol: 'g', dimension: 0, baseUnitFactor: 1 }] });
    component.loading.set(false); component.open('revision'); fixture.detectChanges();
    return { fixture, component, revision, api };
  }
  afterEach(() => vi.restoreAllMocks());

  it('keeps edits across tabs and supports keyboard navigation without additional API calls', async () => {
    const { fixture, component, revision, api } = setup();
    await fixture.whenStable();
    const name = fixture.nativeElement.querySelector('[name="revisionName"]') as HTMLInputElement;
    name.value = 'Butter neu'; name.dispatchEvent(new Event('input', { bubbles: true })); fixture.detectChanges();
    await fixture.whenStable();
    component.tabKey(new KeyboardEvent('keydown', { key: 'End' }), 0); fixture.detectChanges();
    expect(component.activeTab()).toBe('sources');
    expect(document.activeElement?.id).toBe('ingredient-tab-sources');
    component.tabKey(new KeyboardEvent('keydown', { key: 'Home' }), 6); fixture.detectChanges();
    expect(revision.name).toBe('Butter neu');
    expect(fixture.nativeElement.querySelectorAll('[role="tabpanel"]:not([hidden])')).toHaveLength(1);
    expect(api.get).toHaveBeenCalledTimes(1);
  });

  it('opens a newly added variant and falls back when the selected variant is removed', () => {
    const { component, revision } = setup();
    component.addVariant(revision); const first = revision.variants[0];
    component.addVariant(revision); const second = revision.variants[1];
    expect(component.variantVisible(revision, first.id)).toBe(false);
    expect(component.variantVisible(revision, second.id)).toBe(true);
    revision.variants.pop();
    expect(component.variantVisible(revision, first.id)).toBe(true);
  });

  it('keeps conversion edits while paging and clamps the visible page after removal', async () => {
    const { fixture, component, revision } = setup();
    revision.unitConversions = Array.from({ length: 6 }, (_, index) => ({ sourceUnitId: 'unit-' + index,
      factorToBaseUnit: index + 1, factorInput: String(index + 1), precision: 0 }));
    component.activeTab.set('units'); fixture.detectChanges(); await fixture.whenStable();
    expect([...fixture.nativeElement.querySelectorAll('.unit-table .table-row')].filter((row: HTMLElement) => !row.hidden)).toHaveLength(5);
    const factor = fixture.nativeElement.querySelector('.unit-table .table-row input') as HTMLInputElement;
    factor.value = '12,5'; factor.dispatchEvent(new Event('input', { bubbles: true }));
    (fixture.nativeElement.querySelector('.compact-units scp-list-pagination button:last-child') as HTMLButtonElement).click();
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    expect(component.unitPage()).toBe(1);
    expect(revision.unitConversions[0].factorToBaseUnit).toBe(12.5);
    expect([...fixture.nativeElement.querySelectorAll('.unit-table .table-row')].filter((row: HTMLElement) => !row.hidden)).toHaveLength(1);
    const lastRow = [...fixture.nativeElement.querySelectorAll('.unit-table .table-row')].find((row: HTMLElement) => !row.hidden) as HTMLElement;
    (lastRow.querySelector('button') as HTMLButtonElement).click();
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    expect([...fixture.nativeElement.querySelectorAll('.unit-table .table-row')].filter((row: HTMLElement) => !row.hidden)).toHaveLength(5);
    expect(revision.unitConversions[0].factorInput).toBe('12,5');
  });

  it('tracks sources even without nutrition data and protects closing a changed draft', () => {
    const { component, revision } = setup();
    expect(component.hasPendingChanges()).toBe(false);
    component.newSourceEntry.set('Herstelleretikett'); component.addSourceEntry(revision);
    expect(component.hasPendingChanges()).toBe(true);
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    component.closeEditor(); expect(component.selected()).toBe(revision);
    confirm.mockReturnValue(true); component.closeEditor(); expect(component.selected()).toBeNull();
  });
});
