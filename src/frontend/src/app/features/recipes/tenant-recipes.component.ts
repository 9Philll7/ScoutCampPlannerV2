import { Component, effect, inject, input, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { API_BASE_URL } from '../../core/api-base-url';
import { RecipeCatalogEntry } from './recipe-editor-api.service';

@Component({
  selector: 'scp-tenant-recipes',
  template: `
    <h2>Organisationsrezepte</h2>
    <p>Veröffentlichte Rezepte deiner Organisation bleiben bei der Rezeptauswahl im Lager verfügbar.</p>
    <p>Die Bearbeitung von Organisationsrezepten ist noch nicht an die Oberfläche angebunden.</p>
    @if (error()) { <p role="alert">{{ error() }}</p> }
    @if (loading()) { <p role="status">Rezepte werden geladen …</p> }
    @else { @for (recipe of recipes(); track recipe.recipeId) {
      <article class="content-card"><h3>{{ recipe.name }}</h3><p>Revision {{ recipe.revisionNumber }}</p></article>
    } @empty { @if (!error()) { <p>Noch keine Organisationsrezepte vorhanden.</p> } } }
  `,
})
export class TenantRecipesComponent {
  readonly tenantId = input.required<string>();
  readonly recipes = signal<RecipeCatalogEntry[]>([]);
  readonly loading = signal(false);
  readonly error = signal('');
  private readonly http = inject(HttpClient);
  private readonly base = inject(API_BASE_URL);
  constructor() {
    effect(onCleanup => {
      this.recipes.set([]); this.loading.set(true); this.error.set('');
      const request = this.http.get<RecipeCatalogEntry[]>(`${this.base}/api/tenants/${this.tenantId()}/recipes`,
        { withCredentials: true }).subscribe({
        next: recipes => { this.recipes.set(recipes); this.loading.set(false); },
        error: () => { this.loading.set(false); this.error.set('Die Organisationsrezepte konnten nicht geladen werden.'); },
      });
      onCleanup(() => request.unsubscribe());
    });
  }
}
