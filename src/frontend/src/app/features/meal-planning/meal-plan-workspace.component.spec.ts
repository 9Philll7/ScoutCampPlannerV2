import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MealPlanWorkspaceComponent } from './meal-plan-workspace.component';
import { MealPlanDocument, MealPlanningOverview } from './meal-planning-api.service';
import { MealPlanningApiService } from './meal-planning-api.service';
import { of } from 'rxjs';

describe('MealPlanWorkspaceComponent', () => {
  let fixture: ComponentFixture<MealPlanWorkspaceComponent>;
  let component: MealPlanWorkspaceComponent;
  let plan: MealPlanDocument;
  let data: MealPlanningOverview;
  beforeEach(() => {
    plan = { id: 'standard', campId: 'camp', name: 'Standardplan', version: 3, sortOrder: 0, offerGroups: [] };
    const meal = { id: 'lunch', mealTypeId: 'type', mealTypeName: 'Mittagessen', date: '2027-07-02', isActive: true, isInsideCampPeriod: true, changeVersion: 0 };
    const recipe = { revisionId: 'rice-1', recipeId: 'rice', name: 'Reis', revisionNumber: 1, isPortionBased: true, suggestedRole: 1 as const };
    data = { campId: 'camp', meals: [meal, { ...meal, id: 'inactive', mealTypeName: 'Inaktiv', isActive: false },
      { ...meal, id: 'outside', date: '2027-07-03', isInsideCampPeriod: false },
      { ...meal, id: 'dinner', mealTypeName: 'Abendessen' }],
      structureNodes: [], mealPlans: [{ id: plan.id, name: plan.name, version: 3, sortOrder: 0, missingActiveMealCount: 2 }],
      mealPlanDocuments: [plan], cookingUnits: [], cookingUnitGroups: [], operationalMeals: [], coverageWarnings: [],
      recipeOptions: [recipe, { ...recipe, revisionId: 'rice-2', revisionNumber: 2 },
        { ...recipe, revisionId: 'pasta', recipeId: 'pasta', name: 'Nudeln' },
        { ...recipe, revisionId: 'sauce', name: 'Sauce', isPortionBased: false }] };
    TestBed.configureTestingModule({ imports: [MealPlanWorkspaceComponent], providers: [{ provide: MealPlanningApiService, useValue: {
      recipeHints: (_camp: string, revisionId: string) => of({ recipeRevisionId: revisionId, basis: 'PublishedStandard', evaluatedAtUtc: '2026-11-01T00:00:00Z', hasUnknownData: true, hints: [] })
    } }] });
    fixture = TestBed.createComponent(MealPlanWorkspaceComponent);
    fixture.componentRef.setInput('data', data); fixture.detectChanges(); component = fixture.componentInstance;
  });
  function openEditor() { fixture.componentRef.setInput('plan', plan); fixture.detectChanges(); }
  function choose(revision: string) { component.previewRevision.set(revision); expect(component.applyRecipe()).toBe(true); }
  it('separates the plan list from the editor and navigates only active in-period meals', () => {
    expect(fixture.nativeElement.querySelector('.plan-list')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.plan-editor-layout')).toBeNull();
    openEditor();
    expect(fixture.nativeElement.querySelector('.plan-list')).toBeNull();
    expect(component.dates()).toEqual(['2027-07-02']);
    expect(component.mealsForDate(component.date()).map(meal => meal.id)).toEqual(['lunch', 'dinner']);
    expect(fixture.nativeElement.querySelector('.meal-tabs').textContent).not.toContain('Inaktiv');
  });
  it('makes the first selection standard, further selections alternatives and switches only the current component', () => {
    openEditor(); component.addGroup(); const group = plan.offerGroups[0];
    expect(group.entries).toEqual([]);
    component.beginPicker(group); choose('rice-1');
    component.beginPicker(group); choose('pasta');
    expect(group.entries.map(entry => entry.isStandard)).toEqual([true, false]);
    component.addGroup(); const side = plan.offerGroups[1]; component.beginPicker(side); choose('rice-2');
    component.makeStandard(group, group.entries[1].id);
    expect(component.standard(group)?.recipeRevisionId).toBe('pasta');
    expect(component.standard(side)?.recipeRevisionId).toBe('rice-2');
    expect(component.covered('lunch')).toBe(true);
    expect(component.missingMeals()).toBe(1);
  });
  it('offers published portion-based revisions, searches them and prevents duplicate assignments', () => {
    openEditor(); component.addGroup(); const group = plan.offerGroups[0];
    component.beginPicker(group); choose('rice-1'); component.beginPicker(group);
    expect(component.filteredRecipes().map(recipe => recipe.revisionId)).toEqual(['rice-2', 'pasta']);
    component.recipeSearch = 'NUDEL'; component.searchedRecipes();
    expect(component.preview()?.revisionId).toBe('pasta');
    component.previewRevision.set('rice-1'); expect(component.applyRecipe()).toBe(false);
    component.beginPicker(group, group.entries[0].id);
    expect(component.filteredRecipes().map(recipe => recipe.revisionId)).toContain('rice-1');
  });
  it('keeps pinned revisions on catalog refresh and preserves optional metadata on explicit revision changes', () => {
    openEditor(); component.addGroup(); const group = plan.offerGroups[0];
    component.beginPicker(group); choose('rice-1');
    const entry = group.entries[0]; Object.assign(entry, { displayName: 'Reisbeilage', role: 1, note: 'Separat anbieten' });
    fixture.componentRef.setInput('data', { ...data, recipeOptions: data.recipeOptions.filter(recipe => recipe.revisionId !== 'rice-1') }); fixture.detectChanges();
    expect(entry.recipeRevisionId).toBe('rice-1');
    component.beginPicker(group, entry.id); choose('rice-2');
    expect(entry).toMatchObject({ recipeRevisionId: 'rice-2', displayName: 'Reisbeilage', role: 1, note: 'Separat anbieten', isStandard: true });
  });
  it('maintains one standard when removing it and allows an empty incomplete draft', () => {
    openEditor(); component.addGroup(); const group = plan.offerGroups[0];
    component.beginPicker(group); choose('rice-1'); component.beginPicker(group); choose('pasta');
    component.removeEntry(group, group.entries[0].id);
    expect(component.standard(group)?.recipeRevisionId).toBe('pasta');
    component.removeEntry(group, group.entries[0].id);
    expect(component.emptyGroups()).toBe(1); expect(component.missingMeals()).toBe(2);
    component.addGroup(); const newGroup = plan.offerGroups[1]; component.removeGroup(group); component.addGroup();
    expect(new Set(plan.offerGroups.map(group => group.sortOrder)).size).toBe(2);
    expect(plan.offerGroups).toContain(newGroup);
  });
  it('blocks mutations when read-only or while saving', () => {
    openEditor(); component.addGroup(); const group = plan.offerGroups[0];
    component.beginPicker(group); choose('rice-1'); const baseline = JSON.stringify(plan);
    for (const input of ['disabled', 'busy']) {
      fixture.componentRef.setInput(input, true); fixture.detectChanges();
      component.addGroup(); component.removeGroup(group); component.removeEntry(group, group.entries[0].id);
      component.makeStandard(group, 'missing'); component.beginPicker(group);
      expect(component.applyRecipe()).toBe(false); expect(JSON.stringify(plan)).toBe(baseline);
      fixture.componentRef.setInput(input, false); fixture.detectChanges();
    }
  });
});
