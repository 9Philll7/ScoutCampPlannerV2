import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, effect, ElementRef, inject, input, signal, viewChild } from '@angular/core';
import { RecipeCatalogComponent } from './recipe-catalog.component';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatMenuModule } from '@angular/material/menu';
import { MatSelectModule } from '@angular/material/select';
import { IngredientCatalogEntry } from '../camp/camp-api.service';
import { ActionIconComponent, ActionIconName } from '../../shared/action-icon.component';
import { RecipeCatalogEntry, RecipeEditorApiService, RecipeEditorContent,
  RecipeEditorDraft, RecipeEditorGroup, RecipeEditorIngredientPosition, RecipeEditorIngredientReplacement,
  RecipeEditorIngredientReference, RecipeEditorConflict,
  RecipeNutritionCalculation, RecipeNutritionValues, RecipePublicationResponse,
  RecipeValidationIssue, RecipeEditorIngredientUpdate, RecipeEditorSubrecipePosition, RecipeEditorSubrecipeReference, RecipeEditorSubrecipeReplacement } from './recipe-editor-api.service';

type RecipeEditorEntry = { kind: 'ingredient'; position: RecipeEditorIngredientPosition } | { kind: 'subrecipe'; position: RecipeEditorSubrecipePosition };

interface IngredientSection {
  id: string | null;
  group: RecipeEditorGroup | null;
  positions: RecipeEditorIngredientPosition[];
  entries: RecipeEditorEntry[];
}

type IngredientDialogMode = 'picker' | 'group' | 'move' | 'scaling' | 'replacements' | 'updates' | 'subrecipe' | 'subrecipe-replacements';

interface EditorConflictOption extends RecipeEditorConflict { name: string; }

@Component({
  selector: 'scp-recipe-editor',
  standalone: true,
  imports: [RecipeCatalogComponent, FormsModule, MatButtonModule, MatCheckboxModule, MatFormFieldModule,
    MatInputModule, MatProgressSpinnerModule, MatSelectModule, MatSlideToggleModule, MatMenuModule, ActionIconComponent],
  templateUrl: './recipe-editor.component.html',
  styleUrl: './recipe-editor.component.scss'
})
export class RecipeEditorComponent {
  private readonly api = inject(RecipeEditorApiService);
  readonly activeTab = signal('basic');
  readonly editing = signal(false);
  readonly readOnly = computed(() => this.disabled() || !this.editing() || this.saving() || this.publishing() || this.selected()?.status === 2);
  readonly tabs: { id: string; label: string; icon: ActionIconName }[] = [
    { id: 'basic', label: 'Grunddaten', icon: 'planning' },
    { id: 'ingredients', label: 'Zutaten', icon: 'ingredients' },
    { id: 'nutrition', label: 'Nährwerte', icon: 'nutrition' },
    { id: 'publication', label: 'Prüfung & Veröffentlichung', icon: 'check' }
  ];
  private savedDraft: RecipeEditorDraft | null = null;
  readonly revisionNumber = computed(() => this.recipes().find(value => value.recipeId === this.selected()?.id)?.revisionNumber ?? null);
  hasPendingChanges() { return this.editing() && (this.changed() || this.ingredientDialogDirty()); }
  beginEdit() {
    if (this.disabled() || this.saving() || this.publishing() || this.selected()?.status === 2 || !this.selected()) return;
    this.savedDraft = structuredClone(this.selected()!);
    this.snapshot = JSON.stringify(this.selected()!.content);
    this.editing.set(true); this.publicationWarningsAcknowledged.set(false); this.notice.set(''); this.error.set('');
  }
  cancelEdit() {
    if (this.saving() || this.publishing()) return;
    this.closeIngredientDialog();
    if (this.savedDraft) this.setDraft(structuredClone(this.savedDraft));
    this.editing.set(false); this.notice.set(''); this.error.set('');
  }
  tabKey(event: KeyboardEvent, index: number) {
    let next = index;
    if (event.key === 'ArrowRight') next = (index + 1) % this.tabs.length;
    else if (event.key === 'ArrowLeft') next = (index + this.tabs.length - 1) % this.tabs.length;
    else if (event.key === 'Home') next = 0;
    else if (event.key === 'End') next = this.tabs.length - 1;
    else return;
    event.preventDefault(); this.activeTab.set(this.tabs[next].id);
    (event.currentTarget as HTMLElement).parentElement?.querySelectorAll<HTMLButtonElement>('[role="tab"]')[next]?.focus();
  }
  readonly campId = input.required<string>();
  readonly usedRevisionIds = input<ReadonlySet<string> | null>(null);
  readonly visibleRecipes = computed(() => this.recipes().filter(recipe =>
    this.usedRevisionIds() === null || recipe.isLocal || !!recipe.revisionId && this.usedRevisionIds()!.has(recipe.revisionId)));
  readonly disabled = input(false);
  readonly recipes = signal<RecipeCatalogEntry[]>([]);
  readonly ingredients = signal<IngredientCatalogEntry[]>([]);
  readonly selected = signal<RecipeEditorDraft | null>(null);
  readonly ingredientSearch = signal('');
  readonly targetGroupId = signal<string | null>(null);
  readonly activeReplacementPicker = signal<string | null>(null);
  readonly replacementSearches = signal<Record<string, string>>({});
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly nutrition = signal<RecipeNutritionCalculation | null>(null);
  readonly nutritionLoading = signal(false);
  readonly nutritionMessage = signal('');
  private nutritionRequest = 0;
  readonly nutritionRows: { key: keyof RecipeNutritionValues; label: string; subrow?: boolean }[] = [
    { key: 'energyKilocalories', label: 'Energie' },
    { key: 'fatGrams', label: 'Fett' },
    { key: 'saturatedFatGrams', label: 'davon gesättigte Fettsäuren', subrow: true },
    { key: 'carbohydrateGrams', label: 'Kohlenhydrate' },
    { key: 'sugarsGrams', label: 'davon Zucker', subrow: true },
    { key: 'proteinGrams', label: 'Eiweiß' },
    { key: 'saltGrams', label: 'Salz' },
    { key: 'fiberGrams', label: 'Ballaststoffe' }
  ];
  readonly publishing = signal(false);
  readonly publicationIssues = signal<RecipeValidationIssue[]>([]);
  readonly publicationWarningsAcknowledged = signal(false);
  readonly validationTargetId = signal<string | null>(null);
  readonly publicationHasErrors = computed(() => this.publicationIssues().some(issue => issue.severity === 0));
  private snapshot = '';

  readonly ingredientCandidates = computed(() => {
    const search = this.ingredientSearch().trim().toLocaleLowerCase('de');
    const used = new Set(this.selected()?.content.ingredientPositions
      .filter(value => value.groupId === this.targetGroupId()).map(value => value.ingredientRevisionId));
    const matches = this.ingredients().filter(value => value.revisionId && !used.has(value.revisionId) &&
      value.name.toLocaleLowerCase('de').includes(search));
    const camp = matches.filter(value => value.scope === 'Camp');
    if (camp.length) return camp;
    const tenant = matches.filter(value => value.scope === 'Tenant');
    return tenant.length ? tenant : matches.filter(value => value.scope === 'Central');
  });

  readonly ingredientDialogMode = signal<IngredientDialogMode | null>(null);
  readonly dialogPositionId = signal<string | null>(null);
  readonly dialogPosition = computed(() => this.selected()?.content.ingredientPositions.find(value => value.id === this.dialogPositionId()) ?? null);
  readonly replacementDraft = signal<RecipeEditorIngredientPosition | null>(null);
  readonly scalingDraft = signal<{ scalingMode: number; stepSize: number | null; quantityPerStep: number | null; minimumQuantity?: number | null; maximumQuantity?: number | null } | null>(null);
  readonly groupDialogId = signal<string | null>(null);
  readonly groupDialogName = signal('');
  readonly dialogTargetGroupId = signal<string | null>(null);
  readonly dialogError = signal('');
  private readonly ingredientDialog = viewChild<ElementRef<HTMLDialogElement>>('ingredientDialog');
  private readonly hostElement = inject<ElementRef<HTMLElement>>(ElementRef);
  private ingredientDialogReturnFocus: HTMLElement | null = null;
  private ingredientDialogSnapshot = '';

  constructor() {
    effect(() => { const campId = this.campId(); if (campId) this.load(); });
    effect(() => { const dialog = this.ingredientDialog()?.nativeElement; if (dialog && !dialog.open) dialog.showModal(); });
  }
  ingredientDialogTitle() {
    return { picker: 'Zutat hinzufügen', group: this.groupDialogId() ? 'Gruppe umbenennen' : 'Gruppe hinzufügen',
      move: 'Zutat verschieben', scaling: 'Mengenregel', replacements: 'Ernährung & Ersatz', updates: 'Zutatenaktualisierungen', 'subrecipe-replacements': 'Ernährung & Ersatz', subrecipe: this.subrecipeOriginalId() ? 'Unterrezept bearbeiten' : 'Unterrezept hinzufügen' }[this.ingredientDialogMode() ?? 'picker'];
  }
  private ingredientDialogPayload() {
    switch (this.ingredientDialogMode()) {
      case 'group': return this.groupDialogName();
      case 'move': return this.dialogTargetGroupId();
      case 'scaling': return this.scalingDraft();
      case 'subrecipe-replacements':
      case 'subrecipe': return { position: this.subrecipeDraft(), replacement: this.subrecipeReplacementDirty() ? this.subrecipeReplacementDraft() : null };
      case 'replacements': return this.replacementDraft()?.replacements;
      default: return null;
    }
  }
  ingredientDialogDirty() { return !!this.ingredientDialogMode() && JSON.stringify(this.ingredientDialogPayload()) !== this.ingredientDialogSnapshot; }
  openIngredientDialog(mode: IngredientDialogMode) {
    if (['picker','group','move','scaling','subrecipe'].includes(mode) && this.readOnly()) return;
    this.ingredientDialogReturnFocus = document.activeElement instanceof HTMLElement ? document.activeElement : null;
    this.dialogError.set(''); this.ingredientDialogMode.set(mode);
    this.ingredientDialogSnapshot = JSON.stringify(this.ingredientDialogPayload());
  }
  openPositionDialog(mode: 'move' | 'scaling' | 'replacements', position: RecipeEditorIngredientPosition) {
    if (mode !== 'replacements' && this.readOnly()) return;
    this.dialogPositionId.set(position.id);
    this.dialogTargetGroupId.set(position.groupId);
    this.scalingDraft.set({ scalingMode: position.scalingMode, stepSize: position.stepSize, quantityPerStep: position.quantityPerStep, minimumQuantity: position.minimumQuantity ?? null, maximumQuantity: position.maximumQuantity ?? null });
    this.replacementDraft.set(structuredClone(position)); this.openIngredientDialog(mode);
  }
  openGroupEditor(group: RecipeEditorGroup | null) {
    if (this.readOnly()) return;
    this.groupDialogId.set(group?.id ?? null); this.groupDialogName.set(group?.name ?? this.nextGroupName()); this.openIngredientDialog('group');
  }
  closeIngredientDialog() {
    const wasOpen = !!this.ingredientDialogMode();
    const dialog = this.ingredientDialog()?.nativeElement;
    if (dialog?.open && typeof dialog.close === 'function') dialog.close();
    this.subrecipeReplacementDraft.set(null); this.subrecipeDraft.set(null); this.subrecipeOriginalId.set(null); this.subrecipeSearch.set('');
    this.ingredientDialogMode.set(null); this.dialogPositionId.set(null); this.replacementDraft.set(null); this.scalingDraft.set(null);
    this.dialogError.set(''); this.activeReplacementPicker.set(null); this.replacementSearches.set({}); this.ingredientDialogSnapshot = '';
    if (wasOpen) {
      const target = this.ingredientDialogReturnFocus?.isConnected ? this.ingredientDialogReturnFocus : this.hostElement.nativeElement.querySelector<HTMLElement>('[role=tab][aria-selected=true]');
      target?.focus();
    }
    this.ingredientDialogReturnFocus = null;
  }
  changeDialogScalingMode(mode: number) {
    this.scalingDraft.set({ scalingMode: mode, stepSize: null, quantityPerStep: null, minimumQuantity: null, maximumQuantity: null });
  }
  applyIngredientDialog() {
    if (this.readOnly()) return;
    const mode = this.ingredientDialogMode(); const position = this.dialogPosition();
    if (mode === 'subrecipe' || mode === 'subrecipe-replacements') { this.applySubrecipe(); return; }
    if (mode === 'group') {
      const name = this.groupDialogName().trim();
      if (!name) { this.dialogError.set('Bitte gib einen Gruppennamen ein.'); return; }
      if (this.groupDialogId()) { const group = this.selected()?.content.groups.find(value => value.id === this.groupDialogId()); if (group) group.name = name; }
      else { this.addGroup(); const group = this.selected()?.content.groups.find(value => value.id === this.targetGroupId()); if (group) group.name = name; }
    } else if (mode === 'move' && position) { this.movePositionToGroup(position, this.dialogTargetGroupId()); }
    else if (mode === 'scaling' && position && this.scalingDraft()) { if (!this.validScalingDraft()) { this.dialogError.set('Bitte prüfe die Mengenregel: positive Schrittwerte oder gültige Grenzen (Minimum darf Maximum nicht überschreiten).'); return; } Object.assign(position, this.scalingDraft()); }
    else if (mode === 'replacements' && position && this.replacementDraft()) { position.replacements = structuredClone(this.replacementDraft()!.replacements); }
    this.markContentChanged(); this.closeIngredientDialog();
  }
  scalingLabel(mode: number) { return mode === 1 ? 'Fixe Menge' : mode === 2 ? 'In festen Schritten' : 'Linear'; }
  scalingDescription(position: RecipeEditorIngredientPosition) {
    if (position.scalingMode === 0) {
      const unit = this.ingredientUnitSymbol(position);
      return ['Linear', position.minimumQuantity != null ? `min. ${this.scalingNumber(position.minimumQuantity)} ${unit}` : '', position.maximumQuantity != null ? `max. ${this.scalingNumber(position.maximumQuantity)} ${unit}` : ''].filter(Boolean).join(' · ');
    }
    return position.scalingMode === 2 ? `${this.scalingNumber(position.quantityPerStep)} ${this.ingredientUnitSymbol(position)} je angefangene ${this.scalingNumber(position.stepSize)} Standardportionen` : this.scalingLabel(position.scalingMode);
  }
  scalingNumber(value: number | null) { return value !== null && Number.isFinite(value) ? new Intl.NumberFormat('de-DE', { maximumFractionDigits: 12 }).format(value) : '—'; }
  validScalingDraft() {
    const rule = this.scalingDraft();
    if (!rule) return false;
    if (rule.scalingMode === 0) return [rule.minimumQuantity, rule.maximumQuantity].every(value => value == null || Number.isFinite(value) && value >= 0)
      && (rule.minimumQuantity == null || rule.maximumQuantity == null || rule.minimumQuantity <= rule.maximumQuantity);
    return (rule.scalingMode !== 2 || [rule.stepSize, rule.quantityPerStep].every(value => value !== null && Number.isFinite(value) && value > 0));
  }
  scalingPreview() {
    const rule = this.scalingDraft();
    if (!rule || rule.scalingMode !== 2 || !this.validScalingDraft()) return [];
    return [1, 2, 3].map(index => ({
      range: index === 1 ? `Bis ${this.scalingNumber(rule.stepSize)}` : `Über ${this.scalingNumber(rule.stepSize! * (index - 1))} bis ${this.scalingNumber(rule.stepSize! * index)}`,
      amount: this.scalingNumber(rule.quantityPerStep! * index)
    }));
  }
  scalingUsesStageFactors() {
    const mode = this.dialogPosition()?.ageGroupScaling;
    return mode === 1 || mode === 0 && this.selected()?.content.defaultAgeGroupScalingApplies === true;
  }

  readonly referenceData = signal<import('../ingredients/ingredient-revision-api.service').IngredientEditorReferenceData | null>(null);
  readonly referenceDataLoading = signal(false);
  loadRecipeReferenceData() {
    if (this.referenceData() || this.referenceDataLoading()) return;
    this.referenceDataLoading.set(true);
    this.api.referenceData().subscribe({ next: value => { this.referenceData.set(value); this.referenceDataLoading.set(false); }, error: () => { this.referenceDataLoading.set(false); this.error.set('Die Einheiten konnten nicht geladen werden. Bitte erneut versuchen.'); } });
  }
  recipeUnits() {
    return this.referenceData()?.units ?? [];
  }
  recipeReferenceLabel() {
    const content = this.selected()?.content;
    if (!content) return '—';
    if (content.recipeType === 0) return `${this.scalingNumber(content.referenceServings)} ${content.referenceServings === 1 ? 'Standardportion' : 'Standardportionen'}`;
    const unit = this.recipeUnits().find(value => value.id === content.referenceUnitId)
      ?? this.ingredients().flatMap(value => value.units).find(value => value.unitId === content.referenceUnitId);
    return `${this.scalingNumber(content.referenceQuantity)} ${unit?.symbol ?? '—'}`;
  }
  changeRecipeType(type: number) {
    const content = this.selected()?.content;
    if (!content || this.readOnly() || content.recipeType === type) return;
    content.recipeType = type;
    if (type === 1) { content.referenceServings = null; content.referenceQuantity = null; content.referenceUnitId = null; content.authoringStage = null; content.defaultAgeGroupScalingApplies = false; this.loadRecipeReferenceData(); }
    else { content.referenceQuantity = null; content.referenceUnitId = null; content.referenceServings = null; }
    this.markContentChanged();
  }
  readonly subrecipeReplacementDraft = signal<RecipeEditorSubrecipeReplacement | null>(null);
  private subrecipeReplacementSnapshot = '';
  subrecipeReplacementDirty() { return !!this.subrecipeReplacementDraft() && JSON.stringify(this.subrecipeReplacementDraft()) !== this.subrecipeReplacementSnapshot; }
  openSubrecipeReplacement(value: RecipeEditorSubrecipeReplacement | null = null) {
    if (this.readOnly() || !this.subrecipeDraft()) return;
    const draft = value ? structuredClone(value) : { id: crypto.randomUUID(), recipeRevisionId: null, servings: null, quantity: null, unitId: null, conflicts: [] };
    this.subrecipeReplacementDraft.set(draft); this.subrecipeReplacementSnapshot = JSON.stringify(draft);
  }
  subrecipeReplacementCandidates() {
    const source = this.subrecipeDraft(), current = this.subrecipeReplacementDraft();
    return (this.selected()?.subrecipeReferences ?? []).filter(value => value.recipeId !== this.selected()?.id
      && (value.canSelect || value.revisionId === current?.recipeRevisionId)
      && value.recipeType === (source ? this.subrecipeReference(source)?.recipeType : null)
      && value.revisionId !== source?.recipeRevisionId);
  }
  selectSubrecipeReplacement(revisionId: string | null) {
    const draft = this.subrecipeReplacementDraft(), reference = this.subrecipeReplacementCandidates().find(value => value.revisionId === revisionId);
    if (!draft || !reference) return;
    this.subrecipeReplacementDraft.set({ ...draft, recipeRevisionId: revisionId, servings: reference.recipeType === 0 ? this.subrecipeDraft()?.servings ?? 1 : null,
      quantity: reference.recipeType === 1 ? reference.referenceQuantity : null, unitId: reference.recipeType === 1 ? reference.referenceUnit?.unitId ?? null : null });
  }
  subrecipeConflictOptions(): EditorConflictOption[] {
    const source = this.subrecipeDraft(); if (!source) return [];
    const named = [...(this.referenceData()?.allergens ?? []).map(value => ({type: 0, id: value.id, name: value.name})),
      ...(this.referenceData()?.intolerances ?? []).map(value => ({type: 1, id: value.id, name: value.name})),
      ...(this.selected()?.subrecipeReferences ?? []).flatMap(value => value.conflicts)];
    const values = [...(this.subrecipeReference(source)?.conflicts ?? []), ...source.replacements.flatMap(value => value.conflicts)];
    return values.filter((value,index) => values.findIndex(other => other.type === value.type && other.id === value.id) === index)
      .map(value => ({...value, name: named.find(other => other.type === value.type && other.id === value.id)?.name ?? 'Gespeicherte Anforderung'}));
  }
  subrecipeConflictUsed(conflict: RecipeEditorConflict) { return this.subrecipeDraft()?.replacements.some(value => value.id !== this.subrecipeReplacementDraft()?.id && value.conflicts.some(other => other.type === conflict.type && other.id === conflict.id)) ?? false; }
  subrecipeReplacementHasConflict(conflict: RecipeEditorConflict) { return this.subrecipeReplacementDraft()?.conflicts.some(value => value.type === conflict.type && value.id === conflict.id) ?? false; }
  toggleSubrecipeReplacementConflict(conflict: RecipeEditorConflict, checked: boolean) {
    const draft = this.subrecipeReplacementDraft(); if (!draft || this.subrecipeConflictUsed(conflict)) return;
    this.subrecipeReplacementDraft.set({...draft, conflicts: checked ? [...draft.conflicts.filter(value => value.type !== conflict.type || value.id !== conflict.id), {type: conflict.type,id: conflict.id}] : draft.conflicts.filter(value => value.type !== conflict.type || value.id !== conflict.id)});
  }
  validSubrecipeReplacement() {
    const value = this.subrecipeReplacementDraft(); if (!value || this.readOnly()) return false;
    const reference = this.subrecipeReplacementCandidates().find(other => other.revisionId === value.recipeRevisionId);
    return !!reference && value.conflicts.length > 0 && !value.conflicts.some(conflict => this.subrecipeConflictUsed(conflict))
      && (reference.recipeType === 0 ? value.servings != null && Number.isFinite(value.servings) && value.servings > 0 : value.quantity != null && Number.isFinite(value.quantity) && value.quantity > 0 && reference.units.some(unit => unit.unitId === value.unitId));
  }
  subrecipeReplacementWarnings() {
    const value = this.subrecipeReplacementDraft(), source = this.subrecipeDraft();
    if (!value || !source) return [];
    return (this.subrecipeReference(value)?.conflicts ?? []).filter(conflict => value.conflicts.some(other => other.type === conflict.type && other.id === conflict.id)
      || !(this.subrecipeReference(source)?.conflicts ?? []).some(other => other.type === conflict.type && other.id === conflict.id));
  }
  applySubrecipeReplacement() {
    if (!this.validSubrecipeReplacement()) return;
    const source = this.subrecipeDraft()!, value = structuredClone(this.subrecipeReplacementDraft()!);
    this.subrecipeDraft.set({...source, replacements: [...source.replacements.filter(other => other.id !== value.id), value]});
    this.subrecipeReplacementDraft.set(null);
  }
  removeSubrecipeReplacement(id: string) {
    const source = this.subrecipeDraft(); if (this.readOnly() || !source) return;
    this.subrecipeDraft.set({...source, replacements: source.replacements.filter(value => value.id !== id)});
  }

  openSubrecipeReplacements(position: RecipeEditorSubrecipePosition) {
    this.subrecipeOriginalId.set(position.id);
    this.subrecipeDraft.set(structuredClone(position));
    this.subrecipeReplacementDraft.set(null); this.subrecipeSearch.set('');
    this.openIngredientDialog('subrecipe-replacements');
  }
  subrecipeReplacementConflictNames(position: RecipeEditorSubrecipePosition, replacement: RecipeEditorSubrecipeReplacement) {
    const known = [...(this.subrecipeReference(position)?.conflicts ?? []), ...(this.selected()?.subrecipeReferences ?? []).flatMap(value => value.conflicts)];
    return replacement.conflicts.map(conflict => known.find(value => value.type === conflict.type && value.id === conflict.id)?.name ?? 'Gespeicherte Anforderung').join(', ');
  }
  readonly subrecipeDraft = signal<RecipeEditorSubrecipePosition | null>(null);
  readonly subrecipeOriginalId = signal<string | null>(null);
  readonly subrecipeSearch = signal('');
  subrecipeReference(position: Pick<RecipeEditorSubrecipePosition, 'recipeRevisionId'>): RecipeEditorSubrecipeReference | undefined {
    return this.selected()?.subrecipeReferences?.find(value => value.revisionId === position.recipeRevisionId);
  }
  subrecipeDietState(position: Pick<RecipeEditorSubrecipePosition, 'recipeRevisionId'>) {
    const reference = this.subrecipeReference(position);
    if (!reference) return 'unknown';
    return reference.conflicts.some(value => !value.preventableByVariants.length) ? 'unavoidable'
      : reference.conflicts.length ? 'preventable' : 'clear';
  }
  subrecipeConflictTitle(conflict: {name: string; preventableByVariants: string[]}) {
    return conflict.preventableByVariants.length ? `Vermeidbar durch ${conflict.preventableByVariants.join(', ')}.` : 'Durch keine aktive Zutatenvariante vermeidbar.';
  }
  subrecipeName(position: Pick<RecipeEditorSubrecipePosition, 'recipeRevisionId'>) { return this.subrecipeReference(position)?.name ?? 'Unterrezeptrevision nicht verfügbar'; }
  subrecipeCandidates() {
    const query = this.subrecipeSearch().trim().toLocaleLowerCase('de');
    return (this.selected()?.subrecipeReferences ?? []).filter(value => value.canSelect && value.recipeId !== this.selected()?.id && value.name.toLocaleLowerCase('de').includes(query));
  }
  subrecipeAmount(position: Pick<RecipeEditorSubrecipePosition, 'recipeRevisionId' | 'servings' | 'quantity' | 'unitId'>) {
    const reference = this.subrecipeReference(position);
    return position.servings != null ? `${this.scalingNumber(position.servings)} ${position.servings === 1 ? 'Standardportion' : 'Standardportionen'}`
      : `${this.scalingNumber(position.quantity)} ${reference?.units.find(value => value.unitId === position.unitId)?.symbol ?? '—'}`;
  }
  openSubrecipeEditor(position: RecipeEditorSubrecipePosition | null = null) {
    if (this.readOnly()) return;
    this.subrecipeOriginalId.set(position?.id ?? null);
    this.subrecipeDraft.set(position ? structuredClone(position) : { id: crypto.randomUUID(), groupId: null, recipeRevisionId: null, servings: 1, quantity: null, unitId: null, sortOrder: 0, replacements: [] });
    this.subrecipeSearch.set(''); this.openIngredientDialog('subrecipe');
  }
  selectSubrecipe(reference: RecipeEditorSubrecipeReference) {
    const draft = this.subrecipeDraft(); if (!draft || !reference.canSelect || reference.recipeId === this.selected()?.id) return;
    if (draft.recipeRevisionId === reference.revisionId) return;
    const previous = this.subrecipeReference(draft);
    if (draft.replacements.length && previous && previous.recipeType !== reference.recipeType) {
      this.dialogError.set('Mit vorhandenen Ersatzrezepten kann der Rezepttyp nicht gewechselt werden.'); return;
    }
    this.dialogError.set('');
    this.subrecipeDraft.set({ ...draft, recipeRevisionId: reference.revisionId,
      servings: reference.recipeType === 0 ? 1 : null,
      quantity: reference.recipeType === 1 ? reference.referenceQuantity : null,
      unitId: reference.recipeType === 1 ? reference.referenceUnit?.unitId ?? null : null });
  }
  validSubrecipeDraft() {
    const draft = this.subrecipeDraft(); if (!draft || this.readOnly() || this.subrecipeReplacementDraft()) return false;
    const reference = this.subrecipeReference(draft);
    const original = this.selected()?.content.subrecipePositions.find(value => value.id === this.subrecipeOriginalId());
    if (!reference || reference.recipeId === this.selected()?.id || !reference.canSelect && original?.recipeRevisionId !== draft.recipeRevisionId) return false;
    if (draft.groupId !== null && !this.selected()?.content.groups.some(value => value.id === draft.groupId)) return false;
    if (this.selected()?.content.subrecipePositions.some(value => value.id !== draft.id && value.groupId === draft.groupId && value.recipeRevisionId === draft.recipeRevisionId)) return false;
    return reference.recipeType === 0
      ? draft.servings !== null && Number.isFinite(draft.servings) && draft.servings > 0
      : draft.quantity !== null && Number.isFinite(draft.quantity) && draft.quantity > 0 && reference.units.some(value => value.unitId === draft.unitId);
  }
  private applySubrecipe() {
    if (!this.validSubrecipeDraft()) { this.dialogError.set('Wähle eine zulässige veröffentlichte Revision, eine positive Menge und eine Gruppe ohne doppelten Eintrag.'); return; }
    const content = this.selected()!.content, value = structuredClone(this.subrecipeDraft()!);
    const original = content.subrecipePositions.find(position => position.id === this.subrecipeOriginalId());
    const previousGroup = original?.groupId;
    if (!original || original.groupId !== value.groupId) value.sortOrder = this.allPositionsForGroup(value.groupId).filter(position => position.id !== value.id).length;
    if (original) content.subrecipePositions = content.subrecipePositions.map(position => position.id === original.id ? value : position);
    else content.subrecipePositions.push(value);
    if (previousGroup !== undefined) this.normalizePositions(previousGroup);
    this.normalizePositions(value.groupId); this.markContentChanged(); this.closeIngredientDialog();
  }
  removeSubrecipe(position: RecipeEditorSubrecipePosition) {
    if (this.readOnly()) return;
    this.selected()!.content.subrecipePositions = this.selected()!.content.subrecipePositions.filter(value => value.id !== position.id);
    this.normalizePositions(position.groupId); this.markContentChanged();
  }
  private entriesForGroup(groupId: string | null): RecipeEditorEntry[] {
    const content = this.selected()?.content;
    return [ ...(content?.ingredientPositions ?? []).map(position => ({kind: 'ingredient' as const, position})),
      ...(content?.subrecipePositions ?? []).map(position => ({kind: 'subrecipe' as const, position})) ]
      .filter(value => value.position.groupId === groupId).sort((a,b) => a.position.sortOrder - b.position.sortOrder);
  }
  private allPositionsForGroup(groupId: string | null) { return this.entriesForGroup(groupId).map(value => value.position); }

  ingredientUnitSymbol(position: RecipeEditorIngredientPosition) { return this.ingredient(position)?.units.find(value => value.unitId === position.unitId)?.symbol ?? '—'; }
  ingredientOrigin(position: RecipeEditorIngredientPosition) { return this.ingredients().find(value => value.revisionId === position.ingredientRevisionId)?.originInformation || 'Keine Angabe'; }
  availableIngredientUpdates() { return (this.selected()?.content.ingredientPositions ?? []).filter(value => !!this.ingredientReference(value)?.availableUpdate); }
  ingredientHintCount() { return (this.selected()?.content.ingredientPositions ?? []).filter(value => this.positionConflicts(value).length > 0).length
    + (this.selected()?.content.subrecipePositions ?? []).filter(value => (this.subrecipeReference(value)?.conflicts.length ?? 0) > 0).length; }
  subrecipeCount(groupId: string | null) { return this.selected()?.content.subrecipePositions.filter(value => value.groupId === groupId).length ?? 0; }
  dietState(position: RecipeEditorIngredientPosition) {
    const values = this.positionConflicts(position).map(value => this.conflictVariantNames(position, value.id));
    return values.some(value => value?.length === 0) ? 'unavoidable' : values.length && values.every(value => !!value?.length) ? 'preventable' : 'unknown';
  }
  dietLabel(position: RecipeEditorIngredientPosition) {
    const values = this.positionConflicts(position);
    const label = values.length === 1 ? values[0].name : values.length ? `${values.length} Ernährungshinweise` : 'Ersatzzutaten';
    return label + (this.dietState(position) === 'preventable' ? ' (Variante)' : position.replacements.length ? ' · Ersatz hinterlegt' : '');
  }

  load() {
    this.loading.set(true); this.error.set('');
    this.api.list(this.campId()).subscribe({ next: recipes => {
      this.recipes.set(recipes); this.api.ingredients(this.campId()).subscribe({
        next: ingredients => { this.ingredients.set(ingredients); this.loading.set(false); },
        error: () => { this.loading.set(false); this.error.set('Die Zutaten konnten nicht geladen werden.'); }
      });
    }, error: () => { this.loading.set(false); this.error.set('Die Rezepte konnten nicht geladen werden.'); } });
  }

  createDraft() {
    const content: RecipeEditorContent = { name: this.nextDraftName(), description: null, source: null,
      internalNotes: null, recipeType: 0, referenceServings: 10, referenceQuantity: null,
      referenceUnitId: null, defaultAgeGroupScalingApplies: true, authoringStage: null,
      tags: [], groups: [], ingredientPositions: [], subrecipePositions: [] };
    this.saving.set(true); this.error.set('');
    this.api.create(this.campId(), content).subscribe({ next: draft => {
      this.saving.set(false); this.setDraft(draft); this.activeTab.set('basic'); this.beginEdit(); this.loadCatalogOnly();
    }, error: (response: HttpErrorResponse) => { this.saving.set(false);
      this.error.set(response.status === 403 ? 'Du darfst keine Lagerrezepte bearbeiten.' :
        'Das Rezept konnte nicht angelegt werden.'); } });
  }

  open(recipe: RecipeCatalogEntry) {
    if (this.hasPendingChanges() && !window.confirm('Ungespeicherte Änderungen verwerfen?')) return;
    this.activeTab.set('basic');
    this.loading.set(true); this.error.set(''); this.notice.set('');
    this.api.get(this.campId(), recipe.recipeId).subscribe({ next: draft => {
      this.loading.set(false); this.setDraft(draft);
    }, error: () => { this.loading.set(false); this.error.set('Das Rezept konnte nicht geladen werden.'); } });
  }

  closeEditor() {
    if (this.saving() || this.publishing()) return;
    if (this.hasPendingChanges() && !window.confirm('Ungespeicherte Änderungen verwerfen und zur Rezeptliste zurückkehren?')) return;
    this.editing.set(false);
    this.selected.set(null); this.ingredientSearch.set(''); this.targetGroupId.set(null);
    this.activeReplacementPicker.set(null); this.replacementSearches.set({}); this.notice.set(''); this.load();
  }

  ingredient(position: RecipeEditorIngredientPosition) {
    return this.ingredientReference(position) ??
      this.ingredients().find(value => value.revisionId === position.ingredientRevisionId);
  }

  ingredientReference(position: RecipeEditorIngredientPosition) {
    const references = this.selected()?.ingredientReferences ?? [];
    const reference = references.find(value => value.revisionId === position.ingredientRevisionId);
    if (reference) return reference;
    const previous = references.find(value => value.availableUpdate?.revisionId === position.ingredientRevisionId);
    const update = previous?.availableUpdate;
    return previous && update ? { ...previous, ...update, availableUpdate: null } : undefined;
  }

  replacementIngredient(replacement: RecipeEditorIngredientReplacement):
    RecipeEditorIngredientReference | IngredientCatalogEntry | undefined {
    return this.selected()?.ingredientReferences.find(value => value.revisionId === replacement.ingredientRevisionId) ??
      this.ingredients().find(value => value.revisionId === replacement.ingredientRevisionId);
  }

  replacementUnitSymbol(replacement: RecipeEditorIngredientReplacement) { return this.replacementIngredient(replacement)?.units.find(value => value.unitId === replacement.unitId)?.symbol ?? '—'; }
  replacementAssignedNames(position: RecipeEditorIngredientPosition, replacement: RecipeEditorIngredientReplacement) { return this.positionConflicts(position).filter(value => this.replacementHasConflict(replacement, value)).map(value => value.name).join(', '); }

  replacementConflictVariantNames(replacement: RecipeEditorIngredientReplacement, conflictId: string): string[] | null {
    const reference = this.selected()?.ingredientReferences
      .find(value => value.revisionId === replacement.ingredientRevisionId);
    if (!reference) return null;
    return reference.conflicts.find(conflict => conflict.id === conflictId)?.preventableByVariants ?? [];
  }

  replacementConflictTitle(
    replacement: RecipeEditorIngredientReplacement, conflictId: string, conflictName: string) {
    const variants = this.replacementConflictVariantNames(replacement, conflictId);
    if (variants === null) return `${conflictName}: Variantenprüfung nach dem Speichern verfügbar.`;
    return variants.length
      ? `${conflictName}: vermeidbar durch ${variants.join(', ')}.`
      : `${conflictName}: durch keine aktive Variante vermeidbar.`;
  }

  positionConflicts(position: RecipeEditorIngredientPosition): EditorConflictOption[] {
    const reference = this.ingredientReference(position);
    if (reference) return reference.conflicts.map(value => ({ type: value.type, id: value.id, name: value.name }));
    return (this.ingredients().find(value => value.revisionId === position.ingredientRevisionId)?.conflicts ?? [])
      .map(value => ({ type: this.conflictType(value.type), id: value.id, name: value.name }));
  }

  unassignedConflicts(position: RecipeEditorIngredientPosition) {
    return this.positionConflicts(position).filter(conflict =>
      !position.replacements.some(replacement => this.replacementHasConflict(replacement, conflict)));
  }

  replacementHasConflict(replacement: RecipeEditorIngredientReplacement, conflict: RecipeEditorConflict) {
    return replacement.conflicts.some(value => value.type === conflict.type && value.id === conflict.id);
  }

  conflictAssignedToOtherReplacement(
    position: RecipeEditorIngredientPosition, replacementId: string, conflict: RecipeEditorConflict) {
    return position.replacements.some(replacement => replacement.id !== replacementId &&
      this.replacementHasConflict(replacement, conflict));
  }

  setReplacementConflict(position: RecipeEditorIngredientPosition, replacement: RecipeEditorIngredientReplacement,
    conflict: RecipeEditorConflict, selected: boolean) {
    if (selected && this.conflictAssignedToOtherReplacement(position, replacement.id, conflict)) return;
    replacement.conflicts = selected
      ? [...replacement.conflicts.filter(value => value.type !== conflict.type || value.id !== conflict.id),
          { type: conflict.type, id: conflict.id }]
      : replacement.conflicts.filter(value => value.type !== conflict.type || value.id !== conflict.id);
    this.markContentChanged();
  }

  toggleReplacementPicker(positionId: string) {
    this.activeReplacementPicker.set(this.activeReplacementPicker() === positionId ? null : positionId);
  }

  replacementSearch(positionId: string) { return this.replacementSearches()[positionId] ?? ''; }

  setReplacementSearch(positionId: string, value: string) {
    this.replacementSearches.update(searches => ({ ...searches, [positionId]: value }));
  }

  replacementCandidates(position: RecipeEditorIngredientPosition) {
    const search = this.replacementSearch(position.id).trim().toLocaleLowerCase('de');
    if (!search) return [];
    const used = new Set(position.replacements.map(value => value.ingredientRevisionId));
    const originalIngredientId = this.ingredientReference(position)?.ingredientId ??
      this.ingredients().find(value => value.revisionId === position.ingredientRevisionId)?.id;
    const matches = this.ingredients().filter(value => value.revisionId && value.id !== originalIngredientId &&
      !used.has(value.revisionId) && value.name.toLocaleLowerCase('de').includes(search));
    const camp = matches.filter(value => value.scope === 'Camp');
    if (camp.length) return camp;
    const tenant = matches.filter(value => value.scope === 'Tenant');
    return tenant.length ? tenant : matches.filter(value => value.scope === 'Central');
  }

  addReplacement(position: RecipeEditorIngredientPosition, ingredient: IngredientCatalogEntry) {
    if (!ingredient.revisionId || !ingredient.units.length) return;
    const remaining = this.unassignedConflicts(position);
    position.replacements.push({
      id: crypto.randomUUID(), ingredientRevisionId: ingredient.revisionId,
      quantity: position.quantity ?? 1, unitId: ingredient.units[0].unitId,
      conflicts: remaining.length === 1 ? [{ type: remaining[0].type, id: remaining[0].id }] : [],
    });
    this.activeReplacementPicker.set(null);
    this.setReplacementSearch(position.id, '');
    this.markContentChanged();
  }

  removeReplacement(position: RecipeEditorIngredientPosition, replacementId: string) {
    position.replacements = position.replacements.filter(value => value.id !== replacementId);
    this.markContentChanged();
  }

  adoptIngredientUpdate(position: RecipeEditorIngredientPosition, update: RecipeEditorIngredientUpdate) {
    const draft = this.selected();
    if (!draft) return;
    const duplicate = draft.content.ingredientPositions.some(value => value.id !== position.id &&
      value.groupId === position.groupId && value.ingredientRevisionId === update.revisionId);
    if (duplicate) {
      this.error.set('Die aktuelle Revision dieser Zutat ist in der Gruppe bereits vorhanden.');
      return;
    }
    position.ingredientRevisionId = update.revisionId;
    if (!update.units.some(unit => unit.unitId === position.unitId))
      position.unitId = update.units[0]?.unitId ?? null;
    this.error.set('');
    this.markContentChanged();
  }

  conflictVariantNames(position: RecipeEditorIngredientPosition, conflictId: string): string[] | null {
    const reference = this.ingredientReference(position);
    if (!reference) return null;
    return reference.conflicts.find(conflict => conflict.id === conflictId)?.preventableByVariants ?? [];
  }

  conflictTitle(position: RecipeEditorIngredientPosition, conflictId: string, conflictName: string) {
    const variants = this.conflictVariantNames(position, conflictId);
    if (variants === null) return `${conflictName}: Variantenprüfung nach dem Speichern verfügbar.`;
    return variants.length
      ? `${conflictName}: vermeidbar durch ${variants.join(', ')}.`
      : `${conflictName}: durch keine aktive Variante vermeidbar.`;
  }

  orderedGroups() {
    return [...(this.selected()?.content.groups ?? [])].sort((first, second) => first.sortOrder - second.sortOrder);
  }

  ingredientSections(): IngredientSection[] {
    const draft = this.selected();
    if (!draft) return [];
    return [
      { id: null, group: null, positions: this.positionsForGroup(null), entries: this.entriesForGroup(null) },
      ...this.orderedGroups().map(group => ({ id: group.id, group, positions: this.positionsForGroup(group.id), entries: this.entriesForGroup(group.id) })),
    ];
  }

  addGroup() {
    const draft = this.selected(); if (!draft) return;
    const group: RecipeEditorGroup = {
      id: crypto.randomUUID(), name: this.nextGroupName(), sortOrder: draft.content.groups.length,
    };
    draft.content.groups.push(group);
    this.targetGroupId.set(group.id);
    this.markContentChanged();
  }

  removeGroup(group: RecipeEditorGroup) {
    const draft = this.selected();
    if (!draft || !this.groupIsEmpty(group.id)) return;
    draft.content.groups = draft.content.groups.filter(value => value.id !== group.id);
    this.normalizeGroups();
    if (this.targetGroupId() === group.id) this.targetGroupId.set(null);
    this.markContentChanged();
  }

  groupIsEmpty(groupId: string) {
    const content = this.selected()?.content;
    return !!content && !content.ingredientPositions.some(value => value.groupId === groupId) &&
      !content.subrecipePositions.some(value => value.groupId === groupId);
  }

  canMoveGroup(group: RecipeEditorGroup, direction: -1 | 1) {
    const groups = this.orderedGroups();
    const index = groups.findIndex(value => value.id === group.id);
    return index >= 0 && index + direction >= 0 && index + direction < groups.length;
  }

  moveGroup(group: RecipeEditorGroup, direction: -1 | 1) {
    const groups = this.orderedGroups();
    const index = groups.findIndex(value => value.id === group.id);
    const targetIndex = index + direction;
    if (index < 0 || targetIndex < 0 || targetIndex >= groups.length) return;
    [groups[index], groups[targetIndex]] = [groups[targetIndex], groups[index]];
    groups.forEach((value, sortOrder) => value.sortOrder = sortOrder);
    this.markContentChanged();
  }

  addIngredient(ingredient: IngredientCatalogEntry) {
    const draft = this.selected();
    if (!draft || !ingredient.revisionId || !ingredient.units.length) return;
    const groupId = this.targetGroupId();
    const sortOrder = this.allPositionsForGroup(groupId).length;
    draft.content.ingredientPositions.push({ id: crypto.randomUUID(), groupId,
      ingredientRevisionId: ingredient.revisionId, quantity: 1, unitId: ingredient.units[0].unitId,
      sortOrder, scalingMode: 0, ageGroupScaling: 0,
      stepSize: null, quantityPerStep: null, replacements: [] });
    this.markContentChanged();
    this.ingredientSearch.set('');
  }

  removePosition(positionId: string) {
    const draft = this.selected(); if (!draft) return;
    const position = draft.content.ingredientPositions.find(value => value.id === positionId);
    if (!position) return;
    draft.content.ingredientPositions = draft.content.ingredientPositions.filter(value => value.id !== positionId);
    this.normalizePositions(position.groupId);
    this.markContentChanged();
  }

  movePosition(position: RecipeEditorIngredientPosition | RecipeEditorSubrecipePosition, direction: -1 | 1) {
    if (this.readOnly()) return;
    const positions = this.allPositionsForGroup(position.groupId);
    const index = positions.findIndex(value => value.id === position.id);
    const targetIndex = index + direction;
    if (index < 0 || targetIndex < 0 || targetIndex >= positions.length) return;
    [positions[index], positions[targetIndex]] = [positions[targetIndex], positions[index]];
    positions.forEach((value, sortOrder) => value.sortOrder = sortOrder);
    this.markContentChanged();
  }

  movePositionToGroup(position: RecipeEditorIngredientPosition, groupId: string | null) {
    if (position.groupId === groupId) return;
    const draft = this.selected(); if (!draft) return;
    const duplicate = draft.content.ingredientPositions.some(value => value.id !== position.id &&
      value.groupId === groupId && value.ingredientRevisionId === position.ingredientRevisionId);
    if (duplicate) {
      this.error.set('Diese Zutat ist in der gewählten Gruppe bereits vorhanden.');
      return;
    }
    const previousGroupId = position.groupId;
    position.groupId = groupId;
    position.sortOrder = this.allPositionsForGroup(groupId).filter(value => value.id !== position.id).length;
    this.normalizePositions(previousGroupId);
    this.normalizePositions(groupId);
    this.error.set('');
    this.markContentChanged();
  }

  changed() { return !!this.selected() && JSON.stringify(this.selected()!.content) !== this.snapshot; }

  save() {
    const draft = this.selected(); if (!draft || this.readOnly() || this.ingredientDialogMode() || !this.changed()) return;
    this.saving.set(true); this.error.set(''); this.notice.set('');
    this.api.save(this.campId(), draft).subscribe({ next: saved => {
      this.saving.set(false); this.setDraft(saved); this.notice.set('Das Rezept wurde gespeichert.');
      this.loadCatalogOnly();
    }, error: (response: HttpErrorResponse) => { this.saving.set(false);
      this.error.set(response.status === 409 ?
        'Das Rezept wurde zwischenzeitlich geändert. Bitte öffne es erneut.' : 'Das Rezept konnte nicht gespeichert werden.');
    } });
  }

  publish(acknowledgeWarnings: boolean) {
    const draft = this.selected();
    if (!draft || !this.canPublishRecipe() || acknowledgeWarnings && (!this.publicationWarningsAcknowledged() || !this.publicationWarnings().length || this.publicationErrors().length > 0)) return;
    this.publicationWarningsAcknowledged.set(false);
    this.publishing.set(true); this.error.set(''); this.notice.set('');
    this.api.publish(this.campId(), draft, acknowledgeWarnings).subscribe({
      next: result => {
        this.publicationIssues.set([]);
        this.api.get(this.campId(), draft.id).subscribe({
          next: current => {
            this.publishing.set(false); this.setDraft(current);
            this.notice.set(`Das Rezept wurde als Revision ${result.revisionNumber} veröffentlicht.`);
            this.loadCatalogOnly();
          },
          error: () => {
            this.publishing.set(false);
            this.error.set('Das veröffentlichte Rezept konnte nicht neu geladen werden.');
          }
        });
      },
      error: (response: HttpErrorResponse) => {
        this.publishing.set(false);
        const result = response.error as RecipePublicationResponse | undefined;
        if (result?.status === 4 || result?.status === 5) {
          this.publicationIssues.set(result.validation?.issues ?? []);
          return;
        }
        this.publicationIssues.set([]);
        this.error.set(response.status === 403 ? 'Du darfst Lagerrezepte nicht veröffentlichen.' :
          result?.status === 3 ? 'Das Rezept wurde zwischenzeitlich geändert. Bitte öffne es erneut.' :
          result?.status === 6 ? 'Ein archiviertes Rezept kann nicht veröffentlicht werden.' :
          'Das Rezept konnte nicht veröffentlicht werden.');
      }
    });
  }

  loadNutrition() {
    const draft = this.selected();
    if (!draft || this.changed() || this.nutritionLoading()) return;
    const request = ++this.nutritionRequest;
    this.nutritionLoading.set(true); this.nutritionMessage.set('');
    this.api.nutrition(this.campId(), draft.id).subscribe({
      next: value => {
        if (request !== this.nutritionRequest || this.selected()?.id !== draft.id) return;
        this.nutrition.set(value); this.nutritionLoading.set(false);
      },
      error: (response: HttpErrorResponse) => {
        if (request !== this.nutritionRequest || this.selected()?.id !== draft.id) return;
        this.nutrition.set(null); this.nutritionLoading.set(false);
        this.nutritionMessage.set(response.status === 422
          ? 'Ergänze zuerst Standardportionen und mindestens eine vollständig konfigurierte Zutat.'
          : 'Die Nährwerte konnten nicht berechnet werden.');
      }
    });
  }

  private displayedNutrition(scope: 'portion' | 'total') {
    if (this.changed() || this.nutritionLoading()) return null;
    return scope === 'portion' ? this.nutrition()?.perStandardPortion : this.nutrition()?.total;
  }
  nutritionEnergy(scope: 'portion' | 'total') {
    const value = this.displayedNutrition(scope)?.energyKilocalories;
    return value == null ? '—' : `${this.formatNumber(value, 0)} kcal`;
  }
  nutritionEnergyKilojoules(scope: 'portion' | 'total') {
    const value = this.displayedNutrition(scope)?.energyKilojoules;
    return value == null ? 'Noch nicht berechenbar' : `${this.formatNumber(value, 0)} kJ`;
  }
  nutritionCell(key: keyof RecipeNutritionValues, scope: 'portion' | 'total') {
    const values = this.displayedNutrition(scope);
    if (!values || values[key] == null) return '—';
    if (key === 'energyKilocalories') return `${this.formatNumber(values.energyKilocalories, 0)} kcal / ${this.formatNumber(values.energyKilojoules, 0)} kJ`;
    return `${this.formatNumber(values[key]!, key === 'saltGrams' ? 3 : 2)} g`;
  }
  nutritionReferenceServings() {
    const content = this.savedDraft?.content;
    return content?.recipeType === 0 && content.referenceServings != null
      ? content.referenceServings * (content.authoringStage?.factor ?? 1) : null;
  }
  nutritionReferences() { return this.savedDraft?.ingredientReferences ?? []; }
  nutritionMissingIngredients() {
    const grouped = new Map<string, RecipeNutritionCalculation['missingContributions'][number] & { occurrences: number; revisionNumber: number | null }>();
    for (const missing of this.nutrition()?.missingContributions ?? []) {
      const key = `${missing.ingredientRevisionId}-${missing.reason}`;
      const existing = grouped.get(key);
      if (existing) existing.occurrences++;
      else grouped.set(key, { ...missing, occurrences: 1, revisionNumber: this.nutritionReferences().find(value => value.revisionId === missing.ingredientRevisionId)?.revisionNumber ?? null });
    }
    return [...grouped.values()];
  }

  nutritionReason(reason: number) {
    return reason === 0 ? 'kein Nährwertprofil hinterlegt' :
      reason === 1 ? 'Nährwertprofil noch nicht vollständig geprüft' :
      'Bezugsmenge oder Umrechnung ist ungültig';
  }

  scopeLabel(scope: IngredientCatalogEntry['scope'] | number | undefined) {
    return scope === 'Camp' || scope === 2 ? 'Lager' : scope === 'Tenant' || scope === 1 ? 'Organisation' :
      scope === 'Central' || scope === 0 ? 'Zentral' : '';
  }
  statusLabel(status: number) { return status === 0 ? 'Entwurf' : status === 1 ? 'Aktiv' : 'Archiviert'; }

  canPublishRecipe() { return !!this.selected() && this.selected()!.status !== 2 && !this.disabled() && !this.editing() && !this.saving() && !this.changed() && !this.publishing(); }
  publicationErrors() { return this.changed() ? [] : this.publicationIssues().filter(issue => issue.severity === 0); }
  publicationWarnings() { return this.changed() ? [] : this.publicationIssues().filter(issue => issue.severity === 1); }
  publicationStatusTitle() {
    if (this.selected()?.status === 2) return 'Rezept archiviert';
    if (this.editing()) return this.changed() ? 'Änderungen zuerst speichern' : 'Entwurf in Bearbeitung';
    if (this.publicationErrors().length) return 'Die Veröffentlichung ist derzeit nicht möglich.';
    if (this.publicationWarnings().length) return 'Warnungen vor der Veröffentlichung prüfen';
    return 'Noch keine separate Vorabprüfung';
  }
  publicationStatusText() {
    if (this.selected()?.status === 2) return 'Dieses Rezept kann nicht veröffentlicht werden.';
    if (this.editing()) return 'Die Veröffentlichung verwendet ausschließlich den gespeicherten Rezeptstand.';
    if (this.publicationErrors().length) return 'Behebe zuerst die aufgeführten Fehler und speichere den Entwurf.';
    if (this.publicationWarnings().length) return 'Diese Warnungen müssen vor der Veröffentlichung ausdrücklich bestätigt werden.';
    return 'Beim Veröffentlichen wird das Rezept geprüft. Ohne blockierende Fehler wird direkt eine neue Revision erstellt.';
  }
  publicationAdvisories() {
    const result: { id: string; title: string; text: string; tab: string; action: string }[] = [];
    if (this.changed()) return result;
    const missing = this.nutritionMissingIngredients().length;
    if (!this.nutritionLoading() && this.nutrition()?.isComplete === false && missing > 0)
      result.push({ id: 'nutrition', title: 'Unvollständige Nährwertdaten', text: `Für ${missing} Zutatenrevision${missing === 1 ? '' : 'en'} fehlen verwendbare Nährwertangaben.`, tab: 'nutrition', action: 'Nährwerte ansehen' });
    const updates = this.availableIngredientUpdates().length;
    if (updates) result.push({ id: 'updates', title: 'Zutatenaktualisierungen verfügbar', text: `Für ${updates} Zutatenposition${updates === 1 ? '' : 'en'} sind neuere Revisionen vorhanden.`, tab: 'ingredients', action: 'Zutaten ansehen' });
    return result;
  }
  publicationIssueTitle(issue: RecipeValidationIssue) {
    if (issue.code.startsWith('recipe.reference.') || issue.code.startsWith('recipe.authoring-stage.')) return 'Rezeptbezug prüfen';
    if (issue.code.startsWith('recipe.group.')) return 'Zutatengruppe prüfen';
    if (issue.code.startsWith('recipe.ingredient-replacement.') || issue.code.startsWith('recipe.replacement.')) return 'Ersatzzutat prüfen';
    if (issue.code.startsWith('recipe.ingredient.')) return 'Zutatenangaben prüfen';
    if (issue.code.startsWith('recipe.subrecipe.') || issue.code.startsWith('recipe.recipe-replacement.')) return 'Unterrezept prüfen';
    if (issue.code === 'recipe.conflict.unresolved') return 'Offener Ernährungskonflikt';
    if (issue.code === 'recipe.name.missing') return 'Rezeptname fehlt';
    if (issue.code === 'recipe.description.missing') return 'Beschreibung fehlt';
    if (issue.code === 'recipe.source.missing') return 'Quelle fehlt';
    return issue.severity === 0 ? 'Rezeptangaben prüfen' : 'Hinweis zum Rezept';
  }
  private validationDestination(issue: RecipeValidationIssue) {
    const content = this.selected()?.content;
    const replacementId = issue.context?.['replacementId'];
    const position = content?.ingredientPositions.find(value => value.id === issue.context?.['positionId'] || !!replacementId && value.replacements.some(rule => rule.id === replacementId));
    const subrecipe = content?.subrecipePositions.find(value => value.id === issue.context?.['positionId'] || !!replacementId && value.replacements.some(rule => rule.id === replacementId));
    const basic = ['recipe.name.missing', 'recipe.description.missing', 'recipe.source.missing'].includes(issue.code) || issue.code.startsWith('recipe.reference.') || issue.code.startsWith('recipe.authoring-stage.');
    return { tab: basic ? 'basic' : 'ingredients', positionId: position?.id ?? subrecipe?.id, groupId: content?.groups.find(group => group.id === issue.context?.['groupId'])?.id };
  }
  validationIssueAction(issue: RecipeValidationIssue) {
    const target = this.validationDestination(issue);
    return target.positionId || target.groupId ? 'Zum Eintrag' : target.tab === 'basic' ? 'Zu den Grunddaten' : 'Zu den Zutaten';
  }
  goToValidationIssue(issue: RecipeValidationIssue) {
    if (this.saving() || this.publishing()) return;
    const target = this.validationDestination(issue); this.validationTargetId.set(target.positionId ?? target.groupId ?? null); this.activeTab.set(target.tab);
    requestAnimationFrame(() => {
      const host = this.hostElement.nativeElement;
      const row = target.positionId ? [...host.querySelectorAll<HTMLElement>('[data-validation-position]')].find(el => el.dataset['validationPosition'] === target.positionId) : null;
      const groupTarget = target.groupId ? [...host.querySelectorAll<HTMLDetailsElement>('[data-validation-group]')].find(el => el.dataset['validationGroup'] === target.groupId) : null;
      const element = row ?? groupTarget ?? host.querySelector<HTMLElement>(`#recipe-panel-${target.tab}`);
      const group = groupTarget ?? row?.closest('details'); if (group) group.open = true;
      element?.focus({ preventScroll: true }); element?.scrollIntoView({ block: 'nearest' });
    });
  }

  validationIssueText(issue: RecipeValidationIssue) {
    const conflictName = issue.context?.['conflictName'];
    if (conflictName) {
      if (issue.code === 'recipe.conflict.unresolved')
        return `Für „${conflictName}“ ist noch kein Ersatz definiert.`;
      if (issue.code === 'recipe.replacement.conflict.remains')
        return `Die Ersatzzutat enthält weiterhin „${conflictName}“ bzw. kann es enthalten.`;
      if (issue.code === 'recipe.replacement.conflict.created')
        return `Die Ersatzzutat bringt zusätzlich den Konflikt „${conflictName}“ mit.`;
    }
    const messages: Record<string, string> = {
      'recipe.name.missing': 'Ein Rezeptname ist erforderlich.',
      'recipe.reference.quantity.invalid': 'Die Bezugsmenge muss größer als null sein.',
      'recipe.reference.unit.invalid': 'Für den Rezeptbezug wird eine gültige Einheit benötigt.',
      'recipe.authoring-stage.central.forbidden': 'Zentrale Rezepte dürfen keine lagerbezogene Erfassungsstufe verwenden.',
      'recipe.ingredient.scaling-mode.invalid': 'Die Mengenregel dieser Zutat ist ungültig.',
      'recipe.ingredient.age-scaling.invalid': 'Die Einstellung der Altersstufenfaktoren ist ungültig.',
      'recipe.ingredient.stepwise.invalid': 'Die Mengenregel in festen Schritten benötigt positive Schrittwerte.',
      'recipe.subrecipe.revision.invalid': 'Die gespeicherte Unterrezeptrevision ist nicht verfügbar.',
      'recipe.subrecipe.demand.invalid': 'Der Mengen- oder Portionsbezug des Unterrezepts ist ungültig.',
      'recipe.subrecipe.duplicate': 'Ein Unterrezept ist innerhalb derselben Gruppe mehrfach eingetragen.',
      'recipe.subrecipe.cycle': 'Die Unterrezepte verweisen zyklisch aufeinander.',
      'recipe.recipe-replacement.revision.invalid': 'Die Ersatzrezeptrevision ist nicht verfügbar.',
      'recipe.recipe-replacement.type.mismatch': 'Das Ersatzrezept hat einen ungeeigneten Rezepttyp.',
      'recipe.recipe-replacement.demand.invalid': 'Der Mengen- oder Portionsbezug des Ersatzrezepts ist ungültig.',
      'recipe.subrecipe.archived': 'Eine verwendete Unterrezeptrevision ist archiviert.',
      'recipe.replacement.conflict.remains': 'Ein bekannter Ernährungskonflikt bleibt trotz Ersatz bestehen.',
      'recipe.replacement.conflict.created': 'Eine Ersatzlösung erzeugt einen zusätzlichen Ernährungskonflikt.',

      'recipe.reference.servings.invalid': 'Die Anzahl der Standardportionen muss größer als null sein.',
      'recipe.positions.empty': 'Das Rezept benötigt mindestens eine Zutat.',
      'recipe.group.name.missing': 'Jede Gruppe benötigt einen Namen.',
      'recipe.group.empty': 'Leere Gruppen müssen entfernt oder mit Zutaten befüllt werden.',
      'recipe.position.sort-order.invalid': 'Die Reihenfolge der Zutaten ist ungültig.',
      'recipe.ingredient.missing': 'Eine verwendete Zutatenrevision ist nicht mehr verfügbar.',
      'recipe.ingredient.scope.forbidden': 'Eine Zutat ist für dieses Lagerrezept nicht zulässig.',
      'recipe.ingredient.quantity.invalid': 'Jede Zutat benötigt eine Menge größer als null.',
      'recipe.ingredient.unit.invalid': 'Für jede Zutat muss eine gültige Einheit gewählt werden.',
      'recipe.ingredient.duplicate': 'Eine Zutat darf innerhalb derselben Gruppe nur einmal vorkommen.',
      'recipe.ingredient-replacement.ingredient.missing': 'Eine Ersatzzutat ist nicht mehr verfügbar.',
      'recipe.ingredient-replacement.quantity.invalid': 'Jede Ersatzzutat benötigt eine Menge größer als null.',
      'recipe.ingredient-replacement.unit.invalid': 'Für jede Ersatzzutat muss eine gültige Einheit gewählt werden.',
      'recipe.replacement.conflicts.empty': 'Jede Ersatzzutat muss mindestens einem Konflikt zugeordnet sein.',
      'recipe.replacement.conflict.duplicate': 'Ein Konflikt darf an dieser Position nur einer Ersatzzutat zugeordnet sein.',
      'recipe.description.missing': 'Es fehlt eine Beschreibung oder Zubereitungsanleitung.',
      'recipe.source.missing': 'Es fehlt eine Quellenangabe.',
      'recipe.conflict.unresolved': 'Für einen bekannten Konflikt ist noch kein Ersatz definiert.',
    };
    return messages[issue.code] ?? (issue.message && issue.message !== issue.code ? issue.message : 'Bitte prüfe die Angaben im Rezept.');
  }

  private setDraft(draft: RecipeEditorDraft) {
    this.closeIngredientDialog();
    this.publicationWarningsAcknowledged.set(false); this.validationTargetId.set(null);
    this.savedDraft = structuredClone(draft); this.editing.set(false);
    this.selected.set(draft); if (draft.content.recipeType === 1) this.loadRecipeReferenceData(); this.targetGroupId.set(null); this.snapshot = JSON.stringify(draft.content);
    this.activeReplacementPicker.set(null); this.replacementSearches.set({});
    this.nutritionRequest++; this.nutritionLoading.set(false);
    this.nutrition.set(null); this.nutritionMessage.set(''); this.publicationIssues.set([]); this.loadNutrition();
  }
  private positionsForGroup(groupId: string | null) {
    return [...(this.selected()?.content.ingredientPositions ?? [])]
      .filter(value => value.groupId === groupId)
      .sort((first, second) => first.sortOrder - second.sortOrder);
  }
  private normalizePositions(groupId: string | null) {
    this.allPositionsForGroup(groupId).forEach((value, sortOrder) => value.sortOrder = sortOrder);
  }
  private normalizeGroups() {
    this.orderedGroups().forEach((value, sortOrder) => value.sortOrder = sortOrder);
  }
  private markContentChanged() {
    const draft = this.selected(); if (!draft) return;
    this.selected.set({ ...draft, content: { ...draft.content,
      groups: [...draft.content.groups], ingredientPositions: [...draft.content.ingredientPositions], subrecipePositions: [...draft.content.subrecipePositions] } });
  }
  private nextDraftName() {
    const baseName = 'Neues Rezept';
    const existingNames = new Set(this.recipes().map(recipe => recipe.name.trim().toLocaleLowerCase('de')));
    if (!existingNames.has(baseName.toLocaleLowerCase('de'))) return baseName;
    let suffix = 2;
    while (existingNames.has(`${baseName} ${suffix}`.toLocaleLowerCase('de'))) suffix++;
    return `${baseName} ${suffix}`;
  }
  private nextGroupName() {
    const existingNames = new Set((this.selected()?.content.groups ?? [])
      .map(group => (group.name ?? '').trim().toLocaleLowerCase('de')));
    let suffix = 1;
    while (existingNames.has(`Gruppe ${suffix}`.toLocaleLowerCase('de'))) suffix++;
    return `Gruppe ${suffix}`;
  }
  private loadCatalogOnly() { this.api.list(this.campId()).subscribe({ next: values => this.recipes.set(values) }); }
  removeLibraryEntry(entryId: string) {
    this.api.removeLibraryEntry(this.campId(), entryId).subscribe({
      next: () => { this.notice.set('Rezept aus der Lagerbibliothek entfernt.'); this.loadCatalogOnly(); },
      error: error => {
        const references = error.error?.references?.join(' · ');
        this.error.set([error.error?.message || 'Das Rezept konnte nicht entfernt werden.', references]
          .filter(Boolean).join(' '));
      },
    });
  }
  private formatNumber(value: number, maximumFractionDigits: number) {
    return new Intl.NumberFormat('de-DE', { maximumFractionDigits }).format(value);
  }
  private conflictType(value: IngredientCatalogEntry['conflicts'][number]['type']) {
    return value === 'Allergen' ? 0 : value === 'Intolerance' ? 1 : 2;
  }
}
