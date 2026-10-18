# Teilnehmer: Stufe und Kategorie

Stand: 2026-10-18. Implementiert; manuelle Abnahme ausstehend. Kein Commit.

## Umsetzung

- Teilnehmereditor, Liste und Vorschau unterstützen optionale Lagerstufe und unabhängig davon KiJu (`ChildYouth`) / Leiter/Erwachsene (`LeaderAdult`). Altbestand bleibt ausdrücklich nicht zugeordnet.
- API validiert Kategorie und lagerzugehörige Stufe. Bestehende Health-Rechte, Konkurrenzprüfung, Ernährungs- und Anwesenheitsdaten bleiben erhalten.
- Datensparsame Zusammenfassung unter `GET /api/camps/{campId}/participant-counts` mit bestehendem `Camp.View`: nur gruppierte Zahlen nach Strukturknoten, Stufe und Kategorie, keine Namen oder Gesundheitsangaben. Strukturdetail zeigt erfasste Summen und Stufenverteilung getrennt von Schätzwerten; lagerweit auch Teilnehmer ohne Strukturzuordnung.
- Stufenänderungen erhalten IDs, vorhandene Schätzungen und Zuordnungen. Hinzufügen ist trotz Schätzwerten möglich. Verwendete Stufen können nicht entfernt werden. Der Editor sendet `StageIds`; ältere Aufrufer ohne IDs erhalten Identität nur bei unverändertem Namen.
- PostgreSQL-/SQLite-Migrationen `AddParticipantStageCategory`: nullable Felder und Index, keine geratenen Bestandswerte. Stufenreferenzen werden in API und Paket validiert; kein zusätzlicher Datenbank-Fremdschlüssel.
- Teilnehmer-Paketabschnitt Version 3 transportiert beide Felder; Versionen 1/2 bleiben lesbar. Alte Clients müssen aktualisiert werden. Sensible Offline-Daten bleiben ausschließlich Dummy-/Testfunktion ohne Produktionsfreigabe.
- Keine Änderung der Verpflegungsberechnung, keine neuen Berechtigungen.

## Prüfstand

- PASS: vollständige .NET-Release-Suite, 528 Tests einschließlich PostgreSQL und SQLite. PostgreSQL ausschließlich in separat angelegter Testdatenbank auf Port 55441; keine bestehende Anwendungsdatenbank migriert.
- PASS: Angular, 96 Tests in 17 Dateien; Production Build.
- Regressionen: API-Scope und Health-Abgrenzung, nullable Altbestand, Persistenz beider Provider, Paket-Rückimport, Editor-Speicherung, Teilbaumzählung und Fehleranzeige statt falscher Nullsumme.
- PASS: abschließender erweiterter Stufen-Regressionslauf (1 Test) mit bestehenden Schätzwerten, Hinzufügen, Umbenennen/Umsortieren unter Erhalt der IDs und Entfernungsschutz. Eingeschränkter Lauf ohne Ergebnis; Wiederholung außerhalb der Sandbox erfolgreich.
- PASS: `git diff --check`; keine generierten Build-Artefakte im Git-Status.
- NOT_RUN: manuelle Browserprüfung und installierter Desktop-Roundtrip mit neuen Feldern; kein neuer Desktop-Installer erzeugt.

## Manuelle Abnahme / Betrieb

1. Backend mit aktuellem Build neu starten und Migrationen über den bestehenden Startpfad anwenden; der bisher laufende Debug-Prozess wurde nicht beendet.
2. Stufe bei vorhandenen Schätzwerten hinzufügen, umbenennen und umsortieren; Werte und Teilnehmerzuordnung müssen erhalten bleiben.
3. Teilnehmer mit Stufe/Kategorie speichern und neu laden; beide Zuordnungen wieder leeren. Ernährung und Anwesenheit müssen unverändert bleiben.
4. Lagerwurzel und Teilbaum prüfen: erfasste Zahlen getrennt von Schätzungen, nicht zugeordnete Teilnehmer explizit sichtbar.
5. Löschen einer verwendeten Stufe muss abgewiesen werden. Mit aktualisiertem Desktop Dummy-Paket hin/zurück testen.

Die vollständige visuelle Übernahme erfasster Zahlen in sämtliche Baum-/Mockup-Spalten ist nicht Teil dieser Ergänzung: aktuell eigener Abschnitt im Strukturdetail. Vor Commit manuelle Abnahme durchführen.
