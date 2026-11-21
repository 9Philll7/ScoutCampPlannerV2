import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { afterEach, beforeEach, vi } from 'vitest';

// jsdom does not implement the native dialog top layer; interaction is checked in the browser.
let originalShowModal: PropertyDescriptor | undefined;
beforeEach(() => { originalShowModal = Object.getOwnPropertyDescriptor(HTMLDialogElement.prototype, 'showModal'); Object.defineProperty(HTMLDialogElement.prototype, 'showModal', { configurable: true, writable: true, value: function(this: HTMLDialogElement) { this.open = true; } }); });
afterEach(() => { if (originalShowModal) Object.defineProperty(HTMLDialogElement.prototype, 'showModal', originalShowModal); else Reflect.deleteProperty(HTMLDialogElement.prototype, 'showModal'); });
import { RecipeEditorComponent } from './recipe-editor.component';
import { RecipeEditorApiService, RecipeEditorDraft, RecipeEditorIngredientPosition, RecipeEditorIngredientReference } from './recipe-editor-api.service';

const position = (id: string, revision: string, groupId = 'g1', order = 0): RecipeEditorIngredientPosition => ({ id, groupId, ingredientRevisionId: revision, quantity: 10, unitId: 'g', sortOrder: order, scalingMode: 0, ageGroupScaling: 2, stepSize: null, quantityPerStep: null, replacements: [] });
const reference = (revisionId: string, name: string): RecipeEditorIngredientReference => ({ revisionId, ingredientId: revisionId + '-identity', revisionNumber: 1, name, scope: 2, units: [{ unitId: 'g', name: 'Gramm', symbol: 'g', dimension: 0, baseUnitFactor: 1, referenceQuantityPerUnit: 1 }], conflicts: [], availableUpdate: null });

describe('Recipe ingredients tab', () => {
  async function setup() {
    const milk = reference('milk', 'Milch'); milk.conflicts = [{ id: 'allergen', type: 0, name: 'Milch', preventableByVariants: ['Milchvariante'] }];
    milk.availableUpdate = { revisionId: 'milk-new', revisionNumber: 2, name: 'Milch neu', units: milk.units, conflicts: [{ id: 'allergen', type: 0, name: 'Milch', preventableByVariants: [] }] };
    const oil = reference('oil', 'Öl');
    const stored: RecipeEditorDraft = { id: 'recipe', campId: 'camp', status: 1, draftVersion: 7, ingredientReferences: [milk, oil], content: {
      name: 'Teig', description: 'Test', source: null, internalNotes: 'Bewahren', recipeType: 0, referenceServings: 10, referenceQuantity: null, referenceUnitId: null, defaultAgeGroupScalingApplies: true, authoringStage: null, tags: ['tag'], groups: [{ id: 'g1', name: 'Teig', sortOrder: 0 }, { id: 'g2', name: 'Zum Backen', sortOrder: 1 }], ingredientPositions: [position('p1', 'milk'), position('p2', 'oil', 'g1', 1)], subrecipePositions: [] } };
    const entry = { libraryEntryId: null, recipeId: 'recipe', revisionId: 'published', revisionNumber: 1, name: 'Teig', scope: 2, status: 1, isLocal: true, updatedAtUtc: '' };
    const ingredients = [milk, oil].map(item => ({ id: item.ingredientId, revisionId: item.revisionId, name: item.name, scope: 'Camp' as const, scopeId: 'camp', originInformation: 'Pflanzlich', variants: [], units: item.units.map(unit => ({ ...unit, dimension: 'Mass' as const })), conflicts: item.conflicts.map(c => ({ id: c.id, name: c.name, type: 'Allergen' as const })) }));
    const api = { list: vi.fn(() => of([entry])), ingredients: vi.fn(() => of(ingredients)), get: vi.fn(() => of(structuredClone(stored))), nutrition: vi.fn(() => of(null)), save: vi.fn((_id: string, value: RecipeEditorDraft) => of(structuredClone(value))) };
    TestBed.configureTestingModule({ imports: [RecipeEditorComponent], providers: [{ provide: RecipeEditorApiService, useValue: api }] });
    const fixture = TestBed.createComponent(RecipeEditorComponent); fixture.componentRef.setInput('campId', 'camp'); fixture.detectChanges(); await fixture.whenStable();
    fixture.componentInstance.open(entry); fixture.componentInstance.activeTab.set('ingredients'); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, api, ingredients };
  }
  it('renders text rows in view mode without quantity inputs or an empty ungrouped card', async () => {
    const { fixture, component, api } = await setup();
    expect(fixture.nativeElement.querySelectorAll('.ingredients-table tbody tr')).toHaveLength(3);
    expect(fixture.nativeElement.querySelector('#recipe-panel-ingredients input')).toBeNull();
    expect(fixture.nativeElement.querySelector('.ingredient-table-group summary').textContent).toContain('Teig');
    expect(fixture.nativeElement.querySelector('#recipe-panel-ingredients').textContent).not.toContain('Ohne Gruppe');
    expect(component.hasPendingChanges()).toBe(false); expect(api.save).not.toHaveBeenCalled();
  });
  it('allows unit and quantity edits while preserving the selected revision and unrelated metadata', async () => {
    const { fixture, component, api } = await setup(); (fixture.nativeElement.querySelector('.editor-heading button') as HTMLButtonElement).click(); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    const input = fixture.nativeElement.querySelector('input[aria-label="Menge für Milch"]') as HTMLInputElement;
    input.value = '25'; input.dispatchEvent(new Event('input')); fixture.detectChanges(); await fixture.whenStable();
    component.save();
    expect(api.save).toHaveBeenCalledWith('camp', expect.objectContaining({ draftVersion: 7, content: expect.objectContaining({ internalNotes: 'Bewahren', tags: ['tag'], ingredientPositions: expect.arrayContaining([expect.objectContaining({ quantity: 25, ingredientRevisionId: 'milk', ageGroupScaling: 2 })]) }) }));
  });
  it('keeps scaling dialog changes local until apply and protects them before page unload', async () => {
    const { fixture, component } = await setup(); component.beginEdit(); component.openPositionDialog('scaling', component.selected()!.content.ingredientPositions[0]); fixture.detectChanges(); await fixture.whenStable();
    component.changeDialogScalingMode(2); component.scalingDraft()!.stepSize = 5; component.scalingDraft()!.quantityPerStep = 30;
    expect(component.changed()).toBe(false); expect(component.hasPendingChanges()).toBe(true);
    component.closeIngredientDialog(); expect(component.hasPendingChanges()).toBe(false); expect(component.selected()!.content.ingredientPositions[0].scalingMode).toBe(0);
    component.openPositionDialog('scaling', component.selected()!.content.ingredientPositions[0]); component.changeDialogScalingMode(1); component.applyIngredientDialog();
    expect(component.selected()!.content.ingredientPositions[0]).toMatchObject({ scalingMode: 1, stepSize: null, quantityPerStep: null, ageGroupScaling: 2 });
  });
  it('cancels replacement edits without touching the recipe and applies only replacement configuration', async () => {
    const { fixture, component, ingredients } = await setup(); component.beginEdit(); const original = component.selected()!.content.ingredientPositions[0];
    component.openPositionDialog('replacements', original); fixture.detectChanges(); await fixture.whenStable();
    component.addReplacement(component.replacementDraft()!, ingredients[1]);
    expect(original.replacements).toHaveLength(0); expect(component.hasPendingChanges()).toBe(true);
    component.closeIngredientDialog(); expect(component.hasPendingChanges()).toBe(false);
    component.openPositionDialog('replacements', original); component.addReplacement(component.replacementDraft()!, ingredients[1]); component.applyIngredientDialog();
    expect(original.replacements).toHaveLength(1); expect(original.replacements[0].conflicts).toEqual([{ id: 'allergen', type: 0 }]);
    expect(original.groupId).toBe('g1'); expect(original.ingredientRevisionId).toBe('milk');
    component.cancelEdit(); expect(component.selected()!.content.ingredientPositions[0].replacements).toHaveLength(0);
  });
  it('does not adopt updates when displaying them and resolves metadata after explicit adoption', async () => {
    const { fixture, component, api } = await setup(); component.openIngredientDialog('updates'); fixture.detectChanges(); await fixture.whenStable();
    expect(component.selected()!.content.ingredientPositions[0].ingredientRevisionId).toBe('milk');
    expect((fixture.nativeElement.querySelector('.update-list button') as HTMLButtonElement).disabled).toBe(true);
    component.closeIngredientDialog(); component.beginEdit(); const original = component.selected()!.content.ingredientPositions[0];
    component.adoptIngredientUpdate(original, component.ingredientReference(original)!.availableUpdate!);
    expect(original.ingredientRevisionId).toBe('milk-new'); expect(component.ingredient(original)?.name).toBe('Milch neu'); expect(component.ingredientReference(original)?.revisionNumber).toBe(2);
    expect(component.availableIngredientUpdates()).toHaveLength(0); expect(component.ingredientOrigin(original)).toBe('Keine Angabe'); expect(api.save).not.toHaveBeenCalled();
    component.cancelEdit(); expect(component.selected()!.content.ingredientPositions[0].ingredientRevisionId).toBe('milk');
  });
  it('moves groups and rows using existing ordering and discards new group dialogs', async () => {
    const { component } = await setup(); component.beginEdit(); component.openGroupEditor(null); component.groupDialogName.set('Neue Gruppe'); component.closeIngredientDialog();
    expect(component.selected()!.content.groups).toHaveLength(2); expect(component.hasPendingChanges()).toBe(false);
    component.openGroupEditor(null); component.groupDialogName.set('Sauce'); component.applyIngredientDialog(); expect(component.orderedGroups()[2].name).toBe('Sauce');
    component.moveGroup(component.orderedGroups()[2], -1); expect(component.orderedGroups().map(g => g.name)).toEqual(['Teig', 'Sauce', 'Zum Backen']);
    const rows = component.selected()!.content.ingredientPositions; component.movePosition(rows[1], -1); expect(rows[1].sortOrder).toBe(0);
    component.openPositionDialog('move', rows[0]); component.dialogTargetGroupId.set('g2'); component.applyIngredientDialog(); expect(rows[0].groupId).toBe('g2'); expect(rows[0].sortOrder).toBe(0);
    component.removeGroup(component.orderedGroups().find(g => g.id === 'g2')!); expect(component.selected()!.content.groups.some(g => g.id === 'g2')).toBe(true);
  });
});
