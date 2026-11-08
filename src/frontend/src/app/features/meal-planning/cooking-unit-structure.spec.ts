import { CookingUnitStructure } from './cooking-unit-structure';

describe('CookingUnitStructure', () => {
  const nodes = [
    { id: 'camp', parentId: null, name: 'Lager' },
    { id: 'north', parentId: 'camp', name: 'Unterlager Nord' },
    { id: 'fox', parentId: 'north', name: 'Fuchs' },
    { id: 'owl', parentId: 'north', name: 'Eule' },
    { id: 'south', parentId: 'camp', name: 'Unterlager Süd' },
    { id: 'bear', parentId: 'south', name: 'Bär' },
  ];
  it('selects a complete subtree and sends only non-overlapping roots', () => {
    const tree = new CookingUnitStructure(nodes);
    const selected = tree.toggle('north', [], true);
    expect(selected).toEqual(['north']);
    expect(tree.state('north', selected)).toBe('full');
    expect(tree.state('fox', selected)).toBe('full');
    expect(tree.state('camp', selected)).toBe('partial');
    expect(tree.state('south', selected)).toBe('empty');
  });
  it('propagates indeterminate state through every ancestor after a child is deselected', () => {
    const tree = new CookingUnitStructure(nodes);
    const selected = tree.toggle('owl', ['camp'], false);
    expect(selected).toEqual(['fox', 'south']);
    expect(tree.state('camp', selected)).toBe('partial');
    expect(tree.state('north', selected)).toBe('partial');
    expect(tree.state('owl', selected)).toBe('empty');
    expect(tree.toggle('owl', selected, true)).toEqual(['camp']);
  });
  it('searches with ancestors visible without narrowing the selection scope', () => {
    const tree = new CookingUnitStructure(nodes);
    expect(tree.rows('Fuchs', new Set(['camp', 'north'])).map(row => row.id)).toEqual(['camp', 'north', 'fox']);
    const selected = tree.toggle('north', [], true);
    expect(tree.selectedLeaves(selected)).toEqual(new Set(['fox', 'owl']));
    expect(tree.rows('', new Set(['north'])).map(row => row.id)).not.toContain('fox');
  });
  it('keeps missing saved references until explicitly removed', () => {
    const tree = new CookingUnitStructure(nodes);
    expect(tree.toggle('north', ['missing'], true)).toEqual(['north', 'missing']);
  });
  it('handles several roots and empty selections', () => {
    const tree = new CookingUnitStructure([{ id: 'one', parentId: null, name: 'One' }, { id: 'two', parentId: null, name: 'Two' }]);
    expect(tree.toggle('one', ['two'], true)).toEqual(['one', 'two']);
    expect(tree.toggle('one', ['one'], false)).toEqual([]);
    expect(new CookingUnitStructure([]).valid).toBe(true);
  });
  it('rejects cycles and missing parents without looping or rewriting saved selections', () => {
    for (const invalid of [[{ id: 'one', parentId: 'one', name: 'Cycle' }], [{ id: 'one', parentId: 'missing', name: 'Orphan' }]]) {
      const tree = new CookingUnitStructure(invalid);
      expect(tree.valid).toBe(false);
      expect(tree.rows('', new Set())).toEqual([]);
      expect(tree.toggle('one', ['one'], false)).toEqual(['one']);
    }
  });
});
