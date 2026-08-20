# ADR-018: Scope of ingredient and conflict catalogs

## Status

Accepted

## Context

Central, tenant and camp recipes require stable ingredient, unit and conflict references. The recipe specification deliberately assigns variants and conversions to an ingredient domain, but did not yet define the ownership scopes of these catalogs.

## Decision

Measurement units, allergens, intolerances and dietary requirements are platform-wide master data.

Base ingredients use the same explicit scopes as recipes:

- `central` ingredients have no owner ID;
- `tenant` ingredients belong to one tenant;
- `camp` ingredients belong to one camp.

A central recipe may reference only central ingredients. A tenant recipe may reference central ingredients and ingredients of its tenant. A camp recipe may reference central ingredients, ingredients of its tenant and ingredients of its camp. These visibility rules are enforced in application logic without cross-module database foreign keys.

Ingredient variants belong to exactly one base ingredient and are treated as 1:1 interchangeable. Units available for a base ingredient and their ingredient-specific conversions also belong to that base ingredient.

Offline packages contain every ingredient, variant, unit and conflict entry required by the included recipe revisions. Package consumers do not resolve missing master data from the cloud.

## Consequences

- Central recipes remain portable across tenants and camps.
- Tenants and camps can add local ingredients without modifying the central catalog.
- Platform-wide conflict identifiers remain stable across recipe scopes.
- Scope visibility needs explicit validation when a recipe is saved or published.
- Package schemas must include the transitive master-data closure of transferred recipe revisions.
