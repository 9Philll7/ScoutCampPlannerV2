# Audit Security Validation

## Status

Phase 1 and phase 2 completed on 2026-08-12. The key-rotation model portion of phase 3 is also validated. Canonical event encoding, the in-memory HMAC-chain model, database transaction atomicity, crash reconciliation, concurrent append allocation, bidirectional segment transitions, and rotation-state recovery are technically validated. The complete ADR-012 security spike is not yet complete.

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

| Environment | Append 10,000 | Verify 10,000 | Golden fixture |
| --- | ---: | ---: | --- |
| Windows 11, .NET 10 | 124.8 ms | 123.5 ms | matched |
| Docker Linux, .NET 10, 2 CPU / 2 GB | 166.9 ms | 138.1 ms | matched |

The encoding and HMAC cost is small enough to proceed to persistence validation. Database transactions, locking, storage size, startup verification, and realistically sized journals remain unmeasured.

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

The tests prove that verification requires both old and new key material and rejects modified boundary metadata. Concrete protected key-bundle serialization, atomic file replacement, DPAPI, Docker-volume permissions, cloud secret integration, and rotation of persisted provider data remain open.

## Remaining ADR-012 validation

Before productive audit implementation, the spike still must validate:

- protected key-bundle and checkpoint serialization plus atomic file replacement
- cloud secret integration, Docker-volume permissions, and Windows DPAPI behavior
- persisted provider-level rotation transaction and historic full-chain verification across multiple segments
- segment transitions and deletion of complete retained segments
- persistence round trips without canonical-value drift
- startup incremental verification and full verification at realistic journal sizes
- blocked mode and protected recovery behavior after verification failure
- package-version-2 binding and transfer separately before audit transfer is implemented

The completed phases validate the encoding, chain, transaction, concurrency, checkpoint-recovery, and key-rotation model candidates. They do not authorize productive role-changing endpoints.
