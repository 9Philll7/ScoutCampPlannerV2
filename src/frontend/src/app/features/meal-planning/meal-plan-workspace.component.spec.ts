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
  it('reorders components within one meal and preserves recipes, identities and other meals', () => {
    openEditor();
    component.addGroup(); const first = component.groups()[0];
    component.beginPicker(first); choose('rice-1');
    component.addGroup(); const second = component.groups()[1];
    component.beginPicker(second); choose('pasta');
    first.sortOrder = 5; second.sortOrder = 20;
    component.mealId.set('dinner'); component.addGroup(); const dinner = component.groups()[0];
    component.mealId.set('lunch');
    component.moveGroup(second, -1);
    expect(component.groups().map(group => group.id)).toEqual([second.id, first.id]);
    expect(component.groups().map(group => group.sortOrder)).toEqual([0, 1]);
    expect(first.entries[0].recipeRevisionId).toBe('rice-1');
    expect(second.entries[0].recipeRevisionId).toBe('pasta');
    expect(dinner.sortOrder).toBe(0);
    const baseline = JSON.stringify(plan);
    component.moveGroup(second, -1); component.moveGroup(first, 1); component.moveGroup(dinner, -1);
    expect(JSON.stringify(plan)).toBe(baseline);
    for (const input of ['disabled', 'busy']) {
      fixture.componentRef.setInput(input, true); fixture.detectChanges();
      component.moveGroup(first, -1);
      expect(JSON.stringify(plan)).toBe(baseline);
      fixture.componentRef.setInput(input, false); fixture.detectChanges();
    }
    component.moveGroup(second, 1);
    expect(component.groups().map(group => group.id)).toEqual([first.id, second.id]);
  });
  it('expands the note from its icon and supports reading it without edit permission', async () => {
    openEditor(); component.addGroup(); component.beginPicker(plan.offerGroups[0]); choose('rice-1');
    plan.offerGroups[0].entries[0].note = 'Bei Bedarf weniger Salz verwenden.';
    fixture.detectChanges();
    const button = () => fixture.nativeElement.querySelector('.recipe-note-hint') as HTMLButtonElement;
    expect(button().getAttribute('aria-expanded')).toBe('false');
    button().click(); fixture.detectChanges();
    await fixture.whenStable();
    expect(button().getAttribute('aria-expanded')).toBe('true');
    expect(fixture.nativeElement.querySelector('.entry-metadata textarea').value).toBe('Bei Bedarf weniger Salz verwenden.');
    fixture.componentRef.setInput('disabled', true); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.entry-note-text').textContent).toContain('Bei Bedarf weniger Salz verwenden.');
    expect(fixture.nativeElement.querySelector('.entry-metadata textarea')).toBeNull();
    button().click(); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.entry-metadata')).toBeNull();
  });
  it('separates the plan list from the editor and navigates only active in-period meals', () => {
    expect(fixture.nativeElement.querySelector('.plan-list')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('.plan-editor-layout')).toBeNull();
    openEditor();
    expect(fixture.nativeElement.querySelector('.plan-list')).toBeNull();
    expect(component.dates()).toEqual(['2027-07-02']);
    expect(component.mealsForDate(component.date()).map(meal => meal.id)).toEqual(['lunch', 'dinner']);
    const inactive = fixture.nativeElement.querySelector('.meal-tabs button.inactive') as HTMLButtonElement;
    expect(inactive.textContent).toContain('Inaktiv');
    expect(inactive.textContent).toContain('Entfällt');
    expect(inactive.disabled).toBe(true);
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

  it('keeps a day with no active meals reachable for reactivation and additional meals', () => {
    fixture.componentRef.setInput('data', { ...data, scheduleVersion: 0, meals: data.meals.map(meal => ({ ...meal, isActive: false })) });
    openEditor();
    expect(component.dates()).toEqual(['2027-07-02']);
    expect(component.dayMeals()).toHaveLength(3);
    expect(component.mealsForDate(component.date())).toHaveLength(0);
    expect(component.scheduleLocked()).toBe(false);
    fixture.componentRef.setInput('planDirty', true); fixture.detectChanges();
    expect(component.scheduleLocked()).toBe(true);
    expect(component.scheduleOrderLocked()).toBe(false);
  });
  it('allows ordering and activity with a dirty plan while keeping removal blocked', () => {
    fixture.componentRef.setInput('data', { ...data, scheduleVersion: 4 });
    fixture.componentRef.setInput('planDirty', true);
    openEditor();
    const actions: unknown[] = [];
    component.schedule.subscribe(action => actions.push(action));
    component.moveMeal(0, 1);
    component.removeAdditionalMeal('lunch');
    component.changeActivity('lunch', false);
    expect(actions).toEqual([{ kind: 'order', date: '2027-07-02', mealIds: ['inactive', 'lunch', 'dinner'] },
      { kind: 'activity', mealId: 'lunch', isActive: false }]);
    fixture.componentRef.setInput('busy', true); fixture.detectChanges();
    component.moveMeal(0, 1);
    component.changeActivity('lunch', true);
    expect(actions).toHaveLength(2);
  });
  it('keeps deactivated meals visible without counting their recipes as active coverage', () => {
    openEditor();
    component.addGroup(); component.beginPicker(plan.offerGroups[0]); choose('rice-1');
    fixture.componentRef.setInput('data', { ...data, meals: data.meals.map(meal => meal.id === 'lunch' ? { ...meal, isActive: false } : meal) });
    fixture.detectChanges();
    expect(component.displayedMealsForDate(component.date()).map(meal => meal.id)).toContain('lunch');
    expect(component.mealsForDate(component.date()).map(meal => meal.id)).toEqual(['dinner']);
    expect(component.coveredForDate(component.date())).toBe(0);
    expect(plan.offerGroups[0].entries[0].recipeRevisionId).toBe('rice-1');
    expect(fixture.nativeElement.querySelectorAll('.meal-tabs button.inactive')).toHaveLength(2);
  });
  it('renders German characters correctly in the meal editor', () => {
    openEditor();
    const text = fixture.nativeElement.querySelector('.schedule-dialog').textContent;
    expect(text).toContain('Änderungen gelten sofort für alle Menüpläne.');
    expect(text).toContain('Zusätzliche Mahlzeit');
    expect(text).toContain('Regelmäßig');
    expect(text).toContain('Hinzufügen');
    expect(text).toContain('Schließen');
    expect(text).not.toContain('?');
  });
});
