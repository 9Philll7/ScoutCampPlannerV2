# UX Verpflegung V4 – operative Versorgungsplanung

Stand: 2026-11-01. Frontend umgesetzt, kein Commit. API, Domain, Datenbank, Berechtigungen und Offline-Paketformat unverändert. Bereits vorhandene V3-Änderungen bleiben erhalten.

## Grundlage

Auftrag `.codex/UX_Verpflegung_Implementierung.md`, Fachmodell und Original-PNGs `08_operative_mahlzeit_automatisch_geloest.png`, `09_operative_versorgungsplanung.png`, `10_operative_mahlzeit_offener_konflikt.png` ausgewertet. Referenz 09 bestimmt die Navigation innerhalb einer gewählten Einheit; keine dauerhafte zweite Kocheinheitenliste übernommen.

Vorher lagen Auswahl, Planmodus, Mengen-/Strukturüberschreibungen, individuelle Rezeptwahl, Versorgungslösungseditor und Verifikation als lange editierbare Karten hintereinander. Die bestehenden Endpunkte unterstützen diese Fachoperationen bereits; sie bleiben ausdrücklich getrennt.

## Umsetzung

- Neuer OperativeMealWorkspaceComponent: links aktive Mahlzeiten nach Datum und gespeicherter Reihenfolge, rechts ein einzelner Mahlzeitenkontext mit geplantem Angebot, Standard-/Alternativrezepten und Bedarfs-/Berechnungsstreifen. Direkte Übersicht-/Konfliktziele und Rückweg zur gewählten Einheit bleiben erhalten.
- Normalansicht überwiegend lesend. Planmodus, eigene Rezepte, Bedarfsüberschreibung, Komponentenziele und eigene Struktur liegen unter „Weitere Anpassungen“. Getrennter Entwurf mit ausdrücklichem Speichern; Standardplanwiederherstellung bleibt zunächst im Entwurf. Strukturreset nutzt seinen vorhandenen separaten Endpunkt.
- Versorgungslösungen sofort sichtbar: Rezept, Portionen, automatische/manuelle Herkunft, zugeordnete Anforderungen und Zutatenanpassungen. Mengen/Positionsdetails gezielt aufklappbar. Eindeutige automatische Lösungen öffnen keinen Dialog.
- Konflikte und Ask-Entscheidungen besitzen gezielte Einstiege. Exklusive Gemeinsam-/Getrennt-Auswahl, Positionsentscheidungen, manuelle Herkunft und Supply-Versionierung bleiben erhalten. Bearbeitung und Verifikation haben getrennte Dialoge. Native Auswahlfelder vermeiden Material-Overlayprobleme über nativen modalen Dialogen.
- Schätzung, reale Teilnehmerbasis und Fallback ausdrücklich beschriftet. Komponentenportionen werden nicht zu vermeintlichen Teilnehmerzahlen addiert. Unbekannter Berechnungsstand, veraltete Berechnung, offene Versorgung und manuelle Verifikation bleiben unterschiedliche Zustände.
- Anforderungen werden aus strukturierten Signaturen bzw. vorhandenen älteren Schlüsseln beschrieben. Namen ausschließlich aus dem bestehenden Health-geschützten /participant-filters-Referenzkatalog; keine Teilnehmerprofile dafür geladen. Ohne Zugriff oder bei unbekannten Referenzen generische lesbare Angaben statt GUIDs/Gruppennummern.
- Mahlzeiten-/Bereichswechsel, Dialogschließen/Escape und Browser-Unload schützen echte Entwürfe. Neuberechnung überschreibt keine offenen Entscheidungen. Laufende Vorgänge sperren konkurrierende Änderungen. Eine aktualisierte Übersicht ersetzt offene Supply-Entscheidungen nicht; deren ursprüngliche Version bleibt beim Speichern erhalten.
- Eingefrorene Lager sperren auch Verifikation. Verifikation bleibt unabhängig vom allgemeinen Bearbeitungsrecht; separate Berechtigungsprüfung unverändert im Backend.

## Grenzen und Vertragslücken

- Kein canVerify im bestehenden Übersichtsvertrag. Die UI erläutert das gesonderte Recht; abgewiesene Verifikation zeigt den Fehler. Kein Recht aus dem Bearbeitungsstatus abgeleitet.
- Kein Bildvertrag für diese Rezeptoptionen: ehrliche Platzhalter. Keine erfundenen Eignungsbadges pro Komponente. Problemreferenzen lassen sich nicht immer sicher einer einzelnen Komponente zuordnen; tatsächliche Probleme deshalb auf Mahlzeitenebene dargestellt.
- Bedarfsstreifen zeigt die vorhandene wirksame Portionszahl, keine neu abgeleitete Anwesenheitszahl. Zutatenmengen bleiben der letzte Berechnungssnapshot.
- Neue Clientmethode nutzt ausschließlich einen bestehenden Referenzendpoint. TypeScript bildet bereits ausgelieferte Signaturfelder zusätzlich ab; keine Backendänderung.

## Prüfung

Abschließende automatisierte Ergebnisse werden nach dem Lauf ergänzt. Neue Tests: unbekannte/veraltete Zustände, humanisierte Anforderungen, Mahlzeitenreihenfolge, lokale Entwürfe, Fehler, Freeze, Navigationsschutz und konkurrierende Vorgänge. Bestehende Ask-/manuelle Entscheidungsfälle bleiben Teil der Regression.

Edge/localhost:4200 meldet „The browser access host is unavailable“. Original-PNGs inspiziert; die gerenderte Oberfläche konnte nicht visuell gegengeprüft werden. Keine neuen Screenshots und kein behaupteter Browsernachweis. Manuelle Abnahme: docs/testing/catering-operative-planning-ux.md. Keine fachlichen Testlagerdaten verändert.

Automatisierter Nachweis: 193 Tests in 29 Dateien erfolgreich. Nach Entfernung der alten ungenutzten Kartenimporte zusätzlich 18 Integrationstests erfolgreich. Auch der finale Production-Build nach dem Aufräumen ist erfolgreich. git diff --check ohne Fehler (nur bestehende CRLF/LF-Hinweise).


## Nachtrag: ausdrücklich freigegebene Backend-/API-Anpassung

Die frühere Frontend-only-Grenze wurde durch den Auftrag „Passe die API an bevor ich weiter teste“ aufgehoben. Der aktuelle Vertrag und die Implementierungsdetails stehen in docs/architecture/cooking-together-api.md. Das Backend liefert geprüfte Auswahlgruppen und beide Ask-Szenarien, speichert die bestätigte Richtung versioniert und bündelt kompatible gemeinsame Versorgung. Die Oberfläche nutzt diese Daten einschließlich Varianten direkt. Alte Rezeptarrays bleiben lesbar; neue Szenarioentscheidungen nutzen dieselbe JSON-Spalte. Offline-Modulversion 5, abwärts lesbar für Versionen 1–4, keine DB-Migration.

Nachweis: 326 Cateringtests, 11 HTTP-Rechte-/Scope-Tests, 19 Pakettests und 213 Frontendtests erfolgreich. Solution-Build ohne Warnungen/Fehler, Frontend-Produktionsbuild erfolgreich. Lokale API auf localhost:5180 mit derselben bestehenden Testdatenbank neu gestartet und per /health geprüft (healthy/Sqlite). Alte Berechnungen sind durch die neue Fingerprint-Version veraltet und müssen vor der weiteren Abnahme neu berechnet werden. Kein Commit und keine neue visuelle Browserprüfung.
