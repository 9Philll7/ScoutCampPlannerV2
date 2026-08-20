# ScoutCampPlanner - Recipe Domain Model

## Purpose

This document defines the recipe domain for ScoutCampPlanner. It is the authoritative functional specification for implementing recipes. It intentionally excludes menu-plan, cooking-unit, purchasing and inventory behavior except where an interface boundary must be defined.

## Core principle

A published recipe revision must be fully calculable without additional recipe decisions on the menu-plan level. A menu plan later provides usage context such as the selected recipe revision and target participants. Recipe-specific ingredient decisions live in the recipe itself, except for runtime conflict handling based on participant requirements.

## 1. Recipe identity and scope

A recipe has an immutable technical ID and belongs to exactly one scope:

- `central`: shared central recipe catalog
- `tenant`: reusable recipe library of one tenant
- `camp`: local to one camp

The normalized recipe name must be unique within its scope. `tenant` and `camp` recipes require their owning tenant or camp ID. Names are not globally unique across tenants or camps.

Name normalization must at least be case-insensitive and trim/collapse whitespace.

A human-readable recipe number may be added as a presentation feature, but the technical ID is authoritative.

## 2. Recipe types

Recipes have exactly one reference model.

### 2.1 Portion-based recipe

Used for meals that can be placed directly in a menu plan.

Required reference data:

- `reference_servings > 0`
- the platform-wide standard-portion reference with factor `1.0`

All ingredient and subrecipe quantities describe the stated number of normalized standard reference servings.

Tenant and camp drafts may use one of their configured stages as an authoring basis. The draft records the stage ID, stage name and the factor used for authoring. Publication normalizes the reference servings to standard portions before creating the immutable revision:

`standard_reference_servings = entered_reference_servings * authoring_stage_factor`

Ingredient and subrecipe quantities are not changed during this normalization. Published revisions in every scope therefore always use the standard-portion reference with factor `1.0`. The authoring-stage snapshot remains audit/lineage metadata and never changes published calculation semantics.

Only portion-based recipes are eligible for direct menu-plan use.

### 2.2 Quantity-based recipe

Used primarily for components/subrecipes such as sauces, doughs or spice mixes.

Required reference data:

- `reference_quantity > 0`
- `reference_unit_id`

A quantity-based recipe is not directly menu-plan eligible.

### 2.3 Mutual nesting

Portion-based and quantity-based recipes may reference each other as subrecipes.

A subrecipe position always uses the reference model of the referenced recipe revision:

- portion-based target -> required subrecipe servings
- quantity-based target -> required subrecipe quantity + compatible unit

## 3. Recipe master data

A recipe contains:

- ID
- normalized unique name in scope
- optional description
- optional source as free text
- status
- recipe type
- type-specific reference data
- tags
- internal recipe notes
- audit data

There is no structured recipe category. Meal classification belongs to the menu-plan level.

There are no structured preparation steps, timing fields, difficulty, equipment, infrastructure, image, attachment, translation, or external-link collections. Relevant preparation information belongs in the recipe description. The optional source field may contain a URL.

## 4. Status lifecycle

Statuses:

- `draft`
- `active`
- `archived`

### Draft

May be incomplete and invalid. Draft changes are explicitly saved; there is no autosave.

### Active

May publish revisions. First activation publishes revision 1.

### Archived

Remains readable and historically referenceable. It may still be used by existing published revisions. It is hidden from normal new-selection flows and may produce a warning when newly referenced. New revisions cannot be published while archived.

### Active -> Draft

Allowed only when no published revision has ever become an externally relevant reference. This includes at least menu/cooking usage, subrecipe or replacement-recipe references, derivations, central submissions or central publication lineage.

If safe, returning to draft may discard the existing revision history and a later activation begins again at revision 1.

If any published revision is already relevant externally, returning to draft is forbidden; archive instead.

### Deletion

Normal users can only archive. Permanent physical deletion is reserved for super-admin storage cleanup and must be blocked while references exist.

## 5. Tags

Tags are lightweight search aids only.

- freely creatable
- no central mandatory catalog
- no effect on calculation or conflict evaluation
- versioned as part of recipe revisions
- copied into local derivations and independently editable afterwards

Tag values should be normalized to avoid case/whitespace duplicates.

## 6. Ingredient groups

Groups are optional and contain only:

- name
- sort order

They have no calculation or conflict semantics.

A recipe can contain grouped and ungrouped positions simultaneously.

Empty groups are allowed in draft but are a publication error.

## 7. Recipe positions

There are exactly two functional position types:

- ingredient position
- subrecipe position

Both support:

- optional group
- display sort order

There are no free-text positions.

All published positions are mandatory parts of the recipe; there is no generic `optional` flag.

## 8. Ingredient positions

An ingredient position contains:

- one base ingredient reference
- numeric quantity > 0
- unit valid for that base ingredient
- optional group
- sort order
- scaling mode
- age-group-scaling override
- zero or more replacement ingredient rules

There is no position-specific preparation text. Such instructions belong to the recipe description.

### Units

A recipe may use every unit for which the referenced base ingredient defines a valid conversion. Unit conversion remains the responsibility of the base-ingredient domain.

Changing the base ingredient must revalidate the selected unit.

### Variants

Variants of a base ingredient are treated as 1:1 interchangeable by the ingredient domain and are not explicitly selected in the recipe position.

### Duplicate base ingredients

The same base ingredient may occur multiple times in the same recipe only in different groups. The ungrouped area behaves like one implicit group for this uniqueness rule.

## 9. Replacement ingredient rules

Replacement rules are bound to one concrete ingredient position.

A rule contains:

- replacement base ingredient
- replacement quantity > 0
- replacement unit valid for the replacement ingredient
- one or more conflict references divided into:
  - allergens
  - intolerances
  - dietary requirements

The semantics are **applicable for conflicts**, not **guaranteed to solve conflicts**.

The relational model should keep direct foreign keys to the three authoritative conflict catalogs. Application code may expose a common `ConflictReference { type, id }` abstraction.

At one ingredient position, one individual conflict reference may belong to at most one replacement rule. A single rule may cover multiple conflicts.

Different replacement rules are not automatically composed into a new solution. If a runtime combination is not explicitly resolved, it remains an unresolved conflict.

Replacement rules are optional. A recipe is not required to solve all possible participant conflicts.

A replacement that does not actually eliminate its declared conflict is a warning, not a publication error.

## 10. Subrecipes

A subrecipe position references one immutable published recipe revision.

It contains:

- referenced recipe revision
- required servings OR required quantity/unit according to target recipe type
- optional group
- sort order
- zero or more replacement-recipe rules

Cycles are forbidden, including indirect cycles.

The same referenced recipe revision may appear more than once only in different groups. The ungrouped area again behaves as one implicit group.

Conflicts from nested recipes propagate upward. The parent recipe does not modify the internals of the referenced recipe.

## 11. Replacement recipes

A subrecipe position may declare replacement-recipe rules analogous to replacement ingredients.

A rule contains:

- replacement published recipe revision
- replacement servings OR quantity/unit
- allergen references
- intolerance references
- dietary requirement references

Original and replacement recipe must use the same reference model:

- portion-based -> portion-based
- quantity-based -> quantity-based

This restriction avoids ambiguous conversion semantics.

Replacement recipes are re-evaluated for conflicts. Remaining or new conflicts continue to propagate upward.

## 12. Scaling

### 12.1 Age-group factor

The age-group factor originates later from cooking-unit/menu-plan context. Published recipe reference servings are always standard portions with factor `1.0`; the context factor is applied only to the top-level portion-based recipe demand.

Nested recipes do not independently apply age-group factors.

A portion-based recipe has a default policy for whether age-group scaling applies. Ingredient positions can override with:

- `inherit`
- `apply`
- `ignore`

Replacement ingredient quantities inherit the policy of their ingredient position and do not define a separate override.

`ignore` is required for per-person discrete items such as one ice cream per participant.

### 12.2 Quantity scaling modes

Supported modes:

- `linear` (default)
- `fixed`
- `stepwise`

No arbitrary formulas and no free-form piecewise tables in the first implementation.

Stepwise scaling must be represented by explicit structured parameters rather than executable expressions.

Replacement ingredient rules use the position's scaling semantics unless the implementation explicitly models a narrowly defined replacement scaling override. Do not introduce arbitrary formulas.

### 12.3 No recipe-level rounding

Calculated recipe quantities remain exact. Procurement rounding belongs to the purchasing layer.

### 12.4 No yield model

Do not calculate or store recipe yield/portion mass. Doing so would require density, cooking loss, swelling and other transformations outside the current scope.

## 13. Revision model

Drafts are mutable. Published revisions are immutable snapshots.

A new revision exists only after an explicit `publish revision` action. Ordinary draft save does not create a revision.

Each revision stores at least:

- recipe ID
- sequential revision number
- published timestamp
- published by
- complete functional snapshot
- optional change note
- historical publication warnings
- warning acknowledgements
- optional lineage metadata for restored revisions or central submissions

Every external recipe use must reference a concrete published revision, never a floating recipe head.

### Restore

A previous published revision can be copied into the current draft. Existing history remains untouched. The next publication receives the next sequential revision number.

There is no separate persistent draft-history log. Draft audit data only needs last editor, last update and concurrency version.

## 14. Central, tenant and camp recipe model

Recipe distribution follows an explicit three-level flow:

`central catalog -> tenant library -> camp library`

Central recipes are read-only to tenants and camps. Tenant recipes are reusable inside their tenant and read-only to camps until copied into a camp-local draft.

A tenant may explicitly adopt a central published revision into its library. It may retain the exact central revision as a read-only reference or create an independently editable tenant draft with lineage to that revision. No later central change is applied automatically.

A camp may adopt either a central revision or a tenant revision. When a user edits a referenced recipe:

1. create a camp-local mutable draft from the selected immutable snapshot;
2. retain lineage to the source recipe, source revision and source scope;
3. local editing/revisioning then proceeds independently.

A published tenant or camp revision may later be submitted as a central change proposal. A proposal is not globally visible until explicitly accepted by a platform administrator.

When reviewing a proposal, show three states where applicable:

- central revision originally copied
- latest current central revision
- submitted local revision

Do not implement automatic semantic merge of recipes.

If a tenant or camp elects to adopt an upstream revision again, the active local entry returns to referencing that exact revision. Future edits again create a local draft. Never silently discard local changes.

### Update checks

There is no automatic synchronization. Users may explicitly check for upstream updates for one recipe or all upstream references in a tenant or camp library. Adoption is always explicit.

## 15. Recipe duplication and derivation

### Duplicate

A recipe may be duplicated from a draft or a published revision. The duplicate:

- gets a new recipe ID
- starts as draft
- gets a new unique name
- starts with empty revision history
- copies functional content
- continues to reference the same nested published recipe revisions

### Derivation

Git-like arbitrary branching is intentionally not implemented.

Instead, a recipe may record lineage to a source recipe and source revision. The derived recipe is otherwise independent and has its own linear revision history.

There is no automatic synchronization or merge between source and derivative.

## 16. Local camp notes

Local notes are separate from revisioned recipe notes.

They belong to the camp's local recipe entry, survive central revision changes, and are never submitted as recipe content.

Multiple notes are supported. Each contains:

- text
- created by / at
- last edited by / at
- optional soft-delete metadata

They are editable and visible to all users with appropriate access to the camp. They are not private per-user notes and do not need their own version history initially.

## 17. Concurrent editing

Do not hard-lock recipe drafts.

Maintain lightweight editing presence so users can see that another user is currently editing. Presence expires after inactivity and is informational only.

Draft saves use optimistic concurrency with a monotonically changing draft version.

If the persisted draft version changed since the user loaded it, reject blind overwrite and present a three-way field-level comparison:

- originally loaded state
- user's changes
- current persisted draft

Automatic merge is allowed only for clearly independent non-overlapping changes. Conflicting fields/positions require explicit user selection.

## 18. Permissions

Define permissions independently from role names. At minimum:

- recipe read
- recipe edit
- recipe publish/activate
- recipe archive/reactivate
- recipe reset-to-draft (subject to lifecycle constraints)
- local recipe notes manage
- central recipe change proposal submit
- central recipe change proposal review
- permanent recipe delete (super-admin only)

A user with publish permission may publish their own edits. No four-eyes approval is required.

## 19. Audit

At minimum retain:

Recipe:
- created_by / created_at
- updated_by / updated_at
- archived_by / archived_at
- reactivated_by / reactivated_at

Revision:
- published_by / published_at

Warning acknowledgement:
- acknowledged_by / acknowledged_at

Local note:
- created_by / created_at
- updated_by / updated_at

## 20. Scope boundaries

Do not implement the following as recipe-domain responsibilities:

- participant conflict-resolution workflow
- menu-plan revision update workflow
- procurement rounding
- inventory selection
- automatic variant choice at cooking-unit level
- organizational handling of unresolved conflicts

The recipe domain only exposes enough immutable data and conflict information for those later layers to operate.
