# Codex Implementation Brief - Recipe Domain

## Mission

Implement the ScoutCampPlanner recipe domain described by the companion documents in `docs/domain`.

Treat these documents as the functional source of truth for the recipe feature. Reuse the repository's established architecture, conventions and ingredient-domain abstractions where they already exist. Do not redesign adjacent domains unless a minimal interface is required.

## Read first

1. `docs/domain/RECIPE_DOMAIN_MODEL.md`
2. `docs/domain/RECIPE_VALIDATION_AND_CALCULATION_RULES.md`
3. `docs/domain/RECIPE_PERSISTENCE_MODEL.md`

Also inspect the existing project documentation for:

- base ingredients and variants
- allergens
- intolerances
- dietary requirements
- units and ingredient-specific conversions
- tenants/camps/users/permissions
- central/local data behavior
- architecture-spike decisions and coding guidelines

If existing repository conventions differ from illustrative names in these docs, follow repository conventions while preserving the functional invariants.

## Non-negotiable invariants

1. Published recipe revisions are immutable.
2. External references always target a concrete revision ID.
3. Recipe drafts are mutable and do not create history on every save.
4. Publication is explicit.
5. Errors block publication; warnings require acknowledgement but do not block.
6. Recipes are not required to resolve every participant conflict.
7. Base-ingredient variants are handled by the ingredient domain, not manually selected as recipe alternatives.
8. Replacement rules are position-specific and conflict-specific.
9. Different replacement rules are not automatically composed.
10. Subrecipe cycles are forbidden.
11. Age-group factors are applied only from top-level cooking/menu context, not recursively in subrecipes.
12. Recipe calculation does not round for purchasing.
13. Central recipes are not edited in place by tenants or camps; tenant recipes are not edited in place by camps.
14. No automatic central synchronization or semantic merge.
15. Archived historical references remain valid.

## Implementation approach

### Phase 1 - Repository analysis

Before changing code:

- locate domain/application/infrastructure/UI boundaries;
- identify existing ID, audit, result/error, permission and persistence patterns;
- identify existing ingredient/unit/conflict APIs;
- identify existing central/local synchronization abstractions;
- identify test conventions.

Document any conflict between the existing architecture and this specification before making a substantial workaround.

### Phase 2 - Domain model

Implement domain types/enums/value objects for at least:

- RecipeStatus
- RecipeType
- ScalingMode
- AgeGroupScalingMode
- recipe draft aggregate
- ingredient group
- ingredient position
- ingredient replacement rule
- subrecipe position
- replacement recipe rule
- immutable recipe revision/snapshot
- structured validation result
- conflict reference abstraction in application/domain code

Keep persistence-specific details out of core domain types where architecture allows.

### Phase 3 - Persistence and migrations

Implement migrations according to the persistence document and repository conventions.

Prefer immutable revision snapshots with schema versioning unless the existing architecture has a strong established normalized-snapshot pattern.

Add all feasible DB constraints, but do not force cross-aggregate business rules into fragile SQL when service/domain validation is clearer.

### Phase 4 - Draft CRUD and concurrency

Implement explicit draft save with optimistic concurrency.

Requirements:

- no autosave assumption in backend semantics;
- `draft_version` checked on update;
- return enough data on conflict for the UI/application layer to build a field-level comparison;
- lightweight edit-presence API/state may be implemented separately and must not act as a hard lock.

### Phase 5 - Validation/publication

Implement deterministic publication validation.

Return structured errors and warnings with stable codes and context.

Publication flow must be transactional and create an immutable complete snapshot.

Warnings acknowledged during publication are persisted on the revision and remain visible forever.

### Phase 6 - Calculation engine

Implement a pure/testable recipe calculation service that can:

- scale a top-level portion-based recipe;
- honor recipe default and position age-group override;
- calculate linear/fixed/stepwise quantities;
- expand nested portion-based and quantity-based recipes;
- convert units through the existing ingredient/unit domain;
- evaluate replacement ingredients and replacement recipes when instructed by conflict context;
- propagate remaining/new conflicts with provenance;
- preserve exact decimal quantities;
- guard against cycles defensively.

Do not implement purchasing rounding.

Do not invent participant-level conflict resolution workflows; expose evaluation data to later layers.

### Phase 7 - lifecycle/actions

Implement application commands/use cases for at least:

- create recipe draft
- save recipe draft
- activate/publish first revision
- publish new revision
- restore revision into draft
- archive
- reactivate
- reset active recipe to draft only when safe
- duplicate recipe
- create derived/local copy from a revision
- permanent delete with super-admin/reference checks

### Phase 8 - central/tenant/camp behavior

Implement or prepare domain/application support for:

- the explicit `central -> tenant -> camp` distribution flow
- tenant and camp references to exact upstream revisions
- explicit conversion to an independently editable tenant or camp draft
- central update check (single + bulk query capability)
- explicit central revision adoption
- local revision submission as central change proposal
- three-way comparison metadata
- no automatic merge

If the central distribution layer is outside the current implementation slice, create clean interfaces and tests but do not build unrelated infrastructure.

### Phase 9 - local notes

Implement camp-level recipe notes separately from recipe revision content.

They are:

- multiple
- editable
- visible camp-wide subject to permissions
- retained across central-revision changes
- not submitted centrally

### Phase 10 - permissions

Use the existing authorization system and define granular permissions corresponding to:

- read
- edit
- publish
- archive/reactivate
- reset-to-draft
- manage local notes
- submit central change
- review central change
- permanent delete

Do not introduce fixed role names in the recipe feature.

## Tests required

Add unit/integration tests for at least:

### Reference models

- valid/invalid portion recipe
- valid/invalid quantity recipe
- mutual subrecipe nesting
- unit conversion for quantity-based subrecipe

### Ingredient uniqueness

- same ingredient same group rejected
- same ingredient different groups allowed
- same ingredient twice ungrouped rejected

### Replacement rules

- multiple conflicts on one rule allowed
- same conflict on two rules at same position rejected
- unresolved replacement conflict produces warning, not error
- replacement-created conflicts propagate

### Subrecipes

- direct cycle rejected
- indirect cycle rejected
- same subrecipe same group rejected
- same subrecipe different groups allowed
- archived referenced revision remains usable with warning

### Scaling

- linear
- fixed
- stepwise with ceil semantics
- age-group apply/inherit/ignore
- no recursive age factor on subrecipe
- exact decimal output/no purchasing rounding

### Lifecycle

- draft may be incomplete
- publish blocked by errors
- publish allowed after warnings acknowledged
- historical warnings persist
- active -> draft only when no external references
- used active recipe cannot return to draft
- archived recipe cannot publish new revision
- archived -> active preserves revision history

### Revision immutability

- published snapshot cannot be modified
- restore copies into draft without rewriting history
- nested revision references never float to latest

### Concurrency

- stale draft version update rejected
- non-stale update accepted

### Central/local

- central recipe cannot be edited in place by tenant or camp
- tenant recipe cannot be edited in place by camp
- edit creates scope-appropriate lineage
- explicit central update adoption
- no automatic synchronization

## UI expectations if UI is in scope

Keep UI behavior aligned with backend semantics:

- explicit Save button
- show 'being edited' presence as warning only
- surface stale-draft conflicts with field-level differences
- separate errors from warnings
- require warning acknowledgement before publish
- show revision history and historical warnings
- show archived-reference warnings without invalidating historical usage
- provide ingredient groups and mixed position ordering
- do not expose unsupported free-text/optional ingredient concepts

## Out of scope

Do not implement as part of this feature unless already required by the repository slice:

- menu-plan business logic
- cooking-unit participant assignment
- organizational resolution of unresolved conflicts
- purchasing/package rounding
- inventory reservation
- recipe images/files
- translations
- arbitrary scaling formulas
- automatic Git-like branching/merging
- automatic central synchronization

## Deliverable expectations

When implementation is complete, provide:

1. concise architecture summary of what was added;
2. migrations/schema changes;
3. public interfaces/use cases;
4. validation/warning codes;
5. calculation behavior;
6. tests added and test results;
7. any deliberate deviations from this specification and why;
8. remaining integration points for menu plan/cooking unit.

Do not silently simplify an invariant because implementation is inconvenient. If an invariant conflicts with existing architecture, make the smallest compatible design and document the trade-off.
