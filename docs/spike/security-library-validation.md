# Argon2id Library Validation

## Status

Completed on 2026-08-10 for the initial Argon2id library candidate.

This focused spike validates a technical dependency and benchmark harness. It does not implement user accounts, password storage, login, sessions, or authorization.

## Candidate

`Konscious.Security.Cryptography.Argon2` version 1.3.1 was evaluated because it:

- implements Argon2id in managed .NET code
- exposes memory, iteration, parallelism, salt, secret, and associated-data parameters explicitly
- runs without an additional native runtime on Windows and Linux
- is MIT-licensed
- has a substantially larger usage base than the newer candidates considered

The package was last released in 2024. This is acceptable for the validation result but requires dependency monitoring and review before every security-sensitive upgrade. A newer version must not be adopted automatically without repeating compatibility and benchmark checks.

The only observed transitive package is `Konscious.Security.Cryptography.Blake2` version 1.1.1. `dotnet list package --vulnerable --include-transitive` reported no known vulnerable package from the configured NuGet sources on 2026-08-10.

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

The spike does not claim that the library's complete implementation was independently cryptographically audited.

## Benchmark results

Measurements use Release builds, five sequential derivations per profile after one warm-up derivation, and report the median. They are local engineering measurements, not universal performance guarantees.

| Environment | Profile | Parameters (`m`, `t`, `p`) | Median |
| --- | --- | --- | ---: |
| Windows 11, .NET 10.0.4, 12 logical processors | interactive minimum | 19 MiB, 2, 1 | 81.9 ms |
| Windows 11, .NET 10.0.4, 12 logical processors | server candidate | 64 MiB, 3, 1 | 336.6 ms |
| Docker Linux, .NET 10.0.10, 12 logical processors | interactive minimum | 19 MiB, 2, 1 | 80.5 ms |
| Docker Linux, .NET 10.0.10, 12 logical processors | server candidate | 64 MiB, 3, 1 | 362.4 ms |

Eight derivations of the 19-MiB profile were also run through a gate limited to two concurrent operations. Both Windows and Docker observed a peak of exactly two operations.

## Result

`Konscious.Security.Cryptography.Argon2` 1.3.1 is accepted as the initial Argon2id implementation for ScoutCampPlanner Infrastructure.

The acceptance is subject to these constraints:

- Domain and Contracts projects must not reference the package.
- Stored verifiers must use a ScoutCampPlanner-owned, versioned representation containing algorithm version and all verification parameters. The library does not provide the complete persistence contract for the application.
- Salt generation must use the platform cryptographic random-number generator.
- Derived values must be compared with `CryptographicOperations.FixedTimeEquals`.
- Password verification must use a configured concurrency limit and authentication rate limiting.
- Password bytes and derived temporary buffers must be cleared where the managed API permits it.
- Parameter upgrades occur only after successful verification.
- Package vulnerability monitoring must cover the direct and transitive dependency.

The production parameter profile is not final. The 64-MiB/3-iteration profile is a viable server candidate on the measured machine, while the 19-MiB/2-iteration profile is the current secure lower-bound candidate. Final profiles require a Release benchmark on the supported minimum Windows device, the production server baseline, and the configured container memory limit.

## Still open

- final server and single-device parameter profiles
- maximum concurrent verification counts per operating model
- measurements under real container memory and CPU limits
- the maintained backend-compatible password-strength estimator
- the versioned local compromised-password denylist and update process
- the productive password-verifier persistence and rehash workflow

These points must be resolved before productive authentication is considered complete.
