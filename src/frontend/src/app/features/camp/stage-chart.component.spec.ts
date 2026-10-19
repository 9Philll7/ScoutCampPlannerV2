import { TestBed } from '@angular/core/testing';
import { StageChartComponent } from './stage-chart.component';

describe('StageChartComponent', () => {
  it('uses a shared count scale for categories and includes unassigned people', () => {
    const fixture = TestBed.createComponent(StageChartComponent);
    fixture.componentRef.setInput('title', 'Erfasst');
    fixture.componentRef.setInput('values', [
      { id: 'a', name: 'Stufe A', youth: 6, adults: 2 },
      { id: null, name: 'Nicht zugeordnet', youth: 2, adults: 1, unknown: 3 }
    ]);
    fixture.detectChanges();
    expect(fixture.componentInstance.total()).toBe(14);
    expect(fixture.componentInstance.maximum()).toBe(8);
    expect(fixture.nativeElement.querySelector('.segment').style.width).toBe('75%');
    expect(fixture.nativeElement.querySelectorAll('[role="img"]').length).toBe(3);
    expect(fixture.nativeElement.textContent).toContain('Kategorie offen');
    expect(fixture.componentInstance.description('youth')).toContain('Nicht zugeordnet: 2');
  });
  it('renders empty values without invalid widths or invented people', () => {
    const fixture = TestBed.createComponent(StageChartComponent);
    fixture.componentRef.setInput('title', 'Geschätzt');
    fixture.componentRef.setInput('values', []); fixture.detectChanges();
    expect(fixture.componentInstance.maximum()).toBe(1);
    expect(fixture.nativeElement.querySelectorAll('.segment').length).toBe(0);
    expect(fixture.nativeElement.textContent).toContain('noch keine Teilnehmerzahlen');
  });
});
