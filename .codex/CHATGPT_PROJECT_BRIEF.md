# ScoutCampPlanner – kompakte Projektübergabe

Stand: 2026-10-05 (Inkrement 2: laufende Teilimplementierung)

## Zweck

Diese Datei ist der kurze Einstieg für eine neue ChatGPT-Planungsphase. Sie
ersetzt keine Architektur- oder Domänendokumentation. Verbindlich bleiben in
dieser Reihenfolge:

1. `docs/decisions/`
2. `docs/architecture/`
3. `docs/domain/`
4. `.codex/agent-instructions.md` und `.codex/coding-guidelines.md`

Bei Widersprüchen muss zuerst die Dokumentation geklärt werden. Neue
Architekturentscheidungen werden nicht stillschweigend in einem
Implementierungsauftrag versteckt.

## Projektziel und Architektur

ScoutCampPlanner ist ein modularer Monolith für Pfadfinderlager mit drei
bestätigten Betriebsformen: Cloud/Server mit PostgreSQL, lokale
Docker-Lagerinstanz mit PostgreSQL und Windows-Single-Device mit Tauri,
ASP.NET-Core-Sidecar und SQLite.

Die aktive Modulrichtung lautet `Platform → Camp → Catering`. Module besitzen
ihre Daten und EF-Core-Kontexte selbst; fremde Infrastructure darf nicht direkt
verwendet werden. PostgreSQL und SQLite haben getrennte, modulspezifische
Migrationen. Offlinebetrieb verwendet versionierte Lagerpakete und bewusstes
Cloud → Lokal → Cloud Replace, keine automatische Synchronisation.

## Implementierter Produktstand

### Platform

- einmalige Ersteinrichtung, Passwortlogin, Cookie-Sitzungen und Logout
- Benutzerkonten, Tenant-Mitgliedschaften, Rollen- und Berechtigungskatalog
- Argon2id-Passwortverifier und lokale Passwortstärkeprüfung
- append-only Sicherheitsaudit mit HMAC-Kette und geschützter Schlüsselablage
- erste atomare fachliche Audit-Integrationen für Setup, Authentifizierung und
  Lagererstellung/-änderung

Eine vollständige Benutzer-/Mitgliederverwaltung in der Oberfläche,
vorbereitete Offline-Anmeldung und Tauri-Entsperrung fehlen noch.

### Camp

- Lager mit Mandant, Name, Zeitraum und explizitem `CampAdmin`
- freie oder administrator-definierte Strukturtiefe
- Baumknoten anlegen, umbenennen, verschieben, einklappen und regelkonform
  löschen
- mandantenweite Stufenvorlage und stabile Lagerkopie
- anonyme `KiJu`-/`Leiter`-Schätzungen nur an zulässigen Blättern
- aggregierte Planungsübersichten

Der minimale personalisierte Teilnehmerkern ist inzwischen als Camp-Domain,
Contract und Persistenz für beide Provider angelegt. Interne CRUD-Anwendungsfälle
mit expliziten Rechten, Referenz-/Freeze-/Bearbeitungsstandprüfung und atomarem
Dummy-Entwicklungsaudit sind umgesetzt. HTTP/API, Teilnehmeroberfläche,
explizite Online-/lokale Rechtevergabe und Dummy-Teilnehmer-Package-Roundtrip
sind angebunden. Der zentrale/mandanteneigene DietType-Katalog besitzt jetzt
versionierte Herkunftsregeln und geprüfte Beiträge. Direkte Catering-Zuordnungen
sind durch Camp-Strukturzuordnung und CookingUnit-Filter ersetzt. Die tatsächliche
Bedarfsbasis ist in MealPlanningService,
Oberfläche und Dummy-Paket integriert; der operative Versorgungs-/Verifikationsschnitt
bleibt offen. Aktuelle Prüfergebnisse stehen im Inkrement-2-Ergebnisbericht. Keine Freigabe
für echte Teilnehmer-/Gesundheitsdaten.

### Catering

- mandantenweite und lagerbezogene Verpflegungsfaktoren
- gewichtete Verpflegungseinheiten
- konfigurierbare Mahlzeitenarten und tägliche Aktivierung
- zentrale, Mandanten- und Lagerrezeptbibliotheken
- Rezeptentwürfe, unveränderliche Revisionen, Veröffentlichung und
  Versionskonflikte
- Zutatenpositionen, Gruppen, Skalierung, Unterrezepte im Domain-/Application-
  Modell und positionsbezogene Ersatzzutaten
- Konfliktanzeige und Publikationsvalidierung
- Nährwertberechnung gesamt und pro Standardportion mit Vollständigkeitsstatus
- revisionsfähige Zutaten in zentralem, Mandanten- und Lagerscope
- Kategorien, Einheiten, Umrechnungen, Allergene, Herkunft, Varianten,
  Nährwertprofile und quantitative unverträglichkeitsrelevante Stoffgehalte
- kontrollierte Einreichung lokaler Zutaten nach zentral sowie Ablösung durch
  vorhandene zentrale Zutaten

### Offline und Desktop

- technisch validierter PostgreSQL-/SQLite-Roundtrip mit atomarem Replace
- Paketversion 1 transportiert Camp/Catering-Daten sowie die unveränderliche
  transitive Rezept-/Zutaten-Referenzmenge einer Lagerbibliothek
- Tauri-Sidecar, persistente SQLite-Datenbank und Windows-Bundles sind im Spike
  technisch validiert

Veränderliche lagerlokale Rezeptentwürfe werden noch nicht vollständig als
bearbeitbarer Replace-Bereich im Paket transportiert. Paketversion 1 besitzt
nur eine Prüfsumme, keine produktive Verschlüsselung oder Signatur.

## Aktueller Working Tree

Im noch nicht committeten Inkrement wird Mahlzeitenplanung Inkrement 1 als
durchgängiger Vertikalschnitt umgesetzt: versionierte MealPlans und Snapshots,
OfferGroups und Rezeptrevisionen, CookingUnits, Strukturzuordnung,
FollowStandard/Custom/NoSupplyRequired, explizite Bedarfsberechnung,
Current/Stale/Incomplete, Löschblockaden, API, Angular-Oberfläche und der
vollständige Camp-Package-Replace. Der konkrete Prüfnachweis steht in
`.codex/meal-planning-increment-1-result.md`.

## Wichtigste offene Produktphase

Mahlzeitenplanung Inkrement 1 ist umgesetzt. Inkrement 2 ist fachlich bestätigt
und teilweise implementiert, aber noch kein nutzbarer Vertikalschnitt.
Die drei Rückfragen zu Stoffmodi, expliziten Health-/Verify-Rechten und
Tenant-DietTypes sind entschieden und in ADR-026/011/018 konsolidiert.
Nicht erneut zur Entscheidung stellen. Verbindlicher Fortsetzungsstand:
`.codex/meal-planning-increment-2-result.md`; Auftrag und Fachmodell stehen in
`.codex/meal-planning-increment-2.md` und
`docs/domain/catering-meal-planning-increment-2.md`.
Teilnehmer-/Sonderverpflegungs-Offlineentwicklung ist ausschließlich mit
Dummy-/Testdaten erlaubt, keine Produktionsfreigabe.

## Weitere offene Punkte

### Kurz- bis mittelfristig

- Mahlzeitenplanung Inkrement 1 manuell vollständig prüfen und gemeinsam committen
- entscheiden, ob und unter welchen ODbL-/Cache-/Rate-Limit-Regeln Open Food
  Facts ergänzt wird
- zentrale und Mandanten-Rezeptverwaltung in der Oberfläche vervollständigen
- Camp-lokale veränderliche Rezeptdaten vollständig offline transportieren
- stabile API-Problem-Details-Verträge einführen
- Benutzer- und Mitgliedsverwaltung vervollständigen
- Offline-Login und Tauri-Unlock implementieren

### Vor sensiblen Personendaten oder Produktrelease

- Paketversion 2 mit der validierten Verschlüsselungs-/Signaturrichtung
  produktiv implementieren
- Paket-Migrationsregistry, historische Fixtures und Kompatibilitätsfenster
  festlegen
- Datenschutzregeln für Aufbewahrung, Archivierung, Löschung und
  Anonymisierung entscheiden
- Audit-Aufbewahrung rechtlich prüfen und verbleibende Use Cases anbinden
- Backup-Restore und Desktop-Installer auf sauberer Zielmaschine testen
- Windows-10-Kompatibilität nur noch gemäß ADR-013 behandeln

### Spätere Module

- erweiterte Teilnehmerverwaltung außerhalb des minimalen Inkrement-2-Kerns
- Finance
- Program
- Material

## Bekannte Grenzen und nicht zu treffende Annahmen

- Keine medizinischen Grenzwerte erfinden.
- Allergene bleiben qualitativ. Dosisabhängige Inhaltsstoffe unterstützen nach
  ADR-026 Quantitative, Qualitative und Unknown, ohne erfundene Mengen.
  Personentoleranzen stehen getrennt von der Zutat am Teilnehmer;
  zentrale Defaults werden später beim Anlegen kopiert, nicht live vererbt.
- Rezeptpositionen wählen keine Zutatenvariante. Varianten werden erst im
  konkreten Verpflegungs-/Kocheinheitenkontext ausgewählt.
- Margarine ist beispielsweise eine eigene Basiszutat, keine Buttervariante.
- Lagerstruktur und Kocheinheiten bleiben getrennte Domänen.
- Keine automatische Cloud-/Offline-Synchronisation und kein stilles Merge.

## Empfohlenes hybrides Arbeitsformat

Für jede neue Phase sollte ChatGPT zuerst ein kurzes Phasendokument entwickeln:

1. Ziel und sichtbarer Nutzwert
2. verbindliche Fachregeln
3. offene Entscheidungen mit Empfehlung
4. Nicht-Ziele
5. betroffene Module und Offline-Auswirkungen
6. Akzeptanzfälle und notwendige Tests
7. erforderliche Dokumentationsänderungen

Erst nach Bestätigung wird daraus ein abgegrenzter Codex-Auftrag erstellt. Ein
Codex-Auftrag soll auf konkrete Repository-Dokumente verweisen, keine bereits
entschiedenen Fragen erneut öffnen und einen manuellen Prüfablauf sowie einen
sinnvollen Commit-Punkt enthalten.
