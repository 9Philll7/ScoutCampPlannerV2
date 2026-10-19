import { Component, computed, input, output } from '@angular/core';

export const LIST_PAGE_SIZE = 5;
export interface ListPage { pageIndex: number; pageSize: number; }

@Component({
  selector: 'scp-list-pagination',
  template: `
    <nav [attr.aria-label]="label()" class="pagination">
      <span class="range" aria-live="polite">{{ length() ? index() * pageSize() + 1 : 0 }}–{{ end() }} von {{ length() }}</span>
      <div class="pages">
        <button type="button" aria-label="Vorherige Seite" [disabled]="index() === 0" (click)="go(index() - 1)">‹</button>
        <span>Seite {{ index() + 1 }} / {{ pages() }}</span>
        <button type="button" aria-label="Nächste Seite" [disabled]="index() + 1 >= pages()" (click)="go(index() + 1)">›</button>
      </div>
    </nav>
  `,
  styles: `
    :host { display:block; }
    .pagination { display:flex; align-items:center; flex-wrap:wrap; gap:8px 16px; padding-top:12px; font-size:12px; color:var(--color-text-muted); }
    .range { margin-right:auto; font-variant-numeric:tabular-nums; }
    label,.pages { display:flex; align-items:center; gap:8px; }
    button,select { font:inherit; min-height:32px; border:1px solid var(--color-border); border-radius:6px; background:var(--color-surface); color:var(--color-text); }
    select { padding:4px 8px; } button { width:34px; cursor:pointer; font-size:20px; } button:disabled { opacity:.4; cursor:default; }
    button:focus-visible,select:focus-visible { outline:2px solid var(--color-primary); outline-offset:2px; }
    @media(pointer:coarse) { button,select { min-height:44px; } button { width:44px; } }
  `
})
export class ListPaginationComponent {
  readonly length = input.required<number>();
  readonly pageIndex = input(0);
  readonly pageSize = input(LIST_PAGE_SIZE);
  readonly label = input('Listenseiten');
  readonly changed = output<ListPage>();
  readonly pages = computed(() => Math.max(1, Math.ceil(this.length() / this.pageSize())));
  readonly index = computed(() => Math.min(Math.max(0, this.pageIndex()), this.pages() - 1));
  readonly end = computed(() => Math.min(this.length(), (this.index() + 1) * this.pageSize()));
  go(index: number) { this.changed.emit({ pageIndex: Math.max(0, Math.min(index, this.pages() - 1)), pageSize: this.pageSize() }); }
}
