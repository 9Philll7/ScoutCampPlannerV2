# Codex Architecture Spike Auftrag

## Ziel

Implementiere den ScoutCampPlanner Architecture Spike in einem neuen
Repository.

Der Spike validiert die technische Zielarchitektur und ist kein
vollständiges Produkt.

## Zu validieren

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

## Definition of Done

Der Spike ist erfolgreich, wenn:

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
