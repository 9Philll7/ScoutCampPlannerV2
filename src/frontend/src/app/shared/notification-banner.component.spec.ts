import { TestBed } from '@angular/core/testing';
import { afterEach, vi } from 'vitest';
import { NotificationBannerComponent } from './notification-banner.component';

describe('NotificationBannerComponent', () => {
  afterEach(() => { TestBed.resetTestingModule(); vi.useRealTimers(); });
  it('dismisses notices after eight seconds and restarts the timer for new text', () => {
    vi.useFakeTimers();
    const fixture = TestBed.createComponent(NotificationBannerComponent);
    fixture.componentRef.setInput('message', 'Gespeichert'); fixture.detectChanges();
    const dismissed = vi.fn(); fixture.componentInstance.dismissed.subscribe(dismissed);
    vi.advanceTimersByTime(7000); expect(dismissed).not.toHaveBeenCalled();
    fixture.componentRef.setInput('message', 'Neu geladen'); fixture.detectChanges();
    vi.advanceTimersByTime(1000); expect(dismissed).not.toHaveBeenCalled();
    vi.advanceTimersByTime(7000); expect(dismissed).toHaveBeenCalledTimes(1);
  });
  it('keeps errors readable while hovered or focused and allows manual dismissal', () => {
    vi.useFakeTimers();
    const fixture = TestBed.createComponent(NotificationBannerComponent);
    fixture.componentRef.setInput('message', 'Speichern fehlgeschlagen'); fixture.componentRef.setInput('kind', 'error'); fixture.detectChanges();
    const dismissed = vi.fn(); fixture.componentInstance.dismissed.subscribe(dismissed);
    fixture.componentInstance.focused.set(true); fixture.detectChanges();
    vi.advanceTimersByTime(20000); expect(dismissed).not.toHaveBeenCalled();
    fixture.componentInstance.focused.set(false); fixture.detectChanges();
    vi.advanceTimersByTime(11999); expect(dismissed).not.toHaveBeenCalled();
    fixture.nativeElement.querySelector('button').click(); expect(dismissed).toHaveBeenCalledTimes(1);
    expect(fixture.nativeElement.querySelector('[role="alert"]')).not.toBeNull();
  });
});
