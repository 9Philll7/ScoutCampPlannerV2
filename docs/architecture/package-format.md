# Camp Package Format

## Increment 2 integration status — 2026-10-07

The optional participant section (schema 2, DummyDataOnly) transports Camp
participants, attendance, requirements and referenced dietary rules, including
tenant scope and rule version. Grants are never imported. Export requires explicit
read permission; return replace and local removal require explicit edit permission.
Atomic replacement and rollback have automated tests. Conflicting existing local
dietary references reject the import instead of silently changing the rules.
Supply/verification transport is not implemented. Do not transfer real participant
or health data using this development feature.
The confirmed [development decision](../decisions/meal-planning-increment-2-offline-sensitive-data.md)
permits dummy/test-data integration, but grants no read/edit permission and no
production security release. The outer container format and Freeze/Replace are unchanged.

Participant schema 2 includes the Camp-owned leaf structure reference. Meal-planning
schema 3 carries the camp-wide demand mode, calculated basis and CookingUnit filters;
existing structure references remain authoritative. It does not accept operational
direct participant assignments. Old schema 1/2 packages are upgraded only when the
direct references resolve to an unambiguous leaf without individual meal overrides;
otherwise import fails atomically with a migration message. Existing packages are
never overwritten. A return cannot silently drop existing planning configuration.
This is not the protected production package format version 2 described in ADR-012.

## Purpose

A camp package transfers one camp and the camp-related data of explicitly listed modules between a server instance and a single-device instance. It is a domain-level interchange format and not a database backup.

## Container

Spike format version 1 is a ZIP container with the media type `application/vnd.scoutcampplanner.camp-package` and the recommended extension `.scoutcamp`.

It contains:

- `payload.json`: manifest and module payloads
- `payload.sha256`: uppercase hexadecimal SHA-256 checksum of the exact payload bytes

The checksum detects accidental or malicious modification but does not authenticate the sender. Package version 1 is therefore unsuitable for sensitive data. ADR-012 defines and the security spike validates the protection direction for version 2, but productive encryption, signatures, serialization and migration are not implemented yet.

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

Package format version 1 also contains the mandatory, independently versioned
`cateringMealPlanningData` object. Its embedded schema version 3 transfers the
complete mutable state of meal planning increment 1: plans and immutable
snapshots, offer groups and entries, cooking-unit groups and units, default and
meal-specific structure assignments, subscription states, demand and target
overrides, recipe choices, calculation snapshots, fingerprints, warnings and
status markers. The data is validated against the package camp meals and
structure nodes and participates in the same atomic replacement transaction.
The former development-only duplicate `mealPlans` payload was removed; there is
one authoritative meal-planning representation.

Before the first product release, version 1 was additionally extended with a versioned `cateringReferenceData` module object. It contains the immutable dependency closure of every recipe revision included in the camp recipe library or referenced by meal-plan entries and cooking-unit recipe choices. Planning references remain pinned to their exact revisions, even when the library has changed. Export, validation and import use the same roots; unrelated revisions remain disallowed:

- the camp-library reference needed to expose the recipe locally
- the exact published recipe revision and all recursively referenced subrecipe revisions
- the referenced recipe identities
- every referenced published ingredient revision and its ingredient identity
- the current published revision of each included ingredient identity when it differs from the recipe-pinned revision
- variants and their property, unit-conversion, and nutrition overrides
- revision-bound properties, unit conversions, and nutrition profiles including source, review state, and reference date
- measurement units and the provider-identical ingredient reference catalogues needed to resolve those records locally

The embedded Catering reference schema currently has version 2 and is validated independently inside package format version 1. It accepts the development-only schema 1 as an explicit migration input. A missing, malformed, cross-camp, duplicate, or transitively incomplete reference section rejects the complete package. Offline use does not load a missing dependency from the cloud. Published camp-local recipe revisions used by the camp library are transferred as immutable offline references; mutable camp-local drafts remain outside this reference section.

These records are imported idempotently: an existing immutable identity is not updated or deleted. On return import, the cloud remains authoritative for central and tenant-wide recipe and ingredient catalogues; the reference section never grants replacement authority over them. Transfer and replacement of mutable camp-local recipe drafts is not part of this increment and remains separate work.

## Import rules

Meals outside the current camp period are retained and transferred, as required
by meal-planning period-change rules. They are operationally excluded, not invalid
package data. Meal identity, type reference and change-version checks still apply.

### Explicit recovery after a lost package

The source camp card offers return import and, separately, **unlock without a
return package**. Both require the existing camp-level `camp.import-package`
permission; single-device operators cannot cancel a source transfer.
Return import additionally checks the camp selected on the card.

Explicit recovery requires confirmation that offline changes will not be adopted.
It preserves the current source domain data, clears freeze and active transfer,
and advances the baseline. The expected transfer ID and baseline are required;
a stale request cannot cancel a newer transfer. A later recovered package from
the abandoned transfer is rejected, including after a new outbound transfer.
The cancellation and `camp.offline-transfer.cancelled` audit event are committed
atomically. Return import and cancellation serialize on the affected camp row.
This is not synchronization or recovery of lost offline changes. The disconnected
local application cannot be remotely stopped; its abandoned copy must not be used
further. No existing camp is automatically unlocked by deployment of this feature.

The frozen source camp and the writable local copy have distinct transfer
states: the source has `IsFrozen = true`; the local copy has `IsFrozen = false`
while retaining the same `ActiveTransferId` and original `BaselineVersion`.
Initial import must not reset the baseline to 1. A camp with an active transfer
cannot start a further outbound transfer. Only the writable local copy can
produce a return package; the source validates and completes the transfer.

The frozen source camp and the writable local copy have distinct transfer
states: the source has `IsFrozen = true`; the local copy has `IsFrozen = false`
while retaining the same `ActiveTransferId` and original `BaselineVersion`.
Initial import must not reset the baseline to 1. A camp with an active transfer
cannot start a further outbound transfer. Only the writable local copy can
produce a return package; the source validates and completes the transfer.

The browser export selects and opens the destination file before requesting the
offline transfer. Cancelling the save dialog therefore leaves the camp online.
This requires a browser supporting `showSaveFilePicker` (for example Chrome or
Edge in a secure context). Once the transfer request has been sent, write or
network failures do not automatically unfreeze the camp because a package may
already have been issued. Existing frozen transfers are not reset by this UI fix.

The browser export selects and opens the destination file before requesting the
offline transfer. Cancelling the save dialog therefore leaves the camp online.
This requires a browser supporting `showSaveFilePicker` (for example Chrome or
Edge in a secure context). Once the transfer request has been sent, write or
network failures do not automatically unfreeze the camp because a package may
already have been issued. Existing frozen transfers are not reset by this UI fix.

- Manifest and payload identities must match.
- Every exported entity must belong to the package camp.
- Initial import rejects an existing local camp.
- Return import requires the matching frozen camp, transfer ID, tenant, and baseline.
- A repeated or stale return package is rejected.
- Included module data is replaced inside one shared database transaction.
- IDs are preserved across the roundtrip.

## Versioning

The implementation currently accepts exactly version 1. As no product version has been released yet, earlier development-only version-1 files without the mandatory Catering reference section are intentionally rejected. Compatibility fixtures and an explicit migration registry must be added before a second format version is introduced.

## Planned audit transfer in version 2

ADR-012 requires a future format version 2 to carry a separate, cryptographically protected audit section for the local phase.

- The audit section is append-only and is not a replaceable Platform module payload.
- It identifies the source instance and audit sequence range and carries chain-bound verification material.
- Import is idempotent for byte-identical events and rejects conflicting duplicate identities.
- A mandatory invalid or incomplete audit section rejects the return package.
- Verified audit ingestion is independent from the atomic Camp/Catering replacement so evidence remains when domain import is rejected or rolled back.
- Audit transfer never replaces cloud users, memberships, roles, credentials, or tenant-wide Platform state.

Version 2 must not be implemented until package migration, encryption, signatures, source-instance key binding, compatibility fixtures, and the ADR-012 integrity spike are complete.
