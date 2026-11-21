# Rezepteditor: Zutaten-Tab

Stand: 19.11.2026. Grunddaten wurden vom Benutzer abgenommen; der Zutaten-Tab ist als nächster Schritt umgesetzt und wartet auf visuelle Abnahme. Referenzen: `mockups/recipes/02_Zutaten.png` und `06_Zutaten_Ansicht.png`. Bestehende Fachlogik und explizite Speicherung bleiben maßgeblich. Fehlende Funktionen sind nach ergänzender Benutzervorgabe als „Geplant“ sichtbar.

## Umgesetzte Oberfläche

- Kompakte, aufklappbare Gruppentabellen mit Zutatenzahl. Ein leerer Bereich „Ohne Gruppe“ entfällt, solange benannte Gruppen existieren und dort keine Positionen oder Unterrezepte liegen.
- Gemeinsame Spalten für Ansicht und Bearbeitung: Zutat, Menge mit Bezugsportionen, Mengenregel, Herkunft, Zutatenrevision und Ernährung/Ersatz.
- Ansichtsmodus: Mengen, Einheiten und Regeln als Text; keine deaktivierten Tabellenfelder. Ernährungsdetails und Ersatzzuordnungen sind schreibgeschützt einsehbar.
- Bearbeitung: kompakte Mengen-/Einheitenfelder direkt in der Zeile. Drei-Punkte-Menüs für Gruppen und Positionen. Vorhandene Umreihfunktionen bleiben über Nach oben/Nach unten erreichbar.
- Gruppennamen und neue Gruppen werden im Kontextdialog bearbeitet. Neue Gruppe entsteht erst nach „Übernehmen“. Löschen bleibt auf leere Gruppen beschränkt, einschließlich der bestehenden Unterrezeptprüfung.
- Zutatensuche im Dialog mit Auswahl der Zielgruppe; bestehende Suchpriorität Lager vor Organisation vor Zentral bleibt erhalten. Hinzufügen schreibt nur in den Arbeitsentwurf, nicht in die API.
- Mengenregeln Linear/Fixe Menge/In festen Schritten im fokussierten Dialog. Stufenparameter und Altersstufenverhalten werden unverändert gespeichert. Moduswechsel setzt Stufenparameter wie zuvor zurück. Abbrechen verwirft nur die lokalen Dialogwerte.
- Verschieben in eine andere Gruppe im Dialog; bestehende Reihenfolge-Normalisierung bleibt erhalten.
- Ernährung/Ersatz im Dialog statt dauerhaft ausgebreiteter Ersatzkarten. Ersatzmenge, Einheit und Konfliktzuordnungen verwenden die vorhandenen Funktionen. Dialogkopien verhindern unbeabsichtigtes Überschreiben vor „Übernehmen“. Mehrfachzuordnungen desselben Konflikts zu unterschiedlichen Ersatzzutaten bleiben gesperrt.
- Zutatenaktualisierungen zeigen jede betroffene Position mit alter/neuer Revisionsnummer. Übernahme nur einzeln per ausdrücklicher Aktion im Bearbeitungsmodus, keine Massen- oder automatische Aktualisierung. Neue Revisionsmetadaten werden nach der Auswahl aus den vorhandenen Updateinformationen korrekt angezeigt; keine neuen API-Aufrufe.
- Zusammenfassungen unten nennen tatsächliche Zutatenhinweise und verfügbare Updates. Keine erfundenen Statuszahlen oder Nährwerte im Zutaten-Tab.
- Native modale Dialoge mit Escape/Abbrechen, Fokus-Rückgabe und begrenztem internen Scrollbereich. Native Einheiten-Auswahllisten verhindern Portalprobleme innerhalb der modalen Ebene.
- Gemeinsames Speichern und Abbrechen des Rezepts umfasst weiterhin alle Zutatenänderungen. Dialogänderungen werden beim Verlassen der Seite als ungespeichert erkannt; eine implizite Formularabgabe bei offenem Dialog speichert nicht den gesamten Entwurf.

## Daten und bekannte Grenzen

- Herkunft wird nur aus dem verfügbaren Katalogeintrag exakt derselben Zutatenrevision gelesen. Fehlt diese Information, steht „Keine Angabe“. Daten einer neueren Revision werden nicht stillschweigend verwendet.
- Revisionsnummern bleiben echte Ganzzahlen des bestehenden Vertrags; keine erfundenen v1.x-Versionen.
- Die Farben der Zutatenhinweise zeigen wie bisher die Varianten-Vermeidbarkeit der Ausgangszutat: Gelb bei vorhandener Variante, Rot ohne aktive Variante, neutral bei ausstehender Prüfung. „Ersatz hinterlegt“ behauptet keine erfolgreich geprüfte Gesamtversorgung; diese verbleibt bei der bestehenden Fachprüfung.
- Zutatenbilder und Umordnen durch Ziehen sind visuell vorgesehen und als geplant gekennzeichnet. Umordnen über Menüs funktioniert bereits. Keine Drag-Aktion wird vorgetäuscht.
- Unterrezepte sind inzwischen als eigene Einträge dargestellt und ihre Einbindungen bearbeitbar. Details: [Unterrezepte](recipe-editor-ux-subrecipes.md).
- Große Rezepte können innerhalb des Tabs scrollen; Gruppen sind einklappbar. Schmale Ansichten scrollen die Tabelle innerhalb der Karte horizontal. Es gibt keinen globalen horizontalen Seitenüberlauf.
- Grunddaten, Nährwerttab und Veröffentlichung wurden in diesem Schritt nicht weiter fachlich umgebaut. Keine Backend-, API-, Domain- oder Datenbankänderungen.

## Geänderte Dateien

- `src/frontend/src/app/features/recipes/recipe-editor.component.html`: kompakte Tabellen, Kontextmenüs und Dialoge.
- `src/frontend/src/app/features/recipes/recipe-editor.component.scss`: Tabellen, Dialoge, Responsive-Verhalten und Lesedarstellung.
- `src/frontend/src/app/features/recipes/recipe-editor.component.ts`: Dialogzustand und -kopien, Textdarstellung, vorhandene Updates korrekt auflösen, Fokus und Dirty-State.
- `src/frontend/src/app/features/recipes/recipe-editor-ingredients.spec.ts`: sechs neue Regressionstests.
- `src/frontend/src/app/features/recipes/recipe-editor-scaling.spec.ts`: vorhandener Mengenregeltest prüft nun den Kontextdialog und seine Übernahme.
- Diese Dokumentation, Referenzverweise und Screenshots unter `screenshots/recipe-editor/ingredients/`.

## Verifikation

- Vollständiger Frontend-Lauf: **292 Tests in 36 Dateien bestanden**.
- Produktionsbuild: bestanden.
- Neue Tests: Textansicht, Mengenänderungen mit gepinnter Revision, lokale Mengenregelwerte/Abbrechen, lokale Ersatzkonfiguration/Abbrechen, ausdrückliche Updateübernahme mit korrekten Metadaten, Gruppen und Positionen umreihen/verschieben. Bestehender Mengenregeltest besteht weiter.
- Testumgebung bildet ausschließlich die fehlende native Dialogmethode nach und stellt den ursprünglichen Prototyp nach jedem Test wieder her. Die echten Dialoginteraktionen wurden zusätzlich im Browser geprüft.
- Browser mit vorhandenem synthetischem „TEST 05 Zwei unabhaengige Aenderungen“: Ansicht/Bearbeitung, Stufenregel im Dialog übernehmen und danach global verwerfen, Ersatzmenge/Einheit lokal ändern und abbrechen, Fokus-Rückgabe, Gruppenmenü und Zutatensuche. Kein Rezept gespeichert oder veröffentlicht.
- 1920×1080: getestete Zutatenansicht/Bearbeitung ohne Seiten- oder Panel-Scrollen.
- 1440×900: keine Seitenscrollbalken im getesteten Rezept.
- 390×844: Tabellen innerhalb ihrer Karten seitlich scrollbar; nach Korrektur der unsichtbaren Aktionsspalten-Beschriftung kein globaler horizontaler Überlauf (Seitenbreite 375px bei Viewport 390px). Mengenregel-Dialog 337px breit und vollständig im Viewport.

## Abnahme und nächste Schritte

Visuelle Abnahme dieses Zutaten-Umbaus abwarten. Danach folgt der Nährwerttab gemäß den bereitgestellten Ansichts- und Bearbeitungsreferenzen. Die eigenständige Veröffentlichungsprüfung und weitere geplante Fachfunktionen bleiben separate Erweiterungen.


## Nachher-Bilder

- [Ansicht 1920×1080](screenshots/recipe-editor/ingredients/recipe-ingredients-view-1920.jpg)
- [Bearbeitung 1920×1080](screenshots/recipe-editor/ingredients/recipe-ingredients-edit-1920.jpg)
- [Bearbeitung 1440×900](screenshots/recipe-editor/ingredients/recipe-ingredients-edit-1440.jpg)
- [Schmale Ansicht](screenshots/recipe-editor/ingredients/recipe-ingredients-edit-narrow.jpg)
- [Mengenregel-Dialog](screenshots/recipe-editor/ingredients/recipe-ingredients-scaling-dialog.jpg)
- [Ersatzdialog](screenshots/recipe-editor/ingredients/recipe-ingredients-replacements-dialog.jpg)
- [Schmaler Dialog](screenshots/recipe-editor/ingredients/recipe-ingredients-dialog-narrow.jpg)


## Mengenregel: In festen Schritten

Die bisherige Bezeichnung „Stufenweise“ wurde durch „In festen Schritten“ ersetzt, auch in der operativen Versorgungsansicht. Sie ist damit von Altersstufenfaktoren klarer unterscheidbar.

- Eingabe als Satz: „Für je angefangene [5] Standardportionen werden [30] g benötigt.“
- Eine sofort aktualisierte Vorschau zeigt drei Mengenbereiche, beispielsweise bis 5: 30 g, über 5 bis 10: 60 g, über 10 bis 15: 90 g.
- Beide Werte müssen endlich und größer als null sein. Ungültige Eingaben sperren die Übernahme; Dezimalwerte bleiben erlaubt.
- Die Zutatenzeile zeigt die konkrete Regel. Die Mengenspalte zeigt „Nach Mengenregel“ statt einer konkurrierenden Mengenangabe. Der bisherige Referenzwert bleibt erhalten und wird bei Linear/Fixe Menge wieder angezeigt.
- Bei aktiven Altersstufenfaktoren erläutert ein Hinweis den gewichteten Standardportionsbedarf.
- Die vorhandenen API-Felder, gespeicherten Regeln und die Berechnung im Backend bleiben unverändert. Die Vorschau ist eine Darstellung der bestehenden Aufrundungsregel.

Verifikation: 294 Frontend-Tests in 36 Dateien bestanden. Im Browser bei 1920×1080 und 390×844 geprüft: Live-Vorschau, Sperre bei null, Übernahme in die lokale Zutatenzeile und globales Verwerfen. Keine Testdaten gespeichert oder veröffentlicht.

- [Mengenregel auf Desktop](screenshots/recipe-editor/fixed-steps/recipe-fixed-steps-dialog.jpg)
- [Mengenregel auf schmalem Bildschirm](screenshots/recipe-editor/fixed-steps/recipe-fixed-steps-dialog-narrow.jpg)
- [Konkrete Regel in der Zutatenliste](screenshots/recipe-editor/fixed-steps/recipe-fixed-steps-list.jpg)


## Lineare Mengenregel: optionale Grenzen (20.11.2026)

Im Dialog „Mengenregel“ können bei Linear unabhängig Mindestmenge und Höchstmenge angegeben werden. Die Einheit ist die Einheit der Zutatenposition. Leere Felder bedeuten keine Grenze; null ist als explizite Grenze erlaubt. Negative Grenzen und Minimum größer als Maximum werden im Editor und in der API abgewiesen. Beim Wechsel zu Fixe Menge oder In festen Schritten werden die Grenzen entfernt.

Berechnung: Zuerst linear mit den vorhandenen Portions-/Altersstufenfaktoren rechnen, dann die Gesamtmenge dieser Zutatenposition auf die Grenzen begrenzen. Beispiel 10 g pro Referenzportion mit 5 g Minimum und 100 g Maximum: 0,1 Portionen ergeben 5 g, 1 Portion 10 g, 20 Portionen 100 g. Nullbedarf erzeugt keine Zutatenmenge. Die Begrenzung gilt je berechneter Zutatenposition und Rezeptvorkommen, auch in Unterrezepten; nicht nachträglich auf die aggregierte Einkaufsliste. Nährwerte basieren auf der begrenzten Menge. Bei Ersatz mit anderer Einheit werden Grenzen in diese Einheit umgerechnet; dieselbe Einheit behält den Zahlenwert bei.

API: Zutatenpositionen im bestehenden Rezepteditor erhalten die optionalen JSON-Felder `minimumQuantity` und `maximumQuantity` (Dezimalzahl oder null). Beide gelten nur für `scalingMode: 0`. Ungültige Kombinationen liefern HTTP 400. Domainmodell, Entwurfsspeicherung, veröffentlichte Snapshots und Kopieren in einen neuen Entwurf tragen beide Werte durchgängig mit. Alte API-Aufrufe und Snapshots ohne die Felder bleiben unbegrenzt.

Migrationen `AddLinearRecipeQuantityLimits` für SQLite und PostgreSQL ergänzen zwei nullable Spalten ohne Änderung vorhandener Mengen. Die lokale SQLite-Testdatenbank wurde erfolgreich aktualisiert und die API neu gestartet. PostgreSQL-Migration ist erzeugt und gebaut; kein PostgreSQL-Laufzeittest in diesem Schritt.

Browser: Desktop 1920×1080 und schmal 390×844 geprüft; Dialog in der schmalen Ansicht 337 px breit und vollständig im Viewport. Minimum über Maximum sperrt die Übernahme. Nach lokaler Übernahme erscheint „Linear · min. 5 g · max. 100 g“ in der Zeile. Anschließend verworfen; keine Testrezepte gespeichert oder veröffentlicht.

- [Desktop](screenshots/recipe-editor/linear-limits/recipe-linear-limits-desktop.jpg)
- [Schmale Ansicht](screenshots/recipe-editor/linear-limits/recipe-linear-limits-narrow.jpg)

Abschlussprüfung: **295 Frontend-Tests und 367 Catering-Backend-Tests bestanden**; Frontend-Produktionsbuild und API-Build ohne Fehler. Regressionen decken API-Abweisung ungültiger Grenzen, SQLite-Entwurfsspeicherung, Snapshot-Kompatibilität, Nullbedarf, unabhängige Grenzen, Altersstufenfaktoren und Einheitenwechsel bei Ersatz ab.
