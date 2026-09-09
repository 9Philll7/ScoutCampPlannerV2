# ADR-022: Weitergabe lokaler Zutaten an den zentralen Zutatenstamm

## Status

Accepted

## Context

ADR-021 erlaubt eigenständige sowie von zentralen Zutaten abgeleitete Zutaten auf Mandanten- und Lagerebene. Damit ein verlässlicher, gemeinsam nutzbarer Zutatenstamm wachsen kann, müssen erprobte lokale Zutaten kontrolliert nach zentral weitergegeben werden können. Gleichzeitig darf eine Einreichung weder lokale Arbeitsstände verändern noch ungeprüfte Angaben unmittelbar zentral veröffentlichen.

Während der lokalen Bearbeitung kann außerdem inzwischen eine passende zentrale Zutat entstanden sein. In diesem Fall soll kein unnötiges lokales Duplikat veröffentlicht werden müssen.

## Decision

Eine veröffentlichte Mandanten- oder Lagerrevision kann unmittelbar zur zentralen Prüfung eingereicht werden. Eine zusätzliche Weitergabestufe von Lager zu Mandant ist nicht erforderlich.

Eine eigenständige lokale Zutat ist ein Vorschlag für eine neue zentrale Zutat. Eine von einer zentralen Zutat abgeleitete lokale Zutat ist standardmäßig ein Änderungsvorschlag für diese zentrale Identität. Die prüfende Person darf eine Einreichung stattdessen einer anderen bereits bestehenden zentralen Zutat zuordnen, um Dubletten zu vermeiden.

Einreichungen sind unveränderliche Verweise auf die konkret eingereichte veröffentlichte Revision. Für dieselbe lokale Revision kann nur eine Einreichung bestehen. Die lokale Zutat bleibt durch Einreichen, Annehmen oder Ablehnen unverändert.

Annehmen erzeugt ausschließlich einen neuen zentralen Entwurf. Bei einer neuen zentralen Zutat wird eine neue Identität angelegt; bei Zuordnung zu einer bestehenden zentralen Zutat entsteht deren nächste Revision. Die Prüfstati für Allergene, Unverträglichkeiten und Herkunft werden auf `Unreviewed` zurückgesetzt. Zentrale Prüfung und Veröffentlichung erfolgen anschließend ausdrücklich über den bestehenden Revisionsworkflow.

Mit der erfolgreichen Veröffentlichung dieses zentralen Entwurfs wird die eingereichte lokale Identität automatisch durch die zentrale Identität abgelöst. Das geschieht nur, wenn die eingereichte Revision weiterhin der aktuelle lokale Veröffentlichungsstand ist und kein neuer lokaler Entwurf besteht. Wurde lokal inzwischen weitergearbeitet, bleibt die lokale Identität aktiv und muss bewusst abgeglichen werden.

Vor dem Veröffentlichen einer lokalen Zutat werden passende veröffentlichte zentrale Zutaten anhand des normalisierten Namens angeboten. Die verantwortliche Person kann bewusst trotzdem lokal veröffentlichen oder die lokale Identität durch eine ausgewählte zentrale Identität ablösen. Ablösen archiviert die lokale Identität und dokumentiert das zentrale Ziel. Historische veröffentlichte Rezeptrevisionen behalten ihre exakte lokale Zutatenreferenz; bestehende Entwürfe werden nicht stillschweigend umgeschrieben.

Für Einreichung und Ablösung gelten die vorhandenen lokalen Berechtigungen `ingredients.manage`. Prüfung, Zuordnung und Annahme oder Ablehnung verwenden `ingredients.central.manage`.

## Consequences

- Der zentrale Zutatenstamm kann aus tatsächlich verwendeten lokalen Daten wachsen, ohne die zentrale Qualitätskontrolle zu umgehen.
- Annahme und zentrale Veröffentlichung bleiben zwei getrennte Schritte.
- Erst die zentrale Veröffentlichung entfernt das unveränderte lokale Duplikat aus den aktiven Mandanten- oder Lagerlisten.
- Dubletten können sowohl vor lokaler Veröffentlichung als auch während der zentralen Prüfung vermieden werden.
- Die Ablösung ist für neue Verwendungen wirksam, wahrt aber unveränderliche historische Rezeptrevisionen.
- Ein späterer Assistent zur expliziten Migration bestehender Rezeptentwürfe kann ergänzt werden; er ist nicht Teil dieses Workflows.
- Einreichungen und Ablösungen werden im gemeinsamen PostgreSQL-/SQLite-Modell persistiert.
- Das aktuelle Lagerpaket transportiert noch keine Zutatenrevisionen. Eine zentrale Einreichung ist deshalb zunächst nur in einer verbundenen Serverinstanz möglich; die spätere Paket-Erweiterung muss Zutatenrevisionen und ihre Herkunft gemeinsam behandeln.
