# Rezepteditor: Prüfung und Veröffentlichung

Stand: 20.11.2026. Referenzen: `mockups/recipes/04_Pruefung_und_Veroeffentlichung.png` und `08_Pruefung_und_Veroeffentlichung_Ansicht.png`. Nach Freigabe des Nährwerttabs ist der Prüfungstab als weiteres UX-Inkrement umgesetzt und bereit zur visuellen Abnahme.

## Oberfläche und Ablauf

Links stehen der Prüfstatus und getrennte aufklappbare Bereiche für Fehler, Warnungen und Hinweise. Rechts stehen Veröffentlichung, bekannte Revisionsinformationen und der Unterschied zwischen Speichern und Veröffentlichen. Lange Meldungslisten besitzen einen eigenen Scrollbereich.

Die Aktion „Prüfen & veröffentlichen“ verwendet die bestehende Veröffentlichungs-API. Diese prüft den gespeicherten Entwurf und veröffentlicht ein gültiges Rezept direkt. Fehler verhindern die Veröffentlichung und werden rot dargestellt. Warnungen aus der API werden gelb angezeigt und verlangen eine ausdrückliche Checkbox-Bestätigung vor einem zweiten Veröffentlichungsaufruf. Eine Bestätigung wird beim Bearbeiten, Neuladen des Entwurfs und nach jedem Versuch zurückgesetzt.

Zusätzliche Hinweise aus bestehenden Daten, etwa fehlende Nährwertprofile und verfügbare Zutatenaktualisierungen, stehen ebenfalls im Warnungsbereich. Sie sind als „Zusätzlicher Hinweis, keine Freigabeprüfung“ gekennzeichnet und verlangen keine erfundene Veröffentlichungsbestätigung. Sie verlinken zum Nährwert- oder Zutatentab. Ein leerer Fehlerbereich bedeutet keine behauptete erfolgreiche Vorabprüfung.

Veröffentlichen ist während Bearbeitung, Speichern, laufender Veröffentlichung, bei ungespeicherten Änderungen, archivierten Rezepten und schreibgeschütztem Zugriff gesperrt. Der bestehende serverseitige Berechtigungscheck bleibt maßgeblich. Im Bearbeitungsmodus wird auf Speichern beziehungsweise Beenden der Bearbeitung verwiesen. Meldungen für den alten Stand werden bei geändertem Inhalt nicht als aktuelle Prüfung angezeigt.

Der Footer bietet im geöffneten Prüfungstab keinen erneuten Link auf denselben Tab an. Speichern und Abbrechen bleiben im Bearbeitungsmodus verfügbar.

## Navigation zu Fehlerursachen

Fehler mit `positionId` verlinken zur passenden Zutatenposition. Bei `replacementId` wird die zugehörige Zutatenposition ermittelt. Gruppenfehler führen zur vorhandenen Gruppe. Der Bereich wird geöffnet, markiert und fokussiert; der Bearbeitungsmodus wird nicht automatisch aktiviert. Metadatenfehler führen zu den Grunddaten. Fehlende oder nicht zuordenbare Kontexte führen zum passenden Tab statt zu einem erfundenen Eintrag. Unterrezeptpositionen und ihre Ersatzverweise werden inzwischen ebenfalls zur passenden Tabellenzeile aufgelöst.

Alle vorhandenen Prüfcodes besitzen verständliche Texte; Konfliktnamen aus der Antwort werden beibehalten. Unbekannte Codes werden nicht als technische Fehlermeldung ausgegeben.

## Sichtbar geplante Funktionen

- Die separate „Prüfung ausführen“ bleibt deaktiviert und mit „Geplant“ gekennzeichnet. Der vorhandene Veröffentlichungsaufruf wird nicht als folgenlose Vorabprüfung ausgegeben.
- Erfolgreiche einzelne Fachprüfungen werden nicht erfunden. Der Hinweisbereich reserviert diese Darstellung für eine spätere separate Prüfantwort.
- Die Revisionshistorie zeigt ausschließlich die tatsächlich bekannte aktuelle Revisionsnummer. Ein vollständiger Verlauf mit Autoren und Zeitpunkten bleibt geplant; der Verlaufsbutton ist deaktiviert.

Keine neuen APIs, Domainregeln oder Datenbankänderungen in diesem Inkrement.

## Verifikation

308 Frontend-Tests in 38 Dateien bestanden. Sieben neue Tests decken geplante Aktionen ohne Seiteneffekt, getrennte Serverfehler/-warnungen, ausdrückliche Warnungsbestätigung, Navigation von Ersatz- und Gruppenfehlern, unverbindliche Nährwerthinweise sowie die Veröffentlichungssperren ab. Frontend-Produktionsbuild bestanden.

Browser mit vorhandenem synthetischem Testrezept: Hinweise auf Nährwerte verlinken korrekt, Veröffentlichen ist im Bearbeitungsmodus gesperrt, geplante Aktionen sind deaktiviert. Es wurden keine Rezeptdaten gespeichert oder veröffentlicht. Die Serverantwortzustände für Fehler und Warnungen sind mit Testantworten im Komponententest abgedeckt.

- 1920×1080: Seitenhöhe 1080 px, Tabhöhe und Inhalt jeweils 698 px; die geprüfte normale Ansicht benötigt kein Scrollen.
- 1440×900: kein globaler Seitenüberlauf; vertikales Scrollen bleibt innerhalb des Tabs möglich.
- 390×844: Karten untereinander, keine horizontale Überbreite der Seite. Vertikales Scrollen ist vorgesehen.

## Geänderte Dateien

- `recipe-editor.component.html`: Prüfkarten, getrennte Meldungsbereiche, Bestätigung, Navigation, Revisionsbereich und Footer.
- `recipe-editor.component.ts`: Meldungsgruppierung/-texte, Kontextnavigation und abgesicherte Warnungsbestätigung.
- `recipe-editor.component.scss`: kompakte Karten, Meldungslisten und schmale Darstellung.
- `recipe-editor-publication.spec.ts`: sieben neue Regressionstests.

## Nachher-Bilder

- [Desktop 1920×1080](screenshots/recipe-editor/publication/recipe-publication-1920.jpg)
- [Desktop 1440×900](screenshots/recipe-editor/publication/recipe-publication-1440.jpg)
- [Schmale Ansicht](screenshots/recipe-editor/publication/recipe-publication-narrow.jpg)
- [Bearbeitungsmodus](screenshots/recipe-editor/publication/recipe-publication-editing.jpg)


## Browserprüfung mit ergänzten Testentwürfen

Die zusätzlichen [Nährwert-Testdaten](../testing/recipe-editor-nutrition-fixtures.md) decken die echten Serverantworten im Browser ab: Fehlerentwurf mit Mengenfehler, Fokusnavigation zur Milchposition und Warnungsentwurf mit verpflichtender Checkbox. Beide Entwürfe bleiben unveröffentlicht. Die abschließende Veröffentlichung wurde nicht ausgelöst.
