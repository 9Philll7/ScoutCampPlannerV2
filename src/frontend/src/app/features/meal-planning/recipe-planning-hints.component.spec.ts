import { TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { vi } from 'vitest';
import { RecipePlanningHintsComponent } from './recipe-planning-hints.component';
import { MealPlanningApiService, RecipePlanningHint, RecipePlanningHints } from './meal-planning-api.service';

describe('RecipePlanningHintsComponent', () => {
  const hint = (state: RecipePlanningHint['state'], kind: RecipePlanningHint['kind'] = 'Allergen'): RecipePlanningHint => ({
    kind, state, referenceId: state, code: 'MILK', name: 'Milch', explanation: 'Zutat Milch im Standardrezept.',
    evidence: [{ ingredientRevisionId: 'ingredient', ingredientName: 'Milchpulver', positionPath: 'path' }],
  });
  const result = (hints: RecipePlanningHint[], unknown = false): RecipePlanningHints => ({ recipeRevisionId: 'revision', basis: 'PublishedStandard', evaluatedAtUtc: '2026-11-01T00:00:00Z', hasUnknownData: unknown, hints });
  function setup(value: RecipePlanningHints) {
    const api = { recipeHints: vi.fn(() => of(value)) };
    TestBed.configureTestingModule({ imports: [RecipePlanningHintsComponent], providers: [{ provide: MealPlanningApiService, useValue: api }] });
    const fixture = TestBed.createComponent(RecipePlanningHintsComponent);
    fixture.componentRef.setInput('campId', 'camp'); fixture.componentRef.setInput('revisionId', 'revision'); fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, api };
  }
  it('shows known causes, quantities and data gaps without advertising missing data as free-from', () => {
    const { fixture, component } = setup(result([hint('Contains'), { ...hint('QuantityDependent', 'Substance'), name: 'Laktose', code: 'LACTOSE' }, hint('NotContained')], true));
    const chips = fixture.nativeElement.querySelector('.hint-chips').textContent;
    expect(chips).toContain('Milch (G)'); expect(chips).toContain('Laktose');
    expect(chips).toContain('Angaben unvollständig'); expect(chips).not.toContain('laktosefrei');
    expect(component.visibleHints().some(row => row.state === 'NotContained')).toBe(false);
    expect(fixture.nativeElement.querySelector('.hint-details').textContent).toContain('Milchpulver');
    expect(fixture.nativeElement.querySelector('.hint-details').textContent).not.toContain('Laut Angaben nicht enthalten');
  });
  it('separates excluded dietary rules from compatible labels', () => {
    const { component, fixture } = setup(result([{ ...hint('Excluded', 'Diet'), name: 'Vegan' }, { ...hint('Compatible', 'Diet'), name: 'Vegetarisch' }]));
    expect(component.label(component.visibleHints()[0])).toBe('Vegan');
    expect(component.label(component.visibleHints()[1])).toBe('Vegetarisch');
    expect(fixture.nativeElement.querySelector('.hint-details').textContent).toContain('Vegan');
    expect(fixture.nativeElement.querySelector('.hint-details').textContent).not.toContain('Vegetarisch');
    component.data.set(result([{ ...hint('Compatible', 'Diet'), name: 'Vegetarisch' }, hint('NotContained')])); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('.hint-chips').textContent).toContain('Vegetarisch');
    expect(fixture.nativeElement.querySelector('.hint-chips button')).toBeNull();
    const dialog = { showModal: vi.fn() }; component.openDetails(dialog);
    expect(dialog.showModal).not.toHaveBeenCalled();
  });
  it('clears old revision facts while loading a different revision and can retry a failure', () => {
    const { fixture, component, api } = setup(result([hint('Contains')]));
    const pending = new Subject<RecipePlanningHints>(); api.recipeHints.mockReturnValue(pending as never);
    fixture.componentRef.setInput('revisionId', 'other'); fixture.detectChanges();
    expect(component.data()).toBeNull(); expect(component.loading()).toBe(true);
    pending.error(new Error('unavailable')); fixture.detectChanges();
    expect(component.error()).toBe(true);
    expect(fixture.nativeElement.querySelector('.hint-chips').textContent).toContain('nicht verfügbar');
    api.recipeHints.mockReturnValue(of(result([], true))); component.refresh(); fixture.detectChanges();
    expect(component.error()).toBe(false); expect(api.recipeHints).toHaveBeenLastCalledWith('camp', 'other');
  });
  it('marks complete ingredient adjustments yellow, uncovered causes red and missing assessments neutral', () => {
    const { fixture } = setup(result([
      { ...hint('Contains'), referenceId: 'adjustable', adjustment: 'Available' },
      { ...hint('Excluded', 'Diet'), referenceId: 'uncovered', name: 'Vegan', adjustment: 'Unavailable' },
      { ...hint('QuantityDependent', 'Substance'), referenceId: 'unknown', name: 'Laktose', adjustment: 'Unknown' },
    ]));
    expect(fixture.nativeElement.querySelector('.hint-chips .adjustable').getAttribute('aria-label')).toContain('abdeckbar');
    expect(fixture.nativeElement.querySelector('.hint-chips .uncovered').getAttribute('aria-label')).toContain('Nicht abgedeckt');
    const neutral = Array.from(fixture.nativeElement.querySelectorAll('.hint-chips button') as NodeListOf<HTMLElement>).find(node => node.textContent?.includes('Laktose'))!;
    expect(neutral.classList.contains('uncovered')).toBe(false); expect(neutral.classList.contains('adjustable')).toBe(false);
  });
});
