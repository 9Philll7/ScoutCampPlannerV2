# Participant development audit catalogue

This catalogue implements the ADR-012 audit prerequisite for the confirmed
increment-2 **dummy/test-data development workflow only**. It does not establish
production retention or authorization for real health data.

| Action | Result | Target |
|---|---|---|
| health.participants.read | success | camp |
| health.participant.created | success | participant |
| health.participant.updated | success | participant |
| health.participant.deleted | success | participant |
| health.participants.denied | denial | camp |

Only common actor, tenant/camp, target and instance identifiers, UTC time,
correlation and authorization catalogue version are stored. Metadata is empty.
Names, requirements, thresholds, sources, request bodies and concurrency tokens
are never recorded. A write and its success event share one transaction. Reads
must record their event before returning any data; audit failure is fail-closed.
Denied access returns the same application result as an unknown camp.

No production health-audit retention period is inferred from the existing
security defaults. This internal service must not be released for real data;
production retention remains the separate privacy/legal decision in ADR-012.
Public endpoints and package integration are not enabled by this catalogue.
