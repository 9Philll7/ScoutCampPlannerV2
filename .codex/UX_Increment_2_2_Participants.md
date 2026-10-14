# Codex – UX Increment 2.2: Teilnehmerverwaltung

## Voraussetzung

UX 2.1 soll abgeschlossen und manuell geprüft sein; dessen vereinbarte Strukturnavigation wird weiterverwendet. Grundlage: `docs/architecture/UX_Increment_2_Structure_Participants_Spec.md`, `docs/architecture/Visual_Design_Guide.md`. Referenzen: `docs/architecture/mockups/ux-2/participants_table.png`, `participant_detail_reference.png`, `participant_edit_reference.png`.

## Phase A – verbindliche Ist-Analyse

- Bestehende Teilnehmerfelder, Stufe, KiJu/Leiter-Kategorie und **Anwesenheit** mit zulässigen Werten/Zeiträumen und Kalkulationsbezug feststellen. Keine Anwesenheits-Boolean- oder Aktivstatus-Logik erfinden.
- Vorhandene Teilnehmerlisten, Filter, Detail-/Editabläufe und Gruppenzuordnung sowie Rechteprüfungen prüfen.
- Insbesondere klären, ob der vorhandene API-Vertrag ein gemeinsames sicheres Speichern unter **feldweisen Lese-/Schreibrechten** trägt, ohne verborgene sensible Werte zurückzusetzen. Fehlt diese Eigenschaft, Implementierung der betroffenen Editierfunktion aussetzen und explizit als fachlich-technischen Blocker dokumentieren.
- Rechte für Ernährung/Gesundheit über Tabelle, Filter, Vorschau, Detailansicht, Export und Offline-Kontext prüfen; vorhandene Backenddurchsetzung respektieren.

## Phase B – UI

- Lagerweite Tabelle für Name, Strukturzuordnung, Stufe, KiJu/Leiter; Suche, Strukturzweigfilter, Stufen-/Kategorie-Filter und Sortierung gemäß vorhandenen Daten. Optional bestehende sensible Felder nur bei effektiver Berechtigung, auch in Suche/Filtern.
- Maximal kompakte Summen zu Anzahl Gesamt, KiJu, Leiter/Erwachsene; keine erfundenen Gesundheitskennzahlen.
- Klick auf Tabellenzeile zeigt **lesende rechte Schnellvorschau**; vollständige Detailseite über Aktion „Details öffnen“. Filter, Sortierung, Pagination und Kontext bei Rückkehr bewahren. Vorschau bei schmalen Breiten nicht auf Kosten der Tabellenlesbarkeit dauerhaft abbilden.
- Detailseite als **eine Seite mit Abschnitten**, keine zusätzlichen Haupttabs. Ansicht startet lesend. Berechtigte Benutzer sehen „Bearbeiten“; ein gemeinsamer Bearbeitungsmodus für alle erlaubten Felder mit Speichern/Abbrechen, sofern der Vertrag dies sicher erlaubt.
- **Anwesenheit** in Darstellung und Bearbeitung auf Basis der realen Fachmodellfelder; nicht mit Aktiv/Inaktiv verwechseln.
- Teilnehmer aus Blattknoten anlegen mit automatisch übernommener gültiger Zuordnung; aus lagerweiter Liste muss zulässiger Blattknoten gewählt werden. Keine Zuordnung an Oberknoten.
- Berechtigungsabhängige Details und sensible Abschnitte komplett ausblenden, wo kein Leserecht besteht. Kein Informationsleck über Listen, Filter, Vorschau oder Datenexport.
- UI-Texte und Fehler klar; kein neues Modell für Fotos, Notfallkontakte, Änderungsverlauf, Hinweise oder Status.

## Nicht im Scope

Keine neue Datenbank-/API-/Rollenlogik, kein delegierter Gruppenverantwortlichenzugang (separates Zukunftskonzept), keine Änderungen an Kalkulationsregeln, Verpflegung, Phasen, Archivierung oder Offline-Speicherung. Fehlende Pflichtfunktionen als Folgeinkrement erfassen statt zu improvisieren.

## Tests und Ergebnis

Teste Teilnehmerliste, Filter, Strukturpfad, Auswahl/Preview, Detailnavigation, Anwesenheit, Schreib-/Leserechte und geschützte Felder, sichere Änderungen und Dirty-Guard, Rückkehrzustand, schmale Breiten. Angular-Tests und Production Build, fachliche Integrationstests nach geänderter Oberfläche; Ergebnisse als PASS/FAIL/NOT_RUN dokumentieren.

Erstelle `.codex/ux-increment-2-2-result.md` mit Bestandsaufnahme, Änderungen, blockierten Anforderungen, Testnachweisen und manueller Abnahmecheckliste. Kein Commit; uncommittete Fremdänderungen bewahren.
