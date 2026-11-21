# Rezeptsektion: offener Funktionsumfang nach UX-Abnahme

Stand: 21.11.2026. Die aktuelle Gestaltung der Rezeptliste und der vier Rezeptbereiche ist vom Benutzer vorläufig abgenommen. Die folgenden Punkte bleiben ausdrücklich offen. Sichtbare Platzhalter sind mit „Geplant“ gekennzeichnet und lösen keine vermeintlich fertigen Aktionen aus. Die Mockups sind Gestaltungsreferenzen; neue Fachregeln müssen vor ihrer Umsetzung geklärt werden.

## Offene Punkte

| Bereich | Noch offen | Nötige Ergänzung / Abgrenzung |
| --- | --- | --- |
| Grunddaten | Separate Kurzbeschreibung | Eigenes Datenfeld und API-Vertrag. Beschreibung und Zubereitung sind derzeit ein gemeinsames Feld. |
| Grunddaten und Liste | Kategorien | Katalog, Zuordnung und Speicherung sowie anschließend Anzeige, Filter und Sortierung. Die Liste zeigt derzeit nur tatsächlich vorhandene Metadaten. |
| Bilder | Rezept- und Zutatenbilder | Bildverwaltung, Upload, Entfernung und Zuordnung. Die reservierten Flächen verwenden vorerst Symbole. |
| Grunddaten | Tags bearbeiten | Modell enthält Tags bereits; Eingabe und Bearbeitung fehlen. Gespeicherte Werte bleiben erhalten. |
| Grunddaten | Interne Notizen bearbeiten | Feld ist vorhanden, wird derzeit nur gelesen. Eingabe und Bearbeitung ergänzen. |
| Mengenberechnung | Personenstaffelung | Fachliche Definition, Modell und Berechnungsregeln für Faktoren ab einer Personenzahl. Nicht mit Lagerstufenfaktoren oder der bestehenden Mengenregel „In festen Schritten“ verwechseln. |
| Zutatenliste | Umordnen durch Ziehen | Zugängliche Interaktion für Zutaten, Unterrezepte und Gruppen; Umreihen per Menü ist bereits möglich. |
| Nährwerte | Vollständiger Berechnungsnachweis | Berechnungszeitpunkt und verwendete Revisionen einschließlich verschachtelter Unterrezepte im Lesevertrag. Direkt referenzierte Zutatenrevisionen werden bereits angezeigt. |
| Prüfung | Eigenständige Vorabprüfung | Separater Prüfaufruf ohne Veröffentlichung und ohne Seiteneffekt. „Prüfen & veröffentlichen“ ist derzeit eine echte Veröffentlichung. |
| Prüfung | Erfolgreiche Einzelprüfungen | Strukturierter Prüfbericht für tatsächliche Ergebnisse. Keine synthetischen Erfolgsmeldungen aus fehlenden Fehlern ableiten. |
| Versionen | Vollständige Revisionshistorie | Lesevertrag, Autor, Änderungszeitpunkt und Verlauf. Die aktuelle veröffentlichte Revision ist sichtbar; ein vollständiger Verlauf fehlt. |
| Rezeptliste | Details übernommener Organisations-/Zentralrezepte öffnen | Diese Einträge besitzen bislang keine Detail-Öffnen-Aktion. Eine revisionsgebundene schreibgeschützte Ansicht und gegebenenfalls ein ausdrücklicher lokaler Anpassungsablauf sind gesondert umzusetzen; keine Bearbeitungsrechte aus der neuen Tabellenzeile ableiten. |
| Rezeptliste | Zusätzliche fachliche Listenmetadaten | Kategorie, Ernährungszusammenfassung und Bilder fehlen im Listenvertrag. Bei Bedarf gesammelt liefern, statt pro Tabellenzeile den vollständigen Entwurf abzurufen. |

## Bereits umgesetzt

- Einheitliche Rezeptliste im Stil der Zutatenliste mit Suche, Bereichsauswahl, Filtern, Sortierung, Listen-/Kartenansicht, zehn Einträgen pro Seite und Zeilenklick für Lagerrezepte.
- Vier Tabs mit getrenntem Ansichts-/Bearbeitungsmodus, explizitem Speichern und Abbrechen sowie Schutz ungespeicherter Änderungen.
- Berechnungsbezug Portionen oder Menge mit Bezugseinheit; Lagerstufenfaktoren bei Portionsrezepten.
- Zutaten- und Unterrezeptgruppen, gemeinsame Reihenfolge, Mengenregeln linear/fix/in festen Schritten sowie optionale Minimal-/Maximalmengen für lineare Regeln. Modell, API, Berechnung und Migrationen sind umgesetzt.
- Ersatzzutaten und Ersatzrezepte mit Mengen und eindeutigen Anforderungszuordnungen; einheitliches Popup „Ernährung & Ersatz“ aus der Liste.
- Rekursive Variantenhinweise für gebundene Unterrezeptrevisionen. Gelb mit „(Variante)“ nur bei vollständiger Abdeckung des Konflikts; andernfalls rot. Konkrete Varianten werden im Popup genannt.
- Ausdrückliche Übernahme verfügbarer Zutatenrevisionen; bestehende Verwendungen bleiben revisionsgebunden.
- Nährwertsummen und vorläufige Teilsummen bei unvollständigen Daten. Ein Hinweis im Bereich Datenqualität erklärt fehlende Beiträge; fehlende Werte werden nicht als null angenommen.
- Tatsächliche Veröffentlichungsprüfung, getrennte Fehler/Warnungen, ausdrückliche Warnungsbestätigung und Navigation zu betroffenen Einträgen.
- Wiederholbar ergänzbare Testzutaten und Mehrzutatenrezepte für vollständige/unvollständige Nährwerte sowie Prüffehler und Warnungen.

## Prüfung des gesicherten Standes

373 Catering-Backend-Tests und 327 Frontend-Tests bestanden; API- und Frontend-Produktionsbuild erfolgreich. Die Rezeptliste passt bei 1920×1080 mit zehn Zeilen ohne Seitenscrollen. Schmale Listen-/Kartenansicht, Kontextdialoge, Suche, Pagination, Filter, Zeilenklick und rekursive Variantenhinweise wurden im Browser geprüft. Die Testprüfungen haben keine Rezeptdaten veröffentlicht oder geändert. Einzelheiten und Abnahmebilder stehen in den Bereichsdokumenten.

## Weiterführende Dokumentation

- [Rezeptliste](recipe-editor-ux-catalog.md)
- [Grunddaten und Editorrahmen](recipe-editor-ux-increment-1.md)
- [Zutaten und Mengenregeln](recipe-editor-ux-ingredients.md)
- [Unterrezepte, Ersatzrezepte und Varianten](recipe-editor-ux-subrecipes.md)
- [Nährwerte](recipe-editor-ux-nutrition.md)
- [Prüfung und Veröffentlichung](recipe-editor-ux-publication.md)
- [Nährwert-Testdaten](../testing/recipe-editor-nutrition-fixtures.md)
- [Mockups und ursprünglicher Auftrag](mockups/recipes/README.md)
