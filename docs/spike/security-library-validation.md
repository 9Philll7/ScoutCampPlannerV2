# Password Security Library Validation

## Status

Completed on 2026-08-10 for the initial Argon2id and password-strength library candidates.

This focused spike validates a technical dependency and benchmark harness. It does not implement user accounts, password storage, login, sessions, or authorization.

## Argon2id candidate

`Konscious.Security.Cryptography.Argon2` version 1.3.1 was evaluated because it:

- implements Argon2id in managed .NET code
- exposes memory, iteration, parallelism, salt, secret, and associated-data parameters explicitly
- runs without an additional native runtime on Windows and Linux
- is MIT-licensed
- has a substantially larger usage base than the newer candidates considered

The package was last released in 2024. This is acceptable for the validation result but requires dependency monitoring and review before every security-sensitive upgrade. A newer version must not be adopted automatically without repeating compatibility and benchmark checks.

The only observed transitive package is `Konscious.Security.Cryptography.Blake2` version 1.1.1.

## Password-strength candidate

`zxcvbn-core` version 7.0.92 was evaluated because it:

- provides the required zxcvbn-compatible score from 0 through 4
- detects common words, repetitions, sequences, keyboard patterns, dates, and context-specific user inputs
- runs directly in managed .NET on Windows and Linux without adding a Node.js runtime
- has no additional transitive package dependency
- is MIT-licensed

The stable package was last released in 2021 and is no longer considered actively maintained. This is a material maintenance risk. It is accepted because the component is an estimator rather than a cryptographic primitive, remains secondary to the mandatory versioned denylist, and will be isolated behind a ScoutCampPlanner-owned Infrastructure adapter with golden compatibility tests. A maintained compatible replacement is preferred when one becomes available.

The library's `Result` object contains the evaluated plaintext password. Product code must never return, persist, cache, serialize, or log that object. The Infrastructure adapter may expose only an application-owned result containing the numeric score and neutral reason identifiers.

`dotnet list package --vulnerable --include-transitive` reported no known vulnerable direct or transitive package from the configured NuGet sources on 2026-08-10.

## Validation harness

The isolated harness is located at:

- `tools/ScoutCampPlanner.SecuritySpike`
- `tests/ScoutCampPlanner.SecuritySpikeTests`

It is intentionally not referenced by Platform, Camp, Catering, or the API.

The automated checks prove:

- compatibility with the official Argon2id version-19 test vector
- different verifiers for the same password with independent salts
- deterministic verification when the stored parameters are reused
- detection of stored parameters that require an upgrade
- use of `CryptographicOperations.FixedTimeEquals` by the planned integration boundary
- an application-level concurrency gate for resource-exhaustion control
- stable expected scores for common, repeated, generated, and passphrase fixtures
- acceptance of Unicode and spaces without normalization by the estimator
- Unicode-scalar length counting independent from UTF-16 code-unit count
- versioned denylist metadata, frequency selection, deduplication, exact UTF-8 lookup, and corruption detection
- a 100,000-entry synthetic denylist with an approximately 2 MB binary representation
- use of application-specific input to reduce context-dependent strength
- completion at the 128-character policy boundary
- the plaintext-bearing `Result` behavior that the future adapter must contain

The spike does not claim that the library's complete implementation was independently cryptographically audited.

## Benchmark results

Measurements use Release builds, five sequential derivations per profile after one warm-up derivation, and report the median. They are local engineering measurements, not universal performance guarantees.

| Environment | Profile | Parameters (`m`, `t`, `p`) | Median |
| --- | --- | --- | ---: |
| Windows 11, .NET 10.0.4, 12 logical processors | interactive minimum | 19 MiB, 2, 1 | 81.9 ms |
| Windows 11, .NET 10.0.4, 12 logical processors | server profile | 64 MiB, 3, 1 | 336.6 ms |
| Docker Linux, .NET 10.0.10, 12 logical processors | interactive minimum | 19 MiB, 2, 1 | 80.5 ms |
| Docker Linux, .NET 10.0.10, 12 logical processors | server profile | 64 MiB, 3, 1 | 362.4 ms |

A subsequent constrained Docker run used the ADR-013 server minimum of 2 vCPU and 2 GB RAM. The 64-MiB/3-iteration profile reached a 353.4-ms median, while the 19-MiB/2-iteration profile reached an 83.0-ms median. Eight minimum-profile derivations remained limited to an observed peak of two concurrent operations.

Eight derivations of the 19-MiB profile were also run through a gate limited to two concurrent operations. Both Windows and Docker observed a peak of exactly two operations.

### Password-strength benchmark

The password-strength measurements run after one warm-up evaluation. Fixture names are reported instead of evaluated values so the harness establishes the same non-logging boundary required in production.

| Environment | Fixture | Score | Duration |
| --- | --- | ---: | ---: |
| Windows 11, .NET 10.0.4 | common word | 0 | 1.4 ms |
| Windows 11, .NET 10.0.4 | repeated pattern | 0 | 0.6 ms |
| Windows 11, .NET 10.0.4 | generated 13-character value | 4 | 5.0 ms |
| Windows 11, .NET 10.0.4 | Unicode passphrase | 4 | 3.8 ms |
| Windows 11, .NET 10.0.4 | 128-character pattern | 3 | 53.1 ms |
| Docker Linux, .NET 10.0.10 | common word | 0 | 1.7 ms |
| Docker Linux, .NET 10.0.10 | repeated pattern | 0 | 0.9 ms |
| Docker Linux, .NET 10.0.10 | generated 13-character value | 4 | 5.5 ms |
| Docker Linux, .NET 10.0.10 | Unicode passphrase | 4 | 3.9 ms |
| Docker Linux, .NET 10.0.10 | 128-character pattern | 3 | 58.6 ms |

## Result

`Konscious.Security.Cryptography.Argon2` 1.3.1 is accepted as the initial Argon2id implementation for ScoutCampPlanner Infrastructure.

`zxcvbn-core` 7.0.92 is accepted as the initial password-strength estimator for ScoutCampPlanner Infrastructure despite its maintenance limitation. It must be replaceable without changing Application or Domain contracts.

The acceptance is subject to these constraints:

- Domain and Contracts projects must not reference the package.
- Stored verifiers must use a ScoutCampPlanner-owned, versioned representation containing algorithm version and all verification parameters. The library does not provide the complete persistence contract for the application.
- Salt generation must use the platform cryptographic random-number generator.
- Derived values must be compared with `CryptographicOperations.FixedTimeEquals`.
- Password verification must use a configured concurrency limit and authentication rate limiting.
- Password bytes and derived temporary buffers must be cleared where the managed API permits it.
- Parameter upgrades occur only after successful verification.
- Package vulnerability monitoring must cover the direct and transitive dependency.
- The zxcvbn library result must remain inside its adapter because it contains the plaintext password.
- Golden score fixtures must detect behavioral changes when replacing or upgrading the estimator.
- Backend policy combines length, zxcvbn score, and the independent denylist; the estimator alone never decides password acceptance.

The server parameter profile is accepted as 64 MiB memory, three iterations, one lane, a 16-byte salt, and a 32-byte derived value, with at most two concurrent password derivations per application instance. It was validated against the initial 2-vCPU/2-GB container baseline.

The initial single-device implementation profile is 19 MiB memory, two iterations, one lane, a 16-byte salt, and a 32-byte derived value, with at most two concurrent password derivations. It is the secure lower-bound candidate and was measured successfully on the available Windows 11 development machine. No maintained Windows 10 test device is currently available. The Windows 10 22H2 compatibility measurement required by ADR-013 therefore remains a release-readiness check; it does not block implementation and must not lead to parameters below this floor.

## Still open

- Windows 10 22H2 measurement of the initial single-device profile on representative ADR-013 minimum hardware
- acquisition of a pinned real HIBP snapshot plus attribution, packaging, and update-process validation for the generated denylist asset
- the productive password-verifier persistence and rehash workflow

These points must be resolved before productive authentication is considered complete.
