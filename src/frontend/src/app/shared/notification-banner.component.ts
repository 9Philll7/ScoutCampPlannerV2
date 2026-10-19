import { Component, effect, input, output, signal } from '@angular/core';

@Component({
  selector: 'scp-notification-banner',
  template: `
    <div class="banner" [class.error]="kind() === 'error'" [attr.role]="kind() === 'error' ? 'alert' : 'status'"
      (mouseenter)="hovered.set(true)" (mouseleave)="hovered.set(false)"
      (focusin)="focused.set(true)" (focusout)="focused.set(false)">
      <span class="symbol" aria-hidden="true">{{ kind() === 'error' ? '!' : 'i' }}</span>
      <span class="text">{{ message() }}</span>
      <button type="button" aria-label="Meldung schließen" (click)="dismissed.emit()">×</button>
    </div>`,
  styles: `
    :host { display:block; pointer-events:auto; }
    .banner { display:flex; align-items:center; gap:12px; padding:12px 16px; border:1px solid var(--color-primary); border-radius:10px; background:var(--color-surface,#fff); color:var(--color-text); box-shadow:0 6px 24px #20302930; font-size:14px; }
    .banner.error { border-color:var(--color-danger); }
    .symbol { display:grid; place-items:center; flex-shrink:0; width:24px; height:24px; border-radius:50%; background:var(--color-primary); color:white; font-weight:700; }
    .error .symbol { background:var(--color-danger); }
    .text { flex:1; overflow-wrap:anywhere; }
    button { flex-shrink:0; width:36px; height:36px; border:0; border-radius:6px; background:transparent; color:inherit; font:inherit; font-size:24px; cursor:pointer; }
    button:focus-visible { outline:2px solid currentColor; }
    @media(pointer:coarse) { button { width:44px; height:44px; } }
  `
})
export class NotificationBannerComponent {
  readonly message = input.required<string>();
  readonly kind = input<'notice' | 'error'>('notice');
  readonly dismissed = output<void>();
  readonly hovered = signal(false);
  readonly focused = signal(false);
  constructor() {
    effect(cleanup => {
      this.message(); const duration = this.kind() === 'error' ? 12000 : 8000;
      if (this.hovered() || this.focused()) return;
      const timer = setTimeout(() => this.dismissed.emit(), duration);
      cleanup(() => clearTimeout(timer));
    });
  }
}
