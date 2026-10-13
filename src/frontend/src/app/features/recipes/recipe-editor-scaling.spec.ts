import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { of } from 'rxjs';
import { vi } from 'vitest';
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
    fixture.componentInstance.selected.set(draft); fixture.detectChanges(); await fixture.whenStable();
    const step = fixture.debugElement.query(By.css('input[name="step-position"]'));
    expect(step).toBeTruthy();
    step.nativeElement.value = '5'; step.nativeElement.dispatchEvent(new Event('input'));
    fixture.detectChanges(); await fixture.whenStable();
    expect(draft.content.ingredientPositions[0].stepSize).toBe(5);
    fixture.componentInstance.save();
    expect(api.save).toHaveBeenCalledWith('camp', expect.objectContaining({ content: expect.objectContaining({
      ingredientPositions: [expect.objectContaining({ scalingMode: 2, stepSize: 5, quantityPerStep: 30 })] }) }));
    draft.content.ingredientPositions[0].scalingMode = 1;
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    expect(fixture.debugElement.query(By.css('input[name="step-position"]'))).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Fixe Menge');
  });
});
