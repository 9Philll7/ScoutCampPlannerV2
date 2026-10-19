# Direkte Strukturaktionen

Stand: 2026-10-18.

Der allgemeine Bearbeitungsmodus wurde entfernt. Berechtigte Benutzer sehen die Drei-Punkte-Menüs an Lagerwurzel und Strukturknoten dauerhaft. Bei eingefrorenen Lagern oder fehlendem Bearbeitungsrecht bleiben die Aktionen ausgeblendet.

Der Detailkopf enthält einen kleinen Namensbutton. Er öffnet den bestehenden Namenseditor mit Speichern und Abbrechen; das Menü bietet dieselbe Aktion. Anlegen und Verschieben haben ebenfalls einen eigenen Abbruch. Während einer Strukturaktion weicht die Teilnehmerliste bzw. der Tabinhalt dem jeweiligen Formular. Kennzahlen bleiben sichtbar. Jede bestätigte Mutation wird weiterhin einzeln gespeichert. Wechsel zwischen Aktionen und zur Schätzung schützt geänderte Entwürfe über die bestehende Verwerfprüfung.

## Abnahme

- 103 Tests bestanden, Production Build und `git diff --check` erfolgreich. Regressionen an den direkten Workflow angepasst: Menü ohne Modus, Namensbutton, Leserechte und Ablehnen eines Aktionswechsels mit geändertem Entwurf.
- Edge bei 1920 × 1080: dauerhafte Menüs und Namensbutton sichtbar, Namenseditor direkt geöffnet und unverändert abgebrochen. Screenshot `rename.jpg`. Keine Testdaten verändert.
- Rücksetzen der temporären Viewportvorgabe schlug nach einem Browser-Timeout auch beim Wiederholungsversuch fehl; keine weitere Browseraktion ausgeführt.

## Ergänzungen seit den früheren Screenshots

Die vorherigen Screenshotordner dokumentieren Zwischenstände. Inzwischen: feste fünf Zeilen pro Listenpage ohne Größenauswahl, redundante Untertitel und Warn-/Zahlenhinweise entfernt, Schätzungsbutton im Detailkopf. Stufenverteilung wechselt zwischen Diagramm (Standard) und Tabelle; beide Darstellungen zeigen nur im ausgewählten Bereich erfasste oder geschätzte Stufen. Nicht zugeordnete Stufe und offene Kategorie nur bei entsprechenden Teilnehmern. Die erfasste Tabelle enthält Zeilen- und Gesamtsummen einschließlich offener Kategorien. Keine API-Änderungen in diesen visuellen Schritten.
