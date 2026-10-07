# Ergebnis: Mahlzeitenplanung – Inkrement 2

Stand: 2026-10-07.

## Status

**PARTIALLY_COMPLETED** für das gesamte Inkrement 2. Die angeforderte
Struktur-/CookingUnit-Korrektur ist implementiert, automatisiert geprüft und
vom Benutzer manuell abgenommen. Abgeschlossener Zwischenmeilenstein,
ausdrücklich kein Abschluss von Inkrement 2. Offener Scope bleibt unverändert.

## Neu umgesetzt

- Camp-owned Teilnehmerstrukturzuordnung zu Blattknoten, bei definierter
  Struktur ausschließlich letzte Ebene. Belegte Knoten sind gegen Löschen
  und Erweiterung durch Unterknoten geschützt.
- Datenminimierte Camp-Projektion: technische Teilnehmerreferenz,
  Strukturreferenz, effektive Anwesenheit und benötigte Verpflegungsanforderungen.
  Keine Klarnamen, Quellenmetadaten oder vollständigen Gesundheitsprofile.
- CookingUnits verwenden Strukturreferenzen und All / SpecialCateringOnly /
  WithoutSpecialCatering. Sonderverpflegung wird aus DietType, Allergenen
  oder Unverträglichkeiten abgeleitet.
- Direkte Catering-Teilnehmerzuordnung aus operativer API/UI entfernt.
  Bestehender MealPlanningService/CookingUnitMealState nutzt die Projektion für
  reale Bedarfsbasis, RequirementGroups und Fingerprints. Durch Filter oder
  Abwesenheit leerer vorhandener Kontext ergibt ActualParticipants mit Bedarf 0.
- Überlappende effektive Mengen werden beim Ändern der Konfiguration abgewiesen.
  Inkonsistenter Bestand erzeugt Incomplete mit strukturiertem Problem, keine
  willkürliche Zuordnung. Unzugeordnete Anwesende bleiben ein Problem.
- MealServingAssignment als separates, geschütztes Ausgabekonzept dokumentiert;
  noch keine personenbezogene Ausgabe-API oder Ausgabelogistik aktiviert.
- Package-Sektionen Teilnehmer Schema 2 und Mahlzeitenplanung Schema 3:
  Strukturzuordnung und Filter in Replace/Rollback integriert. Äußeres
  Paketformat unverändert. Lokale Entfernung löscht Teilnehmer vor ihren Knoten.

## Migrationen / verbleibende Migrationsprobleme

- SQLite Camp: `20261006213153_AddParticipantStructure`.
- SQLite Catering: `20261006213157_StructureBasedParticipantPlanning`.
- PostgreSQL Camp: `20261006213201_AddParticipantStructure`.
- PostgreSQL Catering: `20261006213205_StructureBasedParticipantPlanning`.
- Eindeutige Altzuordnungen werden beim Start atomar auf Camp-Blattknoten
  übertragen; frühere Catering-Zuordnungsdaten anschließend geleert.
  Das alte JSON-Feld bleibt ausschließlich als Migrationsrest erhalten,
  nicht als parallele aktive Zuordnungsquelle.
- Mehrdeutige Zuordnungen, widersprechende Camp-Zuordnungen oder alte
  Teilnehmer-Meal-Overrides bleiben unverändert erhalten und verhindern
  vollständige Berechnung/Export bis zur Bereinigung. Keine dedizierte
  Auflösungsoberfläche vorhanden. Auch redundante Overrides werden konservativ
  nicht automatisch übertragen.
- Eingefrorene Cloud-Lager werden bei der Startmigration übersprungen.
  Alte Pakete werden nur bei eindeutiger Übertragung angenommen; andernfalls
  Ablehnung vor Übernahme, kein stiller Datenverlust.
- Entwicklungsdatenbank nur lesend geprüft: die untersuchte Zuordnung ist
  eindeutig übertragbar. Migration dort nicht manuell ausgeführt.

## Tests

- Kleine Web-Session-Nachkorrektur: HTTP 401 der eigenen Fach-API beendet die
  aktive Browseransicht und zeigt die Anmeldung mit Ablaufhinweis. Keine
  Wiederholung fehlgeschlagener Aktionen; 403/404, Loginfehler, fremde URLs
  und Desktop-Geräteanmeldung bleiben unverändert. Verspätete 401 aus einer
  früheren Sitzung beenden keine neue Sitzung. Kein Timer.
  **PASS:** 34 Angular-Tests und Production Build. Interaktive Abnahme **NOT_RUN**.
  Bestehende Installer wurden für diese reine Web-Korrektur nicht erneut gebaut.

- Export-Nachkorrektur: Die Rezept-Referenzmenge berücksichtigt jetzt auch
  Mahlzeitenplaneinträge und CookingUnit-Rezeptentscheidungen, nicht nur die
  Lagerbibliothek. Exakte Revisionen samt transitiven Abhängigkeiten werden
  exportiert und beim Import/Validieren mit denselben Wurzeln geprüft.
  Keine Änderung der Benutzerdaten oder automatische Entsperrung.
- **PASS:** Pakettests erneut 18/18, einschließlich Rundlauf mit Planrezept
  ohne Lagerbibliothekseintrag. HTTP-Sidecar-Rundlauf erneut **PASS**;
  Artefakte: `%TEMP%/scp-roundtrip-7b1516f3-fd23-42eb-a820-60622c8c60bf`.
  Vollständige .NET-Suite nach dieser Nachkorrektur nicht erneut ausgeführt.

- **PASS:** vollständiger .NET-Release-Testlauf, 427 erfolgreich, 0 übersprungen:
  Architektur 4, Catering 208, Migrationen 20, Package 17, Platform 94, Security 84.
- **PASS:** SQLite und isoliertes PostgreSQL auf Port 55439; aktuelle Migrationen
  und bestehende Upgrade-Tests enthalten.
- Neue/angepasste Fälle: Camp-Blattzuordnung und Strukturänderungsschutz,
  minimierter Contract, Filter, abgeleitete Sonderverpflegung, Anwesenheit,
  Überlappung, reale Basis/RequirementGroups, Actual 0, eindeutige und
  mehrdeutige Bestandsmigration sowie Package-Replace. Frühere direkte
  Zuordnungstests wurden durch Strukturmodelltests ersetzt.
- **PASS:** Angular 27 Tests; aktueller Production Build.
- Nachkorrektur der Teilnehmerauswahl: API liefert die erforderliche Strukturtiefe;
  bei fester Tiefe bietet das Dropdown nur Blattknoten auf der letzten Ebene an.
  Angular erneut **28 PASS**, einschließlich Regression für zu flache Blätter
  und freie Struktur. Manuelle Strukturzuordnung vom Benutzer bestätigt: **PASS**.
- **PASS:** aktuelles self-contained win-x64 Sidecar Publish.
- **PASS:** `tools/test-desktop-roundtrip.ps1 -IncludeParticipants`:
  Strukturzuordnung/Filter Cloud → Lokal → Cloud, lokale Änderungen,
  Health-Zugriffsgrenze, lokale Entfernung und Ablehnung veralteter Rückpakete.
  Artefakte: `%TEMP%/scp-roundtrip-0c58f234-a3e5-4699-a1ca-bf87ae4f58c9`.
- Vorheriger Rundlauf scheiterte bei lokaler Entfernung an der neuen
  Knotenreferenz. Löschreihenfolge korrigiert; aktueller Wiederholungslauf PASS.
- **PASS (2026-10-07):** aktueller Tauri Release, MSI und NSIS, inklusive
  Dropdown- und Rezeptreferenz-Nachkorrektur. Sidecar frisch veröffentlicht, Frontend im
  Tauri-Build neu gebaut. Installer-Quelldateien geprüft; Sidecar-Publish,
  Tauri-Binary und MSI-Staging haben identischen SHA-256
  `A66BB8EC8C66ED89CB561ABE434757E2B159FD052BCA2C2ABC732346125E32B5`.
  MSI/NSIS referenzieren die aktuelle Desktop-EXE und dieses Sidecar.
  Keine Build-Probleme. Artefakte:
  - `src/desktop/src-tauri/target/release/bundle/msi/ScoutCampPlanner_0.1.0_x64_en-US.msi`
  - `src/desktop/src-tauri/target/release/bundle/nsis/ScoutCampPlanner_0.1.0_x64-setup.exe`
  Installierter Desktop-Roundtrip vom Benutzer bestätigt: **PASS**.

## Manuelle Prüfungen

Benutzerbestätigte manuelle Abnahme am 2026-10-07:

- Strukturzuordnung: **PASS**.
- CookingUnit mit realer Bedarfsbasis: **PASS**.
- Komplementäre Sonderverpflegungsfilter: **PASS**.
- Überlappungsschutz: **PASS**.
- Anwesenheit/Verpflegungsprofil und Neuberechnung: **PASS**.
- Strukturschutz: **PASS**.
- Installierter Desktop-Roundtrip Cloud → lokal → Cloud: **PASS**.

Die separate Web-Session-Nachkorrektur ist damit nicht zusätzlich manuell
abgenommen; deren interaktiver Test bleibt **NOT_RUN**.

## Noch offen / echte Blocker / Risiken

- Kein neuer fachlicher Blocker für die Korrektur. Mehrdeutiger Altbestand
  benötigt bewusste Bereinigung; keine automatische Umdeutung.
- SupplySolutions, Konfliktauflösung, manuelle Versorgungsentscheidungen,
  vollständige selektive Invalidierung und Verifikation bleiben ausstehender
  Inkrement-2-Scope; hier nicht neu geplant.
- Strukturwarnungen/Invalidierung aus Inkrement 1 sind teilweise breiter als
  die tatsächliche Bedarfsbasis. Vollständige Nebenläufigkeitsabsicherung
  paralleler Struktur-/Teilnehmeränderungen ist nicht nachgewiesen.
- Keine aktive Serving-Ausgabeprojektion: deren eigene Rechteprüfung ist
  noch nicht ausführbar; bestehende Health-Grenzen sind getestet.
- Sensible Offline-Daten ausschließlich Dummy-/Testdaten; keine Produktionsfreigabe.

## Commit-Bereitschaft

Zwischenmeilenstein abgenommen und für den beauftragten Zwischen-Commit bereit:
`Implement structure-based participant planning for cooking units`.
Enthält die bisher uncommitteten Grundlagen und zugehörigen Nachkorrekturen.
Kein Abschluss von Inkrement 2; danach Stopp ohne Erweiterung des offenen Scopes.
