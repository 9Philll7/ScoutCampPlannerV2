# Codex Coding Guidelines

## Allgemeine Prinzipien

Code soll langfristig wartbar sein.

Bevorzuge: - einfache Lösungen - klare Grenzen - nachvollziehbare
Architektur

Vermeide: - unnötige Abstraktionen - Framework um des Frameworks
willen - versteckte Seiteneffekte

## Architektur

Die Modulstruktur ist verbindlich.

Nicht erlaubt:

-   Zugriff auf fremde Datenbankkontexte
-   Umgehen von Modulgrenzen
-   direkte Abhängigkeiten zwischen nicht erlaubten Modulen

## Domain Code

Domain-Code bleibt frei von:

-   ASP.NET Core
-   Entity Framework
-   Datenbankdetails
-   UI-Abhängigkeiten

## Application Layer

Application enthält:

-   Use Cases
-   Contracts
-   fachliche Abläufe

Keine UI- oder Datenbanklogik.

## Infrastructure

Infrastructure enthält:

-   EF Core
-   Datenbankzugriffe
-   externe Systeme

## Frontend

Angular soll featureorientiert aufgebaut werden.

Bevorzugte Struktur:

features/ core/ shared/

Keine unstrukturierten globalen Ordner für beliebigen Code.

## Datenbanken

PostgreSQL und SQLite müssen unterstützt werden.

Businesslogik darf nicht doppelt implementiert werden.

## Abhängigkeiten

Neue Libraries nur einführen, wenn ein konkreter Nutzen besteht.

Keine automatische Einführung von:

-   MediatR
-   AutoMapper
-   CQRS
-   generischen Repositorys
-   Event Bus

ohne Begründung.

## Dokumentation

Architekturentscheidungen dokumentieren.

Nicht stillschweigend Architektur ändern.

## Tests

Neue Funktionalität benötigt passende Tests.

Besonders wichtig:

-   Architekturtests
-   Integrationstests
-   Package-Kompatibilitätstests

## Qualität

Nach Änderungen:

-   Build ausführen
-   Tests ausführen
-   Probleme dokumentieren
