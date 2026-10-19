import { TestBed } from '@angular/core/testing';
import { ListPaginationComponent } from './list-pagination.component';
import { vi } from 'vitest';

describe('ListPaginationComponent', () => {
  it('clamps the last page after list shrinkage and uses five rows', () => {
    const fixture = TestBed.createComponent(ListPaginationComponent);
    fixture.componentRef.setInput('length', 12); fixture.componentRef.setInput('pageIndex', 2); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('11–12 von 12');
    expect(fixture.nativeElement.querySelector('[aria-label="Nächste Seite"]').disabled).toBe(true);
    const changed = vi.fn(); fixture.componentInstance.changed.subscribe(changed);
    fixture.componentInstance.go(1);
    expect(changed).toHaveBeenCalledWith({ pageIndex: 1, pageSize: 5 });
    expect(fixture.nativeElement.querySelector('select')).toBeNull();
    fixture.componentRef.setInput('length', 0); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('0–0 von 0');
    expect(fixture.componentInstance.index()).toBe(0);
    fixture.componentRef.setInput('length', 23); fixture.componentRef.setInput('pageIndex', 0); fixture.componentRef.setInput('pageSize', 11); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('1–11 von 23');
    expect(fixture.componentInstance.pages()).toBe(3);
  });
});
