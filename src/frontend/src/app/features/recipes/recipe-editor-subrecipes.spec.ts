import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { beforeEach, afterEach, vi } from 'vitest';
import { RecipeEditorComponent } from './recipe-editor.component';
import { RecipeEditorApiService, RecipeEditorDraft, RecipeEditorSubrecipeReference } from './recipe-editor-api.service';
let original: PropertyDescriptor | undefined;
beforeEach(()=>{ original=Object.getOwnPropertyDescriptor(HTMLDialogElement.prototype,'showModal'); Object.defineProperty(HTMLDialogElement.prototype,'showModal',{configurable:true,writable:true,value:function(this:HTMLDialogElement){this.open=true;}}); });
afterEach(()=>{if(original)Object.defineProperty(HTMLDialogElement.prototype,'showModal',original);else Reflect.deleteProperty(HTMLDialogElement.prototype,'showModal');});
const portion: RecipeEditorSubrecipeReference={revisionId:'r1',recipeId:'child',revisionNumber:1,name:'Sauce alt',recipeType:0,referenceServings:10,referenceQuantity:null,referenceUnit:null,units:[],canSelect:false,conflicts:[]};
const latest={...portion,revisionId:'r2',revisionNumber:2,name:'Sauce neu',canSelect:true};
const quantity: RecipeEditorSubrecipeReference={...portion,revisionId:'rq',recipeId:'quantity',name:'Teig',recipeType:1,referenceServings:null,referenceQuantity:1000,referenceUnit:{unitId:'g',name:'Gramm',symbol:'g',dimension:0,baseUnitFactor:1},units:[{unitId:'g',name:'Gramm',symbol:'g',dimension:0,baseUnitFactor:1},{unitId:'kg',name:'Kilogramm',symbol:'kg',dimension:0,baseUnitFactor:1000}],canSelect:true};
async function setup(){
 const stored:RecipeEditorDraft={id:'parent',campId:'camp',status:1,draftVersion:3,ingredientReferences:[],subrecipeReferences:[portion,latest,quantity,{...latest,revisionId:'self',recipeId:'parent',name:'Self'},{...latest,revisionId:'cycle',recipeId:'cycle',canSelect:false}],content:{name:'Parent',description:null,source:null,internalNotes:null,recipeType:0,referenceServings:10,referenceQuantity:null,referenceUnitId:null,defaultAgeGroupScalingApplies:false,authoringStage:null,tags:[],groups:[{id:'g1',name:'Basis',sortOrder:0},{id:'g2',name:'Extra',sortOrder:1}],ingredientPositions:[{id:'ingredient',groupId:'g1',ingredientRevisionId:null,quantity:10,unitId:null,sortOrder:0,scalingMode:0,ageGroupScaling:0,stepSize:null,quantityPerStep:null,replacements:[]}],subrecipePositions:[{id:'sub',groupId:'g1',recipeRevisionId:'r1',servings:2,quantity:null,unitId:null,sortOrder:1,replacements:[{id:'replacement',recipeRevisionId:'r2',servings:2,quantity:null,unitId:null,conflicts:[{type:0,id:'allergen'}]}]}]}};
 const api={referenceData:()=>of({units:[{id:'g',name:'Gramm',symbol:'g',dimension:0,baseUnitFactor:1}],allergens:[],intolerances:[],origins:[],categories:[]}),list:()=>of([]),ingredients:()=>of([]),get:()=>of(structuredClone(stored)),nutrition:()=>of(null),save:vi.fn((_camp:string,value:RecipeEditorDraft)=>of(structuredClone(value)))};
 TestBed.configureTestingModule({imports:[RecipeEditorComponent],providers:[{provide:RecipeEditorApiService,useValue:api}]});
 const fixture=TestBed.createComponent(RecipeEditorComponent);fixture.componentRef.setInput('campId','camp');fixture.detectChanges();
 const component=fixture.componentInstance;component.open({libraryEntryId:null,recipeId:'parent',revisionId:'published',revisionNumber:1,name:'Parent',scope:2,status:1,isLocal:true,updatedAtUtc:''});component.activeTab.set('ingredients');fixture.detectChanges();await fixture.whenStable();
 return{component,fixture,api};
}
describe('Recipe subrecipes',()=>{
 it('shows the exact pinned published name and amount as a real read-only row',async()=>{
  const {fixture,component}=await setup();const row=fixture.nativeElement.querySelector('.subrecipe-row');expect(row.textContent).toContain('Sauce alt');expect(row.textContent).not.toContain('Sauce neu');expect(row.textContent).toContain('2 Standardportionen');expect(row.querySelector('input')).toBeNull();component.openSubrecipeEditor();expect(component.ingredientDialogMode()).toBeNull();
 });
 it('keeps pending additions local, applies them explicitly and saves the pinned revision',async()=>{
  const {component,fixture,api}=await setup();component.beginEdit();component.openSubrecipeEditor();component.selectSubrecipe(latest);component.subrecipeDraft()!.groupId='g2';component.subrecipeDraft()!.servings=4;fixture.detectChanges();
  expect(component.selected()!.content.subrecipePositions).toHaveLength(1);expect(component.ingredientDialogDirty()).toBe(true);component.save();expect(api.save).not.toHaveBeenCalled();component.applyIngredientDialog();expect(component.selected()!.content.subrecipePositions).toHaveLength(2);component.save();expect(api.save).toHaveBeenCalledWith('camp',expect.objectContaining({content:expect.objectContaining({subrecipePositions:expect.arrayContaining([expect.objectContaining({recipeRevisionId:'r2',servings:4,groupId:'g2'})])})}));
 });
 it('cancels local edits and preserves existing replacement rules when editing the embedding',async()=>{
  const {component}=await setup();component.beginEdit();const child=component.selected()!.content.subrecipePositions[0];const replacements=structuredClone(child.replacements);component.openSubrecipeEditor(child);component.subrecipeDraft()!.servings=9;component.closeIngredientDialog();expect(child.servings).toBe(2);
  component.openSubrecipeEditor(child);component.subrecipeDraft()!.servings=3;component.subrecipeDraft()!.groupId='g2';expect(component.validSubrecipeDraft()).toBe(true);component.applyIngredientDialog();expect(component.selected()!.content.subrecipePositions[0]).toMatchObject({recipeRevisionId:'r1',servings:3,groupId:'g2',replacements});component.cancelEdit();expect(component.selected()!.content.subrecipePositions[0].servings).toBe(2);
 });
 it('uses the appropriate quantity mode and refuses invalid amounts or units',async()=>{
  const {component}=await setup();component.beginEdit();component.openSubrecipeEditor();component.selectSubrecipe(quantity);expect(component.subrecipeDraft()).toMatchObject({servings:null,quantity:1000,unitId:'g'});expect(component.validSubrecipeDraft()).toBe(true);component.subrecipeDraft()!.unitId='litre';expect(component.validSubrecipeDraft()).toBe(false);component.subrecipeDraft()!.unitId='kg';component.subrecipeDraft()!.quantity=0;expect(component.validSubrecipeDraft()).toBe(false);component.subrecipeDraft()!.quantity=0.5;expect(component.validSubrecipeDraft()).toBe(true);
 });
 it('prevents duplicates in the same group while allowing a separate group',async()=>{
  const {component}=await setup();component.beginEdit();component.openSubrecipeEditor();component.selectSubrecipe(latest);component.subrecipeDraft()!.groupId='g1';component.applyIngredientDialog();component.openSubrecipeEditor();component.selectSubrecipe(latest);component.subrecipeDraft()!.groupId='g1';expect(component.validSubrecipeDraft()).toBe(false);component.subrecipeDraft()!.groupId='g2';expect(component.validSubrecipeDraft()).toBe(true);
 });
 it('orders ingredient and subrecipe entries together and keeps contiguous sort indexes',async()=>{
  const {component}=await setup();component.beginEdit();const content=component.selected()!.content;component.movePosition(content.subrecipePositions[0],-1);expect(content.subrecipePositions[0].sortOrder).toBe(0);expect(content.ingredientPositions[0].sortOrder).toBe(1);component.openSubrecipeEditor();component.selectSubrecipe(latest);component.subrecipeDraft()!.groupId='g1';component.applyIngredientDialog();expect(component.selected()!.content.subrecipePositions[1].sortOrder).toBe(2);component.removeSubrecipe(component.selected()!.content.subrecipePositions[0]);expect(component.selected()!.content.ingredientPositions[0].sortOrder).toBe(0);expect(component.selected()!.content.subrecipePositions[0].sortOrder).toBe(1);
 });
 it('excludes self and cyclic candidates and does not silently upgrade a pinned revision',async()=>{
  const {component}=await setup();component.beginEdit();expect(component.subrecipeCandidates().map(value=>value.revisionId)).toEqual(['r2','rq']);component.openSubrecipeEditor();component.selectSubrecipe({...latest,recipeId:'parent'});expect(component.subrecipeDraft()!.recipeRevisionId).toBeNull();component.closeIngredientDialog();expect(component.selected()!.content.subrecipePositions[0].recipeRevisionId).toBe('r1');
 });
});

describe('Recipe reference and replacements',()=>{
 it('switches the recipe reference without inventing a conversion and restores it on cancel',async()=>{
  const {component}=await setup(); component.beginEdit(); component.changeRecipeType(1);
  expect(component.selected()!.content).toMatchObject({recipeType:1,referenceServings:null,referenceQuantity:null,referenceUnitId:null,authoringStage:null,defaultAgeGroupScalingApplies:false});
  expect(component.recipeUnits()[0].symbol).toBe('g'); component.selected()!.content.referenceQuantity=500; component.selected()!.content.referenceUnitId='g'; expect(component.recipeReferenceLabel()).toBe('500 g');
  component.changeRecipeType(0); expect(component.selected()!.content.referenceQuantity).toBeNull(); component.cancelEdit(); expect(component.selected()!.content.referenceServings).toBe(10);
 });
 it('edits replacement amounts locally and cancels the entire embedding without changing the saved rule',async()=>{
  const {component}=await setup(); component.beginEdit(); const position=component.selected()!.content.subrecipePositions[0]; component.openSubrecipeEditor(position); component.openSubrecipeReplacement(position.replacements[0]);
  expect(component.ingredientDialogDirty()).toBe(false); expect(component.validSubrecipeDraft()).toBe(false); component.subrecipeReplacementDraft()!.servings=4; expect(component.ingredientDialogDirty()).toBe(true); expect(component.validSubrecipeReplacement()).toBe(true);
  component.applySubrecipeReplacement(); expect(component.subrecipeDraft()!.replacements[0].servings).toBe(4); component.closeIngredientDialog(); expect(position.replacements[0].servings).toBe(2);
 });
 it('requires a valid same-type replacement, amount and unique conflict assignment',async()=>{
  const {component}=await setup(); component.beginEdit(); component.openSubrecipeEditor(component.selected()!.content.subrecipePositions[0]); component.openSubrecipeReplacement();
  expect(component.subrecipeReplacementCandidates().map(value=>value.revisionId)).toEqual(['r2']); component.selectSubrecipeReplacement('r2'); expect(component.validSubrecipeReplacement()).toBe(false);
  const conflict={type:0,id:'allergen'}; expect(component.subrecipeConflictUsed(conflict)).toBe(true); component.toggleSubrecipeReplacementConflict(conflict,true); expect(component.subrecipeReplacementDraft()!.conflicts).toEqual([]);
  component.subrecipeReplacementDraft.set(null); component.removeSubrecipeReplacement('replacement'); component.openSubrecipeReplacement(); component.selectSubrecipeReplacement('r2'); component.toggleSubrecipeReplacementConflict(conflict,true); expect(component.validSubrecipeReplacement()).toBe(true); component.subrecipeReplacementDraft()!.servings=0; expect(component.validSubrecipeReplacement()).toBe(false);
  component.subrecipeReplacementDraft()!.servings=3; component.applySubrecipeReplacement(); component.applyIngredientDialog(); expect(component.selected()!.content.subrecipePositions[0].replacements[0]).toMatchObject({recipeRevisionId:'r2',servings:3,conflicts:[conflict]});
 });
});

describe('Subrecipe replacement popup',()=>{
 it('opens the same dialog style from the list in view mode without changing the recipe',async()=>{
  const {component,fixture}=await setup(); const before=JSON.stringify(component.selected()!.content);
  const button=fixture.nativeElement.querySelector('.subrecipe-row .diet-status') as HTMLButtonElement;
  expect(button.getAttribute('aria-haspopup')).toBe('dialog'); button.click(); fixture.detectChanges();
  const dialog=fixture.nativeElement.querySelector('dialog'); expect(dialog.getAttribute('aria-labelledby')).toBeTruthy(); expect(component.ingredientDialogTitle()).toBe('Ernährung & Ersatz');
  expect(dialog.textContent).toContain('Sauce neu'); expect(dialog.textContent).toContain('2 Standardportionen'); expect(dialog.textContent).toContain('Gespeicherte Anforderung'); expect(dialog.querySelector('input,select')).toBeNull();
  expect(fixture.nativeElement.querySelector('.subrecipe-details-row')).toBeNull(); expect(component.editing()).toBe(false); expect(component.hasPendingChanges()).toBe(false); component.closeIngredientDialog(); expect(JSON.stringify(component.selected()!.content)).toBe(before);
 });
 it('edits replacement rules directly in the popup with local cancel and explicit apply',async()=>{
  const {component,fixture}=await setup(); component.beginEdit(); const position=component.selected()!.content.subrecipePositions[0]; component.openSubrecipeReplacements(position); fixture.detectChanges();
  expect(component.ingredientDialogMode()).toBe('subrecipe-replacements'); expect(fixture.nativeElement.querySelector('[name=subrecipeGroup]')).toBeNull();
  component.openSubrecipeReplacement(position.replacements[0]); component.subrecipeReplacementDraft()!.servings=5; component.applySubrecipeReplacement(); component.closeIngredientDialog(); expect(position.replacements[0].servings).toBe(2);
  component.openSubrecipeReplacements(position); component.openSubrecipeReplacement(position.replacements[0]); component.subrecipeReplacementDraft()!.servings=4; component.applySubrecipeReplacement(); component.applyIngredientDialog(); expect(component.selected()!.content.subrecipePositions[0].replacements[0].servings).toBe(4);
 });
});

describe('Subrecipe variant coverage',()=>{
 it('shows the variant label and concrete ingredient variants in the list and popup',async()=>{
  const {component,fixture}=await setup(); const reference=component.selected()!.subrecipeReferences!.find(value=>value.revisionId==='r1')!;
  reference.conflicts=[{type:0,id:'milk',name:'Milch',preventableByVariants:['Milch: Haferdrink']}]; component.selected.set(structuredClone(component.selected()!)); fixture.detectChanges();
  const button=fixture.nativeElement.querySelector('.subrecipe-row .diet-status') as HTMLButtonElement; expect(button.classList.contains('preventable')).toBe(true); expect(button.textContent).toContain('(Variante)'); button.click(); fixture.detectChanges();
  const dialog=fixture.nativeElement.querySelector('dialog'); expect(dialog.textContent).toContain('Vermeidbar durch Milch: Haferdrink.'); expect(dialog.querySelector('.dialog-conflict-list .preventable')).not.toBeNull();
 });
 it('keeps a mixed set red while explaining each conflict separately',async()=>{
  const {component,fixture}=await setup(); component.selected()!.subrecipeReferences![0].conflicts=[{type:0,id:'milk',name:'Milch',preventableByVariants:['Haferdrink']},{type:1,id:'other',name:'Histamin',preventableByVariants:[]}]; component.selected.set(structuredClone(component.selected()!)); fixture.detectChanges();
  const button=fixture.nativeElement.querySelector('.subrecipe-row .diet-status') as HTMLButtonElement; expect(button.classList.contains('unavoidable')).toBe(true); expect(button.textContent).not.toContain('(Variante)'); button.click(); fixture.detectChanges();
  expect(fixture.nativeElement.querySelectorAll('.dialog-conflict-list .preventable')).toHaveLength(1); expect(fixture.nativeElement.querySelectorAll('.dialog-conflict-list .unavoidable')).toHaveLength(1);
 });
});
