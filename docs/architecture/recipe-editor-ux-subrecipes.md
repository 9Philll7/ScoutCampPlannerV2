# Rezepteditor Unterrezepte

Stand: 21.11.2026. Unterrezepte sind im Zutaten-Tab als echte Einträge darstellbar und ihre Einbindungen lassen sich hinzufügen, bearbeiten, verschieben, umreihen und entfernen. Die bestehenden Modelle und Berechnungsregeln bleiben erhalten.

## Bedienung

Im Bearbeitungsmodus öffnet „Unterrezept hinzufügen“ eine Suche in den veröffentlichten Rezeptrevisionen der Lagerbibliothek. Die Auswahl bindet ausdrücklich diese Revision ein. Name und Revisionsnummer stammen aus dem veröffentlichten Stand, nicht aus dem aktuellen Arbeitsentwurf des Unterrezepts.

Die Einbindung erhält eine Gruppe und entweder Standardportionen des Unterrezepts oder eine benötigte Menge mit kompatibler Einheit. Die Menge gilt für den Rezeptbezug des Hauptrezepts; der vorhandene Rechner skaliert sie bei anderem Bedarf. Positive Dezimalwerte sind erlaubt. Selbstbezug, bekannte Kreisverweise, nicht auswählbare Revisionen, ungültige Einheiten und doppelte Einbindungen derselben Revision in derselben Gruppe werden vor der Übernahme abgefangen. Die serverseitige Veröffentlichungsprüfung bleibt maßgeblich.

Bestehende Einbindungen werden über ihre Mengenangabe oder das Drei-Punkte-Menü bearbeitet. Bei einer Suche nach einem anderen Rezept ist ein Revisionswechsel eine ausdrückliche Auswahl; es gibt keine automatische Aktualisierung auf einen neueren Stand. Im Ansichtsmodus stehen Name, Menge, Revisionsnummer und Ernährungshinweise als Text in der Tabelle.

Zutaten und Unterrezepte teilen sich dieselbe Reihenfolge innerhalb einer Gruppe. Hinzufügen und Umreihen erzeugt daher auch bei gemischten Listen fortlaufende, eindeutige Sortierwerte. Die Fehlernavigation im Prüfungstab kann Unterrezeptpositionen und zugehörige Ersatzverweise markieren und fokussieren.

Dialogänderungen werden erst mit „Übernehmen“ in den lokalen Rezeptentwurf kopiert. Dialogabbruch verwirft lokale Eingaben. Gemeinsames Speichern und Abbrechen des Hauptrezepts umfasst alle Einbindungen; bei offenem Dialog wird nicht versehentlich der Hauptentwurf gespeichert. Veröffentlichte Unterrezepte selbst werden dabei nicht bearbeitet.

## Vorhandene Ersatzrezepte

Bestehende Ersatzrezepte und Konfliktzuordnungen bleiben bei Mengen- und Gruppenänderungen unverändert erhalten und sind im Dialog einsehbar und bearbeitbar. Ein Wechsel des Rezepttyps ist bei vorhandenen Ersatzzuordnungen gesperrt. Ersatzrezepte und ihre Konfliktzuordnungen werden direkt im Unterrezeptdialog bearbeitet. Ebenso bleiben Zutatenbilder und Umordnen durch Ziehen geplant; Umreihen per Menü funktioniert.

## Daten und API

Die vorhandenen Schreibfelder unter `content.subrecipePositions` werden weiterverwendet: `id`, `groupId`, `recipeRevisionId`, `servings`, `quantity`, `unitId`, `sortOrder`, `replacements`. Domain, Speicherung, Snapshot-Format und Datenbank benötigen keine Erweiterung.

Die bestehende Rezepteditor-Antwort erhält additiv `subrecipeReferences`. Jeder Eintrag enthält `revisionId`, `recipeId`, `revisionNumber`, `name`, `recipeType`, `referenceServings`, `referenceQuantity`, `referenceUnit`, `units`, `canSelect` und `conflicts`. Diese Metadaten beschreiben veröffentlichte Revisionen und sind keine neue Schreiboberfläche für den Rezeptinhalt.

Der Metadatenleser begrenzt sich auf Rezepte der zugänglichen Lagerbibliothek. Neben auswählbaren Revisionen werden dort verfügbare, bereits gebundene historische Revisionen einschließlich vorhandener Ersatzrezepte aufgelöst. Mengenrezepte bieten ausschließlich Einheiten derselben Dimension an. Die vorhandene Zyklusprüfung wird wiederverwendet. Fehlende Referenzen werden als nicht verfügbar angezeigt und nicht durch Daten einer neueren Revision ersetzt.

## Verifikation

- 316 Frontend-Tests in 39 Dateien und 369 Catering-Backend-Tests bestanden.
- Sieben neue UI-Regressionen decken die exakte Revisionsanzeige, Hinzufügen/Speicherpayload, lokale und globale Verwerfung, Erhalt vorhandener Ersatzregeln, Mengen-/Einheitenprüfung, doppelte Einbindungen, gemischte Reihenfolge sowie Selbst-/Kreisverweise ab.
- Ein neuer SQLite-Integrationstest prüft unveränderliche Revisionsnamen, historische Bindungen, Lagergrenzen, kompatible Einheiten und Zyklusmarkierung.
- API-Build ohne Warnungen/Fehler und Frontend-Produktionsbuild bestanden.
- Browser mit „TEST 12 Subrecipe Ersatz“: echte Zeile statt Platzhalter, Menge von einer auf zwei Standardportionen geändert und lokal übernommen, globales Abbrechen stellt die ursprüngliche Einbindung samt Ersatzregel wieder her.
- Eine weitere veröffentlichte Revision über die Suche gewählt, mit drei Standardportionen lokal eingefügt und danach wieder verworfen. Keine Testrezepte gespeichert oder veröffentlicht.
- Desktop 1920×1080 und schmal 390×844 geprüft. Der schmale Dialog ist 337 px breit und 796 px hoch; Header und Aktionen bleiben sichtbar, längerer Inhalt scrollt innerhalb des Dialogs.
- Lokale Test-API mit erweitertem Lesevertrag neu gestartet; keine Migration erforderlich.

## Geänderte Bereiche

- `RecipeEditorService`: additive Metadaten in der bestehenden Antwort.
- `RecipeEditorSubrecipeReferenceStore` und Registrierung in der API: veröffentlichte, zugängliche Revisionen und Mengenbezug auflösen.
- Frontend API-Typen und `recipe-editor.component.{ts,html,scss}`: gemeinsame Eintragsliste, Tabellenzeilen, Such-/Mengendialog, lokale Übernahme und Reihenfolge.
- `recipe-editor-subrecipes.spec.ts` und `RecipeDraftStoreTests.cs`: Regressionen und Metadaten-Integrationstest.

## Nachher Bilder

- [Ansicht mit bestehendem Unterrezept](screenshots/recipe-editor/subrecipes/recipe-subrecipes-view-1920.jpg)
- [Bestehende Einbindung bearbeiten](screenshots/recipe-editor/subrecipes/recipe-subrecipes-edit-dialog.jpg)
- [Unterrezept hinzufügen](screenshots/recipe-editor/subrecipes/recipe-subrecipes-add-dialog.jpg)
- [Zusätzliche Einbindung im lokalen Entwurf](screenshots/recipe-editor/subrecipes/recipe-subrecipes-local-addition.jpg)
- [Schmaler Dialog](screenshots/recipe-editor/subrecipes/recipe-subrecipes-dialog-narrow.jpg)


## Ergänzung: Berechnungsbezug und Ersatzrezepte

In den Grunddaten kann zwischen Portionen und Menge gewechselt werden. Die neue Bezugsmenge muss bewusst eingegeben werden; es wird keine Gewichtsumrechnung aus Portionen erfunden. Beim Wechsel werden unpassende Referenzfelder geleert. Mengenrezepte verwenden keine Stufenfaktoren. Die Einheiten kommen aus dem bestehenden Endpunkt `GET /api/ingredient-reference-data`; Speichern und Veröffentlichen verwenden unverändert die vorhandenen Rezeptverträge.

Bei einer Unterrezept-Einbindung richtet sich die Eingabe nach der gebundenen veröffentlichten Revision. Deren Ersatzrezepte lassen sich jetzt direkt hinzufügen, bearbeiten und entfernen. Ein Ersatz verwendet denselben Rezepttyp und braucht eine positive Menge sowie mindestens eine zugeordnete Ernährungsanforderung. Dieselbe Anforderung darf nicht zwei Ersatzregeln zugeordnet sein. Historische gespeicherte Zuordnungen bleiben erhalten. Weiterhin enthaltene oder neu hinzukommende Konflikte werden angezeigt; die bestehende Veröffentlichungsprüfung bleibt verbindlich.

Ersatzänderungen werden zuerst innerhalb des Unterrezeptdialogs übernommen und anschließend als Teil des Rezeptentwurfs gespeichert. Abbrechen verwirft lokale Änderungen. Weder Datenbankmigration noch neue Backend-Aufrufe sind erforderlich.

Validierung: Frontend-Build erfolgreich; 319 Tests in 39 Dateien erfolgreich, einschließlich Umschalten/Abbrechen des Berechnungsbezugs, Bearbeiten bestehender Ersatzregeln und Prüfung positiver Mengen, gleicher Rezepttypen und eindeutiger Anforderungszuordnungen.


Browsernachprüfung: Berechnungsbezug Portionen/Menge und Laden der Einheiten getestet, Änderungen verworfen. Vorhandenes Ersatzrezept lokal von einer auf zwei Standardportionen geändert. Ein weiterhin milchhaltiger Ersatz zeigt einen Konflikthinweis. Schmale Ansicht 390×844: Dialog 337 px breit, ca. 754 px hoch, alle Aktionen erreichbar. Keine Rezeptdaten gespeichert oder veröffentlicht.

- [Ersatzübersicht](screenshots/recipe-editor/subrecipes/recipe-subrecipe-replacements.jpg)
- [Ersatz bearbeiten](screenshots/recipe-editor/subrecipes/recipe-subrecipe-replacement-editor.jpg)
- [Ersatzeditor schmal](screenshots/recipe-editor/subrecipes/recipe-subrecipe-replacement-mobile.jpg)


### Einheitliches Popup für Ernährung und Ersatz

Die Spalte „Ernährung & Ersatz“ öffnet bei Unterrezepten denselben nativen Dialograhmen wie bei Zutaten. Die zuvor aufklappende Tabellenzeile entfällt. Im Ansichtsmodus zeigt das Popup Ersatzrevisionen, Mengen und Anforderungen schreibgeschützt. Im Bearbeitungsmodus lassen sich Ersatzregeln direkt im Popup ändern, lokal verwerfen und ausdrücklich übernehmen. Das Öffnen erzeugt keine ungespeicherten Änderungen. Einbindungsmenge und Gruppe bleiben im separaten Einbindungseditor.


### Variantenabdeckung verschachtelter Unterrezepte

Der Metadatenleser füllt `preventableByVariants` jetzt auch für Unterrezepte. Dafür werden die gebundenen veröffentlichten Unterrezeptrevisionen rekursiv ausgewertet und die vorhandene Variantenprüfung der Zutaten wiederverwendet. Ein Konflikt gilt nur als durch Varianten vermeidbar, wenn jede verursachende Zutatenposition – auch in verschachtelten Rezepten – mindestens eine aktive passende Variante hat. Fehlende Metadaten oder Kreisverweise werden nicht als abgedeckt gewertet. Ersatzregeln werden nicht als Zutatenvarianten ausgegeben. Kein neuer Endpunkt und keine Migration erforderlich.

In der Liste werden vollständig durch Varianten vermeidbare Konflikte gelb mit „(Variante)“ markiert. Bleibt ein Konflikt ohne Variante bestehen, ist der Gesamtstatus rot. Das Popup erklärt je Konflikt die verfügbaren Zutatenvarianten. Geprüft mit „TEST 11 Dasselbe Subrecipe zweimal“: Die tief verschachtelte Variante von TEST 09 wird sowohl in der Liste als auch im Popup von TEST 10 korrekt aufgelöst. Keine Testdaten geändert.

373 Backend-Tests und 323 Frontend-Tests bestanden. Regressionen decken vollständige und teilweise Variantenabdeckung, fehlende Referenzen, Zyklen sowie gemischte gelbe/rote UI-Hinweise ab.

[Variantenhinweis im Popup](screenshots/recipe-editor/subrecipes/recipe-subrecipe-variant-conflict.jpg)
