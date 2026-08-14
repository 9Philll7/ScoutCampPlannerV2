# Audit Security Validation

## Status

Completed on 2026-08-14. Canonical encoding, HMAC chaining, provider transaction behavior, checkpoint recovery, key storage and rotation, blocked mode, backup and restore, retention segments, provider-backed performance, and package-version-2 transfer binding are technically validated. This accepts the focused ADR-012 security spike; it is not a productive audit or package-version-2 release.

## Scope of phase 1

This phase evaluates a dependency-free candidate for:

- canonical representation of the mandatory audit fields and small string metadata
- HMAC-SHA-256 chaining with sequence, predecessor hash, key ID, and format version
- verification against a protected external chain head
- deterministic behavior on Windows and Linux
- detection of modified, removed, inserted, reordered, truncated, incorrectly signed, and unsupported records
- initial full-chain performance

It does not implement productive audit persistence, key storage, role-change use cases, API behavior, retention, or package transfer.

## Canonical encoding candidate

Format version 1 uses compact UTF-8 JSON generated directly through `Utf8JsonWriter`.

- Mandatory fields have a fixed order.
- Missing optional values are encoded as explicit JSON `null` values.
- GUIDs use lowercase .NET `D` formatting.
- Timestamps must have UTC offset zero and use the round-trip `O` representation.
- Binary hashes use the JSON writer's base64 representation.
- Metadata is restricted to string keys and string values and sorted by ordinal key order before writing.
- Sequence number, previous hash, key ID, and format version are part of the authenticated bytes.

No general-purpose object serializer or external canonical-JSON dependency is used. Changing field order, formatting, metadata types, or encoding requires a new chain-format version and compatibility fixture.

The version 1 golden fixture has this SHA-256 hash:

```text
A4B35365C0824C46D93804CAC8CB33C98F632C2FB76D053ACA0F562E107B4C0E
```

Windows 11 and the Linux SDK container produced the same value.

## HMAC-chain candidate

Each entry stores its sequence number, predecessor HMAC, key identifier, format version, event data, and HMAC-SHA-256 value. Verification requires:

- the expected predecessor of the first verified entry
- the externally protected expected final head
- a key resolver keyed by the stored non-secret key ID

The protected final head is necessary to detect deletion of the newest records. The database chain alone cannot prove that its current tail is complete.

Automated tests reject:

- changed event fields
- changed HMAC values
- deletion from the middle
- deletion of the tail
- insertion and duplication
- reordering
- an incorrect or missing key
- unsupported format versions

## Performance probe

The Release probe appends and then verifies 10,000 synthetic events. It does not write a database and is not a production capacity benchmark.

| Environment | Append 100,000 | Verify 100,000 | Verify latest 1,000 | Golden fixture |
| --- | ---: | ---: | ---: | --- |
| Windows 11, .NET 10 | 968.3 ms | 588.1 ms | 8.6 ms | matched |
| Docker Linux, .NET 10, 2 CPU / 2 GB | 1,224.8 ms | 617.4 ms | 6.0 ms | matched |

The encoding and HMAC cost is small enough for the defined startup and full-verification model. These measurements exclude database reads; productive end-to-end measurements remain required after the final schema and storage adapter exist.

## Evidence

- `tools/ScoutCampPlanner.AuditSecuritySpike`
- `tests/ScoutCampPlanner.SecuritySpikeTests/AuditHmacChainTests.cs`
- `tests/ScoutCampPlanner.SecuritySpikeTests/AuditCheckpointTests.cs`
- `tests/ScoutCampPlanner.SecuritySpikeTests/AuditKeyRotationTests.cs`
- `tests/ScoutCampPlanner.DatabaseMigrationTests/AuditPersistenceSpikeTests.cs`

## Phase 2 atomicity and crash reconciliation

Provider-level spike tables validate the intended transaction boundary without prematurely introducing productive audit migrations.

- SQLite and PostgreSQL commit the business-state change, audit row, and database head together.
- Explicit rollback leaves all three at their previous values.
- The external protected checkpoint is intentionally outside this transaction and advances only after commit.
- A verified database suffix after an older checkpoint is treated as recoverable interrupted checkpoint advancement.
- Reconciliation is idempotent when the checkpoint is current.
- A checkpoint ahead of the database, a mismatching head, a missing suffix, or a modified suffix is rejected.

This confirms that a distributed two-phase transaction is unnecessary.

### Concurrent append validation

Twelve parallel append requests produced exactly the contiguous sequence range 1 through 12 without conflicts for both supported providers.

- SQLite uses a process-wide asynchronous gate held for the complete transaction. This matches the single ASP.NET Core sidecar process and intentionally does not claim shared multi-process SQLite support.
- PostgreSQL locks the application-instance database-head row with `SELECT ... FOR UPDATE` before reading the predecessor and allocating the next sequence.

The final productive schema must additionally enforce a unique instance-and-sequence constraint.

## Phase 3 key-rotation model

The dependency-free rotation candidate uses a versioned protected key bundle with exactly one active key, at most one prepared key, and retained historical verification keys.

- A new key is staged as `Prepared` before database changes.
- The old key signs a segment-closing event that identifies the new segment and key.
- The new key signs the next contiguous segment-start event, whose predecessor is the closing HMAC and whose metadata identifies the old boundary.
- Only after the transition transaction commits does the protected bundle atomically make the old key `Historical` and the prepared key `Active`.
- A crash before database commit allows the unreferenced prepared key to be discarded.
- A crash after database commit allows the two signed boundary events and database head to authorize idempotent activation.
- Incomplete, mismatching, or unverifiable intermediate state is rejected.
- Historic keys remain resolvable for verification and cannot be activated again.

The tests prove that verification requires both old and new key material and rejects modified boundary metadata.

### Protected-file and operating-system probes

- The versioned key-bundle format round-trips one active, one optional prepared, and historical keys and rejects unsupported versions, invalid states, invalid material lengths, and invalid active/prepared cardinality.
- The checkpoint uses a versioned envelope, canonical payload, explicit checkpoint-purpose domain separation, and HMAC-SHA-256 authentication with the referenced audit key.
- Checkpoint modification, wrong keys, and unavailable keys are rejected.
- Same-directory temporary-file writing with write-through and atomic replacement leaves either the previous or complete new file and does not treat temporary files as active state.
- Windows 11 `CurrentUser` DPAPI successfully protected and restored a 32-byte key fixture and rejected a modified protected payload. The probe uses the official .NET Windows reference API without a third-party dependency.
- The Docker ASP.NET Core image runs as the official non-root `app` identity (UID/GID 1654). Its dedicated persistent security volume is mounted only into the backend; a probe file created with umask `077` was owned by `app:app` with mode `0600`. PostgreSQL had no mount for this volume, and the application health endpoint remained available.

Cloud secret injection remains an operational adapter contract because no specific cloud or orchestrator is selected. Productive code must accept protected key-bundle bytes from deployment-secret configuration without inventing a ScoutCampPlanner cloud-encryption format.

## Persisted multi-segment compatibility

Provider-level spike tables store sequence, predecessor hash, key ID, format version, serialized common event data, and HMAC as separate values. SQLite and PostgreSQL both round-trip a two-key segment transition, reconstruct byte-identical canonical version-1 representations, and verify the complete chain with the retained old and new keys. This validates provider type behavior and JSON value preservation for the current candidate.

The performance baseline uses 100,000 retained events per instance. Full in-memory verification remains well below one second on the Windows development system and close to 0.6 seconds in the constrained Linux container after the events are available in memory. The bounded latest-1,000 startup verification remains below 10 ms in both measurements. Database-loading time must be added to the productive benchmark.

## Blocked-mode and protected-recovery policy

The framework-free policy probe models the global operating state after audit verification fails. It deliberately does not add productive middleware, endpoints, authentication, or recovery tooling.

- An anonymous caller can see only a generic degraded health status.
- An existing authenticated session can continue to read non-sensitive data, but cannot mutate data.
- New sign-ins, business changes, sensitive administration, offline preparation, and package import or export fail closed because their mandatory audit events cannot be trusted.
- Diagnosis, protected-state restoration, and full verification require a local operator path. A normal authenticated application session is insufficient and no public recovery API is implied.
- A failed or partial verification keeps the blocked state and its original failure code.
- Only a successful full-chain verification clears the failure and returns the instance to normal operation.
- The policy never repairs, deletes, resigns, or recreates protected material automatically.

Automated tests cover the blocked-operation matrix, local-operator boundary, preservation after failed verification, and successful release after full verification. Productive adapters must map a denied operation to a stable service-unavailable response without exposing the internal failure code.

## Productive protected-storage adapters

The validated storage boundary is now represented by productive contracts in Platform Application and operating-model adapters in Platform Infrastructure. It stores the versioned key-bundle and authenticated checkpoint as opaque bytes; audit encoding and key lifecycle rules remain independent of operating-system storage.

- `FileAuditProtectedMaterialStore` atomically replaces each file. A crash between the two replacements leaves a detectable partial state rather than silently generating a replacement key. On Linux it applies owner-only `0600` permissions, matching the dedicated non-root Docker security volume.
- `WindowsDpapiAuditKeyBundleProtection` uses the official .NET `System.Security.Cryptography.ProtectedData` implementation with `CurrentUser` scope and explicit purpose entropy. Its API is marked Windows-only, so cross-platform callers require an operating-system guard.
- `ConfiguredAuditProtectedMaterialStore` reads the cloud key bundle from base64 deployment-secret input and writes only the external checkpoint. It refuses to replace a deployment-owned key.
- Plain file protection is permitted only for the already isolated Docker security volume; it is not an encryption mechanism.
- Startup distinguishes an explicitly new instance from an existing instance. Only the former may create initial material. Missing, partial, unreadable, or unprotectable existing state fails closed and must not call the key factory.

Platform integration tests validate explicit creation, reload, missing and partial state, immutable deployment keys, and a real Windows DPAPI modification rejection. Host registration remains intentionally deferred until the productive audit journal is introduced; the application startup coordinator and adapter boundary are ready for that composition.

## Encrypted recovery-set probe

The recovery archive candidate treats database bytes, the complete plaintext key-bundle representation, and the authenticated checkpoint as exactly one encrypted set. This specifically resolves the portability limitation of Windows CurrentUser DPAPI without weakening protection of the active local key file.

- Argon2id derives a 256-bit archive key from the user-supplied backup password with the already accepted password-derivation baseline.
- AES-256-GCM encrypts and authenticates the complete ZIP payload with an explicit version-1 recovery-archive purpose value.
- Every archive receives a fresh random 16-byte salt and 12-byte nonce.
- The archive envelope fixes and validates its version, KDF, parameters, cipher, salt, nonce, and authentication-tag sizes.
- Restore requires exactly the database, key bundle, and checkpoint entries and rejects empty components.
- Tests confirm round trip, independent archive output, wrong-password rejection, ciphertext-modification rejection, and refusal to create incomplete sets.

The operational sequence is documented in [`audit-backup-and-restore.md`](../architecture/audit-backup-and-restore.md). The current candidate buffers the archive in memory and is therefore validation code, not the final large-database backup implementation.

## Monthly segments and retention

The retention probe separates segment lifecycle from key lifecycle. A UTC month change closes the active segment and starts a new segment with the same active key at the first subsequent audit append. Months without events do not produce empty segments. Explicit key rotation continues to create an immediate two-key boundary independently.

- Closing and starting records use consecutive instance sequence numbers.
- The starting predecessor is the closing HMAC, and both records use the unchanged key ID for a monthly boundary.
- Complete transition verification succeeds through the existing canonical HMAC-chain verifier.
- A segment is eligible only when it is closed, was fully verified, and the latest retention expiry among all contained events has passed.
- Any legal hold or active offline transfer affecting the segment blocks deletion of the complete segment.
- Eligible deletion produces a non-personal closing proof containing segment ID, sequence range, predecessor hash, closing hash, key ID, and chain-format version.
- The probe never deletes rows, rewrites later events, or resigns a chain. Provider-specific transactional deletion and deletion-event persistence belong to productive audit storage implementation.

Automated tests cover continuity, unchanged keys, open and unverified segments, longest-retention behavior, legal hold, active offline transfer, and proof creation.

## Provider-backed loading performance

The database-loading probe persists 100,000 canonical chain entries, then measures query, materialization, JSON reconstruction, and HMAC verification together. Data generation and insertion are outside the measured interval. Both providers use an indexed primary-key sequence query matching the intended productive access path.

| Environment | Load and verify latest 1,000 | Load and verify all 100,000 |
| --- | ---: | ---: |
| SQLite file, Windows 11, .NET 10 | 31.7 ms | 1,449.7 ms |
| PostgreSQL 18 in Docker, Windows host, .NET 10 | 43.4 ms | 1,628.6 ms |

The automated acceptance limits are deliberately generous at 5 seconds for the startup window and 20 seconds for the full journal to reduce hardware-dependent test instability. Both measured providers remain far below those limits. The result validates the existing startup policy; it is not a general capacity promise for larger journals, concurrent production load, remote cloud latency, or slower storage.

## Package-version-2 binding

The transfer-security probe provisions one ECDSA P-256 key pair for one cloud-recorded transfer context. Only the public key remains in the cloud transfer record; the private key is delivered in the encrypted outbound package and is not an audit-journal key.

- Argon2id and AES-256-GCM protect the complete package in both directions with separate purpose values.
- The signed return proof binds format version, transfer, tenant, camp, baseline, source instance, audit range and heads, domain-payload hash, and audit-section hash.
- The audit-section hash covers every length-delimited canonical event representation and stored HMAC in order.
- Empty, truncated, discontinuous, cross-instance, or context-mismatching sections are rejected.
- A wrong public key, transfer password, direction, baseline, domain payload, or audit section fails verification.
- Event ID, source instance, and sequence identify duplicates. Only byte-identical canonical content and HMAC are idempotent; differing content is a conflict.
- Local audit HMAC keys remain local. Full local verification precedes signing, while cloud verification uses the public transfer key bound at offline preparation.

The tests validate key provisioning, encrypted private-key transport, complete bidirectional package envelopes, context and payload binding, mandatory continuity, wrong-key and wrong-password rejection, and duplicate classification. Productive version-2 serialization, migration from version 1, compatibility fixtures, persistent transfer records, independent audit-ingestion transactions, and domain-replacement orchestration remain implementation tasks.

## Conclusion

All focused technical validation items required by ADR-012 are complete. The results support productive implementation but do not authorize role-changing endpoints, health-data processing, or replacing Platform-owned state through camp packages.
