# Erfasste Teilnehmer und Stufenverteilung – vorhandene Fähigkeiten und Lücken

## Aktualisierung 2026-10-18

Die nachfolgende Analyse beschreibt den historischen Stand vor der ausdrücklich beauftragten Umsetzung. Stufe/Kategorie, stabile Stufen-IDs, datensparsame erfasste Zahlen sowie Persistenz und Pakettransport sind inzwischen implementiert. Aktueller Prüfstand und verbleibende manuelle Abnahme: [Ergebnisbericht](../../.codex/participant-stage-assignment-result.md). Die frühere Einschränkung auf Frontend-Änderungen gilt für diesen freigegebenen Umsetzungsschritt nicht mehr. Die vollständige visuelle Integration in alle Mockup-Spalten bleibt davon getrennt.

## Historischer Stand: 2026-10-17

## Nachgewiesener Ist-Stand

- `Camp.Domain/Participant.cs` enthält Anzeigename, Strukturzuordnung, Ernährung und Anwesenheitsausnahmen. Es gibt weder `CampStageId` noch KiJu-/Erwachsenenkategorie am erfassten Teilnehmer.
- Der Vertrag `Camp.Contracts/ParticipantContracts.cs` und der Teilnehmer-Editvertrag bieten diese beiden Eigenschaften ebenfalls nicht an. Weitere Lagerstufen erzeugen daher keine Zuordnung vorhandener Teilnehmer.
- `GET /api/camps/{campId}/planning-summary` liefert ausschließlich Schätzwerte: lagerweit nach Stufe und je Strukturknoten aggregiert. „stageTotals“ bezeichnet keine erfassten Teilnehmer.
- `GET /api/camps/{campId}/structure/{nodeId}/participant-estimates` liefert direkte Schätzwerte dieses Knotens, für jede vorhandene Lagerstufe einschließlich Nullwerten. Die UI kann diese bestehenden Werte für einen Teilbaum aufsummieren.
- Die Teilnehmerliste liefert individuelle Dokumente einschließlich sensibler Angaben. Der Zugriff wird durch `Health.ReadParticipantRequirements` geschützt. Sie ist keine geeignete allgemeine Quelle für eine Strukturkennzahl für Benutzer mit ausschließlich `Camp.View`. Für reine Zahlen sollen keine vollständigen Gesundheitsdokumente neu angefordert werden.

## Umsetzung ohne API-Änderung

Die Strukturansicht bietet nun bei Lagerwurzel, Ober- und Blattknoten einen standardmäßig geschlossenen Abschnitt **Stufenverteilung (geschätzt)**. Für die Lagerwurzel werden vorhandene `stageTotals` genutzt. Für einen ausgewählten Teilbaum werden beim Aufklappen die direkten Schätzwerte jedes Knotens einmal geladen und je Stufe summiert. Fehler eines Teilabrufs werden nicht als vollständige Null-/Teilsumme angezeigt. Bei Kontextwechsel werden laufende Abrufe beendet und der Abschnitt geschlossen.

Stufen ohne Schätzwerte bleiben als Nullzeile sichtbar. Sind alle Werte null, erklärt die Ansicht, dass Stufen angelegt, aber keine Schätzwerte eingetragen sind. Geschätzte und erfasste Teilnehmer werden nicht addiert.

Für große Hierarchien entstehen bei erstmaligem Aufklappen entsprechend viele bestehende Abrufe. Ein künftiger serverseitig aggregierter Leservertrag wäre effizienter, wird hier aber nicht eingeführt.

## Erforderliches funktionales Folgeinkrement

1. Stufe und Kategorie als unabhängige Teilnehmerattribute fachlich festlegen und Erfassung/Bearbeitung ermöglichen. Knotenname darf niemals als Stufenkennung dienen. Keine Kategorie aus Namen oder Ernährung ableiten.
2. Umgang mit bestehenden Teilnehmern ohne Zuordnung entscheiden: explizit unbekannt/nicht zugeordnet darstellen, keine stillschweigende Übernahme aus Schätzungen. Historische Daten dürfen nicht als KiJu oder Erwachsene geraten werden.
3. Datensparsame Zusammenfassung für Anzahl erfasster Teilnehmer je Knoten/Teilbaum und je Stufe/Kategorie definieren. Lagerweite nicht zugeordnete Teilnehmer gesondert ausweisen. Kein Zugriff auf Gesundheitsangaben für reine Organisationszahlen.
4. Leserecht für diese Zusammenfassung ausdrücklich festlegen und im Backend durchsetzen. Sensible Filter und Daten unverändert getrennt schützen.
5. Paket-/Offlinevertrag, Migrationen, Export/Import und StateToken-/Concurrency-Verhalten für neue Teilnehmerattribute berücksichtigen. Keine neue Verpflegungsregel implizit aus der Darstellung ableiten.

Erst damit können die Mockup-Spalten „Erfasst KiJu/Erw.“ und die erfasste Stufenverteilung fachlich korrekt gefüllt werden. Eine bloße Gesamtzählung der bereits berechtigt geöffneten Teilnehmerliste ist möglich, ersetzt aber weder diese Attribute noch den allgemeinen Aggregationsvertrag.

## Prüfung dieser Frontend-Ergänzung

- PASS: 93 Angular-Tests in 16 Dateien; neue Tests für eingeklappten Start, Stufen ohne Werte, Summierung direkter Knotenschätzungen ohne doppelte IDs, Kontextwechsel und fehlgeschlagene Teilabrufe.
- PASS: Production Build, keine Angular-Templatewarnungen im Abschlusslauf.
- PASS: Edge-Sichtprüfung mit vorhandenen Testdaten. Lager und Obergruppe: TEST Stufe 25/6, TEST Stufe 2 25/8; Gesamtsumme 50/14 = 64. Blatt „TEST Gruppe B komplementaer“: TEST Stufe 0/0, TEST Stufe 2 15/5; Gesamtsumme 20. Keine Daten verändert.
- Bilder: `docs/architecture/screenshots/ux-overhaul/stage-distribution/parent-1920x1080.jpg` und `leaf-1920x1080.jpg` (volle Seiten bei eingestelltem 1920×1080-Viewport).
- Zwischenläufe: Worker-Starttimeout ohne ausgeführte Tests und Sandbox-`spawn EPERM`; erfolgreicher vollständiger Wiederholungslauf nach angeforderter Eskalation. Nicht als fachlicher Testfehler gewertet.
- Erfasste Stufen-/Kategorieverteilung ist weiterhin offen; keine API-/Backenddateien geändert.
