# Audit Security Validation

## Status

Phase 1 completed on 2026-08-12. Canonical event encoding and the in-memory HMAC-chain model are technically validated. The complete ADR-012 security spike is not yet complete.

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

## Remaining ADR-012 validation

Before productive audit implementation, the spike still must validate:

- atomic event, sequence, protected-head, and business-state updates for PostgreSQL and SQLite
- safe concurrent append behavior and provider-specific locking
- key loading, protection, rotation, and historic verification for cloud, Docker, and Windows single-device operation
- segment transitions and deletion of complete retained segments
- persistence round trips without canonical-value drift
- startup incremental verification and full verification at realistic journal sizes
- blocked mode and protected recovery behavior after verification failure
- package-version-2 binding and transfer separately before audit transfer is implemented

Phase 1 validates the encoding and cryptographic chain candidate only. It does not authorize productive role-changing endpoints.
