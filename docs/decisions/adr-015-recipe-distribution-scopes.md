# ADR-015: Recipe distribution scopes

## Status

Accepted

## Context

ScoutCampPlanner needs a curated recipe catalog shared across tenants, reusable tenant-owned recipe libraries, and stable camp-local recipe data for offline operation. A two-level `central`/`camp` model would skip tenant ownership and would not support organization-wide reuse without coupling camps directly to the shared catalog.

Published recipe revisions are immutable. Existing offline rules prohibit automatic synchronization and silent replacement of locally controlled data.

## Decision

Recipes support exactly three ownership scopes:

- `central`: curated, tenant-independent catalog
- `tenant`: reusable library owned by one tenant
- `camp`: recipe owned by one camp

Distribution follows `central -> tenant -> camp`. Adoption is explicit and always references a concrete immutable revision. Editing upstream content creates an independent draft in the receiving scope and records lineage to the source recipe and revision. Upstream updates are checked for and adopted explicitly; there is no automatic synchronization or semantic merge.

Tenant or camp revisions may be submitted as central change proposals. Publication in the central catalog requires explicit review and acceptance by a platform administrator.

## Consequences

- Recipe names are unique only inside their concrete scope.
- Tenant and camp scopes require an owner ID; central recipes have no tenant or camp owner.
- Authorization must distinguish central review, tenant library management, and camp recipe management.
- Offline packages contain the camp-owned recipes and exact external revisions required by the camp; they do not replace central or tenant libraries on return import.
- Lineage and library-entry persistence must represent the source scope and exact source revision.
- Central, tenant, and camp recipes cannot be edited in place from a downstream scope.
