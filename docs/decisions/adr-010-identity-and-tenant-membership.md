# ADR-010 Identity and Tenant Membership

## Status

Accepted

## Context

ADR-009 defines password authentication, online and offline authentication modes, sessions, and password recovery. ScoutCampPlanner must now place identity records without coupling Domain assemblies to ASP.NET Core Identity or mixing technical credentials with tenant and camp authorization.

A person may participate in multiple organisations. Local server instances also need explicitly prepared offline access without exporting the central password verifier or changing the camp-package ownership rules from ADR-005.

## Decision

### Identity ownership

User identities belong to the Platform module.

- Every user has a stable, randomly generated `Guid` user ID.
- A confirmed email address is the unique cloud sign-in identifier.
- The original email representation is retained for display.
- A normalized representation is used for lookup and uniqueness.
- One cloud user may be a member of multiple tenants.

### Account and membership separation

The global user account and its tenant memberships are separate records.

- The user account represents identity and account security state.
- Tenant membership grants association with one tenant.
- Future camp membership grants association with one camp inside a tenant.
- Roles and permissions attach to tenant or camp membership, not to the password-verifier record.
- Authentication establishes identity but does not grant tenant or camp access by itself.

The exact role catalogue and permission matrix remain open.

### Tenant isolation

Every tenant-scoped request is evaluated inside an explicit tenant context.

- A tenant ID supplied by request content is never trusted by itself.
- The requested tenant is resolved from the route or owning resource and checked against the authenticated user's active membership.
- A user may switch between tenants in which the user has an active membership. Every request performs its own server-side membership and authorization check.
- Tenant-owned aggregate roots, including Camp, carry an immutable tenant ID.
- Child entities derive tenant ownership through their owning aggregate when they do not store a tenant ID directly.
- Queries are constrained to the authorized tenant or camps before materialization. Frontend filtering is not an authorization control.
- Background jobs and administrative use cases require an explicit tenant context. There is no process-global implicit current tenant.
- Access to an unauthorized resource in another tenant is reported as not found when revealing its existence would disclose information.
- No global super-administrator bypass is introduced initially. A future cross-tenant support role requires a separate architecture and audit decision.

Module boundaries remain in force during tenant checks. For example, Catering verifies camp and tenant ownership through Camp contracts and never reads `CampDbContext` directly.

PostgreSQL schemas remain module-specific rather than tenant-specific. Tenant isolation is enforced by Domain/Application ownership rules, constrained persistence queries, authorization policies, and automated tests.

### Credential storage

The central Argon2id password verifier is stored only in the cloud Platform Infrastructure persistence area.

- Domain assemblies do not reference ASP.NET Core Identity, cookie middleware, token providers, or password-hashing libraries.
- Framework-dependent identity stores and credential services belong to Platform Infrastructure.
- Cookie and endpoint composition belongs to the API application host.
- Domain and Application code work with stable user IDs, memberships, account state, and explicit contracts.

### Local server offline identity

A local Docker-based camp instance stores an offline-access record only for a user who prepared offline login according to ADR-009.

The local record contains only the information needed for local authentication and authorization:

- stable cloud user ID
- local display and sign-in information
- independent local Argon2id verifier
- confirmed tenant and camp associations required by the local instance
- authorization snapshot reference or version
- credential and security version
- time of the last successful cloud confirmation
- local enabled, expired, or revoked state

The local verifier and authorization state are authoritative only while the instance is operating offline under the rules in ADR-009.

### Transfer boundary

- Local offline identity records are Platform-owned data of the local application instance.
- They are not part of camp-package format version 1.
- They are not returned to or merged into the cloud.
- The central user account and central password verifier are never replaced by a package import.
- Preparing offline login requires direct cloud contact by the local instance before disconnection.

These rules retain ADR-005's restriction that the camp package does not replace user or tenant-wide Platform data.

### Single-device security state

The optional Tauri single-device password is not a cloud user account.

- Its verifier, recovery material, and security version form a device-local security record.
- The record has no tenant membership or cloud role.
- It is not transferred through a camp package.
- Future encryption may bind this record to locally protected data-encryption keys as required by ADR-009.

## Consequences

- Platform owns identity, membership, and technical credential persistence while maintaining internal separation between them.
- Authorization checks must always include tenant and, where required, camp scope after authentication.
- Every tenant-scoped endpoint, use case, export, import, and background job must obtain and validate an explicit tenant context.
- Local offline preparation needs an explicit provisioning use case and cloud-to-local security contract outside the camp package.
- Local identity records and backups become sensitive security data and require encryption, retention, audit, and secure-deletion decisions.
- Identity storage requires provider-specific migrations for PostgreSQL and SQLite under ADR-008.
- Architecture tests must prevent Domain assemblies from referencing authentication frameworks and must prevent credential persistence from leaking into Camp or Catering.
- Tenant-isolation integration tests must use at least two tenants with similar data and verify that reading, changing, exporting, importing, and preparing offline access cannot cross the boundary.
