# ScoutCampPlanner - Recipe Validation and Calculation Rules

## 1. Validation levels

All publication validation returns structured results with severity:

- `error`: blocks activation/publication
- `warning`: publication allowed only after explicit acknowledgement

Draft saves do not require publication-valid state.

Warnings remain visible on the published revision as historical publication warnings.

## 2. Publication errors

At minimum, reject publication for the following conditions.

### Recipe identity/reference

- missing name
- normalized duplicate name in recipe scope
- invalid recipe type
- portion-based recipe without `reference_servings > 0`
- portion-based recipe without reference age group
- quantity-based recipe without `reference_quantity > 0`
- quantity-based recipe without valid reference unit

### Structure

- recipe has zero positions
- any ingredient group is empty
- group name missing
- invalid sort order representation

### Ingredient position

- missing base ingredient
- quantity <= 0
- unit not available/convertible for base ingredient
- duplicate base ingredient in same group or ungrouped area
- unsupported scaling mode
- invalid stepwise scaling parameters
- invalid age-group-scaling override

### Ingredient replacement

- missing replacement base ingredient
- replacement quantity <= 0
- replacement unit invalid for replacement ingredient
- replacement rule has no conflict references
- same individual conflict reference assigned to multiple replacement rules of the same ingredient position

### Subrecipe position

- target is not a published immutable recipe revision
- quantity basis does not match referenced recipe type
- quantity unit not convertible to quantity-based target reference unit
- referenced portion-based recipe has missing/invalid serving demand
- recipe cycle detected
- duplicate same target revision in same group or ungrouped area

### Replacement recipe

- replacement target is not a published recipe revision
- replacement recipe type differs from original referenced recipe type
- invalid replacement servings/quantity/unit
- no conflict references
- same individual conflict reference assigned to multiple replacement rules of one subrecipe position
- replacement introduces a cycle

## 3. Publication warnings

Warnings do not block publication after acknowledgement.

Recommended warnings include:

- declared replacement ingredient still carries the declared allergen/intolerance/dietary conflict
- declared replacement recipe still carries one or more declared conflicts
- replacement creates additional conflicts
- recipe exposes conflicts for which no replacement rule exists
- archived recipe revision is newly referenced as subrecipe/replacement recipe
- missing description
- missing source
- stepwise scaling is syntactically valid but produces suspicious discontinuities

Do not treat 'recipe is not conflict-free' as an error.

## 4. Warning snapshots

At publication, persist warnings as immutable revision-scoped snapshots.

Suggested fields:

- revision_id
- warning_code
- message
- structured context JSON
- affected_position_snapshot_id or stable snapshot-local identifier
- optional affected replacement-rule identifier
- optional conflict type/id
- acknowledged_by
- acknowledged_at

Later changes to ingredients or conflict catalogs must not mutate historical warning records.

A separate current revalidation may display newly computed warnings, but must distinguish them from historical publication warnings.

## 5. Top-level age-group calculation

Age-group scaling is resolved outside the recipe domain from the cooking-unit/menu-plan context.

The recipe engine receives a top-level demand. For a portion-based recipe, conceptually:

`effective_target_servings = participant_count * context_age_group_factor`

However, ingredient positions with `ignore` must be calculated against direct participant-equivalent demand rather than the age-adjusted serving demand.

The caller should therefore supply enough top-level context to calculate both:

- age-adjusted serving demand
- direct participant demand

Do not recursively apply age-group factors inside subrecipes.

## 6. Ingredient position calculation

For each top-level ingredient position, determine whether age-group scaling applies:

- `inherit` -> recipe default
- `apply` -> apply age-group factor
- `ignore` -> use direct participant demand

Then calculate the demand ratio against recipe reference servings and apply the quantity scaling mode.

For nested recipes, only the already-calculated parent demand ratio propagates; no additional age factor is introduced.

## 7. Scaling modes

### Linear

`result = reference_quantity * demand_ratio`

### Fixed

`result = reference_quantity`

### Stepwise

Represent explicitly, e.g.:

- `step_size`: size of demand block
- `quantity_per_step`: quantity required per block
- `rounding`: currently fixed to `ceil` for 'per started block'

Conceptually:

`result = ceil(target_demand / step_size) * quantity_per_step`

Avoid executable formulas or user-authored expressions.

## 8. Subrecipe scaling

### Portion-based referenced recipe

`subrecipe_ratio = required_subrecipe_servings / referenced_revision.reference_servings`

### Quantity-based referenced recipe

Convert requested quantity to the referenced revision's reference unit, then:

`subrecipe_ratio = converted_required_quantity / referenced_revision.reference_quantity`

Apply that ratio to the referenced snapshot recursively.

Nested ratios multiply naturally through recursion.

## 9. Replacement quantities

Replacement ingredient rules contain a concrete reference replacement quantity corresponding to the original recipe position at reference scale.

When the replacement is selected, calculate the replacement quantity using the same demand basis and position scaling semantics as the original position.

Replacement recipes similarly use their declared replacement serving/quantity requirement and are then recursively calculated using the replacement revision's own reference basis.

## 10. Conflict propagation

The recipe evaluation engine should produce a collected conflict result tree or flattened set with provenance.

Rules:

1. Base ingredient/variant domain reports conflict properties.
2. Ingredient replacement can be selected for specific conflicts.
3. Selected replacement is evaluated again; never assume its declared conflicts are solved.
4. Nested subrecipe conflicts propagate upward.
5. Replacement subrecipes are fully reevaluated and propagate remaining/new conflicts.
6. Different replacement rules are not automatically composed to solve a multi-conflict combination.
7. Unresolved conflicts are valid evaluation results.

The later cooking-unit layer decides what to do with unresolved conflicts.

## 11. Exact quantities

Do not round recipe calculation results for purchasing convenience.

Persist/return sufficient decimal precision for deterministic downstream aggregation.

Procurement packaging and whole-unit rounding are later concerns.

## 12. Cycle validation

Cycle validation must traverse published subrecipe references and replacement-recipe references that would create a dependency edge.

Before publishing a draft recipe revision, verify that adding the proposed edges cannot create a path back to the current recipe.

Prefer deterministic graph validation at publish time and defensive cycle guards at evaluation runtime.

## 13. Historical stability

Published revisions are immutable and all external references target a revision ID.

Never resolve a historical subrecipe reference to 'latest revision'.

Never mutate historical quantities/warnings because a base ingredient or central recipe later changes. If current catalog semantics are also needed, expose them as a separate re-evaluation view.
