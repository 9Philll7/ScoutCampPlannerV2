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

### Ingredient and conflict domain

No implemented types or persistence exist for:

- base ingredients and variants
- units and ingredient-specific conversions
- allergens
- intolerances
- dietary requirements
- conflict evaluation

The recipe brief requires these to remain authoritative outside individual recipe positions. Recipe implementation must not replace them with free-text identifiers or recipe-owned catalogs.

### Reference age groups (resolved)

The current Camp stages are configurable tenant templates with stable camp copies and cannot identify central recipe semantics. Published revisions therefore always use a platform-wide standard portion with factor `1.0`. Tenant/camp drafts may retain a complete authoring-stage snapshot, but publication normalizes reference servings before creating the immutable revision.

### Platform-wide authorization

Authorization currently supports only tenant and camp scopes. The central recipe catalog requires platform-wide permissions for reading published catalog data, reviewing central submissions and permanent cleanup. Recipe code must define permissions independently of role names, but the owning platform scope and assignment mechanism still need an architecture decision.

### Application-layer boundary

Catering currently has Domain and Infrastructure projects while product use cases live in the API composition project. The recipe brief introduces substantial application behavior: optimistic concurrency, publication, validation, calculation and central/tenant/camp adoption. Continuing to place this logic in the API project would conflict with the documented application-layer responsibility. The first recipe use case should introduce `ScoutCampPlanner.Catering.Application` and keep HTTP mapping in the API project.

### Central distribution infrastructure

No central catalog synchronization service exists. The initial implementation can provide domain/application interfaces, exact revision references and package support without inventing automatic synchronization. Remote distribution and proposal review UI should remain a later explicit slice.

## Proposed implementation boundary

1. Implement Catering-owned ingredient, unit and conflict catalogs with application contracts.
2. Introduce `ScoutCampPlanner.Catering.Application`.
3. Implement mutable recipe drafts and immutable revisions for `central`, `tenant` and `camp` scopes.
4. Add publication validation and exact-decimal calculation against the ingredient contracts.
5. Add central distribution interfaces first; implement remote catalog infrastructure separately.

No recipe persistence migration should be created before the unresolved age-group and platform-authorization decisions are recorded.
