# UX Increment 2.1 – Strukturverwaltung

Stand: 2026-10-15. IMPLEMENTED_WITH_DEVIATIONS – bereit zur manuellen Prüfung, keine uneingeschränkte UX-Abnahme. Kein Commit.

## Ist-Analyse vor Implementierung

- Strukturansicht bisher im AppComponent: eingerückte Karten, Ein-/Ausklappen, Anlegen, Umbenennen, Verschieben und Löschen. API und Backend validieren weiterhin alle Operationen. Freie und definierte Tiefe vorhanden; nur teilnehmerfähige Blattknoten dürfen Schätzungen erhalten.
- Strukturänderungen werden einzeln sofort nach expliziter Aktionsbestätigung persistiert. Kein transaktionaler Gesamtentwurf: kein erfundenes Sammelspeichern/-abbrechen. Bearbeitungsmodus wird daher mit Hinweis auf einzelnes Speichern beendet.
- Schätzung unterstützt Stufe × KiJu/Leiter, explizites PUT und Löschen durch Nullsetzen. PlanningSummary liefert aggregierte Schätzwerte pro Strukturknoten und lagerweite Stufensummen. Keine Addition geschätzt + erfasst.
- ParticipantData enthält Namen und Strukturzuordnung, aber keine Stufe oder KiJu-/Erwachsenenkategorie. Erfasste KiJu-/Erwachsenenzahlen aus den Mockups sind damit nicht belegbar; keine Schemaerweiterung oder Interpretation von Knotennamen.
- Kein bestehender Suchzustand im Baum. Keine neue Suche notwendig. Bestehende Teilnehmeransicht bleibt der Erfassungsweg; ihr Detailredesign gehört zu UX 2.2.
- Mockups gelesen: zweigeteilte Fläche und Auswahl übernehmen; keine Illustrationszahlen, Differenzspalten, Knotentypen oder neuen Rechte übernehmen. Bestehende canEdit-/Frozen-Bedingungen und Backendvalidierung bleiben verbindlich.

## Implementierte Änderungen

- Zweigeteilte Arbeitsfläche (38/62), links dauerhaft auswählbarer Baum mit Ein-/Ausklappen, Auswahlmarkierung, Ebenenbezeichnungen und vorhandenen Schätzaggregaten; rechts ausgewählter Knoten und vollständiger Pfad. Unter 900px untereinander. Lager-Sidebar unverändert.
- Oberknoten zeigen Unterknoten und vorhandene Aggregationen, keine direkten Schätz-/Teilnehmeraktionen. Unvollständige Zweige definierter Strukturen erhalten einen erklärenden Hinweis. Lagerweite Stufenauswertung bleibt explizit über den Info-Button aufklappbar.
- Normalmodus lesend; berechtigte Benutzer können den klar gekennzeichneten Struktur-Bearbeitungsmodus aktivieren. Bestehende Einzelaktionen und Frozen-Schutz bleiben erhalten. Wechsel/Beenden verwendet vorhandenen konservativen Dirty-Guard.
- Schätzungen in separatem nativem modalen Dialog mit beschrifteten Zahlenfeldern, vorhandenen Stufen, Zeilen-/Gesamtsummen, Speichern, Löschen und Abbrechen. Escape/Abbrechen fragen bei geändertem Entwurf nach; Fehler bleiben im Dialog sichtbar. Speichern schließt erst nach erfolgreichem API-Ergebnis. Browser übernimmt Modal-Fokusführung und Rückgabe.
- Teilnehmer-Erfassungsweg bleibt die vorhandene Ansicht. Einstieg aus einem zulässigen Blatt belegt bei „Testteilnehmer anlegen“ die Struktur vor; ungültige/übergeordnete Knoten werden nicht vorbelegt. Die bestehende Liste bleibt ausdrücklich lagerweit, kein Teilnehmerdetailredesign.
- Fehlermeldungen präzisiert: maximale Tiefe, belegte Knoten (Backend verwendet denselben HasEstimates-Code für Schätzungen und tatsächliche Teilnehmer), unzulässige Schätzebene, Teilnehmerkonflikt beim Verschieben. Kein Backendcode oder API-Vertrag geändert.

## Abweichungen / Entscheidungsvorlagen

- Kein Sammelspeichern und kein globales Rückgängigmachen bereits persistierter Strukturaktionen. Dafür wäre ein separater fachlicher Persistenzauftrag nötig.
- Erfasste KiJu-/Erwachsenenzahlen und erfasste Stufenauswertung fehlen im Teilnehmervertrag. Nicht durch Schätzung oder Knotennamen ersetzt. Für eine spätere Erweiterung gesonderte Fach-/Vertragsfreigabe notwendig.
- Knotenbezogene Stufenaggregate fehlen im PlanningSummary; nur vorhandene lagerweite Stufentabelle genutzt. Keine zusätzlichen Aggregations-APIs.
- Direkte gefilterte Teilnehmerliste im Knotendetail nicht umgesetzt; vorhandener Erfassungs-Einstieg mit Blattvorbelegung verwendet (zulässiger reduzierter Umfang des Inkrementauftrags). Teilnehmeransicht bleibt UX-2.2-Folgeblock.
- Baum besitzt bisher keine Suche; keine neue Suche hinzugefügt. Kein Erfassen sensibler Daten allein zur Anzeige von Strukturkennzahlen.
- Vorhandene Strukturantwort liefert keine Belegungsflagge für tatsächliche Teilnehmer. Manche Aktionen können daher weiterhin erst durch Backendvalidierung abgewiesen werden; jetzt korrekt erläutert. Keine Rechte aufgeweicht.

## Tests

- PASS: vollständige Angular-Suite, 79 Tests / 15 Dateien. Neuer Scope: freie/definierte Tiefe, Oberknoten ohne Blattaktionen, Leserechte, individueller Editmodus, kollabierte Zweige, Aggregation ohne Eltern-Doppelzählung, gemischte Schätzmatrix, ungültige Werte, Abbrechen/Bestätigung, Speichern und Erhalt bei API-Konflikt, zulässige Teilnehmer-Vorbelegung. Bestehende Navigation-, Verpflegungs-, Session- und Pakettests enthalten.
- Erster Teststart: Vitest forks runner timeout, keine Tests ausgeführt; Wiederholung erfolgreich.
- PASS: abschließender Production Build und git diff --check. Abschließende Angular-Wiederholung ebenfalls 79/79 PASS. Git-Status enthält nur die fünf betroffenen Frontend-/Testdateien und diesen Bericht, keine generierten Artefakte.
- NOT_RUN: eigene Browser-/Desktop-/mobile Sichtprüfung, native Dialogfokus-Interaktion im echten Browser, manueller Offline-Roundtrip. Keine Browseranbindung; Nutzer bevorzugt Screenshots statt zusätzlicher Browserinstallation.
- Backend-/Provider-Tests nicht erneut ausgeführt: keine Backend-, Schema-, Berechtigungs-, Kalkulations- oder Paketänderung.

## Manuelle Prüfschritte

1. Freien Baum mit unterschiedlich tiefen Zweigen öffnen; auswählen, ein-/ausklappen, Pfad und Schätzzahlen prüfen. Gleichnamige Stufen/Knoten dürfen keine Zuordnungswirkung haben.
2. Definierten Baum prüfen: zu flacher Blattknoten ohne Teilnehmer-/Schätzaktion, letzte Ebene mit Aktion. Oberknoten ohne direkte Erfassung.
3. Struktur bearbeiten: Anlegen, Umbenennen, Verschieben, Löschen; gespeicherte Aktionen bleiben nach Beenden bestehen. Ungespeicherte Eingaben beim Wechsel nicht unbemerkt verlieren. Belegten Knoten prüfen: Backendfehler lesbar.
4. Gemischte Stufenmatrix ändern, Summen prüfen, Abbrechen/Escape ablehnen und bestätigen, speichern und neu öffnen, löschen. Tab-Fokus muss im geöffneten Dialog bleiben und nach Schließen zurückkehren.
5. Aus Blatt Teilnehmerverwaltung öffnen und neuen Testteilnehmer beginnen: zulässige Zuordnung vorbelegt. Keine echten Gesundheitsdaten verwenden.
6. Rein lesendes und eingefrorenes Lager prüfen. Desktop und 390px-Screenshot der Baum-/Detailfläche und des Schätzdialogs erstellen; vorhandene Verpflegungs-/Offline-Flows stichprobenartig prüfen.

## Commit-Bereitschaft

Kein Commit. Nach manueller Abnahme des reduzierten, dokumentierten Umfangs commitfähig; fehlende erfasste Kategorien nicht als implementiert betrachten. Bestehende Funktionen/Verträge erhalten, keine neuen Dependencies.
