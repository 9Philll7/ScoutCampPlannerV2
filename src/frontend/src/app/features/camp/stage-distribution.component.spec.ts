import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { StageDistributionComponent } from './stage-distribution.component';
import { CampApiService } from './camp-api.service';

describe('StageDistributionComponent', () => {
  function setup() {
    const get = vi.fn((camp: string, id: string) => of([{ campStageId: 'stage', stageName: 'Gemischte Stufe', childYouthCount: id === 'a' ? 4 : 2, leaderCount: 1 }]));
    TestBed.configureTestingModule({ imports: [StageDistributionComponent], providers: [{ provide: CampApiService, useValue: { getParticipantEstimates: get } }] });
    const fixture = TestBed.createComponent(StageDistributionComponent);
    fixture.componentRef.setInput('campId', 'camp'); fixture.componentRef.setInput('context', 'Bereich');
    fixture.detectChanges(); return { fixture, component: fixture.componentInstance, get };
  }
  it('uses existing camp totals without new requests and starts collapsed', () => {
    const { fixture, component, get } = setup();
    fixture.componentRef.setInput('stageTotals', [{ campStageId: 'stage', stageName: 'Neue Stufe', childYouthCount: 0, leaderCount: 0 }]); fixture.detectChanges();
    expect(component.expanded()).toBe(false);
    component.toggle({ target: { open: true } } as unknown as Event); fixture.detectChanges();
    expect(get).not.toHaveBeenCalled(); expect(fixture.nativeElement.textContent).toContain('Neue Stufe');
    expect(fixture.nativeElement.textContent).toContain('noch keine Schätzwerte');
  });
  it('sums direct node estimates once and does not change API data', () => {
    const { fixture, component, get } = setup(); fixture.componentRef.setInput('nodeIds', ['a', 'b', 'a']); fixture.detectChanges();
    component.toggle({ target: { open: true } } as unknown as Event); fixture.detectChanges();
    expect(get).toHaveBeenCalledTimes(2); expect(component.total('childYouthCount')).toBe(6); expect(component.total('leaderCount')).toBe(2);
    expect(component.rows()![0].stageName).toBe('Gemischte Stufe');
    fixture.componentRef.setInput('campId', 'another'); fixture.detectChanges();
    expect(component.expanded()).toBe(false); expect(component.rows()).toBeNull();
  });
  it('does not present partial data as complete when a node request fails', () => {
    const { fixture, component, get } = setup(); fixture.componentRef.setInput('nodeIds', ['a', 'b']); fixture.detectChanges();
    get.mockReturnValue(throwError(() => new Error('failed')));
    component.load(); expect(component.rows()).toBeNull(); expect(component.error()).toContain('vollständige');
  });
});
