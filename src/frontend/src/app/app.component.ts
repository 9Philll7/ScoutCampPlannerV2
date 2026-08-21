import { HttpErrorResponse } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
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
import { MatSelectModule } from '@angular/material/select';
import { MatToolbarModule } from '@angular/material/toolbar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { concatMap, forkJoin } from 'rxjs';
import { AuthenticationApiService, AuthenticatedUser } from './features/authentication/authentication-api.service';
import { CampAdministratorOption, CampApiService, CampMeal, CampMealType, CampPlanningSummary, CampStageFoodFactor, CampSummary, IngredientCatalogEntry, IngredientConflictType, IngredientScope, MeasurementDimension, ParticipantEstimate, StructureConfiguration, StructureNodeSummary, TenantOption, TenantStageFoodFactor, WeightedStageTotal } from './features/camp/camp-api.service';
import { SetupApiService } from './features/setup/setup-api.service';
import { ActionIconComponent } from './shared/action-icon.component';

type ViewState = 'loading' | 'setup' | 'login' | 'application' | 'unavailable';
type ApplicationSection = 'camps' | 'organization';
type CampSection = 'general' | 'structure' | 'catering';

@Component({
  selector: 'scp-root',
  standalone: true,
  imports: [FormsModule, MatButtonModule, MatButtonToggleModule, MatCardModule, MatCheckboxModule, MatFormFieldModule, MatInputModule,
    MatProgressSpinnerModule, MatSelectModule, MatToolbarModule, MatTooltipModule, MatDatepickerModule,
    ActionIconComponent],
  providers: [provideNativeDateAdapter(), { provide: MAT_DATE_LOCALE, useValue: 'de-AT' }],
  template: `
    <mat-toolbar color="primary" class="app-toolbar">
      <span class="brand"><span class="brand-mark">SCP</span><span>ScoutCampPlanner</span></span>
      @if (state() === 'application') {
        <nav class="toolbar-section-toggle" aria-label="Bereich auswählen">
          <button matButton [class.active]="applicationSection() === 'camps'" (click)="showSection('camps')">
            <scp-action-icon name="camp"/>Lager</button>
          <button matButton [class.active]="applicationSection() === 'organization'" (click)="showSection('organization')">
            <scp-action-icon name="organization"/>Organisation</button>
        </nav>
        @if (applicationSection() === 'camps' && openedCampId()) {
          <button matButton type="button" class="toolbar-back" (click)="closeCamp()">
            <scp-action-icon name="back"/>Lagerübersicht</button>
        }
      }
      <span class="toolbar-spacer"></span>
      @if (user(); as currentUser) {
        <span class="user-email">{{ currentUser.email }}</span>
        <button matButton (click)="signOut()"><scp-action-icon name="logout"/>Abmelden</button>
      }
    </mat-toolbar>
    <main>
      @switch (state()) {
        @case ('loading') {
          <section class="centered"><mat-spinner diameter="42"/><p>ScoutCampPlanner wird geladen …</p></section>
        }
        @case ('setup') {
          <mat-card class="account-card">
            <mat-card-header>
              <mat-card-title>ScoutCampPlanner einrichten</mat-card-title>
              <mat-card-subtitle>Lege die erste Organisation und das Administratorkonto an.</mat-card-subtitle>
            </mat-card-header>
            <mat-card-content>
              <form id="initial-setup" (ngSubmit)="completeSetup()">
                <mat-form-field appearance="outline"><mat-label>Organisation</mat-label>
                  <input matInput name="tenantName" [(ngModel)]="tenantName" maxlength="200" required autocomplete="organization">
                </mat-form-field>
                <mat-form-field appearance="outline"><mat-label>E-Mail-Adresse</mat-label>
                  <input matInput name="setupEmail" [(ngModel)]="email" maxlength="320" required type="email" autocomplete="username">
                </mat-form-field>
                <mat-form-field appearance="outline"><mat-label>Passwort</mat-label>
                  <input matInput name="setupPassword" [(ngModel)]="password" maxlength="128" required type="password" autocomplete="new-password">
                  <mat-hint>Mindestens 8 Zeichen; eine lange Passphrase wird empfohlen.</mat-hint>
                </mat-form-field>
              </form>
              @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
            </mat-card-content>
            <mat-card-actions align="end"><button matButton="filled" form="initial-setup" type="submit" [disabled]="submitting()"><scp-action-icon name="save"/>Einrichtung abschließen</button></mat-card-actions>
          </mat-card>
        }
        @case ('login') {
          <mat-card class="account-card">
            <mat-card-header><mat-card-title>Anmelden</mat-card-title></mat-card-header>
            <mat-card-content>
              <form id="login" (ngSubmit)="signIn()">
                <mat-form-field appearance="outline"><mat-label>E-Mail-Adresse</mat-label>
                  <input matInput name="loginEmail" [(ngModel)]="email" required type="email" autocomplete="username">
                </mat-form-field>
                <mat-form-field appearance="outline"><mat-label>Passwort</mat-label>
                  <input matInput name="loginPassword" [(ngModel)]="password" required type="password" autocomplete="current-password">
                </mat-form-field>
              </form>
              @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
            </mat-card-content>
            <mat-card-actions align="end"><button matButton="filled" form="login" type="submit" [disabled]="submitting()"><scp-action-icon name="login"/>Anmelden</button></mat-card-actions>
          </mat-card>
        }
        @case ('application') {
          <div class="application-shell">
          <header class="page-header">
            <div>
              @if (applicationSection() === 'camps' && openedCampId()) {
                <h1>{{ openedCamp()?.name }}</h1>
              } @else {
              <p class="eyebrow">{{ selectedTenant()?.name ?? 'ScoutCampPlanner' }}</p>
              <h1>{{ applicationSection() === 'camps' ? 'Lager' : 'Organisation' }}</h1>
              <p class="page-description">{{ applicationSection() === 'camps'
                ? 'Lager anlegen, öffnen und für den Offlinebetrieb vorbereiten.'
                : 'Mandantenweite Vorgaben für zukünftige Lager verwalten.' }}</p>
              }
            </div>
            @if (tenants().length > 1) {
              <mat-form-field appearance="outline" class="tenant-select"><mat-label>Organisation</mat-label>
                <mat-select [ngModel]="selectedTenant()?.id" (ngModelChange)="selectTenant($event)">
                  @for (tenant of tenants(); track tenant.id) { <mat-option [value]="tenant.id">{{ tenant.name }}</mat-option> }
                </mat-select>
              </mat-form-field>
            }
          </header>
          @if (error()) { <p class="message error" role="alert">{{ error() }}</p> }
          @if (notice()) { <p class="message notice" role="status">{{ notice() }}</p> }
          @if (applicationSection() === 'organization') {
          @if (selectedTenant(); as tenant) {
            <mat-card class="content-card">
              <mat-card-header><mat-card-title>Stufenvorlage</mat-card-title>
                <mat-card-subtitle>Für zukünftige Lager von {{ tenant.name }}</mat-card-subtitle>
              </mat-card-header>
              <mat-card-content>
                <form id="stage-template" (ngSubmit)="addTenantStage(tenant.id)">
                  <p class="context-info">Der KiJu-Faktor wird für die Verpflegungsplanung verwendet. Leiter zählen immer mit Faktor 1,0.</p>
                  <div class="stage-grid">
                    @for (entry of tenantStageFoodFactors; track $index) {
                      <section class="stage-card">
                        <div class="stage-card-toolbar">
                          <button matIconButton type="button" class="icon-only" aria-label="Stufe nach vorne verschieben"
                            matTooltip="Nach vorne verschieben" (click)="moveTenantStage(tenant.id, $index, -1)"
                            [disabled]="submitting() || $first"><scp-action-icon name="up"/></button>
                          <button matIconButton type="button" class="icon-only" aria-label="Stufe nach hinten verschieben"
                            matTooltip="Nach hinten verschieben" (click)="moveTenantStage(tenant.id, $index, 1)"
                            [disabled]="submitting() || $last"><scp-action-icon name="down"/></button>
                          @if (tenantStageEditing($index) && tenantStageChanged($index)) {
                            <button matIconButton type="button" class="icon-only save-required" aria-label="Änderungen speichern"
                              matTooltip="Änderungen speichern" (click)="saveTenantStage(tenant.id)"
                              [disabled]="submitting()"><scp-action-icon name="save"/></button>
                          } @else {
                            <button matIconButton type="button" class="icon-only"
                              [class.editing-active]="tenantStageEditing($index)"
                              [attr.aria-label]="tenantStageEditing($index) ? 'Bearbeitung beenden' : 'Stufe bearbeiten'"
                              [matTooltip]="tenantStageEditing($index) ? 'Bearbeitung beenden' : 'Stufe bearbeiten'"
                              (click)="toggleTenantStageEditing($index)"
                              [disabled]="submitting()"><scp-action-icon name="edit"/></button>
                          }
                          <button matIconButton type="button" class="remove-action icon-only" aria-label="Stufe entfernen"
                            matTooltip="Stufe entfernen" (click)="removeTenantStage(tenant.id, $index)"
                            [disabled]="submitting()"><scp-action-icon name="remove"/></button>
                        </div>
                        <mat-form-field appearance="outline"><mat-label>Stufenname</mat-label>
                          <input matInput [(ngModel)]="entry.stageName" [name]="'stageName-' + $index"
                            maxlength="100" required [disabled]="!tenantStageEditing($index) || submitting()">
                        </mat-form-field>
                        <mat-form-field appearance="outline"><mat-label>KiJu-Faktor</mat-label>
                          <input matInput type="number" min="0.1" max="3" step="0.01"
                            [(ngModel)]="entry.factor" [name]="'foodFactor-' + $index" required
                            [disabled]="!tenantStageEditing($index) || submitting()">
                        </mat-form-field>
                      </section>
                    } @empty {
                      <p class="stage-empty">Noch keine Stufen konfiguriert.</p>
                    }
                  </div>
                  <div class="add-stage-row">
                    <mat-form-field appearance="outline"><mat-label>Neue Stufe</mat-label>
                      <input matInput name="newStageName" [(ngModel)]="newStageName" maxlength="100">
                    </mat-form-field>
                    <button matButton type="submit"
                      [disabled]="submitting() || !newStageName.trim()"><scp-action-icon name="add"/>Stufe hinzufügen</button>
                  </div>
                </form>
              </mat-card-content>
            </mat-card>
          }
          } @else {
          @if (openedCamp(); as opened) {
            <nav class="section-navigation camp-navigation" aria-label="Lagernavigation">
              <button matButton [class.active]="campSection() === 'general'" (click)="campSection.set('general')">
                <scp-action-icon name="edit"/>Grundeinstellungen</button>
              <button matButton [class.active]="campSection() === 'structure'" (click)="openCampStructure(opened)">
                <scp-action-icon name="structure"/>Lagerstruktur</button>
              <button matButton [class.active]="campSection() === 'catering'" (click)="openCampCatering(opened)">
                <scp-action-icon name="planning"/>Verpflegung</button>
            </nav>
          }
          @if (!openedCampId()) {
          @if (selectedTenant(); as tenant) {
            <mat-card class="content-card create-camp-card">
              <mat-card-header>
                <mat-card-title>Neues Lager anlegen</mat-card-title>
                <mat-card-subtitle>{{ tenant.name }}</mat-card-subtitle>
              </mat-card-header>
              <mat-card-content>
                <form id="create-camp" (ngSubmit)="createCamp()">
                  <mat-form-field appearance="outline"><mat-label>Name des Lagers</mat-label>
                    <input matInput name="campName" [(ngModel)]="campName" maxlength="200" required>
                  </mat-form-field>
                  <mat-form-field appearance="outline"><mat-label>Startdatum</mat-label>
                    <input matInput name="campStartDate" [(ngModel)]="campStartDate" [matDatepicker]="campStartPicker" required>
                    <mat-datepicker-toggle matIconSuffix [for]="campStartPicker"/>
                    <mat-datepicker #campStartPicker/>
                  </mat-form-field>
                  <mat-form-field appearance="outline"><mat-label>Enddatum</mat-label>
                    <input matInput name="campEndDate" [(ngModel)]="campEndDate" [matDatepicker]="campEndPicker" required>
                    <mat-datepicker-toggle matIconSuffix [for]="campEndPicker"/>
                    <mat-datepicker #campEndPicker/>
                  </mat-form-field>
                  <p>Mindestens einen Camp-Administrator auswählen:</p>
                  @for (candidate of administratorCandidates(); track candidate.membershipId) {
                    <mat-checkbox [checked]="isAdministratorSelected(candidate.membershipId)"
                      (change)="setAdministratorSelected(candidate.membershipId, $event.checked)">
                      {{ candidate.email }}
                    </mat-checkbox>
                  } @empty {
                    <p>Keine berechtigten Mitglieder verfügbar.</p>
                  }
                </form>
              </mat-card-content>
              <mat-card-actions align="end">
                <button matButton="filled" form="create-camp" type="submit" [disabled]="submitting()"><scp-action-icon name="add"/>Lager anlegen</button>
              </mat-card-actions>
            </mat-card>
          }
          }
          @for (camp of visibleCamps(); track camp.id) {
            <mat-card class="content-card camp-card">
              @if (!openedCampId()) {
                <mat-card-header><mat-card-title>{{ camp.name }}</mat-card-title></mat-card-header>
              }
              <mat-card-content>
                @if (!openedCampId()) {
                  <p>{{ camp.startDate && camp.endDate ? camp.startDate + ' bis ' + camp.endDate : 'Legacy-Lager ohne Zeitraum' }}</p>
                  <p>{{ camp.isFrozen ? 'Offlinephase aktiv' : 'Online bearbeitbar' }}</p>
                } @else {
                @if (campSection() === 'general') {
                <section class="settings-section">
                  <div class="section-heading">
                    <div><p class="eyebrow">Allgemein</p><h3>Lagerdaten</h3></div>
                    <div class="section-actions">
                      @if (editingCampId() === camp.id) {
                        <button matButton type="button" (click)="cancelCampEdit()"><scp-action-icon name="back"/>Abbrechen</button>
                        <button matIconButton type="submit" class="icon-only" aria-label="Lager speichern"
                          matTooltip="Lager speichern" [attr.form]="'edit-camp-' + camp.id" [disabled]="submitting()">
                          <scp-action-icon name="save"/></button>
                      } @else {
                        @if (camp.canEdit) {
                          <button matIconButton class="icon-only" aria-label="Lager bearbeiten" matTooltip="Lager bearbeiten"
                            (click)="editCamp(camp)" [disabled]="camp.isFrozen"><scp-action-icon name="edit"/></button>
                        }
                      }
                    </div>
                  </div>
                @if (editingCampId() === camp.id) {
                  <form [id]="'edit-camp-' + camp.id" (ngSubmit)="saveCamp(camp)">
                    <mat-form-field appearance="outline"><mat-label>Name des Lagers</mat-label>
                      <input matInput name="editCampName" [(ngModel)]="editCampName" maxlength="200" required>
                    </mat-form-field>
                    <mat-form-field appearance="outline"><mat-label>Startdatum</mat-label>
                      <input matInput name="editCampStartDate" [(ngModel)]="editCampStartDate" [matDatepicker]="editCampStartPicker" required>
                      <mat-datepicker-toggle matIconSuffix [for]="editCampStartPicker"/>
                      <mat-datepicker #editCampStartPicker/>
                    </mat-form-field>
                    <mat-form-field appearance="outline"><mat-label>Enddatum</mat-label>
                      <input matInput name="editCampEndDate" [(ngModel)]="editCampEndDate" [matDatepicker]="editCampEndPicker" required>
                      <mat-datepicker-toggle matIconSuffix [for]="editCampEndPicker"/>
                      <mat-datepicker #editCampEndPicker/>
                    </mat-form-field>
                  </form>
                } @else {
                  <p>{{ camp.startDate && camp.endDate ? camp.startDate + ' bis ' + camp.endDate : 'Legacy-Lager ohne Zeitraum' }}</p>
                  <p>{{ camp.isFrozen ? 'Offlinephase aktiv' : 'Online bearbeitbar' }}</p>
                }
                </section>
                <section class="settings-section structure-settings">
                  <div class="section-heading">
                    <div><p class="eyebrow">Strukturadministration</p><h3>Strukturtiefe</h3></div>
                    @if (camp.canEdit && structureConfigurationChanged()) {
                      <button matIconButton type="button" class="icon-only save-required"
                        aria-label="Strukturkonfiguration speichern" matTooltip="Strukturkonfiguration speichern"
                        (click)="saveStructureConfiguration(camp)" [disabled]="submitting() || camp.isFrozen">
                        <scp-action-icon name="save"/></button>
                    }
                  </div>
                  <mat-button-toggle-group aria-label="Strukturmodus" [ngModel]="structureMode()"
                    (ngModelChange)="setStructureMode($event)" [disabled]="!camp.canEdit || camp.isFrozen">
                    <mat-button-toggle value="Free">Freie Struktur</mat-button-toggle>
                    <mat-button-toggle value="Fixed">Definierte Ebenen</mat-button-toggle>
                  </mat-button-toggle-group>
                  @if (structureMode() === 'Free') {
                    <p class="context-info">Knoten können ohne vorgegebene maximale Tiefe angelegt werden.</p>
                  } @else {
                    <div class="level-list">
                      @for (level of structureLevelNames(); track $index) {
                        <div class="level-row">
                          <span class="level-number">{{ $index + 1 }}</span>
                          <mat-form-field appearance="outline"><mat-label>Bezeichnung der Ebene</mat-label>
                            <input matInput [ngModel]="level" (ngModelChange)="renameStructureLevel($index, $event)"
                              [name]="'structureLevel-' + $index" maxlength="100" required
                              [disabled]="!camp.canEdit || camp.isFrozen">
                          </mat-form-field>
                          <div class="level-actions">
                            <button matIconButton type="button" class="icon-only" aria-label="Ebene nach oben verschieben"
                              matTooltip="Nach oben verschieben" (click)="moveStructureLevel($index, -1)"
                              [disabled]="$first || !camp.canEdit || camp.isFrozen"><scp-action-icon name="up"/></button>
                            <button matIconButton type="button" class="icon-only" aria-label="Ebene nach unten verschieben"
                              matTooltip="Nach unten verschieben" (click)="moveStructureLevel($index, 1)"
                              [disabled]="$last || !camp.canEdit || camp.isFrozen"><scp-action-icon name="down"/></button>
                            <button matIconButton type="button" class="icon-only remove-action" aria-label="Ebene entfernen"
                              matTooltip="Ebene entfernen" (click)="removeStructureLevel($index)"
                              [disabled]="structureLevelNames().length === 1 || !camp.canEdit || camp.isFrozen">
                              <scp-action-icon name="remove"/></button>
                          </div>
                        </div>
                      }
                    </div>
                    @if (camp.canEdit) {
                      <button matButton type="button" class="add-level-button" (click)="addStructureLevel()"
                        [disabled]="camp.isFrozen"><scp-action-icon name="add"/>Ebene hinzufügen</button>
                    }
                  }
                </section>
                <section class="settings-section">
                  <div class="section-heading"><div><p class="eyebrow">Planungsgrundlage</p><h3>Lagerstufen</h3></div></div>
                  <p class="context-info">Diese Stufen gelten nur für dieses Lager. Der KiJu-Faktor wird für die Verpflegungsplanung verwendet; Leiter zählen immer mit Faktor 1,0.</p>
                  @if (campStagesLoading()) {
                    <div class="inline-loading"><mat-spinner diameter="28"/><span>Lagerstufen werden geladen …</span></div>
                  }
                  <div class="stage-grid">
                    @for (entry of campStageFoodFactors(); track entry.campStageId) {
                      <section class="stage-card">
                        <div class="stage-card-toolbar">
                          <button matIconButton type="button" class="icon-only" aria-label="Stufe nach vorne verschieben"
                            matTooltip="Nach vorne verschieben" (click)="moveCampStage(camp, $index, -1)"
                            [disabled]="submitting() || $first || camp.isFrozen"><scp-action-icon name="up"/></button>
                          <button matIconButton type="button" class="icon-only" aria-label="Stufe nach hinten verschieben"
                            matTooltip="Nach hinten verschieben" (click)="moveCampStage(camp, $index, 1)"
                            [disabled]="submitting() || $last || camp.isFrozen"><scp-action-icon name="down"/></button>
                          @if (campStageEditing($index) && campStageChanged($index)) {
                            <button matIconButton type="button" class="icon-only save-required" aria-label="Änderungen speichern"
                              matTooltip="Änderungen speichern" (click)="saveCampStage(camp)"
                              [disabled]="submitting() || camp.isFrozen"><scp-action-icon name="save"/></button>
                          } @else {
                            <button matIconButton type="button" class="icon-only" [class.editing-active]="campStageEditing($index)"
                              [attr.aria-label]="campStageEditing($index) ? 'Bearbeitung beenden' : 'Stufe bearbeiten'"
                              [matTooltip]="campStageEditing($index) ? 'Bearbeitung beenden' : 'Stufe bearbeiten'"
                              (click)="toggleCampStageEditing($index)" [disabled]="submitting() || camp.isFrozen">
                              <scp-action-icon name="edit"/></button>
                          }
                          <button matIconButton type="button" class="remove-action icon-only" aria-label="Stufe entfernen"
                            matTooltip="Stufe entfernen" (click)="removeCampStage(camp, $index)"
                            [disabled]="submitting() || camp.isFrozen"><scp-action-icon name="remove"/></button>
                        </div>
                        <mat-form-field appearance="outline"><mat-label>Stufenname</mat-label>
                          <input matInput [(ngModel)]="entry.stageName" [name]="'campStageName-' + entry.campStageId"
                            maxlength="100" required [disabled]="!campStageEditing($index) || submitting()">
                        </mat-form-field>
                        <mat-form-field appearance="outline"><mat-label>KiJu-Faktor</mat-label>
                          <input matInput type="number" min="0.1" max="3" step="0.01"
                            [(ngModel)]="entry.factor" [name]="'campFoodFactor-' + entry.campStageId" required
                            [disabled]="!campStageEditing($index) || submitting()">
                        </mat-form-field>
                      </section>
                    }
                  </div>
                  @if (camp.canEdit) {
                    <form class="add-stage-row" (ngSubmit)="addCampStage(camp)">
                      <mat-form-field appearance="outline"><mat-label>Neue Lagerstufe</mat-label>
                        <input matInput name="newCampStageName" [(ngModel)]="newCampStageName" maxlength="100">
                      </mat-form-field>
                      <button matButton type="submit" [disabled]="submitting() || camp.isFrozen || !newCampStageName.trim()">
                        <scp-action-icon name="add"/>Stufe hinzufügen</button>
                    </form>
                  }
                </section>
                }
                @if (campSection() === 'structure' && structureCampId() === camp.id) {
                    <section class="structure-tree" aria-label="Lagerstruktur">
                      <div class="structure-node-row camp-root-node">
                        <div class="structure-node-main">
                          @if (structureNodes().length > 0) {
                            <button matIconButton type="button" class="icon-only structure-collapse-button"
                              [attr.aria-label]="isStructureNodeCollapsed('camp') ? 'Lagerstruktur ausklappen' : 'Lagerstruktur einklappen'"
                              [matTooltip]="isStructureNodeCollapsed('camp') ? 'Ausklappen' : 'Einklappen'"
                              (click)="toggleStructureNodeCollapsed('camp')">
                              <scp-action-icon [name]="isStructureNodeCollapsed('camp') ? 'expand' : 'collapse'"/></button>
                          }
                          <span class="structure-node-marker"></span><div>
                          <span class="structure-level-badge">Lager</span><strong>{{ camp.name }}</strong>
                          <small>KiJu: {{ campChildYouthTotal() }} · Leiter: {{ campLeaderTotal() }}</small>
                        </div></div>
                        <div class="structure-node-actions">
                          <button matIconButton type="button" class="icon-only"
                            [class.editing-active]="planningDetailsVisible()" aria-label="Planungsdetails anzeigen"
                            matTooltip="Planungsdetails" (click)="planningDetailsVisible.update(value => !value)">
                            <scp-action-icon name="info"/></button>
                          @if (camp.canEdit) {
                            <button matIconButton type="button" class="icon-only" aria-label="Knoten hinzufügen"
                              matTooltip="Knoten hinzufügen" (click)="toggleCreateStructureNode(null)"
                              [disabled]="submitting() || camp.isFrozen"><scp-action-icon name="add"/></button>
                          }
                        </div>
                        @if (planningDetailsVisible() && planningSummary(); as summary) {
                          <div class="structure-planning-editor"><table><thead><tr><th>Stufe</th><th>KiJu</th><th>Leiter</th></tr></thead><tbody>
                            @for (total of summary.stageTotals; track total.campStageId) {
                              <tr><td>{{ total.stageName }}</td><td>{{ total.childYouthCount }}</td><td>{{ total.leaderCount }}</td></tr>
                            }
                          </tbody></table></div>
                        }
                        @if (creatingStructureParent() === 'camp') {
                          <form class="structure-create-editor" (ngSubmit)="createStructureNode(camp)">
                            <mat-form-field appearance="outline"><mat-label>Name des neuen Knotens</mat-label>
                              <input matInput name="rootStructureName" [(ngModel)]="newStructureNodeName" maxlength="200" required>
                            </mat-form-field>
                            <button matButton="filled" type="submit" [disabled]="submitting() || !newStructureNodeName.trim()">
                              <scp-action-icon name="add"/>Anlegen</button>
                          </form>
                        }
                      </div>
                    @for (row of structureRows(); track row.node.id) {
                      <div class="structure-node-row" [style.--tree-depth]="row.depth + 1">
                        <div class="structure-node-main">
                          @if (!isStructureLeaf(row.node)) {
                            <button matIconButton type="button" class="icon-only structure-collapse-button"
                              [attr.aria-label]="isStructureNodeCollapsed(row.node.id) ? 'Unterknoten ausklappen' : 'Unterknoten einklappen'"
                              [matTooltip]="isStructureNodeCollapsed(row.node.id) ? 'Ausklappen' : 'Einklappen'"
                              (click)="toggleStructureNodeCollapsed(row.node.id)">
                              <scp-action-icon [name]="isStructureNodeCollapsed(row.node.id) ? 'expand' : 'collapse'"/></button>
                          }
                          <span class="structure-node-marker"></span><div>
                          @if (structureLevelLabel(row.depth); as levelLabel) {
                            <span class="structure-level-badge">{{ levelLabel }}</span>
                          }
                          @if (editingStructureNodeId() === row.node.id) {
                            <mat-form-field appearance="outline" class="structure-node-name-field">
                              <mat-label>Name des Knotens</mat-label>
                              <input matInput [(ngModel)]="editStructureNodeName" [ngModelOptions]="{ standalone: true }"
                                maxlength="200" required
                                (keydown.enter)="renameStructureNode(camp, row.node)"
                                (keydown.escape)="toggleRenameStructureNode(row.node)">
                            </mat-form-field>
                          } @else {
                            <strong>{{ row.node.name }}</strong>
                          }
                        @if (structureTotal(row.node.id); as total) {
                          <small>KiJu: {{ total.childYouthCount }} · Leiter: {{ total.leaderCount }}</small>
                        }
                        </div></div>
                        @if (camp.canEdit) {
                          <div class="structure-node-actions">
                          @if (canPlanOnStructureRow(row)) {
                            <button matButton type="button" [class.editing-active]="estimateNodeId() === row.node.id"
                              (click)="openEstimates(camp, row.node)"><scp-action-icon name="planning"/>Planung</button>
                          }
                          @if (canAddStructureChild(row)) {
                            <button matIconButton type="button" class="icon-only" aria-label="Unterknoten hinzufügen"
                              matTooltip="Unterknoten hinzufügen" (click)="toggleCreateStructureNode(row.node.id)"
                              [disabled]="submitting() || camp.isFrozen"><scp-action-icon name="add"/></button>
                          }
                          @if (editingStructureNodeId() === row.node.id && hasStructureNodeNameChanged(row.node)) {
                            <button matIconButton type="button" class="icon-only save-required"
                              aria-label="Neuen Namen speichern" matTooltip="Neuen Namen speichern"
                              (click)="renameStructureNode(camp, row.node)"
                              [disabled]="submitting() || !editStructureNodeName.trim() || camp.isFrozen">
                              <scp-action-icon name="save"/></button>
                          } @else {
                            <button matIconButton type="button" class="icon-only"
                              [class.editing-active]="editingStructureNodeId() === row.node.id"
                              [attr.aria-label]="editingStructureNodeId() === row.node.id ? 'Umbenennen beenden' : 'Eintrag umbenennen'"
                              [matTooltip]="editingStructureNodeId() === row.node.id ? 'Umbenennen beenden' : 'Eintrag umbenennen'"
                              (click)="toggleRenameStructureNode(row.node)" [disabled]="submitting() || camp.isFrozen">
                              <scp-action-icon name="edit"/></button>
                          }
                          <button matIconButton type="button" class="icon-only"
                            [class.editing-active]="movingStructureNodeId() === row.node.id"
                            [attr.aria-label]="movingStructureNodeId() === row.node.id ? 'Verschieben beenden' : 'Eintrag verschieben'"
                            [matTooltip]="movingStructureNodeId() === row.node.id ? 'Verschieben beenden' : 'Eintrag verschieben'"
                            (click)="toggleMoveStructureNode(row.node)" [disabled]="submitting() || camp.isFrozen">
                            <scp-action-icon name="move"/></button>
                          <span class="icon-button-tooltip" [matTooltip]="structureDeleteTooltip(row.node, camp)">
                            <button matIconButton type="button" class="icon-only remove-action" aria-label="Eintrag löschen"
                              (click)="deleteStructureNode(camp, row.node)"
                              [disabled]="submitting() || camp.isFrozen || !canDeleteStructureNode(row.node)">
                              <scp-action-icon name="remove"/></button>
                          </span>
                          </div>
                        }
                        @if (creatingStructureParent() === row.node.id) {
                          <form class="structure-create-editor" (ngSubmit)="createStructureNode(camp)">
                            <mat-form-field appearance="outline"><mat-label>Neuer Unterknoten</mat-label>
                              <input matInput name="childStructureName" [(ngModel)]="newStructureNodeName" maxlength="200" required>
                            </mat-form-field>
                            <button matButton="filled" type="submit" [disabled]="submitting() || !newStructureNodeName.trim()">
                              <scp-action-icon name="add"/>Anlegen</button>
                          </form>
                        }
                        @if (movingStructureNodeId() === row.node.id) {
                          <div class="structure-move-editor">
                            <mat-form-field appearance="outline"><mat-label>Neuer übergeordneter Knoten</mat-label>
                              <mat-select [name]="'moveTarget-' + row.node.id" [(ngModel)]="moveTargetParentIds[row.node.id]">
                                <mat-option value="">Oberste Ebene</mat-option>
                                @for (target of moveTargets(row.node); track target.id) {
                                  <mat-option [value]="target.id">{{ structureNodePath(target) }}</mat-option>
                                }
                              </mat-select>
                            </mat-form-field>
                            <button matButton="filled" type="button" (click)="moveStructureNode(camp, row.node)"
                              [disabled]="submitting() || camp.isFrozen"><scp-action-icon name="move"/>Verschieben bestätigen</button>
                          </div>
                        }
                        @if (estimateNodeId() === row.node.id) {
                          <section class="structure-planning-editor">
                            <div class="section-heading"><div><p class="eyebrow">Anonyme Planung</p><h3>Teilnehmerschätzung</h3></div>
                              @if (camp.canEdit) {
                                @if (hasPersistedParticipantEstimates()) {
                                  <button matButton type="button" class="remove-action" (click)="deleteEstimates(camp)"
                                    [disabled]="submitting() || camp.isFrozen"><scp-action-icon name="remove"/>Schätzung löschen</button>
                                }
                                <button matIconButton type="button" class="icon-only" aria-label="Schätzung speichern"
                                  matTooltip="Schätzung speichern" (click)="saveEstimates(camp)"><scp-action-icon name="save"/></button>
                              }
                            </div>
                            <table><thead><tr><th>Stufe</th><th>KiJu</th><th>Leiter</th></tr></thead><tbody>
                              @for (estimate of participantEstimates(); track estimate.campStageId) {
                                <tr><td>{{ estimate.stageName }}</td>
                                  <td><input type="number" min="0" [(ngModel)]="estimate.childYouthCount"></td>
                                  <td><input type="number" min="0" [(ngModel)]="estimate.leaderCount"></td></tr>
                              }
                            </tbody></table>
                          </section>
                        }
                      </div>
                    }
                    @if (structureNodes().length === 0) {
                      <p>Noch keine Struktureinträge vorhanden.</p>
                    }
                    </section>
                  }
                  @if (campSection() === 'catering') {
                    <section class="settings-section">
                      <div class="section-heading"><div><p class="eyebrow">Mahlzeiten</p><h3>Tagesplan</h3></div>
                        @if (camp.canEdit) {
                          <button matIconButton type="button" class="icon-only save-required" aria-label="Mahlzeitenbezeichnungen speichern"
                            matTooltip="Mahlzeitenbezeichnungen speichern" (click)="saveMealTypes(camp)"
                            [disabled]="submitting() || camp.isFrozen || !mealTypesChanged()"><scp-action-icon name="save"/></button>
                        }
                      </div>
                      <p class="context-info">Alle Mahlzeiten sind zunächst an jedem Lagertag aktiv. Deaktiviere einzelne Einträge etwa für An- und Abreisetage.</p>
                      <div class="meal-type-list">
                        @for (type of mealTypes(); track type.id; let index = $index) {
                          <div class="meal-type-entry"><mat-form-field appearance="outline"><mat-label>Mahlzeitenbezeichnung</mat-label>
                            <input matInput [ngModel]="type.name" (ngModelChange)="updateMealTypeName(index, $event)"
                              [name]="'mealType-' + type.id" maxlength="100" [disabled]="!camp.canEdit || camp.isFrozen">
                          </mat-form-field>
                          @if (camp.canEdit && !camp.isFrozen) {
                            <button matIconButton type="button" class="icon-only" aria-label="Bezeichnung nach vorne verschieben"
                              matTooltip="Nach vorne verschieben" (click)="moveMealType(index, -1)" [disabled]="$first">
                              <scp-action-icon name="up"/></button>
                            <button matIconButton type="button" class="icon-only" aria-label="Bezeichnung nach hinten verschieben"
                              matTooltip="Nach hinten verschieben" (click)="moveMealType(index, 1)" [disabled]="$last">
                              <scp-action-icon name="down"/></button>
                            <button matIconButton type="button" class="icon-only remove-action" aria-label="Bezeichnung entfernen"
                              matTooltip="Bezeichnung entfernen" (click)="removeMealType(index)" [disabled]="mealTypes().length <= 1">
                              <scp-action-icon name="remove"/></button>
                          }</div>
                        }
                        @if (camp.canEdit && !camp.isFrozen) {
                          <button matButton type="button" (click)="addMealType()"><scp-action-icon name="add"/>Bezeichnung hinzufügen</button>
                        }
                      </div>
                      @if (mealDates().length) {
                        <div class="meal-plan-table"><table><thead><tr><th>Datum</th>
                          @for (type of mealTypes(); track type.id) { <th>{{ type.name }}</th> }
                        </tr></thead><tbody>
                          @for (date of mealDates(); track date) {
                            <tr><td>{{ date }}</td>
                              @for (type of mealTypes(); track type.id) {
                                <td>@if (mealFor(date, type.id); as meal) {
                                  <mat-checkbox [checked]="meal.isActive" (change)="setMealActivity(camp, meal, $event.checked)"
                                    [disabled]="!camp.canEdit || camp.isFrozen || submitting()"/>
                                }</td>
                              }
                            </tr>
                          }
                        </tbody></table></div>
                      }
                    </section>
                    <section class="settings-section">
                      <div class="section-heading"><div><p class="eyebrow">Verpflegungsplanung</p><h3>Gewichtete Verpflegungseinheiten</h3></div></div>
                      <p class="context-info">Die Einheiten ergeben sich aus den Schätzwerten der Lagerstruktur und den Faktoren der Lagerstufen.</p>
                      @if (weightedFoodTotals().length) {
                        <table><thead><tr><th>Stufe</th><th>KiJu</th><th>Leiter</th><th>Faktor</th><th>Einheiten</th></tr></thead><tbody>
                          @for (total of weightedFoodTotals(); track total.campStageId) {
                            <tr><td>{{ total.stageName }}</td><td>{{ total.childYouthCount }}</td><td>{{ total.leaderCount }}</td>
                              <td>{{ total.factor }}</td><td><strong>{{ total.foodUnits }}</strong></td></tr>
                          }
                        </tbody></table>
                      } @else { <p>Noch keine Verpflegungseinheiten vorhanden.</p> }
                    </section>
                    <section class="settings-section">
                      <div class="section-heading"><div><p class="eyebrow">Zutatenkatalog</p><h3>Verfügbare Zutaten</h3></div>
                        <div class="section-actions"><span class="catalog-count">{{ filteredIngredients().length }} von {{ ingredients().length }}</span>
                          @if (camp.canEdit && !camp.isFrozen) {
                            <button matButton type="button" (click)="ingredientEditorOpen.set(!ingredientEditorOpen())">
                              <scp-action-icon name="add"/>Lagerzutat</button>
                          }
                        </div>
                      </div>
                      <p class="context-info">Hier erscheinen zentrale Zutaten sowie Zutaten deiner Organisation und dieses Lagers.</p>
                      @if (ingredientEditorOpen()) {
                        <form class="ingredient-editor" (ngSubmit)="createCampIngredient(camp)">
                          <mat-form-field appearance="outline"><mat-label>Name</mat-label>
                            <input matInput name="newIngredientName" [(ngModel)]="newIngredientName" maxlength="200" required>
                          </mat-form-field>
                          <mat-form-field appearance="outline"><mat-label>Herkunft oder Hinweis</mat-label>
                            <textarea matInput name="newIngredientOrigin" [(ngModel)]="newIngredientOrigin" maxlength="2000" rows="2"></textarea>
                          </mat-form-field>
                          <mat-form-field appearance="outline"><mat-label>Varianten</mat-label>
                            <input matInput name="newIngredientVariants" [(ngModel)]="newIngredientVariants"
                              placeholder="Kommagetrennt, z. B. Bio, Vollkorn">
                            <mat-hint>Varianten gelten als 1:1 austauschbar.</mat-hint>
                          </mat-form-field>
                          <div class="section-actions">
                            <button matButton type="button" (click)="closeIngredientEditor()">Abbrechen</button>
                            <button matButton="filled" type="submit" [disabled]="submitting() || !newIngredientName.trim()">
                              <scp-action-icon name="save"/>Zutat anlegen</button>
                          </div>
                        </form>
                      }
                      <mat-form-field appearance="outline" class="catalog-search"><mat-label>Zutaten durchsuchen</mat-label>
                        <input matInput [ngModel]="ingredientSearch()" (ngModelChange)="ingredientSearch.set($event)"
                          name="ingredientSearch" autocomplete="off">
                      </mat-form-field>
                      @if (ingredientsLoading()) {
                        <div class="inline-loading"><mat-spinner diameter="28"/><span>Zutaten werden geladen …</span></div>
                      } @else {
                        <div class="ingredient-grid">
                          @for (ingredient of filteredIngredients(); track ingredient.id) {
                            <article class="ingredient-card">
                              <header><div><span class="scope-badge" [class]="'scope-badge scope-' + ingredient.scope.toLowerCase()">
                                {{ ingredientScopeLabel(ingredient.scope) }}</span><h4>{{ ingredient.name }}</h4></div></header>
                              @if (ingredient.originInformation) { <p class="ingredient-origin">{{ ingredient.originInformation }}</p> }
                              <div class="ingredient-detail"><strong>Einheiten</strong>
                                @if (ingredient.units.length) {
                                  <div class="chip-list">@for (unit of ingredient.units; track unit.unitId) {
                                    <span class="detail-chip" [matTooltip]="measurementDimensionLabel(unit.dimension)">{{ unit.name }} ({{ unit.symbol }})</span>
                                  }</div>
                                } @else { <span class="muted">Keine Einheit hinterlegt</span> }
                              </div>
                              @if (ingredient.variants.length) {
                                <div class="ingredient-detail"><strong>Varianten</strong><div class="chip-list">
                                  @for (variant of ingredient.variants; track variant.id) { <span class="detail-chip">{{ variant.name }}</span> }
                                </div></div>
                              }
                              @if (ingredient.conflicts.length) {
                                <div class="ingredient-detail"><strong>Hinweise</strong><div class="chip-list">
                                  @for (conflict of ingredient.conflicts; track conflict.type + conflict.id) {
                                    <span class="detail-chip conflict-chip">{{ conflictTypeLabel(conflict.type) }}: {{ conflict.name }}</span>
                                  }
                                </div></div>
                              }
                            </article>
                          } @empty { <p class="stage-empty">Keine passenden Zutaten vorhanden.</p> }
                        </div>
                      }
                    </section>
                  }
                }
              </mat-card-content>
              <mat-card-actions>
                @if (!openedCampId()) {
                  <button matButton="filled" type="button" (click)="openCamp(camp)"><scp-action-icon name="camp"/>Lager öffnen</button>
                  @if (camp.canExport) {
                    <button matButton type="button" [disabled]="camp.isFrozen || !camp.startDate || !camp.endDate"
                      (click)="exportCamp(camp)"><scp-action-icon name="download"/>Offlinepaket erstellen</button>
                  }
                }
              </mat-card-actions>
            </mat-card>
          } @empty { <div class="empty-state"><h2>Noch keine Lager</h2><p>Lege das erste Lager für diese Organisation an.</p></div> }
          }
          </div>
        }
        @case ('unavailable') {
          <mat-card class="account-card"><mat-card-header><mat-card-title>Backend nicht erreichbar</mat-card-title></mat-card-header>
            <mat-card-content><p class="error">{{ error() }}</p></mat-card-content>
            <mat-card-actions align="end"><button matButton="filled" (click)="initialize()"><scp-action-icon name="refresh"/>Erneut versuchen</button></mat-card-actions>
          </mat-card>
        }
      }
    </main>
  `
})
export class AppComponent {
  private readonly setupApi = inject(SetupApiService);
  private readonly authenticationApi = inject(AuthenticationApiService);
  private readonly campApi = inject(CampApiService);
  readonly state = signal<ViewState>('loading');
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);
  readonly applicationSection = signal<ApplicationSection>('camps');
  readonly user = signal<AuthenticatedUser | null>(null);
  readonly camps = signal<CampSummary[]>([]);
  readonly tenants = signal<TenantOption[]>([]);
  readonly selectedTenant = signal<TenantOption | null>(null);
  readonly administratorCandidates = signal<CampAdministratorOption[]>([]);
  readonly selectedAdministratorIds = signal<ReadonlySet<string>>(new Set());
  readonly editingCampId = signal<string | null>(null);
  readonly openedCampId = signal<string | null>(null);
  readonly campSection = signal<CampSection>('general');
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
  readonly ingredientEditorOpen = signal(false);
  newIngredientName = '';
  newIngredientOrigin = '';
  newIngredientVariants = '';
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

  constructor() { this.initialize(); }

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
    this.authenticationApi.signOut().subscribe({ next: () => this.clearSession(), error: () => this.clearSession() });
  }

  exportCamp(camp: CampSummary) {
    this.campApi.startOfflineTransfer(camp.id).subscribe(blob => {
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url; anchor.download = `camp-${camp.id}.scoutcamp`; anchor.click(); URL.revokeObjectURL(url);
    });
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
    this.openedCampId.set(camp.id);
    this.campSection.set('general');
    this.loadCampStageFoodFactors(camp.id);
    this.loadStructureConfiguration(camp.id);
    this.error.set(null);
    this.notice.set(null);
  }

  openCampStructure(camp: CampSummary) {
    this.campSection.set('structure');
    if (this.structureCampId() !== camp.id) this.toggleStructure(camp);
  }

  openCampCatering(camp: CampSummary) {
    this.campSection.set('catering');
    this.loadWeightedFoodSummary(camp.id);
    this.loadMealPlan(camp.id);
    this.loadIngredients(camp.id);
  }

  closeCamp() {
    this.openedCampId.set(null);
    this.campSection.set('general');
    this.editingCampId.set(null);
    this.movingStructureNodeId.set(null);
    this.collapsedStructureNodeIds.set(new Set());
    this.creatingStructureParent.set(null); this.planningDetailsVisible.set(false);
    if (this.structureCampId()) this.structureCampId.set(null);
    this.ingredients.set([]); this.ingredientSearch.set('');
    this.closeIngredientEditor();
    this.error.set(null);
    this.notice.set(null);
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
        this.participantEstimates.set(values.map(value => ({ ...value })));
        this.persistedParticipantEstimates = values.map(value => ({ ...value }));
      },
      error: () => this.error.set('Die Teilnehmerschätzung konnte nicht geladen werden.')
    });
  }

  saveEstimates(camp: CampSummary) {
    const nodeId = this.estimateNodeId(); if (!nodeId) return;
    this.submitting.set(true); this.error.set(null);
    this.campApi.updateParticipantEstimates(camp.id, nodeId, this.participantEstimates()).subscribe({
      next: () => {
        this.submitting.set(false);
        this.persistedParticipantEstimates = this.participantEstimates().map(value => ({ ...value }));
        this.notice.set('Die Teilnehmerschätzung wurde gespeichert.');
        this.loadPlanningSummary(camp.id); this.loadWeightedFoodSummary(camp.id);
      },
      error: () => { this.submitting.set(false); this.error.set('Die Teilnehmerschätzung konnte nicht gespeichert werden.'); }
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
        this.campStagesLoading.set(false);
        this.editingCampStageIndexes.set(new Set());
        this.campStageFoodFactors.set(factors.map(value => ({ ...value })));
        this.persistedCampStageFoodFactors = factors.map(value => ({ ...value }));
      },
      error: () => {
        this.campStagesLoading.set(false);
        this.error.set('Die Lager-Verpflegungsfaktoren konnten nicht geladen werden.');
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

  private loadIngredients(campId: string) {
    this.ingredientsLoading.set(true);
    this.campApi.getCampIngredients(campId).subscribe({
      next: ingredients => { this.ingredients.set(ingredients); this.ingredientsLoading.set(false); },
      error: () => { this.ingredients.set([]); this.ingredientsLoading.set(false);
        this.error.set('Der Zutatenkatalog konnte nicht geladen werden.'); }
    });
  }

  createCampIngredient(camp: CampSummary) {
    if (this.submitting() || !this.newIngredientName.trim()) return;
    const variants = this.newIngredientVariants.split(',').map(value => value.trim()).filter(Boolean);
    this.submitting.set(true); this.error.set(null); this.notice.set(null);
    this.campApi.createCampIngredient(camp.id, {
      name: this.newIngredientName.trim(),
      originInformation: this.newIngredientOrigin.trim() || null,
      variants
    }).subscribe({
      next: () => { this.submitting.set(false); this.closeIngredientEditor();
        this.notice.set('Die Lagerzutat wurde angelegt.'); this.loadIngredients(camp.id); },
      error: (response: HttpErrorResponse) => { this.submitting.set(false);
        this.error.set(response.status === 403 ? 'Du darfst keine Lagerzutaten anlegen.' :
          response.status === 409 ? 'Eine Lagerzutat mit diesem Namen existiert bereits.' :
          'Die Lagerzutat konnte nicht angelegt werden.'); }
    });
  }

  closeIngredientEditor() {
    this.ingredientEditorOpen.set(false);
    this.newIngredientName = ''; this.newIngredientOrigin = ''; this.newIngredientVariants = '';
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
          ? 'Unter einem Knoten mit Schätzwerten kann kein Untereintrag angelegt werden.' :
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
      next: () => { this.submitting.set(false); this.structureNodes.update(nodes => nodes.filter(value => value.id !== node.id)); this.notice.set('Der Struktureintrag wurde gelöscht.'); },
      error: (response: HttpErrorResponse) => {
        this.submitting.set(false);
        this.error.set(response.status === 409 && response.error?.code === 'structure_node_has_children'
          ? 'Ein Struktureintrag mit Untereinträgen kann nicht gelöscht werden.'
          : response.status === 409 && response.error?.code === 'structure_node_has_estimates'
          ? 'Ein Struktureintrag mit Schätzwerten kann nicht gelöscht werden.'
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
        this.error.set(code === 'duplicate_structure_name' ? 'Am Ziel existiert bereits ein Eintrag mit diesem Namen.'
          : code === 'maximum_structure_depth_reached' ? 'Der Zweig wäre am Ziel tiefer als erlaubt.'
          : code === 'structure_cycle' ? 'Ein Zweig kann nicht unter einen eigenen Untereintrag verschoben werden.'
          : 'Der Strukturzweig konnte nicht verschoben werden.');
      }
    });
  }

  showSection(section: ApplicationSection) {
    this.applicationSection.set(section);
    this.error.set(null);
    this.notice.set(null);
  }

  selectTenant(tenantId: string) {
    const tenant = this.tenants().find(candidate => candidate.id === tenantId) ?? null;
    this.selectedTenant.set(tenant); this.camps.set([]); this.administratorCandidates.set([]);
    this.openedCampId.set(null); this.campSection.set('general'); this.structureCampId.set(null);
    this.selectedAdministratorIds.set(new Set()); this.error.set(null); this.notice.set(null);
    if (!tenant) return;
    this.loadCamps(tenant.id);
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
    this.user.set(user);
    this.state.set('application');
    this.campApi.listTenants().subscribe({
      next: tenants => {
        this.tenants.set(tenants);
        const tenant = tenants[0] ?? null;
        this.selectedTenant.set(tenant);
        if (!tenant) { this.error.set('Für dieses Konto ist kein aktiver Mandant verfügbar.'); return; }
        this.loadCamps(tenant.id);
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
      next: camps => this.camps.set(camps),
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
