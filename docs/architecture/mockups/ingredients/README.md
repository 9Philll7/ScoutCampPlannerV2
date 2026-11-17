# Zutateneditor

Die sieben vom Nutzer gelieferten PNGs bilden die Referenz für den Zutateneditor.

| Bild | Bereich |
| --- | --- |
| 01_Grunddaten.png | Kopf, Grunddaten und Kurzüberblick |
| 02_Einheiten.png | Basiseinheit und Umrechnungen |
| 03_Allergene_und_Unvertraeglichkeiten.png | Qualitative und quantitative Stoffangaben |
| 04_Herkunft.png | Herkunftsmerkmale |
| 05_Naehrwerte.png | Bezugsmenge und Nährwerte |
| 06_Varianten.png | Varianten und Vererbung |
| 07_Quellen_und_Pruefung.png | Quellen, Referenzvorschläge und Veröffentlichung |

## Abgleich mit vorhandenen Datenverträgen

- Zutatenbild, Beschreibung, freie Hinweise je Herkunftsmerkmal und beschreibende Umrechnungsnotizen fehlen im aktuellen Vertrag. Der Kopf verwendet einen Bildplatzhalter. Ergänzungen dieser Felder erfordern einen gesonderten API-Auftrag.
- Der Geltungsbereich folgt der geöffneten zentralen, Organisations- oder Lagerverwaltung. Ein Wechsel per Dropdown benötigt definierte Kopier- oder Verschiebesemantik.
- Die API liefert Revisionskennungen und einen technischen Zeilenversion-Zähler. Semantische Versionsnummern, Autoren, Zeitpunkte und Änderungskommentare aus dem Mockup fehlen. Angezeigt werden die verfügbaren Revisionen mit ihrem Status.
- Herkunft folgt dem bestehenden Modell mit einer Hauptherkunft und zusätzlichen tierischen Merkmalen.
- Die vorhandene Allergen-Codezuordnung wird verwendet. Die Beispielzuordnung im Mockup weicht davon ab.
- Die Zustände enthalten auch „Kann enthalten“. Die Umrechnungsgenauigkeit kennt Exakt, Durchschnitt und Geschätzt.
- Quellen werden gemeinsam als Freitexteinträge gespeichert. Referenzvorschläge verwenden die vorhandene BLS-4.0-Suche und deren bestehende Übernahmefunktionen.
- Nährwerte und Stoffgehalte verwenden die vorhandenen Bezugs- und Prüfregeln. Die Variantenübersicht zeigt Abweichungen und vererbte Bereiche.

## Umsetzung

Der Editor bietet sieben per Tastatur bedienbare Reiter, einen gemeinsamen Kopf, Zusammenfassungskarten und eine am unteren Rand haftende Aktionsleiste. Einheiten und Allergene verwenden Seiten mit fünf Zeilen. Quantitative Stoffe verwenden vier Zeilen je Seite, Quellen drei. Die Vorlagen für Lagerkopien stehen in der Zutatenübersicht. Varianten werden einzeln ausgewählt, ihre Details in einem begrenzten Dialog bearbeitet. Eingaben bleiben beim Reiterwechsel erhalten.

Die bestehenden Endpunkte für Entwürfe, Veröffentlichung, Lagerkopien und zentrale Einreichungen werden weiterverwendet. API und Datenbankschema wurden für diese UX-Überarbeitung nicht erweitert.

## Verifikation

280 Frontendtests und der Produktionsbuild sind erfolgreich. Zusätzliche Regressionstests prüfen den Erhalt von Eingaben beim Reiterwechsel, Tastaturbedienung, neue und entfernte Varianten sowie Quellenänderungen ohne Nährwertprofil.

Die Browserprüfung verwendete das vorhandene lokale Testlager bei 1920 × 1080, 1440 × 900 und 390 × 844. Geprüft wurden alle sieben Reiter, vererbte und abweichende Varianten, paginierte Allergen- und Stofflisten sowie ein zurückgesetzter Namensentwurf. Die schmale Ansicht ordnet die Karten untereinander und lässt die Reiter horizontal scrollen. Veröffentlichen und Speichern wurden dabei nicht im Testbestand ausgeführt.

Die verdichtete Desktopfassung wurde erneut bei 1920 × 1080 geprüft. Alle sieben Reiter bleiben innerhalb der Seitenhöhe. Zusätzliche Inhalte scrollen innerhalb der Karten oder Dialoge. Die Quellenansicht ordnet Quellen, Prüfung und Veröffentlichung nebeneinander an. Die BLS-Suche mit Referenzvergleich wird aus Nährwerten und Unverträglichkeiten geöffnet. Der Vergleich normiert vorhandene Nährwerte auf die Bezugsmenge des Vorschlags. Eine echte lokale Suche nach Butter lieferte BLS-Treffer. Die Übernahme und Veröffentlichung wurden im Browser dabei nicht ausgeführt.

Die verfügbare Desktop-Höhe umfasst den gesamten Editor einschließlich Meldungen. Kopf, Reiter und Aktionsleiste sind verdichtet. Die Nährwert-Zusammenfassung verwendet zwei Spalten, damit auch die Quellenkarte bei geringerer Desktop-Höhe Platz erhält.

## Zutatenliste

`08_Zutatenliste.png` ergänzt die Referenzen um die Katalogansicht. Die Liste zeigt Basiszutaten und Varianten mit den wirksamen Eigenschaften einschließlich Vererbung. Suchfeld, Typauswahl, Kategorie-, Status- und Allergenfilter, Sortierung, Listen-/Kartenansicht und zehn Einträge je Seite sind implementiert. Eine Variantenzeile öffnet die Basisrevision direkt im Variantenreiter.

Die vorhandene Revisionsliste liefert nur Name und Status. Detailangaben werden mit maximal vier gleichzeitigen bestehenden Detailabfragen geladen. Einzelne Fehler lassen die Basiszeile sichtbar und kennzeichnen die fehlenden Daten. Für große Kataloge wäre ein eigener Listendatenvertrag mit wirksamen Eigenschaften erforderlich. Der dargestellte Katalog folgt dem geöffneten Geltungsbereich. Weitere verfügbare Vorlagen sind in der Lageransicht separat erreichbar.

Bilder und fachliche Versionsnummern stehen im Vertrag nicht zur Verfügung. Die Liste verwendet einen Bildplatzhalter sowie den vorhandenen Entwurfs-/Veröffentlichungsstatus. Mehrfachauswahl ohne vorhandene Sammelaktion und zusätzliche funktionslose Menüs werden aus dem Mockup nicht übernommen.

Die Zutatenliste wurde im Browser bei 1920 × 1080 und 390 × 844 verglichen. Variantenfilter, Allergenfilter, Suche, Sortierung, Kartenansicht und der direkte Einstieg in eine Variante wurden geprüft. Der Rückweg erhält den Such- und Filterkontext. In der schmalen Ansicht scrollt die Tabelle horizontal innerhalb ihres Rahmens. Drei Katalogtests prüfen wirksame Varianteneigenschaften, zehn Zeilen je Seite und die Wiederholung fehlgeschlagener Detailabrufe.


### Gemeinsamer Zutatenkatalog

Lagerzutaten und verfügbare zentrale bzw. Organisationszutaten stehen jetzt in derselben Liste. Der Geltungsbereich wird je Zeile angezeigt und kann gefiltert werden. Lokale Revisionen haben Vorrang vor dem veröffentlichten Katalogeintrag derselben Zutat. Bestandszutaten ohne Revision bleiben sichtbar. Externe Einträge erhalten eine schreibgeschützte Vorschau mit den vom Katalog gelieferten Angaben; fehlende Variantendetails werden nicht aus der Basiszutat abgeleitet.

Die separate Sektion „Zutat als Vorlage verwenden“ entfällt. „Für Lager anpassen“ verwendet unverändert den bestehenden Fork-Preview-/Fork-Aufruf und ist nur für zentrale veröffentlichte Revisionen mit Lagerbearbeitungsrecht verfügbar. Organisationszutaten können mit der vorhandenen API nicht als Lagerkopie erstellt werden. Hierfür wäre eine gesonderte API-Erweiterung erforderlich.
