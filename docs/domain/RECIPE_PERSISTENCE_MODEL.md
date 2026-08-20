# ScoutCampPlanner - Recipe Persistence Model

## Purpose

This document proposes a relational persistence model. Names are illustrative and should be adapted to the repository's existing naming conventions and database technology.

The important requirement is the separation of mutable recipe drafts from immutable published revision snapshots.

## 1. Mutable recipe aggregate

### recipes

Suggested columns:

- `id` UUID PK
- `scope_type` enum(`central`,`tenant`,`camp`)
- `scope_id` nullable UUID (required for tenant and camp scope)
- `name`
- `normalized_name`
- `status` enum(`draft`,`active`,`archived`)
- `recipe_type` enum(`portion_based`,`quantity_based`)
- `description` nullable text
- `source` nullable text
- `internal_notes` nullable text
- `reference_servings` nullable decimal/integer
- `reference_portion_kind` fixed to the platform standard portion for published semantics
- `authoring_stage_id` nullable (tenant/camp draft only)
- `authoring_stage_name` nullable snapshot
- `authoring_stage_factor` nullable positive decimal snapshot
- `reference_quantity` nullable decimal
- `reference_unit_id` nullable FK
- `default_age_group_scaling_applies` nullable boolean / defined only for portion recipes
- `draft_version` bigint not null
- `created_by`, `created_at`
- `updated_by`, `updated_at`
- `archived_by`, `archived_at` nullable
- `reactivated_by`, `reactivated_at` nullable
- lineage fields described below

Constraints:

- unique `(scope_type, scope_id-normalized, normalized_name)` using DB-appropriate handling for central null scope
- `central` requires a null scope ID; `tenant` and `camp` require the ID of their owning scope
- portion-based fields XOR quantity-based fields
- positive references
- central drafts cannot have authoring-stage metadata
- tenant/camp authoring-stage metadata is either complete or entirely absent

### Recipe lineage

Recommended optional fields or separate relation:

- `derived_from_recipe_id`
- `derived_from_revision_id`
- `central_source_recipe_id`
- `central_source_revision_id`
- `tenant_source_recipe_id`
- `tenant_source_revision_id`

Do not overload lineage with active upstream-reference state. Prefer separate tenant and camp library/reference entries when architecture supports them.

## 2. Draft tags

### tags

Scope-aware normalized values if tags are shared within scope:

- `id`
- `scope_type`
- `scope_id`
- `name`
- `normalized_name`

### recipe_tags

- `recipe_id`
- `tag_id`

Alternatively store recipe-owned tag values directly if global tag reuse adds little value. Preserve snapshot semantics either way.

## 3. Draft groups

### recipe_ingredient_groups

- `id`
- `recipe_id`
- `name`
- `sort_order`

Empty groups allowed in draft.

## 4. Draft ingredient positions

### recipe_ingredient_positions

- `id`
- `recipe_id`
- `group_id` nullable
- `base_ingredient_id`
- `quantity` decimal
- `unit_id`
- `sort_order`
- `scaling_mode` enum(`linear`,`fixed`,`stepwise`)
- `age_group_scaling` enum(`inherit`,`apply`,`ignore`)
- stepwise parameters, preferably structured columns or one validated JSON value

Recommended stepwise columns:

- `step_size` nullable decimal
- `quantity_per_step` nullable decimal

For `linear` and `fixed`, stepwise fields must be null.

Enforce base-ingredient uniqueness per `(recipe_id, group-or-ungrouped, base_ingredient_id)` with database-specific indexes or application validation.

## 5. Draft ingredient replacements

### recipe_ingredient_replacements

- `id`
- `ingredient_position_id`
- `replacement_base_ingredient_id`
- `replacement_quantity`
- `replacement_unit_id`

Conflict link tables:

### recipe_ingredient_replacement_allergens
- `replacement_id`
- `allergen_id`

### recipe_ingredient_replacement_intolerances
- `replacement_id`
- `intolerance_id`

### recipe_ingredient_replacement_dietary_requirements
- `replacement_id`
- `dietary_requirement_id`

Enforce conflict uniqueness per ingredient position across replacement rules in application/service validation and, if practical, via denormalized uniqueness strategies.

## 6. Draft subrecipe positions

### recipe_subrecipe_positions

- `id`
- `recipe_id`
- `group_id` nullable
- `recipe_revision_id` FK to immutable revision
- `required_servings` nullable
- `required_quantity` nullable
- `required_unit_id` nullable
- `sort_order`

Constraint based on referenced revision type:

- portion-based -> servings only
- quantity-based -> quantity + unit only

This cross-table constraint is generally service-level validation.

## 7. Draft replacement recipes

### recipe_subrecipe_replacements

- `id`
- `subrecipe_position_id`
- `replacement_recipe_revision_id`
- `replacement_servings` nullable
- `replacement_quantity` nullable
- `replacement_unit_id` nullable

Conflict link tables mirror ingredient replacements:

- `recipe_subrecipe_replacement_allergens`
- `recipe_subrecipe_replacement_intolerances`
- `recipe_subrecipe_replacement_dietary_requirements`

## 8. Immutable revisions

Two acceptable implementation strategies exist.

### Preferred initial strategy: snapshot document + indexed metadata

### recipe_revisions

- `id` UUID PK
- `recipe_id`
- `revision_number`
- `published_at`
- `published_by`
- `change_note` nullable
- `snapshot_schema_version`
- `snapshot_json` JSON/JSONB
- optional `restored_from_revision_id`
- optional `central_submission_id`

Unique `(recipe_id, revision_number)`.

The snapshot must contain complete immutable recipe semantics, including stable snapshot-local IDs for groups/positions/rules so warnings can reference them.

Advantages:

- straightforward immutable snapshot creation
- simple historical reproducibility
- avoids duplicating the full mutable table graph for revision tables
- schema can evolve with explicit `snapshot_schema_version`

If repository conventions strongly favor fully normalized history tables, that is acceptable, but immutability and complete reproducibility are non-negotiable.

## 9. Revision warnings

### recipe_revision_warnings

- `id`
- `recipe_revision_id`
- `warning_code`
- `message`
- `context_json`
- `snapshot_position_id` nullable
- `snapshot_replacement_id` nullable
- `conflict_type` nullable
- `conflict_id` nullable
- `acknowledged_by`
- `acknowledged_at`

Warnings are immutable historical publication records.

## 10. Tenant and camp recipe library/reference state

A tenant library entry records either an exact central recipe revision reference or a tenant-owned recipe. It is the explicit boundary between the shared catalog and tenant-controlled data.

### tenant_recipe_entries

Suggested columns:

- `id`
- `tenant_id`
- either `central_recipe_revision_id` OR `tenant_recipe_id`
- lineage/reference metadata required to transition between upstream reference and tenant draft
- `created_at`, `created_by`
- `updated_at`, `updated_by`

A separate local entry is recommended so central-reference state and local notes do not pollute the recipe aggregate.

### camp_recipe_entries

Suggested columns:

- `id`
- `camp_id`
- one exact upstream recipe revision reference OR one camp-local recipe ID
- lineage/reference metadata required to transition between central reference and local draft
- `created_at`, `created_by`
- `updated_at`, `updated_by`

Ensure exactly one active source mode.

This table is the appropriate owner for local camp notes.

## 11. Local notes

### camp_recipe_notes

- `id`
- `camp_recipe_entry_id`
- `text`
- `created_by`
- `created_at`
- `updated_by`
- `updated_at`
- `deleted_by` nullable
- `deleted_at` nullable

Notes are not revisioned recipe content.

## 12. Editing presence

Presence should be ephemeral, ideally not durable business data.

Possible representation:

### recipe_edit_presence

- `recipe_id`
- `user_id`
- `started_at`
- `last_activity_at`

A short-lived cache/distributed presence store is preferable if infrastructure exists.

## 13. Central change submissions

### central_recipe_change_submissions

Suggested fields:

- `id`
- `central_recipe_id`
- `source_central_revision_id`
- `submitted_local_recipe_revision_id`
- `status` enum(`pending`,`accepted`,`rejected`)
- `submitted_by`, `submitted_at`
- `reviewed_by`, `reviewed_at` nullable
- `resulting_central_revision_id` nullable

No automatic merge state is required.

## 14. Snapshot schema recommendation

A revision snapshot should have an explicit schema contract resembling:

```json
{
  "schemaVersion": 1,
  "recipe": {
    "name": "...",
    "description": null,
    "source": null,
    "internalNotes": null,
    "recipeType": "portion_based",
    "reference": {
      "servings": 10,
      "portionKind": "standard",
      "factor": 1.0
    },
    "defaultAgeGroupScalingApplies": true,
    "tags": ["..."],
    "groups": [],
    "positions": []
  }
}
```

For the implemented schema, the published snapshot reference identifies the standard portion with factor `1.0`. An optional authoring-context object may retain the tenant/camp stage ID, name and factor used before normalization, but calculation always uses the normalized `reference.servings` value.

Each group, ingredient position, subrecipe position and replacement rule should contain a stable snapshot-local ID.

Do not embed mutable 'latest' pointers in the snapshot. All nested recipe dependencies must reference exact revision IDs.

## 15. Transaction boundaries

Publishing a recipe revision should be one transaction:

1. lock/verify current draft version as needed;
2. run publication validation;
3. reject errors;
4. require acknowledged warnings;
5. build deterministic snapshot;
6. allocate next revision number;
7. insert immutable revision;
8. insert warning snapshots;
9. update recipe status/metadata;
10. commit.

Central update adoption and reset-to-draft operations should also be transactional.
