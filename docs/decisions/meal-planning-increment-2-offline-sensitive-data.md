# Architekturentscheidung: Offline-Transport sensibler Teilnehmerdaten während Inkrement 2

## Status

Vom Projekt fachlich bestätigt.

Diese auftragsspezifische Entscheidung bleibt unter ihrem bestätigten Dateinamen
referenzierbar; sie ersetzt oder nummeriert keine bestehende ADR um.

Umsetzungsstand 2026-10-04: Teilnehmer-Persistenz und explizite Zugriffsgrundlage
sind angelegt, der Teilnehmer-Package-Roundtrip jedoch noch nicht integriert.
Die Transportfreigabe vergibt keine Rechte. ADR-011 verlangt weiterhin explizite
Health-Rechte auch lokal; bestehende Rollen erhalten diese nicht automatisch.

## Kontext

Mahlzeitenplanung Inkrement 2 führt einen minimalen Camp-owned Teilnehmerkern ein.

Dazu gehören verpflegungsrelevante personenbezogene und potenziell sensible Angaben wie:

- Teilnehmeridentität/Anzeigename,
- Anwesenheit,
- Ernährungsform,
- Allergene,
- Unverträglichkeiten,
- individuelle Grenzwerte.

Die Mahlzeitenplanung soll weiterhin denselben funktionalen Online-/Offline-Ansatz wie Inkrement 1 unterstützen.

Die bestehende Package-Architektur verwendet Freeze/Replace ohne Merge. Eine weitergehende Security-Stufe für produktiven Transport sensibler Daten ist noch vorgesehen.

Aktuell wird in der Entwicklungsphase ausschließlich mit Dummy-/Testdaten gearbeitet.

## Entscheidung

Die Domain-, Application-, Persistenz- und Offline-Package-Funktionalität für Teilnehmer- und Sonderverpflegungsdaten darf bereits in Inkrement 2 vollständig umgesetzt werden.

Diese Daten dürfen im bestehenden Entwicklungs-Package transportiert werden, solange ausschließlich Dummy-/Testdaten verwendet werden.

Dies stellt ausdrücklich keine Produktionsfreigabe für den Offline-Transport realer personenbezogener oder sensibler Teilnehmerdaten dar.

Vor einer produktiven Nutzung mit echten Daten muss die vorgesehene Package-Sicherheitsstufe umgesetzt und separat freigegeben werden.

Dabei sollen mindestens die bereits vorgesehene Schutzrichtung für Vertraulichkeit des Paketinhalts und Integrität/Authentizität des Pakets berücksichtigt werden.

Die konkrete Security-Implementierung ist nicht Bestandteil von Mahlzeitenplanung Inkrement 2, sofern keine bestehende ADR dies anders vorgibt.

## Konsequenzen

### Positiv

- Domain- und Offline-Workflow können mit realistischen Dummy-Daten vollständig entwickelt und getestet werden.
- Es entsteht keine zweite Online-only-Fachlogik.
- Der spätere Security-Ausbau betrifft primär den Transport und nicht das Fachmodell.
- PostgreSQL-/SQLite-/Package-Roundtrip kann früh vollständig getestet werden.

### Einschränkung

- Echte sensible Teilnehmerdaten dürfen auf Basis dieser Entwicklungsentscheidung nicht als produktionsfreigegebenes Offlinepaket behandelt werden.
- UI, Projektdokumentation und Abschlussbericht müssen diese Grenze klar benennen.
- Eine spätere Produktionsfreigabe benötigt eine eigene Security-Prüfung bzw. bestehende vorgesehene Package-v2-/Security-Entscheidung.

## Unverändert

Das bestehende Offline-Betriebsmodell bleibt bestehen:

- Cloud -> Package -> lokale Bearbeitung -> Return Package -> Cloud Replace,
- Freeze während aktiver Offlinephase,
- lokale Daten während der Offlinephase führend,
- kein automatischer Merge,
- atomarer Replace beim Rückimport.

Diese Entscheidung ändert keine Modulgrenze und keine Ownership-Regel.
