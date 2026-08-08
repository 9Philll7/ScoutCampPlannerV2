import { Component, inject, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatCardModule } from '@angular/material/card';
import { MatToolbarModule } from '@angular/material/toolbar';
import { CampApiService, CampSummary } from './features/camp/camp-api.service';

@Component({
  selector: 'scp-root',
  standalone: true,
  imports: [MatButtonModule, MatCardModule, MatToolbarModule],
  template: `
    <mat-toolbar color="primary">ScoutCampPlanner Architecture Spike</mat-toolbar>
    <main>
      <h1>Lager</h1>
      @if (error()) { <p class="error">{{ error() }}</p> }
      @for (camp of camps(); track camp.id) {
        <mat-card>
          <mat-card-header><mat-card-title>{{ camp.name }}</mat-card-title></mat-card-header>
          <mat-card-content>
            <p>{{ camp.isFrozen ? 'Offlinephase aktiv' : 'Online bearbeitbar' }}</p>
          </mat-card-content>
          <mat-card-actions>
            <button matButton="filled" [disabled]="camp.isFrozen" (click)="exportCamp(camp)">Offlinepaket erstellen</button>
          </mat-card-actions>
        </mat-card>
      } @empty { <p>Keine Lager vorhanden.</p> }
    </main>
  `
})
export class AppComponent {
  private readonly api = inject(CampApiService);
  readonly camps = signal<CampSummary[]>([]);
  readonly error = signal<string | null>(null);

  constructor() {
    this.api.list().subscribe({
      next: camps => this.camps.set(camps),
      error: () => this.error.set('Das Backend ist nicht erreichbar.')
    });
  }

  exportCamp(camp: CampSummary) {
    this.api.startOfflineTransfer(camp.id).subscribe(blob => {
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      anchor.href = url;
      anchor.download = `camp-${camp.id}.scoutcamp`;
      anchor.click();
      URL.revokeObjectURL(url);
    });
  }
}
