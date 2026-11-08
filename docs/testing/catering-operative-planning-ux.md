# Operative Versorgungsplanung – UI-Abnahme V4

Stand: 2026-11-01. Einstieg: Verpflegung → Kocheinheiten → Einheit auswählen → Versorgung öffnen. Alternativ eine konkrete Mahlzeit über Übersicht/Konflikte öffnen.

## Desktop und schmale Ansicht

1. Bei 1920×1080 mit Mockups `08`, `09`, `10` vergleichen: links ausschließlich die Mahlzeiten der gewählten Einheit, nach Datum gruppiert; rechts kompakter Mahlzeitenkopf, geplante Komponenten/Rezeptkarten, Bedarfsstreifen, Versorgungslösungen und Verifikation. Keine zweite Einheitenliste und keine ständig offenen Eingabefelder.
2. Mahlzeitenwechsel, vorherige/nächste Mahlzeit und Rückweg zur gleichen Kocheinheit prüfen. Inaktive und außerhalb der Lagerzeit liegende Mahlzeiten erscheinen nicht. Zusätzliche aktive Mahlzeiten gehören zum jeweiligen Datum und berücksichtigen die gespeicherte Reihenfolge.
3. Auf schmaler Breite stehen Navigation und Details untereinander. Lange Namen, Anforderungsbeschreibungen und Rezeptnotizen dürfen Aktionen nicht überdecken. Rezeptbilder sind bewusst Platzhalter, solange kein Bildvertrag besteht.

## Versorgung und Status

Kompakte Rezeptkarten: Bei zusammengefassten Lösungen nur Gesamtportionen anzeigen; keine sichtbare Liste aller zugeordneten Anforderungsgruppen. Die zugrunde liegenden Allokationen müssen für Berechnung, Speicherung und Bearbeitung erhalten bleiben. Die allgemeine Gruppeninfo steht unter Kocheinheit → Verpflegung; Konflikte behalten die zum Lösen benötigten Anforderungen.

„Alles neu berechnen“ im Kopf der gewählten Kocheinheit prüfen: Veraltete und noch nicht berechnete aktive Mahlzeiten innerhalb der Lagerzeit werden anhand einer frisch geladenen Übersicht nacheinander berechnet. Aktuelle Mahlzeiten, frisch unvollständige Berechnungen und andere Kocheinheiten bleiben unberührt. Offene Entwürfe blockieren den Start. Fortschritt, gesperrte Navigation und die abschließende Aktualisierung prüfen. Einzelne Berechnungsfehler dürfen andere Mahlzeiten nicht blockieren; verlorener Zugriff, Freeze oder ein Verbindungsabbruch stoppen weitere Anfragen. Bereits erfolgreiche Berechnungen bleiben bei Fehlern erhalten. Kein automatischer Wiederholungsversuch und keine automatische Verifikation.

Ask-Nachtrag: Identische Ergebnisse von gemeinsam/getrennt erzeugen keine Richtungsabfrage, auch bei identischen offenen Rezeptoptionen oder identischen verbleibenden Konflikten. Nach einer bestätigten Richtungswahl verschwindet deren Karte vollständig aus der normalen Liste. Ändern ausschließlich über „Weitere Anpassungen → Versorgungsart ändern“. Der Rezeptreset „Zurück zum Plan“ erhält die Präferenz. Schließen/Escape muss offene Änderungen weiterhin schützen; nach Bestätigung automatisch neu berechnen.

Allgemeine Präferenzabweichung: „Weitere Anpassungen → Versorgungspräferenz ändern“ steht auch bei Gemeinsam und Getrennt zur Verfügung und bietet alle drei Werte. Die Wahl betrifft nur diese Mahlzeit/Komponente; die Grundeinstellung der Kocheinheit bleibt erhalten. Wechsel zu Fragen muss bei unterschiedlichen Ergebnissen wieder die unbestätigte Richtungsabfrage erzeugen. Bei identischen Ergebnissen entfällt sie weiterhin. „Zurück zum Plan“ setzt nur eigene Rezeptlösungen zurück und erhält diese Präferenz; Neuladen muss die Abweichung erhalten.

Die Zieldefinition steht in `docs/domain/cooking-together-preference-ux.md`, der inzwischen erweiterte API-Vertrag in `docs/architecture/cooking-together-api.md`. Bei „Getrennt“ sind Gruppenauswahlen unabhängig; bei „Gemeinsam“ möglichst zusammenfassen. „Fragen“ liefert beide Szenarien ohne automatische Vorauswahl. Geprüfte Varianten kommen direkt aus den API-Auswahlgruppen. Nach dem API-Update alte Berechnungen zuerst neu berechnen; bestätigte Richtungen müssen nach Neuladen erhalten bleiben.

4. Noch nicht berechnete Mahlzeit: keine grüne Erfolgsmeldung oder vermeintliche Verifikation. Neu berechnen öffnet keinen Dialog und verifiziert nicht automatisch.
5. Eindeutig automatisch gelöste Versorgung: Rezept, Portionen, menschlich lesbare Anforderungen und gegebenenfalls Zutatenanpassungen direkt sichtbar. Zutaten/Mengen lassen sich gezielt aufklappen; sie sind als letzter Berechnungsstand gekennzeichnet.
6. Offene Konflikte: gelber Hinweis mit konkretem Grund, gezielter Einstieg zur Bearbeitung. Diagnosen für dieselben Anforderungen werden in einer Karte zusammengefasst, Ursachen sind aufklappbar. Fehlende Portionen aus berechnetem Ziel und zugeordneten Portionen stehen im Vordergrund; Hauptspeise/Beilage werden dabei nicht doppelt gezählt. Ohne belastbaren Zielwert wird keine Zahl erfunden. Beim gezielten Bearbeiten sind betroffene Entscheidungen geöffnet, andere zunächst zugeklappt. „Gemeinsam oder getrennt?“ bietet explizite Optionen. Umschalten ersetzt die bisherige Auswahl für die betroffenen Gruppen, ohne andere Entscheidungen zu entfernen. Speichern erhält die manuelle Herkunft; anschließend ausdrücklich neu berechnen.
7. Schätzung, reale Teilnehmerbasis und Schätzung als Fallback unterscheiden. Portionen mehrerer Komponenten dürfen nicht zu Teilnehmerzahlen aufsummiert werden. Veraltete Berechnung und veraltete Verifikation separat prüfen.
8. Katalogzugriff ohne Health-Leserecht: lesbare generische Anforderungsbeschreibung statt interner Schlüssel oder erfundener Gruppennummern. Mit Zugriff werden gespeicherte Ernährungsformen, Allergene und Stoffe aufgelöst. Keine Teilnehmerprofile werden dafür geladen.

## Anpassungen, Fehler und Rechte

Bei mehreren bestätigten Angebotsalternativen: ausschließlich die passenden veröffentlichten Alternativrezepte als Einzelauswahl im Versorgungsfall anbieten. Eine Auswahl ersetzt die vorherige für dieselbe Komponente und Anforderung; andere Zuordnungen bleiben erhalten. Die Auswahl bleibt zuerst ein lokaler Entwurf. Erst nach Klick auf „Auswahl bestätigen“ wird gespeichert, danach automatisch neu berechnet und die Übersicht aktualisiert. Keine Berechnung bei fehlgeschlagenem Speichern. Scheitert erst die Berechnung, muss der Hinweis deutlich machen, dass die Auswahl bereits gespeichert ist; kein automatischer Wiederholungsversuch. Teilweise gelöste Vorschläge dürfen nicht als bestätigte passende Alternative erscheinen. Bei veraltetem Stand, Freeze oder fehlendem Bearbeitungsrecht ist die Auswahl gesperrt. Keine automatische Verifikation. Der allgemeine manuelle Editor behält seine explizite Speicherung und Neuberechnung.

Gruppenübergreifende Auswahl prüfen: Wenn dieselbe Alternative mehrere Bedarfsgruppen abdecken kann, wird eine gemeinsame Entscheidung mit der Schnittmenge geeigneter Rezepte angeboten. Nur ohne gemeinsame Option aufteilen. Bei „Always Together“ haben vom Backend gemeinsam geprüfte Alternativen Vorrang: Die Auswahl umfasst auch bereits durch das Standardrezept versorgte Gruppen derselben Komponente. Eine Wahl ersetzt deren bisherigen Standardanteil, statt zusätzlich Portionen anzulegen. Identische manuelle Rezeptentscheidungen werden beim Laden angezeigt und vor dem Speichern zusammengefasst: gleiche Revision, Komponente, Zutatenentscheidungen und Herkunft; Portionen sowie Gruppenzuordnungen bleiben erhalten. Unterschiedliche Anpassungen, Komponenten oder Herkunft dürfen nicht versehentlich zusammengeführt werden. Bereits gespeicherte Einzelentscheidungen werden erst beim nächsten ausdrücklichen Speichern vereinigt.

Bei einer gemeinsamen Alternative darf der bisherige Standardanteil (z. B. sieben Portionen) nicht als zusätzliche aktive Lösung über der Auswahl stehen. Stattdessen kurzer Hinweis: Standardrezept deckt nicht alle Anforderungen ab; eine Alternative kann die gesamte Komponente übernehmen. Die Auswahl selbst zeigt die Gesamtportionen. Andere Komponenten und unabhängig weiterbestehende Versorgungen bleiben sichtbar. Nach Auswahl und bis zur Neuberechnung keine zweite Lösungskarte für dieselbe Entscheidung anzeigen.

9. „Weitere Anpassungen“ öffnen: Standardplan/individuell/keine Versorgung, Bedarfsüberschreibung, Komponentenziele und eigene Strukturzuordnung prüfen. Änderungen erst mit „Anpassungen speichern“ persistieren. Ein leerer eigener Strukturbereich wird abgewiesen; Eltern-/Kindauswahl wird ohne redundante Referenzen normalisiert.
10. Individuelle Rezepte hinzufügen/entfernen. „Standardplan wiederherstellen“ ändert zunächst den Entwurf; Speichern bleibt erforderlich. „Struktur zurücksetzen“ nutzt den vorhandenen separaten Reset und verlangt einen sauberen Entwurf.
11. Ungespeicherte Eingaben beim Mahlzeitenwechsel, Rückweg, Bereichswechsel, Browser-Unload und Dialogschließen/Escape prüfen: Ablehnen erhält sie; bestätigtes Verwerfen verwirft sie. Neuberechnung blockiert bei offenen Einstellungen, Versorgungsentscheidungen oder Verifikationsbegründung. Laufende Vorgänge sperren konkurrierende Schreibaktionen und Navigation.
12. Speichern/Neuberechnen mit Serverfehler und Versionskonflikt prüfen: kein automatischer Wiederholungsversuch; Entwurf bleibt erhalten. Nach erfolgreicher Mutation mit anschließendem Ladefehler gespeicherten Stand prüfen. Eine aktualisierte Übersicht darf offene Versorgungsentscheidungen nicht überschreiben; Speichern verwendet weiterhin deren ursprüngliche Supply-Version.
13. Verifikation bewusst separat auslösen: ohne Abweichung nur gemäß aktuellem Problemstatus; mit Abweichung ausdrückliche Begründung. Automatische Lösung ist keine manuelle Freigabe. Verifikationsrecht wird weiterhin vom Backend unabhängig vom Bearbeitungsrecht geprüft. Eingefrorenes Lager sperrt sämtliche Schreibaktionen einschließlich Verifikation.
14. Dialoge mit Tab, Shift+Tab und Escape prüfen; nach Schließen Fokus am Auslöser. Native Auswahlfelder sollen im Dialog sichtbar und bedienbar sein.

## Nachweisgrenze

Komponenten-/Regressionstests und Produktionsbuild werden im V4-Ergebnisbericht dokumentiert. Der Edge-Browserhost war bei der Umsetzung nicht erreichbar. Die obige visuelle und interaktive Browserabnahme ist daher noch auszuführen; es wurden keine Nachher-Screenshots erzeugt.


### Zusammengefasste Konfliktkarte (02.11.2026)
- Abendessen 02.07., TEST All - AlwaysTogether: eine Karte mit 3 nicht abgedeckten Portionen; darunter 2 Portionen Milch und 1 Portion pflanzliche Ernährungsform samt Ursachen.
- Mehrere betroffene Komponenten derselben Anforderungsgruppe erhöhen die Portionssumme nicht mehrfach. Unbekannte Bedarfsmengen erhalten keine erfundene Zahl.
- Eine gemeinsame Aktion „Versorgung klären“ öffnet den bestehenden Editor; die manuelle Bearbeitung wurde nicht umgebaut.
- In-App-Browser: Desktop 1920×1080 sowie kleinere Desktop-, Tablet- und schmale Ansicht visuell geprüft. Karte ohne horizontales Überlaufen, Ursachen umbrechend; auf niedrigen/schmalen Ansichten bleibt vertikales Scrollen erforderlich.
- 229 Frontend-Tests erfolgreich, darunter Mehrfachkomponenten, unbekannter Bedarf und schreibgeschützte Ansicht.


### Getrennte Bearbeitung und Planbezug (02.11.2026)
1. Konfliktkarte öffnet „Versorgung klären“. Bestehende Lösungen sind nur lesbar; offenen Bedarf auswählen, Rezept ergänzen, Portionen/Varianten bearbeiten und bestätigen. Bestehende Abdeckungen dürfen weder verschwinden noch geändert werden.
2. Mehr Portionen als offen, ungültige Zuordnungen und veraltete Berechnungen dürfen im Konflikteditor nicht gespeichert werden. Bei Konflikten ohne bekannte Unterdeckung auf Datenprüfung oder eigene Lösung verweisen.
3. „Versorgungsentscheidungen bearbeiten“ befindet sich ausschließlich unter „Weitere Anpassungen“. Teil-/Gesamtlösung auswählen. Bei Teil: unveränderte Karten folgen dem Plan. Bei Gesamt: keine automatische Ergänzung aus dem Plan.
4. Plan nach einer eigenen Gesamtlösung ändern: eigene Versorgung bleibt bestehen. „Zurück zum Plan“ entfernt eigene Entscheidungen und berechnet neu. Bei Teilversorgung einzelne eigene Karte wieder dem Plan folgen lassen.
5. Schließen/Abbrechen verwirft erst nach bestätigter Rückfrage; Freeze, fehlende Rechte, Versionskonflikte und fehlgeschlagenes Speichern erhalten ihre Sperren bzw. Entwürfe.
6. In-App-Zugriff nach Neuinitialisierung wiederhergestellt. Konflikteditor mit ungespeicherter Rezept-Ergänzung bei 1920×1080, 1366×768, 768×1024 und 390×844 visuell geprüft: bestehende sechs Portionen nur lesbar, neue zwei Portionen separat editierbar, kein horizontales Überlaufen. Bei geringer Höhe scrollt der Dialog intern. Eigener Editor unter Weitere Anpassungen auf Desktop und schmal geprüft; beide Umfangsoptionen sichtbar. Testergänzungen verworfen, keine eigene Versorgung für die UI-Prüfung gespeichert. Nur Abendessen 02.07. der Testkocheinheit neu berechnet.

Prüfnachweis: 235 Frontendtests erfolgreich, anschließend nach visuellen Korrekturen 36 Dialogtests erneut erfolgreich; 335 Cateringtests, 19 Pakettests und 11 HTTP-Prüfungen erfolgreich. Frontend-Produktionsbuild und API-Releasebuild erfolgreich. API auf localhost:5180 aktualisiert; Health-Check erfolgreich.


### Rückkehr zum Plan erhält die Gemeinsamkeitspräferenz
- Mit gespeicherter Mahlzeitenpräferenz und eigener Rezeptlösung „Zurück zum Plan“ ausführen: Rezepte zurückgesetzt, gleiche scenarioSelections gespeichert, automatische Neuberechnung mit dieser Präferenz.
- Gemeinsam/getrennt bestätigen: nur die Präferenz plus tatsächlich ausgewählte Rezeptoptionen speichern; deterministische Vorschau-Rezepte bleiben automatisch.
- Eine reine Präferenzantwort wird als „Folgt dem Plan“ angezeigt. Geänderte Rezepte, Portionen oder Zutaten sowie eigene Gesamtlösungen bleiben als eigene Lösungen erkennbar.
- Versionskonflikt beim Zurücksetzen: gespeicherte Präferenz bleibt auch im lokalen Entwurf erhalten.
- Prüfung: 238 Frontendtests erfolgreich.


### Verschachtelte Entscheidungen: TEST Vergleich Ask, Abendessen 02.07.
- Zurück zum Plan muss die gewählte Gemeinsamkeitspräferenz und bestätigte Rezeptoptionen innerhalb des Plans erhalten; nur tatsächlich eigene Abweichungen entfernen.
- Identisches Rezept mit/ohne Zutatenanpassung für denselben geeigneten Bedarf: unveränderte Lösung automatisch bevorzugen. Keine Zusatzabfrage nur wegen unnötiger Anpassung.
- Andere Rezeptalternativen und mehrere notwendige Varianten weiterhin ausdrücklich auswählen lassen.
- Automatisierte Prüfung: 240 Frontendtests und 336 Cateringtests erfolgreich, einschließlich verschachtelter Auswahl, zusammengefasster Anforderungsgruppen, echter eigener Abweichungen und unveränderter Teilabdeckung neben einem unlösbaren Konflikt.


Browsernachweis: TEST Vergleich Ask, 02.07.2027 Abendessen nach gezielter Neuberechnung mit Planner-Version 6: bisherige doppelte Rezeptabfrage entfällt, zwei Portionen automatisch mit unverändertem TEST 07 Partielle Loesung versorgt, zwei Portionen mit tatsächlichem Allergenkonflikt weiterhin offen. Gespeicherte Präferenz Gemeinsam unverändert. Prüfung in separatem Tab; offener ungespeicherter Nutzerentwurf im ursprünglichen Tab nicht verworfen oder gespeichert. API- und Frontendbuild erfolgreich.


### Status und Verifikation in der Mahlzeiten-Kopfzeile (03.11.2026)
- Pfeile für vorherige/nächste Mahlzeit entfernt; die linke Mahlzeitenliste dient zur Navigation.
- Berechnungsstand, Versorgung und Verifikation stehen unmittelbar unter dem Mahlzeitentitel, vor dem geplanten Angebot. Offene Konflikte und veraltete Verifikationen sind erkennbar; eine gelöste Versorgung allein markiert die Verifikation nicht als erledigt.
- „Neu berechnen“, „Versorgung prüfen“ und „Weitere Anpassungen“ stehen im Kopfbereich. Die doppelte Verifikationsleiste unter den Versorgungslösungen entfällt.
- Bestehender Verifikationsdialog und getrennte Berechtigung bleiben erhalten. Bei fehlender Berechnung, Freeze und laufenden Aktionen gelten die bisherigen Sperren. Browserprüfung öffnete und schloss den Dialog, ohne eine Verifikation zu speichern.
- Desktop 1920×1080, Laptop 1366×768 und schmale Ansicht 390×844 geprüft; kein horizontales Überlaufen. Aktionen umbrechen auf Laptopbreite, Statusangaben stehen in schmaler Ansicht untereinander.
- 241 Frontendtests erfolgreich. Der erste Teststart scheiterte vor Testausführung an einem temporären Fork-Starttimeout; die vollständige Wiederholung war erfolgreich. Keine API-Änderung für diese Anpassung.


## Visuelle Angleichung an Mockup 09 (03.11.2026)

- Geplantes Angebot als hellblau gerahmter Bereich mit separaten Komponentenkarten; Standardrezepte sind direkt sichtbar, Alternativen je Komponente standardmäßig eingeklappt.
- Alternativen lassen sich per Klick oder Tastatur öffnen und schließen. Dies ändert keine Versorgungsentscheidung und löst keine Berechnung aus.
- Status der Mahlzeit oberhalb des Angebots: Planung, Versorgung, Berechnung und Verifikation in einer gemeinsamen Leiste mit Symbolen und Trennlinien. Weitere Anpassungen sind im Statuskopf erreichbar.
- Browserprüfung mit vorhandenen Testdaten bei 1920×1080, 1366×768 und 390×844: Standardansicht und aufgeklappte Alternativen geprüft, kein horizontaler Seitenüberlauf. Auf schmalen Bildschirmen stehen die Statusfelder untereinander.
- Validierung: 242 Frontendtests erfolgreich; Produktionsbuild erfolgreich; git diff --check ohne Fehler. Keine API-Änderungen.


## Vorschlagsdialog nach Mockup 11 (05.11.2026)

- GET-Vorschau und POST-Übernahme mit serverseitiger Eignungsprüfung integriert; kein Schreiben durch Auswahl, Sortierung oder Seitenwechsel.
- Gemeinsame Versorgung und Ergänzung offener Personen getrennt; Auswirkung rechts vor der Übernahme. Einzelne offene Anforderungsgruppen bei Getrennt zusätzlich auswählbar.
- Backendtests decken Eignung, Zutatenersatz, stabile Vorschlagsidentitäten, schreibfreie Vorschau, ungültige IDs, fehlende Katalogfreigabe, Versionskonflikte und erfolgreiche Übernahme samt Neuberechnung ab.
- Frontendtests decken Auswahl ohne Vorbelegung, Vorschau ohne Schreiben und ausdrückliche Übernahme sowie die Behandlung eines 409 ab.

- Ergebnis: 339 Cateringtests und 247 Frontendtests erfolgreich; nach den letzten Layout-/Kontextanpassungen nochmals 61 betroffene Frontendtests erfolgreich. API-Build ohne Warnungen/Fehler, Frontend-Produktionsbuild erfolgreich.
- Browser: TEST All - AlwaysTogether, 01.07.2027 Mittagessen, eine offene und neun bereits versorgte Personen. Geprüfte gemeinsame Vorschläge für zehn Personen und Ergänzungen für eine Person. Auswahl, Auswirkungen, Seitenwechsel und Abbrechen geprüft; keine Versorgungsdaten im Browser geändert. 1920x1080, 1366x768 und 390x844 ohne horizontalen Seitenüberlauf; Vorschlagsliste auf Desktop intern scrollbar, Fußaktionen sichtbar.


## Weitere Anpassungen nach Referenzen 12–15 (06.11.2026)

- 340 Cateringtests, 250 Frontendtests erfolgreich. Neuer SQLite-Test prüft gemeinsamen Save/Reset, Erhalt bestätigter Entscheidungen beim Rezeptreset, Versionskonflikte und Rollback einschließlich bereits gelöschter Zielwerte bei einem Fehler. Neue Frontendtests prüfen Entwurf/Abbrechen und All-Reset erst beim Speichern.
- API-Build ohne Warnungen/Fehler, Produktionsbuild erfolgreich. Browserprüfung bei 1920x1080, 1366x768 und 390x844. Desktop zeigt den Standarddialog vollständig, kleinere Ansichten verwenden internen Dialogscroll und sichtbare Fußaktionen. Kein horizontaler Seitenüberlauf.
- Vier Akkordeonbereiche, Bedarfs-/Strukturauswahlkarten und Präferenzansicht mit vorhandenen TEST-Daten geprüft. Präferenz lokal geändert und abgebrochen; erneutes Öffnen zeigt wieder die gespeicherte Gemeinsam-Präferenz. Keine Anpassungen an Testlagerdaten durch die Browserprüfung gespeichert.


## Fünf Abnahmekorrekturen (07.11.2026)

341 Cateringtests, 253 Frontendtests und 19 Pakettests erfolgreich. Neue Regressionen prüfen unverändertes Öffnen des eigenen Editors ohne Schreibzugriff, Neuberechnung nach Reset, gesperrte Komponentenwerte bei Automatik, erlaubte manuelle Dezimalwerte, zweistufige Präferenz und Speicherung/Erhalt des Mahlzeitenfilters.

Browserprüfung mit TEST All - AlwaysTogether: Wechsel zur Automatik deaktiviert Komponentenfelder und leert deren Werte. Manueller Bedarf lässt sich mit ArrowUp von 12 auf 13 erhöhen; 12,5 lässt sich eintippen. Verpflegungsfilter mit vier Optionen vorhanden. Präferenz Standard blendet die drei Optionen aus, Abweichung blendet sie wieder ein. Nur Entwürfe im temporären Browser-Tab geändert, keine Testlageranpassungen gespeichert.


## Individueller Editor nach Mockup 16 (07.11.2026)

- Referenz `16_eigene_versorgungsloesung.png` abgelegt und indexiert. Teil-/Gesamtlösung mit kompakten Rezeptzeilen, Komponentenauswahl, Personenzahl und Gruppen-/Zutatenbearbeitung umgesetzt. Bilder bleiben ohne vorhandene Daten Platzhalter; keine erfundenen Ernährungslabels.
- Korrektur 08.11.2026: Der Teilmodus bietet nur noch offene Versorgungsgruppen und deren verbleibende Personenzahl je Komponente an. Automatische Zuordnungen bleiben geschützt; gespeicherte manuelle Entscheidungen bleiben bearbeitbar. Der Gesamtmodus startet ohne automatische Rezeptzeilen; vorhandene manuelle Entscheidungen werden übernommen. Das Öffnen oder Umschalten schreibt keine Daten. Entwürfe der beiden Modi bleiben getrennt.
- Navigation Lösungen, Auswirkungen und Prüfung. Auswirkungen unterscheiden Ersetzen der gesamten Versorgung von betroffenen Zuordnungen im Teilmodus. Gruppenzuordnungen und Personenzahlen lassen sich unter der Zeile bearbeiten; Zutatenvarianten und Ersatz verwenden die vorhandene API.
- Seitenkarte prüft Zuordnungsabdeckung, fehlende/überzählige Personen und inkonsistente Summen im Entwurf. Mehrere Komponenten werden nicht als zusätzliche Personen summiert. Eignung ist weiterhin Ergebnis der serverseitigen Neuberechnung nach dem Speichern; aktuelle Zuordnung allein behauptet keine Ernährungsfreigabe. Alte Konflikte sind als letzter Berechnungsstand gekennzeichnet.
- Beim Speichern einer Teillösung werden Änderungen automatischer Zuordnungen und eine Überbelegung bereits versorgter Gruppen abgewiesen. Bereits versorgte Anteile derselben Gruppe bleiben beim Ergänzen erhalten. Unverändertes Öffnen persistiert keine eigenen Rezepte.
- 255 Frontendtests erfolgreich. Neue Tests für Teil-/Gesamtumfang, ausdrückliches Anpassen ohne Schreiben, Abdeckung über mehrere Komponenten und abweichende Zuordnungssummen. Browser mit vorhandenen TEST-Daten bei 1920x1080, 1366x768 und 390x844 geprüft: kein horizontaler Seitenüberlauf, schmale Tabellen intern scrollbar. Nur lokale Entwürfe im temporären Testtab bearbeitet, keine Versorgungsänderungen gespeichert.
- Bestehende Save-/Reset-/Neuberechnungsaufrufe verwendet, keine neuen API- oder Datenbankänderungen für diesen visuellen Umbau.

- Korrektur 08.11.2026: Manuelle Entscheidungen bleiben im Teilmodus bearbeitbar und entfernbar; ihre bisherigen Zuordnungen werden beim Ermitteln freier Kapazitäten nicht doppelt gezählt. Der Save-Endpunkt prüft Rezepte samt gewählten Zutatenvarianten gegen die tatsächlich zugeordneten Anforderungen. Ungeeignete oder unbekannte Anforderungszuordnungen werden mit `unsuitable_supply` ohne Speicherung abgewiesen; der lokale Entwurf bleibt erhalten.

## Einheitlicher Lösungseditor (08.11.2026, ersetzt die Teil-/Gesamtauswahl)

- „Lösung bearbeiten“ zeigt die vollständige aktuelle Versorgung, inklusive automatischer und gespeicherter manueller Rezepte. Alle Zeilen sind bearbeitbar und entfernbar; es gibt keine Umfangsauswahl oder geschützte Planzeilen mehr.
- Erst ein geänderter und gespeicherter Entwurf übernimmt diese Versorgung als manuelle Lösung. Bloßes Öffnen schreibt nichts. „Zurück zum Plan“ stellt die automatische Planung wieder her und erhält bestätigte Gemeinsamkeitsentscheidungen.
- Rezepte und Zuordnungen sind frei wählbar. Die neue schreibfreie POST-API `supply-preview` prüft die tatsächlichen Zutatenentscheidungen gegen die zugeordneten Anforderungen und liefert Konflikte je Rezept. Zugriffsschutz und Rezeptzugriff werden serverseitig geprüft.
- Der Editor zeigt die Vorschau automatisch nach Änderungen, verwirft veraltete Antworten und kennzeichnet fehlgeschlagene Prüfungen. Konflikte blockieren das Speichern nicht; nach dem Speichern wird neu berechnet. Die gezielte passende Rezeptsuche unter „Versorgung klären“ bleibt bestehen.
