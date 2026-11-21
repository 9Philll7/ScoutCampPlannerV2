# Rezeptliste im Stil der Zutatenliste

## Umsetzung

Die Rezeptliste verwendet jetzt das Gestaltungsmuster der Zutatenliste: kompakte Kopfzeile mit Erstellaktion, Bereichsauswahl mit Zählern, Suche, aufklappbare Filter, Sortierung, Listen-/Kartenansicht und zehn Einträge pro Seite. Die Tabelle zeigt Name, Bereich, Status, veröffentlichte Revision, Änderungsdatum und ein Drei-Punkte-Menü. Lagerrezepte öffnen sich per Zeilenklick oder fokussierbarem Namensbutton. Bibliothekseinträge behalten die bisherigen Zugriffsregeln; ihr Entfernen bleibt über die vorhandene Aktion möglich.

Suche, Filter und Sortierung wirken auf alle sichtbaren Katalogeinträge, bevor die Seite gebildet wird. Beim Filtern wird die erste Seite gewählt. Verkürzt sich die Liste, wird die aktuelle Seite auf den verbleibenden Bereich begrenzt. Bestehende Einschränkungen auf im Lager verwendete Revisionen bleiben im Rezepteditor wirksam.

Die vorhandene API liefert die erforderlichen Listenfelder. Keine zusätzlichen Detailabfragen, keine API- oder Modelländerungen. Kategorie, Rezeptbilder und Ernährungszusammenfassungen sind im Listenvertrag noch nicht enthalten und werden nicht erfunden. Die Bildfläche zeigt das vorhandene Rezeptsymbol. Übernommene Organisations-/Zentralrezepte hatten bislang keine Detail-Öffnen-Aktion; diese funktionale Lücke bleibt bestehen.

## Prüfung

327 Frontend-Tests in 40 Dateien erfolgreich. Vier neue Katalogtests prüfen Seitenwechsel, Suche, kombinierte Filter, Sortierung, Zeilenklick, Zugriffsbeschränkung und Seitenbegrenzung nach dem Entfernen eines Eintrags.

Browserprüfung mit 22 Testrezepten: 1920×1080, zehn Tabellenzeilen und Pagination ohne Scrollen der Seite (Dokumenthöhe 1080 px). Seite zwei, Suche nach einem Rezept auf der letzten Seite, Statusfilter mit zwei Entwürfen und Öffnen per Zeile geprüft. Bei 390×844 bleibt die Tabelle innerhalb ihres eigenen Bereichs horizontal scrollbar, ohne die Seite zu verbreitern; Kartenansicht ebenfalls geprüft. Keine Daten geändert.

- [Desktopliste](screenshots/recipe-editor/catalog/recipe-catalog-1920.jpg)
- [Schmale Kartenansicht](screenshots/recipe-editor/catalog/recipe-catalog-mobile.jpg)
