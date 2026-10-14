# Codex-Implementierungsauftrag – UX Increment 1.1: Visual Foundation

**Projekt:** ScoutCampPlanner  
**Arbeitsgrundlage:** `docs/Architecture/Visual_Design_Guide.md` (verbindlich)  
**Basis:** UX Increment 1 (App-Shell und kontextbezogene Navigation); siehe vorhandene Ergebnisdatei.  
**Ziel:** Den bereits funktionierenden Organisations-Start und die Anwendungsshell visuell auf den Stil „Modern Outdoor“ bringen – mit wiederverwendbaren Gestaltungsgrundlagen für spätere Screens.

## 1. Ausgangslage und überprüfbare Einschränkungen

- Das vorherige UX-Inkrement implementierte organisations- und lagerbezogene Navigation, Startseite, Hash-Deep-Links, Berechtigungsanzeige und Offlinezugänge.
- Die bisherige Startansicht wirkt uneinheitlich: gemischte Schriftcharaktere, lose Textlinks statt klarer Einträge, kaum Flächenhierarchie, starke ungenutzte Leerfläche, unruhiger Header.
- Organisationsrezepte sind derzeit nur lesend integriert; **keinen** Tenant-Rezepteditor erfinden.
- Ein eigenständiger Bereich „Auswertungen & Listen“ wurde bisher nicht implementiert; **nicht** im Zuge dieses Auftrags erzeugen.
- Der bestehende Wechsel-Warnschutz für Editoren ist konservativ. Nicht beiläufig ersetzen; Verhalten erhalten.
- Bestehende Backend-, Package-, Offline- und Berechtigungslogik darf nicht verändert werden.

## 2. Bestandsaufnahme vor der Änderung

1. Prüfe aktuelle Angular-Komponenten, Styles, bestehende CSS-Variablen und UI-Bibliotheken sowie die Aufteilung zwischen Shell, Startseite und Fachansichten.
2. Prüfe das tatsächliche Routing (derzeit Hash-Navigation), aktiven Organisationskontext und Lagerkontext.
3. Identifiziere wiederverwendbare Komponenten, die ohne flächendeckendes Refactoring vereinheitlicht werden können.
4. Notiere vorhandene Styles, die spätere Arbeiten betreffen, aber **außerhalb des Scopes** liegen.

## 3. Umsetzung – verpflichtender Scope

### 3.1 Design Tokens und Basistypografie

- Übernehme die semantischen Farben, Typografie-, Spacing- und Radius-Vorgaben aus dem Visual Design Guide.
- Definiere Tokens zentral und nutze sie in betroffenen Komponenten; keine ungeordneten lokalen Literalwerte.
- Einheitliche Sans-Serif-Typografie für die gesamte Shell und den Startbereich; keine Serifenschrift.
- Keine extern nachgeladenen Schriftdateien oder neuen Framework-Abhängigkeiten ohne begründete Notwendigkeit.

### 3.2 Anwendungsshell und Navigation

- Überarbeite Kopfbereich, Marke, aktive Organisation, Navigationszustände und Benutzeraktionen zu einer ruhigen, klaren Hierarchie.
- Verhindere, dass lange Organisationsnamen, Benutzerkennungen oder schmale Fenster die Navigation auseinanderdrücken.
- Markiere aktive Navigation visuell **und semantisch**.
- Im geöffneten Lager weiterhin ausschließlich Lagernavigation mit erkennbarem Rückweg; bestehende Kontextregeln erhalten.
- Offline-/Schreibschutzstatus und Aktionen bleiben sichtbar und zugänglich, soweit bisher vorhanden.

### 3.3 Organisations-Start

- Verwandle lose Lagerlinks in gut erkennbare Listeneinträge oder Karten mit sinnvoller Beschriftung und klarer Aktion.
- Gestalte „Zuletzt verwendete Lager“, „Zugängliche Lager“ und die Verweise auf andere Arbeitsbereiche konsistent.
- Leerer Verlauf (kein kürzlich geöffnetes Lager) erhält einen kurzen, verständlichen Leerzustand.
- Keine fiktiven Kennzahlen, Aufgaben, Lager oder Benutzeraktionen hinzufügen.
- Startseite mit definierter Inhaltsbreite und sinnvoller vertikaler Hierarchie umsetzen; vorhandene Daten und Berechtigungen unverändert nutzen.

### 3.4 Minimale gemeinsame UI-Stile

- Definiere/reuse konsistente Basisstile für Buttons, Links, Formularfelder, Status/Badge, Listen/Karten und Tabellen, soweit dies ohne fachliche Editor-Umbauten möglich ist.
- Verbessere Hover, Fokus, Disabled und Fehlermeldungsdarstellung der bearbeiteten UI.
- Wenn vorhandene CSS-Struktur eine Wiederverwendung erschwert, dokumentiere die Grenze statt einen zweiten konkurrierenden Komponentenbaukasten einzuführen.

## 4. Explizit nicht umsetzen

- Keine Änderung an Domänenmodellen, API-Verträgen, Datenbankschemas, Berechtigungen oder Kalkulation.
- Kein neues Routing-Konzept; vorhandene Hash-Links, History und Reload müssen funktionieren.
- Kein Redesign der Verpflegung, Teilnehmerverwaltung, Rezept- und Zutateneditoren.
- Keine neue Organisationsrezept-Bearbeitung, keine neue Aufgabenverwaltung und kein neuer Auswertungsbereich.
- Kein Dark Theme, kein umfassendes Animationssystem, keine neue UI-Library ohne Rücksprache.
- Keine automatischen Änderungen an bestehender Speichern-/Dirty-State-Semantik.

## 5. Validierung und Akzeptanzkriterien

### Funktional
- Aktive Organisation korrekt; Start zeigt nur berechtigte Lager und richtige zuletzt verwendete Lager.
- Lager öffnen/verlassen, expliziter Organisationswechsel, globale vs. lokale Navigation unverändert korrekt.
- Bestehende Hash-Deep-Links und Browser-Zurück funktionieren.
- Rechteabhängige Navigation und Offline-/Desktop-Paketzugänge unverändert erreichbar.
- Keine regressiven fachlichen Änderungen.

### Visuell / UX
- **Einheitliche Sans-Serif-Schrift** in Kopfbereich, Navigation und Start; keine visuell unverbundenen Schrifttypen.
- Header klar in Marke, Organisationskontext, Navigation und Benutzerbereich strukturiert.
- Lager als eindeutige interaktive Einträge statt bloßer grüner Textzeilen.
- Aktiver Navigationspunkt sofort zu erkennen, ohne ausschließlich von Farbe abhängig zu sein.
- Definierte Inhaltsbreite, konsistente Typografie/Abstände, verständliche Leerzustände.
- Tastatur-Fokus sichtbar; wesentliche Text-/Control-Kontraste gegen WCAG-AA-Ziele überprüft.
- Sichtprüfung bei ungefähr 1440px, 1024px und 390px Breite. Engpässe dokumentieren statt Inhalte still zu verstecken.

### Tests / Dokumentation
1. Führe vorhandene Angular-Tests und Production Build sowie weitere berührte Tests aus.
2. Ergänze gezielte Tests für geändertes Interaktionsverhalten, nicht für zufällige CSS-Klassennamen.
3. Dokumentiere Befehle und Ergebnisse; unterscheide `PASS`, `FAIL` und `NOT_RUN`. Einen nicht durchgeführten PostgreSQL-Nachweis nicht als bestanden melden.
4. Lege einen Ergebnisbericht im bestehenden Repo-Konventionspfad ab (z. B. `docs/Spike/` oder bestehende Ergebnisstruktur; tatsächliche Struktur zuerst prüfen).
5. Liefere eine Liste geänderter Dateien und benenne Abweichungen vom Visual Design Guide ausdrücklich.
6. Erstelle nach Möglichkeit Screenshots der Startseite im Desktop- und schmalen Layout zur manuellen Abnahme. Falls Screenshots in der Umgebung nicht möglich sind: `NOT_RUN` vermerken und genaue manuelle Prüfschritte beschreiben.
7. Keinen Commit erstellen, sofern nicht ausdrücklich beauftragt.

## 6. Vorgehensreihenfolge

1. Repository untersuchen und technische Abhängigkeiten klären.
2. Zentrale Tokens/Basistypografie erstellen.
3. Shell und Navigation umgestalten.
4. Startbereich umgestalten.
5. Minimal gemeinsam nutzbare Komponentenstile vereinheitlichen.
6. Tests, Build und Sichtprüfung.
7. Ergebnisdatei erstellen und **vor weiteren fachlichen UX-Inkrementen** zur manuellen Abnahme bereitstellen.

**Bei Konflikten zwischen visuellen Anforderungen und bestehenden Fach-/Rechte-/Offline-Regeln gilt die bestehende Fachlogik. Abweichungen dokumentieren, nicht stillschweigend ändern.**
