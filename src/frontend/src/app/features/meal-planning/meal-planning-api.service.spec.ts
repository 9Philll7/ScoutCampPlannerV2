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

  it('uses schedule versions for creating, ordering, activity and removing day meals', () => {
    service.addAdditionalMeal('camp', '2027-07-02', 'Snack', 7).subscribe();
    const add = http.expectOne('http://localhost:5180/api/camps/camp/meals/additional');
    expect(add.request.body).toEqual({ date: '2027-07-02', name: 'Snack', expectedScheduleVersion: 7 }); add.flush({ scheduleVersion: 8 });
    service.orderDayMeals('camp', '2027-07-02', ['one', 'two'], 8).subscribe();
    const order = http.expectOne('http://localhost:5180/api/camps/camp/meals/days/2027-07-02/order');
    expect(order.request.body).toEqual({ mealIds: ['one', 'two'], expectedScheduleVersion: 8 }); order.flush({ scheduleVersion: 9 });
    service.setMealActivity('camp', 'one', false, 9).subscribe();
    const activity = http.expectOne('http://localhost:5180/api/camps/camp/meals/one/activity');
    expect(activity.request.body).toEqual({ isActive: false, expectedScheduleVersion: 9 }); activity.flush(null);
    service.removeAdditionalMeal('camp', 'two', 10).subscribe();
    const remove = http.expectOne('http://localhost:5180/api/camps/camp/meals/two');
    expect(remove.request.method).toBe('DELETE'); expect(remove.request.headers.get('If-Match')).toBe('"10"');
    remove.flush({ outcome: 'Deleted', scheduleVersion: 11 });
  });
});
