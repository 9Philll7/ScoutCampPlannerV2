# Recipe implementation analysis

## Status

Repository analysis completed before recipe implementation. This document records missing prerequisites and prevents temporary implementation choices from becoming implicit architecture decisions.

## Existing foundations that can be reused

- Catering owns food factors, meal labels, meal-day activation and its own `DbContext`.
- Camp exposes camp and tenant boundaries through contracts and API-level permission checks.
- Platform provides tenant/camp memberships, permission resolution and transactional audit operations.
- PostgreSQL and SQLite use separate provider-specific migrations per module.
- Camp packages atomically replace Camp and Catering payloads and can carry immutable external recipe revisions required offline.
- Existing domain projects remain independent of ASP.NET Core and Entity Framework.

## Missing recipe prerequisites

### Ingredient and conflict domain (resolved)

No implemented types or persistence exist for:

- base ingredients and variants
- units and ingredient-specific conversions
- allergens
- intolerances
- dietary requirements
- conflict evaluation

ADR-018 resolves their ownership: units and conflict catalogs are platform-wide, while base ingredients use central, tenant, and camp scopes. Ingredient variants and ingredient-specific conversions remain owned by their base ingredient. Recipe implementation must not replace these catalogs with free-text identifiers or recipe-owned catalogs.

### Reference age groups (resolved)

The current Camp stages are configurable tenant templates with stable camp copies and cannot identify central recipe semantics. Published revisions therefore always use a platform-wide standard portion with factor `1.0`. Tenant/camp drafts may retain a complete authoring-stage snapshot, but publication normalizes reference servings before creating the immutable revision.

### Platform-wide authorization

Authorization supports platform, tenant and camp scopes. ADR-017 defines platform-wide permissions for reading the central catalog, reviewing central submissions and permanent cleanup. ADR-019 defines the tenant- and camp-scoped recipe permissions and their initial role mappings. Recipe code checks permissions independently of role names.

### Application-layer boundary (resolved)

`ScoutCampPlanner.Catering.Application` now owns recipe publication validation and its domain-facing reference interfaces. Further recipe use cases, optimistic concurrency, calculation, and central/tenant/camp adoption remain in this project; HTTP mapping stays in the API composition project.

### Central distribution infrastructure

The local central-distribution foundation is implemented: exact upstream revision references, explicit update checks and adoption, editable tenant/camp derivations, and central change submissions with a three-way comparison are persisted. Authorized platform reviewers can reject a pending submission or accept it as a newly validated immutable central revision. Acceptance also aligns the mutable central draft and commits the draft replacement, publication, audit fields, and submission status in one database transaction. A central copy discards any tenant/camp authoring-stage label and retains the normalized standard portion.

No remote catalog synchronization service or proposal-review UI exists. Distribution between separate installations and the visual review workflow remain later explicit slices; automatic synchronization and semantic merging are not introduced.

### Local camp notes

Camp recipe entries own multiple independently editable local notes. Notes retain creation and update audit data, use soft deletion, survive explicit upstream-revision adoption, and are neither revisioned nor included in central change submissions. Reading requires camp recipe-read access; mutations require the separate camp-scoped `recipes.notes.manage` permission. HTTP endpoints and UI remain a later integration slice.

## Proposed implementation boundary

1. Implement Catering-owned ingredient, unit and conflict catalogs with application contracts.
2. Introduce `ScoutCampPlanner.Catering.Application`.
3. Implement mutable recipe drafts and immutable revisions for `central`, `tenant` and `camp` scopes.
4. Add publication validation and exact-decimal calculation against the ingredient contracts.
5. Add central distribution interfaces first; implement remote catalog infrastructure separately.

No recipe persistence migration should be created before the unresolved age-group and platform-authorization decisions are recorded.
