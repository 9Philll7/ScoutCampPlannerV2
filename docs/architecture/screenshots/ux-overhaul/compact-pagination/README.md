# Kompakte Felder und Listenseiten

Stand: 2026-10-18. Orientierung an `structure_parent.png`, `structure_leaf.png` und den Editorreferenzen.

Kennzahlenkarten, Abschnittsabstände, Tabellenzellen und Teilnehmereditor sind verdichtet. Normale Eingabefelder sind am Desktop mindestens 36px hoch, Struktur-Editorfelder 40px. Für grobe Zeiger bleiben größere Bedienflächen erhalten. Beschriftungen und Zuordnungshinweise bleiben sichtbar.

Teilnehmer- und Untergruppenlisten verwenden eine gemeinsame Seitennavigation: standardmäßig fünf Zeilen, wahlweise zehn oder zwanzig. Bereich, Trefferzahl und aktuelle Seite werden angezeigt; Randseiten deaktivieren die entsprechende Navigation. Suche/Sortierung erfolgen vor der Seitenteilung, Filter- und Knotenwechsel beginnen auf Seite 1. Verkürzte Ergebnisse begrenzen die aktuelle Seite auf einen gültigen Wert. Vorschau wird beim Seitenwechsel geschlossen. Statistiken und Teilbaumaggregation bleiben unabhängig von der sichtbaren Seite vollständig.

Seitenteilung erfolgt im Frontend auf den bestehenden Daten; keine API-Änderung und keine Verringerung der geladenen Datensätze.

## Prüfung

- 103 Tests in 20 Dateien bestanden. Neue Regressionen für Seitengrenzen, leere/verkleinerte Listen, Seitengröße, gefilterte Teilnehmer und vollständige Untergruppen trotz Seitenteilung.
- Production Build und `git diff --check` erfolgreich.
- Edge 1920 × 1080: Teilnehmerseite 1/2 mit 1–5, Seite 2/2 mit 6–10 von 10; Wechsel auf zehn Zeilen ergibt Seite 1/1 mit 1–10. Untergruppen mit 1–5 von 5; ein tatsächlicher Mehrseitenwechsel bei Untergruppen ist mit zwölf synthetischen Gruppen im Komponententest geprüft.
- Teilnehmereditor geöffnet, kompakte Felder visuell geprüft und unverändert abgebrochen. Keine Testdaten verändert. Temporären Viewport zurückgesetzt.
- Screenshots: `participants-page-2.jpg`, `editor.jpg`, `subgroups.jpg`.
