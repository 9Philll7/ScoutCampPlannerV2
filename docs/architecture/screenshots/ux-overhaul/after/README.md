# UX Overhaul 1 – gerenderte Prüfung in Edge

Datum: 2026-10-16. Die lokale Anwendung auf `http://localhost:4200` wurde mit dem verbundenen Edge-Browser gegen die Referenzmockups geprüft. Die früheren NOT_RUN-Angaben im Ergebnisbericht beschreiben den vorherigen Stand; diese Prüfung ergänzt sie. **Visuelle Abnahme: nicht bestanden.**

Es wurden ausschließlich Ansichten geöffnet, Knoten ausgewählt, der Bearbeitungsmodus umgeschaltet und der Schätzdialog geöffnet/abgebrochen. Keine Daten gespeichert oder gelöscht, keine Rechte geändert. Keine Änderungen am Anwendungscode in dieser Prüfung. Vorhandene Dummy-Daten verwendet; alle sichtbaren Schätzwerte sind null. Zustände mit gefüllten Schätzungen wurden nicht geprüft.

## Aufnahmen

Dateinamen bezeichnen die eingestellte CSS-Viewportgröße, nicht die Fenstergröße einschließlich Browserleisten. Browser-Screenshots liegen als JPEG vor; bei sichtbarer Scrollleiste kann die aufgenommene Inhaltsbreite geringer ausfallen. Keine nachträgliche Skalierung oder Bildbearbeitung.

| Ansicht | Datei |
|---|---|
| Organisationsstart | [organization-start-1920x1080.jpg](organization-start-1920x1080.jpg) |
| Lagerübersicht | [camp-overview-1920x1080.jpg](camp-overview-1920x1080.jpg) |
| Strukturwurzel | [structure-root-1920x1080.jpg](structure-root-1920x1080.jpg) |
| Oberknoten | [structure-parent-1920x1080.jpg](structure-parent-1920x1080.jpg) |
| Blattknoten | [structure-leaf-1920x1080.jpg](structure-leaf-1920x1080.jpg) |
| Bearbeitungsmodus | [structure-edit-1920x1080.jpg](structure-edit-1920x1080.jpg) |
| Schätzdialog | [structure-estimate-dialog-1920x1080.jpg](structure-estimate-dialog-1920x1080.jpg) |
| Bestehende Teilnehmerverwaltung | [participants-1920x1080.jpg](participants-1920x1080.jpg) |
| Blatt bei 1440px | [structure-leaf-1440x900.jpg](structure-leaf-1440x900.jpg) |
| Blatt bei 1024px | [structure-leaf-1024x768.jpg](structure-leaf-1024x768.jpg) |
| Blatt mobil, Viewport | [structure-leaf-390x844.jpg](structure-leaf-390x844.jpg) |
| Blatt mobil, vollständige Seite | [structure-leaf-390x844-full.jpg](structure-leaf-390x844-full.jpg) |
| Mobile Navigation geöffnet | [navigation-open-390x844.jpg](navigation-open-390x844.jpg) |
| Mobiler Schätzdialog | [structure-estimate-dialog-390x844.jpg](structure-estimate-dialog-390x844.jpg) |

## Befunde und Priorität

### P1: Navigation auf dunklem Hintergrund schlecht lesbar

Inaktive Sidebar-Einträge, Rückweg und mobiler Menübutton sind dunkel auf dunkel. Das ist ein Fehler, kein berechtigungsbedingter Disabled-Zustand: Der untersuchte Verpflegungsbutton ist `disabled: false`, gerenderte Textfarbe `rgb(49, 91, 69)` auf Sidebarhintergrund `rgb(18, 57, 47)`. Die Material-Buttonfarbe setzt sich gegenüber der vorgesehenen hellen Navigationstextfarbe durch. Anders als im Mockup ist die Navigation dadurch nur bei aktiver Position gut lesbar.

Korrektur: Material-Textbutton-Tokens für Sidebar und mobile Utility-Leiste explizit setzen; auch Rückweg, Hover, Fokus und Disabled getrennt überprüfen.

### P1: Großer Leerraum im mobilen Seitenkopf

Bei 390px liegt der lokale Ansichtswechsel weit unter dem Seitentitel. Ursache im CSS: `.page-header` wechselt auf Spaltenrichtung, während `.structure-page .page-header > div` weiterhin `flex: 1 1 420px` verwendet. Die für Desktop gedachte Basisbreite wirkt dann als Basishöhe.

Korrektur: Unter dem Spalten-Breakpoint `flex-basis: auto` bzw. `flex: initial` setzen und Kopfabstände anschließend gerendert prüfen.

### P2: Baum bei 1024px zu schmal für direkt sichtbare Zahlen

Die 38/62-Aufteilung bleibt bestehen, die Baumzeilen benötigen lokalen horizontalen Scroll. Der Screenshot zeigt abgeschnittene Zahlen auf der rechten Seite des Baumfensters. App-weiter horizontaler Überlauf wurde nicht festgestellt: DOM-Messung 1009px Scrollbreite und 1009px Clientbreite. Bei 390px jeweils 390px.

Korrektur: Mindestbreite des Baumarbeitsbereichs oder früheren Wechsel auf einspaltige Darstellung prüfen. Lokales Scrollen ist zulässig, die häufig benötigten Zahlen sollten bei Tabletbreite möglichst direkt lesbar sein.

### P2: Gesamteindruck noch deutlich schlichter als die Mockups

Die dunkle Navigation, gemeinsame Inhaltskante, Auswahlflächen und Detailgliederung sind erkennbar näher an den Strukturreferenzen. Marke und Navigationstypografie bleiben jedoch kleiner und zurückhaltender; die Informationskomposition bietet weniger visuelle Hierarchie. Start- und Lagerübersicht sind weiterhin sehr sparsam. Fehlende Daten rechtfertigen den Verzicht auf erfundene Dashboardinhalte, verhindern aber keine stärkere Gestaltung realer Lagerinformationen.

Die Teilnehmerverwaltung bleibt das bisherige Kartenraster. Tabelle, Vorschau und Teilnehmerdetail aus UX 2.2 wurden in diesem Inkrement nicht umgesetzt und sind keine abgeschlossene Mockupangleichung.

## Bestätigte Verbesserungen und Stichproben

- PASS: Lokaler Struktur-/Teilnehmerwechsel im gemeinsamen Kopf; im Desktop-Bearbeitungsmodus keine frühere Überlappung mit der Moduskennzeichnung.
- PASS: Ober-/Blattauswahl aktualisiert Kopf und passende Inhalte. Oberknoten zeigt Untergruppen, Blatt den vorhandenen Teilnehmereinstieg und Schätzaktion.
- PASS: Bearbeitungsmodus lässt sich öffnen und beenden, Hinweis zur Einzelpersistenz sichtbar.
- PASS: Schätzdialog öffnet auf Desktop und mobil. Abbrechen auf Desktop und Escape mobil schließen ihn ohne Speichern. Mobil ist die Matrix lokal horizontal scrollbar, die Dialogaktionen bleiben sichtbar.
- PASS: Mobiles Menü öffnet, Escape schließt und gibt Fokus an den Menübutton zurück. Der Menübutton ist dennoch wegen des Kontrastfehlers visuell mangelhaft.
- PASS: Kein App-weiter horizontaler Überlauf in den gemessenen Strukturansichten bei 1024/390px.
- BEOBACHTET: Der bestehende konservative Dirty-Guard zeigte beim Zurückwechseln von Teilnehmer zu Struktur eine Navigationswarnung. Kein Nachweis tatsächlicher ungespeicherter Daten; Verhalten nicht verändert.
- NOT_RUN: Vollständige Rechte-/Offline-Matrix, echte Persistenztests, gefüllte Schätzwerte, vollständiger Keyboard-/Screenreader-Audit und vollständige WCAG-Kontrastprüfung.

Als nächster Korrekturschritt sind zunächst die beiden P1-Probleme zu beheben und dieselben Desktop-/Mobilaufnahmen erneut zu erstellen. Die vorliegenden Bilder dokumentieren den aktuellen fehlerhaften Stand und sind keine Freigabe.
