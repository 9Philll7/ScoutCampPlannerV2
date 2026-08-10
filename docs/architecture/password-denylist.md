# Password Denylist

## Purpose

ScoutCampPlanner checks every prospective password against a local list of commonly used and compromised complete passwords. This check works identically in cloud, local-server, offline, and single-device operation and never requires a runtime internet request.

The denylist complements the zxcvbn-compatible estimator and authentication rate limiting. It is not a password-verifier store and is not used to authenticate users.

## Dataset policy

The production dataset contains the 100,000 entries with the highest occurrence counts from a dated Have I Been Pwned Pwned Passwords snapshot plus a small reviewed set of product-specific complete values such as `ScoutCampPlanner`.

- HIBP remains identified as the source in distribution and repository documentation.
- The full HIBP source corpus and the generated production denylist are not committed to Git.
- The production-generation process accepts the canonical, strictly hash-sorted HIBP `SHA1:count` representation, validates every record, rejects duplicate or out-of-order hashes, selects the most frequent entries, and finally sorts the selected hashes for lookup.
- Usernames, email addresses, tenant names, camp names, and other user data are never written to the static dataset. They are supplied only to the in-memory strength estimator for the current request.
- A new snapshot is generated for each application release and at least once per quarter when releases are less frequent.
- Cloud, local-server, and single-device artifacts for one release carry the same dataset version.

NIST guidance does not require an exhaustive blocklist and warns that an excessively large list provides little incremental protection against rate-limited online guessing. The 100,000-entry limit is an initial operational decision and must be reviewed with rate-limit behavior and rejected-password metrics before it is increased.

## Comparison semantics

The complete password is encoded as UTF-8 exactly as entered and hashed with SHA-1 for lookup in the HIBP-compatible dataset.

- Comparison is case-sensitive.
- The password is not trimmed, normalized, case-folded, or split into substrings.
- SHA-1 is used only as a dataset identifier and lookup key. It is not used for password verification, integrity, signing, or key derivation.
- A theoretical SHA-1 collision can only cause an additional password rejection in this use case; it cannot make a listed password pass the lookup.
- Temporary UTF-8 password bytes and lookup-hash buffers are cleared where the managed runtime permits it.

The rejection response tells the user that the complete password is commonly used or known from compromised-password data. It does not expose occurrence counts or whether the match came from HIBP or the product-specific additions.

## Binary format version 1

Integers use big-endian byte order. Hash entries are exactly 20 bytes, strictly increasing, and unique.

| Field | Size | Meaning |
| --- | ---: | --- |
| magic | 8 bytes | ASCII `SCPDLST1` |
| format version | 2 bytes | currently `1` |
| lookup algorithm | 1 byte | `1` means HIBP-compatible SHA-1 lookup |
| reserved | 1 byte | must be zero |
| source date | 4 bytes | `YYYYMMDD` |
| dataset-version length | 2 bytes | UTF-8 byte count, 1 through 64 |
| reserved | 2 bytes | must be zero |
| dataset version | variable | non-secret release identifier |
| entry count | 4 bytes | 0 through 1,000,000 |
| sorted lookup hashes | `count × 20` bytes | fixed-width binary SHA-1 values |
| integrity hash | 32 bytes | SHA-256 of every preceding byte |

The parser rejects truncated files, unsupported versions or algorithms, invalid dates or UTF-8, impossible counts, mismatched lengths, unsorted or duplicate hashes, non-zero reserved fields, and failed integrity checks.

The SHA-256 trailer detects corruption but does not establish publisher authenticity. Authenticity comes from the signed application/container release that carries the dataset. A future independently distributed denylist update requires its own signature and rollback-protection design before it is supported.

## Size and lookup

A 100,000-entry version-1 file is approximately 2 MB plus its small header. The validated reader loads the binary hashes once and performs binary search without storing plaintext entries.

The focused implementation and synthetic fixtures are located in:

- `tools/ScoutCampPlanner.PasswordDenylist`
- `src/backend/ScoutCampPlanner.Platform.Infrastructure/Authentication/BinaryPasswordDenylist.cs`
- `tests/ScoutCampPlanner.SecuritySpikeTests/Fixtures/denylist-source.txt`
- `tests/ScoutCampPlanner.SecuritySpikeTests/DenylistFileTests.cs`
- `tests/ScoutCampPlanner.SecuritySpikeTests/PwnedPasswordsGeneratorTests.cs`

The fixture is synthetic and is not a production security dataset.

## Generator

The repository contains a dependency-free generator that reads the HIBP source sequentially and retains only the configured Top-N candidates in a bounded priority queue. Its memory use depends on the selected output count rather than the source-corpus size.

```text
dotnet run --project tools/ScoutCampPlanner.PasswordDenylist -- \
  --input <hibp-source-file> \
  --output <denylist-file> \
  --dataset-version <version> \
  --source-date <yyyy-MM-dd> \
  --entries 100000
```

The command refuses to overwrite an existing output unless `--overwrite` is supplied. It writes to a temporary file in the target directory and moves the completed file into place. Successful output reports source count, selected HIBP count, product-specific count, output size, and the SHA-256 hash of the complete release asset as JSON.

The current reviewed product-specific set contains `ScoutCampPlanner`. The generator hashes this value in memory and adds it when it is not already among the selected HIBP entries.

Tests validate deterministic tie-breaking, malformed input, duplicates, out-of-order input, the product-specific addition, 100,000 streamed input rows with a smaller bounded queue, and the 100,000-entry output format. The real HIBP corpus has not yet been downloaded or processed in this repository.

## Production prerequisites

Before productive authentication is complete, the repository still needs:

- acquisition and recording of a pinned real HIBP snapshot
- secure handling and cleanup of the large source download outside Git
- generation provenance and HIBP attribution in release metadata
- a checked-in expected checksum for the generated release asset
- packaging tests for cloud, Docker, and Tauri artifacts
- a release check that rejects missing, stale, or unexpected dataset versions
- operational monitoring for load failure without logging passwords

Until those controls exist, the generator, format, and Platform Infrastructure reader are production-oriented components, but the generated dataset is not yet a production release asset. The reader is consumed by the password-policy implementation, while application startup wiring and release-asset provisioning remain intentionally open.
