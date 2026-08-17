# Lager Structure Domain

## Lager

A camp belongs to exactly one tenant.

A tenant can own multiple camps.

Every newly created camp has a required start and end date. The end date must not precede the start date. Within one tenant, the combination of invariant-normalized camp name, start date, and end date is unique. The same name may be reused for a different period.

Name and period may be changed by an active camp member with `camp.edit`. The same uniqueness and period rules apply to changes. A camp cannot be changed while it is frozen for an offline transfer. The change and its audit event are committed atomically.

Creating a camp requires at least one explicitly selected `CampAdmin`. The creator is not assigned automatically; another active member of the owning tenant may be selected. Camp creation, initial membership and role assignment, and the required audit event form one atomic operation.

## Structure

Camp
- Subcamps
- Cooking units
- Participants

The camp structure is available to other modules.

It is the foundation for:
- finances
- catering
- program
- material
