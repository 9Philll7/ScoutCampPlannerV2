# Konfigurierte Teilnehmerfilter – Teilstand

Stand: 2026-10-21. **PARTIALLY_COMPLETED**. Kein Commit. Der Konfigurator ist unter Lager → Einstellungen → Teilnehmerfilter bedienbar; operative Verwendung und Offline-Transport fehlen weiterhin.

## Nachtrag: Konfigurator in Lagereinstellungen

- Funktionale Angular-Oberfläche: Liste, Erstellen, Öffnen/Bearbeiten, Kopieren, Name, UND-Bedingungen, ODER-Gruppen, Mehrfachwerte und Ausschlüsse; lesbare Zusammenfassung, Validierung leerer Gruppen, Speichern/Verwerfen und Dirty-Navigationsschutz. Kein UX-Overhaul.
- GET/PUT `/api/camps/{campId}/participant-filters`: Definitionen und Referenzoptionen ohne Teilnehmerdossiers. GET verlangt bestehenden Lagerzugriff und Health.ReadParticipantRequirements; PUT Health.ReadParticipantRequirements und Catering.EditMealPlanning. Eingefrorene Lager sind nicht bearbeitbar; Rechte/Freeze werden in der auditierten Transaktion erneut geprüft. ExpectedVersion schützt Updates; Referenzen sind lager-/tenantbezogen validiert.
- EF-Abbildung und additive AddCampParticipantFilters-Migrationen: SQLite 20261020204735, PostgreSQL 20261020205517. Nur neue Tabelle und CampId-Index; keine bestehenden Daten geändert. Keine Migration gegen die laufende Anwendungsdatenbank ausgeführt.
- API Release Build und Angular Production Build PASS. Abschließende Angular-Suite 112/112 PASS, SQLite-Suite 12/12 PASS. SQLite-Persistenztest für Definition/Reload/Version hinzugefügt. Historische Migrationsanzahl 25 → 26 angepasst; erster Lauf scheiterte ausschließlich an dieser alten Erwartung. Angular-Worker-Starttimeout im eingeschränkten Lauf, erfolgreiche Wiederholung außerhalb der Sandbox. `git diff --check` PASS.
- Noch NICHT implementiert: Löschen, Vorschau der Personenzahlen, Verwendungsauskunft, Zuordnung zur CookingUnit, operative Invalidierung, Entfernungsschutz gegenüber Stufen-/Kataloglöschung und Pakettransport. Die Oberfläche weist auf fehlende Verwendung/Offline-Anbindung hin. Gespeicherte Filter NICHT als im Paket gesichert betrachten.
- NOT_RUN: Browserabnahme, neue HTTP-Rechte-/Scope-Matrixtests, PostgreSQL-Lauf und installierter Desktop. Die vollständige frühere Testmatrix bleibt offen.

Manuelle Prüfung: Backend neu starten (Migration über bestehenden Startpfad), Lager → Einstellungen öffnen, Filter mit mehreren Gruppen speichern und nach Reload öffnen, kopieren, Änderungen abbrechen, ungültige leere Gruppe prüfen. Erforderlich sind explizites Teilnehmer-Leserecht und für Speichern Mahlzeitenplanung-Bearbeitungsrecht.

Die folgenden Abschnitte dokumentieren den vorherigen Grundlagenstand; durch diesen Nachtrag umgesetzte Punkte sind dort historisch.

## Umgesetzt

- Typisiertes Domain-Ausdrucksmodell: OR-Gruppen mit AND-Bedingungen, AnyOf/NoneOf, Stufe, Kategorie, DietType, Allergen, Stoff und Sonderverpflegung. Explizite Nullwerte nur für Stufe/Kategorie; ungültige Versionen/Operatoren/Werte und leere Gruppen werden abgewiesen.
- Kanonischer Schlüssel normalisiert Reihenfolge, Duplikate und GUID-Schreibweise. Keine allgemeine boolesche Algebra-Normalisierung implementiert.
- Domain-Objekt CampParticipantFilter mit ID/CampId, Name, serialisierter Definition und Versionsprüfung. Eingaben werden durch Serialisierung kopiert; ungültige/veraltete Updates verändern den Stand nicht. Änderungsrückgabe trennt Metadaten von Auswahldefinition.
- Gemeinsamer Evaluator und Referenzvalidator (zulässige IDs müssen später vom camp-/tenantbezogenen Use Case geliefert werden).
- Bestehende StructureParticipantProjection akzeptiert optional eine Definition. Struktur, Anwesenheit, Unterdeckung und Überlappungsschutz bleiben erhalten; Mehrfachtreffer aus OR-Gruppen zählen nicht doppelt. Gleichzeitiger benannter und einschränkender Altfilter wird abgewiesen.
- Minimierte CateringParticipantData-Projektion enthält vorhandene CampStageId/Category. Keine Namen oder Quellenmetadaten. Datenschutz-Contracttest entsprechend gezielt erweitert.

## Noch offen / nächster Einstieg

1. CampParticipantFilter in Catering-Persistenz abbilden, Unit-Referenz und Store-Operationen ergänzen, beide Migrationen erzeugen. Bisher ist das Objekt NICHT als EF-Entity registriert.
2. Camp-/Katalogreferenzen über vorhandene Contracts auflösen, Entfernungsschutz, atomare gemeinsame Änderungsprüfung, Concurrency und Audit/Freeze integrieren.
3. MealPlanningService muss gespeicherte Definitionen an allen Projektionsaufrufen übergeben. Aktuelle produktive Aufrufer verwenden weiterhin nur die drei Altfilter. Bestätigte Estimated/Fallback-Grenze und selektive Signaturen/Verifikation integrieren.
4. Konkrete bestehende Rechte-/Endpointmatrix dokumentieren, CRUD/Vorschau mit Scope- und Health-Prüfung implementieren. Keine neuen Endpoints in diesem Teilstand.
5. Angular-Filterverwaltung und Editor, Dirty-Schutz, Verwendungsauskunft und Vorschau.
6. Paketversion/Legacy-Transformation, Referenzvalidierung, Replace/Rollback und vollständiger Provider-/Desktop-Roundtrip.

## Prüfstand

- PASS: `dotnet test tests/ScoutCampPlanner.CateringTests -c Release --no-restore --logger "console;verbosity=minimal"`: 301/301. Enthält neun neue Tests für Regeln, Null/Negation, Kanonisierung, Referenzen, Versionskonflikte und Projektion.
- PASS: `dotnet build ScoutCampPlanner.slnx -c Release --no-restore`: 0 Warnungen/Fehler beim Contract-/Evaluator-Zwischenstand. Spätere Domain-Ergänzungen im erfolgreichen Catering-Testlauf kompiliert.
- Zwischenfehler behoben: alte Contract-Allowlist und ein target-typed-new im neuen Test. Eingeschränkter Teststart ohne Ausgabe; Wiederholung mit genehmigter Eskalation.
- PASS: `dotnet test tests/ScoutCampPlanner.DatabaseMigrationTests -c Release --no-restore --filter "FullyQualifiedName~Sqlite" --logger "console;verbosity=minimal"`: 11/11. Enthält erweiterte Projektion mit Stufe/Kategorie, Anwesenheit und fremdem Camp-Scope.
- PASS: `git diff --check`.
- NOT_RUN: vollständiger abschließender .NET-Prüfstand, PostgreSQL-Nachweis, Angular, Package-/Desktop-Neubuild und manuelle Prüfung der neuen Filterfunktion. Diese Teile sind noch nicht integriert; alte PASS-Ergebnisse sind kein Nachweis dafür.

## Migrationen / Risiken / Commit-Bereitschaft

Keine neuen Migrationen oder Datenbankänderungen. Bestehende UI und die drei bisherigen Filter bleiben unverändert. Nicht als fertiges Feature commit-bereit; insbesondere Referenzvalidator und Domain-Version ersetzen noch keine atomare Datenbankprüfung. Keine fachlichen Blocker, aber erheblicher bestätigter Scope bleibt offen. Schätzwerte für frei konfigurierte Filter bleiben separater Folgeauftrag.
