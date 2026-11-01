# Rezeptmerkmale und potenzielle Konflikte in der Menüplanung

Stand: 2026-11-01. Abschlussbedarf von UX-V2. Die begrenzte API-Erweiterung wurde vom Nutzer ausdrücklich freigegeben und implementiert; visuelle UI-Abnahme steht aus.

## Ziel

Vor dem Anlegen einer Alternative soll erkennbar sein, welche Eigenschaften des Standardrezepts eine Anpassung erforderlich machen könnten. Ernährungskennzeichnungen allein genügen dafür nicht. Beispiel: Ein Rezept enthält Milch; damit besteht potenzieller Anpassungsbedarf bei einer entsprechenden Anforderung. Ob eine konkrete Person/Gruppe damit nicht versorgt werden kann, ist eine andere Aussage.

Die Karten sollen enthalten:

- kompakte, beschriftete Merkmals-/Hinweischips statt unbeschrifteter allgemeiner Konfliktsymbole;
- kurze Erklärung und betroffene Zutaten/Unterrezepte über eine gezielte Detailansicht;
- erkennbare Datenlücken statt einer vermeintlich grünen Freigabe;
- dieselben Angaben in der Rezeptauswahl, damit Alternativen bewusst ausgewählt werden können.

Illustrative Texte, nur bei belegten Daten: „Milch (G) enthalten“, „Getreide (A): Weizen“, „Nicht vegan: enthält Käse“, „Laktose: mengenabhängig“, „Allergenangaben unvollständig“. „Laktosefrei“ oder „für alle geeignet“ darf daraus nicht automatisch abgeleitet werden.

## Zwei Ebenen

1. **Rezeptmerkmale/potenzieller Anpassungsbedarf:** Zu einer bestimmten unveränderlichen Rezeptrevision und klar definierten Varianten. Aussagen über enthaltene Allergene, relevante Herkunftsmerkmale sowie bekannte/qualitative/unbekannte Stoffdaten. Ohne Teilnehmerkontext keine Behauptung eines konkreten Teilnehmerkonflikts.
2. **Konkreter Konflikt:** Bewertung gegen Anforderungen im definierten Kocheinheit-/Mahlzeitenkontext. Erfordert unter anderem wirksame Anwesenheit, Filter, Bedarf, Varianten-/Ersatzwahl und individuelle Grenzwerte. Darf nur im berechtigten Kontext gezeigt werden. Personen-/Anforderungszahlen nicht aus mehreren sich überschneidenden Zuständen addieren.

Ein veröffentlichtes Rezept ist nicht allein wegen seiner Veröffentlichung vollständig bewertet. Ein potenzieller Hinweis ist weder eine Verifikation noch ein ungelöster operativer Konflikt: Der operative Zustand kann bereits eine passende Variante verwenden.

## Bestand und technische Grenze

`MealPlanningRecipeOption`/Angular `RecipeOption` enthalten nur Identität, Name, Revisionsnummer, Portionenbasis und vorgeschlagene Rolle. Keine Merkmals-/Konfliktzusammenfassung und kein veröffentlichter Inhaltsvertrag für diese Oberfläche.

`SupplySuitabilityEvaluator` in `Catering.Application/MealPlanning/SupplySolutions.cs` verwendet bereits veröffentlichte Revisionen, berechnet den resultierenden Zutatenstand einschließlich Unterrezepten und bewertet diesen gegen `RequirementSignature`. Die Mengenberechnung berücksichtigt den gesamten Batch; fixe/stufenweise Zutaten dürfen für mengenabhängige Aussagen nicht pauschal auf eine Testportion umgerechnet werden. Vorhandene `MealPlanningProblem`-Codes unterscheiden Eignungs-, Mengen-, Daten- und Entscheidungsprobleme.

`CookingUnitMeal.supply.problems` ist deshalb kein pauschaler Ersatz für Rezeptmerkmale: anderer Kontext, eventuell andere Revision/Variante und gespeicherter möglicherweise veralteter Teilstand. Eine Übertragung per Rezeptname oder bloßem Vorhandensein eines Problems wäre falsch.

## Begrenzte vorgeschlagene API-Erweiterung

Read-only Projektion für zugängliche veröffentlichte Revisionen. Konkrete Endpointform erst bei Implementierung festlegen; keine bestehenden Vertragsfelder eigenmächtig umdeuten.

Mindestens:

- eindeutige Revisionsreferenz und ausdrücklich definierte Bewertungsbasis (Standardzustand/konkrete Auswahl);
- fachlich aufgelöste Merkmale mit stabiler Referenz, lesbarem Namen und Status wie enthalten / nicht enthalten / unbekannt / mengenabhängig;
- nachvollziehbare Begründung und betroffene Zutaten-/Unterrezeptpositionen;
- Datenvollständigkeit getrennt von Eignung;
- Herkunft und Aktualität der Ableitung, soweit sie nicht allein durch die feste Revision bestimmt sind.

Keine medizinischen Freigaben, keine Namensheuristiken und keine neu erfundenen Eignungsregeln. Bestehende Resolver/Evaluatoren wiederverwenden, nachdem die passende kontextfreie Merkmalsprojektion fachlich abgegrenzt wurde.

Konkrete Teilnehmerkonflikte wären eine zusätzliche, kontextgebundene Projektion mit bestehenden Health-/Planungsrechten. Diese ist nicht Voraussetzung, um zunächst belegte Rezeptmerkmale auf Karten zu zeigen. Keine Teilnehmernamen oder individuellen Grenzwerte in allgemeine Rezeptauswahldaten aufnehmen.

## Darstellung und Alternativen

- Enthaltene relevante Merkmale als Chips mit Symbol **und** Text, z. B. „Milch (G)“; nicht ausschließlich über Farbe vermitteln.
- Auf Klick/Tastaturaktivierung begründete Detailansicht mit Zutaten und Datenlücken.
- Unbekannte Merkmale als unbekannt kennzeichnen; fehlende Hinweise allein bedeuten keine Konfliktfreiheit.
- Alternative zeigt ihre eigenen Merkmale in gleicher Weise. „Löst den Konflikt“ nur anzeigen, wenn derselbe konkrete Anforderungskontext tatsächlich bewertet wurde.
- Reine Rezeptmerkmalsanzeige ändert keine Menüplanversion und erzeugt/verifiziert keine operative Versorgungslösung.

## Abnahme

1. Ein belegtes Milch-/Weizenmerkmal erscheint auch vor Anlage operativer Teilstände mit nachvollziehbarer Herkunft.
2. Enthaltene Unterrezeptzutaten werden berücksichtigt; ausgewählte Ersatz-/Variantenbasis ist eindeutig.
3. Unbekannte Daten erscheinen nicht als frei von einem Stoff/Allergen.
4. Mengenabhängige Angaben erhalten ohne passenden Portions-/Grenzwertkontext keine pauschale Freigabe.
5. Änderungen am Menüplanentwurf verwenden die tatsächlich gewählte Revision; keine Vermischung mit alten Teilständen.
6. Eine Alternative wird nicht allein wegen ihres Namens oder ihrer Zuordnung als geeigneter Ersatz markiert.
7. Autorisierung, Cloud-/lokaler Betrieb, Freeze und explizite operative Berechnung bleiben erhalten.

## Implementierter Vertrag

Nach ausdrücklicher Freigabe umgesetzt:

`GET /api/camps/{campId}/meal-planning/recipes/{revisionId}/hints`

- Cookie-Anmeldung und bestehendes `Camp.View` erforderlich. Fremde/unveröffentlichte/nicht portionenbasierte Revisionen liefern keine Inhalte. Kein Health-Recht nötig, da keinerlei Teilnehmerdaten konsumiert oder ausgegeben werden.
- `recipeRevisionId`, `basis: PublishedStandard`, `evaluatedAtUtc`, `hasUnknownData`, `hints`.
- Jeder Hinweis: `kind` (Allergen/Diet/Substance), Referenz/Code/Name, `state`, Erklärung und Zutatenbezug mit unveränderlicher Zutatenrevision und Auftretenspfad.
- Zustände: Contains, NotContained, Compatible, Excluded, Unknown, QuantityDependent. Nicht enthaltene Angaben werden nicht als pauschale Frei-von-Chips ausgegeben.
- Allergene und Ernährungsformen nutzen `SupplySuitabilityEvaluator`; Stoffdaten den bestehenden `IngredientSubstanceProfile`-Resolver. Die Ableitung verwendet die Standardreferenzmenge und feste Unterrezeptrevisionen ohne gewählte Ersatz-/Variantenentscheidungen. Es werden keine individuellen Grenzwerte verwendet und keine operativen Ergebnisse gespeichert.
- Mengenabhängige Zustände setzen geprüfte quantitative Angaben sowie passende Einheiten voraus. Bekannte Präsenz bleibt bei zusätzlichen Datenlücken erhalten; `hasUnknownData` macht die Unvollständigkeit zusätzlich sichtbar.
- Request-bezogene Memoisierung der veröffentlichten Referenzen, keine persistenten Merkmalssnapshots und `Cache-Control: no-store`. Ernährungsregeln können sich ändern; der angezeigte Zeitpunkt begrenzt den Stand. „Erneut prüfen“ liest neu.
- UI: Chips auf Standard-/Alternativkarten und in der Rezeptvorschau; vollständige Erklärung mit Zutaten über Dialog. Lade-, Fehler- und Revisionswechselzustände zeigen keine alten oder vermeintlich sicheren Ergebnisse.
- Detailfilter nach Benutzerfeedback: ausschließlich enthaltene, mengenabhängige oder unbekannte Stoff-/Allergenangaben sowie ausgeschlossene Ernährungsformen. `NotContained` und `Compatible` werden dort ausgeblendet; leere Kategorien entfallen. Passende Ernährungskennzeichnungen auf den Karten bleiben bestehen. API-Daten unverändert.
- Sind ausschließlich positive oder nicht enthaltene Angaben vorhanden, gibt es keinen Popup-Auslöser mehr. Positive Ernährungskennzeichnungen sind passive Chips. Öffnen ist zusätzlich im Handler auf relevante Hinweise, unbekannte Angaben oder Ladefehler begrenzt; ein leerer Merkmalsdialog wird nicht angeboten.

Keine neue DB-Migration, keine Änderung an Berechnung, Verifikation oder Paketformat. Die lokale API wurde mit der vorhandenen SQLite-Testdatenbank auf localhost:5180 neu gestartet.

Validierung: 312 Catering-Tests, fünf HTTP-Zugriffstests, 131 Frontend-Tests erfolgreich; API- und Frontend-Build erfolgreich. Echter HTTP-Test mit dem dokumentierten Testkonto und drei vorhandenen Revisionen erfolgreich. Browser-Connector weiterhin nicht verfügbar; keine neue Screenshot-Abnahme behauptet.

## Farbcode nach verfügbaren Zutatenanpassungen

Nach Nutzeranforderung ergänzt: jedes Hinweisergebnis besitzt `adjustment` mit Available / Unavailable / Unknown / NotNeeded.

- **Gelb / Available:** Bestehender `SupplySolutionResolver` findet mindestens eine vollständige direkte Lösung durch aktive Zutatenvarianten oder explizite Ersatzentscheidungen. Mehrere vollständige passende Möglichkeiten bleiben eine Entscheidung, zeigen aber die Abdeckbarkeit. Es wird keine davon angewendet.
- **Rot / Unavailable:** Für die einzelne bekannte Anforderung wurde keine vollständige direkte Zutatenlösung gefunden. Reine Teillösungen reichen nicht. Menüplan-Rezeptalternativen werden für diese Farbentscheidung bewusst nicht als Zutatenlösung gezählt.
- **Neutral / Unknown:** Daten unklar, erforderlicher individueller Mengen-/Grenzwertkontext fehlt oder direkte Lösungssuche nicht abschließend abgeschlossen. Kein Rot aus bloßem Nichtwissen. Mengenabhängige Stoffangaben bekommen ohne passenden Kontext keine erfundene Schwelle.
- **NotNeeded:** Standardangaben erfordern für dieses einzelne Merkmal keine Anpassung; positive Chips bleiben passiv.

Die Prüfung gilt je Einzelhinweis und für die hinterlegte Referenzmenge. Gemeinsame Erfüllbarkeit mehrerer Anforderungen, tatsächliche Bedarfsmenge und Verifikation bleiben operative Prüfungen. Keine globale Freigabe aufgrund mehrerer gelber Einzelhinweise.

Die rein informative Lösungssuche besitzt ein gemeinsames dreisekündiges Zeitbudget pro Projektion und respektiert Request-Abbruch. Nicht abschließend gesuchte Ergebnisse bleiben Unknown. Variantenauflösungen werden requestbezogen memoisiert; der bestehende Resolver prüft Abbruch zusätzlich während der Kandidatensuche.

In Karten und Details erscheinen Farbe und Text („Variante möglich“ / „nicht abgedeckt“); Popup mit Legende und Erklärung. Datenlücken bleiben neutral. Prüfung: 316 Catering-Tests und 132 Frontend-Tests erfolgreich, API-/Frontend-Build erfolgreich. Laufende lokale API aktualisiert. Echter HTTP-Test an TEST 03/04/05 bestätigt Milch = Available und die nicht aufgelöste pflanzliche Herkunftsregel = Unavailable. Keine neue Browserabnahme.

Folgekorrektur zur Kompaktheit: Kartenchips zeigen nur den Merkmalsnamen, beispielsweise „Milch (G)“, „Vegan“ oder „Laktose“. Sichtbare Zusätze „enthalten“, „nicht passend“, „Variante möglich“ und „nicht abgedeckt“ entfallen dort. Farbzuordnung bleibt erhalten; Tooltip, zugängliche Beschriftung und Popup enthalten weiterhin die vollständige Status-/Abdeckungserklärung.

Detailpopup weiter verdichtet: eine gemeinsame Farblegende und eine kurze Kontextzeile, pro Hinweis Name mit kurzem Abdeckungsstatus sowie Zutatenchips. Wiederholte allgemeine Absätze und doppelte Zustands-/Abdeckungstexte entfernt. Vollständige Erklärung nur bei Bedarf unter „Begründung“ aufklappbar. Zusätzlicher globaler Hinweis auf ungeprüfte Daten nur, wenn dies nicht bereits in unbekannten Einzelhinweisen sichtbar ist. Ableitungszeitpunkt kompakt im Fußbereich. API und fachliche Bewertung unverändert.

## Farbcode nach verfügbaren Zutatenanpassungen

Nach Nutzeranforderung ergänzt: jedes Hinweisergebnis besitzt `adjustment` mit Available / Unavailable / Unknown / NotNeeded.

- **Gelb / Available:** Bestehender `SupplySolutionResolver` findet mindestens eine vollständige direkte Lösung durch aktive Zutatenvarianten oder explizite Ersatzentscheidungen. Mehrere vollständige passende Möglichkeiten bleiben eine Entscheidung, zeigen aber die Abdeckbarkeit. Es wird keine davon angewendet.
- **Rot / Unavailable:** Für die einzelne bekannte Anforderung wurde keine vollständige direkte Zutatenlösung gefunden. Reine Teillösungen reichen nicht. Menüplan-Rezeptalternativen werden für diese Farbentscheidung bewusst nicht als Zutatenlösung gezählt.
- **Neutral / Unknown:** Daten unklar, erforderlicher individueller Mengen-/Grenzwertkontext fehlt oder direkte Lösungssuche nicht abschließend abgeschlossen. Kein Rot aus bloßem Nichtwissen. Mengenabhängige Stoffangaben bekommen ohne passenden Kontext keine erfundene Schwelle.
- **NotNeeded:** Standardangaben erfordern für dieses einzelne Merkmal keine Anpassung; positive Chips bleiben passiv.

Die Prüfung gilt je Einzelhinweis und für die hinterlegte Referenzmenge. Gemeinsame Erfüllbarkeit mehrerer Anforderungen, tatsächliche Bedarfsmenge und Verifikation bleiben operative Prüfungen. Keine globale Freigabe aufgrund mehrerer gelber Einzelhinweise.

Die rein informative Lösungssuche besitzt ein gemeinsames dreisekündiges Zeitbudget pro Projektion und respektiert Request-Abbruch. Nicht abschließend gesuchte Ergebnisse bleiben Unknown. Variantenauflösungen werden requestbezogen memoisiert; der bestehende Resolver prüft Abbruch zusätzlich während der Kandidatensuche.

In Karten und Details erscheinen Farbe und Text („Variante möglich“ / „nicht abgedeckt“); Popup mit Legende und Erklärung. Datenlücken bleiben neutral. Prüfung: 316 Catering-Tests und 132 Frontend-Tests erfolgreich, API-/Frontend-Build erfolgreich. Laufende lokale API aktualisiert. Echter HTTP-Test an TEST 03/04/05 bestätigt Milch = Available und die nicht aufgelöste pflanzliche Herkunftsregel = Unavailable. Keine neue Browserabnahme.
