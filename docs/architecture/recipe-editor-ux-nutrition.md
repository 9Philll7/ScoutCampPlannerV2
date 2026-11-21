# Rezepteditor: Nährwerttab

Stand: 20.11.2026. Referenzen: `mockups/recipes/03_Naehrwerte.png` und `07_Naehrwerte_Ansicht.png`. Grunddaten und Zutaten wurden vom Benutzer freigegeben; dieses Inkrement wartet auf visuelle Abnahme.

## Umsetzung

- Zweispaltiges Layout: links Nährwertübersicht, rechts Datenqualität und Berechnungsdetails.
- Zwei hervorgehobene Energiekarten für Standardportion und gesamtes Rezept. Die Vergleichstabelle zeigt beide Spalten gleichzeitig; Gesamtwerte sind nicht mehr eingeklappt. Energie in kcal/kJ, Fett, gesättigte Fettsäuren, Kohlenhydrate, Zucker, Eiweiß, Salz und vorhandene Ballaststoffe.
- Werte bleiben in Ansicht und Bearbeitung schreibgeschützt. Neu berechnen nutzt den vorhandenen Aufruf für den gespeicherten Rezeptstand.
- Fehlende Profile werden mit Ursache und, sofern verfügbar, der exakt referenzierten Revisionsnummer aufgeführt. Mehrfache Verwendungen derselben Zutatenrevision mit derselben Ursache werden zusammengefasst. Lange Listen scrollen innerhalb ihrer Karte.
- Standardportionen berücksichtigen den vorhandenen Faktor der Erfassungsstufe wie die Backendberechnung. Direkt referenzierte Zutatenrevisionen sind aufklappbar einsehbar; keine automatische Aktualisierung.
- Ungespeicherte Änderungen blenden bisherige Zahlen aus und sperren die Neuberechnung. Berechnungsdetails beziehen sich ausdrücklich auf den gespeicherten Stand. Speichern oder Abbrechen lädt den passenden Stand neu.
- Verspätete Berechnungsantworten für ein zuvor geöffnetes Rezept überschreiben die aktuelle Ansicht nicht mehr.
- Eigenes Nährwertsymbol im Tab. Mobile Kopfangaben wurden entzerrt, damit Rezeptart, Arbeitsstand und Bearbeitungsbutton nicht überlappen.

## Bestehende Grenzen und vorgesehene Funktionen

Die Zwischenstands-Erweiterung nutzt den bestehenden Antwortvertrag und Rechner. Es gibt keine neuen Endpunkte oder Datenbankmigrationen.

Bei unvollständigen Zutatenprofilen liefert die API nun einen Zwischenstand aus allen auswertbaren, geprüften Beiträgen. `isComplete` bleibt false, und `missingContributions` enthält weiterhin alle fehlenden oder ungeprüften Beiträge samt Ursache. `total` und `perStandardPortion` sind dann vorläufige Teilsummen, keine vollständigen Rezeptwerte. Die Standardportionen bleiben die Bezugsgröße des gesamten Rezepts. Wenn noch kein Beitrag auswertbar ist, bleiben beide Summen null und die Anzeige zeigt „—“ statt einer erfundenen Nullsumme. Unbekannte optionale Ballaststoffe in auswertbaren Profilen bleiben unbekannt.

Die Warnung zur Unvollständigkeit erscheint ausschließlich im Bereich Datenqualität und erläutert die ausgelassenen Beiträge. Überschrift, Energiekarten und Tabelle verwenden dieselbe Darstellung wie bei vollständigen Werten. Ungespeicherte Änderungen blenden auch vorläufige Werte aus.

Berechnungszeitpunkt und vollständiger Revisionsnachweis einschließlich Unterrezepten sind sichtbar als „Geplant“ reserviert. Der Vertrag liefert diese Angaben noch nicht. Es werden keine Zeitpunkte, Bilder, Autoren oder Revisionsbereiche erfunden.

Die erste Browserabnahme nutzte vorhandene Testrezepte ohne Nährwertprofile. Inzwischen stehen zusätzliche [Nährwert-Testdaten](../testing/recipe-editor-nutrition-fixtures.md) mit mehreren Zutaten bereit. Vollständige Werte (308 kcal pro Standardportion), fehlende/ungeprüfte Profile und unbekannte Ballaststoffe sind damit auch im Browser geprüft. Die ursprünglichen Testfälle bleiben erhalten.

## Verifikation

- 301 Frontend-Tests in 37 Dateien bestanden, darunter sechs neue Nährwerttests: vollständige Darstellung, fehlende Profile und Mehrfachverwendung, ungespeicherte Änderungen/Abbrechen, normalisierte Standardportionen, verspätete Antworten und API-Fehler.
- Frontend-Produktionsbuild bestanden.
- Browser 1920×1080: Seitenhöhe 1080 px, Tabhöhe und Inhalt jeweils 685 px; kein Seiten- oder Tabscrollen im geprüften Rezept.
- 1440×900: kein Seitenüberlauf; vertikales Scrollen bleibt innerhalb des Tabs möglich.
- 390×844: Karten untereinander, Vergleichstabelle innerhalb ihrer Karte horizontal scrollbar, kein globaler horizontaler Überlauf. Vertikales Scrollen ist auf schmalen Ansichten vorgesehen.
- Browserprüfung: Ansicht/Bearbeitung, ungespeicherte Portionsänderung, gesperrte Neuberechnung, globale Verwerfung; gespeicherter Bezugsstand bleibt korrekt.

## Geänderte Dateien

- `recipe-editor.component.html`: Energiekarten, Tabelle, rechte Spalte, Lade-/Fehler-/Änderungszustände.
- `recipe-editor.component.scss`: kompakte Nährwertdarstellung und responsive Anordnung; bisherige Kachelstile entfernt.
- `recipe-editor.component.ts`: Anzeigehilfen, normalisierte Bezugsportionen, Gruppierung fehlender Profile und Schutz vor verspäteten Ergebnissen.
- `recipe-editor-nutrition.spec.ts`: sechs Regressionstests.
- `shared/action-icon.component.ts`: Nährwertsymbol.

## Nachher-Bilder

- [Desktop 1920×1080](screenshots/recipe-editor/nutrition/recipe-nutrition-1920.jpg)
- [Desktop 1440×900](screenshots/recipe-editor/nutrition/recipe-nutrition-1440.jpg)
- [Schmale Ansicht](screenshots/recipe-editor/nutrition/recipe-nutrition-narrow.jpg)
- [Ungespeicherte Änderungen](screenshots/recipe-editor/nutrition/recipe-nutrition-unsaved.jpg)

Nach Abnahme folgt der Tab Prüfung & Veröffentlichung als eigenes Inkrement.


[Vollständige Browserauswertung mit Testprofilen](screenshots/recipe-editor/nutrition-fixtures/recipe-nutrition-fixture-complete.jpg).


## Verifikation des Zwischenstands

368 Catering-Backend-Tests und 309 Frontend-Tests bestanden. Neue Regressionen prüfen die Summe bekannter Profile unter Ausschluss fehlender und ungeprüfter Profile, die unveränderte Bezugsportionenzahl und die deutliche Kennzeichnung der Teilwerte. Bestehende Tests für keinerlei auswertbare Profile, unbekannte Ballaststoffe und ungespeicherte Änderungen bestehen weiter. API- und Frontend-Produktionsbuild bestanden.

Im Browser zeigt „TEST Nährwerte 02 Haferbrei unvollständig“ 3.080 kcal als vorläufige Rezeptsumme und 308 kcal pro Standardportion. Gewürz ohne Profil und ungeprüfter Joghurt bleiben mit ihren Ursachen sichtbar; ihre Nährwerte sind nicht eingerechnet. 1920×1080: Seitenhöhe 1080 px, Tabhöhe und Inhalt jeweils 657 px, kein Scrollen. 390×844: Kennzeichnung vorhanden und kein globaler horizontaler Überlauf. Keine Rezeptdaten geändert; lokale API mit neuer Berechnung neu gestartet.

- [Zwischenstand auf Desktop](screenshots/recipe-editor/nutrition-fixtures/recipe-nutrition-partial-1920.jpg)
- [Zwischenstand auf schmalem Bildschirm](screenshots/recipe-editor/nutrition-fixtures/recipe-nutrition-partial-narrow.jpg)
