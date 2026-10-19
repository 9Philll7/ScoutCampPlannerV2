# Listen nutzen die verfügbare Höhe

Stand: 2026-10-19. Ersetzt die frühere feste Begrenzung auf fünf Zeilen.

Teilnehmer- und Untergruppenlisten berechnen die Seitengröße aus Tabellenposition, verfügbarer Karten-/Fensterhöhe und gemessener Zeilenhöhe. Platz für Seitennavigation bleibt reserviert. Änderungen an Fenstergröße oder Tabelleninhalt lösen eine Neuberechnung aus; bei geänderter Kapazität beginnt die Liste wieder auf Seite 1. Maximal zwanzig Zeilen, bei schmalen bzw. sehr niedrigen Fenstern weiterhin fünf als Rückfall. Kein manueller Größenwähler.

Edge 1920 × 1080: Teilnehmerübersicht mit elf Zeilen, Dokumenthöhe exakt 1080px, Seitennavigation vollständig sichtbar. Screenshot `participants-1920.jpg`. Eingebettete Gruppe mit neun Zeilen, Detailkarte 1014px Inhalt bei 1014px Höhe, kein Überlauf. Untergruppentab nutzt dieselbe Messung, die vorhandenen Testdaten haben jedoch nur fünf Untergruppen. Keine Testdaten verändert, Viewport zurückgesetzt.

107 Tests und Production Build erfolgreich; Regressionen prüfen variable Seitenkapazität und passende Seitenangaben. Ein leerer Kartenfuß in der Teilnehmerübersicht wurde entfernt. Bei besonders langen Inhalten bleibt der bestehende Scrollrückfall erhalten.
