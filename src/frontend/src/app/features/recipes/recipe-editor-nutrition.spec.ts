import { TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { HttpErrorResponse } from '@angular/common/http';
import { vi } from 'vitest';
import { RecipeEditorComponent } from './recipe-editor.component';
import { RecipeEditorApiService, RecipeEditorDraft, RecipeNutritionCalculation } from './recipe-editor-api.service';
const values = { energyKilojoules: 1958.112, energyKilocalories: 468, fatGrams: 22.4, saturatedFatGrams: 10.8, carbohydrateGrams: 49.8, sugarsGrams: 16.2, proteinGrams: 18.1, saltGrams: 0.8, fiberGrams: null };
const complete = (): RecipeNutritionCalculation => ({ isComplete: true, perStandardPortion: values, total: { ...values, energyKilocalories: 4680, energyKilojoules: 19581.12 }, missingContributions: [] });
const draft = (id = 'recipe'): RecipeEditorDraft => ({ id, campId: 'camp', status: 1, draftVersion: 0, ingredientReferences: [{ revisionId: 'ingredient-r1', ingredientId: 'ingredient', revisionNumber: 2, name: 'Milch', scope: 2, units: [], conflicts: [], availableUpdate: null }], content: {
 name: 'Pfannkuchen', description: null, source: null, internalNotes: null, recipeType: 0, referenceServings: 10, referenceQuantity: null, referenceUnitId: null, defaultAgeGroupScalingApplies: false, authoringStage: null, tags: [], groups: [], ingredientPositions: [], subrecipePositions: [] } });
async function setup(result: RecipeNutritionCalculation | null = complete()) {
 const api = { list: () => of([]), ingredients: () => of([]), get: vi.fn((_camp: string, id: string) => of(draft(id))), nutrition: vi.fn(() => of(result)) };
 TestBed.configureTestingModule({ imports: [RecipeEditorComponent], providers: [{ provide: RecipeEditorApiService, useValue: api }] });
 const fixture = TestBed.createComponent(RecipeEditorComponent); fixture.componentRef.setInput('campId', 'camp'); fixture.detectChanges();
 const entry = { libraryEntryId: null, recipeId: 'recipe', revisionId: 'r1', revisionNumber: 1, name: 'Pfannkuchen', scope: 2, status: 1, isLocal: true, updatedAtUtc: '' };
 fixture.componentInstance.open(entry); fixture.componentInstance.activeTab.set('nutrition'); fixture.detectChanges(); await fixture.whenStable();
 return { fixture, component: fixture.componentInstance, api, entry, panel: () => fixture.nativeElement.querySelector('#recipe-panel-nutrition') as HTMLElement };
}
describe('Recipe nutrition overview', () => {
 it('shows energy cards and both columns without editable nutrient fields', async () => {
  const { component, panel } = await setup();
  expect(panel().textContent).toContain('468 kcal'); expect(panel().textContent).toContain('4.680 kcal');
  expect(panel().querySelectorAll('.nutrition-detail-table tbody tr')).toHaveLength(8);
  expect(panel().querySelectorAll('input,select,textarea')).toHaveLength(0);
  expect(component.nutritionCell('fiberGrams', 'total')).toBe('—'); expect(panel().textContent).toContain('Revision 2');
 });
 it('groups missing profiles by revision and reason without inventing partial totals', async () => {
  const missing = { ingredientRevisionId: 'ingredient-r1', ingredientName: 'Milch', recipeRevisionPath: ['r1'], positionId: 'p1', reason: 0 };
  const { panel, component } = await setup({ isComplete: false, total: null, perStandardPortion: null, missingContributions: [missing, { ...missing, positionId: 'p2' }] });
  expect(component.nutritionEnergy('total')).toBe('—'); expect(panel().textContent).toContain('Unvollständige Nährwertdaten');
  expect(panel().querySelectorAll('.nutrition-missing-details li')).toHaveLength(1); expect(panel().textContent).toContain('2 Verwendungen'); expect(panel().textContent).not.toContain('468 kcal');
 });
 it('displays available sums as an explicitly incomplete intermediate result', async () => {
  const missing = { ingredientRevisionId: 'ingredient-r1', ingredientName: 'Milch', recipeRevisionPath: ['r1'], positionId: 'p1', reason: 0 };
  const {component, panel} = await setup({...complete(), isComplete:false, missingContributions:[missing]});
  expect(component.nutritionEnergy('portion')).toBe('468 kcal');
  expect(panel().querySelectorAll('.nutrition-status.incomplete')).toHaveLength(1);
  expect(panel().querySelector('#nutrition-heading')!.textContent).toBe('Nährwertübersicht');
  expect(panel().querySelector('.nutrition-energy-cards')!.classList.contains('nutrition-partial')).toBe(false);
  expect(panel().textContent).not.toContain('Zwischenstand · unvollständig');
  expect(panel().textContent).toContain('Fehlende Beiträge sind noch nicht eingerechnet');
  expect(panel().querySelectorAll('.nutrition-missing-details li')).toHaveLength(1);
 });
 it('hides saved results during pending edits and restores them on cancellation', async () => {
  const { component, fixture, panel, api } = await setup(); component.beginEdit(); component.selected()!.content.referenceServings = 20; fixture.detectChanges();
  expect(panel().textContent).toContain('Ungespeicherte Änderungen'); expect(component.nutritionEnergy('portion')).toBe('—'); expect(component.nutritionReferenceServings()).toBe(10);
  component.loadNutrition(); expect(api.nutrition).toHaveBeenCalledTimes(1); component.cancelEdit(); fixture.detectChanges(); expect(component.nutritionEnergy('portion')).toBe('468 kcal');
 });
 it('normalizes standard portions using the authoring-stage factor', async () => {
  const { component, api, entry } = await setup(); api.get.mockReturnValue(of({ ...draft(), content: { ...draft().content, authoringStage: { stageId: 'stage', stageName: 'Kinder', factor: 0.8 } } }));
  component.open(entry); expect(component.nutritionReferenceServings()).toBe(8);
 });
 it('ignores late responses for another recipe and starts the new request immediately', async () => {
  const { component, api, entry } = await setup(); const first = new Subject<RecipeNutritionCalculation>(); const second = new Subject<RecipeNutritionCalculation>();
  api.nutrition.mockReturnValueOnce(first).mockReturnValueOnce(second); component.loadNutrition(); component.open({ ...entry, recipeId: 'other' });
  first.next(complete()); expect(component.nutrition()).toBeNull(); expect(component.nutritionLoading()).toBe(true);
  second.next({ isComplete: false, total: null, perStandardPortion: null, missingContributions: [] }); expect(component.nutritionLoading()).toBe(false); expect(component.nutrition()?.isComplete).toBe(false);
 });
 it('shows recoverable calculation errors instead of old values', async () => {
  const { component, api, fixture, panel } = await setup(); api.nutrition.mockReturnValue(throwError(() => new HttpErrorResponse({status: 422}))); component.loadNutrition(); fixture.detectChanges();
  expect(panel().textContent).toContain('Keine Auswertung verfügbar'); expect(panel().textContent).toContain('Ergänze zuerst Standardportionen'); expect(component.nutritionEnergy('total')).toBe('—'); expect(component.nutritionLoading()).toBe(false);
 });
});
