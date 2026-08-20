# Camp Package Format

## Purpose

A camp package transfers one camp and the camp-related data of explicitly listed modules between a server instance and a single-device instance. It is a domain-level interchange format and not a database backup.

## Container

Spike format version 1 is a ZIP container with the media type `application/vnd.scoutcampplanner.camp-package` and the recommended extension `.scoutcamp`.

It contains:

- `payload.json`: manifest and module payloads
- `payload.sha256`: uppercase hexadecimal SHA-256 checksum of the exact payload bytes

The checksum detects accidental or malicious modification but does not authenticate the sender. Signing and encryption remain product decisions.

## Manifest

The manifest contains:

- format version
- tenant ID
- camp ID
- transfer ID
- baseline version
- transfer direction
- included modules
- UTC creation timestamp

Version 1 requires the `Camp` and `Catering` module payloads. Platform data is limited to the tenant reference needed to establish a local instance. Return import never replaces Platform-owned tenant or user data.

The Catering payload includes the camp-specific meal labels and every dated meal with its active state, so arrival/departure-day adjustments survive Cloud → Local → Cloud replacement.

## Import rules

- Manifest and payload identities must match.
- Every exported entity must belong to the package camp.
- Initial import rejects an existing local camp.
- Return import requires the matching frozen camp, transfer ID, tenant, and baseline.
- A repeated or stale return package is rejected.
- Included module data is replaced inside one shared database transaction.
- IDs are preserved across the roundtrip.

## Versioning

The implementation currently accepts exactly version 1. Compatibility fixtures and an explicit migration registry must be added before a second format version is introduced.

## Planned audit transfer in version 2

ADR-012 requires a future format version 2 to carry a separate, cryptographically protected audit section for the local phase.

- The audit section is append-only and is not a replaceable Platform module payload.
- It identifies the source instance and audit sequence range and carries chain-bound verification material.
- Import is idempotent for byte-identical events and rejects conflicting duplicate identities.
- A mandatory invalid or incomplete audit section rejects the return package.
- Verified audit ingestion is independent from the atomic Camp/Catering replacement so evidence remains when domain import is rejected or rolled back.
- Audit transfer never replaces cloud users, memberships, roles, credentials, or tenant-wide Platform state.

Version 2 must not be implemented until package migration, encryption, signatures, source-instance key binding, compatibility fixtures, and the ADR-012 integrity spike are complete.
