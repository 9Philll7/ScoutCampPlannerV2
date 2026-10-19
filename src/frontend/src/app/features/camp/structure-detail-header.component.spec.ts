import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { StructureDetailHeaderComponent } from './structure-detail-header.component';
import { ParticipantsApiService } from './participants-api.service';

describe('StructureDetailHeaderComponent', () => {
  it('aggregates only the selected subtree and refreshes after participant changes', () => {
    const counts = vi.fn(() => of({ stages: [], counts: [
      { structureNodeId: 'leaf', campStageId: null, category: 'ChildYouth', count: 4 },
      { structureNodeId: 'leaf', campStageId: null, category: null, count: 2 },
      { structureNodeId: null, campStageId: null, category: 'LeaderAdult', count: 1 }
    ] }));
    TestBed.configureTestingModule({ providers: [{ provide: ParticipantsApiService, useValue: { counts } }] });
    const fixture = TestBed.createComponent(StructureDetailHeaderComponent);
    for (const [key, value] of Object.entries({ campId: 'camp', title: 'Gruppe', path: '', description: '', nodeIds: ['leaf', 'leaf'] })) fixture.componentRef.setInput(key, value);
    fixture.detectChanges();
    expect(fixture.componentInstance.actual('ChildYouth')).toBe(4);
    expect(fixture.componentInstance.actual('LeaderAdult')).toBe(0);
    expect(fixture.nativeElement.textContent).toContain('2 Kategorie nicht zugeordnet');
    fixture.componentRef.setInput('wholeCamp', true); fixture.detectChanges();
    expect(fixture.componentInstance.actual('LeaderAdult')).toBe(1);
    counts.mockReturnValue(throwError(() => new Error()));
    fixture.componentRef.setInput('revision', 1); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Nicht verfügbar');
    expect(fixture.componentInstance.counts()).toBeNull();
  });
});
