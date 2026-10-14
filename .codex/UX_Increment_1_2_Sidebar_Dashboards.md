# Codex-Implementierungsauftrag – UX Increment 1.2: Sidebar & Dashboard Redesign

**Status:** Implementierungsauftrag nach UX 1 und UX 1.1. **Kein Commit ohne Freigabe.**

## Verbindliche Quellen (im Repository)

1. `docs/architecture/Visual_Design_Guide.md` (inklusive Abschnitt 9 / Ergänzung v1.1)
2. `docs/architecture/Mockup_Reference_UX_1_2.md`
3. `docs/architecture/mockups/organization-start-reference.png`
4. `docs/architecture/mockups/camp-overview-reference.png`
5. `docs/domain/Camp_Lifecycle_Phases_UX_Concept.md` (**nur Kontext; nicht implementieren**)

**Hinweis:** Falls das Repo eine andere tatsächliche Groß-/Kleinschreibung der Dokumentationspfade hat, vorhandene Pfade verwenden und Doppeldateien vermeiden. `.codex` ist das Ablageverzeichnis dieses Arbeitsauftrags.

## Ziel

Setze die bestehende Angular-Anwendungsshell und die beiden Start-/Übersichtsseiten gestalterisch nach den Referenzmockups auf eine **linke, kontextabhängige Sidebar** um. Bewahre alle vorhandenen Fachprozesse, Daten, Rechte, Offline-Funktionen, Navigationsmöglichkeiten und Speicherregeln. Die Mockups sind **visuelle**, keine fachlichen Vorgaben.

## 0. Bestandsaufnahme vor Codeänderungen

- Prüfe den Ist-Stand inklusive **uncommitteter Änderungen aus UX 1 und 1.1**. Nichts zurücksetzen oder überschreiben.
- Untersuche `AppComponent`, Navigation, Hash-Adressen und Browser-History, Komponentenstruktur, `visual-foundation.scss`, Material-Theme und bestehende Layoutregeln.
- Identifiziere bereits vorhandene reale Daten für Organisations-Start, Lagerübersicht, Offline-Zustände, Berechtigungen und offene fachliche Aufgaben.
- Erstelle eine kompakte Bestandsaufnahme: vorhandene/fehlende Daten, Ziel-/Ist-Abweichungen, Risiken, geplante Dateien. Bei notwendiger Änderung an Fachlogik/API/Berechtigungen: dokumentieren, **nicht** eigenmächtig durchführen.

## 1. Shell und Navigation

- Baue die **horizontale Hauptnavigation zu einer linken Sidebar** um; Richtwert Desktop 240–260 px.
- Sidebar enthält Marke, eindeutigen aktiven Organisationskontext, passende Navigationspunkte und ggf. vorhandene Benutzer-/Statusaktionen.
- **Organisationsmodus**: Start, Lager, Rezepte, Zutaten, Organisation; Berechtigungen beachten.
- **Lagermodus**: Übersicht, Struktur & Teilnehmer, Verpflegung, Rezepte, Zutaten, Einstellungen, plus nur tatsächlich implementierte weitere Funktionen. Kein direkter Lagerwechsel. Ein expliziter Rückweg **„Alle Lager“** führt in den Organisationsmodus.
- Lagermodus **ersetzt** die globale Navigation, zeigt sie nicht parallel. Organisationswechsel nur außerhalb des Lagermodus.
- Behalte bestehende Hash-Deep-Links, Reload-Wiederherstellung, Browser-Zurück, Unsaved-Changes-Warnschutz, Authentifizierung, Offline-/Desktop-Zugänge und Rollen-/Rechteanzeigen bei.
- Gleiche Proportionen, Navigationselemente und aktive Zustände in beiden Modi; keine getrennten lokalen Stilmuster.
- Responsive: bei kleineren Breiten erreichbare ausklappbare/einklappbare Navigation; Tastatur und Screenreader berücksichtigen; keine App-weite horizontale Scrollleiste.

## 2. Organisationsstart – visuelles Redesign

- Richte Seitentitel, optionalen Orientierungssatz, zuletzt verwendete und zugängliche Lager anhand des Referenzmockups ruhig und konsistent aus.
- Verwende **nur tatsächlich zugängliche Lager** der aktiven Organisation und echte Verlaufsdaten.
- Für 0, 1 oder mehrere Lager eine passende, nicht künstlich aufgefüllte Darstellung; **keine Duplikate** desselben Lagers in Verlauf und weiterer Lagerliste.
- Lageröffnen klar und barrierefrei erkennbar; keine losen grünen Textlinks als Hauptmuster.
- Andere Arbeitsbereiche nicht pauschal als Duplikat der Sidebar kacheln; nur begründete hilfreiche Einstiege erhalten.
- Keine neuen Backend-Endpunkte, neuen Statuswerte oder synthetischen „Willkommen“-Inhalte nötig.

## 3. Lagerübersicht – visuelles Redesign

- Zeige Lagerkopf mit echtem Namen/Zeitraum/Organisationskontext.
- Kompakter Planungsstand nur mit **bereits vorhandenen fachlich belastbaren Daten**. Kennzahlen klar und konkret benennen; nach Möglichkeit fachbereichsorientiert und erweiterbar, aber ohne Platzhalter für künftige Module.
- Aufgabenliste konzeptionell **lagerweit**, praktisch nur für **bereits vorhandene, verlässlich ableitbare Zustände** und nur bei vorhandener Zugriffsberechtigung; jeder sichtbare Eintrag hat einen sinnvollen Zielpfad. Besteht keine solche Datenquelle, zeige einen ehrlichen Leer-/Informationszustand und dokumentiere die Lücke.
- **Strikt verboten:** Prozentanzeigen des Gesamtplanungsfortschritts, Ringdiagramme mit künstlichem Fertigstellungsgrad, allgemeine Progressbalken, erfundene offene Aufgaben, fiktive Abschlüsse und Platzhalter-Karten für noch nicht implementierte Module.
- Falls ein konkreter Status bereits fachlich existiert (etwa Offline-Schreibschutz), darf er angezeigt werden, aber klar beschriftet und nicht mit „Planung fertig“ verwechselt.
- Bestehende Aktionen auf der Lagerübersicht (insbesondere Paket-/Offlinefunktionen) dürfen nicht verloren gehen. Bei Bedarf übersichtlich als sekundäre Aktionen gruppieren; nicht verbergen.

## 4. Explizit außerhalb des Scopes

- **Kein** neues Domänenmodell, Schema, API, Rechte-/Rollenmodell oder Berechnungsverhalten.
- **Keine** Implementierung von Phasen, Zeitvorschlägen, Phasenwechsel, Archivierung oder deren Datenpersistenz.
- **Keine** neuen Module Kalkulation/Programm, kein neues Aufgabenverwaltungssystem.
- **Keine** Überarbeitung der fachlichen Rezept-, Zutaten-, Teilnehmer- und Verpflegungseditoren jenseits zwingender Shell-/Layoutkompatibilität.
- Kein Wechsel des Frontend-Frameworks, keine parallele zweite Designsystem-Bibliothek, keine externen Schrift-/Bild-CDN-Abhängigkeiten.

## 5. Technischer Ansatz

- Bestehende zentrale Tokens und Angular-Material-Infrastruktur erweitern statt duplizieren; semantische Tokens wiederverwenden.
- Klare Trennung zwischen Anwendungs-Shell, Navigationsmodell und Seiteninhalt, soweit ohne größeres Refactoring praktikabel.
- CSS sollte die beiden Ansichten konsistent mit den Mockups gestalten, ohne Mockup-Pixel 1:1 festzuschreiben.
- Vorhandene Status-/Rechte-Daten an bestehende UI anbinden; **keine** nur frontendseitige Scheinautorisierung.
- Keine gesonderten Screens nur für Mockup-Demos in der Produktionsnavigation erstellen.

## 6. Abnahmefälle

1. Aktive Organisation erkennbar; alle fünf globalen Navigationsbereiche (sofern berechtigt) erreichbar.
2. Ein Lager aus Start oder Lagerliste öffnen: Sidebar wechselt vollständig in Lagermodus, behält erkennbare Organisation und Lagername.
3. „Alle Lager“ zurück: Organisationsnavigation wiederhergestellt, kein Direkt-Lagerwechsel.
4. Deep Link, Reload, Hash-/Browser-Zurück und Navigation-Abbruch bei ungesicherten Eingaben funktionieren.
5. Berechtigungsabhängige Navigation, fremde Lager, Offline-/Schreibschutz-/Paketfunktionen unverändert.
6. Startzustände 0/1/mehrere Lager, leerer Verlauf, lange Namen, fehlender Zeitraum ordentlich.
7. Lagerübersicht mit fehlenden Kennzahlen/offenen Aufgaben: **keine** erfundenen Informationen; kein Prozent-Fortschritt.
8. 1440×900, 1024×768, 390×844: visuell prüfen (oder dokumentieren, falls Browser/Screenshot nicht verfügbar). Keyboard-Fokus und Kontraste prüfen.
9. Angular-Tests und Production Build erfolgreich; relevante Navigationstests anpassen/ergänzen. Weitergehende Tests nur wenn betroffene Funktion es erfordert.

## 7. Ergebnisse / Dokumentation

Schreibe `.codex/ux-increment-1-2-result.md` mit:
- Bestandsaufnahme und Umsetzung samt geänderten Dateien
- Abweichungen zwischen Mockups und realer Anwendung samt Begründung
- Reale verfügbare Daten für Planungsstand und Aufgaben; nicht verfügbare Informationen klar benennen
- Testergebnisse mit PASS/FAIL/NOT_RUN, inklusive visueller Abnahme und konkreten Browser-Breiten
- Restpunkte und Risiken (u. a. bestehender konservativer Dirty-State-Guard)
- Zustand des Repositories; **keinen Commit erstellen**

**Beginne mit der Bestandsaufnahme. Implementiere anschließend ausschließlich innerhalb des beschriebenen Scopes.**
