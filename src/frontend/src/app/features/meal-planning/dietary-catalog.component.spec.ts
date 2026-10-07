import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { DietaryCatalogComponent } from './dietary-catalog.component';
import { DietaryCatalogApiService, DietaryType } from './dietary-catalog-api.service';

describe('DietaryCatalogComponent', () => {
  const central: DietaryType = { id: 'central', tenantId: null, name: 'Central', description: null, sortOrder: 0, version: 1, rules: [] };
  function setup(tenant: string | null) {
    const api = { list: vi.fn(() => of({ types: [central], origins: [], substances: [] })),
      contributions: vi.fn(() => of([])), save: vi.fn(() => of({})), submit: vi.fn(() => of({})) };
    TestBed.configureTestingModule({ imports: [DietaryCatalogComponent], providers: [{ provide: DietaryCatalogApiService, useValue: api }] });
    const fixture = TestBed.createComponent(DietaryCatalogComponent);
    fixture.componentRef.setInput('tenantId', tenant); fixture.detectChanges();
    return { component: fixture.componentInstance, api };
  }
  it('does not expose central editing or review to tenant mode', () => {
    const { component, api } = setup('tenant');
    component.edit(central); expect(component.draft).toBeNull();
    expect(api.contributions).not.toHaveBeenCalled();
    component.create(); expect(component.draft!.tenantId).toBe('tenant');
  });
  it('keeps absent rules unknown and edits detached values', () => {
    const { component } = setup(null); component.edit(central);
    expect(component.rule(component.draft!, 'plant')).toBeNull();
    component.setRule(component.draft!, 'plant', 0);
    expect(central.rules).toEqual([]);
    component.setRule(component.draft!, 'plant', null);
    expect(component.draft!.rules).toEqual([]);
  });
  it('preserves the draft on concurrency conflict', () => {
    const { component, api } = setup(null); component.edit(central);
    component.draft!.name = 'Edited';
    api.save.mockReturnValue(throwError(() => ({ status: 409 })));
    component.save(); expect(component.draft!.name).toBe('Edited');
    expect(component.error()).toContain('Stand geändert');
    expect(api.save.mock.calls[0]).toBeDefined();
  });
});
