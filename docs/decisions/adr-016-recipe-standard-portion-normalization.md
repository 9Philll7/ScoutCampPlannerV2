# ADR-016: Recipe standard-portion normalization

## Status

Accepted

## Context

Camp stages and their food factors are configurable per tenant and copied into camps. Their IDs and factors are therefore unsuitable as stable semantics for central recipes or immutable revisions shared between scopes. Tenant and camp users should nevertheless be able to author recipes using a familiar stage as their input basis.

Delayed normalization during central submission would make revision meaning dependent on mutable external configuration and would be ambiguous for fixed and stepwise scaling.

## Decision

Every published portion-based recipe revision in the central, tenant and camp scope uses a standard portion with factor `1.0`.

Tenant and camp drafts may select a configured stage as an authoring basis. The draft snapshots the stage ID, stage name and positive factor. Publication calculates:

`standard reference servings = entered reference servings * authoring stage factor`

Ingredient and subrecipe reference quantities remain unchanged. The immutable revision stores the normalized standard reference servings. Authoring-stage data may remain as audit and lineage metadata but never participates in revision calculation.

Central drafts cannot use tenant- or camp-specific authoring stages.

## Consequences

- Every published revision is portable across central, tenant and camp scopes.
- Later changes to stage names or factors do not mutate recipe history.
- Central submission does not perform a second semantic conversion.
- Menu/cooking context applies its stage factor only to top-level demand.
- Nested recipes never apply another stage factor.
- Draft and publication validation must reject incomplete or non-positive authoring-stage snapshots.
