# ADR-009 Authentication Modes

## Status

Accepted

## Context

ScoutCampPlanner supports cloud/server operation, a local Docker-based camp instance, and a Windows single-device instance. Authentication must continue to work when a local camp instance loses its internet connection without exporting central password verifiers or making an active offline phase depend on the cloud.

The single-device instance has a different risk and usability profile: it is operated by one person on one Windows device and must remain usable without mandatory technical setup. ADR-013 defines Windows 11 x64 as the supported desktop reference and security-maintained Windows 10 22H2 x64 as a tolerated transitional compatibility tier.

This ADR decides the authentication modes, the relationship between online and offline passwords, the session models, the user-facing password policy, Argon2id hashing, strength-check behavior, cloud password reset, and single-device password recovery. The focused validation documented in [`security-library-validation.md`](../spike/security-library-validation.md) accepts the initial Argon2id and password-strength libraries. ADR-014 supersedes this ADR's original separate-denylist requirement and requires the strength check for every password length. Identity and tenant membership are defined by ADR-010, roles and permissions by ADR-011, and the security audit model by ADR-012.

## Decision

### Primary authentication

Cloud and local server authentication use a normal user password as the primary authenticator.

Passwords are never stored in plaintext. Password verifiers use Argon2id as defined below. The initial Argon2id and strength-check libraries are accepted by the focused security-library validation. ADR-014 removes the separate denylist and makes the strength estimator authoritative for all accepted lengths.

### Password policy

The same policy applies to cloud passwords and optional single-device passwords:

- The hard minimum length is 8 characters.
- Passwords from 8 through 128 characters are accepted only when a server-side strength check rates them as sufficiently resistant to guessing.
- There is no composition requirement.
- The maximum accepted length is 128 characters.
- Spaces, Unicode, and all printable characters are allowed.
- Minimum, strength-policy, and maximum length boundaries count Unicode scalar values. In .NET this means enumerating `System.Text.Rune` values rather than using UTF-16 `string.Length`.
- Combining sequences are not normalized and are not collapsed into user-perceived grapheme clusters. Each Unicode scalar value counts separately. This preserves the rule that passwords are processed exactly as entered and avoids Unicode-version-dependent grapheme segmentation in the authentication contract.
- Passwords are never silently truncated or normalized into a different value.
- Paste and password-manager use must be supported.
- The UI recommends a long passphrase even though the hard minimum is 8 characters.
- Periodic password changes are not required. A change is required after suspected or confirmed compromise and remains available at the user's request.
- A normal password change requires the current password. Recovery uses the separately controlled reset process.
- The system does not maintain an arbitrary password-history rule such as prohibiting the last five passwords.

The strength check is enforced by the backend. Frontend feedback may assist the user but is not the security boundary.

### Password strength checks

Passwords from 8 through 128 characters require a score of at least 3 on a zxcvbn-compatible 0-to-4 strength scale.

The server-side evaluation must consider at least:

- common words and known password patterns
- repeated and sequential characters
- keyboard sequences
- user-specific inputs such as name and email-address components
- predictable combinations and substitutions

`zxcvbn-core` 7.0.92 is the initially accepted backend estimator. It is isolated behind an Infrastructure adapter because the package is not actively maintained and its result object contains the evaluated plaintext password. The result object must never escape the adapter; only an application-owned score and neutral reason identifiers may be returned. Golden score fixtures make a future replacement observable.

- Cleartext passwords and complete unsalted password hashes are never sent to an external breach-checking service.
- The required checks work without internet connectivity, including on a single-device instance.
- The backend remains authoritative. Angular may run an equivalent estimator only to provide immediate feedback.
- The estimator implementation is recorded so that policy changes remain testable.
- Updating the estimator does not invalidate an existing password automatically. A password change is required when a concrete compromise or unacceptable risk is identified.

The selected .NET implementation must support deterministic tests and must be reviewed for maintenance status, licensing, package size, and consistent behavior on server and Windows single-device targets.

### Password hashing

Cloud, local-server offline, and optional single-device password verifiers use Argon2id.

- Every verifier uses a cryptographically random, unique salt.
- The stored representation is versioned and includes the Argon2 version and all parameters needed for verification.
- Cloud and local offline preparation derive independent verifiers with independent salts even though the user enters the same password.
- A central verifier is never reused as a local verifier.
- The implementation must support detecting outdated parameters and rehashing after the next successful password verification.
- Passwords, salts, derived values, and timing details are never written to application logs.
- Verification uses a constant-time comparison for the derived value.

`Konscious.Security.Cryptography.Argon2` 1.3.1 is accepted as the initial Infrastructure implementation by the focused validation in `docs/spike/security-library-validation.md`. It supports the current .NET target, explicit parameters, the official compatibility vector, and both validated operating-system families without native runtime dependencies. Its 2024 release date requires ongoing dependency monitoring and a maintenance review before security-sensitive upgrades.

Memory, iteration, parallelism, salt length, derived-key length, and maximum concurrent verification settings are calibrated through technical benchmarks. Parameters are versioned configuration with secure lower bounds, not user settings.

- Cloud and local-server instances use 64 MiB memory, three iterations, one lane, a 16-byte salt, and a 32-byte derived value.
- Single-device instances initially use 19 MiB memory, two iterations, one lane, a 16-byte salt, and a 32-byte derived value.
- Every application instance permits at most two concurrent Argon2id derivations. Authentication rate limiting remains separately required.
- Configuration may raise these values after benchmark validation but must not lower them below the operating-mode profile without a new security decision.
- The server profile was validated with a 2-vCPU/2-GB Docker limit. The single-device profile was validated on Windows 11; the Windows 10 compatibility measurement remains an ADR-013 release-readiness check because no maintained Windows 10 test device is currently available.

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

### Cloud password reset

A user who has forgotten the cloud password can request a reset link for the confirmed account email address.

- The public response is identical whether or not an account exists for the submitted address.
- The reset token is cryptographically random, single-use, and valid for 30 minutes.
- No usable plaintext reset token is stored in the database.
- The replacement password must satisfy the current password policy and strength check.
- A successful reset does not sign the user in automatically.
- A successful reset invalidates all cloud sessions for the account.
- Previously prepared local offline verifiers become invalid at the next successful cloud contact and must be prepared again with the new password.
- Reset requests are rate-limited by account identifier and request origin to reduce enumeration, brute force, and email flooding.
- Reset request and completion events are audited without recording the token, password, or password-derived data.

A cloud password cannot be reset through a fully disconnected local server instance. A local administrator cannot replace a cloud password. Cloud connectivity and control of the confirmed reset channel are required.

The email delivery provider, token-protection implementation, rate limits, and notification wording are selected and tested during implementation.

### Single-device password recovery

Enabling an optional single-device password generates a cryptographically random recovery code.

- The complete recovery code is shown exactly once.
- The user is instructed to print it or store it in a password manager outside the device.
- ScoutCampPlanner stores only a verifier or wrapped recovery material, never a usable plaintext recovery code.
- A valid recovery code permits setting a new local password that satisfies the current password and strength policy.
- Successful recovery invalidates all local sessions and rotates the local security state.
- Recovery is audited locally without recording the recovery code, password, or derived values.
- The recovery code is device-specific and cannot reset a cloud password.
- The cloud cannot recover or bypass an independent single-device password.

There is no hidden bypass when both password and recovery code are unavailable. Recovery then requires restoring an appropriate protected backup or creating a new local instance. Offline changes that were not returned may be lost.

When local database encryption is introduced, the recovery mechanism must be extended so that the recovery code safely unlocks or rewraps the data-encryption key. The current decision must not be implemented as a plaintext database-key store.

### Cloud and local server sessions

Cloud and local Docker-based server instances use server-managed sessions with a random, signed authentication cookie.

The productive server session record stores the random session ID, user ID, credential Security Version, creation time, last activity, and absolute expiration in Platform Infrastructure. Each authenticated request validates that record together with the active account and current credential Security Version. Activity persistence is updated at most once per minute.

- The cookie is `HttpOnly` and is not accessible to Angular code.
- Authentication tokens are not stored in browser `localStorage` or `sessionStorage`.
- Production server cookies are transmitted only over HTTPS.
- A session belongs to exactly one application instance and is not copied between cloud and local instances.
- After successful cloud authentication, a local server instance creates its own local session.
- Successful offline password verification creates the same type of local session; only the credential verification source differs.
- Signing out invalidates the current session.
- Password changes, account locks, and role changes invalidate affected online sessions when cloud connectivity exists.

Session limits are:

- 30 minutes without activity
- 12 hours absolute lifetime regardless of activity
- no persistent "remember me" session initially

Sensitive operations require recent password confirmation. This includes at least camp export, user and permission administration, and future access to health data. The exact re-authentication interval and operation list must be finalized with the authorization and privacy model.

### Tauri single-device sessions

The Tauri WebView and loopback ASP.NET Core sidecar use a per-launch channel secret in addition to an optional user session:

- The Rust host generates a cryptographically random secret with at least 256 bits for every application start.
- The secret is passed to the sidecar at process start and is required for every sidecar API request.
- The sidecar remains bound to loopback and does not treat loopback access alone as trusted.
- The channel secret exists only in process memory. It is not written to SQLite, configuration, logs, `localStorage`, or `sessionStorage`.
- Closing Tauri terminates the sidecar and invalidates the channel secret and all local sessions.

If no optional single-device password is configured, possession of the running Tauri application and its channel secret constitutes the unlocked local session.

If a single-device password is configured:

- startup leaves the application locked
- successful local password verification creates an additional random user-session token
- the user-session token remains in memory and is required together with the channel secret
- the application can be locked manually
- 30 minutes without user activity locks the application
- restarting the application always requires a new unlock

Future sensitive operations may require recent password confirmation even when the application is currently unlocked. The operation list and confirmation interval must be finalized with the privacy and authorization model.

Before sensitive personal or health data is implemented, Tauri must use a restrictive Content Security Policy. The current spike configuration with disabled CSP is not acceptable for tokens or sensitive data because injected WebView code could act with the in-memory session.

## Consequences

- Offline capability does not require distributing central password hashes.
- The 8-character hard minimum is a usability compromise. ADR-014 requires a strength score of at least 3 for every password length.
- Argon2id adds a justified security dependency and requires benchmark, compatibility, resource-exhaustion, outdated-parameter, and rehash tests.
- Every user who needs offline access must prepare it before the local instance disconnects.
- Loss of connectivity creates an unavoidable delay for cloud-side revocation. This risk must be reflected in authorization scope, audit events, and offline-phase procedures.
- The local authentication store becomes security-sensitive and must be covered by encryption, backup, retention, and secure-deletion decisions.
- Server-rendered authentication cookies keep credentials out of Angular storage but require HTTPS, CSRF protection, cookie-key protection, and explicit session invalidation tests.
- The Tauri channel secret protects the loopback sidecar from unrelated local callers but is not a replacement for an optional user password or operating-system security.
- Tauri implementation requires cryptographically secure secret generation, request authentication, inactivity tracking, manual locking, secret redaction, and CSP tests.
- Cloud password reset requires generic responses, single-use token tests, expiration tests, rate limiting, session invalidation, offline-verifier invalidation, and audit tests.
- Single-device recovery requires one-time-display, invalid-code, brute-force protection, session invalidation, security-state rotation, no-bypass, and future encryption-key compatibility tests.
- Package format version 1 and the rule that user data is not replaced during package return remain unchanged.
- Implementation must follow ADR-010 through ADR-012 and ADR-014 for identity storage, tenant isolation, roles, permissions, password strength, and security auditing. The Windows 10 compatibility benchmark, the required audit/package-security validation, and the privacy lifecycle remain prerequisites for their affected production releases.
