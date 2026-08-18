import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatCheckboxModule } from '@angular/material/checkbox';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatSelectModule } from '@angular/material/select';
import { MatToolbarModule } from '@angular/material/toolbar';
import { AuthenticationApiService, AuthenticatedUser } from './features/authentication/authentication-api.service';
import { CampAdministratorOption, CampApiService, CampSummary, StructureConfiguration, StructureNodeSummary, TenantOption } from './features/camp/camp-api.service';
import { SetupApiService } from './features/setup/setup-api.service';

type ViewState = 'loading' | 'setup' | 'login' | 'application' | 'unavailable';

@Component({
  selector: 'scp-root',
  standalone: true,
  imports: [FormsModule, MatButtonModule, MatCardModule, MatCheckboxModule, MatFormFieldModule, MatInputModule,
    MatProgressSpinnerModule, MatSelectModule, MatToolbarModule],
  template: `
    <mat-toolbar color="primary">
      <span>ScoutCampPlanner</span>
      <span class="toolbar-spacer"></span>
      @if (user(); as currentUser) {
        <span class="user-email">{{ currentUser.email }}</span>
        <button matButton (click)="signOut()">Abmelden</button>
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
            <mat-card-actions align="end"><button matButton="filled" form="initial-setup" type="submit" [disabled]="submitting()">Einrichtung abschließen</button></mat-card-actions>
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
            <mat-card-actions align="end"><button matButton="filled" form="login" type="submit" [disabled]="submitting()">Anmelden</button></mat-card-actions>
          </mat-card>
        }
        @case ('application') {
          <h1>Lager</h1>
          @if (tenants().length > 1) {
            <mat-form-field appearance="outline"><mat-label>Organisation</mat-label>
              <mat-select [ngModel]="selectedTenant()?.id" (ngModelChange)="selectTenant($event)">
                @for (tenant of tenants(); track tenant.id) { <mat-option [value]="tenant.id">{{ tenant.name }}</mat-option> }
              </mat-select>
            </mat-form-field>
          }
          @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
          @if (notice()) { <p role="status">{{ notice() }}</p> }
          @if (selectedTenant(); as tenant) {
            <mat-card>
              <mat-card-header><mat-card-title>Stufenvorlage</mat-card-title>
                <mat-card-subtitle>Für zukünftige Lager von {{ tenant.name }}</mat-card-subtitle>
              </mat-card-header>
              <mat-card-content>
                <form id="stage-template" (ngSubmit)="saveStageTemplate(tenant.id)">
                  <mat-form-field appearance="outline"><mat-label>Stufen (eine pro Zeile)</mat-label>
                    <textarea matInput name="stageTemplate" [(ngModel)]="stageTemplateInput" rows="6" required></textarea>
                  </mat-form-field>
                </form>
              </mat-card-content>
              <mat-card-actions align="end"><button matButton="filled" form="stage-template" type="submit"
                [disabled]="submitting()">Vorlage speichern</button></mat-card-actions>
            </mat-card>
            <mat-card>
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
                    <input matInput name="campStartDate" [(ngModel)]="campStartDate" type="date" required>
                  </mat-form-field>
                  <mat-form-field appearance="outline"><mat-label>Enddatum</mat-label>
                    <input matInput name="campEndDate" [(ngModel)]="campEndDate" type="date" required>
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
                <button matButton="filled" form="create-camp" type="submit" [disabled]="submitting()">Lager anlegen</button>
              </mat-card-actions>
            </mat-card>
          }
          @for (camp of camps(); track camp.id) {
            <mat-card><mat-card-header><mat-card-title>{{ camp.name }}</mat-card-title></mat-card-header>
              <mat-card-content>
                @if (editingCampId() === camp.id) {
                  <form [id]="'edit-camp-' + camp.id" (ngSubmit)="saveCamp(camp)">
                    <mat-form-field appearance="outline"><mat-label>Name des Lagers</mat-label>
                      <input matInput name="editCampName" [(ngModel)]="editCampName" maxlength="200" required>
                    </mat-form-field>
                    <mat-form-field appearance="outline"><mat-label>Startdatum</mat-label>
                      <input matInput name="editCampStartDate" [(ngModel)]="editCampStartDate" type="date" required>
                    </mat-form-field>
                    <mat-form-field appearance="outline"><mat-label>Enddatum</mat-label>
                      <input matInput name="editCampEndDate" [(ngModel)]="editCampEndDate" type="date" required>
                    </mat-form-field>
                  </form>
                } @else {
                  <p>{{ camp.startDate && camp.endDate ? camp.startDate + ' bis ' + camp.endDate : 'Legacy-Lager ohne Zeitraum' }}</p>
                  <p>{{ camp.isFrozen ? 'Offlinephase aktiv' : 'Online bearbeitbar' }}</p>
                  @if (structureCampId() === camp.id) {
                    <h3>Lagerstufen</h3>
                    @if (camp.canEdit) {
                      <form [id]="'camp-stages-' + camp.id" (ngSubmit)="saveCampStages(camp)">
                        <mat-form-field appearance="outline"><mat-label>Stufen dieses Lagers</mat-label>
                          <textarea matInput name="campStages" [(ngModel)]="campStageInput" rows="4" required></textarea>
                        </mat-form-field>
                        <button matButton type="submit" [disabled]="submitting()">Lagerstufen speichern</button>
                      </form>
                    } @else { <p>{{ campStageInput }}</p> }
                    <h3>{{ structureConfiguration()?.mode === 'Fixed' ? 'Fixierte Lagerstruktur' : 'Freie Lagerstruktur' }}</h3>
                    @if (camp.canEdit) {
                      <form [id]="'structure-configuration-' + camp.id" (ngSubmit)="saveStructureConfiguration(camp)">
                        <mat-form-field appearance="outline"><mat-label>Ebenen (eine pro Zeile; leer = frei)</mat-label>
                          <textarea matInput name="structureLevels" [(ngModel)]="structureLevelInput" rows="3"></textarea>
                        </mat-form-field>
                        <button matButton type="submit" [disabled]="submitting()">Tiefe übernehmen</button>
                      </form>
                    }
                    @for (row of structureRows(); track row.node.id) {
                      <p [style.margin-left.px]="row.depth * 24">
                        {{ row.node.name }}
                        @if (camp.canEdit) {
                          <mat-form-field appearance="outline">
                            <mat-label>Verschieben nach</mat-label>
                            <mat-select [name]="'moveTarget-' + row.node.id" [(ngModel)]="moveTargetParentIds[row.node.id]">
                              <mat-option value="">Oberste Ebene</mat-option>
                              @for (target of moveTargets(row.node); track target.id) {
                                <mat-option [value]="target.id">{{ target.name }}</mat-option>
                              }
                            </mat-select>
                          </mat-form-field>
                          <button matButton type="button" (click)="moveStructureNode(camp, row.node)"
                            [disabled]="submitting() || camp.isFrozen">Verschieben</button>
                          <button matButton type="button" (click)="deleteStructureNode(camp, row.node)"
                            [disabled]="submitting() || camp.isFrozen">Löschen</button>
                        }
                      </p>
                    } @empty { <p>Noch keine Struktureinträge vorhanden.</p> }
                    @if (camp.canEdit) {
                      <form [id]="'create-structure-node-' + camp.id" (ngSubmit)="createStructureNode(camp)">
                        <mat-form-field appearance="outline"><mat-label>Übergeordneter Eintrag</mat-label>
                          <mat-select name="structureParent" [(ngModel)]="newStructureParentId">
                            <mat-option value="">Oberste Ebene</mat-option>
                            @for (row of structureRows(); track row.node.id) {
                              <mat-option [value]="row.node.id">{{ row.node.name }}</mat-option>
                            }
                          </mat-select>
                        </mat-form-field>
                        <mat-form-field appearance="outline"><mat-label>Name</mat-label>
                          <input matInput name="structureName" [(ngModel)]="newStructureNodeName" maxlength="200" required>
                        </mat-form-field>
                      </form>
                    }
                  }
                }
              </mat-card-content>
              <mat-card-actions>
                @if (editingCampId() === camp.id) {
                  <button matButton type="button" (click)="cancelCampEdit()">Abbrechen</button>
                  <button matButton="filled" type="submit" [attr.form]="'edit-camp-' + camp.id" [disabled]="submitting()">Speichern</button>
                } @else {
                  <button matButton (click)="toggleStructure(camp)">
                    {{ structureCampId() === camp.id ? 'Struktur schließen' : 'Struktur' }}
                  </button>
                  @if (structureCampId() === camp.id && camp.canEdit) {
                    <button matButton="filled" type="submit" [attr.form]="'create-structure-node-' + camp.id"
                      [disabled]="submitting()">Eintrag anlegen</button>
                  }
                  @if (camp.canEdit) { <button matButton (click)="editCamp(camp)" [disabled]="camp.isFrozen">Bearbeiten</button> }
                  @if (camp.canExport) {
                    <button matButton="filled" [disabled]="camp.isFrozen || !camp.startDate || !camp.endDate"
                      (click)="exportCamp(camp)">Offlinepaket erstellen</button>
                  }
                }
              </mat-card-actions>
            </mat-card>
          } @empty { <p>Noch keine Lager vorhanden.</p> }
        }
        @case ('unavailable') {
          <mat-card class="account-card"><mat-card-header><mat-card-title>Backend nicht erreichbar</mat-card-title></mat-card-header>
            <mat-card-content><p class="error">{{ error() }}</p></mat-card-content>
            <mat-card-actions align="end"><button matButton="filled" (click)="initialize()">Erneut versuchen</button></mat-card-actions>
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
  readonly user = signal<AuthenticatedUser | null>(null);
  readonly camps = signal<CampSummary[]>([]);
  readonly tenants = signal<TenantOption[]>([]);
  readonly selectedTenant = signal<TenantOption | null>(null);
  readonly administratorCandidates = signal<CampAdministratorOption[]>([]);
  readonly selectedAdministratorIds = signal<ReadonlySet<string>>(new Set());
  readonly editingCampId = signal<string | null>(null);
  readonly structureCampId = signal<string | null>(null);
  readonly structureNodes = signal<StructureNodeSummary[]>([]);
  readonly structureConfiguration = signal<StructureConfiguration | null>(null);
  tenantName = '';
  email = '';
  password = '';
  campName = '';
  campStartDate = '';
  campEndDate = '';
  editCampName = '';
  editCampStartDate = '';
  editCampEndDate = '';
  newStructureParentId = '';
  newStructureNodeName = '';
  structureLevelInput = '';
  stageTemplateInput = '';
  campStageInput = '';
  moveTargetParentIds: Record<string, string> = {};

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
      startDate: this.campStartDate,
      endDate: this.campEndDate,
      initialAdministratorMembershipIds: selectedAdministratorIds
    }).subscribe({
      next: camp => {
        this.submitting.set(false); this.campName = ''; this.campStartDate = ''; this.campEndDate = '';
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
    this.editCampStartDate = camp.startDate ?? ''; this.editCampEndDate = camp.endDate ?? '';
    this.error.set(null); this.notice.set(null);
  }

  cancelCampEdit() {
    this.editingCampId.set(null); this.editCampName = '';
    this.editCampStartDate = ''; this.editCampEndDate = '';
  }

  saveCamp(camp: CampSummary) {
    if (this.submitting()) return;
    this.submitting.set(true); this.error.set(null); this.notice.set(null);
    this.campApi.update(camp.id, {
      name: this.editCampName, startDate: this.editCampStartDate, endDate: this.editCampEndDate
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
      this.structureCampId.set(null); this.structureNodes.set([]); this.structureConfiguration.set(null); return;
    }
    this.structureCampId.set(camp.id); this.structureNodes.set([]);
    this.newStructureParentId = ''; this.newStructureNodeName = '';
    this.campApi.listStructure(camp.id).subscribe({
      next: nodes => { this.structureNodes.set(nodes); this.moveTargetParentIds = Object.fromEntries(nodes.map(node => [node.id, node.parentId ?? ''])); },
      error: () => this.error.set('Die Lagerstruktur konnte nicht geladen werden.')
    });
    this.campApi.getStructureConfiguration(camp.id).subscribe({ next: configuration => {
      this.structureConfiguration.set(configuration); this.structureLevelInput = configuration.levelNames.join('\n');
    }});
    this.campApi.getCampStages(camp.id).subscribe({ next: stages => this.campStageInput = stages.map(value => value.name).join('\n') });
  }

  saveCampStages(camp: CampSummary) {
    const names = this.campStageInput.split(/\r?\n/).map(value => value.trim()).filter(Boolean);
    this.submitting.set(true); this.error.set(null);
    this.campApi.updateCampStages(camp.id, names).subscribe({
      next: () => { this.submitting.set(false); this.notice.set('Die Lagerstufen wurden gespeichert.'); },
      error: () => { this.submitting.set(false); this.error.set('Die Lagerstufen konnten nicht gespeichert werden.'); }
    });
  }

  saveStructureConfiguration(camp: CampSummary) {
    const levels = this.structureLevelInput.split(/\r?\n/).map(value => value.trim()).filter(Boolean);
    this.submitting.set(true); this.error.set(null);
    this.campApi.updateStructureConfiguration(camp.id, levels).subscribe({
      next: () => { this.submitting.set(false); this.structureConfiguration.set({ mode: levels.length ? 'Fixed' : 'Free', levelNames: levels }); this.notice.set('Die Strukturtiefe wurde aktualisiert.'); },
      error: () => { this.submitting.set(false); this.error.set('Die Strukturtiefe ist ungültig oder für den bestehenden Baum zu kurz.'); }
    });
  }

  structureRows(): { node: StructureNodeSummary; depth: number }[] {
    const nodes = this.structureNodes();
    const result: { node: StructureNodeSummary; depth: number }[] = [];
    const append = (parentId: string | null, depth: number) => {
      nodes.filter(node => node.parentId === parentId)
        .sort((left, right) => left.name.localeCompare(right.name, 'de'))
        .forEach(node => { result.push({ node, depth }); append(node.id, depth + 1); });
    };
    append(null, 0);
    return result;
  }

  createStructureNode(camp: CampSummary) {
    if (this.submitting()) return;
    this.submitting.set(true); this.error.set(null); this.notice.set(null);
    this.campApi.createStructureNode(
      camp.id, this.newStructureParentId || null, this.newStructureNodeName).subscribe({
      next: node => {
        this.submitting.set(false); this.structureNodes.update(nodes => [...nodes, node]);
        this.newStructureNodeName = ''; this.notice.set('Der Struktureintrag wurde angelegt.');
      },
      error: (response: HttpErrorResponse) => {
        this.submitting.set(false);
        const errors = response.error?.errors as Record<string, string[]> | undefined;
        this.error.set(response.status === 409 ? 'Während der Offlinephase kann die Struktur nicht geändert werden.' :
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

  moveStructureNode(camp: CampSummary, node: StructureNodeSummary) {
    if (this.submitting()) return;
    const parentId = this.moveTargetParentIds[node.id] || null;
    this.submitting.set(true); this.error.set(null); this.notice.set(null);
    this.campApi.moveStructureNode(camp.id, node.id, parentId).subscribe({
      next: () => { this.submitting.set(false); this.structureNodes.update(nodes => nodes.map(value => value.id === node.id ? { ...value, parentId } : value)); this.notice.set('Der Strukturzweig wurde verschoben.'); },
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

  selectTenant(tenantId: string) {
    const tenant = this.tenants().find(candidate => candidate.id === tenantId) ?? null;
    this.selectedTenant.set(tenant); this.camps.set([]); this.administratorCandidates.set([]);
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
    this.campApi.getStageTemplate(tenantId).subscribe({
      next: entries => this.stageTemplateInput = entries.map(value => value.name).join('\n'),
      error: () => this.error.set('Die Stufenvorlage konnte nicht geladen werden.')
    });
  }

  saveStageTemplate(tenantId: string) {
    const names = this.stageTemplateInput.split(/\r?\n/).map(value => value.trim()).filter(Boolean);
    this.submitting.set(true); this.error.set(null); this.notice.set(null);
    this.campApi.updateStageTemplate(tenantId, names).subscribe({
      next: () => { this.submitting.set(false); this.notice.set('Die Stufenvorlage wurde gespeichert.'); },
      error: (response: HttpErrorResponse) => { this.submitting.set(false); this.error.set(response.status === 403
        ? 'Du darfst die mandantenweite Stufenvorlage nicht ändern.'
        : 'Die Stufenvorlage enthält ungültige oder doppelte Namen.'); }
    });
  }

  private clearSession() {
    this.user.set(null); this.camps.set([]); this.tenants.set([]); this.selectedTenant.set(null);
    this.administratorCandidates.set([]); this.selectedAdministratorIds.set(new Set());
    this.editingCampId.set(null);
    this.structureCampId.set(null); this.structureNodes.set([]);
    this.error.set(null); this.notice.set(null); this.state.set('login');
  }

  private showUnavailable() {
    this.error.set('Das ScoutCampPlanner-Backend ist nicht erreichbar.'); this.state.set('unavailable');
  }
}
