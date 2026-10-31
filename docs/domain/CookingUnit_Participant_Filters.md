# Konfigurierbare Teilnehmerfilter für Kocheinheiten

Stand: 2026-10-20. Zielbild einschließlich Schätzplanung/Fallback-Grenze vom Benutzer bestätigt; Implementierung vorbereitet, noch nicht umgesetzt.

## 1. Ziel und Abgrenzung

Benannte, lagerbezogene Filter halten den Kocheinheitseditor kompakt. Ein separater Filtereditor kombiniert Stufe, Kategorie und Ernährungsanforderungen. Der Filter selektiert Personen; er bewertet keine Gerichte und ändert keine Portions-, Grenzwert- oder Eignungsregel.

Die Strukturentscheidung `meal-planning-participant-structure-correction.md` bleibt erhalten: Camp besitzt Teilnehmer, Strukturzuordnung und Anwesenheit; Catering konsumiert minimierte Contracts. Lediglich die Beschränkung auf drei Filter wird im neuen Inkrement erweitert. Keine direkten Teilnehmerlisten als Kocheinheits-Stammzuordnung.

Effektive Auswahl = vorhandener Strukturbezug einschließlich Teilbäumen UND effektive Mahlzeitenanwesenheit UND Filter. Bestehende mahlzeitenbezogene Strukturabweichungen bleiben wirksam. Ein neuer mahlzeitenbezogener Filter-Override ist nicht beauftragt.

## 2. Bedienung

Kocheinheitseditor: Strukturwahl, eine Filterauswahl (Alle oder gespeicherter Filter), lesbare Regelzusammenfassung, Erstellen/Bearbeiten/Als neuen Filter speichern. Keine parallele widersprüchliche Auswahl zwischen altem Enum und neuem Filter.

Filterverwaltung: lagerbezogene Liste mit Name, Zusammenfassung und Verwendung; Erstellen, Bearbeiten, Kopieren und Löschen unreferenzierter Filter. Organisationstemplates sind nicht im Umfang.

Filtereditor: Name, Bedingungsgruppen, Speichern/Verwerfen, kontextbezogene Vorschau. Änderungen an gemeinsam genutzten Filtern zeigen vor dem Speichern die betroffenen Kocheinheiten. Die Vorschau braucht Struktur- und Mahlzeitenkontext; eine Zahl ohne Datum/Anwesenheitskontext ist keine Zusage für alle Mahlzeiten.

Vorschau liefert zunächst ausschließlich Zahlen, Überschneidungen und verständliche Hinweise. Kein neuer personenbezogener Drilldown. Gesundheitsbezogene Filterzahlen sind ebenfalls geschützt, nicht über das allgemeine `Camp.View`-Zählendpoint auszugeben.

## 3. Filterlogik

- Mehrere Bedingungsgruppen werden mit ODER verbunden.
- Bedingungen innerhalb einer Gruppe werden mit UND verbunden.
- Mehrere ausgewählte Werte einer Bedingung bedeuten ODER: mindestens einer trifft zu.
- Negation bedeutet „keiner der ausgewählten Werte trifft zu“, nicht ODER einzelner Negationen.
- Weggelassene Merkmale schränken nicht ein, einschließlich fehlender Zuordnungen.
- Stufe/Kategorie ohne Zuordnung sind ausdrücklich auswählbar; nicht mit einem ungültigen Referenzwert verwechseln.
- Kein frei verschachtelter Ausdrucksbaum, kein Skript, keine Referenzen auf andere Filter.
- Ein gespeicherter benannter Filter benötigt mindestens eine nichtleere Bedingungsgruppe; leere Gruppen sind ein Validierungsfehler. „Alle“ ist die ausdrückliche uneingeschränkte Auswahl.
- Mehrfachtreffer über ODER-Gruppen zählen dieselbe Person nur einmal.

Merkmale:

| Merkmal | Datenbasis |
| --- | --- |
| Stufe | CampStageId des Teilnehmers; nur Stufen dieses Lagers |
| Kategorie | ChildYouth / LeaderAdult / nicht zugeordnet |
| Ernährungsform | tatsächlich zugewiesene zugängliche DietType-ID |
| Allergenanforderung | Eintrag in den Allergenanforderungen des Teilnehmers |
| Unverträglichkeitsanforderung | referenzierte Stoff-ID im Teilnehmerprofil |
| Sonderverpflegung | vorhandenes abgeleitetes Merkmal RequiresSpecialCatering |

Keine automatisch erfundene Hierarchie zwischen Ernährungsformen: „vegan“ wird nicht still als „vegetarisch“ selektiert. Allergen-/Stofffilter prüfen vorhandene Anforderungseinträge, keine Zutatenzustände. Fehlender Eintrag bedeutet nur fehlender Eintrag, keine medizinische Unbedenklichkeit. Keine Grenzwertvergleiche im Filtereditor und keine Herleitung der Kategorie aus Namen oder Alter.

Beispiele:

1. (Stufe A ODER B) UND KiJu UND (DietType vegan ODER vegetarisch).
2. (KiJu UND vegan) ODER (Leiter/Erwachsene UND vegetarisch).
3. Alle Personen des gewählten Strukturgebiets OHNE Sonderverpflegung.
4. Teilnehmer mit nicht zugeordneter Stufe, unabhängig von der Kategorie.

## 4. Schutzregeln

Pro anwesender Person und Mahlzeit höchstens eine Kocheinheit. Keine Priorität nach Reihenfolge, Name oder spezifischerem Filter. Konfigurationsänderungen mit tatsächlicher Überschneidung werden atomar abgewiesen; die Vorschau benennt die beteiligten Einheiten. Später durch Teilnehmer-/Anwesenheitsänderungen entstandene Überschneidungen bleiben als unvollständiger Plan sichtbar. Nicht zugeordnete anwesende Personen bleiben ebenfalls sichtbar.

Eine Filteränderung muss alle referenzierenden Einheiten gemeinsam gegen den vorgeschlagenen Stand prüfen; kein schrittweises Speichern mit Teilwirkung. Änderungen zwischen Vorschau und Speichern werden erneut geprüft. Bestehende Versions-/Concurrency- und Freeze-Prüfungen bleiben verbindlich.

Referenzierte Filter dürfen nicht gelöscht werden. Fehlende Stufen oder Katalogreferenzen werden nicht ignoriert und niemals in „Alle“ umgedeutet. Entfernungsschutz für referenzierte Stufen/Katalogeinträge erweitern; modulübergreifend über Contracts. Beschädigter/importierter Bestand erzeugt einen expliziten Fehler, keine still veränderte Auswahl.

## 5. Bestand und Offline

Die bisherigen Auswahlen All, SpecialCateringOnly und WithoutSpecialCatering bleiben semantisch unverändert als Voreinstellungen verfügbar. Die Migration darf die Personenmenge nicht ändern. Entweder kanonische eingebaute Auswahlen plus Referenz auf benannten Filter oder eine gleichwertige Migration; genau eine operative Auswertungsstrecke.

PostgreSQL und SQLite erhalten additive Migrationen. Paketversionierung ist separat vorzunehmen. Filterdefinitionen, Unit-Referenzen und erforderliche Referenzdaten werden mittransportiert, validiert und atomar ersetzt. Ältere Pakete werden deterministisch auf die bisherigen Filter abgebildet. Unbekannte Operatoren/Versionen und fremde Referenzen werden abgewiesen, nicht vereinfacht. Rechte werden nicht transportiert oder automatisch vergeben.

Sensible Offline-Daten bleiben ausschließlich Entwicklungsfunktion mit Dummy-/Testdaten, keine Produktionsfreigabe.

## 6. Invalidierung und Verifikation

Filterdefinition und tatsächlich relevante Auswahldaten werden in die vorhandene Abhängigkeits-/Signaturprüfung integriert. Stufe und Kategorie dürfen nicht pauschal jede Berechnung invalidieren: relevant sind die verwendeten Bedingungen und resultierenden Personen/Probleme. Ernährung bleibt unabhängig vom Filter für die Eignung relevant.

Umbenennung, Umsortierung und semantisch identische Ausdrücke dürfen keinen fachlichen Stale-Zustand erzeugen. Änderungen der Auswahl, Bedarfsbasis oder Zuordnungsprobleme betreffen die zugehörigen CookingUnit×Meal-Stände und ihre Verifikation. Kein globales Invalidieren des ganzen Lagers als Ersatz. NoSupplyRequired nach bestehenden Regeln mitprüfen. Gespeicherte manuelle SupplySolutions nicht überschreiben.

## 7. Bestätigte Grenze: Schätzplanung / Fallback

Anonyme Planungszahlen enthalten keine individuellen Ernährungsanforderungen. Die Kombinationen dürfen daher nicht aus Prozentsätzen geraten oder mit dem ungekürzten Gesamtbedarf als scheinbar gefiltert ausgegeben werden.

Vom Benutzer am 2026-10-20 bestätigt: Neue frei konfigurierte Filter operativ nur mit realer Teilnehmerbasis verwenden. In EstimatedPlanning bzw. bei einem erforderlichen EstimatedFallback ausdrücklich „Filter mit dieser Datenbasis nicht auswertbar“ melden und den betroffenen Stand als unvollständig behandeln. Null Treffer bei vorhandenen realen Kontextdaten sind dagegen null reale Treffer und kein Anlass zum Fallback. Die bisherigen drei Auswahlen behalten ihr Bestandsverhalten.

Eine Schätz-Auswertung ist nicht Bestandteil dieses ersten Schnitts. Der folgende Bedarf ist ausdrücklich für einen separaten Folgeauftrag vorgemerkt.

## 8. Folgebedarf: konfigurierbare Schätzwerte für die Erstplanung

Vom Benutzer am 2026-10-20 angefordert: Für die erste Planung und Kalkulation müssen später auch für frei konfigurierte Teilnehmerfilter bewusst erfasste Schätzwerte konfigurierbar sein, solange noch keine ausreichenden realen Teilnehmerdaten vorliegen. Dieser Bedarf ist nicht auf reine Stufen-/Kategoriebedingungen beschränkt.

Die Schätzwerte müssen als solche erkennbar und von realen Teilnehmerzahlen sowie medizinischer Eignung getrennt bleiben. Es dürfen keine Ernährungsanteile oder Grenzwerte automatisch erfunden werden.

Im Folgeauftrag zu klären: Bezug der Schätzung (Filter, Kocheinheit, Struktur und/oder Mahlzeit), Mengenmodell, Umgang mit überlappenden Filtern und Doppelzählung, Wirkung von Filteränderungen, Invalidierung sowie bewusster Übergang auf reale Teilnehmerdaten. Diese Detailregeln sind noch nicht entschieden.

Die bestätigte Einschränkung aus Abschnitt 7 bleibt für das aktuelle Inkrement bestehen; dieser Vermerk erweitert dessen Implementierungsumfang nicht.
