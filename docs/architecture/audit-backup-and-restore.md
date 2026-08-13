# Audit Backup and Restore

## Purpose

An audit-capable ScoutCampPlanner instance can be recovered only when its database, complete audit key bundle, and authenticated external checkpoint belong to the same captured state. Copying only the database is not a valid backup.

This runbook defines the required sequence. Product-specific user interfaces, schedules, storage destinations, and provider commands remain implementation and operations work.

## Recovery set

Every recovery set contains exactly:

1. the SQLite database file or a consistent PostgreSQL database snapshot
2. the complete audit key bundle, including active, prepared, and historical keys
3. the authenticated external checkpoint

The three components share one backup identifier and capture time in operational metadata outside their protected contents. Components from different backups must never be combined.

## Backup sequence

1. Reject new writes and wait for active transactions and audit appends to finish.
2. Reconcile the external checkpoint with the committed database head.
3. Run a full audit-chain verification. Stop without producing a valid backup if it fails.
4. While writes remain blocked, capture the database, key bundle, and checkpoint.
5. Create the protected recovery artifact or provider-native recovery set.
6. Restore the artifact into an isolated temporary location and verify that all three components are readable and belong together.
7. Record a redacted operational result and resume normal writes only after the verification succeeds.

Temporary or failed artifacts are not advertised as valid backups. Logs and filenames must not contain passwords, raw keys, or protected file contents.

## Single-device Windows

The normal key file is bound to the current Windows user by DPAPI and is therefore not itself a portable backup.

- The application decrypts the key bundle only in memory while creating the recovery archive.
- The archive contains the SQLite database, plaintext key-bundle representation, and checkpoint inside one encrypted envelope.
- A user-supplied backup password that satisfies the productive password policy derives a 256-bit archive key through Argon2id.
- AES-256-GCM encrypts and authenticates the complete archive with a versioned purpose value.
- Salt and nonce are newly generated for every archive. The password is never stored.
- After restore, the key bundle is immediately protected with DPAPI for the destination Windows user before active files are replaced.

Losing both the device and the backup password makes the portable archive unrecoverable. The user interface must state this clearly before backup completion.

## Local Docker instance

The database backup, dedicated audit-security volume, and checkpoint are one operator-managed recovery set. PostgreSQL and the backend must not continue writing during capture. File ownership and owner-only permissions must be restored for the non-root application identity before startup.

The security volume must never be mounted into PostgreSQL, copied into a container image, or committed to source control.

## Cloud instance

The PostgreSQL snapshot, deployment-managed audit secret, and external checkpoint are one logical recovery set. The secret remains in the selected deployment secret manager; ScoutCampPlanner does not copy it into ordinary configuration or logs. Backup retention and version alignment must ensure the referenced historic key versions remain recoverable with the database snapshot.

## Restore sequence

1. Stop normal application operation and keep the instance in blocked mode.
2. Restore all components into staging, never directly over active state.
3. Verify archive authentication and required component presence.
4. Verify database integrity and schema compatibility without applying silent repairs.
5. Load and validate every referenced audit key and the checkpoint.
6. Run a complete audit-chain verification from the restored database through the restored checkpoint.
7. For Windows, re-protect the validated key bundle with the destination user's DPAPI context.
8. Replace active state only after every check succeeds, then perform one final startup verification.
9. Return to normal operation only after that final verification succeeds.

Any failure leaves active state unchanged and the instance blocked. Recovery never creates replacement keys, resigns events, combines backup generations, or truncates the journal.

## Validation status and limits

The spike validates the encrypted archive format, three-component round trip, independent salts and nonces, wrong-password rejection, modification rejection, and rejection of incomplete input. Existing provider and audit tests validate SQLite integrity, PostgreSQL transactional behavior, key-bundle parsing, checkpoint authentication, and complete-chain verification separately.

The in-memory archive candidate is not yet suitable for arbitrarily large productive databases. Production implementation must stream or stage large database snapshots, define backup scheduling and retention, and validate provider-specific restore commands in deployment automation.
