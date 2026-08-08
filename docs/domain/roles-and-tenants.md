# Tenant and Role Model

## Tenant

A tenant represents an organisation/group.

A tenant owns:
- users
- camps
- local data context

A shared camp between multiple groups should use its own tenant.

## Roles

There are:
- tenant-wide roles
- camp-specific roles
- possibly module-specific permissions

The exact role set is defined during implementation.

## Authentication modes

Authentication modes are defined by [ADR-009](../decisions/adr-009-authentication-modes.md):

- Cloud users authenticate with a normal password.
- A connected local server instance uses cloud authentication.
- Authorized users can prepare an independent local verifier for offline login by entering the same password while connected.
- Central password verifiers and plaintext passwords are never transferred to the local instance.
- A single-device password is optional and remains independent of cloud credentials.

Authentication does not grant access by itself. Tenant membership, camp assignment, roles, and permissions must be evaluated separately. The exact role and permission set remains open.
