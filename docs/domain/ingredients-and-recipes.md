# Ingredients and Recipes

## Stage-based food factors

Catering owns food-planning factors; Camp owns stage names and anonymous participant estimates. A tenant food factor is matched to a tenant stage by its invariant-normalized name without creating a cross-module database foreign key.

The factor applies only to the `KiJu` estimate. Leaders always count as `1.0`. Each new or otherwise unconfigured stage defaults to `1.0`. Valid factors range from `0.1` through `3.0` with at most two decimal places.

Tenant factors are defaults for future camps. A stable camp-specific Catering copy and weighted planning totals are implemented as a separate increment.

## Ingredients

Base ingredients contain:
- allergens
- intolerances
- dietary characteristics
- origin information

## Variants

Variants are not handled globally on ingredient level.

A recipe defines whether:
- a variant is used
- a replacement ingredient is used

The requirement originates from participant needs.

Example:
Normal butter can require lactose-free butter depending on participants.

## Central and local data

Central published ingredients/recipes can have local copies.

Local changes must not silently overwrite central data.
