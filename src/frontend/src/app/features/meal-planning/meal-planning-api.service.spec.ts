import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { API_BASE_URL } from '../../core/api-base-url';
import { MealPlanDocument, MealPlanningApiService } from './meal-planning-api.service';

describe('MealPlanningApiService', () => {
  let service: MealPlanningApiService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        { provide: API_BASE_URL, useValue: 'http://localhost:5180' },
      ],
    });
    service = TestBed.inject(MealPlanningApiService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('sends the expected version and complete offer document when saving', () => {
    const plan: MealPlanDocument = {
      id: 'plan-1', campId: 'camp-1', name: 'Standard', sortOrder: 2, version: 4,
      offerGroups: [{
        id: 'group-1', campMealId: 'meal-1', name: 'Menü', sortOrder: 0,
        entries: [{
          id: 'entry-1', recipeRevisionId: 'revision-1', isStandard: true,
          displayName: null, role: 0, note: null, sortOrder: 0,
        }],
      }],
    };

    service.savePlan('camp-1', plan).subscribe();

    const request = http.expectOne('http://localhost:5180/api/camps/camp-1/meal-plans/plan-1');
    expect(request.request.method).toBe('PUT');
    expect(request.request.withCredentials).toBe(true);
    expect(request.request.body).toEqual({
      expectedVersion: 4, name: 'Standard', sortOrder: 2, offerGroups: plan.offerGroups,
    });
    request.flush({ id: 'plan-1', version: 5 });
  });

  it('uses the dedicated endpoint when resetting a structure override', () => {
    service.resetStructure('camp-1', 'unit-1', 'meal-1').subscribe();

    const request = http.expectOne(
      'http://localhost:5180/api/camps/camp-1/cooking-units/unit-1/meals/meal-1/structure-override');
    expect(request.request.method).toBe('DELETE');
    request.flush({ id: 'unit-1', version: null });
  });
});
