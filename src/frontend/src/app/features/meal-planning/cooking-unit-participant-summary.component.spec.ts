import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { API_BASE_URL } from '../../core/api-base-url';
import { ParticipantDocument } from '../camp/participants-api.service';
import { CookingUnitParticipantSummaryComponent, filteredParticipantCounts, unitParticipantTotals } from './cooking-unit-participant-summary.component';

describe('Cooking unit participant summary', () => {
  const nodes = [{ id: 'root', parentId: null, name: 'Lager' }, { id: 'north', parentId: 'root', name: 'Nord' },
    { id: 'fox', parentId: 'north', name: 'Fuchs' }, { id: 'owl', parentId: 'north', name: 'Eule' }, { id: 'south', parentId: 'root', name: 'Süd' }];
  const rows = [{ structureNodeId: 'fox', category: 'ChildYouth', count: 3 },
    { structureNodeId: 'owl', category: 'LeaderAdult', count: 2 }, { structureNodeId: 'owl', category: null, count: 1 },
    { structureNodeId: 'south', category: 'ChildYouth', count: 10 }, { structureNodeId: null, category: null, count: 4 }];
  const document = (id: string, fields: Partial<ParticipantDocument['data']> = {}): ParticipantDocument => ({
    stateToken: 'state', data: { id, displayName: 'Confidential participant name', dietTypeId: null, structureNodeId: 'fox', category: 'ChildYouth',
      absentDays: [], absentMealIds: [], allergenIds: [], intolerances: [], ...fields } });
  function setup(filter = 0) {
    TestBed.configureTestingModule({ imports: [CookingUnitParticipantSummaryComponent], providers: [provideHttpClient(), provideHttpClientTesting(), { provide: API_BASE_URL, useValue: '' }] });
    const fixture = TestBed.createComponent(CookingUnitParticipantSummaryComponent);
    fixture.componentRef.setInput('campId', 'camp'); fixture.componentRef.setInput('nodes', nodes);
    fixture.componentRef.setInput('selectedIds', ['north']); fixture.componentRef.setInput('filter', filter); fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, http: TestBed.inject(HttpTestingController) };
  }
  it('counts ancestor scopes once, excludes unassigned participants and preserves category totals', () => {
    expect(unitParticipantTotals(rows, nodes, ['north', 'fox'])).toEqual({ total: 6, children: 3, adults: 2, unknown: 1 });
    expect(unitParticipantTotals(rows, nodes, [])!.total).toBe(0);
    expect(unitParticipantTotals(rows, nodes, ['root'])!.total).toBe(16);
    expect(unitParticipantTotals(rows, nodes, ['missing'])).toBeNull();
  });
  it('matches the existing dietary filter rule and discards individual profile fields', () => {
    const documents = [document('plain'), document('diet', { dietTypeId: 'diet' }), document('allergen', { allergenIds: ['milk'] }),
      document('intolerance', { intolerances: [{ substanceId: 'lactose', thresholdGramsPerPortion: null, thresholdSource: 'Private source' }] })];
    const special = filteredParticipantCounts([...documents, documents[1]], 1);
    expect(unitParticipantTotals(special, nodes, ['north'])!.total).toBe(3);
    expect(unitParticipantTotals(filteredParticipantCounts(documents, 2), nodes, ['north'])!.total).toBe(1);
    const retained = JSON.stringify(special);
    expect(retained).not.toContain('Confidential'); expect(retained).not.toContain('Private');
    expect(retained).not.toContain('milk'); expect(retained).not.toContain('lactose');
  });
  it('uses anonymous counts for All and updates the unsaved scope without another request', () => {
    const { fixture, component, http } = setup();
    const request = http.expectOne('/api/camps/camp/participant-counts');
    expect(request.request.method).toBe('GET'); expect(request.request.withCredentials).toBe(true);
    request.flush({ counts: rows, stages: [] }); fixture.detectChanges();
    expect(component.totals()!.total).toBe(6);
    fixture.componentRef.setInput('selectedIds', ['fox']); fixture.detectChanges();
    expect(component.totals()!.total).toBe(3);
    expect(fixture.nativeElement.querySelector('.participant-total strong').textContent).toBe('3');
    expect(fixture.nativeElement.textContent).toContain('Anwesenheit');
    http.expectNone('/api/camps/camp/participants'); http.verify();
  });
  it('loads dietary counts through the protected existing endpoint and recomputes on filter changes', () => {
    const { fixture, component, http } = setup(1);
    http.expectOne('/api/camps/camp/participants').flush({ participants: [document('plain'), document('special', { dietTypeId: 'diet' })] }); fixture.detectChanges();
    expect(component.totals()!.total).toBe(1);
    expect(fixture.nativeElement.textContent).not.toContain('Confidential');
    fixture.componentRef.setInput('filter', 2); fixture.detectChanges();
    expect(component.rows()).toBeNull(); expect(component.loading()).toBe(true);
    http.expectOne('/api/camps/camp/participants').flush({ participants: [document('plain'), document('special', { dietTypeId: 'diet' })] }); fixture.detectChanges();
    expect(component.totals()!.total).toBe(1); http.verify();
  });
  it('does not present denied or failed reads as zero participants', () => {
    const { fixture, component, http } = setup(1);
    http.expectOne('/api/camps/camp/participants').flush({}, { status: 404, statusText: 'Not Found' }); fixture.detectChanges();
    expect(component.totals()).toBeNull(); expect(fixture.nativeElement.querySelector('.participant-total')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Teilnehmer-Leserecht'); http.verify();
  });
  it('cancels a previous camp request and never displays its stale counts', () => {
    const { fixture, component, http } = setup();
    const old = http.expectOne('/api/camps/camp/participant-counts');
    fixture.componentRef.setInput('campId', 'other'); fixture.detectChanges();
    expect(old.cancelled).toBe(true);
    http.expectOne('/api/camps/other/participant-counts').flush({ counts: [], stages: [] }); fixture.detectChanges();
    expect(component.totals()!.total).toBe(0); http.verify();
  });
});
