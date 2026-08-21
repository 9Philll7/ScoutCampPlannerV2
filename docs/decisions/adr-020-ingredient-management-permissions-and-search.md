# ADR-020: Ingredient management permissions and search priority

## Status

Accepted

## Context

ADR-018 defines central, tenant, and camp ownership for base ingredients but does not assign mutation permissions or define how overlapping catalogs are searched. Ingredient creation must not be granted implicitly through read access, and central master data requires a platform-wide boundary.

## Decision

Authorization catalogue version 4 adds these stable permissions:

- `ingredients.central.manage` in platform scope;
- `ingredients.manage` in tenant and camp scope.

The initial role mapping is:

- `PlatformAdmin` manages central ingredients;
- `TenantOwner` and `TenantAdmin` manage ingredients of their tenant;
- `CampAdmin` and `CampEditor` manage ingredients of their camp;
- read-only roles cannot mutate ingredients.

Ingredient application code checks permissions and scopes, never role names.

Search in camp context prioritizes the most specific usable catalog. Matching camp ingredients are shown first, followed by matching tenant ingredients. Central matches are returned only when neither camp nor tenant contains a match. Without a search term, the complete visible catalog remains available and is grouped by scope.

The initial editor in the camp view creates camp-owned ingredients. Tenant ingredients are managed from the organization view and central ingredients from a later platform-administration view.

## Consequences

- Existing role assignments remain valid and resolve against role-definition version 4.
- A camp editor may create camp ingredients but cannot modify tenant or central ingredients.
- Search fallback reduces central duplicates in everyday use without hiding the full catalog outside an active search.
- Units and conflict catalogs remain platform-wide as decided by ADR-018; their management UI is a separate slice.
