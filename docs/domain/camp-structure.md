# Lager Structure Domain

## Lager

A camp belongs to exactly one tenant.

A tenant can own multiple camps.

Every newly created camp has a required start and end date. The end date must not precede the start date. Within one tenant, the combination of invariant-normalized camp name, start date, and end date is unique. The same name may be reused for a different period.

Name and period may be changed by an active camp member with `camp.edit`. The same uniqueness and period rules apply to changes. A camp cannot be changed while it is frozen for an offline transfer. The change and its audit event are committed atomically.

Creating a camp requires at least one explicitly selected `CampAdmin`. The creator is not assigned automatically; another active member of the owning tenant may be selected. Camp creation, initial membership and role assignment, and the required audit event form one atomic operation.

## Flexible structure

The Camp module stores a neutral tree of structure nodes. It does not define fixed concepts such as subcamp, group, or cooking unit.

A camp supports two structure modes:

- `Free`: branches may have different depths and administrators choose node names freely.
- `Fixed`: the camp defines one ordered sequence of administrator-named levels. Every branch must follow that sequence and participant-bearing leaves must be on its final level.

An existing free tree may be fixed only when every path already matches the selected level sequence. Returning to free mode is always allowed. Changing a fixed level sequence is allowed only when the existing tree remains valid.

Node names are unique after invariant normalization among siblings in the same camp. Root nodes therefore share one uniqueness scope. Equal names in different branches are allowed.

Participants may be assigned only to leaf nodes. A node contains either child nodes or participants, never both. A child cannot be created below a node that already contains participants. A new parent may be inserted above such a node; its participants remain on the existing leaf.

A node with children or participants cannot be deleted directly. Moving a node is allowed only when it causes neither a participant/child conflict nor a violation of the fixed level sequence.

All structure changes require `camp.edit`, are blocked while the camp is frozen, and are recorded atomically with their audit event.

## Catering separation

Cooking units are not Camp structure nodes. The Catering module owns its own cooking-unit model and assigns participants independently of their position in the Camp tree. The former spike-only Camp `CookingUnit` model and its package-v1 payload are removed before the first product release; no released package compatibility promise is affected.

## Module use

The neutral camp structure is available to other modules through Camp contracts. It is the organisational foundation for finances, catering, program, and material without giving those modules ownership of the tree.
