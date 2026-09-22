import { Component, inject, input, output, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { MatButtonModule } from '@angular/material/button';
import { firstValueFrom } from 'rxjs';
import { API_BASE_URL } from '../../core/api-base-url';
import { desktopCommand, isDesktop } from '../../core/desktop-runtime';

@Component({
  selector: 'scp-package-transfer', standalone: true, imports: [MatButtonModule],
  template: `
    @if (local) {
      <p>Lokale Lager werden aus einem Lagerpaket übernommen. Der Zugriff erfolgt über dein Windows-Konto.</p>
      <button matButton="filled" [disabled]="busy()" (click)="openLocal()">Lagerpaket öffnen</button>
    } @else {
      <input #file type="file" accept=".scoutcamp" hidden (change)="openReturn($event)">
      <button matButton [disabled]="busy()" (click)="file.click()">Rückpaket importieren</button>
    }
    @if (message()) { <p role="status">{{ message() }}</p> }
  `,
})
export class PackageTransferComponent {
  readonly local = isDesktop();
  readonly campId = input<string>();
  readonly completed = output<void>();
  readonly busy = signal(false);
  readonly message = signal('');
  private readonly http = inject(HttpClient);
  private readonly baseUrl = inject(API_BASE_URL);

  async openLocal() {
    if (this.busy()) return;
    this.busy.set(true); this.message.set('');
    try {
      const bytes = await desktopCommand<number[] | null>('open_camp_package');
      if (bytes === null) return;
      await this.import(new Blob([new Uint8Array(bytes)]), 'initial');
    } catch (error) { this.failed(error); }
    finally { this.busy.set(false); }
  }

  async openReturn(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0]; input.value = '';
    if (!file || this.busy()) return;
    if (!window.confirm('Das Rückpaket ersetzt die Lagerdaten der aktiven Offline-Phase. Jetzt importieren?')) return;
    this.busy.set(true); this.message.set('');
    try { await this.import(file, 'return'); }
    catch (error) { this.failed(error); }
    finally { this.busy.set(false); }
  }

  private async import(file: Blob, direction: string) {
    const target = direction === 'return' ? `?expectedCampId=${encodeURIComponent(this.campId() ?? '')}` : '';
    await firstValueFrom(this.http.post(`${this.baseUrl}/api/packages/import-${direction}${target}`, file,
      { withCredentials: true, headers: { 'Content-Type': 'application/octet-stream' } }));
    this.message.set('Lagerpaket erfolgreich importiert.'); this.completed.emit();
  }
  private failed(error: unknown) {
    this.message.set(error instanceof HttpErrorResponse
      ? error.error?.message || 'Das Paket konnte nicht importiert werden. Bitte Berechtigung und Transferstand prüfen.'
      : error instanceof Error ? error.message : String(error));
  }
}
