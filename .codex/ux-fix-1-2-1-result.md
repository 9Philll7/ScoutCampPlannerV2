# UX Fix 1.2.1 – Ergebnis

Stand: 2026-10-14. Abschluss mit Einschränkungen: automatisierte Prüfung PASS; Desktop vorläufig manuell freigegeben, letzte mobile Anpassung noch visuell zu prüfen. Kein Commit. Der Abschlussnachtrag unten ersetzt die älteren Test-/Abnahmeangaben.

## Ursachen und Änderungen

- Grünes Rechteck: Die Aktion „Zur Lagerübersicht“ hatte im Template einen festen Text, keine Rechtebedingung und keinen leeren Inhalt. Ein Material-Textbutton wurde über `.primary-action` grün hinterlegt. Die später eingebundene Material-Regel `.mat-mdc-button:not(:disabled)` hat dieselbe Spezifität wie `.start-page .primary-action` und setzt die Schrift wieder auf Primary (Grün auf Grün). Korrigiert durch `matButton=filled` mit den passenden Filled-Container-/Label-Tokens statt widersprüchlicher Textbutton-Färbung. Codebefund; visuelle Bestätigung mangels Browser noch offen.
- Start: Zusatzabschnitt zuvor immer gerendert, auch bei leerer Restliste. Jetzt vollständig verborgen, sobald alle Lager im Verlauf stehen. Bei null Lagern ein einzelner ehrlicher Leerzustand ohne Aufforderung, unten ein nicht vorhandenes Lager zu wählen. Lagerübersichtsaktion bleibt unabhängig von Listen verfügbar. Bestehende Verlaufstrennung unverändert.
- Lagerübersicht: Vier gleichgewichtige Karten durch kompakte Arbeitseinstiege für vorhandene Bereiche ersetzt. Zeitraum steht beim Lagerkopf; realer Betriebs-/Schreibschutzstatus bleibt erhalten. Nur ein kleiner Hinweis auf fehlende Kennzahlen-/Aufgabenquelle. Paketweg weiterhin über „Alle Lager“, keine neue Paketfunktion.
- Shell: Wiederholte Organisationszeile im Inhaltskopf entfernt; Kontext bleibt in Sidebar und mobiler Kopfzeile. Benutzerkennung nutzt die Sidebarbreite statt der später überschreibenden 18ch-/14ch-Begrenzung. Bestehende Umbruch-/Fokusregeln erhalten.

## Dateien und Scope

- `src/frontend/src/app/app.component.ts`: ausschließlich betroffene Templates.
- `src/frontend/src/visual-foundation.scss`: vorhandene semantische Tokens, kompakte Übersicht und Buttonkorrektur.
- `src/frontend/src/app/app-navigation.spec.ts`: gezielte Regressionen ergänzt.
- Keine Änderungen an Backend, Datenmodell, Berechtigungen, Navigation/Guards, Authentifizierung, Editoren oder Berechnungen in diesem Fix. Keine Dependencies. Vorhandene uncommittete UX-Änderungen bleiben erhalten.

## Tests

- PASS: `npm.cmd test -- --watch=false` – 70 Tests, 15 Dateien, einschließlich 13 Navigationstests.
- PASS: `npm.cmd run build` – Production Build.
- PASS: `git diff --check`.
- Git-Status geprüft: bestehender uncommitteter UX-Stand plus dieser Fix; keine neuen Build-Artefakte im Status.
- Abgedeckt: 0/1/mehrere Lager, eindeutige Verlaufeinträge, verborgener leerer Zusatzabschnitt, benannte gefüllte Aktion, kompakte echte Lagerdaten, Moduswechsel, Rechte, Deep Links/Zurück, Dirty-State-Warnung, mobile Menütaste/Fokus und bestehende Paket-/Session-Regressionen.

## Offene Sichtabnahme

- NOT_RUN: gerenderte Sichtprüfung bei 1440×900, 1024×768 und 390×844; Browser meldet „No browser is available“, Browserliste leer.
- NOT_RUN: visuelle Kontrast-, Hover-, Fokus- und Überlaufprüfung einschließlich langer Organisations-/Lagernamen und Benutzerkennungen. Automatisierte DOM-Tests sind keine visuelle Freigabe.
- Bitte manuelle Screenshots von Start und geöffneter Lagerübersicht in diesen Größen bereitstellen; Start mit null/einem/mehreren Lagern sowie langen Namen prüfen.
- Keine fachlichen Blocker oder neu erfundenen Dashboarddaten. Kein Commit erstellt.

## Abschlussnachtrag

### Verpflegungsfaktoren

- Auslöser: GET /api/camps/{campId}/catering-stage-factors beim Öffnen des Lagers. Der Aufruf und die ursprüngliche Fehlermeldung existieren bereits in HEAD vor den uncommitteten UX-Inkrementen. Nicht durch das Dashboard neu eingeführt.
- Backend: authentifizierter Endpoint, CampStageContext mit bestehender Zugriffsprüfung; fehlender Kontext ergibt 404. Anschließend Lesen der Camp-/Tenant-Faktoren durch CateringPlanningService. Keine Änderung an Berechtigungen oder Berechnung.
- Der konkrete beobachtete Request wurde nicht reproduziert; HTTP-Status und Backend-Log fehlen. Daher keine belegbare Zuordnung zu Backend, Berechtigungen oder Daten. Eine erfolgreiche Live-API-Prüfung wird nicht behauptet.
- Zwei belegte Frontendprobleme korrigiert: Fehler wurden nach Start der Requests zurückgesetzt (synchrone Fehler konnten verloren gehen); verspätete Faktorenantworten eines inzwischen verlassenen Lagers konnten den aktuellen Kontext überschreiben. Fehlerrücksetzung erfolgt jetzt vor dem Laden; Antworten gelten nur für das aktuell geöffnete Lager.
- Aktuelle Fehler bleiben sichtbar im bestehenden globalen Fehlerbereich, auch in der Übersicht, weil dort bereits geladen wird. Ergänzt: HTTP-Status bzw. Erreichbarkeitshinweis und erneutes Öffnen als Wiederholungsweg. Keine Fehlerkaschierung.
- Separater funktionaler Folgeauftrag, falls erneut beobachtet: HTTP-Status, betroffenes Lager/Betriebsmodell und zeitgleichen Backend-Log erfassen; Request mit dessen berechtigtem Benutzer reproduzieren; danach Daten-/Endpoint-/Rechteursache gezielt beheben. Keine erfundenen Faktoren oder erweiterten Rechte als Workaround.

### Responsive Navigation

- Bis 1100px bleibt das Menü initial geschlossen. Geöffnet liegt es in derselben Grid-Zeile über dem Inhalt statt diesen um die gesamte Menühöhe zu verschieben; maximal 70dvh, intern scrollbar. Kontext und Benutzeraktionen bleiben erreichbar. Oberhalb 1100px keine Änderung der Desktop-Sidebar.
- Öffnen fokussiert den aktiven Navigationseintrag. Menübutton/Escape schließen mit Fokus zurück zum Button. Fokuswechsel in den Inhalt schließt das nichtmodale Menü ohne Fokusfalle. Erfolgreiche Navigation und konservativer Warnschutz bleiben erhalten.
- Nur die reine Lagerübersicht nimmt nicht mehr an der pauschalen mobilen Karten-Overflow-Regel teil. Tabellen/Editoren unverändert.
- Keine weiteren Redesigns. Die zuvor erwähnte optionale Buttonform-/Farbangleichung wurde im ausdrücklich begrenzten Abschlussauftrag nicht zusätzlich umgesetzt.

### Prüfstand und Abnahme

- PASS: vollständige Angular-Suite, 73 Tests in 15 Dateien (16 Navigationstests). Neue Fälle: aktueller HTTP-Fehler bleibt sichtbar, verspäteter Fehler betrifft nicht das nächste Lager, Menü-Fokusübergabe und Schließen beim Fokuswechsel zum Inhalt.
- PASS: Angular Production Build; git diff --check.
- Bestehende automatisierte Abdeckung: Organisations-/Lagermodus, 0/1/mehrere Lager, Öffnen/Verlassen, Organisationswechsel, Rechtebedingungen, Deep Links/Zurück, Warnschutz, Offline-/Paketaktionen.
- Nutzer-Screenshots geprüft: Start mit leerem Verlauf und nach Lageröffnung; Lagerübersicht Desktop/Tablet/Mobil; offenes Menü. Desktop vorläufig durch Nutzer freigegeben. Nutzer bestätigt Tabulator-Fokus, Menübedienung und Warnschutz vor diesem Abschlussnachtrag als PASS.
- NOT_RUN: eigene Browserprüfung und exakte Viewportprüfung 1440/1024/390px; keine verfügbare Browseranbindung. Screenshots sind kein Beleg für eine selbst ausgeführte Browserprüfung. Die letzte mobile Menü-/Scrolländerung braucht noch eine kurze manuelle Nachprüfung bei 1024/390px, insbesondere alle Menüeinträge, Schließen, Escape und Seiten-Scroll.
- NOT_RUN: Live-Reproduktion der ursprünglichen Faktorenfehlermeldung; funktionaler Folgepunkt siehe oben. Kein Backend geändert; .NET-Suite in diesem reinen Frontend-Abschluss nicht erneut ausgeführt.
- Git-Diff/-Status geprüft: gemeinsamer uncommitteter UX-Stand einschließlich bereits vorhandener Tenant-Capability-/Rezeptnavigationserweiterungen und Dokumente. Keine neuen generierten Artefakte im Status; keine bestehenden Änderungen zurückgesetzt.

### Commit-Bereitschaft

Technisch eingeschränkt abschließbar, kein nachgewiesener neuer Fachblocker. Gemeinsamer Commit von UX 1/1.1/1.2/1.2.1 nach kurzer Sichtbestätigung der letzten mobilen Änderungen empfohlen. Nicht als uneingeschränkte visuelle Freigabe oder behobene ursprüngliche Backendmeldung deklarieren. Kein Commit erstellt.
