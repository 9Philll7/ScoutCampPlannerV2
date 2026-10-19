# Teilnehmerliste gemäß Mockup

Stand: 2026-10-19. Visuelle Referenz: `docs/architecture/mockups/ux-2/participants_table.png`.

Ergänzt: lagerweite Kennzahlen für Gesamt, KiJu, Erwachsene und Teilnehmer mit hinterlegter Ernährungsform; Filter für Stufe, Kategorie und Ernährung zusätzlich zum Strukturzweig und zur Suche. Die Filter wirken gemeinsam vor der Seitenteilung und werden gemeinsam zurückgesetzt. „Mit Ernährungsform“ zählt eine hinterlegte Zuordnung, keine abgeleitete medizinische Besonderheit.

Die Tabelle verwendet kompakte Kategorie- und Ernährungsbadges, kürzere Gruppennamen mit vollständigem Pfad als Titel, Stufenmarkierung und eine schmale Detailaktion. Der Zeilenklick und die Tastaturbuttons bleiben erhalten. Die Vorschau enthält ebenfalls die Ernährungsform und ist kompakter. Eingebettete Gruppenlisten erhalten die Badges und Filter, wiederholen aber nicht die lagerweiten Kennzahlen. Fünf Zeilen pro Seite bleiben fest.

Nicht nachgebildet: Auswahlkästchen ohne Sammelaktion sowie Notfallkontakt, Medikamente, Notizen und Metadaten ohne entsprechende Felder im aktuellen Vertrag. Keine API geändert.

Edge: lagerweite Liste mit 32 Teilnehmern und geöffneter Vorschau bei 1920 × 1080 visuell geprüft. Screenshot `overview-preview-1920.jpg`. Keine Testdaten verändert, Viewport zurückgesetzt. Schmale Ansicht und eingebettete Liste in diesem Schritt nicht erneut im Browser abgenommen.

107 Frontend-Tests bestanden; zusätzlicher Test für kombinierte Filter und Rücksetzung. Production Build und `git diff --check` erfolgreich.
