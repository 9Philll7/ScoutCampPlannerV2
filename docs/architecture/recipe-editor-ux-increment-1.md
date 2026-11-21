# Rezepteditor: Bestandsanalyse und UX-Inkrement 1

Stand: 17.11.2026. Umsetzung abgeschlossen, visuelle Freigabe ausstehend. Inkremente 2 bis 6 wurden noch nicht begonnen.

## Auftrag und Referenzen

Die vier Bilder liegen unter [mockups/recipes](mockups/recipes/README.md). Der unveränderte Benutzerauftrag liegt dort als [UX_Auftrag.md](mockups/recipes/UX_Auftrag.md). Er verlangt nach jedem Inkrement Tests, Dokumentation und Freigabe vor dem nächsten Schritt.

## Bestandsanalyse

- `RecipeEditorComponent` war ein langes Formular mit Grunddaten, gruppierten Zutaten, Mengenregeln, Ersatzzutaten, Nährwertschätzung und Veröffentlichungsaktionen. Die Vorlage und Styles waren inline.
- `RecipeEditorApiService` verwendet Lagerkatalog, verfügbare Zutaten, GET/POST/PUT Arbeitsentwurf, Nährwertberechnung und POST Veröffentlichung. Es gibt keinen separaten API-Aufruf für eine folgenlose Veröffentlichungsprüfung. Ein erfolgreicher Publish-Aufruf veröffentlicht bereits; er wird daher nicht als reine Prüfung angeboten.
- `RecipeEditorService` autorisiert Lesen/Bearbeiten im Lager. `RecipeDraftStore` speichert den vorhandenen Arbeitsentwurf mit optimistischer Versionsprüfung. Veröffentlichte Revisionen sind hiervon getrennt und werden beim Speichern des Entwurfs nicht ersetzt. Der Rezeptstatus „Aktiv“ kann auch bei einem weiterbearbeiteten Entwurf bestehen bleiben.
- Zutatenpositionen behalten die ausgewählte veröffentlichte Zutatenrevision. Updates werden explizit übernommen. Gruppen, Reihenfolge, lineare/fixe/stufenweise Mengenregeln, Konfliktzuordnungen und Ersatzkonfigurationen bleiben unverändert.
- Die vorhandenen Stufenfaktoren beziehen sich auf Lagerstufen. Die Staffelung nach Personenzahl im Mockup entspricht keiner vorhandenen Rezeptfunktion und wird nicht eingeführt.
- Das bestehende Feld `description` enthält Beschreibung und Zubereitung gemeinsam. `source` ist optional im Editor, kann bei der bestehenden Veröffentlichungsprüfung jedoch einen Hinweis auslösen.
- Die Nährwertberechnung beruht auf dem gespeicherten Arbeitsentwurf. Sie liefert Vollständigkeit und fehlende Beiträge; es werden keine manuellen Nährwertwerte ergänzt oder Teilsummen erfunden.
- Modern-Outdoor-Tokens aus `visual-foundation.scss`, vorhandene Material-Komponenten und Aktionsicons werden weiterverwendet. Die dunkle Sidebar bleibt gemäß Design Guide bestehen.

## Inkrement 1: umgesetzt

- Gemeinsamer kompakter Kopf mit Rezeptname, Bildplatzhalter, echtem Rezeptstatus, Arbeitsmodus, Lagerbezug und vorhandener veröffentlichter Revisionsnummer. Keine erfundene semantische Versionsnummer aus der technischen Entwurfsversion.
- Vier erreichbare Tabs mit Tastaturbedienung: Grunddaten, Zutaten, Nährwerte, Prüfung & Veröffentlichung. Ein gemeinsames Formular erhält den Bearbeitungszustand über Tabwechsel. Die alten Inhalte der letzten drei Tabs wurden nur zugeordnet, noch nicht nach deren Mockups umgebaut.
- Zweispaltige Grunddaten: allgemeine Angaben links; Standardportionen und vorhandene Lagerstufenfaktoren rechts. Kompakte Felder, explizite Labels und ein Schalter für die bestehenden Stufenfaktoren.
- Zunächst Ansichtsmodus. „Bearbeiten“ bzw. „Entwurf bearbeiten“ schaltet den vorhandenen Arbeitsentwurf frei. Neu angelegte Rezepte starten direkt in dessen Bearbeitung.
- Gemeinsames Entwurfsspeichern bleibt getrennt vom Veröffentlichen. Während der Bearbeitung ist Veröffentlichen gesperrt. Abbrechen stellt den vollständigen geladenen Zustand einschließlich Zutatenreferenzen wieder her.
- Warnung bei ungespeicherten Änderungen auf dem Rückweg, beim Bereichswechsel und beim Schließen der Seite. Exakte Änderungserkennung ersetzt im Rezepteditor die bisherige pauschale Warnung aufgrund vorhandener Eingabefelder. Während Speichern/Veröffentlichen wird der Bereichswechsel blockiert.
- Der Arbeitsbereich nutzt die gesamte verfügbare Breite. Desktop-Panel scrollt bei langen Inhalten innerhalb des Editors; die gemeinsame Fußleiste bleibt sichtbar. Schmal stehen die Karten untereinander, Tabs sind horizontal scrollbar, die Aktionen bleiben erreichbar.
- Nährwerte stehen ausschließlich im Nährwerttab.

## Geänderte Dateien

- `src/frontend/src/app/features/recipes/recipe-editor.component.ts`: Bearbeitungszustand, Abbrechen, Navigationsschutz, Tastaturtabs und Auslagerung der Vorlage/Styles.
- `src/frontend/src/app/features/recipes/recipe-editor.component.html`: gemeinsame Shell, Grunddaten und Tab-Zuordnung bestehender Bereiche.
- `src/frontend/src/app/features/recipes/recipe-editor.component.scss`: vorhandene Bereiche plus neues Layout mit Design-Tokens und Breakpoints.
- `src/frontend/src/app/features/recipes/recipe-editor-navigation.spec.ts`: neue Workflow- und Navigationstests.
- `src/frontend/src/app/features/recipes/recipe-editor-scaling.spec.ts`: bestehender Mengenregeltest aktiviert den expliziten Bearbeitungsmodus.
- `src/frontend/src/app/app.component.ts` und `.html`: voller Arbeitsbereich, präziser Dirty-State, Schutz laufender Speicheraktionen.
- `docs/architecture/mockups/recipes/*`: vier Referenzbilder, Auftrag und Erläuterungen.
- Diese Dokumentation und die Abnahmebilder unter `docs/architecture/screenshots/recipe-editor/increment-1/`.

## Bewusste Abweichungen und offene Punkte

- Keine getrennte Kurzbeschreibung/Zubereitung, keine Rezeptkategorie oder Bildverwaltung. Tags, interne Notizen und Unterrezepte erhalten hier keine neue Bedienoberfläche; bereits vorhandene Werte bleiben beim Speichern erhalten.
- Keine frei definierbare Mengenstaffel nach Personenzahl. Es bleibt beim vorhandenen Stufenfaktor-Schalter; eine gespeicherte Bezugsstufe wird nur angezeigt.
- Keine erfundenen Zeitstempel, Autorennamen, Revisionsverläufe, v1.0-Labels oder erfolgreichen Einzelprüfungen. Revisionsnummern kommen aus dem vorhandenen Katalog.
- „Entwurf bearbeiten“ erzeugt keinen zusätzlichen Backend-Entwurf: der bestehende Arbeitsentwurf wird gemäß Architektur genutzt. Beim neuen Rezept bleibt der ursprüngliche POST-Erstellungsablauf erhalten. Abbrechen löscht diesen bereits angelegten Entwurf nicht.
- Separate Prüfung ohne Veröffentlichung ist eine API-Lücke für Inkrement 5. Die vorhandene Aktion bleibt wahrheitsgemäß „Veröffentlichen“.
- Kompakte Zutatentabellen, kontextbezogene Mengenregel-/Ersatzdialoge, Konflikt- und Updategestaltung, neues Nährwertlayout sowie detaillierter Prüfungsablauf folgen erst nach jeweiliger Freigabe in den weiteren Inkrementen.
- Keine Backend-, Domain-, API-, Berechnungs- oder Datenbankänderungen.

## Tests und visuelle Prüfung

- Frontend-Tests: **286 bestanden in 35 Dateien**, einschließlich fünf neuer Tests für Ansicht/Bearbeitung, Tabwechsel, Abbrechen, separates Speichern, Rechte, Warnung beim Verlassen, Speicherfehler und Tastaturbedienung. Der bestehende stufenweise Mengenregeltest besteht weiterhin.
- Der erste Gesamtlauf scheiterte beim Start des Vitest-Unterprozesses ohne ausgeführte Tests. Gezielter Wiederholungslauf (6 Tests) und anschließender vollständiger Lauf bestanden.
- Produktionsbuild: bestanden, einschließlich Abschlussbuild nach dem finalen Schalterwechsel; keine Buildfehler oder Warnungen.
- Browser: bestehendes synthetisches TEST-Rezept geöffnet, Ansichtsmodus, explizites Bearbeiten, Tabwechsel mit ungespeicherter Quelle und anschließendes Verwerfen geprüft. Es wurde kein Rezept gespeichert oder veröffentlicht.
- **1920×1080**: Grunddaten und Fußleiste ohne Seitenscrollen.
- **1440×900**: Grunddaten auch im Bearbeitungsmodus ohne Seitenscrollen; Fußleiste innerhalb des Viewports.
- **390×844**: kein globaler horizontaler Überlauf, Karten untereinander, erreichbare Aktionen; vertikales Scrollen ist auf schmalen Bildschirmen vorgesehen.
- `git diff --check`: bestanden (nur bestehende Zeilenenden-Hinweise).

## Nächster Schritt

Freigabe von Inkrement 1 abwarten. Danach Inkrement 2: kompakter Zutatentab einschließlich Gruppen und kontextbezogenen Mengenregeln. Keine automatische Fortsetzung vor der Abnahme.


## Browserabgleich und Nachbesserung vom 19.11.2026

Alle vier Tabs wurden im echten Browser mit dem vorhandenen TEST-Rezept „TEST 04 Eindeutiger Ersatz“ mit den Referenzbildern verglichen. Es wurden keine Rezeptdaten gespeichert oder veröffentlicht. Die Rückmeldung zum visuellen Abstand wird als Nachbesserung von Inkrement 1 behandelt; die fachlichen Folgeinkremente sind damit nicht abgeschlossen.

| Bereich | Beobachtung im Browser | Einordnung |
| --- | --- | --- |
| Kopf und Grunddaten | Status unter dem Namen, fehlende Beschreibungszeile, bis zur Fußleiste gestreckte Karten, kleine Feldtypografie | Layoutmängel; jetzt korrigiert |
| Zutaten | Große Material-Felder und dauerhaft eingeblendete Ersatzkarten statt kompakter Tabellenzeilen und Kontextdialoge | Bestehende Bearbeitungsfunktionen vorhanden; geplanter UI-Umbau aus Inkrementen 2/3 fehlt noch |
| Nährwerte | Große leere Flächen und aufgeblasener Warnhinweis; keine zweispaltige Darstellung | Streckfehler jetzt behoben; eigentlicher Mockup-Umbau folgt in Inkrement 4 |
| Nährwertdaten des Testrezepts | Verwendete Milchzutat besitzt kein Nährwertprofil; Berechnung ist unvollständig | Fehlende Daten. Beispielwerte aus dem Mockup dürfen nicht eingesetzt werden. Die vorhandene API liefert hier keine belastbaren Gesamtsummen |
| Prüfung & Veröffentlichung | Bisheriger Veröffentlichungsknopf und Prüfergebnisse erst nach API-Antwort statt eigenständiger Prüfseite | UI-Umbau folgt in Inkrement 5; separate Prüfung ohne Veröffentlichung fehlt tatsächlich in der API |
| Rezeptbild und Kategorie | Nicht im bestehenden Vertrag des Editors vorhanden | Echte Erweiterungen; nicht als Attrappen eingebaut. Im Kopf steht die vorhandene Rezeptart statt einer erfundenen Kategorie |
| Kurzbeschreibung und Zubereitung | Ein gemeinsames Feld im vorhandenen Modell | Keine verlustfreie Trennung ohne Erweiterung. Die vorhandene Beschreibung wird im Kopf nur gekürzt angezeigt, nicht verändert gespeichert |
| Mengenstaffel nach Personenzahl | Im Mockup als Stufenfaktoren gezeigt | Andere Fachfunktion als die existierenden Faktoren der Lagerstufen. Kein neuer Berechnungsmechanismus eingeführt |
| Autoren/Verlauf | Keine entsprechenden vollständigen Daten im Editorvertrag | Keine fiktiven Historien oder Personen angezeigt |

### Durchgeführte Korrekturen

- Kopf: Name, Status und tatsächliche Revision nebeneinander; vorhandene Beschreibung darunter; reale Rezeptart und Bearbeitungsstand rechts.
- Größere Labels und Eingabetexte, angepasste Tab- und Abschnittstypografie.
- Grunddatenkarten mit inhaltsbezogener Höhe und gemeinsamen Unterkanten statt Füllung der ganzen Editorhöhe. Bei 1920×1080 nun ca. 410px statt ca. 683px.
- Grid-Inhalte und Warnhinweise beginnen oben und werden nicht mehr vertikal auf die gesamte freie Höhe verteilt. Nährwertwarnung im Test ca. 45px hoch.
- Unverändert getrennter Ansichts-/Bearbeitungsmodus und unveränderte Speichervorgänge.

### Verifikation

- Sechs gezielte Rezepteditor-Tests bestanden, einschließlich des vorhandenen Mengenregeltests.
- Produktionsbuild bestanden.
- Browser 1920×1080 und 1440×900: keine Seitenscrollbalken in der Grunddatenbearbeitung.
- Browser 390×844: kein globaler horizontaler Überlauf; Karten und Aktionen erreichbar.
- Neue Screenshots: `screenshots/recipe-editor/correction-2026-11-19/`.
- Betroffene Implementierungsdateien dieser Nachbesserung: `recipe-editor.component.html` und `recipe-editor.component.scss`. API und Fachlogik unverändert.

Die Zutaten-, Nährwert- und Prüfseite werden ausdrücklich noch nicht als mockupgetreu oder visuell abgenommen bezeichnet.


## Ergänzende Referenzen für den Ansichtsmodus (19.11.2026)

Die vier zusätzlichen Benutzermockups sind als Bilder 05 bis 08 im Referenzordner gespeichert. Grunddatenwerte werden jetzt wie dort als echte Texte auf grauen Flächen angezeigt; keine deaktivierten Eingabefelder. Die Zubereitung erhält eine begrenzte Lesefläche, die Standardportionen ihre Einheit. Der explizite Bearbeiten-Button im Kopf ist als Primäraktion hervorgehoben. Die vorhandenen fachlichen Grenzen gelten weiter; insbesondere werden keine neuen Kategorien, Bilder oder Historien aus den Beispielen abgeleitet.

Verifikation der Ansichts-Ergänzung: sechs Editor-Tests bestanden; Browser bei 1920×1080 ohne Seitenscrollen und ohne Eingabefelder im Grunddatenpanel; bei 390px Breite kein globaler horizontaler Überlauf. Screenshot: `screenshots/recipe-editor/correction-2026-11-19/recipe-readonly-reference-1920.jpg`.


## Neue Benutzervorgabe: fehlende Funktionen sichtbar vorsehen

Der Benutzer hat am 19.11.2026 ergänzt, dass fehlende Mockup-Funktionen visuell vorgesehen und dokumentiert werden sollen. Frühere Aussagen dieses Berichts, solche Elemente gar nicht darzustellen, werden dadurch ersetzt: Sie sind jetzt klar als „Geplant“ gekennzeichnet und nicht aktiv. Die Fachfunktionen bleiben weiterhin unimplementiert. Vollständige Zuordnung und Verifikation: [Geplante Funktionen](recipe-editor-planned-features.md).


## Fortsetzung nach Freigabe der Grunddaten

Der Benutzer hat den Grunddaten-Tab abgenommen und den Zutaten-Tab beauftragt. Dessen Stand ersetzt die früheren Aussagen dieses Berichts zum noch ausstehenden Zutatenumbau: [Zutaten-Tab](recipe-editor-ux-ingredients.md).
