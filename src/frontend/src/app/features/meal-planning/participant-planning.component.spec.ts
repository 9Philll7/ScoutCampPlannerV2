import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ParticipantPlanningComponent } from './participant-planning.component';
import { API_BASE_URL } from '../../core/api-base-url';

describe('ParticipantPlanningComponent', () => {
  function setup() {
    TestBed.configureTestingModule({ imports: [ParticipantPlanningComponent], providers: [provideHttpClient(), provideHttpClientTesting(),
      { provide: API_BASE_URL, useValue: '' }] });
    const fixture = TestBed.createComponent(ParticipantPlanningComponent);
    fixture.componentRef.setInput('campId', 'camp');
    fixture.componentRef.setInput('units', [{ id: 'unit', name: 'Kitchen' }]);
    fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/camps/camp/meal-planning/participants').flush({ version: 2, demandMode: 0,
      requiresStructureMigration: false });
    return { component: fixture.componentInstance, http };
  }
  it('has only the planning mode, not names or direct assignments', () => {
    const { component, http } = setup();
    expect(Object.keys(component.configuration()!).sort()).toEqual(['demandMode', 'requiresStructureMigration', 'version']); http.verify();
  });
  it('sends one versioned batch and preserves unsaved assignments on conflict', () => {
    const { component, http } = setup(); component.configuration()!.demandMode = 1; component.save();
    const request = http.expectOne('/api/camps/camp/meal-planning/participants');
    expect(request.request.method).toBe('PUT'); expect(request.request.body.expectedVersion).toBe(2);
    expect(request.request.body.assignments).toBeUndefined();
    request.flush({}, { status: 409, statusText: 'Conflict' });
    expect(component.configuration()!.demandMode).toBe(1); expect(component.error()).toContain('Eingaben bleiben erhalten'); http.verify();
  });
});
