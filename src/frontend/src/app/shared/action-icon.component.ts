import { Component, input } from '@angular/core';

export type ActionIconName = 'add' | 'back' | 'camp' | 'download' | 'edit' | 'info' | 'login' | 'logout' |
  'move' | 'organization' | 'planning' | 'refresh' | 'remove' | 'save' | 'structure' | 'up' | 'down' |
  'expand' | 'collapse' | 'home' | 'recipe' | 'ingredients' | 'people' | 'cooking' | 'warning' | 'question' | 'check' | 'image' | 'power' | 'lock' | 'ban';

@Component({
  selector: 'scp-action-icon',
  standalone: true,
  host: { 'aria-hidden': 'true' },
  template: `
    <svg viewBox="0 0 24 24" focusable="false">
      @switch (name()) {
        @case ('ban') { <circle cx="12" cy="12" r="9"/><path d="m5.6 5.6 12.8 12.8"/> }
        @case ('lock') { <rect x="5" y="10" width="14" height="11" rx="2"/><path d="M8 10V7a4 4 0 0 1 8 0v3m-4 5v2"/> }
        @case ('power') { <path d="M12 3v9M7 5.5a8 8 0 1 0 10 0"/> }
        @case ('image') { <rect x="3" y="4" width="18" height="16" rx="2"/><circle cx="8" cy="9" r="2"/><path d="m3 17 5-5 4 4 4-6 5 7"/> }
        @case ('cooking') { <path d="M4 10h16l-1 10H5L4 10ZM3 7h18M9 7V4h6v3M2 12h2m16 0h2"/> }
        @case ('warning') { <path d="m12 3 10 18H2L12 3ZM12 9v5m0 3h.01"/> }
        @case ('question') { <circle cx="12" cy="12" r="9"/><path d="M9 9a3 3 0 0 1 6 0c0 2-3 2-3 5m0 3h.01"/> }
        @case ('check') { <circle cx="12" cy="12" r="9"/><path d="m7 12 3 3 7-7"/> }
        @case ('people') { <circle cx="9" cy="7" r="3" fill="currentColor" stroke="none"/><path d="M2 20v-3a7 7 0 0 1 14 0v3Z" fill="currentColor" stroke="none"/><path d="M16 4a3 3 0 0 1 0 6m2 3a6 6 0 0 1 4 6v1h-4"/> }
        @case ('home') { <path d="m3 11 9-8 9 8M5 10v10h14V10M10 20v-7h4v7"/> }
        @case ('recipe') { <path d="M4 4h7l1 2 1-2h7v16h-7l-1 1-1-1H4zM12 6v15"/> }
        @case ('ingredients') { <path d="M3 9h18l-2 11H5L3 9ZM7 9l5-6 5 6M9 13v3m6-3v3"/> }
        @case ('add') { <path d="M12 5v14M5 12h14"/> }
        @case ('back') { <path d="m15 18-6-6 6-6"/> }
        @case ('camp') { <path d="m3 20 9-16 9 16M7.5 20 12 12l4.5 8M3 20h18"/> }
        @case ('download') { <path d="M12 3v12m0 0 4-4m-4 4-4-4M5 20h14"/> }
        @case ('edit') { <path d="m4 20 4.5-1 10-10-3.5-3.5-10 10L4 20ZM13.5 7l3.5 3.5"/> }
        @case ('info') { <path d="M12 11v6m0-10h.01M21 12a9 9 0 1 1-18 0 9 9 0 0 1 18 0Z"/> }
        @case ('login') { <path d="M10 17l5-5-5-5m5 5H3m10-8h6a2 2 0 0 1 2 2v12a2 2 0 0 1-2 2h-6"/> }
        @case ('logout') { <path d="m14 17 5-5-5-5m5 5H7m4-8H5a2 2 0 0 0-2 2v12a2 2 0 0 0 2 2h6"/> }
        @case ('move') { <path d="M12 3v18m0-18-3 3m3-3 3 3m-3 15-3-3m3 3 3-3M3 12h18m-18 0 3-3m-3 3 3 3m15-3-3-3m3 3-3 3"/> }
        @case ('organization') { <path d="M4 20h16M6 20V8h12v12M9 8V4h6v4M9 12h2m2 0h2m-6 4h2m2 0h2"/> }
        @case ('planning') { <path d="M5 4h14v16H5zM8 9h8M8 13h3m2 0h3M8 17h3m2 0h3"/> }
        @case ('refresh') { <path d="M20 7v5h-5M4 17v-5h5M18.5 9A7 7 0 0 0 6 7l-2 5m2 3a7 7 0 0 0 12-2l2-5"/> }
        @case ('remove') { <path d="M4 7h16M9 7V4h6v3m3 0-1 13H7L6 7m4 4v5m4-5v5"/> }
        @case ('save') { <path d="M5 4h12l2 2v14H5zM8 4v6h8V4M8 20v-6h8v6"/> }
        @case ('structure') { <path d="M9 4h6v4H9zM4 16h6v4H4zm10 0h6v4h-6zM12 8v4m-5 4v-4h10v4"/> }
        @case ('up') { <path d="m6 15 6-6 6 6"/> }
        @case ('down') { <path d="m6 9 6 6 6-6"/> }
        @case ('expand') { <path d="m9 6 6 6-6 6"/> }
        @case ('collapse') { <path d="m6 9 6 6 6-6"/> }
      }
    </svg>
  `,
  styles: `:host { display: inline-flex; flex: 0 0 auto; width: 1.15rem; height: 1.15rem; }
    svg { width: 100%; height: 100%; fill: none; stroke: currentColor; stroke-width: 1.8; stroke-linecap: round; stroke-linejoin: round; }`
})
export class ActionIconComponent {
  readonly name = input.required<ActionIconName>();
}
