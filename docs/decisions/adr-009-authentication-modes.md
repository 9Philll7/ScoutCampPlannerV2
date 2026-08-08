# ADR-009 Authentication Modes

## Status

Accepted

## Context

ScoutCampPlanner supports cloud/server operation, a local Docker-based camp instance, and a Windows single-device instance. Authentication must continue to work when a local camp instance loses its internet connection without exporting central password verifiers or making an active offline phase depend on the cloud.

The single-device instance has a different risk and usability profile: it is operated by one person on one Windows device and must remain usable without mandatory technical setup.

This ADR decides the authentication modes and the relationship between online and offline passwords. It does not yet define the concrete role set, permission matrix, password policy, session mechanism, password-reset process, or audit events.

## Decision

### Primary authentication

Cloud and local server authentication use a normal user password as the primary authenticator.

Passwords are never stored in plaintext. The concrete password hashing component and password policy must follow the security requirements established before implementation.

### Single-device instance

Creating a local application password is optional for the Windows single-device instance.

- If a password is configured, it protects access to that local application data.
- If no password is configured, access to the Windows account and device is sufficient to open ScoutCampPlanner.
- The application must communicate this consequence clearly during setup and in its security settings.
- The optional-password decision must be reviewed before real health data is supported. Device access alone may not provide sufficient protection for sensitive personal data.

The optional single-device password is local to the device and independent of a cloud password.

### Local server instance while online

When the local Docker-based camp instance can reach the cloud, users authenticate through the normal cloud authentication path. Cloud account state remains authoritative while connected.

### Local server instance while offline

An explicitly authorized camp user can prepare offline login while the local instance still has cloud connectivity:

1. The user authenticates successfully against the cloud.
2. The user explicitly enables offline login on that local instance and enters the same password again.
3. The local instance derives and stores its own salted password verifier.
4. When cloud authentication is unavailable, the local instance verifies the password against that local verifier.

The online and offline password are therefore the same from the user's perspective, but the central and local password verifiers are technically independent.

### Credential boundaries

- The cloud password verifier is never exported.
- Plaintext passwords are never written to a database, package, backup, or log.
- Local password verifiers are not returned to the cloud.
- Local password verifiers are not part of the camp package defined by ADR-005 and package format version 1.
- Only users explicitly authorized for the affected camp may prepare offline login.
- A user who has not prepared offline login on the local instance cannot authenticate after connectivity is lost.

### Password changes and revocation

Cloud password changes, account locks, and permission revocations cannot take effect while the local instance is disconnected.

- During an active offline phase, the last locally provisioned authentication and authorization state remains authoritative.
- On the next successful cloud contact, the local instance must detect security-relevant account changes.
- A changed cloud password or invalidated credential state disables the existing offline verifier. The user must prepare offline login again with the current password.
- The UI must show whether offline login is prepared and when the local security state was last confirmed by the cloud.

## Consequences

- Offline capability does not require distributing central password hashes.
- Every user who needs offline access must prepare it before the local instance disconnects.
- Loss of connectivity creates an unavoidable delay for cloud-side revocation. This risk must be reflected in authorization scope, audit events, and offline-phase procedures.
- The local authentication store becomes security-sensitive and must be covered by encryption, backup, retention, and secure-deletion decisions.
- Package format version 1 and the rule that user data is not replaced during package return remain unchanged.
- Implementation must wait for the remaining identity, session, role, tenant-isolation, audit, and privacy decisions.

