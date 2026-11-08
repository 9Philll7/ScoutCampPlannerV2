# API: präferenzabhängige Versorgungsentscheidungen

Stand: 2026-11-01. Erweiterung bestehender Meal-Planning-Routen und JSON-Verträge. Keine neuen Rechte oder Datenbankspalten.

## Berechnung

- **Gemeinsam:** Gemeinsam geeigneten Standard bzw. eindeutige Variante/Alternative bevorzugen. Mehrere geeignete Lösungen als eine Komponentenentscheidung liefern. Ohne gemeinsame Gesamtlösung schrittweise die größte kompatible Teilgruppe bündeln; Rezeptgleichstände bleiben Auswahlen. Nicht versorgbare Gruppen behalten ihre Konflikte. Diese deterministische Bündelung garantiert kein mathematisch globales Minimum an Kochvorgängen.
- **Getrennt:** Geeigneten Standard beibehalten; sonst eindeutige direkte Variante, danach eindeutige Rezeptalternative. Bei Mehrdeutigkeit eine Auswahl je Anforderungsgruppe. Identische bestätigte Rezepte unter Erhalt aller Allokationen zusammenführen.
- **Fragen:** Beide Szenarien vergleichen. Bei identischen Ergebnissen keine überflüssige Richtungsabfrage erzeugen; die gemeinsame eindeutige Lösung, dieselbe Rezeptauswahl oder dieselben verbleibenden Konflikte direkt anzeigen. Andernfalls beide Wege einschließlich offener Optionen und nicht vollständig lösbarer Anforderungen liefern und ohne bestätigte Richtung keine automatischen Lösungen aktivieren. Manuell gespeicherte Rezeptentscheidungen bleiben autoritativ.

`OperationalSupplyDocument.decisions` enthält ausschließlich auf Eignung geprüfte Auswahlgruppen:

```text
{ id, offerGroupId, requirementGroupKeys[], options: SupplySolution[], together }
```

Die ID ist opak. Optionen enthalten ihren konkreten Portionsbedarf, Positionsentscheidungen und Gruppenzuordnungen. Die frühere Suche mit Abbruch nach zwei geeigneten Varianten entfällt. Teilweise verbesserte, weiterhin ungeeignete Vorschläge werden nicht als geeignete Optionen ausgegeben. Zur Kompatibilität enthält `candidates` ebenfalls die geprüften Optionen. Fixe und stufenweise Mengen werden am jeweiligen Batch und nach dem Zusammenführen erneut geprüft.

`preferenceOptions` enthält den Szenariovergleich für **jede** ursprüngliche Kocheinheitenpräferenz. Dieser Vergleich dient ausschließlich „Weitere Anpassungen → Versorgungspräferenz ändern“ und erzeugt bei Gemeinsam/Getrennt keine zusätzliche Frage in der normalen Liste. Die mahlzeitenspezifische Wahl überschreibt die Grundeinstellung nur für die betreffende Komponente.

`commonSupplyChoices` enthält bei unterschiedlichen Ergebnissen zusätzlich `togetherScenario` und `separateScenario`, jeweils mit `solutions`, `decisions` und `problems`. Beide sind Vorschauen. Die bisherigen Felder `togetherOptions` und `separateSolutions` bleiben vorhanden. Eine notwendige unbestätigte Richtung erzeugt `CommonSupplyDecisionRequired` und verhindert saubere Verifikation. Die Mahlzeitenantwort enthält zusätzlich `manualScenarioSelections` mit der gespeicherten Richtung je Komponente. Nach Bestätigung wird die Richtungswahl nur noch über „Weitere Anpassungen → Versorgungsart ändern“ angeboten. „Zurück zum Plan“ erhält diese Präferenz.

Der Ergebnisvergleich ignoriert technische Lösungs-/Auswahl-IDs und berücksichtigt Rezepte, Positionen, Portionen, Allokationen, Optionen und verbleibende Probleme. Unterschiedliche gruppenübergreifende bzw. unabhängige Auswahlräume bleiben unterschiedliche Ergebnisse. Fingerprint-Version 4 invalidiert noch vorhandene ältere Berechnungen ohne allgemeine Präferenzabweichungen.

## Bestätigung und Reset

Der bestehende PUT-Endpunkt `/api/camps/{campId}/cooking-units/{unitId}/meals/{mealId}/supply` akzeptiert zusätzlich:

```json
{
  "expectedVersion": 4,
  "solutions": [],
  "scenarioSelections": [
    { "offerGroupId": "00000000-0000-0000-0000-000000000001", "preference": 0 }
  ]
}
```

`preference: 0` bedeutet Gemeinsam, `1` Fragen, `2` Getrennt. Auch bei einer Grundeinstellung Gemeinsam oder Getrennt kann damit für diese Mahlzeit zu Fragen gewechselt werden. Bei unterschiedlichen Ergebnissen folgt dann die unbestätigte Gemeinsam-/Getrennt-Abfrage. `offerGroupId` darf bei einem individuellen Angebot null sein. Fremde Komponenten, ungültige Werte und doppelte Richtungsentscheidungen werden abgewiesen. Die Liste wird als Ganzes ersetzt. Fehlt das neue Feld, bleibt die bisherige Wahl für ältere Clients erhalten. Ein ausdrückliches leeres Array setzt sie auf die Grundeinstellung zurück.

Konkrete manuelle Rezeptentscheidungen können mitgespeichert werden und behalten Vorrang. Identische Revision, Komponente, Positionsentscheidungen und Herkunft werden vor dem Speichern zusammengeführt. Der Versionsvergleich schützt Richtung und Rezepte gemeinsam. Das Frontend speichert erst nach „Auswahl bestätigen“, berechnet danach automatisch neu und lädt die Übersicht. Noch offene Rezeptoptionen bleiben passend zur bestätigten Richtung sichtbar; keine automatische Verifikation.

## Persistenz und Offline

Ohne Richtungswahl bleibt die bestehende Speicherung als Rezeptarray erhalten. Mit Richtung wird in derselben JSON-Spalte ein Objekt `{ solutions, scenarioSelections }` abgelegt. Alle Leser akzeptieren beide Formen; keine DB-Migration erforderlich.

Offline-Modulversion 6 schützt die allgemeine Präferenzabweichung einschließlich Fragen vor alten Importern; der neue Leser akzeptiert Versionen 1–5 weiterhin. Neue Auswahlgruppen und sämtliche Vergleichsszenarien werden bei der Ermittlung von Rezeptabhängigkeiten berücksichtigt. Paket- und Datenbank-Roundtrip prüfen auch die Wahl.

Die Berechnungs-Fingerprint-Version ist erhöht und berücksichtigt die Richtungswahl. Bestehende ältere Berechnungen und Verifikationen werden dadurch veraltet. Nach dem API-Neustart für die UI-Abnahme neu berechnen.

Authentifizierung, Lager-/Mandantenscope, Bearbeitungsrecht, Health-Leserecht bei realer Teilnehmerbasis, Freeze, Supply-Version und separates Verifikationsrecht bleiben unverändert.

## Prüfnachweis

- 331 Cateringtests erfolgreich, einschließlich Abweichungen von Gemeinsam/Getrennt zu allen Präferenzen, identischer Ask-Ergebnisse, Richtungswahl, Reset, Versionskonflikten, Variantenklassifikation, Bündelung und Paket-Roundtrip.
- 11 HTTP-Tests für Rechte/Scopes/Freeze/Versionskonflikte erfolgreich; 19 Pakettests erfolgreich.
- 216 Frontendtests erfolgreich; Solution-/API-Build und Frontend-Produktionsbuild erfolgreich. Die Präferenz ist unabhängig von der Grundeinstellung unter „Weitere Anpassungen“ änderbar; der Rezeptreset erhält sie.
- Lokale API mit derselben bestehenden Testdatenbank auf Port 5180 neu gestartet; `/health` meldet `healthy` und `Sqlite`. Keine fachlichen Testlagerentscheidungen automatisch überschrieben. Alte Berechnungen zur Abnahme neu berechnen.
- Browserhost weiterhin nicht verfügbar; keine neue visuelle Browserabnahme oder Screenshots behauptet.


## Teil- und Gesamtlösungen (02.11.2026)

Der vorhandene PUT auf /supply akzeptiert zusätzlich completeOwnSupply (optional, Boolean). Auslassen erhält den gespeicherten Wert; false speichert Ergänzungen zum Plan, true eine eigene Gesamtlösung. CookingUnitMealDocument.completeOwnSupply liefert den Modus. Modus und Entscheidungen werden atomar mit derselben expectedVersion gespeichert, ohne neue Datenbankspalte.

- Teil: Nur manuelle Zuordnungen ersetzen die automatische Planung ihres Komponenten-/Anforderungsbereichs. Andere Bereiche folgen weiter dem Plan.
- Gesamt: Der Rechner bewertet ausschließlich gespeicherte eigene Lösungen und füllt fehlende Portionen nicht automatisch auf. Bedarf und Eignung werden weiterhin geprüft. Planänderungen beeinflussen diese Lösung nicht.
- Zurück zum Plan: eigene Rezeptlösungen aus solutions entfernen, bestätigte verschachtelte Planauswahlen erhalten, completeOwnSupply false und gespeicherte scenarioSelections unverändert mitsenden und anschließend neu berechnen. Gemeinsamkeitspräferenzen werden ausschließlich über die Präferenzbearbeitung geändert.
- Konflikteditor: Bestehende Entscheidungen sind geschützt; neue Zuordnungen sind auf bekannte fehlende Portionen begrenzt. Bereits berechnete Teilabdeckungen desselben manuell ergänzten Bereichs werden explizit erhalten, weil die API diesen Bereich danach nicht automatisch ergänzt.
- Freier Editor: Eigene Teillösung speichert nur geänderte/manuelle Karten. Eigene Gesamtlösung übernimmt alle bearbeiteten Karten als manuelle Entscheidungen. Bei Teilversorgung kann eine eigene Karte wieder dem Plan überlassen werden.

Der Fingerprint verwendet Planner-Version 5; bestehende Berechnungen werden dadurch veraltet. Das Offline-Modul verwendet Schema 7 und importiert weiterhin Schema 1–6. Schema 7 verhindert, dass ältere Clients die Gesamtlösung versehentlich als Planergänzung interpretieren. Rechte, Freeze und Versionskonflikte gelten unverändert.

### Präferenzantwort bleibt Planbezug
Die Bestätigung von gemeinsam/getrennt speichert nur die scenarioSelection. Deterministische Rezeptvorschläge sind eine Vorschau und werden danach automatisch berechnet, statt als eigene manuelle Rezeptlösungen gespeichert zu werden. Explizit ausgewählte mehrdeutige Rezeptoptionen und echte eigene Abweichungen bleiben separat speicherbar. Bereits von früheren Clients gespeicherte Rezeptvorschläge, die dem gewählten Szenario entsprechen, zählen in der Anzeige nicht als eigene Teillösungen.


### Verschachtelte Planentscheidungen und unnötige Anpassungen
Der Rezeptreset erhält neben der Gemeinsamkeitspräferenz auch bestätigte Rezept-/Variantenoptionen innerhalb des gewählten Planszenarios. Die UI vergleicht Revision, Komponente, Zutatenentscheidungen und zugeordnete Portionen mit den berechneten Planoptionen. Solche Auswahlen zählen als Planbezug, auch wenn mehrere Anforderungsgruppen zu einer Rezeptkarte zusammengefasst wurden. Echte Rezept-, Zutaten- oder Portionsabweichungen bleiben eigene Lösungen.

Für einen bereits auf Eignung geprüften identischen Bedarf werden angepasste Kandidaten desselben Rezepts entfernt, wenn eine unveränderte geeignete Ausführung vorliegt. Andere Rezepte und notwendige Anpassungen bleiben Optionen. Die Regel gilt erst nach der Eignungsprüfung des konkreten Batches und reduziert keine Abdeckung. Planner-Fingerprint-Version 6 macht ältere Berechnungen veraltet; Offline-Schema 7 bleibt unverändert.
