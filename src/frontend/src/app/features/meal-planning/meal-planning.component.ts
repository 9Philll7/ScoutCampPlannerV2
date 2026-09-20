import { Component, effect, inject, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatTooltipModule } from '@angular/material/tooltip';
import { ActionIconComponent } from '../../shared/action-icon.component';
import {
  CookingUnit, CookingUnitGroup, CookingUnitMeal, MealPlanDocument, MealPlanEntryDocument,
  MealPlanOfferGroupDocument, MealPlanningApiService, MealPlanningOverview, MealSlot,
  MealSubscriptionState, OfferTarget, RecipeChoice,
} from './meal-planning-api.service';

interface MealDraft {
  subscriptionState: MealSubscriptionState;
  demandOverride: number | null;
  useStructureOverride: boolean;
  structureOverrideNodeIds: string[];
  offerTargets: OfferTarget[];
  recipeChoices: RecipeChoice[];
}

@Component({
  selector: 'scp-meal-planning',
  standalone: true,
  imports: [FormsModule, MatButtonModule, MatCardModule, MatCheckboxModule, MatFormFieldModule,
    MatInputModule, MatSelectModule, MatTooltipModule, ActionIconComponent],
  template: `
    <section class="meal-planning">
      <div class="section-heading">
        <div><p class="eyebrow">Inkrement 1</p><h3>Mahlzeitenplanung</h3></div>
        <button matIconButton type="button" matTooltip="Neu laden" aria-label="Neu laden" (click)="load()">
          <scp-action-icon name="refresh"/>
        </button>
      </div>
      <p class="context-info">Plane veröffentlichte Rezeptrevisionen und berechne den anonymen Bedarf je Kocheinheit. Persönliche Anforderungen sind noch nicht Teil dieses Schritts.</p>
      @if (error()) { <p class="message error">{{ error() }}</p> }
      @if (notice()) { <p class="message success">{{ notice() }}</p> }
      @if (overview(); as data) {
        @if (data.coverageWarnings.length) {
          <div class="warning-panel"><strong>Strukturabdeckung prüfen</strong>
            @for (warning of data.coverageWarnings; track warning) { <p>{{ warning }}</p> }
          </div>
        }

        <section class="planning-section">
          <div class="section-heading"><div><h4>Mahlzeitenpläne</h4><p>Unvollständige Pläne dürfen gespeichert werden.</p></div></div>
          <div class="create-row">
            <mat-form-field appearance="outline"><mat-label>Neuer Plan</mat-label>
              <input matInput [(ngModel)]="newPlanName" name="newPlanName" maxlength="200" [disabled]="disabled()">
            </mat-form-field>
            <button matButton="filled" type="button" (click)="createPlan()" [disabled]="disabled() || !newPlanName.trim()">
              <scp-action-icon name="add"/>Plan anlegen</button>
          </div>
          <div class="card-grid">
            @for (plan of data.mealPlans; track plan.id; let planIndex = $index) {
              <mat-card class="planning-card"><mat-card-header><mat-card-title>{{ plan.name }}</mat-card-title></mat-card-header>
                <mat-card-content><p>Version {{ plan.version }}</p>
                  @if (plan.missingActiveMealCount) { <p class="status incomplete">{{ plan.missingActiveMealCount }} aktive Mahlzeit(en) ohne Standardangebot</p> }
                  @else { <p class="status current">Alle aktiven Mahlzeiten belegt</p> }
                </mat-card-content><mat-card-actions>
                  <button matButton type="button" (click)="openPlan(plan.id)"><scp-action-icon name="edit"/>Öffnen</button>
                  <button matIconButton type="button" matTooltip="Nach vorne" aria-label="Nach vorne" (click)="movePlan(planIndex, -1)" [disabled]="disabled() || $first"><scp-action-icon name="up"/></button>
                  <button matIconButton type="button" matTooltip="Nach hinten" aria-label="Nach hinten" (click)="movePlan(planIndex, 1)" [disabled]="disabled() || $last"><scp-action-icon name="down"/></button>
                  <button matIconButton type="button" class="remove-action" matTooltip="Plan löschen" aria-label="Plan löschen"
                    (click)="deletePlan(plan.id)" [disabled]="disabled()"><scp-action-icon name="remove"/></button>
                </mat-card-actions></mat-card>
            } @empty { <p>Noch kein Mahlzeitenplan vorhanden.</p> }
          </div>
        </section>

        @if (editingPlan(); as plan) {
          <section class="planning-section editor">
            <div class="section-heading"><div><h4>Plan bearbeiten</h4><p>Änderungen werden erst mit Speichern wirksam.</p></div>
              <div class="icon-actions">
                <button matIconButton type="button" matTooltip="Änderungen verwerfen" aria-label="Änderungen verwerfen" (click)="cancelPlan()"><scp-action-icon name="back"/></button>
                <button matIconButton type="button" class="save-required" matTooltip="Plan speichern" aria-label="Plan speichern"
                  (click)="savePlan()" [disabled]="disabled()"><scp-action-icon name="save"/></button>
              </div>
            </div>
            <div class="create-row"><mat-form-field appearance="outline"><mat-label>Name</mat-label>
              <input matInput [(ngModel)]="plan.name" name="planName" maxlength="200" [disabled]="disabled()">
            </mat-form-field><span>Gespeicherte Version: {{ plan.version }}</span></div>
            <div class="create-row"><mat-form-field appearance="outline"><mat-label>Aktive Lagermahlzeit</mat-label>
              <mat-select [(ngModel)]="newOfferMealId" name="newOfferMealId">
                @for (meal of activeMeals(data); track meal.id) { <mat-option [value]="meal.id">{{ meal.date }} · {{ meal.mealTypeName }}</mat-option> }
              </mat-select></mat-form-field>
              <button matButton type="button" (click)="addOfferGroup()" [disabled]="disabled() || !newOfferMealId"><scp-action-icon name="add"/>Angebotsgruppe</button>
            </div>
            @for (group of plan.offerGroups; track group.id; let groupIndex = $index) {
              <article class="offer-group"><header><div><strong>{{ mealLabel(data, group.campMealId) }}</strong>
                <input class="inline-name" [(ngModel)]="group.name" [name]="'group-name-' + group.id" placeholder="Optionaler Gruppenname" [disabled]="disabled()"></div>
                <button matIconButton type="button" class="remove-action" matTooltip="Gruppe entfernen" aria-label="Gruppe entfernen"
                  (click)="removeOfferGroup(groupIndex)" [disabled]="disabled()"><scp-action-icon name="remove"/></button></header>
                @for (entry of group.entries; track entry.id; let entryIndex = $index) {
                  <div class="entry-row">
                    <mat-checkbox [checked]="entry.isStandard" (change)="makeStandard(group, entryIndex)" [disabled]="disabled()">Standard</mat-checkbox>
                    <mat-form-field appearance="outline"><mat-label>Rezeptrevision</mat-label>
                      <mat-select [(ngModel)]="entry.recipeRevisionId" [name]="'entry-recipe-' + entry.id" [disabled]="disabled()">
                        @for (recipe of data.recipeOptions; track recipe.revisionId) { <mat-option [value]="recipe.revisionId">{{ recipe.name }} · Rev. {{ recipe.revisionNumber }}</mat-option> }
                      </mat-select></mat-form-field>
                    <mat-form-field appearance="outline"><mat-label>Rolle</mat-label><mat-select [(ngModel)]="entry.role" [name]="'entry-role-' + entry.id" [disabled]="disabled()">
                      <mat-option [value]="null">Keine</mat-option>@for (role of roles; track role.value) { <mat-option [value]="role.value">{{ role.label }}</mat-option> }
                    </mat-select></mat-form-field>
                    <mat-form-field appearance="outline"><mat-label>Hinweis</mat-label><input matInput [(ngModel)]="entry.note" [name]="'entry-note-' + entry.id" [disabled]="disabled()"></mat-form-field>
                    <button matIconButton type="button" class="remove-action" matTooltip="Eintrag entfernen" aria-label="Eintrag entfernen"
                      (click)="removeEntry(group, entryIndex)" [disabled]="disabled() || group.entries.length === 1"><scp-action-icon name="remove"/></button>
                  </div>
                }
                <button matButton type="button" (click)="addEntry(group)" [disabled]="disabled() || !data.recipeOptions.length"><scp-action-icon name="add"/>Alternative</button>
              </article>
            } @empty { <p class="status incomplete">Der Plan ist noch unvollständig.</p> }
          </section>
        }

        <section class="planning-section">
          <div class="section-heading"><div><h4>Kocheinheitengruppen</h4><p>Gruppen ordnen Kocheinheiten und haben keine Bedarfslogik.</p></div></div>
          <div class="create-row"><mat-form-field appearance="outline"><mat-label>Neue Gruppe</mat-label>
            <input matInput [(ngModel)]="newGroupName" name="newCookingGroup" [disabled]="disabled()"></mat-form-field>
            <button matButton type="button" (click)="createCookingGroup()" [disabled]="disabled() || !newGroupName.trim()"><scp-action-icon name="add"/>Gruppe</button></div>
          <div class="compact-list">@for (group of data.cookingUnitGroups; track group.id) {
            <span>{{ group.name }}</span><button matIconButton type="button" class="remove-action" matTooltip="Gruppe löschen" aria-label="Gruppe löschen"
              (click)="deleteCookingGroup(group.id)" [disabled]="disabled()"><scp-action-icon name="remove"/></button>
          }</div>
        </section>

        <section class="planning-section">
          <div class="section-heading"><div><h4>Kocheinheiten</h4><p>Standardplan und Strukturzuordnung können später pro Mahlzeit abweichen.</p></div></div>
          <div class="unit-form">
            <mat-form-field appearance="outline"><mat-label>Name</mat-label><input matInput [(ngModel)]="newUnitName" name="newUnitName" [disabled]="disabled()"></mat-form-field>
            <mat-form-field appearance="outline"><mat-label>Aus Struktur initialisieren</mat-label><mat-select [(ngModel)]="newUnitNodeId" name="newUnitNode">
              <mat-option [value]="null">Manuell</mat-option>@for (node of data.structureNodes; track node.id) { <mat-option [value]="node.id">{{ node.name }}</mat-option> }
            </mat-select></mat-form-field>
            <mat-form-field appearance="outline"><mat-label>Standardplan</mat-label><mat-select [(ngModel)]="newUnitPlanId" name="newUnitPlan">
              <mat-option [value]="null">Keiner</mat-option>@for (plan of data.mealPlans; track plan.id) { <mat-option [value]="plan.id">{{ plan.name }}</mat-option> }
            </mat-select></mat-form-field>
            <button matButton="filled" type="button" (click)="createUnit()" [disabled]="disabled() || (!newUnitName.trim() && !newUnitNodeId)"><scp-action-icon name="add"/>Kocheinheit</button>
          </div>
          @for (unit of data.cookingUnits; track unit.id) {
            <mat-card class="unit-card"><mat-card-header><mat-card-title>{{ unit.name }}</mat-card-title></mat-card-header>
              <mat-card-content>
                <div class="unit-form"><mat-form-field appearance="outline"><mat-label>Gruppe</mat-label><mat-select [(ngModel)]="unit.groupId" [name]="'unit-group-' + unit.id" [disabled]="disabled()">
                  <mat-option [value]="null">Keine</mat-option>@for (group of data.cookingUnitGroups; track group.id) { <mat-option [value]="group.id">{{ group.name }}</mat-option> }
                </mat-select></mat-form-field><mat-form-field appearance="outline"><mat-label>Standardplan</mat-label><mat-select [(ngModel)]="unit.standardMealPlanId" [name]="'unit-plan-' + unit.id" [disabled]="disabled()">
                  <mat-option [value]="null">Keiner</mat-option>@for (plan of data.mealPlans; track plan.id) { <mat-option [value]="plan.id">{{ plan.name }}</mat-option> }
                </mat-select></mat-form-field><mat-form-field appearance="outline"><mat-label>Standard-Struktur</mat-label><mat-select multiple [(ngModel)]="unit.defaultStructureNodeIds" [name]="'unit-nodes-' + unit.id" [disabled]="disabled()">
                  @for (node of data.structureNodes; track node.id) { <mat-option [value]="node.id">{{ node.name }}</mat-option> }
                </mat-select></mat-form-field>
                <button matIconButton type="button" matTooltip="Kocheinheit speichern" aria-label="Kocheinheit speichern" (click)="saveUnit(unit)" [disabled]="disabled()"><scp-action-icon name="save"/></button>
                <button matIconButton type="button" class="remove-action" matTooltip="Kocheinheit löschen" aria-label="Kocheinheit löschen" (click)="deleteUnit(unit.id)" [disabled]="disabled()"><scp-action-icon name="remove"/></button></div>
                <div class="slot-grid">@for (meal of activeMeals(data); track meal.id) {
                  @if (mealDraft(unit.id, meal.id); as draft) {
                    <article class="slot-card"><header><strong>{{ meal.date }} · {{ meal.mealTypeName }}</strong><span [class]="'status ' + statusClass(stateFor(data, unit.id, meal.id)?.status)">{{ statusLabel(stateFor(data, unit.id, meal.id)?.status) }}</span></header>
                      <mat-form-field appearance="outline"><mat-label>Planmodus</mat-label><mat-select [(ngModel)]="draft.subscriptionState" [name]="'slot-state-' + unit.id + meal.id" [disabled]="disabled()">
                        <mat-option [value]="0">Standard folgen</mat-option><mat-option [value]="1">Individuell</mat-option><mat-option [value]="2">Keine Versorgung</mat-option>
                      </mat-select></mat-form-field>
                      <mat-form-field appearance="outline"><mat-label>Bedarfs-Override</mat-label><input matInput type="number" min="0" step="0.01" [(ngModel)]="draft.demandOverride" [name]="'slot-demand-' + unit.id + meal.id" [disabled]="disabled() || draft.subscriptionState === 2"></mat-form-field>
                      <mat-checkbox [(ngModel)]="draft.useStructureOverride" [name]="'slot-structure-toggle-' + unit.id + meal.id" [disabled]="disabled()">Eigene Strukturzuordnung</mat-checkbox>
                      @if (draft.useStructureOverride) { <mat-form-field appearance="outline"><mat-label>Strukturabweichung</mat-label><mat-select multiple [(ngModel)]="draft.structureOverrideNodeIds" [name]="'slot-structure-' + unit.id + meal.id" [disabled]="disabled()">
                        @for (node of data.structureNodes; track node.id) { <mat-option [value]="node.id">{{ node.name }}</mat-option> }
                      </mat-select></mat-form-field> }
                      @for (group of offerGroupsFor(data, unit, meal.id); track group.id) {
                        <mat-form-field appearance="outline"><mat-label>Zielbedarf {{ group.name || 'Angebot ' + (group.sortOrder + 1) }}</mat-label>
                          <input matInput type="number" min="0" step="0.01" [ngModel]="offerTarget(draft, group.id)" (ngModelChange)="setOfferTarget(draft, group.id, $event)" [name]="'target-' + unit.id + meal.id + group.id" [disabled]="disabled()">
                        </mat-form-field>
                      }
                      @if (draft.subscriptionState === 1) { <div class="choices"><strong>Individuelle Rezeptwahl</strong>
                        @for (choice of draft.recipeChoices; track choice.id; let choiceIndex = $index) { <div class="choice-row"><mat-form-field appearance="outline"><mat-label>Rezeptrevision</mat-label><mat-select [(ngModel)]="choice.recipeRevisionId" [name]="'choice-' + choice.id" [disabled]="disabled()">
                          @for (recipe of data.recipeOptions; track recipe.revisionId) { <mat-option [value]="recipe.revisionId">{{ recipe.name }} · Rev. {{ recipe.revisionNumber }}</mat-option> }
                        </mat-select></mat-form-field><button matIconButton type="button" class="remove-action" (click)="draft.recipeChoices.splice(choiceIndex, 1)" [disabled]="disabled()"><scp-action-icon name="remove"/></button></div> }
                        <button matButton type="button" (click)="addChoice(draft, data)" [disabled]="disabled() || !data.recipeOptions.length"><scp-action-icon name="add"/>Rezept</button>
                      </div> }
                      <p>Berechnet: {{ stateFor(data, unit.id, meal.id)?.calculatedDemand ?? '–' }} · Wirksam: {{ stateFor(data, unit.id, meal.id)?.effectiveDemand ?? '–' }}</p>
                      @for (warning of stateFor(data, unit.id, meal.id)?.warnings || []; track warning) { <p class="status stale">{{ warning }}</p> }
                      <footer><button matButton type="button" (click)="saveMeal(unit, meal, draft)" [disabled]="disabled()"><scp-action-icon name="save"/>Einstellungen</button>
                        @if (draft.subscriptionState !== 0) { <button matButton type="button" (click)="resetPlan(unit, meal, draft)" [disabled]="disabled()">Auf Standardplan zurücksetzen</button> }
                        @if (draft.useStructureOverride) { <button matButton type="button" (click)="resetStructure(unit.id, meal.id)" [disabled]="disabled()">Struktur zurücksetzen</button> }
                        <button matButton="filled" type="button" (click)="calculate(unit.id, meal.id)" [disabled]="disabled()"><scp-action-icon name="refresh"/>Berechnen</button></footer>
                    </article>
                  }
                }</div>
              </mat-card-content></mat-card>
          } @empty { <p>Noch keine Kocheinheit vorhanden.</p> }
        </section>
      } @else { <p>Mahlzeitenplanung wird geladen …</p> }
    </section>
  `,
  styles: `
    .meal-planning, .planning-section { display: grid; gap: 1rem; }
    .planning-section { border-top: 1px solid var(--mat-sys-outline-variant); padding-top: 1.25rem; }
    .section-heading, .offer-group header, .slot-card header, .slot-card footer { display: flex; justify-content: space-between; align-items: center; gap: 1rem; }
    h3, h4, p { margin: 0; } .eyebrow { text-transform: uppercase; letter-spacing: .08em; font-size: .72rem; }
    .context-info { color: var(--mat-sys-on-surface-variant); } .create-row, .unit-form, .entry-row, .choice-row { display: flex; gap: .75rem; align-items: center; flex-wrap: wrap; }
    mat-form-field { min-width: 12rem; flex: 1 1 12rem; } .card-grid { display: grid; grid-template-columns: repeat(auto-fit,minmax(15rem,1fr)); gap: .75rem; }
    .planning-card, .unit-card { border: 1px solid var(--mat-sys-outline-variant); box-shadow: none; }
    .offer-group, .slot-card, .warning-panel { border: 1px solid var(--mat-sys-outline-variant); border-radius: 12px; padding: 1rem; display: grid; gap: .75rem; }
    .warning-panel, .status.stale { color: #8a4b00; background: #fff4df; } .status.incomplete { color: #a11; } .status.current { color: #176b36; }
    .status { border-radius: 99px; padding: .15rem .5rem; font-size: .8rem; } .inline-name { border: 0; border-bottom: 1px solid currentColor; margin-left: .75rem; }
    .compact-list { display: grid; grid-template-columns: 1fr auto; align-items: center; gap: .25rem; }
    .slot-grid { display: grid; grid-template-columns: repeat(auto-fit,minmax(19rem,1fr)); gap: .75rem; } .choices { display: grid; gap: .5rem; }
    .message { padding: .75rem; border-radius: 8px; } .message.error { background: #fde8e8; color: #900; } .message.success { background: #e7f6eb; color: #175c2f; }
    .remove-action { color: var(--mat-sys-error); } .icon-actions { display: flex; } .save-required { outline: 2px solid var(--mat-sys-primary); }
    @media (max-width: 800px) { .entry-row > mat-form-field, .unit-form > mat-form-field { flex-basis: 100%; } }
  `,
})
export class MealPlanningComponent {
  readonly campId = input.required<string>();
  readonly disabled = input(false);
  private readonly api = inject(MealPlanningApiService);
  readonly overview = signal<MealPlanningOverview | null>(null);
  readonly editingPlan = signal<MealPlanDocument | null>(null);
  readonly error = signal(''); readonly notice = signal('');
  newPlanName = ''; newOfferMealId = ''; newGroupName = '';
  newUnitName = ''; newUnitNodeId: string | null = null; newUnitPlanId: string | null = null;
  readonly roles = [
    { value: 0 as const, label: 'Hauptspeise' }, { value: 1 as const, label: 'Beilage' },
    { value: 2 as const, label: 'Vorspeise' }, { value: 3 as const, label: 'Nachspeise' },
    { value: 4 as const, label: 'Getränk' }, { value: 5 as const, label: 'Sonstiges' },
  ];
  private mealDraftMap = new Map<string, MealDraft>();

  constructor() { effect(() => { this.campId(); this.load(); }); }
  load() { this.api.overview(this.campId()).subscribe({ next: value => { this.overview.set(value); this.buildMealDrafts(value); this.error.set(''); }, error: () => this.error.set('Die Mahlzeitenplanung konnte nicht geladen werden.') }); }
  activeMeals(data: MealPlanningOverview) { return data.meals.filter(value => value.isActive && value.isInsideCampPeriod); }
  mealLabel(data: MealPlanningOverview, id: string) { const meal = data.meals.find(value => value.id === id); return meal ? `${meal.date} · ${meal.mealTypeName}` : 'Unbekannte Mahlzeit'; }
  createPlan() { this.api.createPlan(this.campId(), this.newPlanName.trim()).subscribe({ next: () => { this.newPlanName = ''; this.changed('Mahlzeitenplan angelegt.'); }, error: error => this.failed(error, 'Der Mahlzeitenplan konnte nicht angelegt werden.') }); }
  movePlan(index: number, direction: number) { const data = this.overview(); if (!data) return; const plans = [...data.mealPlans]; const target = index + direction; if (target < 0 || target >= plans.length) return; [plans[index], plans[target]] = [plans[target], plans[index]]; this.api.reorderPlans(this.campId(), plans.map(value => value.id)).subscribe({ next: () => this.changed('Planreihenfolge gespeichert.'), error: error => this.failed(error, 'Die Planreihenfolge konnte nicht gespeichert werden.') }); }
  openPlan(id: string) { this.api.plan(this.campId(), id).subscribe({ next: value => { this.editingPlan.set(structuredClone(value)); this.newOfferMealId = ''; }, error: () => this.error.set('Der Mahlzeitenplan konnte nicht geöffnet werden.') }); }
  cancelPlan() { this.editingPlan.set(null); }
  savePlan() { const plan = this.editingPlan(); if (!plan) return; this.api.savePlan(this.campId(), plan).subscribe({ next: result => { plan.version = result.version ?? plan.version; this.editingPlan.set(null); this.changed('Mahlzeitenplan gespeichert.'); }, error: error => this.failed(error, 'Der Mahlzeitenplan konnte nicht gespeichert werden.') }); }
  deletePlan(id: string) { this.api.deletePlan(this.campId(), id).subscribe({ next: () => this.changed('Mahlzeitenplan gelöscht.'), error: error => this.failed(error, 'Der Mahlzeitenplan konnte nicht gelöscht werden.') }); }
  addOfferGroup() { const plan = this.editingPlan(); if (!plan || !this.newOfferMealId) return; const groups = plan.offerGroups.filter(value => value.campMealId === this.newOfferMealId); const entry = this.newEntry(true, 0); plan.offerGroups.push({ id: crypto.randomUUID(), campMealId: this.newOfferMealId, name: null, sortOrder: groups.length, entries: [entry] }); }
  removeOfferGroup(index: number) { this.editingPlan()?.offerGroups.splice(index, 1); }
  addEntry(group: MealPlanOfferGroupDocument) { group.entries.push(this.newEntry(false, group.entries.length)); }
  removeEntry(group: MealPlanOfferGroupDocument, index: number) { const wasStandard = group.entries[index].isStandard; group.entries.splice(index, 1); if (wasStandard && group.entries.length) group.entries[0].isStandard = true; }
  makeStandard(group: MealPlanOfferGroupDocument, index: number) { group.entries.forEach((value, candidate) => value.isStandard = candidate === index); }
  private newEntry(standard: boolean, sortOrder: number): MealPlanEntryDocument { const recipe = this.overview()?.recipeOptions[0]; return { id: crypto.randomUUID(), recipeRevisionId: recipe?.revisionId ?? '', isStandard: standard, displayName: null, role: recipe?.suggestedRole ?? null, note: null, sortOrder }; }
  createCookingGroup() { const data = this.overview(); if (!data) return; this.api.createGroup(this.campId(), { name: this.newGroupName.trim(), sortOrder: data.cookingUnitGroups.length }).subscribe({ next: () => { this.newGroupName = ''; this.changed('Kocheinheitengruppe angelegt.'); }, error: error => this.failed(error, 'Die Gruppe konnte nicht angelegt werden.') }); }
  deleteCookingGroup(id: string) { this.api.deleteGroup(this.campId(), id).subscribe({ next: () => this.changed('Gruppe gelöscht; Kocheinheiten bleiben erhalten.'), error: error => this.failed(error, 'Die Gruppe konnte nicht gelöscht werden.') }); }
  createUnit() { const data = this.overview(); if (!data) return; const node = data.structureNodes.find(value => value.id === this.newUnitNodeId); this.api.createUnit(this.campId(), { name: this.newUnitName.trim() || node?.name || '', sortOrder: data.cookingUnits.length, groupId: null, standardMealPlanId: this.newUnitPlanId, defaultStructureNodeIds: this.newUnitNodeId ? [this.newUnitNodeId] : [], initialStructureNodeId: this.newUnitNodeId }).subscribe({ next: () => { this.newUnitName = ''; this.newUnitNodeId = null; this.newUnitPlanId = null; this.changed('Kocheinheit angelegt.'); }, error: error => this.failed(error, 'Die Kocheinheit konnte nicht angelegt werden.') }); }
  saveUnit(unit: CookingUnit) { this.api.saveUnit(this.campId(), unit).subscribe({ next: () => this.changed('Kocheinheit gespeichert.'), error: error => this.failed(error, 'Die Kocheinheit konnte nicht gespeichert werden.') }); }
  deleteUnit(id: string) { this.api.deleteUnit(this.campId(), id).subscribe({ next: () => this.changed('Kocheinheit und ihre operativen Teilstände wurden gelöscht.'), error: error => this.failed(error, 'Die Kocheinheit konnte nicht gelöscht werden.') }); }
  mealDraft(unitId: string, mealId: string) { return this.mealDraftMap.get(`${unitId}:${mealId}`); }
  stateFor(data: MealPlanningOverview, unitId: string, mealId: string) { return data.operationalMeals.find(value => value.cookingUnitId === unitId && value.campMealId === mealId); }
  saveMeal(unit: CookingUnit, meal: MealSlot, draft: MealDraft) { this.api.configureMeal(this.campId(), unit.id, meal.id, { subscriptionState: draft.subscriptionState, demandOverride: draft.demandOverride, structureOverrideNodeIds: draft.useStructureOverride ? draft.structureOverrideNodeIds : [], offerTargets: draft.offerTargets, recipeChoices: draft.recipeChoices }).subscribe({ next: () => this.changed('Mahlzeiteneinstellungen gespeichert.'), error: error => this.failed(error, 'Die Mahlzeiteneinstellungen konnten nicht gespeichert werden.') }); }
  resetPlan(unit: CookingUnit, meal: MealSlot, draft: MealDraft) { draft.subscriptionState = 0; draft.demandOverride = null; draft.offerTargets = []; draft.recipeChoices = []; this.saveMeal(unit, meal, draft); }
  resetStructure(unitId: string, mealId: string) { this.api.resetStructure(this.campId(), unitId, mealId).subscribe({ next: () => this.changed('Strukturabweichung zurückgesetzt.'), error: error => this.failed(error, 'Die Strukturabweichung konnte nicht zurückgesetzt werden.') }); }
  calculate(unitId: string, mealId: string) { this.api.calculate(this.campId(), unitId, mealId).subscribe({ next: () => this.changed('Verpflegungsteilstand berechnet.'), error: error => this.failed(error, 'Der Verpflegungsteilstand konnte nicht berechnet werden.') }); }
  offerGroupsFor(data: MealPlanningOverview, unit: CookingUnit, mealId: string) { const planId = unit.standardMealPlanId; return data.mealPlanDocuments.find(value => value.id === planId)?.offerGroups.filter(value => value.campMealId === mealId) ?? []; }
  offerTarget(draft: MealDraft, groupId: string) { return draft.offerTargets.find(value => value.offerGroupId === groupId)?.targetOverride ?? null; }
  setOfferTarget(draft: MealDraft, groupId: string, value: number | null) { const existing = draft.offerTargets.find(item => item.offerGroupId === groupId); if (existing) existing.targetOverride = value; else draft.offerTargets.push({ id: crypto.randomUUID(), offerGroupId: groupId, targetOverride: value }); }
  addChoice(draft: MealDraft, data: MealPlanningOverview) { draft.recipeChoices.push({ id: crypto.randomUUID(), recipeRevisionId: data.recipeOptions[0].revisionId, offerGroupId: null, mealPlanEntryId: null, sortOrder: draft.recipeChoices.length }); }
  statusLabel(value: number | undefined) { return value === 0 ? 'Aktuell' : value === 1 ? 'Veraltet' : 'Unvollständig'; }
  statusClass(value: number | undefined) { return value === 0 ? 'current' : value === 1 ? 'stale' : 'incomplete'; }
  private buildMealDrafts(data: MealPlanningOverview) { this.mealDraftMap = new Map(); for (const unit of data.cookingUnits) for (const meal of data.meals.filter(value => value.isActive)) { const state = this.stateFor(data, unit.id, meal.id); this.mealDraftMap.set(`${unit.id}:${meal.id}`, { subscriptionState: state?.subscriptionState ?? 0, demandOverride: state?.demandOverride ?? null, useStructureOverride: !!state?.structureOverrideNodeIds.length, structureOverrideNodeIds: [...(state?.structureOverrideNodeIds ?? [])], offerTargets: structuredClone(state?.offerTargets ?? []), recipeChoices: structuredClone(state?.recipeChoices ?? []) }); } }
  private changed(message: string) { this.notice.set(message); this.error.set(''); this.load(); }
  private failed(error: { error?: { message?: string; references?: string[] } }, fallback: string) { const details = error.error?.references?.join(' · '); this.error.set([error.error?.message || fallback, details].filter(Boolean).join(' ')); }
}
