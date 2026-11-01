# UX-Verpflegung V2 – Menüpläne

Stand: 2026-11-01. Beginn nach ausdrücklicher Abnahme von UX-V1 und Aufforderung zum Fortfahren. Kein Commit. UX-V3/V4 nicht umgesetzt.

## Ausgangslage und Referenzen

V1 zeigte unter Menüplänen noch die alte Kartenliste und darunter den vollständigen Editor mit Dropdowns für alle Angebote. Die Rezeptzuordnung wählte beim Hinzufügen bereits automatisch das erste Katalogrezept; Standards waren Checkboxen. Keine Tagesnavigation und kein Rezeptauswahldialog. Der äußere Camp-Rahmen war in V1 bereits entfernt worden; die volle Arbeitsbreite bleibt erhalten.

Bei Beginn von V2 galt die in V1 dokumentierte Diskrepanz zwischen Bildnamen und Bildinhalten. Damals wurden die tatsächlich passenden Inhalte in `05`/`06`/`07`/`08` verwendet. Der Nutzer hat die Bilder inzwischen mit korrigierter Benennung neu abgelegt: **Aktuell sind `03_menueplan_editor.png` und `05_komponenten_zweispaltig.png` die direkt geöffneten Referenzen für Editor und Kopfzeile.** Die früheren Hinweise auf falsch benannte Bilder und ein ungültiges Bild `02` beschreiben ausschließlich den alten Upload. Die überholte Standardauswahl beim Hinzufügen aus der Rezept-Collage wird weiterhin nicht übernommen. Quelldateien wurden durch die Implementierung nicht umbenannt oder verändert.

Geprüfte Verträge: Angular `MealPlanningApiService`, Backend `MealPlanningService` einschließlich `ValidatePlan`/Zugänglichkeitsprüfung und vorhandene Rezept-Endpunkte in `Program.cs`/`RecipeEditorApiService`. Design-Tokens, Iconkomponente, bestehende Hash-Navigation und Verlassen-Guard weiterverwendet.

## Umsetzung

- Eigene `MealPlanWorkspaceComponent` mit HTML/SCSS, eingebettet in den vorhandenen Menüplanbereich. API-Aufrufe bleiben im bestehenden Controller `MealPlanningComponent`.
- Planübersicht als eigenständige durchsuchbare Liste, acht Zeilen pro Seite, Anlegen-Dialog, Öffnen, Reihenfolge und Löschen. Löschfehler mit Backend-Referenzen werden weiterhin angezeigt.
- Ein Plan wird entweder in der Liste oder im Editor dargestellt; keine breite parallele Planliste. Kompakter Planwechsel und Versionsangabe im Kopf. Echte Standardabonnements werden in der Liste als Anzahl von Kocheinheiten ausgewiesen; kein erfundener Aktivierungsstatus.
- Editor: links Tage mit Belegungszahlen, oben ausschließlich aktive Mahlzeiten im Lagerzeitraum, rechts Komponenten der gewählten Mahlzeit. Auf breitem Desktop eigene Scrollbereiche für Tage und Komponenten; bei schmalem Layout untereinander, Tage horizontal erreichbar.
- Standardrezept prominent links, Alternativen kompakt rechts. Bei geringerer Breite gestapelt. Komponentennamen direkt bearbeitbar, Komponenten einklappbar. Optionale Anzeigenamen/Rollen/Hinweise gezielt über Bearbeitungsaktion erreichbar. Rollen bleiben beschreibende Metadaten.
- Neue Komponente zunächst leer. Erst die bewusste erste Rezeptauswahl erzeugt einen Standardeintrag. Weitere Rezepte sind Alternativen. „Als Standard setzen“ erscheint nur als separate Aktion an einer Alternative. Entfernen des Standards befördert eine verbleibende Alternative; letzter Eintrag darf entfernt werden.
- Rezeptdialog: durchsuchbare, zugängliche veröffentlichte portionenbasierte Revisionen, paginierte Liste und separate Vorschau. Keine doppelte Revision innerhalb einer Komponente. Beim expliziten Revisionswechsel bleiben Anzeigename, Rolle und Hinweis erhalten. Neue Veröffentlichungen ersetzen bestehende Zuordnungen nicht automatisch.
- Speichern/Verwerfen beziehen sich auf den gesamten Plan. Versionskonflikt/Fehler bewahrt den Entwurf. Planwechsel, Rückweg, Neuladen und Bereichswechsel schützen geänderte Entwürfe. Schreibschutz/Freeze und laufende Speicheraktion sperren Änderungen. Laufende Requests werden beim Zerstören der Komponente abbestellt.
- Präziser Dirty-State des Planentwurfs an die bestehende Camp-Navigation angeschlossen. Reine Suche, Tageswechsel und Rezeptvorschau erzeugen keinen pauschalen Änderungsverdacht.
- Meldungen über bestehende automatisch schließende Overlay-Banner; sie verschieben das Layout nicht.
- Bestehende Tagesplan-Aktivierung und gewichtete Berechnungsgrundlagen eingeklappt unter der Planliste. Während der Planeditor geöffnet ist, werden sie nicht darunter gerendert.

## Vertragsgrenzen und dokumentierte API-Bedarfe

1. **Revisionsvorschau:** Die vorhandene Planungsauswahl liefert `revisionId`, `recipeId`, Name, Revisionsnummer, Portionenbasis und vorgeschlagene Rolle. Es existiert kein autorisierter read-only Endpunkt für den vollständigen Inhalt einer veröffentlichten Rezeptrevision. Die vorhandenen Draft-/Draft-Nutrition-Endpunkte wären hierfür fachlich falsch und werden nicht aufgerufen. Vorschau zeigt deshalb ausschließlich diese vorhandenen Angaben plus den Hinweis zur festen Revisionszuordnung. Keine erfundenen Fotos, Zutaten, Ernährungskennzeichnungen oder Nährwerte. Ein späterer vollständiger Preview benötigt einen autorisierten read-only Revisionsvertrag, optional mit echten Bilddaten. **API nicht geändert.**
2. **Leere Komponenten:** Backend `ValidatePlan` verlangt genau einen Standardeintrag pro gespeicherter Komponente. Eine leere UI-Komponente ist deshalb nur ein Entwurfsplatzhalter. Speichern lässt Komponenten ohne Rezepte aus; dies wird vor Speichern sichtbar angekündigt und im Erfolgsbanner bestätigt. Der verbleibende Plan darf beliebig viele unversorgte Mahlzeiten haben und auch vollständig ohne Komponenten gespeichert werden. Ein serverseitiger Platzhalter wäre eine gesonderte Vertragsänderung; hier nicht eingeführt.
3. **Planverwendung:** Sichtbare Nutzungszahl bezeichnet ausschließlich vorhandene Standard-Menüplan-Abonnements. Kein umfassender Verwendungsnachweis für historische Snapshots oder alle operativen Referenzen. Löschung entscheidet weiterhin die bestehende Backendvalidierung.
4. **Zugänglichkeit/Verifikation:** Die publizierte Auswahlliste kommt unverändert aus dem bestehenden geschützten Endpunkt. Keine Ausweitung von Berechtigungen. Planbelegung ist keine Eignungs-/Verifikationsaussage. Mengen- und Versorgungslogik nicht angefasst.

## Validierung

Neue Regressionen decken ab: Listen-/Editortrennung, ausschließlich aktive Mahlzeiten im Zeitraum, erste Auswahl als Standard, weitere als Alternativen, unabhängige additive Komponenten, gezielter Standardwechsel, Duplikatschutz, Suche, Ausschluss nicht portionenbasierter Revisionen, feste Revisionszuordnung bei Katalogänderung, Metadatenerhalt beim Revisionswechsel, Entfernungsverhalten, unvollständiger Plan mit leeren Platzhaltern, Schreibschutz, Versionskonflikt, Ablehnen von Verwerfen/Planwechsel, Dashboard-Ziel ohne wiederholtes Laden sowie Navigation mit ungespeichertem Entwurf.

Finaler Gesamtlauf: **128 Tests in 23 Dateien erfolgreich**, Production Build erfolgreich, `git diff --check` erfolgreich. Keine Backendtests erforderlich, da keine Backend-/Paketänderungen.

## Visueller Prüfstand / Abnahme

Originale visuell analysiert; Umsetzung anhand ihres Layouts und der vorhandenen Tokens. **Keine gerenderte Browserprüfung und keine Nachher-Screenshots behauptet.** Der erneute Versuch, localhost:4200 über Edge zu öffnen, meldete `The browser access host is unavailable`. Der zuvor gesperrte native Computer-Use-Weg wurde nicht erneut verwendet.

Zur manuellen Abnahme:

1. Menüpläne öffnen: reine Liste, Suche/Seiten, Anlegen, Reihenfolge und Referenzfehler beim Löschen prüfen.
2. Plan öffnen: keine breite Planliste; Tag/Mahlzeit wechseln, Standard links und Alternativen rechts bei 1920×1080 prüfen. Lange Komponentenlisten sollen im Editorbereich scrollen. Schmale Ansicht: gestapelte Karten, bedienbare Tage und Dialog.
3. Leere Komponente ergänzen, erstes Rezept auswählen, Alternative ergänzen, ausdrücklich zum Standard machen. Hinzufügen selbst darf keine Standardwahl anbieten.
4. Mehrere Änderungen durchführen: Verwerfen muss alles zurücknehmen; Speichern muss genau eine Planänderung mit erwarteter Version senden. Unvollständige Planung bleibt zulässig.
5. Geänderten Entwurf verlassen und Bestätigung ablehnen; Änderungen müssen erhalten bleiben. Read-only/Freeze prüfen.

V2 wartet auf UI-Abnahme. V3/V4 sind weiterhin separat.

Farbcode-Folgeanpassung: Gelb nur bei vollständig verfügbarer direkter Zutatenvariante/Ersatzlösung, Rot bei nicht vollständig abgedeckter bekannter Anforderung, unbewertete Fälle neutral. `adjustment` im read-only Hinweisvertrag; bestehender Resolver ohne Menüplan-Rezeptalternativen, keine automatische Anwendung. Details und Grenzen in `docs/domain/recipe-planning-hints.md`, Abschnitt „Farbcode“. Vier zusätzliche Backendfälle für eindeutige/mehrdeutige vollständige, inaktive und partielle Varianten; UI-Farbregression. 316 Catering-Tests und 132 Frontend-Tests erfolgreich. API-/Frontend-Build erfolgreich; lokale API neu gestartet und drei echte Seed-Rezeptantworten auf Available/Unavailable geprüft. Keine visuelle Browserabnahme und kein Commit.

## Freigegebene Folgeerweiterung: Rezeptmerkmale und potenzieller Anpassungsbedarf

Die zuvor fehlenden Hinweis-Daten sind nach ausdrücklicher Benutzerfreigabe als read-only API und Karten-/Vorschaudarstellung umgesetzt. Dieser Stand ersetzt die nachfolgenden historischen Vermerke, dass der Vertrag solche Daten noch nicht liefert. Details: `docs/domain/recipe-planning-hints.md`, Abschnitt „Implementierter Vertrag“.

Endpunkt: `GET /api/camps/{campId}/meal-planning/recipes/{revisionId}/hints`. Camp-/Revisionszugriff geprüft; keine Teilnehmerdaten, keine Health-Rechte ausgeweitet. Standardrevision inklusive Unterrezepten, bestehende Eignungs- und Stoffresolver. Chips für bekannte Ursachen, passende Herkunftsregeln und mengenabhängige Angaben, Datenlücken separat. Dialog erklärt die Zutatenbezüge. Keine pauschale Stofffreiheit oder konkrete Teilnehmerfreigabe.

Prüfstand: **312 Catering-Tests**, **5 neue HTTP-Zugriffstests**, **131 Frontend-Tests** erfolgreich. Regulärer API-Build mit App-Host und Frontend-Produktionsbuild erfolgreich; Diff-Prüfung erfolgreich. Keine Migration. Lokale API mit denselben SQLite-/Auditparametern auf localhost:5180 gestartet. Echter HTTP-Test für drei vorhandene veröffentlichte Revisionen erfolgreich, darunter „TEST 03 Eindeutige Variante“ mit zwei bekannten Ursachen.

Anfangs fehlgeschlagene Build-Versuche: `UseAppHost=false` am gesamten Solution-Build ist für xUnit-v3-Projekte ungeeignet; normale API-Ausgabe war durch den vorherigen Serverprozess gesperrt. Danach isolierter Build, Tests mit einem MSBuild-Worker und regulärer API-Build erfolgreich. Die ursprüngliche Test-Challenge-Konfiguration leitete anonym nach Login um; der Endpoint-Test verwendet jetzt dieselbe 401-Challenge wie die Anwendung. Kein Produktiv-Authverhalten verändert. Temporäre Buildausgabe liegt im ignorierten API-bin-Bereich; verbliebene MSBuild-Server beendet.

Erneuter Browserversuch: `The browser access host is unavailable`. Keine neue visuelle Abnahme oder Nachher-Screenshots. Kein Commit.

## Rezeptkarten: Bildfläche und Hinweissymbole

Aktuelle Folgeanpassung: „Als Standard setzen“ befindet sich ausschließlich im Drei-Punkte-Menü einer Alternative. Der zusätzliche Button unter der Karte entfällt. Standardkarten bieten diese Aktion nicht an; die fachliche Umschaltung und der Schutz bei Schreibsperre/laufender Aktion bleiben unverändert.

**Nutzerkorrektur:** Gemeint sind Ernährungskennzeichnungen wie vegetarisch, vegan und laktosefrei, keine Konfliktsymbole. Die zuvor ergänzten neutralen Prüf-/Fragezeichensymbole wurden deshalb wieder entfernt. Hinweis- und Verfügbarkeitsanzeigen bleiben unabhängig davon bestehen. Der unten beschriebene Prüf-Symbolentwurf ist damit verworfen.

**Offener Datenbedarf für Ernährungskennzeichnungen:** `RecipeOption` enthält weiterhin keine belastbaren Ernährungslabels. Für die gewünschten Chips mit Symbol/Text braucht es fachlich belegte Kennzeichnungen je unveränderlicher Revision, einschließlich definierter Bedingungen für Aussagen wie „laktosefrei“. Ein Rezeptname, fehlender Konflikt, Rolle oder ein ungeprüftes Tag ist kein solcher Nachweis. Bis ein entsprechender Vertrag vorhanden ist, zeigt die UI diese Angaben nicht an. Keine API-Änderung oder Ersatzkennzeichnung im UX-Schritt eingeführt.

Fester Bildplatzhalter im Verhältnis 4:3; beim Standard größer, Alternativen kompakter und bei ausreichend Platz nebeneinander. Vorhandene Auswahldaten liefern keine Bild-URL, deshalb ausdrücklich „Kein Bild“, keine erfundenen Rezeptfotos. Bearbeitungsaktionen in kompaktem Kartenmenü; „Als Standard setzen“ bleibt eine separate Aktion.

Symbolzeile für Allergene, Ernährungsanforderungen und Unverträglichkeiten. Da keine rezeptbezogene Eignungsbewertung im Auswahlvertrag vorliegt, neutral mit Fragezeichen und tastaturzugänglichen Tooltips: Bewertung im konkreten Kocheinheit-/Mahlzeitenkontext erforderlich. Keine fingierten Konflikte oder grünen Freigaben. Vorhandener Freitexthinweis erscheint als Info-Symbol; eine nicht im Katalog verfügbare gespeicherte Revision als echter Warnhinweis. Operative Probleme werden nicht ohne belastbare Zuordnung auf Menüplan-Rezeptkarten übertragen.

Für echte Bilder und fachliche Konfliktzustände wäre später ein berechtigter Revisionsbild-/Hinweisvertrag mit definiertem Kontext und Aktualität erforderlich. API/Domain in diesem UX-Schritt unverändert.

## Auswahlhilfe für Komponentenbezeichnungen

Nach Bestätigung durch den Nutzer Dropdown am Komponentenkopf: Hauptspeise, Beilage, Vorspeise, Nachspeise, Getränk sowie „Eigene Bezeichnung“. Nur bei eigener Bezeichnung erscheint das Freitextfeld. Bereits vorhandene individuelle Namen bleiben als eigene Bezeichnung erhalten. Auswahlhilfe schreibt ausschließlich den bestehenden optionalen Komponentennamen; kein neuer Kategorievertrag, keine API-/DB-Änderung, keine Kopplung an Rezeptrollen oder Mengenregeln. Der Katalog ist vorerst fest in der Oberfläche, nicht administrativ konfigurierbar.

## Folgekorrektur: Rezeptname und Komponentenrolle

Anzeigename und Rolle aus der Rezeptdarstellung und -bearbeitung entfernt; ebenso die vorgeschlagene Rolle aus der Rezeptauswahl. Die Karten verwenden den Namen der referenzierten Rezeptrevision. Die fachliche Einordnung erfolgt über den vorhandenen Komponentennamen. Pro Rezept bleibt nur der optionale Hinweis bearbeitbar. Neue Zuordnungen senden `displayName: null` und `role: null`; vorhandene Werte werden beim Speichern unverändert mitgeführt, aber nicht als zweite Namens-/Rollenebene dargestellt. API und Domain unverändert. Ein tatsächliches Entfernen dieser API-Felder oder eine typisierte Rolle auf Komponentenebene wäre separat umzusetzen; hier nicht eingeführt.

## Folgekorrektur: Tageliste

An korrigiertes Original `03` angeglichen: breitere Tagesspalte (auf breitem Desktop bis 330 px), weißes Panel über die Editorhöhe, kompakte zweizeilige Tageszeilen. Wochentag separat links, Datum mit deutschem Monatsnamen, rechts Belegungsicon und kompakte Anzahl. Auswahl mit grüner Fläche und linker Akzentkante. Die Unterzeile zeigt die tatsächliche Zahl aktiver Mahlzeiten statt nicht vorhandener Tagesbeschreibungen wie Anreise/Ausflug. Status bezeichnet ausschließlich Angebotsbelegung, nicht Eignung oder Verifikation. Schmal weiterhin horizontal erreichbar. 128 Tests, Production Build und Diff-Prüfung erfolgreich; visuelle Browserabnahme ausstehend.

## Folgekorrektur: Kopfzeile

Nach Benutzerfeedback enger an Original `07` angeglichen: dezente Pfadnavigation, direkt im Titel bearbeitbarer Planname, kleiner Planwechsel über zugängliches Material-Menü, Status/Belegung/fehlende Angebote neben dem Titel und Speicheraktionen rechts. Separate Namenszeile entfernt, gewonnene Höhe dem Editor zugeteilt. Auf schmalen Ansichten kontrollierter Umbruch. Erneut 128 Tests und Production Build erfolgreich; gerenderter Browservergleich weiterhin ausstehend.

Nach erneutem Feedback und korrigiertem Bild-Upload ersetzt durch den Aufbau aus **03/05**: echter Überschriftentext mit Dropdown statt dauerhaftem Titelfeld; Status/Belegung direkt daneben; rechts „Plan wechseln“, Aktionsmenü, Speichern und Verwerfen. Umbenennen ist im Aktionsmenü und öffnet nur bei Bedarf ein fokussiertes Titelfeld (Enter übernimmt, Escape verwirft diese Namensänderung). Kleine Unterzeile mit tatsächlicher Planversion statt erfundener Planbeschreibung. Ein bei der vorigen CSS-Bearbeitung versehentlich entfernter Block für Planliste, Badges und Karten wurde wiederhergestellt. 128 Tests, Production Build und Diff-Prüfung erneut erfolgreich. Die vollständige visuelle Gleichheit ist ohne gerenderten Browservergleich weiterhin nicht bestätigt.
