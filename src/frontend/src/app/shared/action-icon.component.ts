import { Component, input } from '@angular/core';

export type ActionIconName = 'add' | 'back' | 'camp' | 'download' | 'edit' | 'login' | 'logout' |
  'move' | 'organization' | 'planning' | 'refresh' | 'remove' | 'save' | 'structure' | 'up' | 'down';

@Component({
  selector: 'scp-action-icon',
  standalone: true,
  host: { 'aria-hidden': 'true' },
  template: `
    <svg viewBox="0 0 24 24" focusable="false">
      @switch (name()) {
        @case ('add') { <path d="M12 5v14M5 12h14"/> }
        @case ('back') { <path d="m15 18-6-6 6-6"/> }
        @case ('camp') { <path d="m3 20 9-16 9 16M7.5 20 12 12l4.5 8M3 20h18"/> }
        @case ('download') { <path d="M12 3v12m0 0 4-4m-4 4-4-4M5 20h14"/> }
        @case ('edit') { <path d="m4 20 4.5-1 10-10-3.5-3.5-10 10L4 20ZM13.5 7l3.5 3.5"/> }
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
      }
    </svg>
  `,
  styles: `:host { display: inline-flex; flex: 0 0 auto; width: 1.15rem; height: 1.15rem; }
    svg { width: 100%; height: 100%; fill: none; stroke: currentColor; stroke-width: 1.8; stroke-linecap: round; stroke-linejoin: round; }`
})
export class ActionIconComponent {
  readonly name = input.required<ActionIconName>();
}
