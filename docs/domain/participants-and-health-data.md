# Participants and Personal Data

## Principle

Personal data is handled separately from operational camp data.

The first planning phase uses anonymous participant estimates only. It does not store names, dates of birth, contact details, dietary information, or health information.

## Anonymous planning estimates

Each tenant maintains an ordered stage template. `TenantOwner` and `TenantAdmin` may change it through the tenant-settings permission. The initial suggested entries are `Biber`, `WiWö`, `GuSp`, `CaEx`, `RaRo`, and `Mitarbeiter`, but names and ordering are tenant-configurable.

Creating a camp copies the current tenant template into a stable camp-specific stage list. Later tenant-template changes apply to future camps only. A `CampAdmin` may adjust the camp-specific copy without changing the tenant template. This stable copy is also the future attachment point for stage-specific planning factors; those factors are not part of the first estimate increment.

For every leaf structure node, the camp stores non-negative whole-number estimates per camp stage in two categories:

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
