import { afterNextRender, DestroyRef, Directive, ElementRef, inject, output } from '@angular/core';

/** Fits complete table rows above the paginator, retaining a small-screen fallback. */
@Directive({ selector: '[scpFitListPage]' })
export class FitListPageDirective {
  readonly fittedPageSize = output<number>();
  private readonly element = inject(ElementRef<HTMLElement>).nativeElement;
  private readonly destroy = inject(DestroyRef);
  constructor() {
    afterNextRender(() => {
      if (typeof ResizeObserver === 'undefined') return;
      let frame = 0, previous = 5, rowHeight = 52;
      const measure = () => {
        frame = 0;
        const head = this.element.querySelector('thead')?.getBoundingClientRect();
        if (!head?.height) return;
        let size = 5;
        if (window.innerWidth > 1100 && window.innerHeight >= 600) {
          for (const row of this.element.querySelectorAll('tbody tr')) rowHeight = Math.max(rowHeight, row.getBoundingClientRect().height);
          const panel = this.element.closest('.structure-tree');
          const bottom = panel ? panel.getBoundingClientRect().bottom - 24 : window.innerHeight - 32;
          size = Math.max(1, Math.min(20, Math.floor((bottom - head.bottom - 60) / rowHeight)));
        }
        if (size !== previous) { previous = size; this.fittedPageSize.emit(size); }
      };
      const schedule = () => { if (!frame) frame = requestAnimationFrame(measure); };
      const resize = new ResizeObserver(schedule);
      resize.observe(this.element);
      resize.observe(this.element.closest('.structure-tree') ?? document.documentElement);
      const changes = new MutationObserver(schedule);
      changes.observe(this.element, { childList: true, subtree: true });
      window.addEventListener('resize', schedule);
      schedule();
      this.destroy.onDestroy(() => { resize.disconnect(); changes.disconnect(); cancelAnimationFrame(frame); window.removeEventListener('resize', schedule); });
    });
  }
}
