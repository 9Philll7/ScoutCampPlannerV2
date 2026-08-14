# ADR-014 Password Strength Without a Separate Denylist

## Status

Accepted

## Context

ADR-009 originally required a release-local denylist containing the 100,000 most frequent entries from a dated HIBP Pwned Passwords snapshot. Producing that small runtime artifact requires processing the complete, very large source corpus and adds a separate acquisition, provenance, packaging, update, and failure path.

This operational cost is disproportionate for ScoutCampPlanner's initial scope. The application already uses a local zxcvbn-compatible strength estimator and server-side rate limiting remains required.

## Decision

ScoutCampPlanner does not maintain or distribute a separate password denylist.

- Every password from 8 through 128 Unicode scalar values is evaluated by the backend strength estimator.
- A score of at least 3 on the 0-to-4 scale is required for every password length.
- Long passwords therefore have no composition rule, but predictable long passwords may still be rejected by the estimator.
- The estimator runs locally. Passwords or password-derived lookup values are not sent to HIBP or another external service.
- Frontend feedback remains advisory; the backend result is authoritative.
- Authentication rate limiting remains required independently of password strength.

This decision supersedes the denylist requirements and the previous strength-check exception for passwords of 15 or more characters in ADR-009. The historical security spike remains evidence of what was evaluated, not a current requirement.

## Consequences

- No large password corpus, binary denylist, generator, release asset, or quarterly dataset update is required.
- Password setup and changes have a single local policy dependency.
- The application cannot identify every password known from a breach independently of the estimator's bundled frequency data.
- The estimator dependency becomes a stronger security control and must remain isolated behind the application-owned password-policy contract, covered by compatibility tests, and reviewed before replacement or major upgrades.
- Rate limiting and secure Argon2id verification remain essential.
