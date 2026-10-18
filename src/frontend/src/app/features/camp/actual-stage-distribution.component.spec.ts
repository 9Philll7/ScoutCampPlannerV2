import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { ActualStageDistributionComponent } from './actual-stage-distribution.component';
import { ParticipantsApiService } from './participants-api.service';

describe('ActualStageDistributionComponent', () => {
  it('counts each organizational bucket once and keeps unknown assignments visible', () => {
    TestBed.configureTestingModule({ providers: [{ provide: ParticipantsApiService, useValue: { counts: () => of({
      stages: [{ id: 'stage', name: 'Stufe' }], counts: [
        { structureNodeId: 'leaf', campStageId: 'stage', category: 'ChildYouth', count: 3 },
        { structureNodeId: 'leaf', campStageId: null, category: null, count: 2 },
        { structureNodeId: null, campStageId: 'stage', category: 'LeaderAdult', count: 1 }
      ] }) } }] });
    const fixture = TestBed.createComponent(ActualStageDistributionComponent);
    fixture.componentRef.setInput('campId', 'camp'); fixture.componentRef.setInput('nodeIds', ['leaf', 'leaf']);
    fixture.detectChanges();
    expect(fixture.componentInstance.total()).toBe(5);
    expect(fixture.componentInstance.count(null, null)).toBe(2);
    fixture.componentRef.setInput('wholeCamp', true); fixture.detectChanges();
    expect(fixture.componentInstance.total()).toBe(6);
    expect(fixture.componentInstance.unassignedStructure()).toBe(1);
    expect(fixture.nativeElement.textContent).toContain('Stufe nicht zugeordnet');
  });
  it('does not display failed requests as zero counts', () => {
    TestBed.configureTestingModule({ providers: [{ provide: ParticipantsApiService, useValue: { counts: () => throwError(() => new Error()) } }] });
    const fixture = TestBed.createComponent(ActualStageDistributionComponent);
    fixture.componentRef.setInput('campId', 'camp'); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[role="alert"]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('h3')).toBeNull();
  });
});
