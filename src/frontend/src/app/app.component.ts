import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { HttpErrorResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatToolbarModule } from '@angular/material/toolbar';
import { SetupApiService } from './features/setup/setup-api.service';

type ViewState = 'loading' | 'setup' | 'ready' | 'unavailable';

@Component({
  selector: 'scp-root',
  standalone: true,
  imports: [
    FormsModule,
    MatButtonModule,
    MatCardModule,
    MatFormFieldModule,
    MatInputModule,
    MatProgressSpinnerModule,
    MatToolbarModule
  ],
  template: `
    <mat-toolbar color="primary">ScoutCampPlanner</mat-toolbar>
    <main>
      @switch (state()) {
        @case ('loading') {
          <section class="centered"><mat-spinner diameter="42"/><p>ScoutCampPlanner wird geladen …</p></section>
        }
        @case ('setup') {
          <mat-card class="setup-card">
            <mat-card-header>
              <mat-card-title>ScoutCampPlanner einrichten</mat-card-title>
              <mat-card-subtitle>Lege die erste Organisation und das Administratorkonto an.</mat-card-subtitle>
            </mat-card-header>
            <mat-card-content>
              <form id="initial-setup" (ngSubmit)="completeSetup()">
                <mat-form-field appearance="outline">
                  <mat-label>Organisation</mat-label>
                  <input matInput name="tenantName" [(ngModel)]="tenantName" maxlength="200" required autocomplete="organization">
                </mat-form-field>
                <mat-form-field appearance="outline">
                  <mat-label>E-Mail-Adresse</mat-label>
                  <input matInput name="email" [(ngModel)]="email" maxlength="320" required type="email" autocomplete="username">
                </mat-form-field>
                <mat-form-field appearance="outline">
                  <mat-label>Passwort</mat-label>
                  <input matInput name="password" [(ngModel)]="password" maxlength="128" required type="password" autocomplete="new-password">
                  <mat-hint>Mindestens 8 Zeichen; eine lange Passphrase wird empfohlen.</mat-hint>
                </mat-form-field>
              </form>
              @if (error()) { <p class="error" role="alert">{{ error() }}</p> }
            </mat-card-content>
            <mat-card-actions align="end">
              <button matButton="filled" form="initial-setup" type="submit" [disabled]="submitting()">
                {{ submitting() ? 'Wird eingerichtet …' : 'Einrichtung abschließen' }}
              </button>
            </mat-card-actions>
          </mat-card>
        }
        @case ('ready') {
          <mat-card class="setup-card">
            <mat-card-header><mat-card-title>Einrichtung abgeschlossen</mat-card-title></mat-card-header>
            <mat-card-content>
              <p>Das Administratorkonto und die Organisation wurden angelegt.</p>
              <p>Als Nächstes ergänzen wir hier die Anmeldung.</p>
            </mat-card-content>
          </mat-card>
        }
        @case ('unavailable') {
          <mat-card class="setup-card">
            <mat-card-header><mat-card-title>Backend nicht erreichbar</mat-card-title></mat-card-header>
            <mat-card-content><p class="error">{{ error() }}</p></mat-card-content>
            <mat-card-actions align="end"><button matButton="filled" (click)="loadStatus()">Erneut versuchen</button></mat-card-actions>
          </mat-card>
        }
      }
    </main>
  `
})
export class AppComponent {
  private readonly setupApi = inject(SetupApiService);
  readonly state = signal<ViewState>('loading');
  readonly submitting = signal(false);
  readonly error = signal<string | null>(null);

  tenantName = '';
  email = '';
  password = '';

  constructor() {
    this.loadStatus();
  }

  loadStatus() {
    this.state.set('loading');
    this.error.set(null);
    this.setupApi.status().subscribe({
      next: status => this.state.set(status.isRequired ? 'setup' : 'ready'),
      error: () => {
        this.error.set('Das ScoutCampPlanner-Backend ist nicht erreichbar.');
        this.state.set('unavailable');
      }
    });
  }

  completeSetup() {
    if (this.submitting()) return;
    this.submitting.set(true);
    this.error.set(null);
    this.setupApi.complete({
      tenantName: this.tenantName,
      email: this.email,
      password: this.password
    }).subscribe({
      next: () => {
        this.password = '';
        this.submitting.set(false);
        this.state.set('ready');
      },
      error: (response: HttpErrorResponse) => {
        this.password = '';
        this.submitting.set(false);
        if (response.status === 409) {
          this.state.set('ready');
          return;
        }
        const errors = response.error?.errors as Record<string, string[]> | undefined;
        this.error.set(errors ? Object.values(errors).flat()[0] ?? 'Die Eingaben sind ungültig.' : 'Die Einrichtung ist fehlgeschlagen.');
      }
    });
  }
}
