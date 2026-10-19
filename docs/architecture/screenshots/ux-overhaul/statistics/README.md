# Teilnehmerstatistik – Diagramme

Stand: 2026-10-18. Referenz: `mockups/ux-2/structure_parent.png`.

- Horizontale, nach Stufen gestapelte Balken für KiJu und Leiter/Erwachsene; zusätzliche Zeile bei offener Kategorie.
- Erfasste und geschätzte Teilnehmer in getrennten Karten mit gemeinsamer Personenskala. Stufenfarbe wird aus der stabilen ID bestimmt; offene Stufe grau. Legende, Summen, Segment-Tooltips, zugängliche Zahlenbeschreibungen und aufklappbare Tabellen bleiben verfügbar.
- Breite Ansicht: zwei Karten nebeneinander; unter 1500px untereinander. Keine Planungsprozente oder Fortschrittsbehauptungen.
- Schätzwerte für Teilbäume werden nun direkt beim Kontextwechsel geladen, damit das Diagramm sofort sichtbar ist. Weiterhin bestehende Einzelabrufe je Knoten; bei großen Hierarchien bleibt eine serverseitige Aggregation sinnvoll. Keine API geändert.

## Prüfung

98 Angular-Tests in 18 Dateien bestanden; Production Build und `git diff --check` erfolgreich. Sandbox blockierte Worker-/Buildstarts; abschließende Läufe außerhalb der Sandbox erfolgreich.

Edge mit bestehendem Dummy-Lager geprüft: 32 erfasste Teilnehmer (28 KiJu, 4 Erwachsene), 64 geschätzt (50/14). Beide Diagramme verwenden die Skala bis 50 Personen. Screenshot: [stage-charts.jpg](stage-charts.jpg), tatsächlicher Browser-Viewport ca. 935 × 932, Karten untereinander. Keine Testdaten verändert. Die breite Zweispaltenansicht wurde in diesem Durchlauf nicht separat im Browser abgenommen.

Offen bleibt die frühere visuelle Integration erfasster Zahlen in Baum und Untergruppenliste; dieses Inkrement gestaltet die Stufenverteilung im Strukturdetail.
