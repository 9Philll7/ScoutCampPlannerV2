# Codex Architecture Spike Auftrag

## Status

Technisch validiert und am 2026-08-08 abgeschlossen.

Dieses Dokument bewahrt den ursprünglichen Implementierungs- und Prüfumfang. Das akzeptierte Ergebnis ist in `docs/decisions/adr-006-architecture-spike-validation.md` dokumentiert; Nachweise und verbleibende betriebliche Prüfungen stehen in `docs/spike/results.md`.

## Ziel

Der ScoutCampPlanner Architecture Spike wurde in einem neuen Repository
implementiert.

Der Spike validierte die technische Zielarchitektur und ist kein
vollständiges Produkt.

## Validierter Umfang

-   ASP.NET Core / .NET Backend
-   Angular Frontend
-   Modularer Monolith
-   PostgreSQL Serverbetrieb
-   SQLite Single Device
-   Tauri Desktop-Anwendung
-   Lagerpaket Cloud -\> Lokal -\> Cloud
-   Package-Versionierung und Migration
-   Modulgrenzen

## Repository

Das Repository ist neu und soll nach erfolgreichem Spike weiterverwendet
werden.

Grundstruktur:

-   src/backend
-   src/frontend
-   src/desktop
-   tests
-   docs
-   deploy
-   tools

## Architektur

Module:

Platform ↓ Camp ↓ Catering

Module dürfen nur über definierte Contracts miteinander kommunizieren.

Direkter Zugriff auf fremde Infrastruktur ist verboten.

## Betriebsmodelle

Cloud: - PostgreSQL - mehrere Benutzer

Lokale Lagerinstanz: - Docker - lokale Infrastruktur

Single Device: - Windows - Tauri - SQLite - kein technisches Wissen beim
Benutzer notwendig

## Offline Modell

Kein automatisches Synchronisieren.

Ablauf:

Cloud → Export Lagerpaket → SQLite → Offline arbeiten → Export → Cloud
Replace

Der lokale Stand ist während der Offlinephase autoritativ.

## Definition of Done (erreicht)

Der Spike wurde anhand folgender Kriterien erfolgreich abgeschlossen:

-   Backend funktioniert
-   Frontend funktioniert
-   PostgreSQL funktioniert
-   SQLite funktioniert
-   Tauri funktioniert
-   Lagerpaket übertragen werden kann
-   IDs erhalten bleiben
-   Rückimport atomar funktioniert
-   Modulgrenzen getestet sind
-   Ergebnisse dokumentiert sind
