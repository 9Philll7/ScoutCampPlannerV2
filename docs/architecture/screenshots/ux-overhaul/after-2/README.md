# UX Overhaul 2 – Sichtprüfung in Edge

Stand: 2026-10-16. **Teilprüfung, keine abschließende visuelle Freigabe.**

Die angemeldete lokale Anwendung wurde neu geladen und mit einem eingestellten CSS-Viewport von 1920×1080 geprüft. Die Testdaten enthalten inzwischen gefüllte Schätzwerte (Obergruppe 15 KiJu / 3 Erwachsene); diese wurden ausschließlich gelesen. Keine Daten gespeichert oder gelöscht.

## Gesicherte Bilder

- [Untergruppenliste](structure-parent-1920x1080.jpg)
- [Knotenmenü im Bearbeitungsmodus](structure-node-menu-1920x1080.jpg)
- [Namenseditor](structure-rename-1920x1080.jpg)

## Bestätigt

- Inaktive Sidebar-Einträge und Rückweg sind nun hell und lesbar. Der zuvor dokumentierte Desktop-Kontrastfehler ist sichtbar korrigiert; keine vollständige WCAG-Messung behauptet.
- Untergruppenliste zeigt sauber ausgerichtete Werte und kompaktere Zeilen. Auswahl und Anzeige der gefüllten Schätzwerte funktionieren.
- Knotenmenü öffnet am gewählten Baumknoten. Umbenennen/Verschieben sichtbar, Löschen beim belegten Knoten deaktiviert. Keine Lösch-/Verschiebeaktion ausgeführt.
- Umbenennen öffnet das richtige, bereits befüllte Namensfeld. Ohne Namensänderung ist „Namen speichern“ deaktiviert. Abbrechen kehrt zur lesenden Felddarstellung zurück.
- Ansichtswechsel, Modusmeldung und Detailtitel überlappen auf Desktop nicht.

## Befund: unnötige Warnung nach unverändertem Editor

Nach Öffnen von „Umbenennen“ und anschließendem Abbrechen ohne Eingabe zeigt „Bearbeitung beenden“ die Warnung „Bereich verlassen? Bitte speichere vorher deine Änderungen.“ Das lässt ungespeicherte Änderungen vermuten, obwohl keine vorgenommen wurden.

Die Codeprüfung erklärt den Befund: `hasPotentialEdits()` behandelt jedes nicht deaktivierte Eingabefeld als potentiell ungespeichert, auch ein readonly-Namensfeld. Ebenso sind Such-/Filterfelder der neuen Teilnehmerliste bislang nicht von diesem konservativen Guard ausgenommen. Letzteres ist eine Schlussfolgerung aus dem Code, noch kein gerenderter Nachweis in dieser Prüfung. Den gemeinsamen Guard gezielt nach Feldzweck/echtem Entwurfszustand abzugrenzen ist ein Folgepunkt; echter Entwurfsschutz darf dabei nicht entfallen.

Der browserseitige Bestätigungsdialog blockierte die weitere Steuerung; der Versuch, ihn über die dokumentierte Dialogschnittstelle zu bestätigen, lief in einen Timeout. Nutzer wurde gebeten, die Meldung selbst zu bestätigen. Bis dahin sind Teilnehmerliste, Vorschau, Filter, Teilnehmereditor und 1024-/390px-Ansichten **noch nicht gerendert geprüft**. Keine erneuten Tests oder Codeänderungen im Rahmen dieser reinen Sichtprüfung.

## Gestalterische Einordnung

Die Strukturansicht ist durch kompaktere Tabelle und Aktionen am Knoten näher an den Referenzen. Der rechte Editor bleibt gegenüber dem Mockup sehr sparsam: ein über die ganze Detailbreite laufendes Namensfeld, separater zusätzlicher Bearbeitungseinstieg und weit auseinanderliegende Metadaten statt einer kompakten Formulargruppe. Die Daten-/Persistenzgrenzen erklären fehlende Felder und globales Speichern, aber nicht die gesamte räumliche Abweichung. Dies bleibt ein konkreter Gestaltungsrestpunkt.
