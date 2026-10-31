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
  it('never grants permissions automatically', () => {
    expect(api['grant']).not.toHaveBeenCalled();
    expect(component.days()).toEqual(['2027-07-01', '2027-07-02']);
  });
  it('paginates after filtering and sorting, resets search, and clamps shrinking results', () => {
    const person = component.overview()!.participants[0];
    component.overview.set({ ...component.overview()!, participants: Array.from({ length: 12 }, (_, index) => ({
      ...person, data: { ...person.data, id: `person-${index}`, displayName: `Test ${String(index).padStart(2, '0')}` }
    })) }); fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('tbody tr').length).toBe(5);
    component.fitPage(8); fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('tbody tr').length).toBe(8);
    component.fitPage(5); fixture.detectChanges();
    component.changePage({ pageIndex: 2, pageSize: 5 }); fixture.detectChanges();
    expect(component.pagedParticipants().map(row => row.data.id)).toEqual(['person-10', 'person-11']);
    component.search = 'Test 01'; component.clearHiddenSelection(); fixture.detectChanges();
    expect(component.pageIndex()).toBe(0);
    expect(component.pagedParticipants().map(row => row.data.id)).toEqual(['person-1']);
    component.pageIndex.set(20); expect(component.visiblePageIndex()).toBe(0);
    component.search = ''; component.changePage({ pageIndex: 0, pageSize: 10 }); fixture.detectChanges();
    expect(fixture.nativeElement.querySelectorAll('tbody tr').length).toBe(5);
  });
  it('keeps the embedded leaf list scoped even after resetting filters and changing groups', () => {
    api['list'].mockReturnValue(of({ ...structuredClone(overview), participants: [
      { ...overview.participants[0], data: { ...overview.participants[0].data, id: 'a', structureNodeId: 'a' } },
      { ...overview.participants[0], data: { ...overview.participants[0].data, id: 'b', structureNodeId: 'b' } }
    ] }));
    fixture.componentRef.setInput('scopedNodeId', 'a'); fixture.detectChanges();
    component.groupFilter = ''; component.search = '';
    expect(component.filteredParticipants().map(row => row.data.id)).toEqual(['a']);
    expect(component.scopedCount()).toBe(1);
    expect(fixture.nativeElement.textContent).toContain('Teilnehmer dieser Gruppe');
    fixture.componentRef.setInput('scopedNodeId', 'b'); fixture.detectChanges();
    expect(component.filteredParticipants().map(row => row.data.id)).toEqual(['b']);
  });
  it('keeps legacy assignments unknown and saves stage and category independently', () => {
    component.overview.set({ ...component.overview()!, stages: [{ id: 'stage', name: 'Biber' }] });
    component.edit(component.overview()!.participants[0]); fixture.detectChanges();
    expect(component.draft!.campStageId).toBeNull();
    expect(component.draft!.category).toBeNull();
    expect(fixture.nativeElement.querySelector('select[name="campStageId"]').textContent).toContain('Biber');
    component.draft!.campStageId = 'stage'; component.draft!.category = 'LeaderAdult'; component.save();
    expect(api['update'].mock.calls[0][2]).toMatchObject({ campStageId: 'stage', category: 'LeaderAdult', dietTypeId: null, absentDays: [] });
  });
  it('opens node details without a preview or a separate edit action in the list', () => {
    api['list'].mockReturnValue(of({ ...structuredClone(overview), participants: [
      { ...overview.participants[0], data: { ...overview.participants[0].data, structureNodeId: 'leaf' } }
    ] }));
    fixture.componentRef.setInput('scopedNodeId', 'leaf'); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[aria-label="Bearbeiten: Dummy"]')).toBeNull();
    (fixture.nativeElement.querySelector('tbody td') as HTMLElement).click(); fixture.detectChanges();
    expect(component.detailDocument()?.data.id).toBe('person');
    expect(fixture.nativeElement.querySelector('.participant-preview')).toBeNull();
    (fixture.nativeElement.querySelector('.person-heading button') as HTMLButtonElement).click(); fixture.detectChanges();
    expect(component.draft?.displayName).toBe('Dummy');
    expect(api['update']).not.toHaveBeenCalled();
    component.backToList(); component.overview.set({ ...component.overview()!, canEdit: false }); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[aria-label="Bearbeiten: Dummy"]')).toBeNull();
    expect(fixture.nativeElement.querySelector('[aria-label="Detailansicht öffnen: Dummy"]')).not.toBeNull();
  });
  it('keeps the general-list preview mounted when switching between different names', () => {
    const first = component.overview()!.participants[0];
    const second = { ...first, data: { ...first.data, id: 'long', displayName: 'Ein sehr langer erfundener Teilnehmername mit mehreren Bestandteilen' } };
    component.overview.set({ ...component.overview()!, participants: [first, second] });
    component.select(first); fixture.detectChanges();
    const preview = fixture.nativeElement.querySelector('.participant-preview');
    (fixture.nativeElement.querySelectorAll('.person-button')[1] as HTMLButtonElement).click(); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.participant-preview')).toBe(preview);
    expect(preview.querySelector('h3').title).toBe(second.data.displayName);
    expect(preview.querySelector('.preview-body')).not.toBeNull();
    expect(preview.querySelector('footer')).toBeNull();
    expect(preview.querySelector('header [aria-label="Teilnehmer bearbeiten"]')).not.toBeNull();
    expect(preview.querySelector('header [aria-label="Detailansicht öffnen"]')).not.toBeNull();
    expect(preview.querySelector('header [aria-label="Teilnehmer löschen"]')).not.toBeNull();
    expect(component.detailDocument()).toBeNull();
    (fixture.nativeElement.querySelectorAll('.person-button')[1] as HTMLButtonElement).click(); fixture.detectChanges();
    expect(component.selectedDocument()).toBeNull();
    expect(fixture.nativeElement.querySelector('.participant-preview')).toBeNull();
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
  it('prefills an eligible node without accepting a parent from the structure entry point', () => {
    component.overview.set({ ...component.overview()!, structureNodes: [
      { id: 'parent', parentId: null, name: 'Bereich' }, { id: 'leaf', parentId: 'parent', name: 'Gruppe' }
    ] });
    fixture.componentRef.setInput('initialStructureNodeId', 'leaf'); component.create();
    expect(component.draft?.structureNodeId).toBe('leaf');
    fixture.componentRef.setInput('initialStructureNodeId', 'parent'); component.create();
    expect(component.draft?.structureNodeId).toBeNull();
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
  it('adds, replaces and removes nutrition entries without duplicates or immediate saving', () => {
    const option = component.overview()!.catalog.substances[0];
    component.overview.set({ ...component.overview()!, catalog: { ...component.overview()!.catalog, allergens: [
      { ...option, id: 'milk', code: 'MILK', name: 'Milch' }, { ...option, id: 'nuts', code: 'TREE_NUTS', name: 'Nüsse' }
    ] } });
    component.create(); component.editorSection.set('diet'); fixture.detectChanges();
    component.beginAllergen(); component.allergenGroup = 'milk'; component.chooseAllergenGroup(); component.applyAllergen();
    expect(component.draft!.allergenIds).toEqual(['milk']);
    component.beginAllergen(); component.allergenChoice = 'milk'; component.applyAllergen();
    expect(component.draft!.allergenIds).toEqual(['milk']);
    component.beginAllergen('milk'); component.allergenGroup = 'nuts'; component.chooseAllergenGroup(); component.applyAllergen();
    expect(component.draft!.allergenIds).toEqual(['nuts']);
    component.substanceChoice = 'lactose'; component.addIntolerance();
    expect(component.requirement('lactose')!.thresholdGramsPerPortion).toBeNull();
    component.requirement('lactose')!.thresholdGramsPerPortion = 1.5;
    component.substanceChoice = 'lactose'; component.addIntolerance();
    expect(component.draft!.intolerances.length).toBe(1);
    expect(component.requirement('lactose')!.thresholdGramsPerPortion).toBe(1.5);
    fixture.detectChanges();
    const nutrition = fixture.nativeElement.querySelector('.choice-grid').closest('section');
    expect(nutrition.querySelector('input[type="checkbox"]')).toBeNull();
    expect(nutrition.querySelector('[aria-label="Gewählte Allergene"]').textContent).toContain('Nüsse');
    expect(component.participantSections.some(section => section.id === 'health')).toBe(false);
    component.removeAllergen('nuts'); component.removeIntolerance('lactose');
    expect(component.draft!.allergenIds).toEqual([]); expect(component.draft!.intolerances).toEqual([]);
    expect(api['create']).not.toHaveBeenCalled(); expect(api['update']).not.toHaveBeenCalled();
  });
  it('supports multiple subgroups or an explicitly unknown subgroup', () => {
    const option = component.overview()!.catalog.substances[0];
    component.overview.set({ ...component.overview()!, catalog: { ...component.overview()!.catalog, allergens: [
      { ...option, id: 'grain', code: 'GLUTEN_CEREALS', name: 'Getreide' },
      { ...option, id: 'wheat', code: 'WHEAT', name: 'Weizen', parentId: 'grain' },
      { ...option, id: 'rye', code: 'RYE', name: 'Roggen', parentId: 'grain' },
      { ...option, id: 'custom', code: 'OTHER', name: 'Sonstiges' }
    ] } });
    component.create(); component.beginAllergen(); component.allergenGroup = 'grain'; component.chooseAllergenGroup();
    expect(component.allergenGroups().map(row => row.id)).toEqual(['grain']);
    expect(component.validAllergenChoice()).toBe(true);
    component.allergenChoice = 'grain'; component.applyAllergen();
    expect(component.draft!.allergenIds).toEqual(['grain']);
    component.beginAllergen('grain');
    component.toggleAllergenSubgroup('wheat'); component.toggleAllergenSubgroup('rye'); component.applyAllergen();
    expect(component.draft!.allergenIds).toEqual(['wheat', 'rye']);
    expect(component.groupedAllergens(component.draft!.allergenIds)).toEqual([
      { id: 'grain', name: 'A · Getreide', subgroups: ['Weizen', 'Roggen'] }
    ]);
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.nutrition-items').children.length).toBe(1);
    expect(component.allergenName('wheat')).toBe('A · Weizen');
    component.beginAllergen('grain');
    expect(component.allergenSubgroups).toEqual(['wheat', 'rye']);
    component.toggleAllergenSubgroup('wheat'); component.toggleAllergenSubgroup('rye');
    expect(component.allergenSubgroups).toEqual([]);
    component.applyAllergen();
    expect(component.draft!.allergenIds).toEqual(['grain']);
    component.beginAllergen('grain');
    expect(component.allergenSubgroups).toEqual([]);
    component.toggleAllergenSubgroup('rye');
    expect(component.allergenSubgroups).toEqual(['rye']);
    component.applyAllergen();
    expect(component.draft!.allergenIds).toEqual(['rye']);
    component.draft!.allergenIds.push('wheat', 'custom');
    component.removeAllergen('grain');
    expect(component.draft!.allergenIds).toEqual(['custom']);
  });
  it('stages intolerance popup edits until they are applied', () => {
    component.create(); component.substanceChoice = 'lactose'; component.addIntolerance();
    component.requirement('lactose')!.thresholdGramsPerPortion = 1.5;
    component.beginIntolerance('lactose'); component.intoleranceThreshold = 2;
    expect(component.requirement('lactose')!.thresholdGramsPerPortion).toBe(1.5);
    component.beginIntolerance('lactose');
    expect(component.intoleranceThreshold).toBe(1.5);
    component.intoleranceThreshold = -1; component.applyIntolerance();
    expect(component.requirement('lactose')!.thresholdGramsPerPortion).toBe(1.5);
    component.intoleranceThreshold = 2; component.applyIntolerance();
    expect(component.requirement('lactose')!.thresholdGramsPerPortion).toBe(2);
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
  it('filters and sorts existing participants without mutating the source and resolves full structure paths', () => {
    const first = component.overview()!.participants[0];
    component.overview.set({ ...component.overview()!, structureNodes: [
      { id: 'north', parentId: null, name: 'Nord' }, { id: 'leaf', parentId: 'north', name: 'Gruppe' }
    ], participants: [first, { ...first, data: { ...first.data, id: 'anna', displayName: 'Anna', structureNodeId: 'leaf' } }] });
    expect(component.filteredParticipants().map(document => document.data.id)).toEqual(['anna', 'person']);
    expect(component.overview()!.participants[0].data.id).toBe('person');
    component.groupFilter = 'leaf'; component.search = 'AN';
    expect(component.filteredParticipants().map(document => document.data.id)).toEqual(['anna']);
    expect(component.structurePath('leaf')).toBe('Nord › Gruppe');
    component.groupFilter = 'north'; expect(component.filteredParticipants().map(document => document.data.id)).toEqual(['anna']);
    component.groupFilter = 'unassigned'; component.search = '';
    expect(component.filteredParticipants().map(document => document.data.id)).toEqual(['person']);
  });
  it('opens a read-only preview without starting an editor or sending a mutation', () => {
    component.overview.set({ ...component.overview()!, canEdit: false }); fixture.detectChanges();
    const cell = fixture.nativeElement.querySelector('tbody tr td') as HTMLTableCellElement; cell.click(); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[aria-label="Teilnehmervorschau"]')?.textContent).toContain('Dummy');
    expect(component.draft).toBeNull(); expect(api['update']).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).not.toContain('Teilnehmer löschen');
    expect(fixture.nativeElement.textContent).not.toContain('Teilnehmer hinzufügen');
    component.search = 'absent'; component.clearHiddenSelection();
    expect(component.selectedDocument()).toBeNull();
  });
  it('combines stage, category and diet filters and resets them together', () => {
    const first = component.overview()!.participants[0];
    component.overview.set({ ...component.overview()!, participants: [
      { ...first, data: { ...first.data, id: 'a', campStageId: 'stage', category: 'ChildYouth', dietTypeId: 'diet' } },
      { ...first, data: { ...first.data, id: 'b', campStageId: 'stage', category: 'LeaderAdult', dietTypeId: null } }
    ] });
    component.stageFilter = 'stage'; component.categoryFilter = 'ChildYouth'; component.dietFilter = 'diet';
    expect(component.filteredParticipants().map(row => row.data.id)).toEqual(['a']);
    expect(component.summaryCount('ChildYouth')).toBe(1);
    expect(component.summaryCount('diet')).toBe(1);
    component.resetFilters(); expect(component.filteredParticipants().length).toBe(2);
    component.dietFilter = 'unassigned'; expect(component.filteredParticipants().map(row => row.data.id)).toEqual(['b']);
  });
  it('opens full details without a draft and preserves edits when switching editor sections', () => {
    const person = component.overview()!.participants[0];
    component.openDetails(person); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.participant-detail-grid')).not.toBeNull();
    expect(Array.from(fixture.nativeElement.querySelectorAll('.participant-detail-grid .editor-page')).filter(page => !(page as HTMLElement).hidden).length).toBe(1);
    component.editorSection.set('diet'); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.participant-detail-grid [hidden]')).not.toBeNull();
    expect(fixture.nativeElement.querySelector('table[aria-label="Teilnehmerliste"]')).toBeNull();
    expect(component.draft).toBeNull();
    component.edit(person); expect(component.editorSection()).toBe('diet'); component.draft!.displayName = 'Changed';
    component.editorSection.set('diet'); fixture.detectChanges();
    expect(component.draft!.displayName).toBe('Changed');
    expect(component.hasPendingChanges()).toBe(true);
    expect(api['update']).not.toHaveBeenCalled();
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    component.backToList(); expect(component.draft!.displayName).toBe('Changed');
    confirm.mockReturnValue(true); component.backToList(); fixture.detectChanges();
    expect(component.detailDocument()).toBeNull(); expect(component.draft).toBeNull();
    confirm.mockRestore();
  });
  it('selects a diet using cards without saving and can clear the choice', () => {
    component.overview.set({ ...component.overview()!, catalog: { ...component.overview()!.catalog, dietTypes: [
      { id: 'diet', code: 'TEST', name: 'Test-Ernährung', parentId: null, isQuantityDependent: false }
    ] } });
    component.create(); component.editorSection.set('diet'); fixture.detectChanges();
    (fixture.nativeElement.querySelectorAll('.choice-card')[1] as HTMLButtonElement).click(); fixture.detectChanges();
    expect(component.draft!.dietTypeId).toBe('diet');
    expect(fixture.nativeElement.querySelectorAll('.choice-card')[1].getAttribute('aria-pressed')).toBe('true');
    (fixture.nativeElement.querySelector('.choice-card') as HTMLButtonElement).click();
    expect(component.draft!.dietTypeId).toBeNull(); expect(api['create']).not.toHaveBeenCalled();
  });
  it('protects an edited draft when selecting another participant and preserves filters when closing', () => {
    const person = component.overview()!.participants[0]; component.edit(person); component.draft!.displayName = 'Changed';
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    component.select(person); expect(component.draft!.displayName).toBe('Changed');
    expect(component.selectedDocument()).toBeNull();
    component.search = 'Dummy'; confirm.mockReturnValue(true); component.closeEditor();
    expect(component.draft).toBeNull(); expect(component.search).toBe('Dummy'); confirm.mockRestore();
  });
  it('prevents mutations when the camp becomes frozen', () => {
    component.edit(component.overview()!.participants[0]);
    component.overview.set({ ...component.overview()!, isFrozen: true }); component.save();
    expect(api['update']).not.toHaveBeenCalled();
  });
});
