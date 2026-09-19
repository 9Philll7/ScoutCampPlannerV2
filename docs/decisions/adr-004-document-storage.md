# ADR-004 Document Storage

## Status

Accepted

## Decision

Attachments are not stored directly in ScoutCampPlanner.

External storage is preferred:
- SharePoint
- other document systems

The application stores references/links.

Reason:
- existing infrastructure
- simpler backups
- separation of concerns
