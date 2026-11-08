import { StructureNodeOption } from './meal-planning-api.service';

/** UI selection uses effective leaves; persistence uses non-overlapping subtree roots. */
export class CookingUnitStructure {
  readonly byId: Map<string, StructureNodeOption>;
  readonly children = new Map<string | null, StructureNodeOption[]>();
  readonly leaves = new Map<string, string[]>();
  valid = true;

  constructor(readonly nodes: StructureNodeOption[]) {
    this.byId = new Map(nodes.map(node => [node.id, node]));
    if (this.byId.size !== nodes.length) this.valid = false;
    for (const node of nodes) {
      if (node.parentId !== null && !this.byId.has(node.parentId)) this.valid = false;
      const siblings = this.children.get(node.parentId) ?? [];
      siblings.push(node); this.children.set(node.parentId, siblings);
    }
    const visit = (id: string, path: Set<string>): string[] => {
      if (path.has(id)) { this.valid = false; return []; }
      if (this.leaves.has(id)) return this.leaves.get(id)!;
      const next = new Set(path).add(id), children = this.children.get(id) ?? [];
      const leaves = children.length ? children.flatMap(child => visit(child.id, next)) : [id];
      this.leaves.set(id, leaves); return leaves;
    };
    nodes.forEach(node => visit(node.id, new Set()));
  }

  selectedLeaves(ids: string[]) { return new Set(ids.flatMap(id => this.leaves.get(id) ?? [])); }
  state(id: string, ids: string[]): 'full' | 'partial' | 'empty' {
    const leaves = this.leaves.get(id) ?? [], selected = this.selectedLeaves(ids);
    const count = leaves.filter(leaf => selected.has(leaf)).length;
    return count && count === leaves.length ? 'full' : count ? 'partial' : 'empty';
  }
  toggle(id: string, ids: string[], checked: boolean) {
    if (!this.valid || !this.byId.has(id)) return ids;
    const selected = this.selectedLeaves(ids);
    for (const leaf of this.leaves.get(id) ?? []) checked ? selected.add(leaf) : selected.delete(leaf);
    const result: string[] = [];
    const visit = (node: StructureNodeOption) => {
      const leaves = this.leaves.get(node.id) ?? [];
      if (leaves.every(leaf => selected.has(leaf))) result.push(node.id);
      else (this.children.get(node.id) ?? []).forEach(visit);
    };
    (this.children.get(null) ?? []).forEach(visit);
    // Unknown references require explicit removal, never silently drop them during another edit.
    return result.concat(ids.filter(value => !this.byId.has(value)));
  }
  rows(search: string, collapsed: Set<string>) {
    const term = search.trim().toLocaleLowerCase('de');
    const included = new Set<string>();
    if (term) for (const node of this.nodes.filter(node => node.name.toLocaleLowerCase('de').includes(term))) {
      let current: StructureNodeOption | undefined = node;
      const visited = new Set<string>();
      while (current && !visited.has(current.id)) {
        visited.add(current.id); included.add(current.id);
        current = this.byId.get(current.parentId ?? '');
      }
    }
    const rows: (StructureNodeOption & { depth: number; hasChildren: boolean })[] = [];
    const visit = (node: StructureNodeOption, depth: number) => {
      if (term && !included.has(node.id)) return;
      const children = this.children.get(node.id) ?? [];
      rows.push({ ...node, depth, hasChildren: children.length > 0 });
      if (term || !collapsed.has(node.id)) children.forEach(child => visit(child, depth + 1));
    };
    if (this.valid) (this.children.get(null) ?? []).forEach(node => visit(node, 0));
    return rows;
  }
}
