import { TestBed } from '@angular/core/testing';
import { RecipeCatalogComponent } from './recipe-catalog.component';
import { RecipeCatalogEntry } from './recipe-editor-api.service';

const entries: RecipeCatalogEntry[] = Array.from({length: 23}, (_,index) => ({ libraryEntryId: 'entry-'+index, recipeId: 'recipe-'+index,
  revisionId: index === 0 ? null : 'revision-'+index, revisionNumber: index === 0 ? null : 1,
  name: `Rezept ${String(index).padStart(2,'0')}`, scope: index < 20 ? 2 : 1, status: index === 0 ? 0 : 1,
  isLocal: index < 20, updatedAtUtc: `2026-11-${String(index+1).padStart(2,'0')}T12:00:00Z` }));
function setup() {
 TestBed.configureTestingModule({imports:[RecipeCatalogComponent]});
 const fixture=TestBed.createComponent(RecipeCatalogComponent); fixture.componentRef.setInput('entries',entries); fixture.detectChanges();
 return {fixture,component:fixture.componentInstance};
}
describe('Recipe catalogue',()=>{
 it('pages ten rows, searches the full catalogue and resets the page on filter changes',()=>{
  const {component,fixture}=setup(); expect(fixture.nativeElement.querySelectorAll('tbody tr')).toHaveLength(10);
  const next=fixture.nativeElement.querySelector('button[aria-label="Nächste Seite"]') as HTMLButtonElement; next.click(); fixture.detectChanges(); expect(component.visible()[0].name).toBe('Rezept 10');
  component.query.set('Rezept 22'); component.page.set(0); fixture.detectChanges(); expect(component.visible()).toHaveLength(1); expect(component.visible()[0].recipeId).toBe('recipe-22');
  component.query.set(''); component.page.set(2); component.chooseKind('library'); fixture.detectChanges(); expect(component.page()).toBe(0); expect(component.filtered()).toHaveLength(3);
 });
 it('combines scope and status filters and sorts by modification date',()=>{
  const {component}=setup(); component.scope.set('2'); component.state.set('0'); expect(component.filtered().map(value=>value.recipeId)).toEqual(['recipe-0']);
  component.resetFilters(); component.sort.set('updated'); expect(component.filtered()[0].recipeId).toBe('recipe-22');
  component.query.set('missing'); expect(component.filtered()).toEqual([]);
 });
 it('opens local recipes from their row while retaining library access restrictions',()=>{
  const {component,fixture}=setup(); const opened:RecipeCatalogEntry[]=[]; component.opened.subscribe(value=>opened.push(value));
  (fixture.nativeElement.querySelector('tbody tr') as HTMLTableRowElement).click(); expect(opened[0].recipeId).toBe('recipe-0');
  component.chooseKind('library'); fixture.detectChanges(); (fixture.nativeElement.querySelector('tbody tr') as HTMLTableRowElement).click(); expect(opened).toHaveLength(1);
  expect(fixture.nativeElement.querySelector('tbody .name-button').tagName).toBe('DIV');
  fixture.componentRef.setInput('disabled',true); fixture.detectChanges(); expect(fixture.nativeElement.querySelector('.catalog-heading button').disabled).toBe(true);
 });
 it('clamps the final page after a removed item and preserves the range when changing presentation',()=>{
  const {component,fixture}=setup(); component.page.set(2); fixture.detectChanges(); expect(component.visible()).toHaveLength(3);
  fixture.componentRef.setInput('entries',entries.slice(0,20)); fixture.detectChanges(); expect(component.visible()[0].recipeId).toBe('recipe-10');
  component.grid.set(true); fixture.detectChanges(); expect(fixture.nativeElement.querySelectorAll('.recipe-tile')).toHaveLength(10);
 });
});
