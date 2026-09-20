# Architecture Baseline Status

Architecture-spike evaluation: 2026-08-08

Last product-status review: 2026-09-19

## Current state

The Architecture Spike is complete and the repository is the validated technical baseline for subsequent development. It is not a product release.

Validated capabilities:

- Modular Monolith with enforced boundaries for Platform, Camp, and Catering
- separate module-owned Domain, Infrastructure, contracts, and EF Core contexts
- shared Domain and Application logic with PostgreSQL and SQLite
- module separation through PostgreSQL schemas and SQLite table naming
- provider-specific, module-owned EF Core migrations with tested SQLite and PostgreSQL upgrades
- shared transactions and atomic rollback across module contexts
- version 1 camp-package roundtrip from cloud-style PostgreSQL to local SQLite and back
- preservation of IDs and relationships, controlled freeze, baseline validation, atomic replacement, and rejection of invalid returns
- ASP.NET Core REST API, OpenAPI, Angular integration, and production frontend build
- Docker-based backend/PostgreSQL operation
- Tauri desktop operation with an ASP.NET Core sidecar, persistent SQLite database, process lifecycle handling, and generated MSI/NSIS bundles

The detailed evidence is in [ADR-006](../decisions/adr-006-architecture-spike-validation.md) and the [Architecture Spike report](../spike/results.md).

## Technical basis

- Backend: .NET 10, ASP.NET Core, Entity Framework Core, REST, OpenAPI
- Frontend: Angular 22, TypeScript, Angular Material, Angular CDK
- Server database: PostgreSQL 18
- Single-device database: SQLite
- Local server deployment: Docker Compose with ASP.NET Core and PostgreSQL
- Windows desktop: Tauri 2 with a self-contained ASP.NET Core sidecar; Windows 11 x64 is supported and security-maintained Windows 10 22H2 x64 is tolerated as a transitional best-effort platform under ADR-013
- Architecture: Modular Monolith with directional contracts and module-owned persistence
- Offline transfer: versioned ZIP camp package with JSON payload and SHA-256 checksum; no automatic synchronization or merge

## Still open

The following decisions are required before the affected areas are production-ready:

- operational backup-restore validation on a packaged clean-machine installation; migrations and automatic SQLite pre-upgrade backups are defined by ADR-008
- a package migration registry, historic compatibility fixtures, and a supported compatibility window
- productive package-version-2 serialization, version migration, and integration of the validated encryption and transfer-signature candidate; package version 1 remains checksum-only
- general cross-module business/audit transaction coordination, checkpoint advancement, startup verification, and remaining use-case integration; the Platform-owned contract, EF migrations, active-key loading, canonical HMAC encoding, protected host initialization, standalone append transaction, and Platform-local shared business/audit transaction are present for PostgreSQL and SQLite. Setup, authentication, and the first Platform/Camp creation transaction are integrated
- Windows 10 compatibility benchmarking of the defined single-device Argon2id profile, legal retention review, and privacy lifecycle; authentication, identity, authorization, password security, audit integrity, recovery, retention mechanics, and package-version-2 security binding are defined by ADR-009 through ADR-012 and ADR-014 and technically validated by the focused security validations
- retention, archival, deletion, and anonymisation rules for personal and health data
- a stable API Problem Details contract
- clean-machine installer/removal, normal desktop shutdown, minimum-hardware validation, and the Windows 10 compatibility smoke check defined by ADR-013

The earlier Angular development-server advisory recorded by the spike is no
longer present in the installed dependency tree; `npm audit` reported no known
vulnerabilities on 2026-09-19. Dependency auditing remains a recurring release
check rather than a closed architectural guarantee.

ADR-007 records the architecture risks and required validation without pre-deciding their solutions.

## Next development step

Before implementing health data or other sensitive workflows, resolve the remaining security, authorization, and privacy decisions in ADR-007. Authentication modes are defined by ADR-009 and the production database migration strategy by ADR-008.

Tenant authentication and the first basic Camp workflow are now implemented: an authorized tenant user can create a named and dated camp, explicitly assign at least one active tenant member as `CampAdmin`, see only camps granted through an active camp membership, and change name or period with `camp.edit`. The Camp structure is a neutral tree that supports both freely varying depth and an administrator-defined, named maximum depth while still allowing new nodes. Creating, constrained deletion, and rule-compliant movement of structure branches are implemented. Tenant stage templates, stable camp copies, anonymous `KiJu` and `Leiter` estimates per stage and leaf node, and derived planning totals are implemented. Catering owns tenant food factors, stable camp-specific copies, weighted food-planning totals, and a configurable daily meal schedule with deliberate activation per camp day.

The recipe and ingredient foundation is also implemented: scoped recipe libraries, draft and publication workflows, immutable revisions, ingredient groups and positions, position-specific replacements, publication conflict feedback, revision updates, nutrition profiles and recipe nutrition estimates, revisioned central/tenant/camp ingredients, variants, conversions, central contribution/replacement workflows, and immutable recipe/ingredient reference closure in camp packages. The current uncommitted increment adds local BLS-4.0 suggestions for explicitly selected nutrition and quantitative substance estimates; Open Food Facts remains optional future work.

Mahlzeitenplanung Inkrement 1 ist umgesetzt: veröffentlichte Rezeptrevisionen können in versionierten Plänen konkreten Mahlzeiten zugeordnet, von Catering-eigenen Kocheinheiten abonniert oder je Slot individuell gewählt und aus anonymen Camp-Planungszahlen berechnet werden. Struktur- und Bedarfsabweichungen, nachvollziehbare Snapshots, Veraltungsstatus, Löschblockaden und der vollständige Offline-Replace sind enthalten.

Der nächste fachliche Schritt ist ein getrennt zu planendes Inkrement 2 für personalisierte Anforderungen und eindeutige Ersatzauflösung. Es darf erst begonnen werden, wenn dessen Anforderungskatalog und Datenschutzgrenzen bestätigt sind. Personalisierte Teilnehmer- und Gesundheitsdaten bleiben bis zur Klärung der Datenschutz- und Paketsicherheitsentscheidungen außerhalb des Scopes. Finance, Program, Material und erweiterte Teilnehmer-/Gesundheitsfunktionen bleiben spätere eigene Inkremente.
