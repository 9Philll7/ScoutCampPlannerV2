# Detailtabs und 1920 × 1080 ohne Scrollen

Stand: 2026-10-18.

Überknoten und Lagerwurzel verwenden Angular-Material-Tabs „Untergruppen“ und „Stufenverteilung“. Die Kennzahlen bleiben darüber sichtbar. Beide Inhalte werden getrennt angezeigt; Tabellen, Diagramme und Seitennavigation bleiben erhalten. Eine leere Lagerwurzel zeigt einen Hinweis im Untergruppentab. Der Struktur-Bearbeitungsmodus bleibt gesondert.

Endknoten zeigen weiterhin direkt ihre Teilnehmer. Im eingebetteten Modus entfallen die doppelte Listen-Gesamtzahl (bereits im Header) und die redundante Unterzeile. Der Testdatenhinweis bleibt kompakt sichtbar. Der allgemeine Hinweis zu aggregierten Zahlen bleibt bei Überknoten. Filter, fünf Zeilen und Seitennavigation passen dadurch in die Detailkarte.

## Abnahme

- Edge bei 1920 × 1080 mit vorhandenen Dummy-Daten: Untergruppentab mit fünf Gruppen vollständig sichtbar; Stufentab mit beiden Diagrammen vollständig sichtbar. Endknoten mit fünf Teilnehmerzeilen und vollständiger Seitennavigation sichtbar.
- Gemessen: Stufentab und Endknoten jeweils 868px Kartenhöhe und 868px Inhaltshöhe; Dokumenthöhe 1080px. Keine vertikale Scrollstrecke in diesen Standardansichten. Screenshots: `subgroups-1920.jpg`, `stages-1920.jpg`, `participants-1920.jpg`.
- 103 Tests bestanden; Production Build und `git diff --check` erfolgreich. Regression prüft beide Tabs bei Überknoten und deren Abwesenheit am Endknoten.
- Keine Testdaten oder APIs verändert; temporären Viewport zurückgesetzt.

Die Abnahme gilt für die vorhandenen Daten mit fünf Zeilen je Seite. Bei zehn/zwanzig Zeilen, geöffneten Detailtabellen, Editor, langen Inhalten oder kleineren Fenstern bleibt Scrollen als erreichbarer Rückfall erhalten; Inhalte werden nicht abgeschnitten.
