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
