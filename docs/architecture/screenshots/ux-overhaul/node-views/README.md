# Überknoten und Endpunkt

Stand: 2026-10-18. Referenzen: `structure_parent.png` und `structure_leaf.png` unter `docs/architecture/mockups/ux-2/`.

- Überknoten: Untergruppenliste und bestehende Stufendiagramme bleiben erhalten.
- Zulässiger Endknoten: direkte Teilnehmerliste anstelle des bisherigen Links zur lagerweiten Verwaltung; keine Untergruppenliste oder Stufendiagramme. Zu flache Endknoten einer festen Hierarchie behalten den fachlichen Hinweis statt einer unzulässigen Erfassung.
- Eingebettete Liste ist fest auf den ausgewählten Knoten begrenzt. Suche und Filterrücksetzung heben diese Begrenzung nicht auf. Neue Teilnehmer erhalten die bestehende Knotenvorbelegung. Vorschau, Editor, Berechtigungen und Navigation mit Entwurfsprüfung werden wiederverwendet. Eine nachträglich geänderte Strukturzuordnung im Editor bleibt möglich; der Teilnehmer verschwindet danach gegebenenfalls aus dieser Liste.
- Header zeigt erfasste KiJu/Erwachsene neben den Schätzwerten, gegebenenfalls offene Kategorien. Überknoten summieren ihren Teilbaum; Lagerwurzel umfasst auch Teilnehmer ohne Struktur. Fehlgeschlagene Abrufe erscheinen als nicht verfügbar. Nach erfolgreicher Teilnehmeränderung wird die Headerstatistik neu geladen.
- Keine API-/Backendänderung. Allgemeine Kennzahlen verwenden nur `participant-counts`; die eingebettete Teilnehmerliste benötigt weiterhin das bestehende explizite Leserecht.

## Prüfung

100 Tests in 19 Dateien bestanden; Production Build und `git diff --check` erfolgreich. Zusätzliche Regressionen für feste Listeneingrenzung beim Knotenwechsel und Headeraggregation einschließlich Fehlerfall und Aktualisierung.

Edge mit vorhandenem Dummy-Lager geprüft: Lager und Überknoten 28/4 erfasst, 50/14 geschätzt; Überknoten mit fünf Untergruppen und beiden Diagrammen. Endknoten „TEST Gruppe A All“: 8/2 erfasst, 10/2 geschätzt, zehn Teilnehmer direkt in der Liste, keine Stufendiagramme. Wechsel zwischen Überknoten und Endknoten geprüft; keine Daten verändert.

Screenshots bei tatsächlichem Viewport ca. 935 × 932: `parent.jpg`, `leaf-header.jpg`, `leaf-list.jpg`. Vollseitenaufnahme schlug mit Browser-Timeout fehl; stattdessen Ausschnitte des sichtbaren Viewports gespeichert. Breite Desktopansicht und tatsächliches Speichern wurden in diesem Browserlauf nicht erneut geprüft.

Nicht Teil dieses Schritts: zusätzliche Differenzkarte, neue Filter nach Stufe/Kategorie und erfasste Spalten im Strukturbaum bzw. der Untergruppenliste.
