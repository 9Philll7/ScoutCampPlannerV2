import { TestBed } from '@angular/core/testing';
import { HttpClient } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { ParticipantFilterSettingsComponent } from './participant-filter-settings.component';
import { API_BASE_URL } from '../../core/api-base-url';

describe('ParticipantFilterSettingsComponent', () => {
  function setup(fail = false) {
    const put = vi.fn(() => fail ? throwError(() => ({ error: { message: 'Version geändert' } })) : of({}));
    TestBed.configureTestingModule({ providers: [
      { provide: API_BASE_URL, useValue: '' },
      { provide: HttpClient, useValue: { get: () => of({ canEdit: true, filters: [], stages: [],
        catalog: { dietTypes: [], allergens: [], substances: [] } }), put } }
    ] });
    const fixture = TestBed.createComponent(ParticipantFilterSettingsComponent);
    fixture.componentRef.setInput('campId', 'camp'); fixture.detectChanges();
    return { component: fixture.componentInstance, put };
  }
  it('keeps null values and OR/AND groups in the saved document', () => {
    const { component, put } = setup(); component.create();
    const draft = component.draft()!; draft.name = 'Test';
    draft.definition.groups[0].conditions[0].values = [null];
    draft.definition.groups.push({ conditions: [{ field: 1, operator: 1, values: ['ChildYouth'] }] });
    expect(component.hasPendingChanges()).toBe(true);
    expect(component.summary(draft)).toContain('ODER');
    component.save();
    expect(put).toHaveBeenCalledWith(expect.stringContaining('/participant-filters/'),
      expect.objectContaining({ expectedVersion: 0, definition: draft.definition }), { withCredentials: true });
    expect(component.draft()).toBeNull();
  });
  it('rejects empty groups and preserves edits on conflict', () => {
    const { component, put } = setup(true); component.create();
    const draft = component.draft()!; draft.name = 'Test';
    component.save(); expect(put).not.toHaveBeenCalled();
    draft.definition.groups[0].conditions[0].values = [null];
    component.save();
    expect(component.error()).toBe('Version geändert');
    expect(component.draft()).toBe(draft);
    expect(component.hasPendingChanges()).toBe(true);
  });
  it('does not discard edits when opening another filter is cancelled', () => {
    const { component } = setup(); component.create(); component.draft()!.name = 'Unsaved';
    const previous = component.draft();
    const confirm = vi.spyOn(window, 'confirm').mockReturnValue(false);
    component.create(); expect(component.draft()).toBe(previous);
    confirm.mockRestore();
  });
});
