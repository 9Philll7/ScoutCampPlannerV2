import { TestBed } from '@angular/core/testing';
import { HttpErrorResponse } from '@angular/common/http';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { RecipeEditorComponent } from './recipe-editor.component';
import { RecipeEditorApiService, RecipeEditorDraft, RecipePublicationResponse, RecipeValidationIssue } from './recipe-editor-api.service';
const draft = (): RecipeEditorDraft => ({ id: 'recipe', campId: 'camp', status: 1, draftVersion: 0, ingredientReferences: [], content: {
 name: 'Rezept', description: null, source: null, internalNotes: null, recipeType: 0, referenceServings: 10, referenceQuantity: null, referenceUnitId: null, defaultAgeGroupScalingApplies: false, authoringStage: null, tags: [], groups: [{id:'group',name:'Teig',sortOrder:0}], ingredientPositions: [{id:'position',groupId:'group',ingredientRevisionId:null,quantity:1,unitId:null,sortOrder:0,scalingMode:0,ageGroupScaling:0,stepSize:null,quantityPerStep:null,replacements:[{id:'replacement',ingredientRevisionId:null,quantity:null,unitId:null,conflicts:[]}]}], subrecipePositions: [] } });
const issue = (code: string, severity = 0, context: Record<string,string> = {}): RecipeValidationIssue => ({code, severity, message: code, context});
async function setup() {
 const api = {list: () => of([]), ingredients: () => of([]), get: () => of(draft()), nutrition: () => of(null), publish: vi.fn((_camp: string, _draft: RecipeEditorDraft, _acknowledgeWarnings: boolean) => of({revisionNumber:2} as RecipePublicationResponse))};
 TestBed.configureTestingModule({imports:[RecipeEditorComponent],providers:[{provide:RecipeEditorApiService,useValue:api}]});
 const fixture = TestBed.createComponent(RecipeEditorComponent); fixture.componentRef.setInput('campId','camp'); fixture.detectChanges();
 const component = fixture.componentInstance; component.open({libraryEntryId:null,recipeId:'recipe',revisionId:'revision',revisionNumber:1,name:'Rezept',scope:2,status:1,isLocal:true,updatedAtUtc:''}); component.activeTab.set('publication'); fixture.detectChanges(); await fixture.whenStable();
 return {fixture, component, api, panel:()=>fixture.nativeElement.querySelector('#recipe-panel-publication') as HTMLElement};
}
describe('Recipe publication overview',()=>{
 it('reserves independent validation and history without invoking publication or inventing successful checks',async()=>{
  const {panel,api}=await setup();
  const buttons=Array.from(panel().querySelectorAll('button'));
  expect(buttons.find(button=>button.textContent?.includes('Prüfung ausführen'))?.disabled).toBe(true);
  expect(buttons.find(button=>button.textContent?.includes('Verlauf anzeigen'))?.disabled).toBe(true);
  expect(panel().textContent).toContain('Noch keine separate Vorabprüfung'); expect(panel().textContent).not.toContain('Alle Prüfungen bestanden'); expect(api.publish).not.toHaveBeenCalled();
 });
 it('separates real errors and warnings returned by publication',async()=>{
  const {component,fixture,panel,api}=await setup(); const errors=[issue('recipe.ingredient.quantity.invalid',0,{positionId:'position'})]; const warnings=[issue('recipe.source.missing',1)];
  api.publish.mockReturnValue(throwError(()=>new HttpErrorResponse({status:422,error:{status:4,validation:{issues:[...errors,...warnings]}}})));
  component.publish(false); fixture.detectChanges();
  expect(panel().querySelectorAll('.errors .publication-issue')).toHaveLength(1); expect(panel().querySelectorAll('.warnings .publication-issue')).toHaveLength(1);
  expect(panel().textContent).toContain('Die Veröffentlichung ist derzeit nicht möglich.');
  expect(panel().querySelector<HTMLButtonElement>('.publication-primary-action')!.disabled).toBe(true);
 });
 it('requires explicit warning acknowledgement before a second publication request',async()=>{
  const {component,fixture,panel,api}=await setup();
  api.publish.mockReturnValueOnce(throwError(()=>new HttpErrorResponse({status:422,error:{status:5,validation:{issues:[issue('recipe.source.missing',1)]}}})));
  component.publish(false); fixture.detectChanges(); expect(panel().querySelector<HTMLButtonElement>('.publication-primary-action')!.disabled).toBe(true);
  component.publish(true); expect(api.publish).toHaveBeenCalledTimes(1);
  component.publicationWarningsAcknowledged.set(true); fixture.detectChanges(); expect(panel().querySelector<HTMLButtonElement>('.publication-primary-action')!.disabled).toBe(false);
  component.publish(true); fixture.detectChanges(); expect(api.publish).toHaveBeenCalledTimes(2); expect(api.publish.mock.calls[1][2]).toBe(true); expect(component.publicationWarningsAcknowledged()).toBe(false);
 });
 it('routes replacement issues to their owning ingredient without switching into edit mode',async()=>{
  const {component,fixture}=await setup(); const frame=vi.spyOn(window,'requestAnimationFrame').mockReturnValue(0);
  try { component.goToValidationIssue(issue('recipe.replacement.conflicts.empty',0,{replacementId:'replacement'})); fixture.detectChanges();
   expect(component.activeTab()).toBe('ingredients'); expect(component.validationTargetId()).toBe('position'); expect(component.editing()).toBe(false);
   expect(fixture.nativeElement.querySelector('[data-validation-position="position"].validation-target')).not.toBeNull();
  } finally {frame.mockRestore();}
 });
 it('routes group errors to the group and metadata issues to basic data',async()=>{
  const {component}=await setup(); const frame=vi.spyOn(window,'requestAnimationFrame').mockReturnValue(0);
  try {component.goToValidationIssue(issue('recipe.group.empty',0,{groupId:'group'})); expect(component.validationTargetId()).toBe('group');
   component.goToValidationIssue(issue('recipe.source.missing',1)); expect(component.activeTab()).toBe('basic'); expect(component.validationTargetId()).toBeNull();
  }finally{frame.mockRestore();}
 });
 it('shows nutrient gaps as supplementary hints without asking for a publication acknowledgement',async()=>{
  const {component,fixture,panel}=await setup(); component.nutrition.set({isComplete:false,total:null,perStandardPortion:null,missingContributions:[{ingredientRevisionId:'ingredient',ingredientName:'Zutat',recipeRevisionPath:[],positionId:'position',reason:0}]});fixture.detectChanges();
  expect(panel().textContent).toContain('Zusätzlicher Hinweis'); expect(panel().querySelector('.publication-confirmation')).toBeNull(); expect(panel().querySelector<HTMLButtonElement>('.publication-primary-action')!.disabled).toBe(false);
 });
 it('blocks publishing archived recipes, view-only permissions and pending edits',async()=>{
  const {component,fixture,api}=await setup(); component.selected()!.status=2;component.publish(false);expect(api.publish).not.toHaveBeenCalled();
  component.selected()!.status=1;fixture.componentRef.setInput('disabled',true);fixture.detectChanges();component.publish(false);expect(api.publish).not.toHaveBeenCalled();
  fixture.componentRef.setInput('disabled',false);fixture.detectChanges();component.beginEdit();component.selected()!.content.name='Geändert';component.publish(false);expect(api.publish).not.toHaveBeenCalled();
 });
});
