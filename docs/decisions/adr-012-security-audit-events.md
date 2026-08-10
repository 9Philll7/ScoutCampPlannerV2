# ADR-012 Security Audit Events

## Status

Accepted

## Context

ADR-009 through ADR-011 define authentication, identity, tenant isolation, roles, and permissions. Security-sensitive actions must be traceable across cloud, local server, offline, and single-device operation without recording credentials or sensitive domain payloads.

This ADR defines the first mandatory audit event catalogue, common event fields, instance-local persistence, tamper-evidence model, initial retention policy, authorized access, and the offline-to-cloud transfer direction. Key storage, package protection, and performance details require technical validation before implementation.

## Decision

### Authentication events

The following events are audited:

- successful sign-in
- failed sign-in
- sign-out
- account lock and unlock
- password change
- password-reset request
- successful password reset
- offline access prepared, renewed, revoked, or invalidated
- single-device password recovery
- security-driven session invalidation

Public password-reset responses remain generic even though the internal audit distinguishes permitted event outcomes.

### Identity and authorization events

The following events are audited:

- user account created, disabled, reactivated, or otherwise security-state changed
- tenant membership added, changed, or removed
- camp membership added, changed, or removed
- tenant or camp role assigned, changed, or removed
- tenant ownership transferred
- attempt to remove or demote the last active `TenantOwner`
- denied administrative or otherwise sensitive operation

Ordinary denied read requests are logged operationally where needed but do not automatically become permanent audit records. This avoids creating an unbounded audit stream from routine probing. Sensitive denials are defined by the permission and privacy catalogues.

### Offline and package events

The following events are audited:

- offline phase started
- camp package exported
- initial or return import started
- import completed successfully
- import rejected
- import rolled back
- baseline, transfer, direction, version, or integrity validation failed

### Common event fields

Every audit event contains:

- random stable event ID
- UTC timestamp
- stable action identifier
- result: success, denial, or categorized failure
- actor user ID when known
- tenant ID and camp ID when applicable
- target type and stable target ID when applicable
- operating origin: cloud, local server, offline local server, or single device
- application instance ID
- request or operation correlation ID
- credential, security-state, authorization-snapshot, or role-definition version when relevant

Events may contain a small, explicitly defined metadata object of non-sensitive identifiers and reason codes. Arbitrary serialized request objects are prohibited.

### Prohibited audit content

Audit events never contain:

- passwords or password fragments
- password salts, verifiers, Argon2 derived values, or benchmark details tied to a user
- session, reset, channel, or recovery tokens
- recovery codes
- complete package payloads or database records
- health data
- free-form domain content
- exception dumps, connection strings, or technical messages that may contain secrets

Operational diagnostic logs remain separate from the security audit and must apply their own secret-redaction rules.

### Future sensitive-data events

Health-data access, disclosure, change, export, anonymisation, and deletion require a dedicated audit catalogue before those features are implemented. No current role or audit event implies permission to process health data.

### Audit persistence

Security audit persistence belongs to the Platform module.

- Every cloud, local server, and single-device application instance maintains its own audit journal.
- Events are stored in a dedicated Platform-owned table in the instance's PostgreSQL or SQLite database.
- The journal is append-only through application interfaces.
- Normal application use cases cannot update or delete an existing event.
- Camp, Catering, and future modules emit events through a defined Platform Application contract and never access the audit table or `PlatformDbContext` directly.
- Audit queries use dedicated Platform Application use cases rather than exposing unrestricted persistence access.

The persistence representation includes explicit common columns and a small versioned JSON metadata object. Arbitrary requests, domain entities, exception objects, or package payloads cannot be serialized into the metadata field.

### Transaction boundary

A mandatory audit event and the security-sensitive state change it describes are committed in the same database transaction where technically possible.

- If writing the required audit event fails, the associated administrative or security-state change is rolled back.
- A successful state change must not exist without its required successful audit event.
- Failed authentication and other events without an associated state transaction are appended immediately in their own transaction.
- Audit failure handling must avoid recursive audit attempts and must emit a redacted operational error.

Cross-module operations use the existing composition-level shared-transaction approach. A module contributes the event through the Platform contract without acquiring direct Platform Infrastructure access.

### Current transfer boundary

Package format version 1 does not transfer local audit events to the cloud.

- Audit records remain on the instance that produced them.
- They are not treated as replaceable Platform data during package import.
- The version 2 direction defined below must append deduplicated events and must never replace or rewrite existing cloud audit history.

Application-level append-only behavior alone does not protect against a database administrator or filesystem owner modifying records directly. The following integrity model adds tamper evidence and defines the key-storage direction that must be technically validated.

### Tamper evidence

Every instance protects its audit journal with a versioned HMAC-SHA-256 chain.

Each event stores:

- a monotonically increasing per-instance sequence number
- the previous event or segment hash
- its own HMAC value
- the identifier of the HMAC key and chain-format version

The HMAC covers the canonical representation of every mandatory event field, metadata, sequence number, previous hash, instance ID, key ID, and format version. Canonicalization is explicitly versioned and covered by compatibility fixtures.

The HMAC key is not stored in the audit database:

- cloud operation uses a deployment secret or managed secret store
- local Docker operation uses a protected instance secret outside PostgreSQL
- Windows single-device operation uses operating-system-protected local key storage

Key rotation creates a new chain segment whose first record references the verified head of the previous segment. Historic keys remain available for verification according to the future audit-retention policy but cannot be used to append new events.

The current chain head is stored in protected state outside the audit table. This makes straightforward deletion of the newest table rows detectable. A future offline transfer also carries verified segment boundaries and chain heads rather than flattening or rewriting the source journal.

### Verification failure

Audit verification runs incrementally when appending, checks the recent chain section during startup, runs fully for audit export or administrative verification, and can run periodically in the background.

When verification fails:

- the application never silently repairs, deletes, or resigns records
- the security status becomes clearly visible
- sensitive administration, offline preparation, package export, and package import are blocked
- read-only diagnosis and protected recovery remain available
- an operational alert is emitted without leaking key material

The chain provides tamper evidence, not absolute protection. An actor controlling the database, application runtime, protected key store, and external chain-head state remains outside this threat model.

### Required technical validation

Before implementation, a focused security spike must validate:

- deterministic canonical encoding across supported .NET versions and both providers
- atomic event, sequence, chain-head, and business-state updates
- cloud, Docker, and Windows key storage
- key rotation and historic verification
- detection of edits, deletion, insertion, reordering, truncation, wrong keys, and broken segment links
- startup and full-verification performance for realistically sized journals
- safe blocked-mode and recovery behavior

### Retention

The initial security-audit retention periods are:

| Event category | Retention |
|---|---:|
| failed sign-in | 90 days |
| successful sign-in, sign-out, and ordinary session events | 180 days |
| password, account, tenant membership, camp membership, role, ownership, offline-access, and security-state changes | 24 months |
| offline phase, camp-package export, import, rejection, rollback, and validation events | 24 months |

Events associated with an active offline transfer are retained until at least 24 months after that transfer is completed, rejected, or administratively closed. They are never deleted while the transfer remains active.

Retention is evaluated by event category rather than arbitrary metadata. Unknown event categories use the longest applicable current security-audit period until they receive an explicit classification.

### Retention execution

- Expired events are deleted only as complete, previously verified chain segments.
- A non-personal segment-closing proof remains after deletion so later segments can still demonstrate their expected predecessor boundary.
- Retention never silently rewrites or resigns remaining events.
- The deletion operation and affected sequence range are audited.
- Deleted user accounts remain referenced only by stable user ID; audit events do not copy email addresses or display names for retention convenience.
- Normal tenant administrators cannot arbitrarily extend retention.
- A documented legal hold can suspend deletion for a specific tenant, camp, transfer, incident, or sequence range.
- Creating, changing, and releasing a legal hold are audited and require dedicated authorization.

These periods are technical defaults, not legal advice. They must be reviewed against the operating organisation's actual legal, contractual, and incident-response obligations before production use.

Health-data audit retention is deliberately excluded and requires its own privacy and legal decision before health-data functionality is implemented.

### Authorized access

Audit access uses the permissions defined by ADR-011:

- `tenant.audit.view` reads authorized tenant-wide events.
- `tenant.audit.export` exports authorized tenant-wide events.
- `tenant.audit.legal-hold.manage` creates, changes, and releases legal holds.
- `camp.audit.view` reads events scoped to an explicitly assigned camp.

`TenantOwner` receives all three tenant audit permissions. `TenantAuditor` receives view and export but cannot manage legal holds. `TenantAdmin` receives no tenant audit permission automatically. `CampAdmin` receives only camp-scoped view. `CampEditor` and `CampViewer` receive no audit access.

- Tenant and camp filters are applied before event materialization.
- Camp-scoped readers cannot view tenant-wide authentication, password, account, or security-state events.
- Audit export and legal-hold administration require recent password confirmation under ADR-009.
- Audit export and every legal-hold change are audited themselves.
- Exports include the authorized event fields and verification material but never HMAC keys or protected key-store state.
- The chain and verification status may be displayed without exposing cryptographic secrets.

On a single-device instance, an unlocked local application can display its local journal. Export and recovery-related audit operations require the configured local password when one exists. If no local password is configured, the documented device-access security model from ADR-009 applies.

### Offline-to-cloud audit transfer

A future camp-package format version 2 carries a separate audit section for the local phase.

The audit section contains:

- source application instance ID
- inclusive sequence range
- complete authorized audit events in canonical format
- first predecessor hash and final chain head
- chain-format and key identifiers
- cryptographic proof bound to the package transfer

Audit events are not a replaceable Platform-module payload. The cloud imports them into an append-only source-instance journal.

- Event ID, source instance ID, and sequence number provide deduplication identity.
- A repeated byte-identical event is idempotent.
- A duplicate identity with different canonical content is a security failure.
- Existing cloud audit events are never updated or deleted by package import.
- Audit records never authorize replacing cloud users, memberships, roles, credentials, or tenant-wide Platform state.

The mandatory audit section is cryptographically verified before camp-related replacement begins. A missing, incomplete, discontinuous, or invalid mandatory section causes the return package to be rejected.

After verification, audit ingestion is committed independently from the Camp/Catering replacement transaction. This preserves evidence of the local operation and attempted return even if the domain replacement is later rejected or rolled back. The cloud adds its own acceptance, rejection, or rollback events with the shared correlation and transfer IDs.

Successfully transferred local audit events remain subject to local retention and are not deleted immediately after transfer.

Package encryption, package signatures, binding of the source instance and verification key, key provisioning, key rotation, version migration, and compatibility fixtures must be designed and validated together before package version 2 is implemented. This ADR does not weaken the version 1 rule that Platform user data is not replaced.

## Consequences

- Stable audit action and reason identifiers become versioned contracts.
- Authentication, membership, role, package, and offline use cases must emit audit events inside their defined success or failure boundary.
- Tests must verify required events, common fields, correlation, result categorization, and prohibited-data redaction.
- Event production must not expose the existence of accounts or cross-tenant resources through public API responses.
- The accepted integrity and offline-transfer models still require the technical validation described above and the package-version-2 security work before release. Retention requires organisational/legal confirmation before production use.
- Authorization tests must cover tenant-wide, camp-scoped, denied, export, legal-hold, re-authentication, and single-device access.
- Offline-transfer tests must cover cryptographic binding, continuity, missing segments, deduplication, conflicting duplicates, independent audit commit, domain rollback, and prohibition of Platform-state replacement.
- Retention processing requires event classification, segment sealing, legal-hold state, deletion auditing, and tests at active-transfer and category boundaries.
- Platform requires provider-specific audit migrations under ADR-008 and an Application contract usable without crossing module Infrastructure boundaries.
- Transaction tests must prove that a failed required audit write rolls back the associated state change.
- Integrity tests must prove detection of modified, removed, inserted, reordered, truncated, and incorrectly signed events.
