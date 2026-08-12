# ADR-011 Roles and Permissions

## Status

Accepted

## Context

ADR-010 separates global identities, tenant memberships, camp memberships, and credential storage. ScoutCampPlanner now needs an initial authorization model that is understandable for administrators but can grow as Catering, Finance, Program, Material, participant, and health-data functionality is implemented.

A fixed set of role-name checks spread through the code would be difficult to extend and unsafe for offline authorization snapshots. Fully user-defined roles would add administration and migration complexity before the required permissions are known.

## Decision

### Authorization principle

Roles are named, documented bundles of stable permissions.

- Backend authorization evaluates permissions in an explicit tenant and camp context.
- UI role checks are presentation only and never replace backend authorization.
- Application and Domain policies do not scatter string comparisons against role names.
- Role definitions are maintained centrally in Platform and covered by authorization tests.
- Custom user-defined roles are not supported initially.

### Initial tenant roles

#### `TenantOwner`

- owns the tenant
- manages tenant administrators and members
- transfers ownership
- manages camp assignments
- cannot remove or demote the last active `TenantOwner`

Tenant ownership does not automatically grant access to camp content or future health data.

#### `TenantAdmin`

- manages tenant members
- creates and administers camp registrations and camp assignments
- cannot transfer ownership
- cannot remove or demote the last active `TenantOwner`

Tenant administration does not automatically grant access to camp content or future health data.

#### `TenantMember`

- establishes active membership in the tenant
- grants no camp access by itself

#### `TenantAuditor`

- views tenant-scoped security audit events
- exports authorized tenant-scoped audit events
- does not modify users, memberships, roles, camps, or domain data
- cannot create, change, or release a legal hold

`TenantAuditor` is an additional tenant role and can be assigned alongside another tenant role.

### Tenant role assignment shape

Every active tenant membership has exactly one base role:

- `TenantOwner`
- `TenantAdmin`
- `TenantMember`

The base roles are mutually exclusive. Redundant combinations such as `TenantOwner` plus `TenantAdmin` are not allowed. `TenantAuditor` is an optional additional role and may be combined with any one base role.

Suspending a membership retains all role assignments but they grant no permissions while the membership is suspended. Removing a membership permanently ends authorization while retaining its final role assignments as historical security context. A removed membership cannot receive or change role assignments.

### Tenant ownership transfer

Ownership transfer is one atomic application operation and database transaction:

1. The target membership must be `Active` in the same tenant.
2. The target membership becomes `TenantOwner`.
3. The previous owner becomes `TenantAdmin` in the same transaction.
4. The transaction may commit only after the tenant still has at least one active owner.

Directly demoting, suspending, or removing the last active owner is forbidden. When multiple active owners exist, one may be demoted to `TenantAdmin`, suspended, or removed only if at least one other active owner remains. Concurrent ownership changes must preserve the same invariant through transactional persistence and concurrency handling.

### Initial camp roles

#### `CampAdmin`

- manages general camp data
- manages camp members and camp roles
- prepares authorized offline access
- starts camp-package export and return import
- does not automatically grant access to future health data

#### `CampEditor`

- reads and edits general, non-sensitive camp data
- cannot manage users, roles, offline access, export, or return import

#### `CampViewer`

- reads general, non-sensitive camp data
- cannot modify camp data

### Initial permission catalogue

Permission identifiers are stable technical contracts.

Tenant permissions:

- `tenant.view`
- `tenant.settings.manage`
- `tenant.members.view`
- `tenant.members.manage`
- `tenant.ownership.transfer`
- `tenant.camps.create`
- `tenant.camps.assign-members`
- `tenant.audit.view`
- `tenant.audit.export`
- `tenant.audit.legal-hold.manage`

Camp permissions:

- `camp.view`
- `camp.edit`
- `camp.members.view`
- `camp.members.manage`
- `camp.offline-access.prepare`
- `camp.package.export`
- `camp.package.import`
- `camp.audit.view`

Initial role mappings:

| Role | Permissions |
|---|---|
| `TenantOwner` | all initial tenant permissions |
| `TenantAdmin` | tenant permissions except `tenant.ownership.transfer` and all `tenant.audit.*` permissions |
| `TenantMember` | `tenant.view` |
| `TenantAuditor` | `tenant.audit.view`, `tenant.audit.export` |
| `CampAdmin` | all initial camp permissions, including camp-scoped audit view |
| `CampEditor` | `camp.view`, `camp.edit` |
| `CampViewer` | `camp.view` |

Package export and import remain distinct permissions because they expose different operations and risks. Preparing offline access is independent of package transfer and camp-membership administration.

Audit export and legal-hold administration require recent password confirmation. `CampAdmin` sees only events scoped to an assigned camp and never receives tenant-wide sign-in, password, account, or security-state events through `camp.audit.view`.

Permission identifiers are not renamed or reused silently. A change requires compatibility handling for stored assignments and offline authorization snapshots.

### Explicit camp membership

Every access to camp content requires an active camp membership. Tenant ownership or administration alone is insufficient. Tenant administrators may manage the assignment without thereby receiving content access.

Sensitive health data will require a separate explicit permission and decision before implementation. It is never implied by `TenantOwner`, `TenantAdmin`, or `CampAdmin`.

### Extensibility

The initial catalogue is intentionally not complete.

- New modules may add permissions and convenient role bundles when their use cases are defined.
- Module permissions use a module-specific prefix such as future `catering.*` identifiers.
- Future health-data permissions use an explicit `health.*` prefix and are never added automatically to an existing role.
- New roles require documentation of scope, included permissions, assignment authority, offline behavior, and sensitive-data impact.
- Existing roles may gain a new permission only when that is consistent with their documented responsibility and does not silently grant access to sensitive data.
- Removing or narrowing a permission requires compatibility and offline-snapshot analysis.
- A role change that materially alters trust boundaries requires an ADR update or a new ADR.

Role persistence stores only the current assignment state. A separate role-history table is not introduced. Role assignment, removal, ownership transfer, and attempted last-owner violations are recorded through the append-only security audit defined by ADR-012, which is the authoritative history.

Current tenant roles are stored in a separate `TenantRoleAssignments` relation rather than fixed columns on the membership. The membership-and-role combination is unique. A provider-specific filtered unique index permits at most one of the three base-role identifiers per membership, while allowing the additional `TenantAuditor` role. Application validation requires exactly one known tenant base role, at most one auditor assignment, no camp-scoped or unknown roles, and no role changes on removed memberships. A temporarily incomplete set may exist only inside a transaction and must never be used as an authorization result or committed by a role-management use case.

Production role-changing endpoints must not be enabled before the required audit persistence can commit the role change and its audit event in the same transaction. This restriction does not prevent implementing and testing the role-assignment persistence model beforehand.

### Offline authorization snapshot

Prepared offline access stores the effective, stable permission identifiers plus a role-definition version, not only role names.

- The snapshot is limited to the tenant and camps prepared for that local instance.
- The snapshot remains authoritative during the disconnected phase.
- Role or permission changes in the cloud take effect locally after the next successful security-state refresh.
- Local role changes are not returned to the cloud.

## Consequences

- The first implementation needs the documented central permission catalogue and role-to-permission mapping in Platform.
- Tenant and camp assignment use cases must preserve the last-owner invariant.
- Authorization tests must cover every role, permission, scope, and denial across at least two tenants and camps.
- Offline tests must verify snapshot scope, versioning, refresh, and denial of permissions not present in the snapshot.
- Module-specific and health-data permissions remain unavailable until their associated domain and security decisions are made.
