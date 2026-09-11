# ADR-023: Ingredient variants are selected during catering planning

## Status

Accepted

## Context

ADR-021 introduced revisioned ingredients and allowed a recipe ingredient
position to select an optional stable `variant_key`. During implementation this
made variants fixed parts of a recipe. That does not match their intended use:
variants exist primarily to provide a quickly selectable form of the same
ingredient when participant conflicts require it.

For example, lactose-free butter is still butter and may be selected for an
affected cooking unit. Margarine has a different raw-material basis and is
therefore a separate ingredient. If margarine is suitable as a substitute, the
relationship is declared explicitly for the concrete recipe position.

## Decision

A recipe ingredient position references exactly one concrete published
ingredient revision. Recipe positions and recipe replacement rules do not
select or persist a `variant_key`.

Variants remain immutable parts of the referenced ingredient revision and keep
their stable `variant_key` across ingredient revisions. They continue to inherit
the category, base unit, properties and conversions of their revision and may
apply the overrides defined by the ingredient domain.

The concrete variant is selected later in catering planning, for example for a
cooking unit whose participants require the lactose-free form. The planning
workflow must evaluate the effective conflicts and conversion overrides of the
selected variant. Automatic selection is not part of the recipe domain and is
not introduced by this decision.

A variant is allowed only when it remains professionally the same ingredient.
A different raw material or product is modeled as its own ingredient and can be
attached to a recipe position through an explicit replacement rule.

The same ingredient revision may occur only once within a recipe group. The
ungrouped area acts as one implicit group for this rule.

## Superseded part of ADR-021

This ADR supersedes only the statements in ADR-021 that recipe positions may
select a `variant_key` and that the earlier implicit-interchangeability rule is
superseded. All other decisions in ADR-021 remain valid, especially stable
ingredient identities, immutable published revisions, stable variant keys,
scope ownership, review states and provider-independent persistence.

## Consequences

- The recipe editor does not offer a variant selector.
- Recipe draft persistence and recipe snapshots contain no selected variant.
- Variant choice belongs to a later catering-planning or cooking-unit use case.
- Offline packages that support this use case must include the referenced
  ingredient revision together with its variants and overrides.
- Butter and lactose-free butter may be base form and variant of one ingredient;
  margarine is a separate ingredient and, where appropriate, an explicit recipe
  replacement.

