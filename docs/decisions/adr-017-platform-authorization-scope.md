# ADR-017: Platform authorization scope

## Status

Accepted

## Context

The central recipe catalog is shared across tenants. Tenant and camp permissions cannot authorize catalog review or permanent platform-wide cleanup without violating their scope boundaries.

## Decision

Authorization gains a `Platform` scope with granular permissions for reading the central recipe catalog, reviewing central recipe proposals, and permanently deleting unreferenced central recipes. Recipe application code checks permissions only and does not depend on role names.

The initial platform role is `PlatformAdmin` and grants all platform permissions. Initial setup assigns it to the first user in addition to `TenantOwner`. Platform role assignments belong to the global user identity and not to a tenant membership.

When an existing database is upgraded, the migration assigns `PlatformAdmin` automatically only if exactly one user account exists. With multiple existing accounts, no account is elevated automatically; the assignment must then be made through an explicit administrative recovery process.

## Consequences

- Platform role assignments require dedicated persistence and migrations.
- Future platform administration can add roles without changing recipe-domain code.
- Permanent deletion remains additionally subject to reference checks.
- Tenant and camp administrators receive no implicit platform permissions.
- Multi-user installations need an explicit platform-administrator assignment during upgrade until an administrative recovery workflow exists.
