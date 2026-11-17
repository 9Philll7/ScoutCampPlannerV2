import { IngredientCatalogEntry } from '../camp/camp-api.service';
import { NgTemplateOutlet, DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, effect, ElementRef, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatMenuModule } from '@angular/material/menu';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { forkJoin } from 'rxjs';
import { ActionIconComponent, ActionIconName } from '../../shared/action-icon.component';
import {
  IngredientEditorReferenceData,
  IngredientAllergenReference,
  CentralIngredientCandidate,
  IngredientCentralContribution,
  IngredientConversionPrecision,
  IngredientDataSuggestion,
  IngredientNutritionProfileItem,
  IngredientSuggestionNutrition,
  IngredientNutritionReviewState,
  IngredientNutritionSourceType,
  IngredientSubstanceContentItem,
  IngredientSubstanceContentReviewState,
  IngredientSubstanceContentSourceType,
  IngredientPropertyReviewState,
  IngredientPropertySource,
  IngredientPropertyState,
  IngredientRevisionApiService,
  IngredientRevisionDetails,
  IngredientRevisionPropertyItem,
  IngredientRevisionUnitConversionItem,
  IngredientVariantRevisionItem,
  IngredientRevisionState,
  IngredientRevisionSummary
} from './ingredient-revision-api.service';
import { ListPaginationComponent } from '../../shared/list-pagination.component';
import { IngredientCatalogComponent } from './ingredient-catalog.component';
import { IngredientNutritionEditorComponent } from './ingredient-nutrition-editor.component';

@Component({
  selector: 'scp-ingredient-revision-editor',
  standalone: true,
  imports: [NgTemplateOutlet, ListPaginationComponent, DatePipe, FormsModule, MatButtonModule, MatCardModule, MatCheckboxModule, MatFormFieldModule,
    MatInputModule, MatMenuModule, MatProgressSpinnerModule, MatSelectModule, ActionIconComponent,
    IngredientNutritionEditorComponent, IngredientCatalogComponent],
  templateUrl: './ingredient-revision-editor.component.html',
  styleUrl: './ingredient-revision-editor.component.scss'
})
export class IngredientRevisionEditorComponent {
  readonly availableIngredients = input<IngredientCatalogEntry[]>([]);
  readonly activeTab = signal('basic');
  readonly unitPage = signal(0);
  readonly allergenPage = signal(0);
  readonly substancePage = signal(0);
  readonly sourcePage = signal(0);
  readonly selectedSuggestionKey = signal('');
  readonly referenceContext = signal<'both' | 'nutrition' | 'substances'>('both');
  onPage(index: number, length: number, page: number, size = 5) {
    const start = Math.min(Math.max(0, page), Math.max(0, Math.ceil(length / size) - 1)) * size;
    return index >= start && index < start + size;
  }
  selectedSuggestion(): IngredientDataSuggestion | undefined {
    return this.suggestionResults().find(value => value.sourceKey === this.selectedSuggestionKey()) ?? this.suggestionResults()[0];
  }
  suggestionComparison(revision: IngredientRevisionDetails, suggestion: IngredientDataSuggestion) {
    const profile = revision.nutritionProfile;
    const unit = this.referenceData()?.units.find(value => value.id === profile?.referenceUnitId);
    const grams = this.referenceData()?.units.find(value => value.symbol === 'g' && value.dimension === 0);
    const referenceMass = profile && unit?.dimension === 0 && grams
      ? profile.referenceQuantity * unit.baseUnitFactor / grams.baseUnitFactor : 0;
    const current = profile ? this.nutritionSummary(profile) : [];
    return this.nutritionSummary(suggestion.nutrition).map((item, index) => {
      const value = current[index]?.value;
      const scaled = value != null && referenceMass > 0 ? value * suggestion.referenceQuantity / referenceMass : null;
      return { ...item, current: scaled === null ? null : Math.round(scaled * 1000) / 1000 };
    });
  }
  readonly selectedVariantId = signal<string | null>(null);
  readonly editorTabs: { id: string; label: string; icon: ActionIconName }[] = [
    { id: 'basic', label: 'Grunddaten', icon: 'planning' },
    { id: 'units', label: 'Einheiten', icon: 'refresh' },
    { id: 'allergies', label: 'Allergene & Unverträglichkeiten', icon: 'warning' },
    { id: 'origins', label: 'Herkunft', icon: 'structure' },
    { id: 'nutrition', label: 'Nährwerte', icon: 'planning' },
    { id: 'variants', label: 'Varianten', icon: 'people' },
    { id: 'sources', label: 'Quellen & Prüfung', icon: 'recipe' }
  ];
  scopeLabel() { return this.scope() === 'central' ? 'Zentral' : this.scope() === 'tenant' ? 'Organisation' : 'Lager'; }
  tabKey(event: KeyboardEvent, index: number) {
    const count = this.editorTabs.length;
    let next = index;
    if (event.key === 'ArrowRight') next = (index + 1) % count;
    else if (event.key === 'ArrowLeft') next = (index + count - 1) % count;
    else if (event.key === 'Home') next = 0;
    else if (event.key === 'End') next = count - 1;
    else return;
    event.preventDefault();
    this.activeTab.set(this.editorTabs[next].id);
    this.host.nativeElement.querySelector<HTMLButtonElement>('#ingredient-tab-' + this.editorTabs[next].id)?.focus();
  }
  hasPendingChanges() {
    const revision = this.selected();
    return !!revision && revision.state === this.draftState && (this.isDirty(revision) || !!this.newSourceEntry().trim()) ||
      this.createOpen() && !!this.createName.trim();
  }
  closeEditor() {
    const revision = this.selected();
    if (this.submitting() || revision && this.hasPendingChanges() && !window.confirm('Nicht gespeicherte Zutatenänderungen verwerfen?')) return;
    this.selected.set(null); this.pendingForkSourceRevisionId.set(null); this.centralCandidates.set([]);
    this.activeTab.set('basic');
  }
  openVersion(revisionId: string) {
    if (this.submitting() || this.hasPendingChanges() && !window.confirm('Nicht gespeicherte Zutatenänderungen verwerfen?')) return;
    this.open(revisionId);
  }
  ingredientVersions(revision: IngredientRevisionDetails) { return this.revisions().filter(value => value.ingredientId === revision.ingredientId); }
  variantVisible(revision: IngredientRevisionDetails, id: string) {
    const selected = revision.variants.find(value => value.id === this.selectedVariantId()) ?? revision.variants[0];
    return selected?.id === id;
  }
  variantSummary(revision: IngredientRevisionDetails, variant: IngredientVariantRevisionItem) {
    const reference = this.referenceData();
    const properties = [
      { label: 'Allergene', overrides: variant.allergenOverrides, definitions: reference?.allergens ?? [] },
      { label: 'Unverträglichkeiten', overrides: variant.intoleranceOverrides, definitions: reference?.intolerances ?? [] },
      { label: 'Herkunft', overrides: variant.originOverrides, definitions: reference?.origins ?? [] }
    ];
    return properties.map(group => ({ label: group.label, changed: group.overrides.length > 0,
      value: group.overrides.length ? group.overrides.map(item =>
        (group.definitions.find(definition => definition.id === item.propertyId)?.name ?? 'Merkmal') + ' · ' + this.propertyStateLabel(item.state)).join(', ') : 'Wie Basiszutat' }))
      .concat([
        { label: 'Nährwerte', changed: !!variant.nutritionProfile, value: variant.nutritionProfile ? 'Eigenes Nährwertprofil' : revision.nutritionProfile ? 'Wie Basiszutat' : 'Keine Angaben' },
        { label: 'Stoffgehalte', changed: variant.substanceContentOverrides.length > 0, value: variant.substanceContentOverrides.length ? variant.substanceContentOverrides.length + ' abweichende Angaben' : 'Wie Basiszutat' },
        { label: 'Einheiten', changed: variant.unitConversionOverrides.length > 0, value: variant.unitConversionOverrides.length ? variant.unitConversionOverrides.length + ' abweichende Umrechnungen' : 'Wie Basiszutat' }
      ]);
  }
  nutritionSummary(profile: IngredientNutritionProfileItem | IngredientSuggestionNutrition) {
    return [
      { label: 'Energie', value: profile.energyKilojoules, unit: 'kJ' },
      { label: 'Fett', value: profile.fatGrams, unit: 'g' },
      { label: 'davon gesättigt', value: profile.saturatedFatGrams, unit: 'g' },
      { label: 'Kohlenhydrate', value: profile.carbohydrateGrams, unit: 'g' },
      { label: 'davon Zucker', value: profile.sugarsGrams, unit: 'g' },
      { label: 'Eiweiß', value: profile.proteinGrams, unit: 'g' },
      { label: 'Salz', value: profile.saltGrams, unit: 'g' }
    ];
  }
  readonly scope = input<'camp' | 'tenant' | 'central'>('camp');
  readonly campId = input('');
  readonly tenantId = input('');
  readonly disabled = input(false);
  readonly published = output<void>();
  readonly revisions = signal<IngredientRevisionSummary[]>([]);
  readonly selected = signal<IngredientRevisionDetails | null>(null);
  readonly referenceData = signal<IngredientEditorReferenceData | null>(null);
  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly createOpen = signal(false);
  readonly error = signal('');
  readonly notice = signal('');
  readonly pendingForkSourceRevisionId = signal<string | null>(null);
  readonly centralCandidates = signal<CentralIngredientCandidate[]>([]);
  readonly contributions = signal<IngredientCentralContribution[]>([]);
  readonly contributionTargets = signal<Record<string, string>>({});
  readonly suggestionQuery = signal('');
  readonly suggestionResults = signal<IngredientDataSuggestion[]>([]);
  readonly suggestionLoading = signal(false);
  readonly suggestionSearched = signal(false);
  readonly suggestionError = signal('');
  readonly sourceEntries = signal<string[]>([]);
  readonly newSourceEntry = signal('');
  readonly draftState = IngredientRevisionState.Draft;
  readonly publishedState = IngredientRevisionState.Published;
  readonly containsState = IngredientPropertyState.Contains;
  readonly propertyStates = [
    { value: IngredientPropertyState.Contains, label: 'Enthalten' },
    { value: IngredientPropertyState.DoesNotContain, label: 'Nicht enthalten' },
    { value: IngredientPropertyState.MayContain, label: 'Kann enthalten' },
    { value: IngredientPropertyState.Unknown, label: 'Unbekannt' }
  ] as const;
  readonly conversionPrecisions = [
    { value: IngredientConversionPrecision.Exact, label: 'Exakt' },
    { value: IngredientConversionPrecision.Average, label: 'Durchschnitt' },
    { value: IngredientConversionPrecision.Estimated, label: 'Geschätzt' }
  ] as const;
  private readonly allergenLetters: Readonly<Record<string, string>> = {
    GLUTEN_CEREALS: 'A', CRUSTACEANS: 'B', EGGS: 'C', FISH: 'D', PEANUTS: 'E', SOYBEANS: 'F',
    MILK: 'G', TREE_NUTS: 'H', CELERY: 'L', MUSTARD: 'M', SESAME: 'N',
    SULPHUR_DIOXIDE_AND_SULPHITES: 'O', LUPIN: 'P', MOLLUSCS: 'R'
  };
  private readonly qualitativeIntoleranceCodes = ['HISTAMINE'];
  private readonly primaryOriginCodes = [
    'PLANT', 'FUNGI', 'MICROBIAL', 'MINERAL', 'SYNTHETIC',
    'MEAT', 'POULTRY', 'FISH', 'CRUSTACEAN', 'MOLLUSC', 'DAIRY', 'EGG', 'HONEY', 'INSECT',
    'UNKNOWN_ORIGIN'
  ];
  private readonly additionalOriginCodes = [
    'ANIMAL_FAT', 'GELATIN', 'ANIMAL_RENNET', 'OTHER_ANIMAL_DERIVED'
  ];
  createName = '';
  createCategoryId = '';
  createBaseUnitId = '';
  private selectedSnapshot = '';
  private readonly api = inject(IngredientRevisionApiService);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  constructor() {
    effect(() => {
      const scope = this.scope();
      const campId = this.campId();
      const tenantId = this.tenantId();
      if (scope === 'central' || (scope === 'tenant' && !!tenantId) || (scope === 'camp' && !!campId)) this.load();
    });
  }

  managementTitle() {
    return this.scope() === 'central' ? 'Zentrale Zutaten verwalten'
      : this.scope() === 'tenant' ? 'Organisationszutaten verwalten' : 'Lagerzutaten verwalten';
  }
  createButtonLabel() {
    return this.scope() === 'central' ? 'Neue zentrale Zutat'
      : this.scope() === 'tenant' ? 'Neue Organisationszutat' : 'Neue Lagerzutat';
  }
  emptyListLabel() {
    return this.scope() === 'central'
      ? 'Noch keine revisionsfähigen zentralen Zutaten vorhanden.'
      : this.scope() === 'tenant'
        ? 'Noch keine revisionsfähigen Organisationszutaten vorhanden.'
        : 'Noch keine revisionsfähigen Lagerzutaten vorhanden.';
  }

  canCreate() { return !!this.createName.trim() && !!this.createCategoryId && !!this.createBaseUnitId; }
  baseUnits() {
    const allowedSymbols = new Set(['g', 'kg', 'ml', 'l', 'Stk.']);
    return (this.referenceData()?.units ?? []).filter(value => allowedSymbols.has(value.symbol));
  }
  baseUnitsByDimension(dimension: number) {
    return this.baseUnits().filter(value => value.dimension === dimension);
  }
  stateLabel(state: IngredientRevisionState) { return state === this.publishedState ? 'Veröffentlicht' : 'Entwurf'; }
  isReviewed(state: IngredientPropertyReviewState) { return state === IngredientPropertyReviewState.Reviewed; }
  reviewState(checked: boolean) { return checked ? IngredientPropertyReviewState.Reviewed : IngredientPropertyReviewState.Unreviewed; }
  allReviewed(value: IngredientRevisionDetails) { return this.isReviewed(value.allergenReviewState) &&
    this.isReviewed(value.intoleranceReviewState) && this.isReviewed(value.originReviewState); }
  isDirty(value: IngredientRevisionDetails) { return this.snapshot(value) !== this.selectedSnapshot; }
  specifiedCount(values: IngredientRevisionPropertyItem[]) { return values.length; }
  specifiedMainAllergenCount(revision: IngredientRevisionDetails) {
    return this.mainAllergens().filter(value => this.propertyState(revision.allergens, value.id) !== null).length;
  }
  specifiedVisibleIntoleranceCount(revision: IngredientRevisionDetails) {
    const visibleIds = new Set([...this.qualitativeIntolerances(), ...this.quantitativeSubstances()].map(value => value.id));
    return new Set([...revision.substanceContents.map(value => value.substanceId),
      ...revision.intolerances.filter(value => visibleIds.has(value.propertyId)).map(value => value.propertyId)]).size;
  }
  qualitativeIntolerances() {
    return (this.referenceData()?.intolerances ?? [])
      .filter(value => this.qualitativeIntoleranceCodes.includes(value.code));
  }
  quantitativeSubstances() {
    return (this.referenceData()?.intolerances ?? [])
      .filter(value => value.isQuantityDependent)
      .sort((left, right) => left.name.localeCompare(right.name));
  }
  visibleIntolerances() { return this.qualitativeIntolerances(); }
  intoleranceLabel(code: string, name: string) {
    return code === 'LACTOSE' ? `${name} (nicht Milchallergie)` : name;
  }
  substanceContent(revision: IngredientRevisionDetails, substanceId: string) {
    return revision.substanceContents.find(value => value.substanceId === substanceId) ?? null;
  }
  substanceMode(revision: IngredientRevisionDetails, substanceId: string) {
    if (this.substanceContent(revision, substanceId))
      return revision.intolerances.some(value => value.propertyId === substanceId) ? 'unknown' : 'quantitative';
    const state = this.propertyState(revision.intolerances, substanceId);
    return state === IngredientPropertyState.Contains ? 'contains' :
      state === IngredientPropertyState.DoesNotContain ? 'absent' : 'unknown';
  }
  variantSubstanceContent(variant: IngredientVariantRevisionItem, substanceId: string) {
    return variant.substanceContentOverrides.find(value => value.substanceId === substanceId) ?? null;
  }
  variantSubstanceMode(variant: IngredientVariantRevisionItem, substanceId: string) {
    const state = this.propertyState(variant.intoleranceOverrides, substanceId);
    if (this.variantSubstanceContent(variant, substanceId)) return state === null ? 'quantitative' : 'unknown';
    return state === null ? 'inherit' : state === IngredientPropertyState.Contains ? 'contains' :
      state === IngredientPropertyState.DoesNotContain ? 'absent' : 'unknown';
  }
  setVariantSubstanceMode(revision: IngredientRevisionDetails, variant: IngredientVariantRevisionItem, substanceId: string, mode: string) {
    variant.intoleranceOverrides = variant.intoleranceOverrides.filter(value => value.propertyId !== substanceId);
    if (mode === 'quantitative') {
      if (!this.variantSubstanceContent(variant, substanceId)) {
        const content = this.newSubstanceContent(revision, substanceId);
        if (content) variant.substanceContentOverrides.push(content);
      }
    } else {
      variant.substanceContentOverrides = variant.substanceContentOverrides.filter(value => value.substanceId !== substanceId);
      if (mode !== 'inherit') this.setPropertyValue(variant.intoleranceOverrides, substanceId,
        mode === 'contains' ? IngredientPropertyState.Contains : mode === 'absent' ? IngredientPropertyState.DoesNotContain : IngredientPropertyState.Unknown,
        IngredientPropertySource.ManuallyVerified);
    }
    revision.intoleranceReviewState = IngredientPropertyReviewState.Unreviewed;
  }
  setSubstanceMode(revision: IngredientRevisionDetails, substanceId: string, mode: string) {
    revision.intolerances = revision.intolerances.filter(value => value.propertyId !== substanceId);
    if (mode === 'quantitative') this.addSubstanceContent(revision, substanceId);
    else {
      this.removeSubstanceContent(revision, substanceId);
      this.setPropertyState(revision, 'intolerances', substanceId,
        mode === 'contains' ? IngredientPropertyState.Contains :
        mode === 'absent' ? IngredientPropertyState.DoesNotContain : IngredientPropertyState.Unknown);
    }
    revision.intoleranceReviewState = IngredientPropertyReviewState.Unreviewed;
  }
  substanceAmountUnits() {
    return (this.referenceData()?.units ?? []).filter(value => value.dimension === 0);
  }
  substanceReferenceUnits(revision: IngredientRevisionDetails) {
    const dimension = this.unitDimension(revision.baseUnitId);
    return (this.referenceData()?.units ?? []).filter(value => value.dimension === dimension);
  }
  addSubstanceContent(revision: IngredientRevisionDetails, substanceId: string) {
    if (this.substanceContent(revision, substanceId)) return;
    const content = this.newSubstanceContent(revision, substanceId);
    if (!content) return;
    revision.intolerances = revision.intolerances.filter(value => value.propertyId !== substanceId);
    revision.substanceContents.push(content);
  }
  private newSubstanceContent(revision: IngredientRevisionDetails, substanceId: string): IngredientSubstanceContentItem | null {
    const amountUnit = this.substanceAmountUnits().find(value => value.symbol === 'g') ?? this.substanceAmountUnits()[0];
    const referenceUnit = this.substanceReferenceUnits(revision)
      .find(value => value.symbol === (this.unitDimension(revision.baseUnitId) === 1 ? 'ml' : 'g')) ??
      this.substanceReferenceUnits(revision)[0];
    if (!amountUnit || !referenceUnit) return null;
    return {
      substanceId, amount: Number.NaN, amountUnitId: amountUnit.id, referenceQuantity: 100,
      referenceUnitId: referenceUnit.id, sourceType: IngredientSubstanceContentSourceType.ManualEstimate,
      sourceReference: revision.sourceSummary.slice(0, 500), reviewState: IngredientSubstanceContentReviewState.Unreviewed
    };
  }
  removeSubstanceContent(revision: IngredientRevisionDetails, substanceId: string) {
    revision.substanceContents = revision.substanceContents.filter(value => value.substanceId !== substanceId);
  }
  primaryOrigins() {
    return (this.referenceData()?.origins ?? [])
      .filter(value => this.primaryOriginCodes.includes(value.code))
      .sort((left, right) => this.primaryOriginCodes.indexOf(left.code) - this.primaryOriginCodes.indexOf(right.code));
  }
  additionalOrigins() {
    return (this.referenceData()?.origins ?? [])
      .filter(value => this.additionalOriginCodes.includes(value.code))
      .sort((left, right) => this.additionalOriginCodes.indexOf(left.code) - this.additionalOriginCodes.indexOf(right.code));
  }
  primaryOriginId(revision: IngredientRevisionDetails) {
    const contained = this.primaryOrigins()
      .filter(value => this.propertyState(revision.origins, value.id) === IngredientPropertyState.Contains);
    return contained.length === 1 ? contained[0].id : null;
  }
  primaryOriginLabel(revision: IngredientRevisionDetails) {
    const selectedId = this.primaryOriginId(revision);
    return this.primaryOrigins().find(value => value.id === selectedId)?.name ?? 'Bitte auswählen';
  }
  mainAllergens() {
    return (this.referenceData()?.allergens ?? []).filter(value => value.isEuMajorAllergen)
      .sort((left, right) => this.allergenLetter(left.code).localeCompare(this.allergenLetter(right.code)));
  }
  allergenChildren(parentId: string) {
    return (this.referenceData()?.allergens ?? []).filter(value => value.parentAllergenId === parentId);
  }
  allergenLetter(code: string) { return this.allergenLetters[code] ?? '?'; }
  propertyStateLabel(state: IngredientPropertyState | null) {
    return this.propertyStates.find(value => value.value === state)?.label ?? 'Nicht angegeben';
  }
  propertyState(values: IngredientRevisionPropertyItem[], propertyId: string) {
    return values.find(value => value.propertyId === propertyId)?.state ?? null;
  }

  private effectiveVariantPropertyState(
    baseValues: IngredientRevisionPropertyItem[],
    overrides: IngredientRevisionPropertyItem[],
    propertyId: string)
  {
    return this.propertyState(overrides, propertyId) ?? this.propertyState(baseValues, propertyId);
  }

  variantVisibleAllergens(revision: IngredientRevisionDetails, variant: IngredientVariantRevisionItem) {
    return this.mainAllergens().flatMap(parent => {
      const children = this.allergenChildren(parent.id);
      const parentState = this.effectiveVariantPropertyState(
        revision.allergens, variant.allergenOverrides, parent.id);
      const visibleChildren = parentState === IngredientPropertyState.Contains ||
        children.some(child => this.propertyState(variant.allergenOverrides, child.id) !== null)
        ? children : [];
      return [parent, ...visibleChildren];
    });
  }

  variantOverrideCount(variant: IngredientVariantRevisionItem) {
    return variant.allergenOverrides.length + variant.intoleranceOverrides.length +
      variant.originOverrides.length + variant.unitConversionOverrides.length +
      (variant.nutritionProfile ? 1 : 0);
  }

  setNutritionEnabled(revision: IngredientRevisionDetails, enabled: boolean) {
    revision.nutritionProfile = enabled ? this.newNutritionProfile(revision.baseUnitId) : null;
    this.synchronizeSourceSummary(revision);
  }

  setVariantNutritionEnabled(
    revision: IngredientRevisionDetails,
    variant: IngredientVariantRevisionItem,
    enabled: boolean)
  {
    if (!enabled) {
      variant.nutritionProfile = null;
      return;
    }
    variant.nutritionProfile = revision.nutritionProfile
      ? { ...revision.nutritionProfile, reviewState: IngredientNutritionReviewState.Unreviewed }
      : this.newNutritionProfile(revision.baseUnitId);
    this.synchronizeSourceSummary(revision);
  }

  synchronizeSourceSummary(revision: IngredientRevisionDetails) {
    const technicalReference = (revision.sourceSummary ?? '').trim().slice(0, 500);
    const apply = (profile: IngredientNutritionProfileItem | null) => {
      if (!profile) return;
      profile.sourceType = IngredientNutritionSourceType.ManualEstimate;
      profile.sourceReference = technicalReference;
    };
    apply(revision.nutritionProfile);
    for (const content of revision.substanceContents) {
      content.sourceType = IngredientSubstanceContentSourceType.ManualEstimate;
      content.sourceReference = technicalReference;
    }
    for (const variant of revision.variants) {
      apply(variant.nutritionProfile);
      for (const content of variant.substanceContentOverrides) {
        content.sourceType = IngredientSubstanceContentSourceType.ManualEstimate;
        content.sourceReference = technicalReference;
      }
    }
  }

  setSourceEntry(revision: IngredientRevisionDetails, index: number, value: string) {
    const entries = [...this.sourceEntries()];
    if (index < 0 || index >= entries.length) return;
    entries[index] = value;
    this.persistSourceEntries(revision, entries);
  }

  addSourceEntry(revision: IngredientRevisionDetails) {
    const source = this.newSourceEntry().trim();
    if (!source) return;
    const entries = this.sourceEntries();
    if (entries.some(value => this.normalizeSourceEntry(value) === this.normalizeSourceEntry(source))) {
      this.notice.set('Diese Quelle ist bereits eingetragen.');
      this.newSourceEntry.set('');
      return;
    }
    if (this.persistSourceEntries(revision, [...entries, source]))
      this.newSourceEntry.set('');
  }

  removeSourceEntry(revision: IngredientRevisionDetails, index: number) {
    this.persistSourceEntries(revision, this.sourceEntries().filter((_, entryIndex) => entryIndex !== index));
  }

  searchSuggestions() {
    const query = this.suggestionQuery().trim();
    if (query.length < 2 || this.suggestionLoading()) return;
    this.suggestionLoading.set(true);
    this.suggestionSearched.set(true);
    this.suggestionError.set('');
    this.api.searchSuggestions('BLS', query).subscribe({
      next: result => {
        this.suggestionLoading.set(false);
        this.suggestionResults.set(result.suggestions);
        if (!result.isAvailable)
          this.suggestionError.set('Der lokale BLS-Referenzindex ist noch nicht eingerichtet.');
      },
      error: () => {
        this.suggestionLoading.set(false);
        this.suggestionResults.set([]);
        this.suggestionError.set('Die BLS-Referenzwerte konnten nicht geladen werden.');
      }
    });
  }

  canApplyBlsSuggestion(revision: IngredientRevisionDetails) {
    return this.unitDimension(revision.baseUnitId) === 0;
  }

  hasSuggestedNutrition(suggestion: IngredientDataSuggestion) {
    return Object.values(suggestion.nutrition).some(value => value !== null);
  }

  suggestionNutritionSummary(suggestion: IngredientDataSuggestion) {
    const nutrition = suggestion.nutrition;
    const values = [
      ['Energie', nutrition.energyKilojoules, 'kJ'],
      ['Fett', nutrition.fatGrams, 'g'],
      ['gesättigt', nutrition.saturatedFatGrams, 'g'],
      ['Kohlenhydrate', nutrition.carbohydrateGrams, 'g'],
      ['Zucker', nutrition.sugarsGrams, 'g'],
      ['Eiweiß', nutrition.proteinGrams, 'g'],
      ['Salz', nutrition.saltGrams, 'g'],
      ['Ballaststoffe', nutrition.fiberGrams, 'g']
    ] as const;
    const present = values
      .filter(([, value]) => value !== null)
      .map(([label, value, unit]) => `${label}: ${this.formatSuggestionValue(value!)} ${unit}`);
    return present.length ? present.join(' · ') : 'Keine Nährwerte im Referenzeintrag verfügbar.';
  }

  suggestionSubstanceSummary(suggestion: IngredientDataSuggestion) {
    return suggestion.substanceContents.map(value => {
      const name = this.referenceData()?.intolerances.find(item => item.code === value.code)?.name ?? value.code;
      return `${name}: ${this.formatSuggestionValue(value.amountGrams)} g`;
    }).join(' · ');
  }

  applySuggestionNutrition(revision: IngredientRevisionDetails, suggestion: IngredientDataSuggestion) {
    if (!this.canApplyBlsSuggestion(revision) || !this.hasSuggestedNutrition(suggestion)) return;
    const gramUnit = this.referenceData()?.units.find(value => value.dimension === 0 && value.symbol === 'g');
    if (!gramUnit) return;
    const profile = revision.nutritionProfile ?? this.newNutritionProfile(revision.baseUnitId);
    profile.referenceQuantity = suggestion.referenceQuantity;
    profile.referenceUnitId = gramUnit.id;
    const nutrition = suggestion.nutrition;
    if (nutrition.energyKilojoules !== null) profile.energyKilojoules = nutrition.energyKilojoules;
    if (nutrition.fatGrams !== null) profile.fatGrams = nutrition.fatGrams;
    if (nutrition.saturatedFatGrams !== null) profile.saturatedFatGrams = nutrition.saturatedFatGrams;
    if (nutrition.carbohydrateGrams !== null) profile.carbohydrateGrams = nutrition.carbohydrateGrams;
    if (nutrition.sugarsGrams !== null) profile.sugarsGrams = nutrition.sugarsGrams;
    if (nutrition.proteinGrams !== null) profile.proteinGrams = nutrition.proteinGrams;
    if (nutrition.saltGrams !== null) profile.saltGrams = nutrition.saltGrams;
    if (nutrition.fiberGrams !== null) profile.fiberGrams = nutrition.fiberGrams;
    profile.reviewState = IngredientNutritionReviewState.Unreviewed;
    profile.referenceDate = null;
    revision.nutritionProfile = profile;
    this.appendSuggestionSource(revision, suggestion.sourceSummary);
    this.notice.set(`Nährwerte aus „${suggestion.name}“ wurden als ungeprüfte Schätzung übernommen.`);
  }

  applySuggestionSubstances(revision: IngredientRevisionDetails, suggestion: IngredientDataSuggestion) {
    if (!this.canApplyBlsSuggestion(revision) || !suggestion.substanceContents.length) return;
    const gramUnit = this.referenceData()?.units.find(value => value.dimension === 0 && value.symbol === 'g');
    if (!gramUnit) return;
    const definitions = new Map((this.referenceData()?.intolerances ?? [])
      .filter(value => value.isQuantityDependent).map(value => [value.code, value]));
    for (const suggested of suggestion.substanceContents) {
      const definition = definitions.get(suggested.code);
      if (!definition) continue;
      revision.intolerances = revision.intolerances.filter(value => value.propertyId !== definition.id);
      let content = this.substanceContent(revision, definition.id);
      if (!content) {
        content = {
          substanceId: definition.id,
          amount: suggested.amountGrams,
          amountUnitId: gramUnit.id,
          referenceQuantity: suggestion.referenceQuantity,
          referenceUnitId: gramUnit.id,
          sourceType: IngredientSubstanceContentSourceType.ManualEstimate,
          sourceReference: '',
          reviewState: IngredientSubstanceContentReviewState.Unreviewed
        };
        revision.substanceContents.push(content);
      } else {
        content.amount = suggested.amountGrams;
        content.amountUnitId = gramUnit.id;
        content.referenceQuantity = suggestion.referenceQuantity;
        content.referenceUnitId = gramUnit.id;
        content.reviewState = IngredientSubstanceContentReviewState.Unreviewed;
      }
    }
    this.appendSuggestionSource(revision, suggestion.sourceSummary);
    this.notice.set(`Stoffgehalte aus „${suggestion.name}“ wurden als ungeprüfte Schätzung übernommen.`);
  }

  private appendSuggestionSource(revision: IngredientRevisionDetails, source: string) {
    const entries = this.parseSourceEntries(revision.sourceSummary);
    const key = this.normalizeSourceEntry(source);
    if (!entries.some(value => this.normalizeSourceEntry(value) === key))
      this.persistSourceEntries(revision, [...entries, source.trim()]);
    else {
      this.sourceEntries.set(entries);
      this.synchronizeSourceSummary(revision);
    }
  }

  private persistSourceEntries(revision: IngredientRevisionDetails, entries: string[]) {
    const summary = entries.map(value => value.trim()).filter(Boolean).join('\n');
    if (summary.length > 2000) {
      this.error.set('Die Quellenangaben dürfen zusammen höchstens 2.000 Zeichen enthalten.');
      return false;
    }
    this.sourceEntries.set(entries);
    revision.sourceSummary = summary;
    this.error.set('');
    this.synchronizeSourceSummary(revision);
    return true;
  }

  private parseSourceEntries(summary: string) {
    const separator = summary.includes('\n') ? /\r?\n/ : /\s*;\s*/;
    const result: string[] = [];
    const keys = new Set<string>();
    for (const value of summary.split(separator).map(item => item.trim()).filter(Boolean)) {
      const key = this.normalizeSourceEntry(value);
      if (keys.has(key)) continue;
      keys.add(key);
      result.push(value);
    }
    return result;
  }

  private normalizeSourceEntry(value: string) {
    return value.normalize('NFKC').trim().replace(/\s+/g, ' ').toLocaleLowerCase('de');
  }

  formatSuggestionValue(value: number) {
    return new Intl.NumberFormat('de-DE', { maximumFractionDigits: 3 }).format(value);
  }

  setVariantPropertyState(
    revision: IngredientRevisionDetails,
    variant: IngredientVariantRevisionItem,
    group: 'allergens' | 'intolerances' | 'origins',
    propertyId: string,
    state: IngredientPropertyState | null)
  {
    const values = group === 'allergens' ? variant.allergenOverrides :
      group === 'intolerances' ? variant.intoleranceOverrides : variant.originOverrides;
    this.setPropertyValue(values, propertyId, state, IngredientPropertySource.ManuallyVerified);
    if (group === 'allergens') {
      const children = this.allergenChildren(propertyId);
      if (children.length && state === null) {
        for (const child of children) {
          const inherited = variant.allergenOverrides.find(value => value.propertyId === child.id);
          if (inherited?.source === IngredientPropertySource.Derived)
            this.setPropertyValue(values, child.id, null, IngredientPropertySource.Derived);
        }
      } else if (children.length && state !== IngredientPropertyState.Contains) {
        for (const child of children)
          this.setPropertyValue(values, child.id, state, IngredientPropertySource.Derived);
      } else if (children.length) {
        for (const child of children) {
          const existing = variant.allergenOverrides.find(value => value.propertyId === child.id);
          if (!existing || existing.source === IngredientPropertySource.Derived)
            this.setPropertyValue(values, child.id,
              IngredientPropertyState.Unknown, IngredientPropertySource.Derived);
        }
      }
      revision.allergenReviewState = IngredientPropertyReviewState.Unreviewed;
    }
    else if (group === 'intolerances') revision.intoleranceReviewState = IngredientPropertyReviewState.Unreviewed;
    else revision.originReviewState = IngredientPropertyReviewState.Unreviewed;
  }

  variantPrimaryOriginOverrideId(variant: IngredientVariantRevisionItem) {
    const primaryIds = new Set(this.primaryOrigins().map(value => value.id));
    return variant.originOverrides.find(value => primaryIds.has(value.propertyId) &&
      value.state === IngredientPropertyState.Contains)?.propertyId ?? null;
  }

  setVariantPrimaryOrigin(
    revision: IngredientRevisionDetails,
    variant: IngredientVariantRevisionItem,
    propertyId: string | null)
  {
    for (const property of this.primaryOrigins()) {
      if (propertyId === null)
        this.setPropertyValue(variant.originOverrides, property.id, null, IngredientPropertySource.Derived);
      else
        this.setPropertyValue(variant.originOverrides, property.id,
          property.id === propertyId ? IngredientPropertyState.Contains : IngredientPropertyState.DoesNotContain,
          property.id === propertyId ? IngredientPropertySource.ManuallyVerified : IngredientPropertySource.Derived);
    }
    revision.originReviewState = IngredientPropertyReviewState.Unreviewed;
  }

  variantConversionOverride(variant: IngredientVariantRevisionItem, sourceUnitId: string) {
    return variant.unitConversionOverrides.find(value => value.sourceUnitId === sourceUnitId) ?? null;
  }

  hasVariantConversionOverride(variant: IngredientVariantRevisionItem, sourceUnitId: string) {
    return this.variantConversionOverride(variant, sourceUnitId) !== null;
  }

  setVariantConversionOverride(
    variant: IngredientVariantRevisionItem,
    baseConversion: IngredientRevisionUnitConversionItem,
    enabled: boolean)
  {
    const index = variant.unitConversionOverrides.findIndex(
      value => value.sourceUnitId === baseConversion.sourceUnitId);
    if (!enabled) {
      if (index >= 0) variant.unitConversionOverrides.splice(index, 1);
      return;
    }
    if (index >= 0) return;
    variant.unitConversionOverrides.push({
      sourceUnitId: baseConversion.sourceUnitId,
      factorToBaseUnit: baseConversion.factorToBaseUnit,
      precision: baseConversion.precision,
      factorInput: baseConversion.factorInput ?? String(baseConversion.factorToBaseUnit).replace('.', ',')
    });
  }

  unitSymbol(unitId: string) {
    return this.referenceData()?.units.find(value => value.id === unitId)?.symbol ?? '?';
  }

  categoryName(categoryId: string) {
    return this.referenceData()?.categories.find(value => value.id === categoryId)?.name ?? 'Unbekannte Kategorie';
  }

  availableConversionUnits(revision: IngredientRevisionDetails, currentSourceUnitId?: string) {
    const selectedIds = new Set(revision.unitConversions
      .filter(value => value.sourceUnitId !== currentSourceUnitId)
      .map(value => value.sourceUnitId));
    return (this.referenceData()?.units ?? [])
      .filter(value => this.isAllowedConversionUnit(revision.baseUnitId, value.id) && !selectedIds.has(value.id));
  }

  setBaseUnit(revision: IngredientRevisionDetails, baseUnitId: string) {
    const previousDimension = this.unitDimension(revision.baseUnitId);
    revision.baseUnitId = baseUnitId;
    revision.unitConversions = revision.unitConversions
      .filter(value => this.isAllowedConversionUnit(baseUnitId, value.sourceUnitId));
    this.removeOrphanedVariantConversions(revision);
    if (previousDimension !== this.unitDimension(baseUnitId)) {
      if (revision.nutritionProfile)
        this.resetNutritionReference(revision.nutritionProfile, baseUnitId);
      for (const variant of revision.variants)
        if (variant.nutritionProfile)
          this.resetNutritionReference(variant.nutritionProfile, baseUnitId);
    }
  }

  addUnitConversion(revision: IngredientRevisionDetails) {
    const unit = this.availableConversionUnits(revision)[0];
    if (!unit) return;
    revision.unitConversions.push({
      sourceUnitId: unit.id,
      factorToBaseUnit: 1,
      precision: IngredientConversionPrecision.Average,
      factorInput: '1'
    });
    this.unitPage.set(Math.floor((revision.unitConversions.length - 1) / 5));
  }

  removeUnitConversion(revision: IngredientRevisionDetails, index: number) {
    revision.unitConversions.splice(index, 1);
    this.removeOrphanedVariantConversions(revision);
  }

  setConversionFactor(conversion: IngredientRevisionUnitConversionItem, input: string) {
    conversion.factorInput = input;
    const parsed = Number(input.trim().replace(',', '.'));
    conversion.factorToBaseUnit = Number.isFinite(parsed) && parsed > 0 ? parsed : Number.NaN;
  }

  normalizeConversionFactorInput(conversion: IngredientRevisionUnitConversionItem) {
    if (Number.isFinite(conversion.factorToBaseUnit) && conversion.factorToBaseUnit > 0)
      conversion.factorInput = String(conversion.factorToBaseUnit).replace('.', ',');
  }

  unitConversionsValid(revision: IngredientRevisionDetails) {
    return revision.unitConversions.every(value =>
      Number.isFinite(value.factorToBaseUnit) && value.factorToBaseUnit > 0 &&
      this.isAllowedConversionUnit(revision.baseUnitId, value.sourceUnitId));
  }

  nutritionProfilesValid(revision: IngredientRevisionDetails) {
    return this.nutritionProfileValid(revision.nutritionProfile, revision.baseUnitId) &&
      revision.variants.every(value => this.nutritionProfileValid(
        value.nutritionProfile, revision.baseUnitId));
  }

  substanceContentsValid(revision: IngredientRevisionDetails, requireReviewed: boolean) {
    const values = revision.substanceContents.concat(
      revision.variants.flatMap(value => value.substanceContentOverrides));
    return values.every(value => Number.isFinite(value.amount) && value.amount >= 0 &&
      Number.isFinite(value.referenceQuantity) && value.referenceQuantity > 0 &&
      this.unitDimension(value.amountUnitId) === 0 &&
      this.unitDimension(value.referenceUnitId) === this.unitDimension(revision.baseUnitId) &&
      !!value.sourceReference.trim() && value.sourceReference.trim().length <= 500 &&
      (!requireReviewed || value.reviewState === IngredientSubstanceContentReviewState.Reviewed));
  }

  private nutritionProfileValid(profile: IngredientNutritionProfileItem | null, baseUnitId: string) {
    if (!profile) return true;
    const numbers = [profile.energyKilojoules, profile.fatGrams, profile.saturatedFatGrams,
      profile.carbohydrateGrams, profile.sugarsGrams, profile.proteinGrams,
      profile.saltGrams, profile.fiberGrams];
    const core = numbers.slice(0, 7);
    return Number.isFinite(profile.referenceQuantity) && profile.referenceQuantity > 0 &&
      this.unitDimension(profile.referenceUnitId) === this.unitDimension(baseUnitId) &&
      !!profile.sourceReference.trim() && profile.sourceReference.trim().length <= 500 &&
      numbers.every(value => value === null || Number.isFinite(value) && value >= 0) &&
      (profile.saturatedFatGrams === null || profile.fatGrams === null ||
        profile.saturatedFatGrams <= profile.fatGrams) &&
      (profile.sugarsGrams === null || profile.carbohydrateGrams === null ||
        profile.sugarsGrams <= profile.carbohydrateGrams) &&
      (profile.reviewState !== IngredientNutritionReviewState.Reviewed ||
        core.every(value => value !== null && Number.isFinite(value)));
  }

  variantsValid(revision: IngredientRevisionDetails) {
    const names = revision.variants.map(value => value.name.trim().toLocaleUpperCase('de'));
    const keys = revision.variants.map(value => value.variantKey);
    const baseConversionIds = new Set(revision.unitConversions.map(value => value.sourceUnitId));
    return revision.variants.every(value => !!value.name.trim() && !!value.variantKey) &&
      revision.variants.every(value => value.unitConversionOverrides.every(conversion =>
        baseConversionIds.has(conversion.sourceUnitId) && Number.isFinite(conversion.factorToBaseUnit) &&
        conversion.factorToBaseUnit > 0)) &&
      new Set(names).size === names.length && new Set(keys).size === keys.length;
  }

  addVariant(revision: IngredientRevisionDetails) {
    const id = crypto.randomUUID();
    this.selectedVariantId.set(id);
    revision.variants.push({
      id,
      variantKey: '',
      name: '',
      isActive: true,
      sortOrder: Math.max(-1, ...revision.variants.map(value => value.sortOrder)) + 1,
      allergenOverrides: [],
      intoleranceOverrides: [],
      substanceContentOverrides: [],
      originOverrides: [],
      unitConversionOverrides: [],
      nutritionProfile: null,
      isNew: true
    });
  }

  setVariantName(
    revision: IngredientRevisionDetails,
    variant: IngredientVariantRevisionItem,
    name: string)
  {
    variant.name = name;
    if (variant.isNew)
      variant.variantKey = this.uniqueVariantKey(revision, variant, name);
  }

  removeVariant(revision: IngredientRevisionDetails, index: number) {
    if (!revision.variants[index]) return;
    revision.variants.splice(index, 1);
    revision.variants.forEach((variant, sortOrder) => variant.sortOrder = sortOrder);
  }

  private isAllowedConversionUnit(baseUnitId: string, sourceUnitId: string) {
    const units = this.referenceData()?.units ?? [];
    const baseUnit = units.find(value => value.id === baseUnitId);
    const sourceUnit = units.find(value => value.id === sourceUnitId);
    if (!baseUnit || !sourceUnit || baseUnit.id === sourceUnit.id) return false;
    const kitchenMeasureSymbols = new Set(['TL', 'EL', 'Prise', 'Bund']);
    return kitchenMeasureSymbols.has(sourceUnit.symbol) || sourceUnit.dimension !== baseUnit.dimension;
  }

  private newNutritionProfile(baseUnitId: string): IngredientNutritionProfileItem {
    const reference = this.defaultNutritionReference(baseUnitId);
    return {
      referenceQuantity: reference.quantity,
      referenceUnitId: reference.unitId,
      energyKilojoules: null,
      fatGrams: null,
      saturatedFatGrams: null,
      carbohydrateGrams: null,
      sugarsGrams: null,
      proteinGrams: null,
      saltGrams: null,
      fiberGrams: null,
      sourceType: IngredientNutritionSourceType.ManualEstimate,
      sourceReference: '',
      reviewState: IngredientNutritionReviewState.Unreviewed,
      referenceDate: null
    };
  }

  private resetNutritionReference(profile: IngredientNutritionProfileItem, baseUnitId: string) {
    const reference = this.defaultNutritionReference(baseUnitId);
    profile.referenceQuantity = reference.quantity;
    profile.referenceUnitId = reference.unitId;
    profile.reviewState = IngredientNutritionReviewState.Unreviewed;
  }

  private defaultNutritionReference(baseUnitId: string) {
    const units = this.referenceData()?.units ?? [];
    const dimension = this.unitDimension(baseUnitId);
    const preferredSymbol = dimension === 0 ? 'g' : dimension === 1 ? 'ml' : 'Stk.';
    const unit = units.find(value => value.dimension === dimension && value.symbol === preferredSymbol) ??
      units.find(value => value.id === baseUnitId);
    return { unitId: unit?.id ?? baseUnitId, quantity: dimension === 2 ? 1 : 100 };
  }

  private unitDimension(unitId: string) {
    return this.referenceData()?.units.find(value => value.id === unitId)?.dimension ?? -1;
  }

  private removeOrphanedVariantConversions(revision: IngredientRevisionDetails) {
    const available = new Set(revision.unitConversions.map(value => value.sourceUnitId));
    for (const variant of revision.variants)
      variant.unitConversionOverrides = variant.unitConversionOverrides
        .filter(value => available.has(value.sourceUnitId));
  }

  setPropertyState(
    revision: IngredientRevisionDetails,
    group: 'allergens' | 'intolerances' | 'origins',
    propertyId: string,
    state: IngredientPropertyState | null)
  {
    const values = revision[group];
    const previousState = this.propertyState(values, propertyId);
    this.setPropertyValue(values, propertyId, state, IngredientPropertySource.ManuallyVerified);
    if (group === 'allergens') {
      const definition = this.referenceData()?.allergens.find(value => value.id === propertyId);
      const children = definition?.parentAllergenId ? [] : this.allergenChildren(propertyId);
      if (children.length && state !== IngredientPropertyState.Contains) {
        for (const child of children)
          this.setPropertyValue(values, child.id, state, IngredientPropertySource.Derived);
      } else if (children.length && previousState !== IngredientPropertyState.Contains) {
        for (const child of children)
          this.setPropertyValue(values, child.id, IngredientPropertyState.Unknown, IngredientPropertySource.Derived);
      }
    }
    if (group === 'allergens') revision.allergenReviewState = IngredientPropertyReviewState.Unreviewed;
    else if (group === 'intolerances') revision.intoleranceReviewState = IngredientPropertyReviewState.Unreviewed;
    else revision.originReviewState = IngredientPropertyReviewState.Unreviewed;
  }

  setIntoleranceReviewState(revision: IngredientRevisionDetails, reviewed: boolean) {
    if (reviewed) {
      for (const property of this.qualitativeIntolerances()) {
        if (this.propertyState(revision.intolerances, property.id) === null)
          this.setPropertyValue(revision.intolerances, property.id,
            IngredientPropertyState.Unknown, IngredientPropertySource.Derived);
      }
    }
    revision.intoleranceReviewState = this.reviewState(reviewed);
  }

  setPrimaryOrigin(revision: IngredientRevisionDetails, propertyId: string | null) {
    if (!propertyId) return;
    this.applyPrimaryOrigin(revision, propertyId, IngredientPropertySource.ManuallyVerified);
    revision.originReviewState = IngredientPropertyReviewState.Unreviewed;
  }

  setOriginReviewState(revision: IngredientRevisionDetails, reviewed: boolean) {
    if (reviewed) {
      if (!this.primaryOriginId(revision)) return;
      for (const property of this.additionalOrigins()) {
        if (this.propertyState(revision.origins, property.id) === null)
          this.setPropertyValue(revision.origins, property.id,
            IngredientPropertyState.DoesNotContain, IngredientPropertySource.Derived);
      }
    }
    revision.originReviewState = this.reviewState(reviewed);
  }

  openCreate() {
    this.createName = '';
    this.createCategoryId = this.referenceData()?.categories[0]?.id ?? '';
    this.createBaseUnitId = this.baseUnits()[0]?.id ?? '';
    this.createOpen.set(true); this.error.set(''); this.notice.set('');
  }

  create() {
    if (!this.canCreate() || this.submitting()) return;
    this.submitting.set(true); this.error.set('');
    const request = { name: this.createName.trim(), categoryId: this.createCategoryId,
      baseUnitId: this.createBaseUnitId };
    const creation = this.scope() === 'central'
      ? this.api.createCentral(request)
      : this.scope() === 'tenant'
        ? this.api.createTenant(this.tenantId(), request)
        : this.api.createCamp(this.campId(), request);
    creation.subscribe({
      next: result => { this.submitting.set(false); this.createOpen.set(false); this.notice.set('Der Zutatenentwurf wurde angelegt.');
        this.refreshList(result.revisionId); },
      error: () => { this.submitting.set(false); this.error.set('Der Zutatenentwurf konnte nicht angelegt werden.'); }
    });
  }

  open(revisionId: string, variantId?: string | null) {
    this.pendingForkSourceRevisionId.set(null);
    this.unitPage.set(0); this.allergenPage.set(0); this.substancePage.set(0); this.sourcePage.set(0);
    this.error.set(''); this.notice.set('');
    this.api.get(revisionId).subscribe({ next: value => { this.initializeConversionFactorInputs(value);
      this.initializeNutritionProfiles(value);
      this.selectedSnapshot = this.snapshot(value);
      if (value.state === this.draftState) {
        this.normalizeAllergenDetails(value);
        this.normalizeQualitativeIntolerances(value);
        this.normalizePrimaryOrigin(value);
      }
      this.selected.set(value);
      if (variantId !== undefined) { this.selectedVariantId.set(variantId); this.activeTab.set(variantId ? 'variants' : 'basic'); }
      this.sourceEntries.set(this.parseSourceEntries(value.sourceSummary));
      this.newSourceEntry.set('');
      this.resetSuggestionSearch(value.name); },
      error: () => this.error.set('Die Zutatenrevision konnte nicht geladen werden.') });
  }

  prepareCampFork(sourceRevisionId: string) {
    if (this.submitting() || this.disabled()) return;
    this.submitting.set(true); this.error.set(''); this.notice.set('');
    this.api.getCampForkPreview(this.campId(), sourceRevisionId).subscribe({
      next: value => {
        value.state = this.draftState;
        this.initializeConversionFactorInputs(value);
        this.initializeNutritionProfiles(value);
        this.selectedSnapshot = this.snapshot(value);
        this.pendingForkSourceRevisionId.set(sourceRevisionId);
        this.selected.set(value);
        this.sourceEntries.set(this.parseSourceEntries(value.sourceSummary));
        this.newSourceEntry.set('');
        this.resetSuggestionSearch(value.name);
        this.submitting.set(false);
        this.notice.set('Die zentrale Zutat wurde zur Anpassung geöffnet. Noch wurde keine Lagerkopie angelegt.');
        this.host.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'start' });
      },
      error: () => { this.submitting.set(false);
        this.error.set('Die zentrale Zutat konnte nicht zur Anpassung geöffnet werden.'); }
    });
  }

  private resetSuggestionSearch(name: string) {
    this.suggestionQuery.set(name);
    this.suggestionResults.set([]);
    this.suggestionSearched.set(false);
    this.suggestionError.set('');
  }

  save() {
    const revision = this.selected(); if (!revision || revision.state !== this.draftState) return;
    this.submitting.set(true); this.error.set(''); this.notice.set('');
    this.synchronizeSourceSummary(revision);
    const payload = { name: revision.name, categoryId: revision.categoryId, baseUnitId: revision.baseUnitId,
      allergenReviewState: revision.allergenReviewState, intoleranceReviewState: revision.intoleranceReviewState,
      originReviewState: revision.originReviewState, sourceSummary: revision.sourceSummary,
      allergens: revision.allergens, intolerances: revision.intolerances, origins: revision.origins,
      substanceContents: revision.substanceContents,
      nutritionProfile: revision.nutritionProfile,
      unitConversions: revision.unitConversions.map(value => ({
        sourceUnitId: value.sourceUnitId,
        factorToBaseUnit: value.factorToBaseUnit,
        precision: value.precision
      })), variants: revision.variants.map(value => ({
        id: value.id,
        variantKey: value.variantKey,
        name: value.name,
        isActive: value.isActive,
        sortOrder: value.sortOrder,
        allergenOverrides: value.allergenOverrides,
        intoleranceOverrides: value.intoleranceOverrides,
        substanceContentOverrides: value.substanceContentOverrides,
        originOverrides: value.originOverrides,
        nutritionProfile: value.nutritionProfile,
        unitConversionOverrides: value.unitConversionOverrides.map(conversion => ({
          sourceUnitId: conversion.sourceUnitId,
          factorToBaseUnit: conversion.factorToBaseUnit,
          precision: conversion.precision
        }))
      })) };
    const sourceRevisionId = this.pendingForkSourceRevisionId();
    if (sourceRevisionId) {
      this.api.createCampFork(this.campId(), sourceRevisionId,
        { ...payload, expectedSourceRowVersion: revision.rowVersion }).subscribe({
        next: result => {
          this.pendingForkSourceRevisionId.set(null);
          this.submitting.set(false);
          this.notice.set('Die angepasste Lagerzutat wurde als Entwurf angelegt.');
          if (result.revisionId) this.refreshList(result.revisionId);
        },
        error: (error: HttpErrorResponse) => {
          this.submitting.set(false);
          if (error.status === 409 && error.error?.code === 'ingredient_revision_draft_exists') {
            this.pendingForkSourceRevisionId.set(null);
            this.notice.set('Für diese zentrale Zutat besteht bereits eine Lageranpassung.');
            this.refreshList(error.error?.revisionId as string | undefined);
          } else if (error.status === 409 && error.error?.code === 'ingredient_fork_requires_changes') {
            this.error.set('Ändere mindestens eine Angabe, bevor eine Lagerkopie angelegt wird.');
          } else this.error.set('Die Lageranpassung konnte nicht gespeichert werden.');
        }
      });
      return;
    }
    this.api.save(revision.id, { ...payload, expectedRowVersion: revision.rowVersion }).subscribe({
      next: result => { revision.rowVersion = result.rowVersion; this.selectedSnapshot = this.snapshot(revision);
        revision.variants.forEach(value => value.isNew = false);
        this.selected.set({ ...revision }); this.submitting.set(false);
        this.notice.set('Der Entwurf wurde gespeichert.'); this.refreshList(revision.id, false); },
      error: error => this.handleMutationError(error)
    });
  }

  publish() {
    const revision = this.selected();
    if (!revision || this.isDirty(revision) || !this.allReviewed(revision) ||
        !this.unitConversionsValid(revision) || !this.nutritionProfilesValid(revision) ||
        !this.substanceContentsValid(revision, true) ||
        !this.variantsValid(revision)) return;
    if (this.scope() !== 'central') {
      this.submitting.set(true); this.error.set(''); this.notice.set('');
      this.api.findCentralCandidates(revision.id).subscribe({
        next: candidates => {
          this.submitting.set(false);
          if (candidates.length) this.centralCandidates.set(candidates);
          else this.publishAnyway();
        },
        error: () => { this.submitting.set(false);
          this.error.set('Passende zentrale Zutaten konnten nicht geprüft werden.'); }
      });
      return;
    }
    this.publishAnyway();
  }

  findCentralAlternatives() {
    const revision = this.selected();
    if (!revision || revision.state !== this.publishedState || this.scope() === 'central' || this.submitting()) return;
    this.submitting.set(true); this.error.set(''); this.notice.set('');
    this.api.findCentralCandidates(revision.id).subscribe({
      next: candidates => {
        this.submitting.set(false); this.centralCandidates.set(candidates);
        if (!candidates.length) this.notice.set('Es wurde keine passende zentrale Zutat gefunden.');
      },
      error: () => { this.submitting.set(false);
        this.error.set('Passende zentrale Zutaten konnten nicht geprüft werden.'); }
    });
  }

  publishAnyway() {
    const revision = this.selected();
    if (!revision || this.isDirty(revision) || !this.allReviewed(revision) ||
        !this.unitConversionsValid(revision) || !this.nutritionProfilesValid(revision) ||
        !this.substanceContentsValid(revision, true) ||
        !this.variantsValid(revision)) return;
    this.submitting.set(true); this.error.set(''); this.notice.set('');
    this.api.publish(revision.id, revision.rowVersion).subscribe({
      next: result => { this.submitting.set(false); revision.rowVersion = result.rowVersion;
        revision.state = this.publishedState; this.selectedSnapshot = this.snapshot(revision);
        this.centralCandidates.set([]);
        this.selected.set({ ...revision }); this.notice.set('Die Zutat wurde veröffentlicht.');
        this.published.emit();
        this.refreshList(revision.id, false); },
      error: error => this.handleMutationError(error)
    });
  }

  replaceWithCentral(candidate: CentralIngredientCandidate) {
    const revision = this.selected();
    if (!revision || this.scope() === 'central' || this.submitting()) return;
    this.submitting.set(true); this.error.set(''); this.notice.set('');
    this.api.replaceWithCentral(revision.id, candidate.revisionId).subscribe({
      next: () => {
        this.submitting.set(false); this.centralCandidates.set([]); this.selected.set(null);
        this.notice.set(`Die lokale Zutat wurde durch „${candidate.name}“ abgelöst.`);
        this.refreshList(); this.published.emit();
      },
      error: () => { this.submitting.set(false);
        this.error.set('Die lokale Zutat konnte nicht durch die zentrale Zutat abgelöst werden.'); }
    });
  }

  submitToCentral() {
    const revision = this.selected();
    if (!revision || revision.state !== this.publishedState || this.scope() === 'central' || this.submitting()) return;
    this.submitting.set(true); this.error.set(''); this.notice.set('');
    this.api.submitToCentral(revision.id).subscribe({
      next: () => { this.submitting.set(false);
        this.notice.set('Die veröffentlichte Revision wurde zur zentralen Prüfung eingereicht.'); },
      error: (error: HttpErrorResponse) => {
        this.submitting.set(false);
        if (error.status === 409 && error.error?.code === 'ingredient_revision_already_submitted')
          this.notice.set('Diese veröffentlichte Revision wurde bereits eingereicht.');
        else this.error.set('Die Zutat konnte nicht zur zentralen Prüfung eingereicht werden.');
      }
    });
  }

  publishedCentralRevisions() {
    return this.revisions().filter(value => value.state === this.publishedState);
  }

  contributionTarget(contribution: IngredientCentralContribution) {
    return this.contributionTargets()[contribution.id] ?? contribution.suggestedCentralIngredientId ?? '';
  }

  setContributionTarget(contributionId: string, target: string) {
    this.contributionTargets.update(values => ({ ...values, [contributionId]: target }));
  }

  acceptContribution(contribution: IngredientCentralContribution) {
    if (this.submitting()) return;
    const target = this.contributionTarget(contribution) || null;
    this.submitting.set(true); this.error.set(''); this.notice.set('');
    this.api.acceptCentralContribution(contribution.id, target).subscribe({
      next: result => {
        this.submitting.set(false);
        this.notice.set('Die Einreichung wurde als ungeprüfter zentraler Entwurf übernommen.');
        this.loadContributions();
        this.refreshList(result.centralRevisionId);
      },
      error: (error: HttpErrorResponse) => {
        this.submitting.set(false);
        this.error.set(error.status === 409 && error.error?.code === 'central_ingredient_draft_exists'
          ? 'Für die gewählte zentrale Zutat besteht bereits ein Entwurf.'
          : 'Die Einreichung konnte nicht übernommen werden.');
      }
    });
  }

  rejectContribution(contributionId: string) {
    if (this.submitting()) return;
    this.submitting.set(true); this.error.set(''); this.notice.set('');
    this.api.rejectCentralContribution(contributionId).subscribe({
      next: () => { this.submitting.set(false); this.notice.set('Die Einreichung wurde abgelehnt.');
        this.loadContributions(); },
      error: () => { this.submitting.set(false); this.error.set('Die Einreichung konnte nicht abgelehnt werden.'); }
    });
  }

  createNextDraft() {
    const revision = this.selected();
    if (!revision || revision.state !== this.publishedState || this.submitting()) return;
    this.submitting.set(true); this.error.set(''); this.notice.set('');
    this.api.createDraftFromPublished(revision.id).subscribe({
      next: result => { this.submitting.set(false); this.notice.set('Ein neuer Entwurf wurde aus der veröffentlichten Version erstellt.');
        if (result.revisionId) this.refreshList(result.revisionId); },
      error: (error: HttpErrorResponse) => {
        this.submitting.set(false);
        if (error.status === 409 && error.error?.code === 'ingredient_revision_draft_exists') {
          this.notice.set('Für diese Zutat besteht bereits ein Entwurf.');
          const revisionId = error.error?.revisionId as string | undefined;
          this.refreshList(revisionId);
        } else this.error.set('Der neue Zutatenentwurf konnte nicht erstellt werden.');
      }
    });
  }

  private load() {
    this.loading.set(true); this.selected.set(null); this.pendingForkSourceRevisionId.set(null);
    this.selectedSnapshot = ''; this.error.set('');
    const revisions = this.listRevisions();
    forkJoin({ referenceData: this.api.getReferenceData(), revisions }).subscribe({
      next: result => { this.referenceData.set(result.referenceData); this.revisions.set(result.revisions); this.loading.set(false);
        if (this.scope() === 'central') this.loadContributions(); },
      error: () => { this.loading.set(false); this.error.set('Die Zutatenverwaltung konnte nicht geladen werden.'); }
    });
  }

  private loadContributions() {
    this.api.listCentralContributions().subscribe({
      next: values => {
        this.contributions.set(values);
        this.contributionTargets.set(Object.fromEntries(values.map(value =>
          [value.id, value.suggestedCentralIngredientId ?? ''])));
      },
      error: () => this.error.set('Die offenen Zutaten-Einreichungen konnten nicht geladen werden.')
    });
  }

  private refreshList(openRevisionId?: string, openAfter = true) {
    const revisions = this.listRevisions();
    revisions.subscribe({ next: values => { this.revisions.set(values);
      if (openRevisionId && openAfter) this.open(openRevisionId); },
      error: () => this.error.set('Die Zutatenliste konnte nicht aktualisiert werden.') });
  }

  private listRevisions() {
    return this.scope() === 'central' ? this.api.listCentral()
      : this.scope() === 'tenant' ? this.api.listTenant(this.tenantId())
        : this.api.listCamp(this.campId());
  }

  private handleMutationError(error: HttpErrorResponse) {
    this.submitting.set(false);
    if (error.status === 409) {
      this.error.set('Die Revision wurde zwischenzeitlich geändert und wird neu geladen.');
      const revision = this.selected(); if (revision) this.open(revision.id);
    } else this.error.set('Die Zutatenrevision konnte nicht gespeichert werden.');
  }

  private snapshot(value: IngredientRevisionDetails) {
    return JSON.stringify({ sourceSummary: value.sourceSummary, name: value.name.trim(), categoryId: value.categoryId, baseUnitId: value.baseUnitId,
      allergenReviewState: value.allergenReviewState, intoleranceReviewState: value.intoleranceReviewState,
      originReviewState: value.originReviewState,
      nutritionProfile: value.nutritionProfile,
      substanceContents: [...(value.substanceContents ?? [])].sort((a, b) => a.substanceId.localeCompare(b.substanceId)),
      allergens: this.sortedProperties(value.allergens), intolerances: this.sortedProperties(value.intolerances),
      origins: this.sortedProperties(value.origins),
      unitConversions: this.sortedUnitConversions(value.unitConversions),
      variants: value.variants.map(variant => ({ id: variant.id, variantKey: variant.variantKey,
        name: variant.name.trim(), isActive: variant.isActive, sortOrder: variant.sortOrder,
        allergenOverrides: this.sortedProperties(variant.allergenOverrides),
        intoleranceOverrides: this.sortedProperties(variant.intoleranceOverrides),
        substanceContentOverrides: [...(variant.substanceContentOverrides ?? [])]
          .sort((a, b) => a.substanceId.localeCompare(b.substanceId)),
        originOverrides: this.sortedProperties(variant.originOverrides),
        nutritionProfile: variant.nutritionProfile,
        unitConversionOverrides: this.sortedUnitConversions(variant.unitConversionOverrides) })) });
  }

  private sortedProperties(values: IngredientRevisionPropertyItem[]) {
    return [...values].sort((left, right) => left.propertyId.localeCompare(right.propertyId));
  }

  private sortedUnitConversions(values: IngredientRevisionUnitConversionItem[]) {
    return values.map(value => ({
      sourceUnitId: value.sourceUnitId,
      factorToBaseUnit: value.factorToBaseUnit,
      precision: value.precision,
      factorInput: value.factorInput
    })).sort((left, right) => left.sourceUnitId.localeCompare(right.sourceUnitId));
  }

  private initializeConversionFactorInputs(revision: IngredientRevisionDetails) {
    for (const conversion of revision.unitConversions)
      conversion.factorInput = String(conversion.factorToBaseUnit).replace('.', ',');
    for (const variant of revision.variants)
      for (const conversion of variant.unitConversionOverrides)
        conversion.factorInput = String(conversion.factorToBaseUnit).replace('.', ',');
  }

  private initializeNutritionProfiles(revision: IngredientRevisionDetails) {
    revision.nutritionProfile ??= null;
    revision.substanceContents ??= [];
    revision.sourceSummary ??= '';
    for (const variant of revision.variants)
    {
      variant.nutritionProfile ??= null;
      variant.substanceContentOverrides ??= [];
    }
    if (!revision.sourceSummary) {
      const references = [revision.nutritionProfile?.sourceReference,
        ...revision.substanceContents.map(value => value.sourceReference),
        ...revision.variants.flatMap(value => [value.nutritionProfile?.sourceReference,
          ...value.substanceContentOverrides.map(content => content.sourceReference)])]
        .filter((value): value is string => !!value?.trim());
      revision.sourceSummary = [...new Set(references)].join('; ');
    }
  }

  private uniqueVariantKey(
    revision: IngredientRevisionDetails,
    current: IngredientVariantRevisionItem,
    name: string)
  {
    const normalized = name.trim().toLocaleLowerCase('de').replaceAll('ß', 'ss')
      .normalize('NFD').replace(/[\u0300-\u036f]/g, '')
      .replace(/[^a-z0-9]+/g, '_').replace(/^_+|_+$/g, '') || 'variante';
    const used = new Set(revision.variants.filter(value => value !== current).map(value => value.variantKey));
    let candidate = normalized;
    for (let suffix = 2; used.has(candidate); suffix++) candidate = `${normalized}_${suffix}`;
    return candidate;
  }

  private normalizeAllergenDetails(revision: IngredientRevisionDetails) {
    let changed = false;
    for (const parent of this.mainAllergens()) {
      const children = this.allergenChildren(parent.id);
      const parentState = this.propertyState(revision.allergens, parent.id);
      if (!children.length || parentState === null) continue;
      const inheritedState = parentState === IngredientPropertyState.Contains
        ? IngredientPropertyState.Unknown : parentState;
      for (const child of children) {
        const childState = this.propertyState(revision.allergens, child.id);
        if ((parentState === IngredientPropertyState.Contains && childState !== null) || childState === inheritedState)
          continue;
        this.setPropertyValue(revision.allergens, child.id, inheritedState, IngredientPropertySource.Derived);
        changed = true;
      }
    }
    if (changed) revision.allergenReviewState = IngredientPropertyReviewState.Unreviewed;
  }

  private normalizeQualitativeIntolerances(revision: IngredientRevisionDetails) {
    let changed = false;
    for (const property of this.qualitativeIntolerances()) {
      if (this.propertyState(revision.intolerances, property.id) !== null) continue;
      this.setPropertyValue(revision.intolerances, property.id,
        IngredientPropertyState.Unknown, IngredientPropertySource.Derived);
      changed = true;
    }
    if (changed) revision.intoleranceReviewState = IngredientPropertyReviewState.Unreviewed;
  }

  private normalizePrimaryOrigin(revision: IngredientRevisionDetails) {
    const primaryOrigins = this.primaryOrigins();
    if (primaryOrigins.some(value => this.propertyState(revision.origins, value.id) !== null)) return;
    const unknownOrigin = primaryOrigins.find(value => value.code === 'UNKNOWN_ORIGIN');
    if (!unknownOrigin) return;
    this.applyPrimaryOrigin(revision, unknownOrigin.id, IngredientPropertySource.Derived);
    revision.originReviewState = IngredientPropertyReviewState.Unreviewed;
  }

  private applyPrimaryOrigin(
    revision: IngredientRevisionDetails,
    selectedPropertyId: string,
    selectedSource: IngredientPropertySource)
  {
    for (const property of this.primaryOrigins()) {
      const selected = property.id === selectedPropertyId;
      this.setPropertyValue(revision.origins, property.id,
        selected ? IngredientPropertyState.Contains : IngredientPropertyState.DoesNotContain,
        selected ? selectedSource : IngredientPropertySource.Derived);
    }
  }

  private setPropertyValue(
    values: IngredientRevisionPropertyItem[],
    propertyId: string,
    state: IngredientPropertyState | null,
    source: IngredientPropertySource)
  {
    const index = values.findIndex(value => value.propertyId === propertyId);
    if (state === null) {
      if (index >= 0) values.splice(index, 1);
      return;
    }
    const item = { propertyId, state, source };
    if (index >= 0) values[index] = item;
    else values.push(item);
  }
}
