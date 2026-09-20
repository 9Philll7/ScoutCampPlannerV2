# Ergebnis: Mahlzeitenplanung – Inkrement 1

Stand: 2026-09-20

## 1. Ergebnis

Der vertikale Schnitt ist technisch vollständig umgesetzt: Domain, Application, Persistenz, PostgreSQL-/SQLite-Migrationen, REST-API, Angular-Oberfläche, Berechtigungen und Camp-Package-Replace bilden einen gemeinsamen Stand. Es wurde kein Commit erstellt.

## 2. Domain und fachliche Regeln

Umgesetzt sind mehrere `MealPlan`s pro Lager, `MealPlanOfferGroup`, `MealPlanEntry`, optionale Rollen-Qualifier, unveränderliche Plan-Snapshots, `CookingUnit`, einstufige `CookingUnitGroup`, Standard- und Slot-Strukturzuordnungen, `FollowStandard`, `Custom`, `NoSupplyRequired`, Zielbedarfs-Overrides und individuelle Rezeptwahlen.

Unvollständige Pläne dürfen gespeichert werden. Jede vorhandene Angebotsgruppe benötigt genau einen Standardeintrag; doppelte Rezeptrevisionen innerhalb derselben Gruppe, Parent-/Child-Doppelzuordnungen und fremde Slot-/Planreferenzen werden abgelehnt.

## 3. Versionierung und Bearbeitung

Die Angular-Oberfläche bearbeitet eine lokale Kopie und übernimmt Änderungen nur mit Speichern; Abbrechen verwirft sie. Ein Speichervorgang erzeugt höchstens eine neue fachliche Version und einen Snapshot. Rezeptrevision, Standardkennzeichnung, Gruppen-/Entry-Struktur und Rollen-Qualifier sind versorgungsrelevant. Name, Anzeigename, Hinweis und Sortierung allein erhöhen die Version nicht. Optimistische Versionskonflikte liefern `409 Conflict`.

## 4. Bedarf, Berechnung und Status

Der Bedarf wird deterministisch aus der effektiven Camp-Strukturzuordnung, anonymen KiJu-/Leiter-Schätzungen und Catering-Verpflegungsfaktoren berechnet. Leiter zählen mit Faktor 1; KiJu mit dem Lagerfaktor ihrer Stufe. Ein Slot-spezifischer Bedarfsoverride bleibt eine manuelle Entscheidung.

Der gespeicherte Berechnungs-Snapshot enthält Strukturknoten, Planungszahlen, Faktoren, berechneten und wirksamen Bedarf, Plan/Snapshot, Abonnementzustand, effektive OfferGroup-Ziele, konkrete Rezeptauswahl und Herkunft manueller Entscheidungen. `Current`, `Stale` und `Incomplete` werden ohne Hintergrund-Neuberechnung geführt.

## 5. Präzise Invalidierung

Planversion, verwendete Struktur, verwendete Schätzungen/Faktoren, Meal-Aktivierung, Lagerzeitraum, Abonnement, manuelle Overrides und Rezeptwahl werden berücksichtigt. Darstellungs-, Gruppen- und Sortieränderungen machen einen Stand nicht veraltet. Standard-Strukturänderungen betreffen nur Slots ohne Struktur-Override; Standardplanänderungen nur `FollowStandard`.

Deaktivierte und außerhalb des aktuellen Lagerzeitraums liegende Mahlzeiten werden operativ ausgeblendet, ihre Daten aber nicht gelöscht.

## 6. Rezeptbibliothek

Zugänglich sind veröffentlichte portionenbasierte Revisionen aus Central-, Tenant- und Camp-Scope. Beim ersten Einsatz erfolgt die automatische Aufnahme in die Lagerbibliothek. Das Entfernen eines verwendeten Bibliothekseintrags wird durch MealPlan- oder CookingUnit-Referenzen blockiert und benennt die Verwendungen. Unbenutzte Einträge werden nicht automatisch entfernt.

## 7. Berechtigungen und Isolation

Die neue Camp-Permission lautet `catering.meal-planning.edit`. Sie ist dem vorhandenen Katalog zugeordnet und wird für alle mutierenden Endpunkte zusammen mit Camp-/Tenant-Zugriff und Freeze-Zustand geprüft. `CampAdmin` und `CampEditor` erhalten sie; Lesezugriff folgt `camp.view`. Es wurde keine neue Rolle und keine Verify-Permission eingeführt.

## 8. API und Oberfläche

Die API bietet Übersicht und Detailabruf sowie CRUD/Sortierung für MealPlans, CookingUnitGroups und CookingUnits, Slot-Konfiguration, Struktur-Reset und explizite Berechnung. Fehler unterscheiden ungültige Daten, Versionskonflikte, blockierte Löschungen und nicht gefundene Ressourcen.

Die Angular-Ansicht ermöglicht Planbearbeitung, Angebotsgruppen/Alternativen, Standardwahl, Kocheinheiten, Strukturzuordnung, Abonnementzustände, Bedarf und Zielbedarfe, Custom-Rezeptwahl, Reset und Berechnung. Status und Abdeckungswarnungen sind sichtbar. Die Lager-Rezeptansicht kann unbenutzte Einträge entfernen und zeigt blockierende Referenzen.

## 9. Persistenz und Migrationen

Die neuen Tabellen und Beziehungen gehören dem Catering-Modul. Provider-spezifische Migrationen wurden erzeugt:

- SQLite: `20260919124906_AddMealPlanningIncrementOne`
- PostgreSQL: `20260919124915_AddMealPlanningIncrementOne`

Der Upgrade-Test deckt den bisherigen Stand ab. Die frühere Camp-Spike-Tabelle `CookingUnits` wird entfernt; die neue gleichnamige SQLite-Tabelle gehört ausschließlich Catering und übernimmt keine alten Spike-Datensätze.

## 10. Offline-Package

`cateringMealPlanningData` besitzt innerhalb des weiterhin checksum-only Package-Formats 1 ein eigenes Schema 1 und ist die einzige autoritative Repräsentation der veränderlichen Mahlzeitenplanung. Übertragen werden Pläne, Snapshots, Gruppen, Entries, Kocheinheiten, Strukturreferenzen, Zustände, Overrides, Rezeptwahlen und Berechnungsstände.

Die Catering-Rezeptreferenz-Closure verwendet ihr bestehendes eingebettetes Schema 2. Sie umfasst nun auch veröffentlichte camp-lokale Revisionen als unveränderliche Offline-Referenzen. Jede von MealPlan oder Custom-Wahl verwendete Revision muss in der Closure liegen. Import und Rückimport ersetzen den Camp-Slice atomar; IDs und Beziehungen bleiben erhalten.

Package-Format 2 bleibt unverändert für Verschlüsselung, Signatur und spätere Migration reserviert.

## 11. Automatisierte Prüfung

Erfolgreich ausgeführt:

- `dotnet build ScoutCampPlanner.slnx --no-restore --disable-build-servers -m:1`: 0 Warnungen, 0 Fehler
- vollständiger .NET-Testlauf: 350/350 grün
- Catering: 182/182
- Architektur: 4/4
- Datenbankmigration/Audit: 15/15
- Package: 7/7
- PostgreSQL-Migrations-/Integrationstests mit Docker PostgreSQL 18: grün
- PostgreSQL-Package-/Rollbacktest: grün
- Angular Vitest: 9/9 (API sowie Edit/Cancel/Save, Status, Reset, Deaktivieren einer gespeicherten Strukturabweichung, Löschblockaden und Abbruch des Paket-Speicherdialogs)
- Angular Production Build: erfolgreich
- `npm audit --json`: 0 bekannte Schwachstellen

Die Frontend-Testinfrastruktur verwendet Angular 22 `unit-test` mit Vitest 4 und jsdom; ein einzelner Fork-Worker verhindert sporadische Thread-Runner-Startzeitüberschreitungen unter Windows.

## 12. Manueller Prüfstand

Die Korrektur der Strukturabweichung wurde manuell bestätigt. Beim Offlineexport wurde anschließend festgestellt, dass ein abgebrochener Browserdownload das Lager bereits einfriert. Der Speicherdialog wird jetzt vor dem Transferaufruf geöffnet; Abbrechen startet keinen Transfer. Automatisierte Regressionstests sind erfolgreich. Manuelle Nachprüfung und vollständiger Roundtrip bleiben offen.

Die ersten beiden manuellen Prüfblöcke wurden vom Benutzer als erfolgreich bestätigt. Dabei wurde ein Fehler beim Deaktivieren einer gespeicherten Strukturabweichung gefunden und korrigiert: Die Oberfläche sendet jetzt eine leere Zuordnung zum Entfernen statt `null` (bestehende Zuordnung beibehalten). Regressionstest erfolgreich; erneute manuelle Prüfung dieses Ablaufs und Offline-Roundtrip stehen noch aus.

Der erste manuelle Prüfblock wurde am 2026-09-20 erfolgreich bestätigt: Anlegen und unvollständiges Speichern, gebündeltes Save mit genau einem Versionsschritt, Cancel, Angebotsgruppen mit Standard und Alternative, zwei Kocheinheiten, `Custom`/`NoSupplyRequired`, Berechnung mit Bedarfs- und Zielbedarfs-Override sowie die blockierte Entfernung einer verwendeten Rezeptrevision funktionieren über die Oberfläche.

Noch offen sind die gezielte Prüfung von FollowStandard-Invalidierung und unveränderten Abweichungen, Struktur-Override und Reset, selektives `Stale`, Meal-Deaktivierung/-Reaktivierung, Zeitraumänderung sowie der vollständige Cloud → Lokal → Cloud-Roundtrip.

## 13. Dokumentierte Abweichungen und Grenzen

- Nachprüfung des Desktop-Roundtrips: Die lokale API-Bearbeitung ist noch nicht
  durchgängig nutzbar (Freeze auch lokal, Originalbaseline nicht übernommen,
  fehlende lokale Zugriffszuordnung). Die Aussage „technisch vollständig“ in
  Abschnitt 1 gilt deshalb nicht für den bedienbaren Desktop-Roundtrip. Befunde,
  notwendige Zugriffsentscheidung und Umsetzungsschritte stehen in
  `desktop-roundtrip-next-steps.md`. Ein aktueller Installer wurde noch nicht erstellt.
  Nachtrag: Die Zugriffsregel ist inzwischen bestätigt und in ADR-009/ADR-011
  dokumentiert. Lokales Freeze und die Baselineübernahme sind korrigiert; ein
  zusätzlicher Test prüft zwei Transferzyklen, lokale Domain-Bearbeitung und
  Ablehnung wiederholter Rückimporte. Lokale Identität, Berechtigungsintegration
  und Desktopbedienung bleiben ausstehend.

- Bestehende Rezept-Snapshots besitzen keinen Rollen-Qualifier. Daher gibt es derzeit keinen automatisch übernehmbaren Initialwert; der optionale Qualifier wird direkt am MealPlanEntry gesetzt. Es wurde keine neue Rezepteigenschaft erfunden.
- Mutable camp-lokale Rezeptentwürfe bleiben gemäß bestehender Package-Dokumentation außerhalb des Offline-Replace. Ihre veröffentlichten, für Planung benötigten Revisionen werden jedoch unveränderlich übertragen.
- Die API verwendet weiterhin das bestehende projektspezifische Fehlerformat; ein einheitlicher Problem-Details-Vertrag bleibt ein allgemeiner offener Punkt.
- Personenbezogene Anforderungen, medizinische Bewertung, automatische Ersatzwahl, Verifikation, Einkauf und Bestellung sind nicht enthalten.

## 14. Nächster Schritt und Commit-Punkt

Zuerst den manuellen Prüfablauf vollständig durchführen. Bei erfolgreichem Ergebnis ist ein gemeinsamer Commit sinnvoll:

`Implementiere Mahlzeitenplanung Inkrement 1`

Danach sollte Inkrement 2 zunächst im hybriden Planungsprozess fachlich definiert werden; insbesondere Anforderungskatalog, Datenschutzgrenzen, eindeutige Ersatzauflösung und Verifikation dürfen nicht aus Inkrement 1 heraus implizit erweitert werden.
