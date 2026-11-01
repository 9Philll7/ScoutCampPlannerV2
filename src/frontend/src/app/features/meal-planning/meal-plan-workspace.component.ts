import { Component, computed, effect, ElementRef, input, output, signal, viewChild } from '@angular/core';
import { DatePipe, NgTemplateOutlet } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule } from '@angular/material/menu';
import { MatTooltipModule } from '@angular/material/tooltip';
import { RecipePlanningHintsComponent } from './recipe-planning-hints.component';
import { ActionIconComponent } from '../../shared/action-icon.component';
import { ListPaginationComponent } from '../../shared/list-pagination.component';
import { MealPlanDocument, MealPlanEntryDocument, MealPlanOfferGroupDocument, MealPlanningOverview, RecipeOption } from './meal-planning-api.service';

export type MealScheduleAction = { kind: 'add'; date: string; name: string } | { kind: 'order'; date: string; mealIds: string[] } | { kind: 'remove'; mealId: string } | { kind: 'activity'; mealId: string; isActive: boolean };

@Component({
  selector: 'scp-meal-plan-workspace', standalone: true,
  imports: [DatePipe, NgTemplateOutlet, FormsModule, MatButtonModule, MatMenuModule, MatTooltipModule, ActionIconComponent, ListPaginationComponent, RecipePlanningHintsComponent],
  templateUrl: './meal-plan-workspace.component.html', styleUrl: './meal-plan-workspace.component.scss',
})
export class MealPlanWorkspaceComponent {
  readonly data = input.required<MealPlanningOverview>();
  readonly plan = input<MealPlanDocument | null>(null);
  readonly disabled = input(false);
  readonly busy = input(false);
  readonly planDirty = input(false);
  readonly schedule = output<MealScheduleAction>();
  readonly refreshSchedule = output<void>();
  additionalMealName = "";
  readonly open = output<string>();
  readonly close = output<void>();
  readonly save = output<void>();
  readonly create = output<string>();
  readonly remove = output<string>();
  readonly reorder = output<{ index: number; direction: number }>();
  readonly date = signal('');
  readonly mealId = signal('');
  readonly listPage = signal(0);
  readonly pickerPage = signal(0);
  readonly pickerGroup = signal<string | null>(null);
  readonly replacingEntry = signal<string | null>(null);
  readonly previewRevision = signal('');
  readonly metadataEntry = signal<string | null>(null);
  readonly collapsedGroups = signal<Set<string>>(new Set());
  readonly componentNames = ['Hauptspeise', 'Beilage', 'Vorspeise', 'Nachspeise', 'Getränk'];
  private readonly customComponentNames = signal<Set<string>>(new Set());
  readonly renamingPlan = signal(false);
  private readonly renameInput = viewChild<ElementRef<HTMLInputElement>>('renameInput');
  private nameBeforeRename = '';
  search = ''; recipeSearch = ''; newPlanName = '';
  readonly activeMeals = computed(() => this.data().meals.filter(meal => meal.isActive && meal.isInsideCampPeriod));
  readonly dates = computed(() => [...new Set(this.data().meals.filter(meal => meal.isInsideCampPeriod).map(meal => meal.date))].sort());

  constructor() {
    effect(() => {
      const dates = this.dates();
      if (!dates.includes(this.date())) this.date.set(dates[0] ?? '');
      const meals = this.activeMeals().filter(meal => meal.date === this.date());
      if (!meals.some(meal => meal.id === this.mealId())) this.mealId.set(meals[0]?.id ?? '');
    });
    effect(() => { this.plan()?.id; this.collapsedGroups.set(new Set()); this.customComponentNames.set(new Set()); this.metadataEntry.set(null); this.renamingPlan.set(false); });
    effect(() => { const input = this.renameInput(); if (input) { input.nativeElement.focus(); input.nativeElement.select(); } });
  }
  beginRename() { if (this.locked() || !this.plan()) return; this.nameBeforeRename = this.plan()!.name; this.renamingPlan.set(true); }
  finishRename(cancel = false) {
    const plan = this.plan(); if (!plan || !this.renamingPlan()) return;
    plan.name = cancel || !plan.name.trim() ? this.nameBeforeRename : plan.name.trim(); this.renamingPlan.set(false);
  }
  locked() { return this.disabled() || this.busy(); }
  filteredPlans() { const search = this.search.trim().toLocaleLowerCase('de'); return this.data().mealPlans.filter(plan => plan.name.toLocaleLowerCase('de').includes(search)); }
  planPageIndex() { return Math.min(this.listPage(), Math.max(0, Math.ceil(this.filteredPlans().length / 8) - 1)); }
  listedPlans() { return this.filteredPlans().slice(this.planPageIndex() * 8, (this.planPageIndex() + 1) * 8); }
  subscribers(id: string) { return this.data().cookingUnits.filter(unit => unit.standardMealPlanId === id).length; }
  scheduleOrderLocked() { return this.locked() || this.data().scheduleVersion === undefined; }
  scheduleLocked() { return this.scheduleOrderLocked() || this.planDirty(); }
  dayMeals() { return this.data().meals.filter(meal => meal.isInsideCampPeriod && meal.date === this.date()); }
  addAdditionalMeal() {
    if (this.scheduleLocked() || !this.additionalMealName.trim()) return;
    this.schedule.emit({ kind: 'add', date: this.date(), name: this.additionalMealName.trim() });
  }
  moveMeal(index: number, direction: number) {
    if (this.scheduleOrderLocked()) return;
    const ids = this.dayMeals().map(meal => meal.id); const target = index + direction;
    if (target < 0 || target >= ids.length) return;
    [ids[index], ids[target]] = [ids[target], ids[index]];
    this.schedule.emit({ kind: 'order', date: this.date(), mealIds: ids });
  }
  changeActivity(mealId: string, isActive: boolean) { if (!this.scheduleOrderLocked()) this.schedule.emit({ kind: 'activity', mealId, isActive }); }
  removeAdditionalMeal(mealId: string) { if (!this.scheduleLocked()) this.schedule.emit({ kind: 'remove', mealId }); }
  mealsForDate(date: string) { return this.activeMeals().filter(meal => meal.date === date); }
  displayedMealsForDate(date: string) { return this.data().meals.filter(meal => meal.isInsideCampPeriod && meal.date === date); }
  private readonly weekdayFormatter = new Intl.DateTimeFormat('de-AT', { weekday: 'short', timeZone: 'UTC' });
  private readonly dayFormatter = new Intl.DateTimeFormat('de-AT', { day: 'numeric', month: 'long', timeZone: 'UTC' });
  weekday(date: string) { return this.weekdayFormatter.format(new Date(`${date}T12:00:00Z`)).replace('.', ''); }
  dayLabel(date: string) { return this.dayFormatter.format(new Date(`${date}T12:00:00Z`)); }
  selectDate(date: string) { this.date.set(date); this.mealId.set(this.mealsForDate(date)[0]?.id ?? ''); }
  moveDate(direction: number) { const date = this.dates()[this.dates().indexOf(this.date()) + direction]; if (date) this.selectDate(date); }
  groups(mealId = this.mealId()) { return (this.plan()?.offerGroups ?? []).filter(group => group.campMealId === mealId).slice().sort((a, b) => a.sortOrder - b.sortOrder); }
  covered(mealId: string) { return this.groups(mealId).some(group => group.entries.some(entry => entry.isStandard && !!entry.recipeRevisionId)); }
  coveredForDate(date: string) { return this.mealsForDate(date).filter(meal => this.covered(meal.id)).length; }
  missingMeals() { return this.activeMeals().filter(meal => !this.covered(meal.id)).length; }
  emptyGroups() { return this.plan()?.offerGroups.filter(group => !group.entries.length).length ?? 0; }
  selectedMeal() { return this.activeMeals().find(meal => meal.id === this.mealId()); }
  standard(group: MealPlanOfferGroupDocument) { return group.entries.find(entry => entry.isStandard); }
  alternatives(group: MealPlanOfferGroupDocument) { return group.entries.filter(entry => !entry.isStandard).slice().sort((a, b) => a.sortOrder - b.sortOrder); }
  recipe(id: string) { return this.data().recipeOptions.find(recipe => recipe.revisionId === id); }
  recipeName(entry: MealPlanEntryDocument) { return this.recipe(entry.recipeRevisionId)?.name || 'Rezept nicht verfügbar'; }
  componentNameChoice(group: MealPlanOfferGroupDocument) {
    if (this.customComponentNames().has(group.id) || (group.name && !this.componentNames.includes(group.name))) return 'custom';
    return group.name ?? '';
  }
  chooseComponentName(group: MealPlanOfferGroupDocument, choice: string) {
    if (this.locked() || (choice !== 'custom' && choice !== '' && !this.componentNames.includes(choice))) return;
    const custom = new Set(this.customComponentNames());
    if (choice === 'custom') {
      custom.add(group.id);
      if (!group.name || this.componentNames.includes(group.name)) group.name = '';
    } else { custom.delete(group.id); group.name = choice || null; }
    this.customComponentNames.set(custom);
  }
  addGroup() {
    const plan = this.plan(); if (!plan || this.locked() || !this.selectedMeal()) return;
    plan.offerGroups.push({ id: crypto.randomUUID(), campMealId: this.mealId(), name: null,
      sortOrder: Math.max(-1, ...this.groups().map(group => group.sortOrder)) + 1, entries: [] });
  }
  removeGroup(group: MealPlanOfferGroupDocument) {
    const plan = this.plan(); if (!plan || this.locked()) return;
    plan.offerGroups = plan.offerGroups.filter(value => value.id !== group.id);
  }
  moveGroup(group: MealPlanOfferGroupDocument, direction: -1 | 1) {
    if (this.locked()) return;
    const groups = this.groups();
    const index = groups.findIndex(value => value.id === group.id);
    const target = index + direction;
    if (index < 0 || target < 0 || target >= groups.length) return;
    [groups[index], groups[target]] = [groups[target], groups[index]];
    groups.forEach((value, sortOrder) => value.sortOrder = sortOrder);
  }
  toggleGroup(id: string) { const values = new Set(this.collapsedGroups()); values.has(id) ? values.delete(id) : values.add(id); this.collapsedGroups.set(values); }
  makeStandard(group: MealPlanOfferGroupDocument, id: string) {
    if (this.locked() || !group.entries.some(entry => entry.id === id)) return;
    group.entries.forEach(entry => entry.isStandard = entry.id === id);
  }
  removeEntry(group: MealPlanOfferGroupDocument, id: string) {
    if (this.locked()) return;
    const wasStandard = group.entries.some(entry => entry.id === id && entry.isStandard);
    group.entries = group.entries.filter(entry => entry.id !== id);
    if (wasStandard && group.entries.length) group.entries[0].isStandard = true;
  }
  beginPicker(group: MealPlanOfferGroupDocument, entryId: string | null = null) {
    if (this.locked()) return;
    this.pickerGroup.set(group.id); this.replacingEntry.set(entryId); this.recipeSearch = ''; this.pickerPage.set(0);
    this.previewRevision.set(this.filteredRecipes()[0]?.revisionId ?? '');
  }
  filteredRecipes() {
    const group = this.plan()?.offerGroups.find(group => group.id === this.pickerGroup());
    const term = this.recipeSearch.trim().toLocaleLowerCase('de');
    return this.data().recipeOptions.filter(recipe => recipe.isPortionBased &&
      !group?.entries.some(entry => entry.recipeRevisionId === recipe.revisionId && entry.id !== this.replacingEntry()) &&
      `${recipe.name} ${recipe.revisionNumber}`.toLocaleLowerCase('de').includes(term));
  }
  searchedRecipes() { this.pickerPage.set(0); this.previewRevision.set(this.filteredRecipes()[0]?.revisionId ?? ''); }
  pickerPageIndex() { return Math.min(this.pickerPage(), Math.max(0, Math.ceil(this.filteredRecipes().length / 8) - 1)); }
  pagedRecipes() { return this.filteredRecipes().slice(this.pickerPageIndex() * 8, (this.pickerPageIndex() + 1) * 8); }
  preview() { return this.filteredRecipes().find(recipe => recipe.revisionId === this.previewRevision()); }
  applyRecipe(): boolean {
    const group = this.plan()?.offerGroups.find(group => group.id === this.pickerGroup());
    const recipe = this.preview(); if (!group || !recipe || this.locked()) return false;
    const existing = group.entries.find(entry => entry.id === this.replacingEntry());
    if (this.replacingEntry() && !existing) return false;
    if (existing) existing.recipeRevisionId = recipe.revisionId;
    else group.entries.push(this.newEntry(recipe, group));
    this.pickerGroup.set(null); return true;
  }
  private newEntry(recipe: RecipeOption, group: MealPlanOfferGroupDocument): MealPlanEntryDocument {
    return { id: crypto.randomUUID(), recipeRevisionId: recipe.revisionId, isStandard: !group.entries.length,
      displayName: null, role: null, note: null, sortOrder: Math.max(-1, ...group.entries.map(entry => entry.sortOrder)) + 1 };
  }
}
