import { DatePipe } from '@angular/common';
import { Component, computed, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatMenuModule } from '@angular/material/menu';
import { ActionIconComponent } from '../../shared/action-icon.component';
import { ListPaginationComponent } from '../../shared/list-pagination.component';
import { RecipeCatalogEntry } from './recipe-editor-api.service';

@Component({
  selector: 'scp-recipe-catalog', standalone: true,
  imports: [DatePipe, FormsModule, MatButtonModule, MatMenuModule, ActionIconComponent, ListPaginationComponent],
  templateUrl: './recipe-catalog.component.html', styleUrl: './recipe-catalog.component.scss'
})
export class RecipeCatalogComponent {
  readonly entries = input.required<RecipeCatalogEntry[]>();
  readonly disabled = input(false);
  readonly create = output<void>();
  readonly opened = output<RecipeCatalogEntry>();
  readonly removed = output<string>();
  readonly kind = signal('all');
  readonly query = signal('');
  readonly state = signal('');
  readonly scope = signal('');
  readonly sort = signal('name');
  readonly page = signal(0);
  readonly grid = signal(false);
  readonly filtersOpen = signal(false);
  readonly localCount = computed(() => this.entries().filter(value => value.isLocal).length);
  readonly libraryCount = computed(() => this.entries().length - this.localCount());
  readonly filtered = computed(() => {
    const query = this.query().trim().toLocaleLowerCase('de');
    return this.entries().filter(value => (this.kind() === 'all' || (this.kind() === 'local') === value.isLocal)
      && (!query || value.name.toLocaleLowerCase('de').includes(query))
      && (!this.state() || value.status === Number(this.state())) && (!this.scope() || value.scope === Number(this.scope())))
      .sort((a,b) => this.sort() === 'updated' ? b.updatedAtUtc.localeCompare(a.updatedAtUtc) || a.name.localeCompare(b.name,'de')
        : this.sort() === 'state' ? a.status-b.status || a.name.localeCompare(b.name,'de')
        : a.name.localeCompare(b.name,'de') * (this.sort() === 'name-desc' ? -1 : 1));
  });
  readonly visible = computed(() => {
    const page = Math.min(this.page(), Math.max(0, Math.ceil(this.filtered().length/10)-1));
    return this.filtered().slice(page*10,page*10+10);
  });
  chooseKind(kind: string) { this.kind.set(kind); this.page.set(0); }
  resetFilters() { this.state.set(''); this.scope.set(''); this.page.set(0); }
  scopeLabel(scope: number) { return scope === 2 ? 'Lager' : scope === 1 ? 'Organisation' : 'Zentral'; }
  statusLabel(value: RecipeCatalogEntry) { return value.status === 2 ? 'Archiviert' : value.status === 1 ? 'Veröffentlicht' : 'Entwurf'; }
  open(value: RecipeCatalogEntry) { if (value.isLocal) this.opened.emit(value); }
}
