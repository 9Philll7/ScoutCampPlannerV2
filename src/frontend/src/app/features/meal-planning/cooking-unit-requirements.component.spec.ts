import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { API_BASE_URL } from '../../core/api-base-url';
import { ParticipantDocument } from '../camp/participants-api.service';
import { CookingUnitRequirementsComponent, scopedRequirementCounts, unitRequirementCounts } from './cooking-unit-requirements.component';

describe('Cooking unit requirement information', () => {
  const nodes = [{ id: 'root', parentId: null, name: 'Lager' }, { id: 'north', parentId: 'root', name: 'Nord' },
    { id: 'leaf', parentId: 'north', name: 'Fuchs' }, { id: 'south', parentId: 'root', name: 'Süd' }];
  const participant = (id: string, fields: Partial<ParticipantDocument['data']> = {}): ParticipantDocument => ({ stateToken: 'private-token',
    data: { id, displayName: 'Private Person', dietTypeId: null, structureNodeId: 'leaf', absentDays: ['2027-07-02'], absentMealIds: ['private-meal'], allergenIds: [], intolerances: [], ...fields } });
  const documents = [participant('plain'), participant('milk', { allergenIds: ['milk'] }), participant('milk2', { allergenIds: ['MILK'] }),
    participant('diet', { dietTypeId: 'vegetarian', structureNodeId: 'north' }), participant('outside', { structureNodeId: 'south' }), participant('unassigned', { structureNodeId: null })];
  const catalog = { dietTypes: [{ id: 'vegetarian', name: 'Vegetarisch', code: 'VEGETARIAN' }], allergens: [{ id: 'milk', name: 'Milch', code: 'G' }], substances: [] };
  function setup() {
    TestBed.configureTestingModule({ imports: [CookingUnitRequirementsComponent], providers: [provideHttpClient(), provideHttpClientTesting(), { provide: API_BASE_URL, useValue: '' }] });
    const fixture = TestBed.createComponent(CookingUnitRequirementsComponent);
    fixture.componentRef.setInput('campId', 'camp'); fixture.componentRef.setInput('nodes', nodes); fixture.componentRef.setInput('selectedIds', ['north', 'leaf']); fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, http: TestBed.inject(HttpTestingController) };
  }
  it('groups requirements canonically, counts ancestor scopes once and retains no individual profiles', () => {
    const rows = unitRequirementCounts([...documents, documents[0]]);
    const groups = scopedRequirementCounts(rows, nodes, ['north', 'leaf'], 0)!;
    expect(groups.map(group => group.count).sort()).toEqual([1, 1, 2]);
    expect(scopedRequirementCounts(rows, nodes, ['north'], 1)!.reduce((sum, group) => sum + group.count, 0)).toBe(3);
    expect(scopedRequirementCounts(rows, nodes, ['north'], 2)!.reduce((sum, group) => sum + group.count, 0)).toBe(1);
    expect(JSON.stringify(rows)).not.toMatch(/Private Person|private-token|private-meal|absentDays/);
    expect(scopedRequirementCounts(rows, nodes, ['unknown'], 0)).toBeNull();
  });
  it('preserves distinct substance thresholds and discards private sources', () => {
    const values = [1, 2, null].map((thresholdGramsPerPortion, index) => participant('person-' + index, {
      intolerances: [{ substanceId: 'lactose', thresholdGramsPerPortion, thresholdSource: 'Private health note' }] }));
    const rows = unitRequirementCounts(values);
    expect(scopedRequirementCounts(rows, nodes, ['north'], 0)).toHaveLength(3);
    expect(JSON.stringify(rows)).not.toContain('Private health note');
  });
  it('shows human labels and participant totals from the protected endpoint and reacts to draft filters', () => {
    const { fixture, component, http } = setup();
    const request = http.expectOne('/api/camps/camp/participants'); expect(request.request.withCredentials).toBe(true);
    request.flush({ participants: documents, catalog }); fixture.detectChanges();
    expect(component.total()).toBe(4); expect(fixture.nativeElement.textContent).toContain('Milch (G)');
    expect(fixture.nativeElement.textContent).toContain('Vegetarisch'); expect(fixture.nativeElement.textContent).not.toContain('VEGETARIAN');
    expect(fixture.nativeElement.textContent).not.toContain('Private Person');
    fixture.componentRef.setInput('filter', 1); fixture.detectChanges(); expect(component.total()).toBe(3);
    fixture.componentRef.setInput('selectedIds', ['leaf']); fixture.detectChanges(); expect(component.total()).toBe(2);
    http.expectNone('/api/camps/camp/participants'); http.verify();
  });
  it('does not present denied health access as an empty group list', () => {
    const { fixture, component, http } = setup();
    http.expectOne('/api/camps/camp/participants').flush({}, { status: 404, statusText: 'Not Found' }); fixture.detectChanges();
    expect(component.groups()).toBeNull(); expect(fixture.nativeElement.querySelector('table')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Teilnehmer-Leserecht'); http.verify();
  });
  it('cancels old camp reads and clears their information', () => {
    const { fixture, component, http } = setup(); const old = http.expectOne('/api/camps/camp/participants');
    fixture.componentRef.setInput('campId', 'other'); fixture.detectChanges(); expect(old.cancelled).toBe(true);
    http.expectOne('/api/camps/other/participants').flush({ participants: [], catalog }); fixture.detectChanges();
    expect(component.groups()).toEqual([]); expect(fixture.nativeElement.querySelector('table')).toBeNull(); http.verify();
  });
});
