# Ingredients and Recipes

## Stage-based food factors

Catering owns food-planning factors; Camp owns stage names and anonymous participant estimates. A tenant food factor is matched to a tenant stage by its invariant-normalized name without creating a cross-module database foreign key.

The factor applies only to the `KiJu` estimate. Leaders always count as `1.0`. Each new or otherwise unconfigured stage defaults to `1.0`. Valid factors range from `0.1` through `3.0` with at most two decimal places.

Tenant factors are defaults for future camps. Camp creation atomically copies them into a stable Catering-owned camp configuration aligned with the camp-specific stages. Later tenant changes do not affect existing camps, and camp administrators may adjust their camp copy. Weighted food units are calculated as `KiJu × factor + Leiter`; they are derived rather than persisted.

## Camp meal schedule

Catering owns the camp meal schedule. Every camp starts with the configurable meal labels `Frühstück`, `Mittagessen`, and `Abendessen`. Each configured label is active on every day of the camp period by default. Camp administrators may add, rename, remove, and reorder labels and may deactivate individual meals, for example on arrival and departure days.

Adding a label creates an active meal for every current camp day. Removing it removes its daily entries. Changing the camp period removes entries outside the new period and creates active entries for newly added days. Recipes and cooking-unit assignments are separate later steps.

## Ingredients

Base ingredients contain:
- allergens
- intolerances
- dietary characteristics
- origin information

Base ingredients are scope-aware. They may belong to the central catalog, one tenant, or one camp. Central recipes use central ingredients only. Tenant recipes may additionally use ingredients owned by their tenant. Camp recipes may additionally use ingredients owned by their tenant or camp.

Measurement units and the allergen, intolerance, origin-property, and dietary-requirement identifier catalogs are platform-wide master data. The units usable for a concrete base ingredient and ingredient-specific conversions are owned by its revision. Vegan, vegetarian, pescetarian, lactose-free, milk-free, and gluten-free suitability is calculated from revisioned ingredient properties rather than stored as a direct ingredient assignment. Scope visibility is validated by application logic; Catering does not create database foreign keys into Platform or Camp infrastructure.

## Variants

Variants are owned by the ingredient domain and are treated as the same basic ingredient with targeted property and conversion overrides. Recipe positions reference a concrete published ingredient revision and may select one of its variants through the stable `variant_key`. Without a variant key, the base form is used. This rule is defined by ADR-021 and supersedes the earlier implicit interchangeability rule.

A recipe may define position-specific replacement ingredients for explicit allergen, intolerance, or dietary conflicts. Different replacement rules are not automatically combined.

The requirement originates from participant needs.

Example:
Normal butter can require lactose-free butter depending on participants.

## Central, tenant and camp data

Published recipes flow explicitly from the central catalog into a tenant library and from there into a camp library. Every reference targets a concrete immutable revision. Editing an upstream recipe creates an independently editable copy in the receiving scope with lineage to its source revision.

Tenant and camp changes must not silently overwrite upstream data. There is no automatic synchronization or semantic merge.
