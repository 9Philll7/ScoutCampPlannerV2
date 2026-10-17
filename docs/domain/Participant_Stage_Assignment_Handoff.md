# Übergabe: Stufenzuteilung erfasster Teilnehmer

Stand: 2026-10-17. Der aktuelle Frontend-Stand wird als Checkpoint gesichert.

## Auftrag für den nächsten Chat

Die Stufenzuteilung fehlt im Teilnehmereditor, weil sie auch im Teilnehmermodell und in den API-Verträgen fehlt. Das nächste funktionale Inkrement soll diese Lücke durchgängig schließen und anschließend erfasste Teilnehmer sowie deren Stufenverteilung in der Strukturansicht gemäß Mockups darstellen.

Die bisherige Vorgabe erlaubt ausschließlich Frontend-Änderungen und Dokumentation notwendiger API-Änderungen. Vor der Umsetzung muss der neue Chat die Freigabe für die erforderlichen Backend-, API-, Schema- und Offlinevertragsänderungen klären. Dieses Übergabedokument hebt die Einschränkung nicht auf.

## Einstieg und vorhandene Arbeit

- [Analyse und offene Anforderungen](Participant_Stage_Distribution_Gaps.md)
- [UX-Spezifikation](../architecture/UX_Increment_2_Structure_Participants_Spec.md)
- [Mockup-Referenz](../architecture/Mockup_Reference_UX_1_2.md), PNGs unter `docs/architecture/mockups/`
- Teilnehmermodell: `src/backend/ScoutCampPlanner.Camp.Domain/Participant.cs`
- Verträge: `src/backend/ScoutCampPlanner.Camp.Contracts/ParticipantContracts.cs`
- Bearbeitung und Berechtigungen: `src/backend/ScoutCampPlanner.Api/Camps/ParticipantManagementService.cs`
- Frontend-Vertrag: `src/frontend/src/app/features/camp/participants-api.service.ts`
- Vorhandene Schätzverteilung: `src/frontend/src/app/features/camp/stage-distribution.component.ts`

Der aktuelle Checkpoint ergänzt eine aufklappbare **geschätzte** Stufenverteilung für Lagerwurzel und beliebige Teilbäume. Diese verwendet ausschließlich vorhandene APIs. Sie liefert keine erfassten Teilnehmerzahlen. Validiert mit 93 bestandenen Angular-Tests, Production Build und Edge-Sichtprüfung; Nachweise im Analysedokument.

## Fachliche Leitplanken und Abnahme

1. Teilnehmer erhalten eine explizite Lagerstufe und eine unabhängig festgelegte KiJu-/Erwachsenenkategorie. Kategorienamen und Pflichtfelder vor Implementierung fachlich festlegen.
2. Bestehende Teilnehmer bleiben zunächst ausdrücklich „nicht zugeordnet“. Keine Ableitung aus Struktur, Namen oder Schätzwerten.
3. Editor lädt und speichert die Zuordnung; ungültige und lagerfremde Stufen werden abgewiesen. Bestehende Ernährungs- und Anwesenheitsdaten bleiben erhalten.
4. Strukturansicht zeigt erfasste Zahlen und Stufenverteilung für Lager und Teilbäume einschließlich nicht zugeordneter Teilnehmer. Schätzwerte bleiben klar getrennt; keine Doppelzählung von Kindknoten.
5. Für Organisationszahlen einen datensparsamen Aggregationsvertrag mit expliziten Berechtigungen verwenden. Die derzeit gesundheitsdatengeschützte Teilnehmerliste darf nicht allgemein für Zählungen geladen werden.
6. Migration, Offline-Paket, Import/Export, StateToken und konkurrierende Änderungen berücksichtigen. Verhalten beim Löschen einer verwendeten Stufe ausdrücklich festlegen.
7. Persistenz, Berechtigungen, Altbestand, Teilbaumaggregation und Paket-Rundlauf gezielt prüfen; anschließend Editor und Strukturansicht in Edge gegen Mockups prüfen und Screenshots ablegen.

Keine implizite Änderung der Verpflegungsberechnung durch die neue Zuordnung.
