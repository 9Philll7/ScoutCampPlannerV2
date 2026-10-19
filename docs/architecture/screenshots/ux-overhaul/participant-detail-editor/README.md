# Teilnehmerdetail und Editor

## Visuelle Vertiefung

Nach der ersten Abnahme wurden die Listenmuster weiter aufgelöst: Faktenkacheln in der Leseansicht, getönter Personenkopf, Ernährungsformen als auswählbare Karten, Allergene als Checkbox-Chips, Unverträglichkeiten als Karten und Abwesenheiten als Tages-/Mahlzeitenkacheln. Native Checkboxen und Feldsperren bleiben erhalten. Screenshots `health-cards-1920.jpg` und `diet-cards-1920.jpg` zeigen den neuen Stand; die anderen Bilder zeigen den ersten Entwurf.

109 Tests erfolgreich, einschließlich Auswahl und Leeren einer Ernährungskachel ohne Speichern; Production Build erfolgreich. Edge 1920 × 1080: Ernährung und Gesundheit geöffnet und visuell geprüft, unveränderten neuen Entwurf abgebrochen, Viewport zurückgesetzt. Keine Daten gespeichert.

Stand: 2026-10-19. Referenzen: `participant_detail_reference.png` und `participant_edit_reference.png`.

Die Vorschau bietet „Detailansicht öffnen“. Die vollständige Leseansicht ersetzt die Liste und zeigt Personenkopf, Kategorie/Stufe/Pfad sowie Karten für allgemeine Angaben, Struktur, Ernährung/Gesundheit und Anwesenheit. Zurück führt zur bestehenden Liste. Bearbeiten öffnet einen gemeinsamen Entwurf mit Speichern/Abbrechen im Kopf und Bereichsnavigation für Allgemein, Struktur & Gruppe, Ernährung, Gesundheit und Anwesenheit. Bereiche erhalten denselben Entwurf; Wechsel zwischen ihnen speichert nichts. Verlassen geänderter Entwürfe bleibt geschützt. Gesundheitsauswahl mehrspaltig auf Desktop, einspaltig auf schmalen Ansichten.

Verwendet werden ausschließlich vorhandene Vertragsdaten und Rechte. Anzeigename bleibt ein Feld; keine Aufteilung in Vor-/Nachname. Foto, Geburtsdatum, Geschlecht, Notfallkontakt, Notizen, Historie und ein fachlicher Aktivstatus aus dem Mockup wurden mangels Vertragsgrundlage nicht ergänzt. Ebenso keine Behauptung über eine geänderte Verpflegungsberechnung. Fehlende Gesundheitsangaben werden als fehlende Angaben beschrieben.

Edge bei 1920 × 1080: vorhandenen Dummy-Teilnehmer mit Milchallergie aus der Vorschau vollständig geöffnet, Gesundheitsangabe in der Detailkarte sichtbar; Editor Allgemein und Bereichswechsel zu Gesundheit geprüft. Keine Daten gespeichert. Screenshots `detail-1920.jpg` und `editor-general-1920.jpg`; der nachfolgende mehrspaltige Gesundheitsbereich ist darin nicht abgebildet. Viewport zurückgesetzt. Schmale Ansicht und tatsächliches Speichern in diesem Browserlauf nicht erneut geprüft.

108 Tests und Production Build erfolgreich. Zusätzlicher Test für vollständige Leseansicht ohne Entwurf, Bereichswechsel ohne Mutation und Verwerfprüfung. Bestehende Speicher-, Rechte- und Konflikttests bleiben erfolgreich. Keine API-/Backendänderungen.
