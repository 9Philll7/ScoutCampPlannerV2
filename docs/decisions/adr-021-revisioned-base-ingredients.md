# ADR-021: Revisioned base ingredients and selectable variants

## Status

Accepted

## Context

ADR-018 defines central, tenant, and camp ingredient catalogs. The first implementation stores mutable ingredient data directly on the ingredient identity and treats variants as implicitly interchangeable. This is insufficient for immutable recipe references, auditable master-data changes, local adaptations of central ingredients, and reliable allergen and dietary evaluation.

## Decision

Base ingredients retain the scopes `central`, `tenant`, and `camp`. The term *local* is only a collective term for tenant- and camp-owned data and is not a persisted scope.

An ingredient has a stable identity. All mutable professional content is stored in revisions. Revisions have the states `draft` and `published`; published revisions are immutable. Archiving applies to the stable ingredient identity and does not mutate historical revisions.

Tenant and camp ingredients can be independent entries or forks of a central ingredient. Merely referencing a central ingredient creates no fork. A fork records the central source ingredient and the central revision on which it is based. Central updates never overwrite a local fork. Their adoption uses a three-way comparison and creates a new local draft.

Every saved draft requires a name, category, and base unit. Property assignments, variants, and conversions may still be incomplete. Publishing requires a consistent, sufficiently reviewed revision.

Ingredient variants belong to a revision and retain a stable `variant_key` across revisions. A recipe ingredient position references a concrete published ingredient revision and may additionally select a `variant_key`. Without a variant key, the base form is used.

Allergens, intolerance triggers, and origin properties remain platform-wide catalogs. Vegan, vegetarian, pescetarian, lactose-free, milk-free, and gluten-free suitability is calculated from revisioned properties instead of being stored as direct ingredient assignments. Stable dietary-requirement identifiers may still be used for participant requirements and recipe rules.

Property groups record whether they have been professionally reviewed. Missing values in an unreviewed group evaluate to `unknown`. A reviewed group may treat omitted entries as not present only according to the explicitly documented evaluation rules. Contradictory parent and child allergen states prevent publication.

Persistence is implemented through one provider-independent EF Core model and separate PostgreSQL and SQLite migrations. The PostgreSQL SQL document is a reference schema, not the migration source of truth.

Existing ingredient identities remain stable during migration. Their mutable fields and assignments are transferred into an initial published revision. Existing recipe references must be migrated to that revision; published recipe snapshots remain immutable.

## Consequences

- ADR-018's three ownership scopes remain in force.
- The earlier statement that recipes never select variants is superseded.
- Direct ingredient-to-dietary-requirement assignments are replaced by calculated suitability.
- Ingredient publication, fork/merge behavior, recipe references, offline packages, and both database providers require coordinated migrations and tests.
- Large-scale ingredient data entry should start only after this model and its migration path are implemented.

