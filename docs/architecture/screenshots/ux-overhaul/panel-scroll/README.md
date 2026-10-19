# Gleich hohe Karten mit eigenem Scrollbereich

Stand: 2026-10-18.

Bei mehr als 1100px Fensterbreite und mindestens 600px Höhe füllt die Strukturansicht den verfügbaren Viewport. Seitenkopf, Statusmeldungen und Bearbeitungshinweis behalten ihre tatsächliche Höhe; der restliche Platz wird auf beide gleich hohen Karten verteilt. Struktur und Details scrollen unabhängig innerhalb ihrer Karten. Es werden keine Datensätze ausgeblendet oder abgeschnitten. Beide Bereiche sind per Tastatur fokussierbar. Beim Knotenwechsel beginnt der Detailbereich wieder oben.

Bei schmalen oder sehr niedrigen Fenstern gilt weiterhin der normale Seitenfluss, damit die Karten nicht zu winzigen Scrollfenstern werden. Die lagerweite Teilnehmeransicht wird durch die Begrenzung nicht verändert.

## Prüfung

- Edge 1920 × 1080: Seitenhöhe 1080px; beide Karten von y=178 bis y=1048. Scrollen der Detailkarte zeigt Diagramme, während Struktur und Seitenkopf stehen bleiben. Screenshot `parent-scrolled-1920.jpg`.
- Edge 1366 × 768: Struktur und Teilnehmerkarte mit eigenen Scrollbalken; Teilnehmerliste gescrollt, ausgewählte Gruppe weiterhin links sichtbar. Screenshot `leaf-scrolled-1366.jpg`.
- Edge 390 × 844: normaler Seitenfluss, Detailkarte mit `overflow-y: visible`; keine feste Desktophöhe. Temporäre Viewportvorgabe zurückgesetzt.
- 100 Frontend-Tests bestanden, einschließlich Scrollrücksetzung beim Knotenwechsel; Production Build und `git diff --check` erfolgreich.

Keine API-Änderungen und keine Testdaten verändert.
