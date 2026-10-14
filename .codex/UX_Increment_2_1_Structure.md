# Codex – UX Increment 2.1: Strukturverwaltung

## Ziel

Implementiere das UX-Redesign der Lagerstruktur auf Grundlage von `docs/architecture/UX_Increment_2_Structure_Participants_Spec.md` und `docs/architecture/Visual_Design_Guide.md`. Visuelle Referenzen: `docs/architecture/mockups/ux-2/structure_parent.png`, `structure_leaf.png`, `structure_editor_read.png`, `structure_editor_edit.png`.

## Phase A – verbindliche Ist-Analyse

1. Prüfe aktuelle Komponenten für Strukturbaum, Knotenansicht, Struktureditor, Schätzwerteditor und zugehörige API-/Berechtigungsregeln.
2. Ermittle bei freier und definierter Struktur die bereits vorhandenen Editieroperationen und ihre Speichersemantik (sofort oder explizit).
3. Bestätige, dass Schätzungen nach **Stufe × KiJu/Leiter-Erwachsene** pro Blattknoten bereits fachlich unterstützt sind; falls nicht, **nicht** durch Backend-/Schemaänderung nachrüsten.
4. Prüfe bestehende Validierungsregeln bei belegten Blattknoten, Hierarchieoperationen und Aggregationen; keine Regeln aus dem Mockup ableiten.
5. Dokumentiere bestehende Fähigkeiten und Lücken vor dem Implementieren.

## Phase B – UI

- Bestehende linke **Lager-Sidebar beibehalten**, keine zweite horizontale Hauptnavigation.
- Lokale Ansicht **Struktur** mit dauerhaftem Baum links (ca. 35–40 %) und dynamischem Knotendetail rechts (ca. 60–65 %); schmale Breiten sinnvoll adaptieren.
- Baum unterstützt freie und definierte Strukturen, Suche soweit vorhanden, Ein-/Ausklappen, sichtbare Auswahl; Knotenname wird niemals als Stufe interpretiert.
- Übergeordnete Knoten: Unterknoten, Aggregation aus Blattknoten; keine Aktionen zum direkten Hinzufügen von Teilnehmern oder Schätzwerten. Keine Differenzspalte. Optionale Stufenauswertung standardmäßig zugeklappt, sofern vorhandene Daten das erlauben.
- Blattknoten: Teilnehmerliste/Erfassungs-Einstieg, aggregierte KiJu-/Leiterzahlen; „Schätzung bearbeiten“ öffnet separaten Dialog, mit existierenden Stufen und einer Matrix für beide Kategorien und errechneter Summe. Keine Doppelzählung.
- „Struktur bearbeiten“ nur bei Berechtigung; Normalmodus lesend, Bearbeitungsmodus klar markiert und auf zulässige Aktionen begrenzt.
- Gemeinsame Speichern-/Abbrechen-Aktionen **nur falls durch bestehende Änderungs-/Persistenzsemantik gedeckt**. Andernfalls aktuelles fachliches Speicherverhalten bewahren und Abweichung als Entscheidungsvorlage dokumentieren.
- Fehlermeldungen aus Backendvalidierung verständlich und konkret anzeigen, ohne sie zu verschlucken.

## Nicht im Scope

Keine neuen API-Verträge, Schemaänderungen, Rollen, Berechtigungen, Phasen, Delegationszugänge, Teilnehmer-Detailredesigns oder neue Fachlogik. Keine fiktiven Felder, Stufen oder Knotentypen. Keine Änderung an Verpflegung oder Offline-Paket.

## Tests/Abnahme

- Definierte und freie Struktur, verschiedene Hierarchietiefen, Blatt- und Oberknoten.
- Schätzwert-Matrix für gemischte Stufen, KiJu/Erw. und korrekte Aggregationen im UI.
- Keine Blatt-Aktionen auf Oberknoten; Invalid-Edit-Fehler lesbar; Rechtefälle und Tastaturbedienung.
- Abbrechen, Speichern sowie Navigation/Dirty-Guard nur entsprechend nachgewiesener vorhandener Semantik.
- Angular-Tests + Production Build; relevante bestehende Backend-/Integrationstests nur falls Änderungen dort nötig werden. Browser-/manuelle Sichtprüfung als PASS oder NOT_RUN korrekt ausweisen.
- Bestehende Funktionen, insbesondere Teilnehmer-, Verpflegungs- und Offline-Flüsse, dürfen nicht regressieren.

## Ergebnis

Erstelle `.codex/ux-increment-2-1-result.md`: Ist-Analyse, implementierte Änderungen, klare Abweichungen, Testergebnisse, manuelle Prüfschritte und Commit-Bereitschaft. **Keinen Commit erstellen.** Bereits uncommittete Änderungen nicht zurücksetzen.
