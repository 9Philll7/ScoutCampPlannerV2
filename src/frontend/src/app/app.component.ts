import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatToolbarModule } from '@angular/material/toolbar';
import { AuthenticationApiService, AuthenticatedUser } from './features/authentication/authentication-api.service';
import { CampApiService, CampSummary } from './features/camp/camp-api.service';
import { SetupApiService } from './features/setup/setup-api.service';

type ViewState = 'loading' | 'setup' | 'login' | 'application' | 'unavailable';

@Component({
  selector: 'scp-root',
  standalone: true,
  imports: [FormsModule, MatButtonModule, MatCardModule, MatFormFieldModule, MatInputModule,
    MatProgressSpinnerModule, MatToolbarModule],
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
          @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
          @for (camp of camps(); track camp.id) {
            <mat-card><mat-card-header><mat-card-title>{{ camp.name }}</mat-card-title></mat-card-header>
              <mat-card-content><p>{{ camp.isFrozen ? 'Offlinephase aktiv' : 'Online bearbeitbar' }}</p></mat-card-content>
              <mat-card-actions><button matButton="filled" [disabled]="camp.isFrozen" (click)="exportCamp(camp)">Offlinepaket erstellen</button></mat-card-actions>
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
  readonly user = signal<AuthenticatedUser | null>(null);
  readonly camps = signal<CampSummary[]>([]);
  tenantName = '';
  email = '';
  password = '';

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

  private loadSession() {
    this.authenticationApi.current().subscribe({
      next: user => this.openApplication(user),
      error: response => response.status === 401 ? this.state.set('login') : this.showUnavailable()
    });
  }

  private openApplication(user: AuthenticatedUser) {
    this.user.set(user);
    this.state.set('application');
    this.campApi.list().subscribe({
      next: camps => this.camps.set(camps),
      error: () => this.error.set('Die Lager konnten nicht geladen werden.')
    });
  }

  private clearSession() {
    this.user.set(null); this.camps.set([]); this.error.set(null); this.state.set('login');
  }

  private showUnavailable() {
    this.error.set('Das ScoutCampPlanner-Backend ist nicht erreichbar.'); this.state.set('unavailable');
  }
}
