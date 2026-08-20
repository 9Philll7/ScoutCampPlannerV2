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
