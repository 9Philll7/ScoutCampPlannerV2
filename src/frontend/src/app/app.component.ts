import { HttpErrorResponse } from '@angular/common/http';
import { StructureDetailHeaderComponent } from './features/camp/structure-detail-header.component';
import { Component, computed, effect, HostListener, inject, signal, viewChild } from '@angular/core';
import { TenantRecipesComponent } from './features/recipes/tenant-recipes.component';
import { MealPlanningApiService } from './features/meal-planning/meal-planning-api.service';
import { usedRecipeRevisions } from './features/recipes/camp-recipe-navigation';
import { SessionExpiry } from './core/session-expiry';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MAT_DATE_LOCALE, provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelect, MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatMenuModule } from '@angular/material/menu';
import { concatMap, firstValueFrom, forkJoin } from 'rxjs';
import { saveOfflinePackage } from './shared/save-offline-package';
import { desktopCommand, isDesktop } from './core/desktop-runtime';
import { PackageTransferComponent } from './features/camp/package-transfer.component';
import { ParticipantsComponent } from './features/camp/participants.component';
import { AuthenticationApiService, AuthenticatedUser } from './features/authentication/authentication-api.service';
import { CampAdministratorOption, CampApiService, CampMeal, CampMealType, CampPlanningSummary, CampStageFoodFactor, CampSummary, IngredientCatalogEntry, IngredientConflictType, IngredientScope, MeasurementDimension, ParticipantEstimate, StructureConfiguration, StructureNodeSummary, TenantOption, TenantStageFoodFactor, WeightedStageTotal } from './features/camp/camp-api.service';
import { IngredientRevisionEditorComponent } from './features/ingredients/ingredient-revision-editor.component';
import { DietaryCatalogComponent } from './features/meal-planning/dietary-catalog.component';
import { MealPlanningComponent } from './features/meal-planning/meal-planning.component';
import { RecipeEditorComponent } from './features/recipes/recipe-editor.component';
import { SetupApiService } from './features/setup/setup-api.service';
import { ActionIconComponent } from './shared/action-icon.component';
import { CurrentNavigationDirective } from './shared/current-navigation.directive';

type ViewState = 'loading' | 'setup' | 'login' | 'application' | 'unavailable';
type ApplicationSection = 'home' | 'camps' | 'recipes' | 'ingredients' | 'organization' | 'centralIngredients';
type CampSection = 'overview' | 'general' | 'structure' | 'catering' | 'participants' | 'recipes' | 'ingredients';

@Component({
  selector: 'scp-root',
  standalone: true,
  imports: [FormsModule, MatMenuModule, MatButtonModule, MatButtonToggleModule, MatCardModule, MatCheckboxModule, MatFormFieldModule, MatInputModule,
    MatProgressSpinnerModule, MatSelectModule, MatTooltipModule, MatDatepickerModule,
    ActionIconComponent, IngredientRevisionEditorComponent, RecipeEditorComponent, MealPlanningComponent, PackageTransferComponent, ParticipantsComponent, DietaryCatalogComponent, TenantRecipesComponent, CurrentNavigationDirective, StructureDetailHeaderComponent],
  providers: [provideNativeDateAdapter(), { provide: MAT_DATE_LOCALE, useValue: 'de-AT' }],
  templateUrl: './app.component.html'
})
export class AppComponent {
  readonly participantInitialNodeId = signal<string | null>(null);
  openNodeParticipants(nodeId: string) {
    this.navigateCamp('participants');
    if (this.campSection() === 'participants') this.participantInitialNodeId.set(nodeId);
  }
  readonly selectedStructureNodeId = signal<string | null>(null);
  readonly structureEditing = signal(false);
  selectStructureNode(id: string | null) {
    if (id === this.selectedStructureNodeId() || !this.mayLeave()) return;
    this.selectedStructureNodeId.set(id);
    this.editingStructureNodeId.set(null); this.movingStructureNodeId.set(null);
    this.creatingStructureParent.set(null); this.newStructureNodeName = ''; this.editStructureNodeName = '';
  }
  selectedStructureRows() {
    const node = this.structureNodes().find(value => value.id === this.selectedStructureNodeId());
    if (!node) return [];
    let depth = 0, parentId = node.parentId;
    while (parentId) { depth++; parentId = this.structureNodes().find(value => value.id === parentId)?.parentId ?? null; }
    return [{ node, depth }];
  }
  selectedStructureChildren() {
    return this.structureNodes().filter(value => value.parentId === this.selectedStructureNodeId());
  }
  toggleStructureEditing() {
    if (!this.mayLeave()) return;
    this.structureEditing.update(value => !value);
    this.editingStructureNodeId.set(null); this.movingStructureNodeId.set(null);
    this.creatingStructureParent.set(null); this.newStructureNodeName = ''; this.editStructureNodeName = '';
  }
  estimateTotal() { return this.participantEstimates().reduce((sum, value) => sum + value.childYouthCount + value.leaderCount, 0); }
  closeEstimates(saved = false) {
    if (this.submitting()) return;
    if (!saved && JSON.stringify(this.participantEstimates()) !== JSON.stringify(this.persistedParticipantEstimates) &&
      !window.confirm('Nicht gespeicherte Schätzwerte verwerfen?')) return;
    (document.getElementById('structure-estimate-dialog') as HTMLDialogElement | null)?.close();
    this.estimateNodeId.set(null); this.participantEstimates.set([]);
  }
  readonly sidebarOpen = signal(false);
  toggleSidebar() {
    if (this.sidebarOpen()) { this.closeSidebar(); return; }
    this.sidebarOpen.set(true);
    setTimeout(() => {
      if (this.sidebarOpen()) document.querySelector<HTMLElement>('#workspace-sidebar [aria-current="page"]')?.focus();
    });
  }
  @HostListener('document:keydown.escape')
  closeSidebar() {
    if (!this.sidebarOpen()) return;
    this.sidebarOpen.set(false);
    document.getElementById('navigation-toggle')?.focus();
  }
  readonly localDevice = isDesktop();
  reloadAfterPackageImport() { const user = this.user(); if (user) this.openApplication(user); }
  private readonly setupApi = inject(SetupApiService);
  private readonly authenticationApi = inject(AuthenticationApiService);
  private readonly campApi = inject(CampApiService);
  private readonly mealPlanningApi = inject(MealPlanningApiService);
  readonly usedCampRecipes = signal<ReadonlySet<string>>(new Set());
  private readonly ingredientRevisionEditor = viewChild(IngredientRevisionEditorComponent);
  private readonly tenantPicker = viewChild<MatSelect>('tenantPicker');
  private readonly participantsEditor = viewChild(ParticipantsComponent);
  readonly state = signal<ViewState>('loading');
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);
  readonly applicationSection = signal<ApplicationSection>('home');
  readonly user = signal<AuthenticatedUser | null>(null);
  readonly camps = signal<CampSummary[]>([]);
  readonly tenants = signal<TenantOption[]>([]);
  readonly selectedTenant = signal<TenantOption | null>(null);
  readonly administratorCandidates = signal<CampAdministratorOption[]>([]);
  readonly selectedAdministratorIds = signal<ReadonlySet<string>>(new Set());
  readonly editingCampId = signal<string | null>(null);
  readonly openedCampId = signal<string | null>(null);
  readonly campSection = signal<CampSection>('overview');
  private potentialEdits = false;
  private restoringNavigation = false;
  private recentIds = signal<string[]>([]);
  readonly recentCamps = computed(() => this.recentIds().flatMap(id => {
    const camp = this.camps().find(value => value.id === id);
    return camp ? [camp] : [];
  }).slice(0, 5));
  readonly otherCamps = computed(() => {
    const recentIds = new Set(this.recentCamps().map(camp => camp.id));
    return this.camps().filter(camp => !recentIds.has(camp.id));
  });

  sectionTitle() {
    return { home: 'Start', camps: 'Lager', recipes: 'Rezepte', ingredients: 'Zutaten',
      organization: 'Organisation', centralIngredients: 'Zentraler Zutatenstamm' }[this.applicationSection()];
  }

  trackEdit(event: Event) {
    if (this.state() === 'application' && !(event.target as HTMLElement).closest('.tenant-select, .catalog-search, .structure-workspace, scp-participants, [readonly]')) {
      this.potentialEdits = true;
    }
  }

  private mayLeave() {
    if (this.restoringNavigation) return true;
    if (this.submitting()) return false;
    // Existing editors do not share a dirty-state contract. Protect their forms
    // conservatively, including programmatic additions not emitting input events.
    if (this.hasPotentialEdits() && !window.confirm('Bereich verlassen? Bitte speichere vorher deine Änderungen. Nicht gespeicherte Eingaben werden verworfen.')) return false;
    this.potentialEdits = false;
    return true;
  }

  private hasPotentialEdits() {
    const structureDraft = this.campSection() === 'structure' && (
      (this.editingStructureNodeId() !== null && this.structureNodes().some(node => node.id === this.editingStructureNodeId() && this.hasStructureNodeNameChanged(node))) ||
      (this.creatingStructureParent() !== null && this.newStructureNodeName.trim() !== '') ||
      (this.movingStructureNodeId() !== null && this.structureNodes().some(node => node.id === this.movingStructureNodeId() && (this.moveTargetParentIds[node.id] || null) !== node.parentId)) ||
      (this.estimateNodeId() !== null && JSON.stringify(this.participantEstimates()) !== JSON.stringify(this.persistedParticipantEstimates)));
    return this.state() === 'application' && (structureDraft || this.participantsEditor()?.hasPendingChanges() || this.potentialEdits || Array.from(document.querySelectorAll(
      'main input:not([disabled]):not([readonly]), main textarea:not([disabled]):not([readonly]), main mat-select:not(.mat-mdc-select-disabled)'
    )).some(control => !control.closest('.tenant-select, .catalog-search, .structure-workspace, scp-participants')));
  }

  @HostListener('window:beforeunload', ['$event'])
  beforeUnload(event: BeforeUnloadEvent) {
    if (this.hasPotentialEdits()) { event.preventDefault(); event.returnValue = ''; }
  }

  private navigationHash() {
    const tenant = this.selectedTenant()?.id;
    if (!tenant) return '#/home';
    return `#/organization/${encodeURIComponent(tenant)}/${this.openedCampId()
      ? `camps/${encodeURIComponent(this.openedCampId()!)}/${this.campSection()}` : this.applicationSection()}`;
  }

  private rememberNavigation() {
    if (this.sidebarOpen()) {
      this.sidebarOpen.set(false);
      document.getElementById('workspace-content')?.focus();
    }
    if (!this.restoringNavigation && window.location.hash !== this.navigationHash()) {
      window.history.pushState(null, '', this.navigationHash());
    }
  }

  @HostListener('window:hashchange')
  restoreNavigation(skipGuard = false) {
    if (this.state() !== 'application' || !this.selectedTenant()) return;
    if (!skipGuard && !this.mayLeave()) { window.history.replaceState(null, '', this.navigationHash()); return; }
    const parts = window.location.hash.slice(2).split('/');
    const tenant = this.tenants().find(value => value.id === parts[1]);
    this.restoringNavigation = true;
    try {
      if (tenant && tenant.id !== this.selectedTenant()?.id) { this.selectTenant(tenant.id); return; }
      const camp = parts[2] === 'camps' && parts[3] ? this.camps().find(value => value.id === parts[3]) : null;
      if (camp) {
        this.openCamp(camp);
        const section = parts[4] as CampSection;
        this.navigateCamp(['overview', 'general', 'structure', 'participants', 'catering', 'recipes', 'ingredients'].includes(section) ? section : 'overview');
      } else {
        this.openedCampId.set(null);
        const section = parts[2] as ApplicationSection;
        this.showSection(['home', 'camps', 'recipes', 'ingredients', 'organization', 'centralIngredients'].includes(section) ? section : 'home');
        if (parts[3]) this.error.set('Dieses Lager ist nicht verfügbar oder nicht zugänglich.');
      }
    } finally { this.restoringNavigation = false; }
    window.history.replaceState(null, '', this.navigationHash());
  }

  navigateCamp(section: CampSection) {
    if (section === this.campSection() || !this.mayLeave()) return;
    const camp = this.openedCamp();
    if (!camp) return;
    this.participantInitialNodeId.set(null);
    this.campSection.set(section);
    if (section === 'structure' && this.structureCampId() !== camp.id) this.toggleStructure(camp);
    if (section === 'catering') { this.loadWeightedFoodSummary(camp.id); this.loadMealPlan(camp.id); }
    if (section === 'ingredients') this.loadIngredients(camp.id);
    if (section === 'recipes') {
      this.usedCampRecipes.set(new Set());
      this.mealPlanningApi.overview(camp.id).subscribe({
        next: overview => { if (this.openedCampId() === camp.id) this.usedCampRecipes.set(usedRecipeRevisions(overview)); },
        error: () => this.error.set('Verwendete Rezepte konnten nicht ermittelt werden. Lokale Rezepte bleiben sichtbar.'),
      });
    }
    this.rememberNavigation();
  }
  readonly structureCampId = signal<string | null>(null);
  readonly structureNodes = signal<StructureNodeSummary[]>([]);
  readonly structureConfiguration = signal<StructureConfiguration | null>(null);
  readonly structureMode = signal<'Free' | 'Fixed'>('Free');
  readonly structureLevelNames = signal<string[]>([]);
  private persistedStructureMode: 'Free' | 'Fixed' = 'Free';
  private persistedStructureLevelNames: string[] = [];
  readonly estimateNodeId = signal<string | null>(null);
  readonly movingStructureNodeId = signal<string | null>(null);
  readonly editingStructureNodeId = signal<string | null>(null);
  readonly creatingStructureParent = signal<string | 'camp' | null>(null);
  readonly collapsedStructureNodeIds = signal<ReadonlySet<string>>(new Set());
  readonly planningDetailsVisible = signal(false);
  readonly planningSummary = signal<CampPlanningSummary | null>(null);
  tenantName = '';
  email = '';
  password = '';
  campName = '';
  campStartDate: Date | null = null;
  campEndDate: Date | null = null;
  editCampName = '';
  editCampStartDate: Date | null = null;
  editCampEndDate: Date | null = null;
  newStructureParentId = '';
  newStructureNodeName = '';
  editStructureNodeName = '';
  newStageName = '';
  newCampStageName = '';
  moveTargetParentIds: Record<string, string> = {};
  readonly participantEstimates = signal<ParticipantEstimate[]>([]);
  private persistedParticipantEstimates: ParticipantEstimate[] = [];
  tenantStageFoodFactors: TenantStageFoodFactor[] = [];
  private persistedTenantStageFoodFactors: TenantStageFoodFactor[] = [];
  readonly editingTenantStageIndexes = signal<ReadonlySet<number>>(new Set());
  readonly campStageFoodFactors = signal<CampStageFoodFactor[]>([]);
  readonly campStagesLoading = signal(false);
  private persistedCampStageFoodFactors: CampStageFoodFactor[] = [];
  readonly editingCampStageIndexes = signal<ReadonlySet<number>>(new Set());
  readonly weightedFoodTotals = signal<WeightedStageTotal[]>([]);
  readonly mealTypes = signal<CampMealType[]>([]);
  readonly meals = signal<CampMeal[]>([]);
  readonly ingredients = signal<IngredientCatalogEntry[]>([]);
  readonly ingredientsLoading = signal(false);
  readonly ingredientSearch = signal('');
  readonly filteredIngredients = computed(() => {
    const search = this.ingredientSearch().trim().toLocaleLowerCase('de');
    if (!search) return this.ingredients();
    const matches = this.ingredients().filter(value => [value.name, value.originInformation ?? '',
      ...value.variants.map(item => item.name), ...value.conflicts.map(item => item.name)]
      .some(text => text.toLocaleLowerCase('de').includes(search)));
    const camp = matches.filter(value => value.scope === 'Camp');
    if (camp.length) return camp;
    const tenant = matches.filter(value => value.scope === 'Tenant');
    if (tenant.length) return tenant;
    return matches.filter(value => value.scope === 'Central');
  });
  private persistedMealTypeNames: string[] = [];

  readonly sessionExpiry = inject(SessionExpiry);
  constructor() {
    effect(() => {
      if (this.sessionExpiry.expired()) {
        this.clearSession();
        this.password = '';
        this.submitting.set(false);
      }
    });
    this.initialize();
  }

  initialize() {
    this.state.set('loading');
    this.error.set(null);
    this.setupApi.status().subscribe({
      next: status => status.isRequired ? this.state.set('setup') : this.loadSession(),
      error: () => this.showUnavailable()
    });
  }

  completeSetup() {
    if (this.submitting()) return;
    this.submitting.set(true);
    this.error.set(null);
    this.setupApi.complete({ tenantName: this.tenantName, email: this.email, password: this.password }).subscribe({
      next: () => { this.password = ''; this.submitting.set(false); this.state.set('login'); },
      error: (response: HttpErrorResponse) => {
        this.password = ''; this.submitting.set(false);
        if (response.status === 409) { this.state.set('login'); return; }
        const errors = response.error?.errors as Record<string, string[]> | undefined;
        this.error.set(errors ? Object.values(errors).flat()[0] ?? 'Die Eingaben sind ungültig.' : 'Die Einrichtung ist fehlgeschlagen.');
      }
    });
  }

  signIn() {
    if (this.submitting()) return;
    this.submitting.set(true);
    this.error.set(null);
    this.authenticationApi.signIn(this.email, this.password).subscribe({
      next: user => { this.password = ''; this.submitting.set(false); this.openApplication(user); },
      error: (response: HttpErrorResponse) => {
        this.password = ''; this.submitting.set(false);
        this.error.set(response.status === 429 ? 'Zu viele Anmeldeversuche. Bitte warte kurz.' : 'E-Mail-Adresse oder Passwort ist falsch.');
      }
    });
  }

  signOut() {
    if (!this.mayLeave()) return;
    this.sessionExpiry.signedOut();
    this.authenticationApi.signOut().subscribe({ next: () => this.clearSession(), error: () => this.clearSession() });
  }

  cancelOfflineTransfer(camp: CampSummary) {
    if (this.submitting() || !window.confirm(`„${camp.name}“ ohne Rückpaket entsperren?\n\nDer bisherige Serverstand bleibt erhalten. Offline-Änderungen werden nicht übernommen. Rückpakete dieses Transfers sind danach ungültig. Die lokale Kopie darf nicht weiterverwendet werden.`)) return;
    this.submitting.set(true); this.error.set(null); this.notice.set(null);
    this.campApi.cancelOfflineTransfer(camp).subscribe({
      next: () => {
        this.submitting.set(false); this.notice.set('Lager entsperrt. Der bisherige Serverstand bleibt erhalten; der alte Transfer ist ungültig.');
        this.loadCamps(camp.tenantId);
      },
      error: (error: HttpErrorResponse) => {
        this.submitting.set(false); this.error.set(error.error?.message ?? 'Das Lager konnte nicht entsperrt werden. Bitte Berechtigung und Transferstand prüfen.');
        this.loadCamps(camp.tenantId);
      },
    });
  }

  removeLocalCopy(camp: CampSummary) {
    if (this.submitting() || !window.confirm(`Lokale Kopie von „${camp.name}“ endgültig entfernen?\n\nNicht exportierte Änderungen gehen verloren. Speichere vorher bei Bedarf ein Rückpaket. Serverdaten und gespeicherte Paketdateien bleiben unverändert; eine Serversperre wird nicht aufgehoben.`)) return;
    this.submitting.set(true); this.error.set(null); this.notice.set(null);
    this.campApi.removeLocalCopy(camp).subscribe({
      next: () => {
        this.submitting.set(false); this.camps.set([]); this.openedCampId.set(null);
        this.notice.set('Lokale Lagerkopie entfernt. Ein neues Lagerpaket kann jetzt importiert werden.');
        this.reloadAfterPackageImport();
      },
      error: (error: HttpErrorResponse) => {
        this.submitting.set(false);
        this.error.set(error.error?.message ?? `Die lokale Kopie konnte nicht entfernt werden (HTTP ${error.status}). Bitte die Lagerliste neu laden.`);
      },
    });
  }

  async exportCamp(camp: CampSummary) {
    if (this.submitting()) return;
    this.submitting.set(true);
    this.error.set(null);
    this.notice.set(null);
    let transferStarted = false;
    try {
      if (this.localDevice) {
        const blob = await firstValueFrom(this.campApi.createReturnPackage(camp.id));
        const saved = await desktopCommand<boolean>('save_return_package', { bytes: Array.from(new Uint8Array(await blob.arrayBuffer())) });
        this.notice.set(saved ? 'Rückpaket gespeichert.' : 'Speichern abgebrochen.');
        return;
      }
      const saved = await saveOfflinePackage(`camp-${camp.id}.scoutcamp`, () => {
        transferStarted = true;
        return firstValueFrom(this.campApi.startOfflineTransfer(camp.id));
      });
      this.notice.set(saved ? 'Offlinepaket gespeichert.' : 'Speichern abgebrochen. Die Offline-Phase wurde nicht gestartet.');
    } catch (error) {
      this.error.set(transferStarted
        ? 'Das Offlinepaket konnte nicht gespeichert werden. Der Transfer wurde bereits angefordert; bitte den Lagerstatus prüfen. Die Sperre wird nicht automatisch aufgehoben.'
        : error instanceof HttpErrorResponse ? `Das Rückpaket konnte vom Backend nicht erstellt werden (HTTP ${error.status}).`
        : error instanceof Error ? error.message : typeof error === 'string' ? error : 'Der Speicherort konnte nicht geöffnet werden.');
    } finally {
      this.submitting.set(false);
      const tenant = this.selectedTenant();
      if (transferStarted && tenant) this.loadCamps(tenant.id);
    }
  }

  isAdministratorSelected(membershipId: string) {
    return this.selectedAdministratorIds().has(membershipId);
  }

  setAdministratorSelected(membershipId: string, selected: boolean) {
    const next = new Set(this.selectedAdministratorIds());
    selected ? next.add(membershipId) : next.delete(membershipId);
    this.selectedAdministratorIds.set(next);
  }

  createCamp() {
    const tenant = this.selectedTenant();
    if (!tenant || this.submitting()) return;
    this.submitting.set(true); this.error.set(null); this.notice.set(null);
    const selectedAdministratorIds = [...this.selectedAdministratorIds()];
    this.campApi.create(tenant.id, {
      name: this.campName,
      startDate: this.formatApiDate(this.campStartDate),
      endDate: this.formatApiDate(this.campEndDate),
      initialAdministratorMembershipIds: selectedAdministratorIds
    }).subscribe({
      next: camp => {
        this.submitting.set(false); this.campName = ''; this.campStartDate = null; this.campEndDate = null;
        this.selectedAdministratorIds.set(new Set());
        const currentUserIsAdmin = this.administratorCandidates().some(candidate =>
          candidate.userId === this.user()?.userId && selectedAdministratorIds.includes(candidate.membershipId));
        this.notice.set(currentUserIsAdmin
          ? 'Das Lager wurde angelegt.'
          : 'Das Lager wurde angelegt. Du hast dir selbst keinen Camp-Zugriff zugewiesen.');
        this.loadCamps(tenant.id);
      },
      error: (response: HttpErrorResponse) => {
        this.submitting.set(false);
        const errors = response.error?.errors as Record<string, string[]> | undefined;
        this.error.set(response.status === 403 ? 'Du darfst in diesem Mandanten kein Lager anlegen.' :
          errors ? Object.values(errors).flat()[0] ?? 'Die Eingaben sind ungültig.' : 'Das Lager konnte nicht angelegt werden.');
      }
    });
  }

  editCamp(camp: CampSummary) {
    this.editingCampId.set(camp.id); this.editCampName = camp.name;
    this.editCampStartDate = this.parseApiDate(camp.startDate); this.editCampEndDate = this.parseApiDate(camp.endDate);
    this.error.set(null); this.notice.set(null);
  }

  cancelCampEdit() {
    this.editingCampId.set(null); this.editCampName = '';
    this.editCampStartDate = null; this.editCampEndDate = null;
  }

  openedCamp() {
    return this.camps().find(camp => camp.id === this.openedCampId()) ?? null;
  }

  visibleCamps() {
    const openedCampId = this.openedCampId();
    return openedCampId ? this.camps().filter(camp => camp.id === openedCampId) : this.camps();
  }

  openCamp(camp: CampSummary) {
    if (!this.mayLeave()) return;
    this.structureEditing.set(false); this.selectedStructureNodeId.set(null);
    this.participantInitialNodeId.set(null);
    this.applicationSection.set('camps');
    this.openedCampId.set(camp.id);
    this.campSection.set('overview');
    this.recentIds.update(ids => [camp.id, ...ids.filter(id => id !== camp.id)].slice(0, 20));
    try { sessionStorage.setItem(`scp-recent-${this.user()?.userId}`, JSON.stringify(this.recentIds())); } catch { /* Storage is optional. */ }
    this.error.set(null);
    this.notice.set(null);
    this.loadCampStageFoodFactors(camp.id);
    this.loadStructureConfiguration(camp.id);
    this.rememberNavigation();
  }

  openCampStructure(camp: CampSummary) {
    this.navigateCamp('structure');
  }

  openCampCatering(camp: CampSummary) {
    this.navigateCamp('catering');
  }

  closeCamp() {
    if (!this.mayLeave()) return;
    this.openedCampId.set(null);
    this.applicationSection.set('camps');
    this.campSection.set('general');
    this.editingCampId.set(null);
    this.movingStructureNodeId.set(null);
    this.collapsedStructureNodeIds.set(new Set());
    this.creatingStructureParent.set(null); this.planningDetailsVisible.set(false);
    if (this.structureCampId()) this.structureCampId.set(null);
    this.ingredients.set([]); this.ingredientSearch.set('');
    this.error.set(null);
    this.notice.set(null);
    this.rememberNavigation();
  }

  saveCamp(camp: CampSummary) {
    if (this.submitting()) return;
    this.submitting.set(true); this.error.set(null); this.notice.set(null);
    this.campApi.update(camp.id, {
      name: this.editCampName,
      startDate: this.formatApiDate(this.editCampStartDate),
      endDate: this.formatApiDate(this.editCampEndDate)
    }).subscribe({
      next: updated => {
        this.submitting.set(false); this.cancelCampEdit();
        this.camps.update(values => values.map(value => value.id === updated.id ? updated : value));
        this.notice.set('Das Lager wurde aktualisiert.');
      },
      error: (response: HttpErrorResponse) => {
        this.submitting.set(false);
        const errors = response.error?.errors as Record<string, string[]> | undefined;
        this.error.set(response.status === 409 ? 'Das Lager kann während der Offlinephase nicht bearbeitet werden.' :
          response.status === 404 ? 'Du darfst dieses Lager nicht bearbeiten.' :
          errors ? Object.values(errors).flat()[0] ?? 'Die Eingaben sind ungültig.' : 'Das Lager konnte nicht aktualisiert werden.');
      }
    });
  }

  toggleStructure(camp: CampSummary) {
    if (this.structureCampId() === camp.id) {
      this.structureCampId.set(null); this.structureNodes.set([]); this.structureConfiguration.set(null);
      this.collapsedStructureNodeIds.set(new Set());
      this.estimateNodeId.set(null); this.movingStructureNodeId.set(null); this.planningSummary.set(null); this.participantEstimates.set([]);
      this.weightedFoodTotals.set([]); return;
    }
    this.structureCampId.set(camp.id); this.structureNodes.set([]); this.collapsedStructureNodeIds.set(new Set());
    this.estimateNodeId.set(null); this.movingStructureNodeId.set(null); this.planningSummary.set(null); this.participantEstimates.set([]);
    this.weightedFoodTotals.set([]);
    this.newStructureParentId = ''; this.newStructureNodeName = '';
    this.campApi.listStructure(camp.id).subscribe({
      next: nodes => { this.structureNodes.set(nodes); this.moveTargetParentIds = Object.fromEntries(nodes.map(node => [node.id, node.parentId ?? ''])); },
      error: () => this.error.set('Die Lagerstruktur konnte nicht geladen werden.')
    });
    this.loadStructureConfiguration(camp.id);
    this.loadPlanningSummary(camp.id);
    this.loadWeightedFoodSummary(camp.id);
  }

  saveStructureConfiguration(camp: CampSummary) {
    const levels = this.structureMode() === 'Fixed'
      ? this.structureLevelNames().map(value => value.trim()).filter(Boolean)
      : [];
    this.submitting.set(true); this.error.set(null);
    this.campApi.updateStructureConfiguration(camp.id, levels).subscribe({
      next: () => {
        const mode = levels.length ? 'Fixed' : 'Free';
        this.submitting.set(false); this.structureConfiguration.set({ mode, levelNames: levels });
        this.structureMode.set(mode); this.structureLevelNames.set([...levels]);
        this.persistedStructureMode = mode; this.persistedStructureLevelNames = [...levels];
        this.notice.set('Die Strukturtiefe wurde aktualisiert.');
      },
      error: () => { this.submitting.set(false); this.error.set('Die Strukturtiefe ist ungültig oder für den bestehenden Baum zu kurz.'); }
    });
  }

  private loadStructureConfiguration(campId: string) {
    this.campApi.getStructureConfiguration(campId).subscribe({
      next: configuration => {
        this.structureConfiguration.set(configuration);
        this.structureMode.set(configuration.mode);
        this.structureLevelNames.set([...configuration.levelNames]);
        this.persistedStructureMode = configuration.mode;
        this.persistedStructureLevelNames = [...configuration.levelNames];
      },
      error: () => this.error.set('Die Strukturkonfiguration konnte nicht geladen werden.')
    });
  }

  setStructureMode(mode: 'Free' | 'Fixed') {
    this.structureMode.set(mode);
    if (mode === 'Fixed' && this.structureLevelNames().length === 0) this.structureLevelNames.set(['Ebene 1']);
  }

  addStructureLevel() {
    this.structureLevelNames.update(levels => [...levels, `Ebene ${levels.length + 1}`]);
  }

  renameStructureLevel(index: number, name: string) {
    this.structureLevelNames.update(levels => levels.map((level, candidateIndex) => candidateIndex === index ? name : level));
  }

  removeStructureLevel(index: number) {
    if (this.structureLevelNames().length <= 1) return;
    this.structureLevelNames.update(levels => levels.filter((_, candidateIndex) => candidateIndex !== index));
  }

  moveStructureLevel(index: number, direction: -1 | 1) {
    const levels = [...this.structureLevelNames()];
    const targetIndex = index + direction;
    if (targetIndex < 0 || targetIndex >= levels.length) return;
    [levels[index], levels[targetIndex]] = [levels[targetIndex], levels[index]];
    this.structureLevelNames.set(levels);
  }

  structureConfigurationChanged() {
    if (this.structureMode() !== this.persistedStructureMode) return true;
    if (this.structureMode() === 'Free') return false;
    const current = this.structureLevelNames().map(value => value.trim());
    return current.length !== this.persistedStructureLevelNames.length ||
      current.some((value, index) => value !== this.persistedStructureLevelNames[index]);
  }

  structureRows(): { node: StructureNodeSummary; depth: number }[] {
    const nodes = this.structureNodes();
    const result: { node: StructureNodeSummary; depth: number }[] = [];
    if (this.isStructureNodeCollapsed('camp')) return result;
    const append = (parentId: string | null, depth: number) => {
      nodes.filter(node => node.parentId === parentId)
        .sort((left, right) => left.name.localeCompare(right.name, 'de'))
        .forEach(node => {
          result.push({ node, depth });
          if (!this.isStructureNodeCollapsed(node.id)) append(node.id, depth + 1);
        });
    };
    append(null, 0);
    return result;
  }

  isStructureNodeCollapsed(nodeId: string) {
    return this.collapsedStructureNodeIds().has(nodeId);
  }

  toggleStructureNodeCollapsed(nodeId: string) {
    this.collapsedStructureNodeIds.update(current => {
      const updated = new Set(current);
      if (updated.has(nodeId)) updated.delete(nodeId); else updated.add(nodeId);
      return updated;
    });
  }

  campChildYouthTotal() {
    return this.planningSummary()?.stageTotals.reduce((sum, value) => sum + value.childYouthCount, 0) ?? 0;
  }

  campLeaderTotal() {
    return this.planningSummary()?.stageTotals.reduce((sum, value) => sum + value.leaderCount, 0) ?? 0;
  }

  canAddStructureChild(row: { node: StructureNodeSummary; depth: number }) {
    const total = this.structureTotal(row.node.id);
    const hasChildren = !this.isStructureLeaf(row.node);
    if (!hasChildren && total && (total.childYouthCount > 0 || total.leaderCount > 0)) return false;
    return this.structureMode() === 'Free' || row.depth + 1 < this.structureLevelNames().length;
  }

  toggleCreateStructureNode(parentId: string | null) {
    const target = parentId ?? 'camp';
    if (this.creatingStructureParent() === target) {
      this.creatingStructureParent.set(null); this.newStructureNodeName = ''; return;
    }
    this.creatingStructureParent.set(target);
    this.newStructureParentId = parentId ?? '';
    this.newStructureNodeName = '';
  }

  structureNodePath(node: StructureNodeSummary) {
    const names = [node.name];
    let parentId = node.parentId;
    while (parentId) {
      const parent = this.structureNodes().find(value => value.id === parentId);
      if (!parent) break;
      names.unshift(parent.name);
      parentId = parent.parentId;
    }
    return names.join(' › ');
  }

  isStructureLeaf(node: StructureNodeSummary) {
    return !this.structureNodes().some(value => value.parentId === node.id);
  }

  canDeleteStructureNode(node: StructureNodeSummary) {
    if (!this.planningSummary() || !this.isStructureLeaf(node)) return false;
    if (this.estimateNodeId() === node.id && this.participantEstimates().some(
      value => value.childYouthCount > 0 || value.leaderCount > 0)) return false;
    const total = this.structureTotal(node.id);
    return !total || total.childYouthCount === 0 && total.leaderCount === 0;
  }

  structureDeleteTooltip(node: StructureNodeSummary, camp: CampSummary) {
    if (camp.isFrozen) return 'Während der Offlinephase nicht löschbar';
    if (!this.planningSummary()) return 'Planungsdaten werden geladen';
    if (!this.isStructureLeaf(node)) return 'Knoten mit Unterknoten können nicht gelöscht werden';
    if (!this.canDeleteStructureNode(node)) return 'Knoten mit Teilnehmerschätzungen können nicht gelöscht werden';
    return 'Eintrag löschen';
  }

  canPlanOnStructureRow(row: { node: StructureNodeSummary; depth: number }) {
    if (!this.isStructureLeaf(row.node)) return false;
    return this.structureMode() === 'Free' || row.depth + 1 === this.structureLevelNames().length;
  }

  structureLevelLabel(depth: number) {
    return this.structureMode() === 'Fixed' ? this.structureLevelNames()[depth] ?? null : null;
  }

  openEstimates(camp: CampSummary, node: StructureNodeSummary) {
    if (this.estimateNodeId() === node.id) {
      this.estimateNodeId.set(null); this.participantEstimates.set([]); this.persistedParticipantEstimates = []; return;
    }
    this.movingStructureNodeId.set(null);
    this.editingStructureNodeId.set(null);
    this.editStructureNodeName = '';
    this.estimateNodeId.set(node.id); this.participantEstimates.set([]); this.persistedParticipantEstimates = [];
    this.campApi.getParticipantEstimates(camp.id, node.id).subscribe({
      next: values => {
        if (this.estimateNodeId() !== node.id || this.openedCampId() !== camp.id) return;
        this.participantEstimates.set(values.map(value => ({ ...value })));
        this.persistedParticipantEstimates = values.map(value => ({ ...value }));
        setTimeout(() => {
          const dialog = document.getElementById('structure-estimate-dialog') as HTMLDialogElement | null;
          if (dialog && !dialog.open) dialog.showModal();
        });
      },
      error: () => this.error.set('Die Teilnehmerschätzung konnte nicht geladen werden.')
    });
  }

  saveEstimates(camp: CampSummary) {
    const nodeId = this.estimateNodeId(); if (!nodeId) return;
    if (this.submitting() || camp.isFrozen || !camp.canEdit) return;
    if (this.participantEstimates().some(value => !Number.isInteger(value.childYouthCount) || value.childYouthCount < 0 || !Number.isInteger(value.leaderCount) || value.leaderCount < 0)) {
      this.error.set('Bitte für jede Stufe ganze, nicht negative Anzahlen eingeben.'); return;
    }
    this.submitting.set(true); this.error.set(null);
    this.campApi.updateParticipantEstimates(camp.id, nodeId, this.participantEstimates()).subscribe({
      next: () => {
        this.submitting.set(false);
        this.persistedParticipantEstimates = this.participantEstimates().map(value => ({ ...value }));
        this.notice.set('Die Teilnehmerschätzung wurde gespeichert.');
        this.loadPlanningSummary(camp.id); this.loadWeightedFoodSummary(camp.id);
        this.closeEstimates(true);
      },
      error: (response: HttpErrorResponse) => {
        this.submitting.set(false);
        const code = response.error?.code;
        this.error.set(code === 'structure_node_not_leaf' ? 'Schätzwerte sind nur an Blattknoten zulässig.'
          : code === 'structure_node_not_participant_level' ? 'Schätzwerte sind nur auf der letzten definierten Ebene zulässig.'
          : code === 'camp_frozen' ? 'Während der Offlinephase können Schätzwerte nicht geändert werden.'
          : 'Die Teilnehmerschätzung konnte nicht gespeichert werden. Bitte Anzahlen und Berechtigung prüfen.');
      }
    });
  }

  hasPersistedParticipantEstimates() {
    return this.persistedParticipantEstimates.some(value => value.childYouthCount > 0 || value.leaderCount > 0);
  }

  deleteEstimates(camp: CampSummary) {
    const nodeId = this.estimateNodeId();
    if (!nodeId || this.submitting()) return;
    const empty = this.participantEstimates().map(value => ({ ...value, childYouthCount: 0, leaderCount: 0 }));
    this.submitting.set(true); this.error.set(null); this.notice.set(null);
    this.campApi.updateParticipantEstimates(camp.id, nodeId, empty).subscribe({
      next: () => {
        this.submitting.set(false); this.participantEstimates.set(empty);
        this.persistedParticipantEstimates = empty.map(value => ({ ...value }));
        this.notice.set('Die Teilnehmerschätzung wurde gelöscht.');
        this.loadPlanningSummary(camp.id); this.loadWeightedFoodSummary(camp.id);
      },
      error: () => {
        this.submitting.set(false); this.error.set('Die Teilnehmerschätzung konnte nicht gelöscht werden.');
      }
    });
  }

  structureTotal(nodeId: string) {
    return this.planningSummary()?.structureTotals.find(value => value.structureNodeId === nodeId) ?? null;
  }

  private loadPlanningSummary(campId: string) {
    this.campApi.getPlanningSummary(campId).subscribe({
      next: summary => this.planningSummary.set(summary),
      error: () => this.error.set('Die Planungsübersicht konnte nicht geladen werden.')
    });
  }

  private loadCampStageFoodFactors(campId: string) {
    this.campStagesLoading.set(true);
    this.campStageFoodFactors.set([]);
    this.persistedCampStageFoodFactors = [];
    this.editingCampStageIndexes.set(new Set());
    this.campApi.getCampStageFoodFactors(campId).subscribe({
      next: factors => {
        if (this.openedCampId() !== campId) return;
        this.campStagesLoading.set(false);
        this.editingCampStageIndexes.set(new Set());
        this.campStageFoodFactors.set(factors.map(value => ({ ...value })));
        this.persistedCampStageFoodFactors = factors.map(value => ({ ...value }));
      },
      error: (failure: { status?: number }) => {
        if (this.openedCampId() !== campId) return;
        this.campStagesLoading.set(false);
        const detail = failure.status === 0 ? 'Backend nicht erreichbar.'
          : failure.status ? `HTTP ${failure.status}.` : 'Keine Statusinformation verfügbar.';
        this.error.set(`Die Lager-Verpflegungsfaktoren konnten nicht geladen werden. ${detail} Bitte das Lager erneut öffnen; bei wiederholtem Fehler den Status melden.`);
      }
    });
  }

  toggleCampStageEditing(index: number) {
    const next = new Set(this.editingCampStageIndexes());
    next.has(index) ? next.delete(index) : next.add(index);
    this.editingCampStageIndexes.set(next);
  }

  campStageEditing(index: number) {
    return this.editingCampStageIndexes().has(index);
  }

  campStageChanged(index: number) {
    const current = this.campStageFoodFactors()[index];
    const persisted = this.persistedCampStageFoodFactors[index];
    return !persisted || current.stageName.trim() !== persisted.stageName || Number(current.factor) !== Number(persisted.factor);
  }

  addCampStage(camp: CampSummary) {
    const stageName = this.newCampStageName.trim();
    if (!stageName) return;
    this.persistCampStages(camp, [...this.campStageFoodFactors(),
      { campStageId: '', stageName, factor: 1 }], 'Die Lagerstufe wurde hinzugefügt.', () => this.newCampStageName = '');
  }

  removeCampStage(camp: CampSummary, index: number) {
    const factors = this.campStageFoodFactors().filter((_, candidateIndex) => candidateIndex !== index);
    if (factors.length === 0) {
      this.error.set('Mindestens eine Lagerstufe muss erhalten bleiben.');
      return;
    }
    this.persistCampStages(camp, factors, 'Die Lagerstufe wurde entfernt.');
  }

  moveCampStage(camp: CampSummary, index: number, direction: -1 | 1) {
    const targetIndex = index + direction;
    if (targetIndex < 0 || targetIndex >= this.campStageFoodFactors().length) return;
    const factors = this.campStageFoodFactors().map(value => ({ ...value }));
    [factors[index], factors[targetIndex]] = [factors[targetIndex], factors[index]];
    this.persistCampStages(camp, factors, 'Die Reihenfolge wurde gespeichert.');
  }

  saveCampStage(camp: CampSummary) {
    this.persistCampStages(camp, this.campStageFoodFactors(), 'Die Änderungen wurden gespeichert.');
  }

  private persistCampStages(camp: CampSummary, values: CampStageFoodFactor[], successNotice: string,
    afterSave?: () => void) {
    const desired = values.map(value => ({ stageName: value.stageName.trim(), factor: value.factor }));
    this.submitting.set(true); this.error.set(null); this.notice.set(null);
    this.campApi.updateCampStages(camp.id, desired.map(value => value.stageName)).pipe(
      concatMap(() => this.campApi.getCampStageFoodFactors(camp.id)),
      concatMap(stored => this.campApi.updateCampStageFoodFactors(camp.id,
        stored.map((value, index) => ({ ...value, factor: desired[index]?.factor ?? 1 }))))
    ).subscribe({
      next: () => {
        this.submitting.set(false); this.notice.set(successNotice); afterSave?.();
        this.loadCampStageFoodFactors(camp.id); this.loadWeightedFoodSummary(camp.id);
      },
      error: () => {
        this.submitting.set(false);
        this.error.set('Die Lagerstufen konnten nicht gespeichert werden. Prüfe Namen und Faktoren; bestehende Schätzwerte können Änderungen verhindern.');
      }
    });
  }

  private loadWeightedFoodSummary(campId: string) {
    this.campApi.getWeightedFoodSummary(campId).subscribe({
      next: totals => this.weightedFoodTotals.set(totals),
      error: () => this.error.set('Die gewichtete Verpflegungsübersicht konnte nicht geladen werden.')
    });
  }

  private loadMealPlan(campId: string) {
    this.campApi.getMealPlan(campId).subscribe({ next: plan => {
      this.mealTypes.set(plan.mealTypes); this.meals.set(plan.meals);
      this.persistedMealTypeNames = plan.mealTypes.map(value => value.name);
    }, error: () => this.error.set('Der Mahlzeitenplan konnte nicht geladen werden.') });
  }

  loadIngredients(campId: string) {
    this.ingredientsLoading.set(true);
    this.campApi.getCampIngredients(campId).subscribe({
      next: ingredients => { this.ingredients.set(ingredients); this.ingredientsLoading.set(false); },
      error: () => { this.ingredients.set([]); this.ingredientsLoading.set(false);
        this.error.set('Der Zutatenkatalog konnte nicht geladen werden.'); }
    });
  }

  prepareIngredientFork(sourceRevisionId: string) {
    this.ingredientRevisionEditor()?.prepareCampFork(sourceRevisionId);
  }

  ingredientScopeLabel(scope: IngredientScope) {
    return scope === 'Central' ? 'Zentral' : scope === 'Tenant' ? 'Organisation' : 'Lager';
  }

  conflictTypeLabel(type: IngredientConflictType) {
    return type === 'Allergen' ? 'Allergen' : type === 'Intolerance' ? 'Unverträglichkeit' : 'Ernährungsform';
  }

  measurementDimensionLabel(dimension: MeasurementDimension) {
    return dimension === 'Mass' ? 'Masse' : dimension === 'Volume' ? 'Volumen' : 'Stück';
  }

  mealDates() { return [...new Set(this.meals().map(value => value.date))].sort(); }
  mealFor(date: string, mealTypeId: string) { return this.meals().find(value => value.date === date && value.mealTypeId === mealTypeId); }
  mealTypesChanged() {
    const current = this.mealTypes().map(value => value.name.trim());
    return current.length !== this.persistedMealTypeNames.length || current.some((value, index) => value !== this.persistedMealTypeNames[index]);
  }
  updateMealTypeName(index: number, name: string) {
    this.mealTypes.update(values => values.map((value, candidate) => candidate === index ? { ...value, name } : value));
  }
  addMealType() { this.mealTypes.update(values => [...values, { id: `new-${crypto.randomUUID()}`, name: '', sortOrder: values.length }]); }
  removeMealType(index: number) { this.mealTypes.update(values => values.filter((_, candidate) => candidate !== index)); }
  moveMealType(index: number, direction: -1 | 1) {
    const values = [...this.mealTypes()]; const target = index + direction;
    if (target < 0 || target >= values.length) return;
    [values[index], values[target]] = [values[target], values[index]];
    this.mealTypes.set(values.map((value, sortOrder) => ({ ...value, sortOrder })));
  }
  saveMealTypes(camp: CampSummary) {
    const names = this.mealTypes().map(value => value.name.trim());
    if (!names.length || names.some(value => !value) || this.submitting()) return;
    this.submitting.set(true); this.error.set(null);
    this.campApi.updateMealTypes(camp.id, names).subscribe({
      next: () => { this.submitting.set(false); this.notice.set('Die Mahlzeitenbezeichnungen wurden gespeichert.'); this.loadMealPlan(camp.id); },
      error: () => { this.submitting.set(false); this.error.set('Die Mahlzeitenbezeichnungen konnten nicht gespeichert werden.'); }
    });
  }
  setMealActivity(camp: CampSummary, meal: CampMeal, isActive: boolean) {
    this.submitting.set(true); this.error.set(null);
    this.campApi.updateMealActivity(camp.id, meal.id, isActive).subscribe({
      next: () => { this.submitting.set(false); this.meals.update(values => values.map(value => value.id === meal.id ? { ...value, isActive } : value)); },
      error: () => { this.submitting.set(false); this.error.set('Die Mahlzeit konnte nicht geändert werden.'); }
    });
  }

  createStructureNode(camp: CampSummary) {
    if (this.submitting()) return;
    this.submitting.set(true); this.error.set(null); this.notice.set(null);
    this.campApi.createStructureNode(
      camp.id, this.newStructureParentId || null, this.newStructureNodeName).subscribe({
      next: node => {
        this.submitting.set(false); this.structureNodes.update(nodes => [...nodes, node]);
        this.newStructureNodeName = ''; this.creatingStructureParent.set(null);
        this.notice.set('Der Struktureintrag wurde angelegt.');
      },
      error: (response: HttpErrorResponse) => {
        this.submitting.set(false);
        const errors = response.error?.errors as Record<string, string[]> | undefined;
        this.error.set(response.error?.code === 'structure_node_has_estimates'
          ? 'Unter einem Knoten mit Schätzwerten oder zugeordneten Teilnehmern kann kein Untereintrag angelegt werden.' :
          response.error?.code === 'maximum_structure_depth_reached' ? 'Die letzte definierte Strukturebene ist erreicht.' :
          response.status === 409 ? 'Während der Offlinephase kann die Struktur nicht geändert werden.' :
          response.status === 404 ? 'Du darfst diese Lagerstruktur nicht ändern.' :
          errors ? Object.values(errors).flat()[0] ?? 'Die Eingabe ist ungültig.' : 'Der Struktureintrag konnte nicht angelegt werden.');
      }
    });
  }

  deleteStructureNode(camp: CampSummary, node: StructureNodeSummary) {
    if (this.submitting()) return;
    this.submitting.set(true); this.error.set(null); this.notice.set(null);
    this.campApi.deleteStructureNode(camp.id, node.id).subscribe({
      next: () => { this.submitting.set(false); this.selectedStructureNodeId.set(node.parentId); this.structureNodes.update(nodes => nodes.filter(value => value.id !== node.id)); this.notice.set('Der Struktureintrag wurde gelöscht.'); },
      error: (response: HttpErrorResponse) => {
        this.submitting.set(false);
        this.error.set(response.status === 409 && response.error?.code === 'structure_node_has_children'
          ? 'Ein Struktureintrag mit Untereinträgen kann nicht gelöscht werden.'
          : response.status === 409 && response.error?.code === 'structure_node_has_estimates'
          ? 'Ein Struktureintrag mit Schätzwerten oder zugeordneten Teilnehmern kann nicht gelöscht werden.'
          : response.status === 409 ? 'Während der Offlinephase kann die Struktur nicht geändert werden.'
          : 'Der Struktureintrag konnte nicht gelöscht werden.');
      }
    });
  }

  moveTargets(node: StructureNodeSummary): StructureNodeSummary[] {
    const excluded = new Set<string>([node.id]);
    const addChildren = (id: string) => this.structureNodes().filter(value => value.parentId === id)
      .forEach(value => { excluded.add(value.id); addChildren(value.id); });
    addChildren(node.id);
    return this.structureNodes().filter(value => !excluded.has(value.id));
  }

  toggleMoveStructureNode(node: StructureNodeSummary) {
    this.editingStructureNodeId.set(null);
    this.editStructureNodeName = '';
    this.movingStructureNodeId.update(current => current === node.id ? null : node.id);
  }

  toggleRenameStructureNode(node: StructureNodeSummary) {
    this.movingStructureNodeId.set(null);
    if (this.editingStructureNodeId() === node.id) {
      this.editingStructureNodeId.set(null);
      this.editStructureNodeName = '';
      return;
    }
    this.editingStructureNodeId.set(node.id);
    this.editStructureNodeName = node.name;
  }

  hasStructureNodeNameChanged(node: StructureNodeSummary) {
    return this.editStructureNodeName.trim() !== node.name;
  }

  renameStructureNode(camp: CampSummary, node: StructureNodeSummary) {
    const name = this.editStructureNodeName.trim();
    if (!name || name === node.name || this.submitting()) return;
    this.submitting.set(true); this.error.set(null); this.notice.set(null);
    this.campApi.renameStructureNode(camp.id, node.id, name).subscribe({
      next: () => {
        this.submitting.set(false); this.editingStructureNodeId.set(null); this.editStructureNodeName = '';
        this.structureNodes.update(nodes => nodes.map(value => value.id === node.id ? { ...value, name } : value));
        this.notice.set('Der Strukturknoten wurde umbenannt.');
      },
      error: (response: HttpErrorResponse) => {
        this.submitting.set(false);
        this.error.set(response.error?.code === 'duplicate_structure_name'
          ? 'Auf dieser Ebene existiert bereits ein Knoten mit diesem Namen.'
          : response.status === 409 ? 'Während der Offlinephase kann der Knoten nicht umbenannt werden.'
          : 'Der Strukturknoten konnte nicht umbenannt werden.');
      }
    });
  }

  moveStructureNode(camp: CampSummary, node: StructureNodeSummary) {
    if (this.submitting()) return;
    const parentId = this.moveTargetParentIds[node.id] || null;
    this.submitting.set(true); this.error.set(null); this.notice.set(null);
    this.campApi.moveStructureNode(camp.id, node.id, parentId).subscribe({
      next: () => {
        this.submitting.set(false); this.movingStructureNodeId.set(null);
        this.structureNodes.update(nodes => nodes.map(value => value.id === node.id ? { ...value, parentId } : value));
        this.notice.set('Der Strukturzweig wurde verschoben.');
      },
      error: (response: HttpErrorResponse) => {
        this.submitting.set(false);
        const code = response.error?.code;
        this.error.set(code === 'structure_node_has_participants' ? 'Die Verschiebung würde Teilnehmer und Unterknoten am selben Knoten zusammenführen.'
          : code === 'duplicate_structure_name' ? 'Am Ziel existiert bereits ein Eintrag mit diesem Namen.'
          : code === 'maximum_structure_depth_reached' ? 'Der Zweig wäre am Ziel tiefer als erlaubt.'
          : code === 'structure_cycle' ? 'Ein Zweig kann nicht unter einen eigenen Untereintrag verschoben werden.'
          : 'Der Strukturzweig konnte nicht verschoben werden.');
      }
    });
  }

  showSection(section: ApplicationSection) {
    if (!this.mayLeave()) return;
    if (this.localDevice && !['home', 'camps'].includes(section)) section = 'home';
    if (section === 'centralIngredients' && !this.user()?.canManageCentralIngredients) section = 'home';
    if (section === 'ingredients' && !this.selectedTenant()?.canManageIngredients && !this.user()?.canManageCentralIngredients) section = 'home';
    if (section === 'organization' && !this.selectedTenant()?.canManageSettings) section = 'home';
    this.openedCampId.set(null);
    this.applicationSection.set(section);
    this.error.set(null);
    this.notice.set(null);
    this.rememberNavigation();
  }

  selectTenant(tenantId: string) {
    if (!this.mayLeave()) {
      const picker = this.tenantPicker();
      if (picker) picker.value = this.selectedTenant()?.id;
      return;
    }
    const tenant = this.tenants().find(candidate => candidate.id === tenantId) ?? null;
    this.selectedTenant.set(tenant); this.camps.set([]); this.administratorCandidates.set([]);
    this.openedCampId.set(null); this.campSection.set('general'); this.structureCampId.set(null);
    this.selectedAdministratorIds.set(new Set()); this.error.set(null); this.notice.set(null);
    this.applicationSection.set('home');
    this.rememberNavigation();
    if (!tenant) return;
    this.loadCamps(tenant.id);
    if (this.localDevice) return;
    this.loadStageTemplate(tenant.id);
    this.campApi.listAdministratorCandidates(tenant.id).subscribe({
      next: candidates => this.administratorCandidates.set(candidates),
      error: () => this.administratorCandidates.set([])
    });
  }

  private loadSession() {
    this.authenticationApi.current().subscribe({
      next: user => this.openApplication(user),
      error: response => response.status === 401 ? this.state.set('login') : this.showUnavailable()
    });
  }

  private openApplication(user: AuthenticatedUser) {
    this.sessionExpiry.signedIn();
    this.user.set(user);
    this.state.set('application');
    this.campApi.listTenants().subscribe({
      next: tenants => {
        this.tenants.set(tenants);
        const tenant = tenants.find(value => value.id === window.location.hash.slice(2).split('/')[1]) ?? tenants[0] ?? null;
        this.selectedTenant.set(tenant);
        try {
          const ids: unknown = JSON.parse(sessionStorage.getItem(`scp-recent-${user.userId}`) ?? '[]');
          this.recentIds.set(Array.isArray(ids) ? ids.filter(id => typeof id === 'string') : []);
        } catch { this.recentIds.set([]); }
        if (!tenant) { if (!this.localDevice) this.error.set('Für dieses Konto ist kein aktiver Mandant verfügbar.'); return; }
        this.loadCamps(tenant.id);
        if (this.localDevice) return;
        this.loadStageTemplate(tenant.id);
        this.campApi.listAdministratorCandidates(tenant.id).subscribe({
          next: candidates => this.administratorCandidates.set(candidates),
          error: () => this.administratorCandidates.set([])
        });
      },
      error: () => this.error.set('Die Mandanten konnten nicht geladen werden.')
    });
  }

  private loadCamps(tenantId: string) {
    this.campApi.list(tenantId).subscribe({
      next: camps => {
        if (this.selectedTenant()?.id !== tenantId) return;
        this.camps.set(camps);
        if (window.location.hash) this.restoreNavigation(true);
        else this.rememberNavigation();
      },
      error: () => this.error.set('Die Lager konnten nicht geladen werden.')
    });
  }

  private loadStageTemplate(tenantId: string) {
    forkJoin({
      entries: this.campApi.getStageTemplate(tenantId),
      factors: this.campApi.getTenantStageFoodFactors(tenantId)
    }).subscribe({
      next: ({ entries, factors }) => {
        this.editingTenantStageIndexes.set(new Set());
        this.tenantStageFoodFactors = entries.map(entry => ({
          stageName: entry.name,
          factor: factors.find(value => value.stageName === entry.name)?.factor ?? 1
        }));
        this.persistedTenantStageFoodFactors = this.tenantStageFoodFactors.map(value => ({ ...value }));
      },
      error: () => this.error.set('Die Stufenvorlage und Verpflegungsfaktoren konnten nicht geladen werden.')
    });
  }

  addTenantStage(tenantId: string) {
    const stageName = this.newStageName.trim();
    if (!stageName) return;
    const factors = [...this.tenantStageFoodFactors, { stageName, factor: 1 }];
    this.persistTenantStages(tenantId, factors, 'Die Stufe wurde hinzugefügt.', () => this.newStageName = '');
  }

  removeTenantStage(tenantId: string, index: number) {
    const factors = this.tenantStageFoodFactors.filter((_, candidateIndex) => candidateIndex !== index);
    if (factors.length === 0) {
      this.error.set('Mindestens eine Stufe muss erhalten bleiben.');
      return;
    }
    this.persistTenantStages(tenantId, factors, 'Die Stufe wurde entfernt.');
  }

  moveTenantStage(tenantId: string, index: number, direction: -1 | 1) {
    const targetIndex = index + direction;
    if (targetIndex < 0 || targetIndex >= this.tenantStageFoodFactors.length) return;
    const factors = this.tenantStageFoodFactors.map(value => ({ ...value }));
    [factors[index], factors[targetIndex]] = [factors[targetIndex], factors[index]];
    this.persistTenantStages(tenantId, factors, 'Die Reihenfolge wurde gespeichert.');
  }

  saveTenantStage(tenantId: string) {
    this.persistTenantStages(tenantId, this.tenantStageFoodFactors, 'Die Änderungen wurden gespeichert.');
  }

  toggleTenantStageEditing(index: number) {
    const next = new Set(this.editingTenantStageIndexes());
    next.has(index) ? next.delete(index) : next.add(index);
    this.editingTenantStageIndexes.set(next);
  }

  tenantStageEditing(index: number) {
    return this.editingTenantStageIndexes().has(index);
  }

  tenantStageChanged(index: number) {
    const current = this.tenantStageFoodFactors[index];
    const persisted = this.persistedTenantStageFoodFactors[index];
    return !persisted || current.stageName.trim() !== persisted.stageName || Number(current.factor) !== Number(persisted.factor);
  }

  private persistTenantStages(tenantId: string, values: TenantStageFoodFactor[], successNotice: string,
    afterSave?: () => void) {
    const factors = values.map(value => ({
      stageName: value.stageName.trim(), factor: value.factor
    }));
    const names = factors.map(value => value.stageName);
    this.submitting.set(true); this.error.set(null); this.notice.set(null);
    this.campApi.updateStageTemplate(tenantId, names).pipe(
      concatMap(() => this.campApi.updateTenantStageFoodFactors(tenantId, factors))
    ).subscribe({
      next: () => {
        this.submitting.set(false);
        this.notice.set(successNotice);
        afterSave?.();
        this.loadStageTemplate(tenantId);
      },
      error: (response: HttpErrorResponse) => { this.submitting.set(false); this.error.set(response.status === 403
        ? 'Du darfst die mandantenweite Stufenvorlage nicht ändern.'
        : 'Die Stufen enthalten ungültige oder doppelte Namen oder Faktoren.'); }
    });
  }

  private clearSession() {
    this.user.set(null); this.camps.set([]); this.tenants.set([]); this.selectedTenant.set(null);
    this.administratorCandidates.set([]); this.selectedAdministratorIds.set(new Set());
    this.editingCampId.set(null);
    this.openedCampId.set(null); this.campSection.set('general');
    this.structureCampId.set(null); this.structureNodes.set([]);
    this.error.set(null); this.notice.set(null); this.state.set('login');
  }

  private showUnavailable() {
    this.error.set('Das ScoutCampPlanner-Backend ist nicht erreichbar.'); this.state.set('unavailable');
  }

  private formatApiDate(value: Date | null) {
    if (!value) return '';
    const year = value.getFullYear();
    const month = String(value.getMonth() + 1).padStart(2, '0');
    const day = String(value.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  private parseApiDate(value: string | null) {
    if (!value) return null;
    const [year, month, day] = value.split('-').map(Number);
    return new Date(year, month - 1, day);
  }
}
