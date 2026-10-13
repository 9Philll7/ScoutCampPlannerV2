# Ergebnis: Mahlzeitenplanung – Inkrement 2

Stand: 2026-10-13.

## Status

**COMPLETED – funktional und technisch abgeschlossen.**

Die manuelle Endabnahme wurde vom Benutzer vollständig mit **PASS** bestätigt.
Auch die letzte Ask-Nachprüfung ist PASS: gemeinsame/getrennte Auswahl, Wechsel
in beide Richtungen, keine parallelen aktiven Lösungen sowie Persistenz,
Reload und Recalc. AlwaysTogether und NeverTogether sind ebenfalls PASS.

## Umgesetzt

- Bestehende Teilnehmer-/CookingUnit-Planung mit realer Bedarfsbasis,
  RequirementGroups, SupplySolutions und transitiver Eignungsprüfung integriert.
- Automatische Auflösung, persistierte manuelle Entscheidungen, Coverage und
  Gemeinsamkeitspräferenz operativ angebunden.
- Ask materialisiert erst nach der Operatorentscheidung die gewählte Versorgung.
  Betroffene Lösungen werden ersetzt, nicht zusätzlich angelegt; Auto/Manual-
  Herkunft und manuelle Entscheidungen bleiben bei Recalc erhalten.
- Selektive Invalidierung, Verifikation mit ersetzbarem Snapshot, Rechte-/Scope-
  Prüfungen sowie UI und Dummy-Package-Roundtrip einschließlich Replace/Rollback.
- Development-Testseed und Snapshot-Codec-Regressionen; Desktop-Importkorrekturen.
- In diesem Abschlussdurchlauf keine neue Fachlogik und keine Refaktorierung.

## Abschließender Prüfstand

- .NET Release Build: **PASS**, 0 Warnungen und 0 Fehler.
- Vollständige .NET-Test-Suite: **526/526 PASS**, keine übersprungenen Tests.
  Architecture 4, Catering 292, DatabaseMigration 20, Package 18,
  Platform 108, Security 84. PostgreSQL-Testinstanz ausdrücklich eingebunden.
- Angular Tests: **56/56 PASS** in 13 Testdateien.
- Angular Production Build: **PASS**, auch frisch im Tauri-Build ausgeführt.
- Self-contained win-x64 Release Sidecar Publish: **PASS**.
- Tauri Release, MSI und NSIS: **PASS**.
- git diff --check: **PASS**. Git-Status auf unbeabsichtigte/generierte Artefakte
  geprüft: keine Build-Binaries, Installer oder Testdatenbanken im Commit.
  EF-Migrationen und deren Snapshots sind beabsichtigte Quellartefakte.

## Desktop-Artefakte und Aktualitätsnachweis

- MSI: src/desktop/src-tauri/target/release/bundle/msi/ScoutCampPlanner_0.1.0_x64_en-US.msi
- NSIS: src/desktop/src-tauri/target/release/bundle/nsis/ScoutCampPlanner_0.1.0_x64-setup.exe
- Sidecar SHA-256:
  FE41770C6B448D43B28F7FC3FC349887621B1F906BFB9467E54676BDB7C7357C
- Publish-Ausgabe, Tauri-Bundle-Eingabe, Release-Sidecar und der ohne Installation
  aus dem MSI extrahierte Sidecar sind hashgleich.
- Das komprimierte Tauri-Frontend ist nach Dekomprimierung bytegleich zum
  aktuellen Angular-Build und enthält den letzten Ask-Fix (commonSupplyChoices).
  Genau dieses Asset ist auch im aus dem MSI extrahierten Desktop-Programm enthalten.
- NSIS-Nachweis: erfolgreicher frischer Build und generiertes Installer-Skript
  referenzieren das geprüfte aktuelle Desktop-Programm und den aktuellen Sidecar.
  NSIS wurde nicht zusätzlich extrahiert.
- MSI SHA-256: CFE429C6992873B1C61CE4BDB9A9E26A08D4F48A5F91D1F4FB21B6F8EBCDFE02
- NSIS SHA-256: B1E2D800F9161131D8756AA963029B960131F04403B1F1DA8ACEB8D78EF4335F
- Keine Build-Blocker. MSI-Decompiler meldete UI-ControlEvent-Warnungen;
  die Payload-Extraktion und deren Prüfungen waren erfolgreich.

## Manuelle Abnahme

**PASS (Benutzerbestätigung):** Strukturzuordnung, reale CookingUnit-Bedarfsbasis,
komplementäre Sonderverpflegungsfilter, Überlappungsschutz, Anwesenheit/
Verpflegungsprofil mit Neuberechnung, Strukturschutz und installierter
Cloud → lokal → Cloud Roundtrip; vollständige Endabnahme einschließlich
AlwaysTogether, Ask und NeverTogether.

Eine erneute Installation der exakt jetzt erzeugten Abschlussinstaller:
**NOT_RUN**. Die oben dokumentierten Build-/Payload-Prüfungen wurden durchgeführt;
dies ist keine erneute manuelle Roundtrip-Abnahme.

## Migrationen

OperationalMealSupply für PostgreSQL (20261007223517) und SQLite
(20261007223512) einschließlich EF-Snapshots. Ask verwendet die bestehende
Persistenz und ein optionales commonSupplyChoices-Feld im berechneten Dokument;
keine zusätzliche Ask-Migration und keine neue Dependency.
Im Abschlussdurchlauf wurden keine weiteren Migrationen erstellt.

## Noch offen / Risiken

- Kein bestätigter funktionaler Inkrement-2-Scope mehr offen; keine technischen Blocker.
- Größerer UI-/UX-Overhaul bleibt ausdrücklich ein separater Folgeblock.
- Sensible Offline-Daten sind weiterhin ausschließlich für Development mit
  Dummy-/Testdaten freigegeben: **keine Produktionsfreigabe**.

## Commit-Bereitschaft

Abschlussprüfungen grün; freigegeben für den beauftragten Abschlusscommit:
Complete meal planning increment 2.
