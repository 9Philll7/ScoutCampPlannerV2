# Tenant and Role Model

## Tenant

A tenant represents an organisation/group.

A tenant owns:
- users
- camps
- local data context

A shared camp between multiple groups should use its own tenant.

## Roles

Roles are documented bundles of stable permissions as defined by [ADR-011](../decisions/adr-011-roles-and-permissions.md).

Initial tenant roles:

- `TenantOwner`
- `TenantAdmin`
- `TenantMember`
- `TenantAuditor`

Initial camp roles:

- `CampAdmin`
- `CampEditor`
- `CampViewer`

Tenant roles do not automatically grant camp-content access. Every camp access requires an explicit camp membership. No initial role automatically grants access to future health data.

The catalogue can grow with implemented modules. New roles and permissions must document their scope, assignment authority, offline behavior, and sensitive-data impact.

Initial tenant permissions:

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

Initial camp permissions:

- `camp.view`
- `camp.edit`
- `camp.members.view`
- `camp.members.manage`
- `camp.offline-access.prepare`
- `camp.package.export`
- `camp.package.import`
- `camp.audit.view`

## Authentication modes

Authentication modes are defined by [ADR-009](../decisions/adr-009-authentication-modes.md):

- Cloud users authenticate with a normal password.
- A connected local server instance uses cloud authentication.
- Authorized users can prepare an independent local verifier for offline login by entering the same password while connected.
- Central password verifiers and plaintext passwords are never transferred to the local instance.
- A single-device password is optional and remains independent of cloud credentials.

Authentication does not grant access by itself. Tenant membership, camp assignment, roles, and permissions must be evaluated separately. ADR-011 defines the initial catalogue; module-specific and sensitive-data permissions are added only with their associated domain decisions.

## Identity and membership storage

[ADR-010](../decisions/adr-010-identity-and-tenant-membership.md) separates:

- the global Platform-owned user identity
- tenant membership
- future camp membership
- role and permission assignments
- central and local technical password verifiers

A user may belong to multiple tenants. Authentication establishes the stable user identity only; access requires an active membership in the requested tenant and, where applicable, the requested camp.

Prepared offline access stores effective permission identifiers and a role-definition version rather than relying only on role names.

Security-sensitive identity, membership, role, authentication, offline-access, and package operations emit the audit events defined by [ADR-012](../decisions/adr-012-security-audit-events.md). Audit records contain stable identifiers and result codes, not credentials or domain payloads.

## Tenant isolation

ADR-010 requires an explicit tenant context for every tenant-scoped request and background process.

- Route or resource ownership identifies the requested tenant.
- The backend validates an active membership on every request.
- Tenant-owned aggregate roots retain their tenant ID.
- Queries are restricted before data is materialized.
- Cross-module tenant checks use contracts and do not bypass module Infrastructure.
- Unauthorized foreign resources are not disclosed.

PostgreSQL schemas separate modules, not tenants. Tenant isolation is enforced consistently in application behavior and persistence queries for PostgreSQL and SQLite.
