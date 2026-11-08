# Rezeptvorschläge zur offenen Versorgung: API-Prüfung

Stand: 05.11.2026. Prüfung vor Umsetzung des neuen Dialogs. Referenz: [Mockup 11](mockups/catering/11_offene_versorgung_loesen.png).

## Ergebnis

Die fachliche Eignungsprüfung und Variantenauflösung existieren bereits. Der bei der Erstprüfung fehlende Vorschlagsaufruf und der Dialog nach Mockup 11 sind inzwischen implementiert. Die folgende Bestandsaufnahme beschreibt die Ausgangslage; der neue Vertrag steht am Ende.

## Vorhandene Bausteine

- `src/backend/ScoutCampPlanner.Catering.Application/MealPlanning/SupplySolutions.cs`: `SupplySuitabilityEvaluator.Evaluate(solution, requirements)` prüft eine konkrete Rezeptrevision samt Zutatenentscheidungen und Personenzahl gegen eine Anforderungssignatur. Allergene, Ernährungsregeln, Unverträglichkeitsgrenzwerte und unbekannte Daten werden berücksichtigt. `IsSuitable` gilt nur ohne Probleme. Die reale Ansatzmenge ist relevant, auch für fixe und stufenweise Zutatenmengen.
- `SupplySolutionResolver.cs`: `Resolve` und `ResolveTogether` bewerten eine bzw. mehrere Anforderungssignaturen. Explizite Zutatenvarianten und Ersatzregeln werden durchsucht. Der Resolver verwendet das Standardrezept und die übergebenen Angebotsalternativen, keinen globalen Rezeptkatalog. Bei bereits geeignetem Standard erfolgt ein früher Rücksprung; auch eindeutige Lösungen werden vorzeitig geliefert. Daher ist die Funktion kein vollständiger Such-/Rankingdienst. `Candidates` kann auch nur teilweise geeignete Varianten enthalten; für geeignete Vorschläge `SuitableCandidates` bzw. die erfolgreiche Auswahl beachten.
- `OperationalSupplyCalculator.cs`: berechnet Gemeinsam/Getrennt-Szenarien und bündelt Entscheidungen. Die API-Daten `supply.candidates`, `decisions` und `preferenceOptions` liefern bereits geprüfte Optionen innerhalb des geplanten Angebots. Sie sind keine vollständige Suche nach weiteren Rezepten.

## Vorhandene HTTP-Aufrufe und Grenzen

- `GET /api/camps/{campId}/meal-planning/recipes/{revisionId}/hints`: allgemeine Hinweise zu einer Rezeptrevision (`RecipePlanningHintsEndpoints.cs`, `RecipePlanningHintsProjector`). Ausdrücklich kontextfrei, keine Freigabe für konkrete Personen. Kein Ersatz für die gemeinsame Prüfung mehrerer Anforderungen und individueller Grenzwerte.
- `GET /api/camps/{campId}/meal-planning/supply-positions/{revisionId}`: editierbare Zutaten-/Ersatzoptionen, keine Liste geeigneter Rezepte (`MealSupplyEndpoints.cs`).
- Speichern einer Versorgung und anschließendes Neuberechnen prüft eine konkrete eigene Lösung. Das ist kein lesender Vorschauaufruf und darf nicht als Suche verwendet werden.
- Der bisherige Konflikteditor kann manuelle Zuordnungen bündeln. Seine Rezeptauswahl ist noch keine vorab auf Eignung gefilterte Vorschlagsliste wie im neuen Mockup.

## Empfohlener nächster Schritt

Vor dem visuellen Umbau einen lesenden Vorschlags-/Vorschauaufruf entwerfen, der die vorhandene Backendprüfung wiederverwendet. Camp, Kocheinheit, Mahlzeit und Komponente bestimmen serverseitig Kontext, Berechtigungen, wirksame Präferenz und Anforderungen. Keine Anforderungs-/Gesundheitsdaten unnötig im Browser nachbauen.

Die Antwort sollte zwei ausdrückliche Umfänge unterscheiden: gesamte Gruppe gemeinsam versorgen (ersetzt deren bestehende Zuordnungen dieser Komponente) und offene Personen ergänzen (erhält bestehende Lösungen). Bei Getrennt zuerst gebündelte offene Anforderungen anbieten, bei Bedarf einzelne Gruppen. Vorschläge benötigen konkrete Rezeptrevisionen, Zutatenentscheidungen, Zuordnungen, Personenzahl, Eignungsnachweis und die Auswirkung auf bisherige Lösungen. Unbekannte Daten nicht als geeignet darstellen. Vollständige Suchstrategie, zugelassener Rezeptbestand und Sortierung gesondert festlegen; keine erfundenen Eignungsprozente.

Die Auswahl bleibt zunächst Vorschau. Erst „Lösung übernehmen“ schreibt; dabei Kontextaktualität und Eignung nochmals prüfen. Szenarioentscheidungen der Gemeinsamkeitspräferenz bleiben von Rezeptresets getrennt. Bilder und Ernährungslabels nur bei tatsächlich verfügbaren, belegten Daten anzeigen.


## Implementierter Vertrag (05.11.2026)

- `GET /api/camps/{campId}/cooking-units/{unitId}/meals/{mealId}/supply-suggestions`: schreibfreie Vorschau aus den zugänglichen portionsbasierten Rezeptrevisionen. Antwort: `expectedVersion`, `offers`, optional `unavailableReason`. Je Komponente: Name, wirksame Präferenz, offene/bereits versorgte Personen und Optionen. Je Option: stabile ID, Umfang (`whole`, `missing`, `single:<Anforderungsschlüssel>`), Rezeptname, Bevorzugung und konkrete `SupplySolution` mit Zutatenentscheidungen und Zuordnungen. Keine Namen einzelner Teilnehmer. Antwort mit `Cache-Control: no-store`.
- `POST .../supply-suggestions/apply`: `{ expectedVersion, suggestionId }`. Baut die Vorschläge erneut aus dem aktuellen Kontext auf und akzeptiert ausschließlich eine weiterhin verfügbare Option. Veraltete Version oder nicht mehr passende/verfügbare Auswahl ergibt 409. Der Browser liefert keine vertrauenswürdigen Eignungsbehauptungen oder selbst definierten Zuordnungen.
- Beide Endpunkte verwenden Campzugriff, Bearbeitungsberechtigung, Freeze-Sperre und bei Ist-Teilnehmern die Berechtigung zum Lesen der Anforderungen. Keine Datenbankmigration notwendig.
- `SupplySolutionResolver.FindSuitable` durchsucht explizite Zutatenentscheidungen einer Revision für alle Anforderungen des gewählten Umfangs. Ein unverändert geeignetes Rezept wird gegenüber unnötigen Varianten bevorzugt. Unbekannte relevante Daten qualifizieren nicht als geeignet. Aktive Varianten und explizite Ersatzregeln werden wiederverwendet.
- Gemeinsam: ganze Gruppe bevorzugt, Ergänzung offener Personen als bewusste Alternative. Getrennt: alle offenen Personen gebündelt bevorzugt, einzelne Bedarfe zusätzlich. Bestätigte Mahlzeitenpräferenz hat Vorrang vor dem Standard der Kocheinheit.
- Vorschau zeigt die Auswirkungen vor dem Schreiben. Ganze Gruppe ersetzt Rezeptzuordnungen ausschließlich dieser Komponente; Ergänzung bewahrt bestehende Abdeckung. Gespeicherte Gemeinsamkeitsentscheidungen bleiben erhalten. Die erfolgreiche Übernahme stößt die vorhandene Neuberechnung an.
- UI: zwei Spalten, keine Vorselektion, drei Vorschläge pro Seite je Umfang, Sortierung nach wenigen Zutatenanpassungen bzw. Rezeptname. Leere Ergebnisse, Ladefehler und veraltete Vorschläge werden explizit angezeigt. Bilder und allgemeine Ernährungslabels bleiben ohne belegte Daten Platzhalter; die Eignungskennzeichnung bezieht sich auf den konkreten gewählten Personenkreis.
