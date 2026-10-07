import { Component, effect, inject, input, output, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { API_BASE_URL } from '../../core/api-base-url';
import { CookingUnit, MealSlot } from './meal-planning-api.service';

interface Configuration { version: number; demandMode: number; requiresStructureMigration: boolean; }

@Component({ selector: 'scp-participant-planning', standalone: true,
  imports: [FormsModule, MatButtonModule, MatFormFieldModule, MatSelectModule],
  template: `
    <details><summary>Bedarfsbasis</summary>
      <p>Teilnehmer werden im Camp einem Strukturknoten zugeordnet. Kocheinheiten übernehmen sie über Struktur und Verpflegungsfilter. Nur Dummy-/Testdaten.</p>
      @if (error()) { <p role="alert">{{ error() }}</p> }
      @if (configuration(); as data) {
        <mat-form-field><mat-label>Lagerweite Bedarfsbasis</mat-label>
          <mat-select [(ngModel)]="data.demandMode" [disabled]="disabled() || busy()">
            <mat-option [value]="0">Schätzplanung</mat-option><mat-option [value]="1">Reale Teilnehmer verwenden</mat-option>
          </mat-select>
        </mat-form-field>
        @if (data.requiresStructureMigration) { <p role="alert">Alte direkte Zuordnungen müssen vor der Berechnung migriert werden.</p> }
        <button matButton (click)="save()" [disabled]="disabled() || busy()">Modus speichern</button>
        <button matButton (click)="load()" [disabled]="busy()">Änderungen verwerfen / neu laden</button>
      }
    </details>
  `,
  styles: [`article { padding: .8rem; margin: .5rem 0; border: 1px solid #dce5da; border-radius: .6rem; }
    mat-form-field { min-width: 250px; margin: .5rem; } [role=alert] { color: #9e2020; }`]
})
export class ParticipantPlanningComponent {
  readonly campId = input.required<string>(); readonly units = input<CookingUnit[]>([]); readonly meals = input<MealSlot[]>([]);
  readonly disabled = input(false); readonly changed = output<void>();
  readonly configuration = signal<Configuration | null>(null); readonly error = signal(''); readonly busy = signal(false);
  private readonly http = inject(HttpClient); private readonly base = inject(API_BASE_URL);
  private generation = 0;
  constructor() { effect(() => { this.campId(); this.units(); this.load(); }); }
  load() {
    const generation = ++this.generation; const units = this.units(); this.configuration.set(null); this.error.set('');
    this.http.get<Configuration>(`${this.base}/api/camps/${this.campId()}/meal-planning/participants`, { withCredentials: true })
      .subscribe({ next: value => {
        if (generation !== this.generation) return;
        this.configuration.set(value);
      }, error: () => { if (generation === this.generation) this.error.set('Teilnehmerzuordnung nicht verfügbar. Das explizite Teilnehmer-Leserecht ist erforderlich.'); } });
  }
  save() {
    const value = this.configuration(); if (!value || this.disabled() || this.busy()) return;
    const generation = this.generation; this.busy.set(true); this.error.set('');
    this.http.put(`${this.base}/api/camps/${this.campId()}/meal-planning/participants`,
      { expectedVersion: value.version, demandMode: value.demandMode }, { withCredentials: true })
      .subscribe({ next: () => { this.busy.set(false); if (generation !== this.generation) return; this.load(); this.changed.emit(); },
        error: error => { this.busy.set(false); if (generation !== this.generation) return;
          this.error.set(error.status === 409 ? 'Der Stand wurde verändert. Eingaben bleiben erhalten; bitte neu laden und abgleichen.' :
            error.error?.message ?? 'Nicht gespeichert. Zuordnungen und Berechtigungen prüfen.'); } });
  }
}
