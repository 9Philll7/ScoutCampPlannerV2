# ADR-013 Desktop Platform Support

## Status

Accepted

## Context

The single-device operating model uses Tauri, a self-contained ASP.NET Core sidecar, and SQLite on Windows. ScoutCampPlanner is intended for volunteer organisations where older computers remain common, so rejecting every Windows 10 installation immediately would create a practical adoption barrier.

Windows 10 Home and Pro reached general end of support on 2025-10-14. Microsoft provides time-limited Extended Security Updates for eligible Windows 10 installations, and some LTSC editions have separate lifecycle dates. .NET 10 currently supports Windows 10 22H2 x64, but .NET support remains conditional on the operating-system lifecycle. ScoutCampPlanner must therefore distinguish technical compatibility from a security-support promise.

## Decision

### Supported desktop platform

Windows 11 x64 is the supported and release-tested single-device platform.

The initial minimum hardware baseline is:

- 64-bit x86 processor
- at least four logical processors
- at least 8 GB RAM
- an operating-system installation receiving current Microsoft security updates
- a supported WebView2 runtime
- sufficient local storage for the application, SQLite data, pre-upgrade backups, exports, and temporary package processing

Windows on Arm is not supported initially because the validated desktop sidecar and packaging path target `win-x64`. Arm64 support requires its own build, packaging, native-dependency, and clean-machine validation.

### Tolerated Windows 10 compatibility

Windows 10 version 22H2 x64 is tolerated as a transitional best-effort platform when the concrete installation still receives Microsoft security updates through an applicable ESU or LTSC lifecycle.

- ScoutCampPlanner does not deliberately block startup solely because the operating system reports Windows 10.
- Clean-machine smoke checks cover Windows 10 22H2 when a maintained test installation is available, but Windows 11 remains the release reference.
- Windows-10-specific defects are fixed when reasonably possible and when the underlying .NET, Tauri, WebView2, and packaging dependencies still support the platform.
- Reproduction on Windows 11 may be required before a defect is treated as a release blocker.
- No promise extends Windows 10 compatibility beyond the support offered by required runtime dependencies.

This tolerance is reviewed for every desktop release and may be removed by a later documented decision when security updates or required runtime support are no longer reasonably available.

### Unpatched Windows installations

An operating system that no longer receives applicable security updates is not a supported environment for sensitive ScoutCampPlanner data.

- Initial releases do not implement a hard operating-system block because edition and ESU status cannot be inferred reliably from a simple Windows version check.
- The application and documentation must warn when an obsolete or unverified platform is detected or reported.
- Health data and other specially protected personal data must not be enabled on a known-unpatched Windows installation.
- Antivirus software is not treated as a replacement for operating-system security updates.
- Support may require upgrading Windows, enrolling in the applicable update program, or moving the data to a supported device before further diagnosis.

### Security calibration

The desktop Argon2id profile is calibrated against the four-logical-processor and 8-GB baseline. Windows 10 tolerance means the final calibration must include a maintained Windows 10 22H2 x64 test device or an equivalently constrained representative device before the profile is declared final.

The current 19-MiB, two-iteration, single-lane Argon2id profile remains a candidate rather than a final production parameter set until that measurement is recorded. Operating-system tolerance does not permit lowering the documented secure parameter floor for one individual device.

## Consequences

- Windows 11 x64 is the primary platform for installer, shutdown, upgrade, recovery, and security testing.
- Windows 10 compatibility remains useful to existing groups without presenting an unsupported operating system as equally secure.
- Release notes must state the current Windows 10 compatibility window and security-update requirement.
- Desktop diagnostics need to record non-secret operating-system and architecture information without collecting user data.
- A future health-data release requires an explicit platform-security check and user-facing handling for unpatched devices.
- Windows Arm64 and other desktop operating systems remain separate future decisions.

## Required tests

Before the first desktop product release:

- install, start, use, upgrade, and remove the packaged application on a clean Windows 11 x64 minimum-baseline machine
- repeat a best-effort smoke test on a security-maintained Windows 10 22H2 x64 machine
- verify WebView2 availability and documented recovery behavior
- verify SQLite pre-upgrade backup and restore on the packaged application
- benchmark the selected Argon2id desktop profile on representative minimum hardware
- verify that unsupported-platform warnings contain no false claim that ESU status was technically proven

## References

- [Microsoft Windows 10 end-of-support information](https://www.microsoft.com/en-us/windows/end-of-support)
- [Microsoft Windows 10 ESU enablement and eligibility](https://learn.microsoft.com/en-us/windows/whats-new/enable-extended-security-updates)
- [Microsoft Windows release information](https://learn.microsoft.com/en-us/windows/release-health/release-information)
- [Microsoft .NET supported Windows versions](https://learn.microsoft.com/en-us/dotnet/core/install/windows#supported-versions)
