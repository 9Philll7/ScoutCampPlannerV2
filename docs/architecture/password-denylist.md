# Password Denylist

## Purpose

ScoutCampPlanner checks every prospective password against a local list of commonly used and compromised complete passwords. This check works identically in cloud, local-server, offline, and single-device operation and never requires a runtime internet request.

The denylist complements the zxcvbn-compatible estimator and authentication rate limiting. It is not a password-verifier store and is not used to authenticate users.

## Dataset policy

The production dataset contains the 100,000 entries with the highest occurrence counts from a dated Have I Been Pwned Pwned Passwords snapshot plus a small reviewed set of product-specific complete values such as `ScoutCampPlanner`.

- HIBP remains identified as the source in distribution and repository documentation.
- The full HIBP source corpus and the generated production denylist are not committed to Git.
- The production-generation process accepts the HIBP `SHA1:count` representation, validates every record, deduplicates hashes, retains the highest observed count, selects the most frequent entries, and finally sorts the selected hashes for lookup.
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

- `tools/ScoutCampPlanner.SecuritySpike/DenylistFile.cs`
- `tests/ScoutCampPlanner.SecuritySpikeTests/Fixtures/denylist-source.txt`
- `tests/ScoutCampPlanner.SecuritySpikeTests/DenylistFileTests.cs`

The fixture is synthetic and is not a production security dataset.

The spike builder validates selection and file creation for bounded inputs up to the proposed 100,000-entry release asset. It is not designed to load and rank the complete HIBP source corpus in memory. The productive generator must process the large source as a streaming top-N pipeline or an equivalent bounded external-sort process.

## Production prerequisites

Before productive authentication is complete, the repository still needs:

- a reproducible, reviewed generation command for a pinned HIBP snapshot
- secure handling and cleanup of the large source download outside Git
- generation provenance and HIBP attribution in release metadata
- a checked-in expected checksum for the generated release asset
- packaging tests for cloud, Docker, and Tauri artifacts
- a release check that rejects missing, stale, or unexpected dataset versions
- operational monitoring for load failure without logging passwords

Until those controls exist, the current implementation remains a validation spike rather than the production denylist service.
