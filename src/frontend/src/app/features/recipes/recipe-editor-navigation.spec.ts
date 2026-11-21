import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { of, Subject } from 'rxjs';
import { vi } from 'vitest';
import { RecipeEditorComponent } from './recipe-editor.component';
import { RecipeEditorApiService, RecipeEditorDraft } from './recipe-editor-api.service';

const makeDraft = (): RecipeEditorDraft => ({ id: 'recipe', campId: 'camp', status: 1, draftVersion: 4, ingredientReferences: [], content: {
  name: 'Pfannkuchen', description: 'Teig mischen.', source: 'Lagerküche', internalNotes: 'Erhalten', recipeType: 0,
  referenceServings: 10, referenceQuantity: null, referenceUnitId: null, defaultAgeGroupScalingApplies: true,
  authoringStage: null, tags: ['existing'], groups: [], ingredientPositions: [], subrecipePositions: [] } });

describe('Recipe editor navigation and editing', () => {
  async function setup(disabled = false) {
    const stored = makeDraft();
    const entry = { libraryEntryId: null, recipeId: 'recipe', revisionId: 'published', revisionNumber: 3, name: stored.content.name!, scope: 2, status: 1, isLocal: true, updatedAtUtc: '' };
    const api = { list: vi.fn(() => of([entry])), ingredients: vi.fn(() => of([])),
      get: vi.fn(() => of(structuredClone(stored))), nutrition: vi.fn(() => of(null)),
      save: vi.fn((_camp: string, draft: RecipeEditorDraft) => of(structuredClone(draft))), publish: vi.fn(() => of({ revisionNumber: 4 })) };
    TestBed.configureTestingModule({ imports: [RecipeEditorComponent], providers: [{ provide: RecipeEditorApiService, useValue: api }] });
    const fixture = TestBed.createComponent(RecipeEditorComponent);
    fixture.componentRef.setInput('campId', 'camp'); fixture.componentRef.setInput('disabled', disabled);
    fixture.detectChanges(); await fixture.whenStable();
    fixture.componentInstance.open(entry); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, api, entry };
  }
  it('starts in read-only view and preserves edits through all four tabs until cancellation', async () => {
    const { fixture, component, api } = await setup();
    expect(fixture.nativeElement.querySelector('input[name="recipeName"]')).toBeNull();
    expect(component.revisionNumber()).toBe(3); expect(component.hasPendingChanges()).toBe(false);
    (fixture.nativeElement.querySelector('.editor-heading button') as HTMLButtonElement).click();
    fixture.detectChanges(); await fixture.whenStable();
    const name = fixture.nativeElement.querySelector('input[name="recipeName"]') as HTMLInputElement;
    name.value = 'Pfannkuchen neu'; name.dispatchEvent(new Event('input')); fixture.detectChanges(); await fixture.whenStable();
    const tabs = Array.from(fixture.nativeElement.querySelectorAll('[role="tab"]')) as HTMLButtonElement[];
    for (const tab of tabs) { tab.click(); fixture.detectChanges(); expect(component.selected()?.content.name).toBe('Pfannkuchen neu'); }
    expect(component.hasPendingChanges()).toBe(true); expect(api.save).not.toHaveBeenCalled();
    component.cancelEdit(); fixture.detectChanges();
    expect(component.selected()?.content.name).toBe('Pfannkuchen'); expect(component.hasPendingChanges()).toBe(false);
    expect(component.selected()?.content.internalNotes).toBe('Erhalten'); expect(component.selected()?.content.tags).toEqual(['existing']);
    expect(component.editing()).toBe(false); expect(api.publish).not.toHaveBeenCalled();
  });
  it('saves the draft separately from publication and leaves view-only users protected', async () => {
    const { component, api, fixture } = await setup();
    component.beginEdit(); component.selected()!.content.referenceServings = 12;
    component.publish(false); expect(api.publish).not.toHaveBeenCalled();
    component.save(); fixture.detectChanges();
    expect(api.save).toHaveBeenCalledWith('camp', expect.objectContaining({ draftVersion: 4, content: expect.objectContaining({ referenceServings: 12, tags: ['existing'], internalNotes: 'Erhalten' }) }));
    expect(api.publish).not.toHaveBeenCalled(); expect(component.editing()).toBe(false); expect(component.changed()).toBe(false);
    fixture.componentRef.setInput('disabled', true); fixture.detectChanges();
    component.beginEdit(); component.save(); component.publish(false);
    expect(component.editing()).toBe(false); expect(api.save).toHaveBeenCalledTimes(1); expect(api.publish).not.toHaveBeenCalled();
  });
  it('warns before leaving with pending changes but leaves without a warning after cancellation', async () => {
    const { component, fixture } = await setup();
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    component.beginEdit(); component.selected()!.content.source = 'Neu'; component.closeEditor();
    expect(component.selected()).not.toBeNull(); expect(confirm).toHaveBeenCalledTimes(1);
    component.cancelEdit(); component.closeEditor(); fixture.detectChanges();
    expect(component.selected()).toBeNull(); expect(confirm).toHaveBeenCalledTimes(1); confirm.mockRestore();
  });
  it('retains pending changes on save conflicts and prevents leaving during a save', async () => {
    const { component, api } = await setup();
    const response = new Subject<RecipeEditorDraft>(); api.save.mockReturnValue(response);
    component.beginEdit(); component.selected()!.content.name = 'Geändert'; component.save();
    component.closeEditor(); expect(component.selected()).not.toBeNull(); expect(component.saving()).toBe(true);
    response.error(new HttpErrorResponse({ status: 409 }));
    expect(component.hasPendingChanges()).toBe(true); expect(component.error()).toContain('zwischenzeitlich');
  });
  it('supports keyboard tabs and renders nutrition only in its own panel', async () => {
    const { fixture, component } = await setup();
    const first = fixture.nativeElement.querySelector('[role="tab"]') as HTMLButtonElement;
    first.dispatchEvent(new KeyboardEvent('keydown', { key: 'End', bubbles: true })); fixture.detectChanges();
    expect(component.activeTab()).toBe('publication');
    expect((fixture.nativeElement.querySelector('#recipe-panel-nutrition') as HTMLElement).hidden).toBe(true);
    expect(fixture.nativeElement.querySelector('#recipe-panel-ingredients .nutrition-section')).toBeNull();
    component.activeTab.set('nutrition'); fixture.detectChanges();
    expect((fixture.nativeElement.querySelector('#recipe-panel-nutrition') as HTMLElement).hidden).toBe(false);
  });
});
