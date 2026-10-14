# Ergebnis: UX Increment 1.2 – Sidebar & Dashboards

Stand: 2026-10-14. Implementiert, automatisiert geprüft, manuelle Sichtabnahme offen.
Kein Commit erstellt.

## Bestandsaufnahme

- Grundlage: uncommittierte UX-1-/UX-1.1-Shell in AppComponent, Hash-Adressen,
  Rechteanzeige, konservativer Wechselwarnschutz, zentrale visual-foundation.scss
  und bestehendes Angular Material. Vorhandene Änderungen nicht zurückgesetzt.
- Bereinigter Visual Design Guide einschließlich Abschnitt 9 und
  Mockup_Reference_UX_1_2.md vollständig gelesen; beide PNG-Referenzen angesehen.
  Die zuvor gemeldete fehlerhafte Dokumentablage ist behoben.
- Ist-Abweichungen: horizontale Hauptnavigation, separate Lagernavigation im
  Inhalt, Organisationswechsel im Seitenkopf, doppelte Lager auf Start und
  wiederholte Bereichskacheln. Lagerübersicht bisher nur wenige Textzeilen.
- Bereits belastbar verfügbar: zugängliche Lager pro Organisation, echter
  benutzerbezogener Verlauf, Name, Zeitraum, Organisationszuordnung, lokale
  Betriebsart, Offline-Schreibschutz und vorhandene Aktionsberechtigungen.
- Keine angebundene lagerweite Aufgabenquelle oder fachlich definierte
  aggregierte Dashboard-Kennzahlen. Einzelne Fachdaten werden nicht spekulativ
  zu einem Gesamtplanungsgrad zusammengezählt. Keine Gesundheitsdaten für
  Dashboard-Zahlen angefordert.
- Geplante/bearbeitete Stellen: Shell-Template und minimale UI-Zustände,
  bestehende zentrale Styles/Icons, Navigationstests, dieser Ergebnisbericht.

## Umsetzung

- Gemeinsame linke Sidebar, Desktopbreite 252px, mit Marke, Organisation,
  ggf. Lagername, passender Navigation und Benutzeraktionen.
- Lagermodus ersetzt die Organisationsnavigation vollständig. „Alle Lager“
  führt zur bestehenden Lagerliste; kein Lagerwechsel-Menü.
- Expliziter Organisationswechsel in der Sidebar ausschließlich außerhalb
  eines geöffneten Lagers. Rechtebedingungen unverändert.
- Unter 1101px einklappbare Navigation im Seitenfluss: beschrifteter Button,
  aria-expanded/aria-controls, Escape mit Fokusrückgabe, Schließen nach
  erfolgreicher Navigation. Abgebrochene Navigation lässt das Menü offen.
  Kein modales Overlay und daher keine künstliche Fokusfalle.
- Aktiver Navigationszustand weiterhin mit aria-current, stärkerem Gewicht
  und nun linker Markierung. Gemeinsame Farben/Abstände aus UX 1.1.
- Start: Verlauf und weitere zugängliche Lager ohne Duplikate. Zustände für
  keine/ein/mehrere Lager bleiben datengetrieben. Keine künstlich gefüllten
  Raster und keine pauschale Verdoppelung der Sidebar durch Bereichskacheln.
- Lagerübersicht: echte Lagerinformationen, ehrlich gekennzeichnete fehlende
  Kennzahlen-/Aufgabenanbindung und konkrete Einstiege in bestehende Bereiche.
- Paketaktionen verbleiben unverändert unter „Alle Lager“; die Übersicht
  bietet einen sichtbaren Einstieg und erklärt den Ort der vorhandenen
  Export-/Rückimport-/Entsperr-/Entfernungsaktionen.
- Hash-Navigation, History, Authentifizierung, Warnschutz und API-Aufrufe
  unverändert weiterverwendet. Keine Fach-, Backend-, Schema- oder Rechteänderung
  durch UX 1.2.

## Mockups versus echte Anwendung

- Übernommen: Sidebar-Proportionen, Kontextgliederung, vertikale Navigation,
  ruhige Karten/Abschnitte und gemeinsame Ausrichtung.
- Keine Landschaftsbanner: laut Guide optional, Daten und Orientierung stehen
  im Vordergrund.
- Keine Beispielnamen/-zahlen, Planungsprozente, Balken, Phasen, Archivierung,
  zusätzlichen Module oder künstlichen Aufgaben übernommen.
- Planungsstand ausdrücklich ohne erfundene Zahlen. Aufgaben-Informationszustand
  bedeutet nicht „keine Aufgaben vorhanden“ oder „Planung abgeschlossen“.
- Die Seiten zeigen nur den tatsächlichen Zeitraum, die Organisation und den
  bestehenden Online-/lokalen-/Schreibschutzkontext.
- Bei 1024px und 390px wird die Sidebar adaptiv ein-/ausgeklappt statt die
  Desktop-Komposition mit zu schmalem Inhaltsbereich zu erzwingen.

## Geänderte Dateien dieses Inkrements

- src/frontend/src/app/app.component.ts
- src/frontend/src/visual-foundation.scss
- src/frontend/src/app/shared/action-icon.component.ts
- src/frontend/src/app/app-navigation.spec.ts
- .codex/ux-increment-1-2-result.md

Andere im Git-Status vorhandene Änderungen gehören zu UX 1/1.1 bzw. den
vom Benutzer hinzugefügten Auftrags-/Referenzdateien und wurden erhalten.
Keine neuen Dependencies, keine Migrationen, keine generierten Artefakte
vorgemerkt.

## Tests

- PASS: npm.cmd test -- --watch=false in src/frontend:
  68/68 Tests in 15 Dateien, darunter 11 Shell-/Navigationstests.
- PASS: npm.cmd run build in src/frontend (Production).
- PASS: git diff --check.
- Neue Tests: 0/1/mehrere Lager ohne Duplikate, Sidebar statt horizontaler
  Lagernavigation, echte Lagerdaten und ehrlicher Kennzahlen-Leerzustand,
  Menüsemantik, Escape/Fokus, Abbruch und erfolgreicher Navigationswechsel.
- Vorhandene Regressionen für Deep Links, Kontextwechsel, Rechte, Desktop-
  Paketzugang, Sessionablauf und fachliche Komponenten bleiben grün.
- Erster Testversuch: FAIL beim Start des Fork-Runners (Timeout, keine Tests
  ausgeführt). Unveränderte Wiederholung erfolgreich.
- Kontrastbasis unverändert aus UX 1.1: Text auf primary-soft 11,33:1,
  Sekundärtext 5,35:1, Fokus 4,79:1. Keine vollständige gerenderte WCAG-Abnahme.
- NOT_RUN: neue .NET-/PostgreSQL-/Installerprüfung; kein entsprechender Code
  in diesem Inkrement verändert. Frühere Nachweise nicht als neue ausgegeben.
- NOT_RUN: responsive Sichtprüfung/Screenshots 1440×900, 1024×768, 390×844.
  Browser-Skill versucht: „No browser is available“, verfügbare Browserliste leer.

## Manuelle Abnahme / Restpunkte

1. Organisation mit 0, 1 und mehreren zugänglichen Lagern sowie leerem/gefülltem
   Verlauf prüfen. Jedes Lager auf Start genau einmal; keine falschen Leerzustände.
2. Beide Modi bei 1440×900, 1024×768 und 390×844 ansehen; lange Namen,
   fehlende Zeiträume, Fokus, Kontraste und horizontale Überläufe kontrollieren.
3. Menü per Tastatur öffnen/schließen, Escape, Lager öffnen, „Alle Lager“,
   Organisationswechsel, Hash-Reload und Browser-Zurück prüfen.
4. Ungesicherte Eingaben: Navigation abbrechen und danach bewusst bestätigen.
   Der bestehende konservative Guard kann auch nach dem Speichern warnen.
5. Eingeschränkte Rechte sowie Offline-/Schreibschutzstatus und Paketaktionen
   mit vorhandenen Dummy-Daten prüfen.
6. Keine Phasen-/Fortschritts-/Aufgabenpersistenz vorhanden oder ergänzt.
   Die fehlende lagerweite fachliche Aggregation bleibt ein eigener Folgeauftrag.

Vor weiteren visuellen Anpassungen manuell abnehmen. Kein Commit.
