import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { of } from 'rxjs';
import { afterEach, beforeEach, vi } from 'vitest';

// jsdom does not implement the native dialog top layer; interaction is checked in the browser.
let originalShowModal: PropertyDescriptor | undefined;
beforeEach(() => { originalShowModal = Object.getOwnPropertyDescriptor(HTMLDialogElement.prototype, 'showModal'); Object.defineProperty(HTMLDialogElement.prototype, 'showModal', { configurable: true, writable: true, value: function(this: HTMLDialogElement) { this.open = true; } }); });
afterEach(() => { if (originalShowModal) Object.defineProperty(HTMLDialogElement.prototype, 'showModal', originalShowModal); else Reflect.deleteProperty(HTMLDialogElement.prototype, 'showModal'); });
import { RecipeEditorComponent } from './recipe-editor.component';
import { RecipeEditorApiService, RecipeEditorDraft } from './recipe-editor-api.service';

describe('Recipe scaling editor', () => {
  it('shows and saves stepwise parameters through the existing draft API', async () => {
    const draft: RecipeEditorDraft = { id: 'recipe', campId: 'camp', status: 0, draftVersion: 0, ingredientReferences: [],
      content: { name: 'TEST', description: null, source: null, internalNotes: null, recipeType: 0,
        referenceServings: 1, referenceQuantity: null, referenceUnitId: null, defaultAgeGroupScalingApplies: false,
        authoringStage: null, tags: [], groups: [], subrecipePositions: [], ingredientPositions: [{ id: 'position',
          groupId: null, ingredientRevisionId: null, quantity: 10, unitId: null, sortOrder: 0, scalingMode: 2,
          ageGroupScaling: 2, stepSize: 3, quantityPerStep: 30, replacements: [] }] } };
    const api = { list: vi.fn(() => of([])), ingredients: vi.fn(() => of([])),
      save: vi.fn(() => of(draft)), nutrition: vi.fn(() => of(null)) };
    TestBed.configureTestingModule({ imports: [RecipeEditorComponent], providers: [{ provide: RecipeEditorApiService, useValue: api }] });
    const fixture = TestBed.createComponent(RecipeEditorComponent);
    fixture.componentRef.setInput('campId', 'camp'); fixture.detectChanges();
    fixture.componentInstance.selected.set(draft); fixture.componentInstance.beginEdit(); fixture.componentInstance.activeTab.set('ingredients'); fixture.detectChanges(); await fixture.whenStable();
    fixture.componentInstance.openPositionDialog('scaling', draft.content.ingredientPositions[0]); fixture.detectChanges(); await fixture.whenStable();
    const step = fixture.debugElement.query(By.css('input[name="dialogStepSize"]'));
    expect(step).toBeTruthy();
    step.nativeElement.value = '5'; step.nativeElement.dispatchEvent(new Event('input'));
    fixture.detectChanges(); await fixture.whenStable();
    expect(draft.content.ingredientPositions[0].stepSize).toBe(3);
    fixture.componentInstance.applyIngredientDialog(); fixture.detectChanges();
    expect(draft.content.ingredientPositions[0].stepSize).toBe(5);
    fixture.componentInstance.save();
    expect(api.save).toHaveBeenCalledWith('camp', expect.objectContaining({ content: expect.objectContaining({
      ingredientPositions: [expect.objectContaining({ scalingMode: 2, stepSize: 5, quantityPerStep: 30 })] }) }));
    draft.content.ingredientPositions[0].scalingMode = 1;
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    expect(fixture.debugElement.query(By.css('input[name="dialogStepSize"]'))).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Fixe Menge');
  });
  function previewComponent() {
    const api = { list: () => of([]), ingredients: () => of([]) };
    TestBed.configureTestingModule({ imports: [RecipeEditorComponent], providers: [{ provide: RecipeEditorApiService, useValue: api }] });
    const fixture = TestBed.createComponent(RecipeEditorComponent); fixture.componentRef.setInput('campId', 'camp'); fixture.detectChanges();
    return fixture.componentInstance;
  }
  it('explains full steps with a readable range table and German decimal formatting', () => {
    const component = previewComponent();
    component.scalingDraft.set({ scalingMode: 2, stepSize: 5, quantityPerStep: 30 });
    expect(component.scalingLabel(2)).toBe('In festen Schritten');
    expect(component.scalingPreview()).toEqual([{ range: 'Bis 5', amount: '30' }, { range: 'Über 5 bis 10', amount: '60' }, { range: 'Über 10 bis 15', amount: '90' }]);
    component.scalingDraft.set({ scalingMode: 2, stepSize: 0.5, quantityPerStep: 0.1 });
    expect(component.scalingPreview().map(row => row.amount)).toEqual(['0,1', '0,2', '0,3']);
    expect(component.scalingPreview()[2].range).toBe('Über 1 bis 1,5');
  });
  it('does not present a misleading preview for incomplete or invalid rules', () => {
    const component = previewComponent();
    for (const value of [null, 0, -1, Number.NaN, Number.POSITIVE_INFINITY]) {
      component.scalingDraft.set({ scalingMode: 2, stepSize: value, quantityPerStep: 30 });
      expect(component.validScalingDraft()).toBe(false); expect(component.scalingPreview()).toEqual([]);
      component.scalingDraft.set({ scalingMode: 2, stepSize: 5, quantityPerStep: value });
      expect(component.validScalingDraft()).toBe(false); expect(component.scalingPreview()).toEqual([]);
    }
    component.scalingDraft.set({ scalingMode: 0, stepSize: null, quantityPerStep: null });
    expect(component.validScalingDraft()).toBe(true); expect(component.scalingPreview()).toEqual([]);
  });

  it('accepts optional linear limits and rejects negative, reversed or non-finite bounds', () => {
    const component = previewComponent();
    for (const [minimumQuantity, maximumQuantity, valid] of [[null, null, true], [5, null, true], [null, 100, true], [0, 0, true], [5, 100, true], [100, 5, false], [-1, null, false], [null, -1, false], [NaN, null, false], [null, Infinity, false]] as const) {
      component.scalingDraft.set({scalingMode: 0, stepSize: null, quantityPerStep: null, minimumQuantity, maximumQuantity});
      expect(component.validScalingDraft()).toBe(valid);
    }
    component.changeDialogScalingMode(2);
    expect(component.scalingDraft()!.minimumQuantity).toBeNull();
    expect(component.scalingDraft()!.maximumQuantity).toBeNull();
  });

});
