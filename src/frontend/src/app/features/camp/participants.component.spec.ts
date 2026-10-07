import { TestBed, ComponentFixture } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { ParticipantsComponent } from './participants.component';
import { ParticipantsApiService, ParticipantsOverview } from './participants-api.service';

describe('ParticipantsComponent', () => {
  const overview: ParticipantsOverview = {
    dummyDataOnly: true, startDate: '2027-07-01', endDate: '2027-07-02', isFrozen: false, canEdit: true,
    participants: [{ data: { id: 'person', displayName: 'Dummy', dietTypeId: null, absentDays: [], absentMealIds: [], allergenIds: [], intolerances: [] }, stateToken: 'original' }],
    meals: [{ id: 'meal', date: '2027-07-01', name: 'Lunch', isActive: true }],
    catalog: { allergens: [], dietTypes: [], substances: [{ id: 'lactose', code: 'LACTOSE', name: 'Laktose', isQuantityDependent: true, parentId: null }] }
  };
  let fixture: ComponentFixture<ParticipantsComponent>;
  let component: ParticipantsComponent;
  let api: Record<string, ReturnType<typeof vi.fn>>;
  beforeEach(() => {
    api = { list: vi.fn(() => of(structuredClone(overview))), members: vi.fn(() => of([])),
      grant: vi.fn(() => of({})), update: vi.fn(() => of({})), create: vi.fn(() => of({})), remove: vi.fn(() => of({})) };
    TestBed.configureTestingModule({ imports: [ParticipantsComponent], providers: [{ provide: ParticipantsApiService, useValue: api }] });
    fixture = TestBed.createComponent(ParticipantsComponent); component = fixture.componentInstance;
    fixture.componentRef.setInput('campId', 'camp'); fixture.detectChanges();
  });
  it('shows the dummy boundary and never grants permissions automatically', () => {
    expect(fixture.nativeElement.textContent).toContain('Dummy-/Testdaten');
    expect(api['grant']).not.toHaveBeenCalled();
    expect(component.days()).toEqual(['2027-07-01', '2027-07-02']);
  });
  it('offers only final-depth leaves for a fixed structure', () => {
    component.overview.set({ ...component.overview()!, participantStructureDepth: 2, structureNodes: [
      { id: 'root', parentId: null, name: 'Bereich' },
      { id: 'leaf', parentId: 'root', name: 'Gruppe' },
      { id: 'shallow', parentId: null, name: 'Unvollständiger Bereich' }
    ] });
    component.create(); fixture.detectChanges();
    const options = Array.from(fixture.nativeElement.querySelectorAll('select[name="structureNodeId"] option'))
      .map(option => (option as HTMLOptionElement).textContent?.trim());
    expect(options).toEqual(['Noch nicht zugeordnet', 'Gruppe']);
    component.overview.set({ ...component.overview()!, participantStructureDepth: null });
    expect(component.participantNodes().map(node => node.id)).toEqual(['leaf', 'shallow']);
  });
  it('preserves edits on conflict and sends the original concurrency token', () => {
    component.edit(component.overview()!.participants[0]); component.draft!.displayName = 'Edited dummy';
    api['update'].mockReturnValue(throwError(() => ({ status: 409 })));
    component.save();
    expect(api['update'].mock.calls[0][1].stateToken).toBe('original');
    expect(component.draft!.displayName).toBe('Edited dummy');
    expect(component.error()).toContain('Stand hat sich geändert');
  });
  it('starts requirements without invented thresholds and prevents read-only saves', () => {
    component.create(); component.toggleRequirement('lactose', true);
    expect(component.requirement('lactose')!.thresholdGramsPerPortion).toBeNull();
    component.overview.set({ ...component.overview()!, canEdit: false }); component.save();
    expect(api['create']).not.toHaveBeenCalled();
  });
  it('drops the previous camp draft on camp change', () => {
    component.edit(component.overview()!.participants[0]);
    fixture.componentRef.setInput('campId', 'other'); fixture.detectChanges();
    expect(component.draft).toBeNull(); expect(api['list']).toHaveBeenLastCalledWith('other');
  });
  it('copies a catalog threshold once and preserves individual edits when the catalog changes', () => {
    const current = component.overview()!;
    component.overview.set({ ...current, catalog: { ...current.catalog, substances: [
      { ...current.catalog.substances[0], defaultThresholdGramsPerPortion: 1.25,
        defaultThresholdSource: 'Synthetische Testvorgabe', defaultThresholdVersion: 1 }
    ] } });
    component.create(); component.toggleRequirement('lactose', true);
    expect(component.requirement('lactose')!.thresholdGramsPerPortion).toBe(1.25);
    expect(component.requirement('lactose')!.thresholdSource).toContain('Stand 1');
    component.requirement('lactose')!.thresholdGramsPerPortion = 0.5;
    component.overview.set(current);
    expect(component.requirement('lactose')!.thresholdGramsPerPortion).toBe(0.5);
  });
});
