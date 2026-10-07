# Participants and Personal Data

## Increment 2 foundation — 2026-10-04

The Camp-owned `Participant` aggregate now stores identity/display name,
whole-day and meal absences, one optional DietType reference, allergen references
and substance requirements with optional grams-per-portion threshold and source
metadata and its leaf StructureNodeId. Camp administration uses the protected
`ICampParticipantLookup`; normal Catering planning instead consumes
`ICampCateringParticipantLookup`, containing only pseudonymous reference, structure,
effective meal presence and structured catering requirements. No names, source
metadata or full health dossier are included. Day absence takes
precedence over meal attendance. Updates do not infer any medical threshold.

Update 2026-10-05: internal composition-level CRUD use cases now enforce explicit
permissions, reference/camp/date validation, freeze and stale-edit checks and
atomic metadata-only auditing. Threshold precision is validated before storage.
See [development audit catalogue](../architecture/participant-development-audit.md).
HTTP endpoints and the participant UI, explicit online/local grant administration
and dummy participant package transport are implemented. The participant editor
copies an optional central threshold/source once when a requirement is added;
later catalogue changes do not overwrite the individual value. DietType selection
is restricted to central entries and the camp's tenant. Camp structure assignment
and CookingUnit structure/filter derivation are implemented; SupplySolutions remain pending.
See [the confirmed correction](../decisions/meal-planning-participant-structure-correction.md).
The audit catalogue covers dummy development only, not production health retention.
The contract itself is not an authorization boundary; callers must enforce
explicit health read/edit permissions before accessing sensitive data.
Existing roles and ordinary local import access do not grant those permissions.
Only dummy/test data is permitted for the development phase.

## Principle

Personal data is handled separately from operational camp data.

The first planning phase uses anonymous participant estimates only. It does not store names, dates of birth, contact details, dietary information, or health information.

## Anonymous planning estimates

Each tenant maintains an ordered stage template. `TenantOwner` and `TenantAdmin` may change it through the tenant-settings permission. The initial suggested entries are `Biber`, `WiWö`, `GuSp`, `CaEx`, `RaRo`, and `Mitarbeiter`, but names and ordering are tenant-configurable.

Creating a camp copies the current tenant template into a stable camp-specific stage list. Later tenant-template changes apply to future camps only. A `CampAdmin` may adjust the camp-specific copy without changing the tenant template. Catering uses this stable copy for camp-specific food factors; the factors are separate Catering data and do not add personal information to the estimates.

For every eligible leaf structure node, the camp stores non-negative whole-number estimates per camp stage in two categories. In a free structure every leaf is eligible; in a fixed structure only leaves on the final configured level are eligible.

- children and youth (`KiJu`)
- leaders (`Leiter`)

Estimates contain no personal identity. A structure node with non-zero estimates cannot receive child nodes. Moving a node keeps its estimates attached to that node.

Planning totals are derived from the stored leaf estimates and are not persisted separately. The overview shows camp totals per stage and aggregated `KiJu` and `Leiter` totals for every structure branch. A parent total therefore includes all descendant leaves.

Participants have:
- identity data
- assignment data
- dietary information
- health information

## Health sheet

Contains for example:
- emergency contact
- medication
- relevant health information

## Lifecycle

When a camp is archived:
- archival is manual
- personal data can be anonymised according to retention rules

The complete privacy workflow is still to be defined.
