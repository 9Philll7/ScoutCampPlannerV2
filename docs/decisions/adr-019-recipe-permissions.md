# ADR-019: Recipe permissions

## Status

Accepted

## Context

ADR-011 defines extensible, scope-bound roles and stable permission identifiers. ADR-017 adds the platform scope for the central recipe catalog. The recipe lifecycle, tenant library, camp library, local camp notes and central change submissions now require explicit tenant and camp permissions. Authorization must not be implemented through recipe-specific role-name checks.

## Decision

The following stable recipe permissions are added to authorization catalogue version 3:

- `recipes.read`
- `recipes.edit`
- `recipes.publish`
- `recipes.archive`
- `recipes.reset-to-draft`
- `recipes.library.manage`
- `recipes.notes.manage`
- `recipes.central.changes.submit`

Permissions are evaluated in an explicit tenant or camp scope. The same technical identifier may occur in both scopes, but a grant in one scope never grants access in the other.

Initial tenant mapping:

| Permission | TenantOwner | TenantAdmin | TenantMember |
|---|---:|---:|---:|
| Read | yes | yes | yes |
| Edit | yes | yes | no |
| Publish | yes | yes | no |
| Archive/reactivate | yes | yes | no |
| Reset to draft | yes | yes | no |
| Manage library | yes | yes | no |
| Submit central change | yes | yes | no |

Initial camp mapping:

| Permission | CampAdmin | CampEditor | CampViewer |
|---|---:|---:|---:|
| Read | yes | yes | yes |
| Edit | yes | yes | no |
| Publish | yes | no | no |
| Archive/reactivate | yes | no | no |
| Reset to draft | yes | no | no |
| Manage library | yes | no | no |
| Manage camp notes | yes | yes | no |
| Submit central change | yes | no | no |

The existing platform permissions remain unchanged:

- `recipes.central.read`
- `recipes.central.changes.review`
- `recipes.central.delete`

`PlatformAdmin` receives these platform permissions through the platform scope. Tenant and camp roles receive no implicit platform permission.

## Consequences

- Recipe application and API code checks stable permissions, never role names.
- `CampEditor` may edit recipe drafts and camp notes but cannot publish or perform lifecycle actions.
- Existing role assignments remain valid; their effective permission snapshot uses role-definition version 3.
- Offline authorization snapshots must be refreshed or migrated before relying on the new permissions.
- Health-data access is not implied by any recipe permission.
