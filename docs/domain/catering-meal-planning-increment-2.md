# Catering Meal Planning – Inkrement 2

> Verbindliche Korrektur vom 2026-10-06: Die direkte personenbezogene CookingUnit-
> Zuordnung und deren Standard-/Meal-Overrides sind durch Camp-Strukturzuordnung,
> Anwesenheit und CookingUnit-Filter ersetzt. Die entsprechenden ursprünglichen
> Abschnitte und Prüffälle unten sind historisch, nicht mehr Implementierungsziel.
> Maßgeblich: ../decisions/meal-planning-participant-structure-correction.md.

## Umsetzungsstand 2026-10-04

Diese Datei beschreibt den bestätigten Sollumfang, nicht einen abgeschlossenen
Release. Bisher umgesetzt: Camp-Teilnehmerkern mit beiden Provider-Migrationen,
explizite Health-/Verify-Berechtigungsgrundlage, reine Ableitung von Anwesenheit
und RequirementGroups sowie isolierte Bedarfsbasis-/Stoffbewertungslogik.
Diese Catering-Bausteine sind noch nicht an den bestehenden operativen
MealPlanningService angeschlossen. Teilnehmer-API/UI, explizite Rechtevergabe
und Dummy-Teilnehmer-Package-Roundtrip sind umgesetzt. DietTypes besitzen jetzt
zentrale/mandanteneigene Pflege, Herkunftsregeln, Versionen und geprüfte Beiträge.
Optionale Stoffvorgaben werden im Teilnehmereditor einmalig übernommen.
Zutaten- und Varianteneditor unterstützen die drei Stoffmodi. SupplySolutions,
operative Zuordnung/Bedarfsbasis und Verifikation bleiben offen.
Details und Prüfnachweise: [Ergebnisbericht](../../.codex/meal-planning-increment-2-result.md).

## Zweck

Dieses Dokument ergänzt die bestehende Mahlzeitenplanung aus Inkrement 1 um:

- reale Teilnehmer als optionale operative Bedarfsbasis,
- Anwesenheit und Zuordnung zu CookingUnits,
- automatisch abgeleitete RequirementGroups,
- Allergene, Unverträglichkeiten und Ernährungsformen,
- qualitative und quantitative Stoffbewertung,
- konkrete Versorgungslösungen,
- automatische und manuelle Konfliktauflösung,
- Gemeinsamkeitspräferenzen,
- strukturierte Konflikte und Plausibilitätsprobleme,
- Verifikation pro `CookingUnit × Meal`.

Die bestätigten Regeln aus Inkrement 1 bleiben bestehen, sofern dieses Dokument sie nicht ausdrücklich erweitert.

Dieses Inkrement führt kein vollständiges Teilnehmermanagement und keine Einkaufs-/Ausgabelogistik ein.

## 1. Scope

### 1.1 Ziel

Inkrement 2 soll aus realen Teilnehmerdaten konkrete verpflegungsrelevante Anforderungen ableiten und für jede `CookingUnit × konkrete Mahlzeit` prüfen, wie diese Anforderungen durch die geplanten Rezepte, Varianten und Ersatzzutaten erfüllt werden.

Die Anwendung bleibt ein unterstützendes Werkzeug. Sie darf eindeutige Lösungen automatisch anwenden, Konflikte und Unsicherheiten strukturiert anzeigen, bewusste manuelle Entscheidungen erhalten und eine fachliche Verifikation dokumentieren. Sie darf keine unbekannten medizinischen Schwellenwerte erfinden, keine mehrdeutigen Alternativen still auswählen, ungeeignete manuelle Entscheidungen automatisch überschreiben oder medizinische Sicherheit garantieren.

### 1.2 Nicht-Ziele

Nicht Bestandteil dieses Inkrements sind insbesondere:

- vollständiges Personen-/Teilnehmermanagement außerhalb der Catering-relevanten Daten,
- Adresse, Kontakt- und Notfallkontaktdaten,
- Check-in/Check-out-System,
- Erziehungsberechtigtenlogik,
- Abrechnung,
- Dokumenten-/Anhangverwaltung,
- Bestellwesen,
- Ausgabestellen,
- Warenlager,
- Einkaufsoptimierung,
- Gebinde-/Mindestmengenoptimierung,
- automatische Suche beliebiger Ersatzrezepte in der Rezeptbibliothek,
- frei programmierbare Regelengine,
- rekursive Ersatzketten,
- historische Kette aller VerificationSnapshots,
- produktive Offlinefreigabe echter sensibler Teilnehmerdaten ohne die vorgesehene Security-Stufe.

## 2. Teilnehmerkern

Teilnehmer sind `Camp`-owned. `Catering` konsumiert Teilnehmerinformationen ausschließlich über definierte Camp-Contracts. Keine Catering-eigene Teilnehmer-Stammdatenbank anlegen.

Für Inkrement 2 werden nur die verpflegungsrelevanten Teilnehmerdaten benötigt:

- stabile Teilnehmer-ID,
- Name bzw. Anzeigename,
- Anwesenheit pro Lagertag,
- mahlzeitenspezifische Abwesenheit,
- optional genau eine primäre Ernährungsform,
- beliebig viele Allergene,
- beliebig viele Unverträglichkeiten,
- bei mengenabhängigen Unverträglichkeiten optional individueller Grenzwert pro Portion,
- Herkunft/Quelle eines individuellen Grenzwerts als Metadatum.

Kein generischer `Active/Inactive`-Status ist erforderlich.

## 3. Anwesenheit

Anwesenheit wird auf zwei Ebenen modelliert.

### 3.1 Tagesebene

Ein Teilnehmer kann für einen kompletten Lagertag als abwesend markiert werden. Dann ist er für alle Mahlzeiten dieses Tages nicht verpflegungsrelevant.

### 3.2 Mahlzeitenebene

An einem grundsätzlich anwesenden Tag kann ein Teilnehmer für einzelne Mahlzeiten explizit als abwesend markiert werden.

### 3.3 Effektive Anwesenheit

Ein Teilnehmer ist für eine konkrete Mahlzeit relevant, wenn der Lagertag für ihn aktiv ist und die konkrete Mahlzeit nicht explizit als abwesend markiert wurde.

Ist der gesamte Tag inaktiv, sind Mahlzeitenabweichungen dieses Tages operativ ohne Wirkung.

Änderungen an der Tagesanwesenheit invalidieren nur betroffene Mahlzeiten dieses Tages. Änderungen an einer Mahlzeitenanwesenheit invalidieren nur den betroffenen Slot.

## 4. Teilnehmerzuordnung zu CookingUnits

Eine CookingUnit besitzt eine Standard-Teilnehmerzuordnung.

Für eine konkrete Mahlzeit darf die Teilnehmerzuordnung bewusst abweichen. Ein Mahlzeiten-Override bleibt von späteren Änderungen der Standardzuordnung unberührt und kann explizit auf die aktuelle Standardzuordnung zurückgesetzt werden.

Ein Teilnehmer darf für dieselbe konkrete Mahlzeit höchstens einer CookingUnit zugeordnet sein. Doppelzuordnungen sind nicht zulässig.

Ein für eine aktive Mahlzeit effektiv anwesender realer Teilnehmer ohne CookingUnit-Zuordnung gilt als nicht abgedeckt. Das System ordnet ihn nicht automatisch zu, zeigt die fehlende Zuordnung klar an und erlaubt für den betroffenen Kontext keine Clean Verification. VerifiedWithDeviation bleibt mit Begründung möglich.

## 5. Lagerweite Bedarfsbasis

Es gibt einen lagerweiten, vom Operator bewusst gesetzten Modus:

- `EstimatedPlanning`
- `UseActualParticipants`

Der Wechsel erfolgt niemals automatisch.

### 5.1 EstimatedPlanning

Die Bedarfsbasis aus Inkrement 1 bleibt maßgeblich.

### 5.2 UseActualParticipants

Sind für einen `CookingUnit × Meal`-Kontext reale Teilnehmerdaten vorhanden, werden diese als operative Bedarfsbasis verwendet.

Nur wenn für diesen Kontext keine realen Teilnehmerdaten vorhanden sind, darf auf `EstimatedFallback` zurückgegriffen werden.

Sind reale Teilnehmer vorhanden, aber unvollständig zugeordnet oder anderweitig lückenhaft, darf die Schätzbasis diese Fehler nicht verdecken. Der reale Stand bleibt maßgeblich und der Kontext wird entsprechend `Incomplete` bzw. problembehaftet.

Der Operator darf bewusst zurück auf `EstimatedPlanning` wechseln.

Ein Wechsel der lagerweiten Basis macht nur jene Verpflegungsstände `Stale`, deren effektiv verwendete Bedarfsbasis sich dadurch ändert.

Für jeden Teilstand muss sichtbar sein, welche Basis tatsächlich verwendet wurde:

- `Estimated`
- `ActualParticipants`
- `EstimatedFallback`

## 6. Teilnehmeranforderungen

Es wird kein universeller zusätzlicher `RequirementType` für Allergene und Unverträglichkeiten eingeführt.

Beim Teilnehmer werden direkt referenziert:

- Allergene,
- Unverträglichkeiten,
- optionale primäre Ernährungsform.

### 6.1 Allergene

Teilnehmer referenzieren vorhandene Allergen-Definitionen direkt. Die Eignungsprüfung erfolgt qualitativ gegen den resultierenden Zutatenstand einer Versorgungslösung.

### 6.2 Unverträglichkeiten

Teilnehmer referenzieren vorhandene Unverträglichkeits-/Stoffdefinitionen direkt. Bei mengenabhängigen Unverträglichkeiten kann ein individueller Grenzwert pro Portion gespeichert werden.

### 6.3 Grenzwert-Default

Der zentrale Unverträglichkeits-/Stoffkatalog darf optional einen Default-Grenzwert enthalten. Beim Anlegen einer Teilnehmer-Unverträglichkeit kann dieser Wert als Startwert kopiert werden. Danach ist der Teilnehmerwert eigenständig. Spätere Änderungen des zentralen Defaults verändern bestehende Teilnehmerwerte nicht automatisch.

### 6.4 Herkunft eines Grenzwerts

Die Herkunft des konkreten Teilnehmer-Grenzwerts wird als Metadatum mitgeführt, z. B.:

- aus zentralem Default übernommen,
- individuell angegeben,
- aus externer Quelle übernommen.

Die Herkunft dient der Nachvollziehbarkeit. Eine reine Änderung dieser Herkunft ohne Änderung des Grenzwerts ist nicht berechnungsrelevant.

## 7. Ernährungsformen

Ein Teilnehmer hat höchstens eine primäre Ernährungsform. Allergene und Unverträglichkeiten bleiben davon unabhängig.

Ernährungsformen werden als zentraler, pflegbarer Katalog modelliert. Beispiele sind vegetarisch, vegan und pescetarisch.

Zentrale Definitionen dürfen von Mandanten nicht verändert werden. Mandanten dürfen eigene zusätzliche DietTypes anlegen. Mandantenspezifische DietTypes können über den bestehenden Contribution-Workflow zur zentralen Übernahme vorgeschlagen werden. Keine automatische Hochstufung.

Eine Zutat besitzt genau eine Hauptherkunft.

Die Eignung einer Versorgungslösung für einen DietType wird ausschließlich über die Hauptherkunft der tatsächlich resultierenden Zutaten bewertet.

Bestehende Zusatzflags wie tierisches Fett, tierisches Lab, Gelatine oder sonstiger tierischer Ursprung werden in Inkrement 2 nicht als Grundlage einer zweiten DietType-Regelengine verwendet. Ihre mögliche spätere Bereinigung ist ein separater Refactoring-Punkt.

Für jede relevante Hauptherkunft kennt ein DietType:

- `Allowed`
- `Excluded`

Fehlt eine Regel, ist die Bewertung `Unknown / prüfbedürftig` und nicht still `Allowed`.

Es wird keine zusätzliche Implikationshierarchie wie `Vegan => Vegetarian` benötigt. Ob eine Versorgungslösung eine vegetarische oder vegane Anforderung erfüllt, ergibt sich direkt aus ihrem resultierenden Zutatenstand und den Regeln des jeweiligen DietType.

Ändert sich eine relevante DietType-Regel oder die Hauptherkunft einer tatsächlich verwendeten Zutat, werden betroffene `CookingUnit × Meal`-Stände `Stale`.

## 8. RequirementGroups

RequirementGroups werden automatisch aus der effektiv anwesenden und der konkreten Mahlzeit zugeordneten realen Teilnehmermenge abgeleitet.

Sie sind kein manuell editierbares Stammdatenobjekt. Der Operator verändert nicht die RequirementGroups, sondern die konkrete Versorgung.

Eine RequirementGroup besteht fachlich aus:

- Portionszahl,
- optionalem DietType,
- ungeordneter Allergenmenge,
- ungeordneter Menge von Unverträglichkeiten samt berechnungsrelevanten individuellen Grenzwerten.

Die Standardgruppe wird als Gruppe mit keinem DietType, leerer Allergenmenge und leerer Unverträglichkeitsmenge modelliert. Es gibt kein künstliches Requirement `Standard`.

Zwei Teilnehmer gehören genau dann in dieselbe RequirementGroup, wenn ihre berechnungsrelevanten Anforderungen identisch sind:

- gleicher DietType bzw. keiner,
- gleiche Allergenmenge,
- gleiche Unverträglichkeitsmenge,
- gleiche relevante Grenzwerte.

Nicht gruppentrennend sind Herkunft/Quelle des Grenzwerts, Auditdaten, Erfassungszeitpunkt oder reine Metadaten.

Unterschiedliche Grenzwerte derselben Unverträglichkeit erzeugen unterschiedliche RequirementGroups.

RequirementGroups werden bei relevanten Teilnehmeränderungen neu abgeleitet und nicht manuell korrigiert.

## 9. Stoffdaten an Zutaten

Für jeden mengenabhängigen Stoff an einer Zutat gibt es einen expliziten Bewertungsmodus:

- `Quantitative`
- `Qualitative`
- `Unknown`

`Quantitative`: Ein konkreter Stoffgehalt pro definierter Bezugsmenge ist bekannt.

`Qualitative`: Es ist nur bekannt, ob der Stoff enthalten oder nicht enthalten ist.

`Unknown`: Es ist keine belastbare Aussage möglich.

`Unknown` darf weder als enthalten noch als nicht enthalten interpretiert werden. Fehlende quantitative Stoffmengen werden nicht geschätzt. Qualitative Daten sind ein zulässiger fachlicher Fallback und kein fehlerhafter Datenzustand.

## 10. Quantitative Unverträglichkeitsbewertung

Quantitative Bewertung erfolgt pro Portion der konkreten Versorgungslösung.

Wenn quantitative Daten und ein Teilnehmergrenzwert vorhanden sind, wird der berechnete Stoffgehalt pro Portion mit dem individuellen Teilnehmer-/Gruppengrenzwert verglichen.

Liegt für eine mengenabhängige Unverträglichkeit kein Grenzwert vor, fällt die Bewertung auf eine qualitative Präsenzprüfung zurück. Dabei wird eine Warnung ausgegeben, dass keine quantitative Schwelle vorliegt. Keine medizinische Schwelle wird erfunden.

Bei `Qualitative = contained` ist keine exakte quantitative Aussage möglich. Für eine relevante Unverträglichkeit entsteht eine qualitative Konflikt-/Warnsituation.

Bei `Qualitative = not contained` entsteht daraus kein Konflikt.

`Unknown` bei einem für eine RequirementGroup relevanten Stoff erzeugt einen offenen Prüfkonflikt. `Unknown` blockiert Clean Verification. VerifiedWithDeviation bleibt mit Begründung möglich.

Für jeden Stoff wird die bestmögliche verfügbare Datenqualität verwendet. Nur wenn alle relevanten Beiträge quantitativ bekannt sind, darf eine exakte Gesamtsumme pro Portion berechnet werden.

Sobald mindestens ein relevanter Beitrag nur qualitativ `contained` ist, darf keine scheinbar exakte Gesamtsumme gebildet werden; es erfolgt qualitative Bewertung mit Warnung.

Sobald ein relevanter Beitrag `Unknown` ist, bleibt die Bewertung offen/problembehaftet.

`Qualitative = not contained` trägt keinen Gehalt bei.

## 11. Versorgungslösung

Die zentrale operative Einheit der Sonderverpflegung ist eine Versorgungslösung.

Sie besteht aus:

- einer veröffentlichten RecipeRevision,
- konkreten Varianten-/Ersatzentscheidungen an Rezeptpositionen,
- Portionsmenge,
- expliziter Zuordnung dieser Portionen zu RequirementGroups.

Die Eignung wird immer auf Basis des resultierenden Zutatenstands dieser Lösung bewertet. Nicht das Ausgangsrezept allein ist die auswertbare Einheit.

## 12. Varianten und Ersatzzutaten

Wird an einer konkreten Rezeptposition eine Variante oder Ersatzzutat gewählt, ersetzt sie für die Eignungsbewertung vollständig den ursprünglichen Zutatenbeitrag dieser Position.

Bewertet werden danach nur die tatsächlich verwendeten Zutaten. Das betrifft Allergene, Unverträglichkeitsstoffe, quantitative Stoffmengen, Hauptherkunft und DietType-Eignung.

Mehrere direkte Varianten-/Ersatzentscheidungen dürfen innerhalb derselben Versorgungslösung kombiniert werden, sofern sie verschiedene konkrete Rezeptpositionen betreffen, miteinander kompatibel sind und der resultierende Gesamtzustand konsistent ist.

Das ist keine rekursive Ersatzkette.

Eine Ersatzzutat wird nicht erneut über eine weitere Ersatzregel automatisch ersetzt. Komplexe Fälle werden über eine andere Versorgungslösung bzw. ein anderes Rezept gelöst.

Die Eignungsprüfung umfasst die komplette transitive Rezeptstruktur inklusive Subrecipes.

Varianten-/Ersatzentscheidungen werden an der konkret betroffenen Rezeptposition innerhalb dieser transitiven Struktur gespeichert.

## 13. Automatische Konfliktauflösung

Die automatische Auflösung folgt einer festen fachlichen Reihenfolge:

1. direkte Varianten/Ersatzzutaten,
2. OfferGroup-Alternativen,
3. offener Konflikt/manuelle Entscheidung.

### 13.1 Direkte Lösung

Eine automatische Auswahl erfolgt nur, wenn genau eine eindeutige vollständige Lösung besteht.

Mehrere unabhängige eindeutige Direktlösungen dürfen automatisch kombiniert werden, wenn sie gemeinsam kompatibel sind und alle Anforderungen erfüllen.

Eine Lösung gilt nur dann als geeignet, wenn sie alle Anforderungen einer RequirementGroup gleichzeitig erfüllt.

Teilauflösungen dürfen erkannt und angezeigt werden, gelten aber nicht als vollständige Lösung.

### 13.2 OfferGroup-Alternative

Nur wenn keine eindeutige vollständige direkte Lösung existiert, werden alternative RecipeRevisions derselben konkreten MealPlanOfferGroup geprüft.

Gibt es genau eine vollständig passende Alternative, darf diese automatisch ausgewählt werden.

Gibt es mehrere passende Alternativen, bleibt die Entscheidung offen.

Die Darstellungs-/Sortierreihenfolge ist keine fachliche Priorität.

Eine mögliche spätere Standardpräferenz wie `Normal -> Vegetarisch -> Vegan` ist ausdrücklich nicht Bestandteil dieses Inkrements.

### 13.3 Begrenzung der Automatik

Die Automatik darf ausschließlich verwenden:

- im Rezept definierte Varianten/Ersatzzutaten,
- alternative RecipeRevisions derselben konkreten MealPlanOfferGroup.

Die Automatik darf nicht selbstständig beliebige andere Rezepte aus der Bibliothek suchen.

Der Operator darf bewusst ein anderes veröffentlichtes Rezept wählen, auch wenn dieses nicht Teil der OfferGroup war. Diese Wahl ist immer manuell, wird als Versorgungslösung gespeichert und normal gegen die RequirementGroups geprüft.

## 14. Manuelle Entscheidungen

Automatische Lösungen dürfen bewusst überschrieben werden.

Manuelle Entscheidungen werden explizit gespeichert.

Bei Neuberechnung werden sie erneut bewertet, aber nicht still überschrieben.

Sind sie weiterhin geeignet, bleiben sie bestehen. Sind sie nicht mehr geeignet, werden sie problem-/konfliktbehaftet markiert.

Eine ungeeignete manuelle Entscheidung verhindert Clean Verification, kann aber über VerifiedWithDeviation bewusst akzeptiert werden.

## 15. Zuordnung von Versorgungslösungen zu RequirementGroups

Jede geplante Versorgungslösung besitzt eine explizite Zuordnung ihrer Portionen zu einer oder mehreren RequirementGroups.

Eine Versorgungslösung darf mehrere RequirementGroups gleichzeitig versorgen, sofern sie für jede dieser Gruppen vollständig geeignet ist.

Die Eignung wird pro Gruppe separat geprüft. RequirementGroups selbst werden dadurch nicht zusammengeführt.

Eine Versorgungslösung darf Anforderungen übererfüllen. Entscheidend ist, dass keine Anforderung der jeweiligen Gruppe verletzt wird.

Die Zuordnung soll rechnerisch exakt sein.

Das System unterscheidet mindestens:

- `RequirementGroupUndercovered`
- `RequirementGroupOvercovered`
- `UnassignedPlannedPortions`

Abweichungen blockieren die Arbeitsplanung nicht, werden aber klar als Probleme/Warnungen angezeigt.

Clean Verification ist bei solchen Abweichungen nicht möglich. VerifiedWithDeviation bleibt mit Begründung möglich.

## 16. Zusammenführung von Versorgungslösungen

Zwei Versorgungslösungen derselben Mahlzeit dürfen zusammengeführt werden, wenn dieselbe RecipeRevision verwendet wird, identische Varianten-/Ersatzentscheidungen vorliegen und keine sonstige versorgungsrelevante Entscheidung unterschiedlich ist.

Die Portionsmengen und RequirementGroup-Zuordnungen werden addiert.

Unterschiedliche RequirementGroup-Zuordnungen allein erzwingen keine getrennten Versorgungslösungen.

Varianten-/Ersatzentscheidungen gelten immer für die gesamte Portionsmenge einer Versorgungslösung.

Soll nur ein Teil anders zubereitet werden, entsteht eine eigene Versorgungslösung.

## 17. Gemeinsamkeitspräferenz

Jede CookingUnit besitzt genau eine Gemeinsamkeitspräferenz:

- `AlwaysTogether`
- `Ask`
- `NeverTogether`

Kein zusätzlicher Meal-Override in Inkrement 2.

Die Präferenz steuert den Umfang bereits fachlich zulässiger Lösungen. Sie verändert keine medizinische oder fachliche Eignung.

`AlwaysTogether`: Wenn eine geeignete Versorgungslösung mehrere RequirementGroups gemeinsam versorgen kann, wird gemeinsame Versorgung bevorzugt, sofern dadurch keine Gruppe ungeeignet wird.

`Ask`: Das System fragt nur dann zwischen gemeinsamer und getrennter Versorgung, wenn beide Wege fachlich zulässig sind.

`NeverTogether`: Unterschiedliche RequirementGroups bleiben getrennt, außer sie verwenden ohnehin exakt dieselbe Versorgungslösung: gleiche RecipeRevision plus identische Varianten-/Ersatzentscheidungen.

Gibt es mehrere fachlich geeignete gemeinsame Versorgungslösungen, wählt das System keine „beste“ Lösung. Insbesondere keine implizite Optimierung nach wenigsten Zubereitungsvarianten, Kosten, Aufwand, Nähe zum Standard oder maximaler Gemeinsamkeit.

## 18. Strukturierte Probleme und Konflikte

Probleme werden maschinenlesbar modelliert.

Empfohlene Obertypen:

- `Suitability`
- `Coverage`
- `DataQuality`
- `ManualDecision`

Mindestens folgende ReasonCodes sind erforderlich:

- `NoMatchingDirectSolution`
- `MultipleDirectSolutions`
- `PartialDirectResolution`
- `NoMatchingOfferAlternative`
- `MultipleMatchingOfferAlternatives`
- `UnknownSubstanceData`
- `QualitativeOnlySubstanceData`
- `ManualDecisionNoLongerSuitable`
- `RequirementGroupUndercovered`
- `RequirementGroupOvercovered`
- `UnassignedPlannedPortions`

Freitext darf ergänzen, ersetzt aber nicht die strukturierte Kategorie.

Konflikte sind grundsätzlich aus dem aktuellen Versorgungszustand abgeleitete Ergebnisse.

Persistiert werden insbesondere bewusste manuelle Entscheidungen und später akzeptierte Abweichungen.

Bei relevanten Änderungen wird die Konfliktlage neu bewertet.

## 19. Verifikation

Verifikation erfolgt pro `CookingUnit × konkrete Mahlzeit`.

Bearbeitung und Verifikation verwenden getrennte Berechtigungen, sinngemäß:

- `MealPlanning.Edit`
- `MealPlanning.Verify`

Bestehende Projektkonventionen für Permission-Namen haben Vorrang.

Ein Benutzer darf beide Permissions besitzen. Kein verpflichtendes Vier-Augen-Prinzip.

Verifikation sperrt die Planung nicht. Nach der Verifikation darf weitergearbeitet werden. Relevante Änderungen machen die Verifikation für den aktuellen Arbeitsstand `Stale`.

### 19.1 Clean Verification

Clean Verification ist nur möglich, wenn:

- der Verpflegungsstand `Current` ist,
- keine notwendigen Daten fehlen,
- alle RequirementGroups vollständig versorgt sind,
- keine offenen relevanten Konflikte bestehen,
- keine Coverage-Abweichungen bestehen,
- keine relevanten `Unknown`-Bewertungen bestehen,
- alle bewussten Entscheidungen auf dem aktuellen Datenstand gültig sind.

### 19.2 VerifiedWithDeviation

Ist Clean Verification nicht möglich, darf ein Benutzer mit Verify-Permission bewusst `VerifiedWithDeviation` ausführen.

Dabei ist zwingend eine Begründung erforderlich.

Die Abweichung muss im aktuellen Verifikationszustand sichtbar bleiben.

## 20. VerificationSnapshot

Pro `CookingUnit × Meal` gibt es höchstens einen aktuellen VerificationSnapshot.

Bei erneuter Verifikation wird ein neuer Snapshot erzeugt, der bisherige ersetzt und der bisherige Snapshot gelöscht.

Es gibt keine vollständige historische Snapshot-Kette.

Der Snapshot friert nur den minimal notwendigen fachlichen Zustand ein.

Mindestens:

- CookingUnitId,
- konkreter MealSlot,
- verwendete MealPlan-Version/Snapshot-Referenz,
- tatsächlich verwendete Bedarfsbasis,
- Gesamtbedarf/Gesamtportionen,
- fachlich relevante Signatur bzw. minimale eingefrorene Darstellung der RequirementGroups,
- verwendete Versorgungslösungen,
- deren RecipeRevision-Referenzen,
- Varianten-/Ersatzentscheidungen,
- Portionsmengen,
- RequirementGroup-Zuordnungen,
- Ergebnis der relevanten Konflikt-/Problemprüfung,
- Verifikationstyp `Clean` oder `WithDeviation`,
- bei Abweichung Begründung,
- VerifiedBy,
- VerifiedAt.

Nicht unnötig duplizieren: vollständige Rezeptdaten, vollständige Zutatenstammdaten, vollständige Teilnehmerstammdaten oder reine Quellen-/Metadaten, die nicht berechnungsrelevant sind.

Daten, die über stabile IDs und unveränderliche Revisionen eindeutig rekonstruierbar sind, sollen referenziert werden.

Audit-Metadaten der Verifikationsaktion bleiben separat nachvollziehbar.

## 21. Invalidierung Inkrement 2

Nur Änderungen, die den tatsächlich verwendeten Datenzustand eines konkreten `CookingUnit × Meal` beeinflussen können, machen diesen Teilstand `Stale`.

Berechnungsrelevant sind insbesondere:

- Teilnehmerzuordnung geändert,
- Mahlzeiten-Override der Teilnehmerzuordnung geändert,
- Teilnehmeranwesenheit geändert,
- DietType eines relevanten Teilnehmers geändert,
- Allergene eines relevanten Teilnehmers geändert,
- Unverträglichkeiten eines relevanten Teilnehmers geändert,
- individueller Grenzwert geändert,
- relevante DietType-Regel geändert,
- Hauptherkunft einer verwendeten Zutat geändert,
- Allergenkennzeichnung einer verwendeten Zutat geändert,
- Stoffmodus `Quantitative/Qualitative/Unknown` einer verwendeten Zutat geändert,
- quantitativer Stoffgehalt einer verwendeten Zutat geändert,
- verwendete RecipeRevision oder SubrecipeRevision geändert,
- relevante Varianten-/Ersatzdefinition geändert,
- manuelle Varianten-/Ersatzentscheidung geändert,
- Versorgungslösungs-Portionsmenge geändert,
- RequirementGroup-Zuordnung einer Versorgungslösung geändert,
- Wechsel der lagerweiten Bedarfsbasis, wenn sich dadurch die tatsächlich verwendete Basis ändert,
- alle bereits in Inkrement 1 als relevant definierten Änderungen.

Nicht berechnungsrelevant sind insbesondere:

- reine Quelle/Herkunft eines unveränderten Teilnehmer-Grenzwerts,
- Anzeigenamen,
- Sortierung,
- reine Beschreibungen,
- Änderungen an nicht verwendeten Zutaten,
- Änderungen an nicht verwendeten Rezepten,
- Änderungen an nicht relevanten DietTypes,
- Änderungen an Teilnehmern, die für den konkreten Slot nicht relevant sind.

Eine bestehende Verifikation wird bei denselben relevanten Änderungen für den aktuellen Arbeitsstand `Stale`.

Der alte Snapshot bleibt unverändert, bis er durch eine neue Verifikation ersetzt wird.

## 22. Offline und sensible Daten

Domain-, Application- und Package-Logik von Inkrement 2 sollen auch im lokalen Paketrundlauf funktionieren.

In der aktuellen Entwicklungsphase werden ausschließlich Dummy-/Testdaten verwendet.

Daher dürfen Teilnehmer- und Sonderverpflegungsdaten im bestehenden Entwicklungs-Package transportiert werden.

Dies ist ausdrücklich keine Produktionsfreigabe für reale personenbezogene bzw. sensible Daten.

Vor produktivem Offlineeinsatz mit echten Teilnehmerdaten ist die vorgesehene Security-Stufe mit angemessenem Schutz des Paketinhalts umzusetzen.

Das bestehende Freeze-/Replace-Modell bleibt unverändert:

- keine parallele Cloud-/Offline-Bearbeitung,
- kein Merge,
- lokaler Stand während Offlinephase führend,
- atomarer Replace beim Rückimport.

Die Security-Stufe verändert den Transport, nicht das Fachmodell.

## 23. Akzeptanzfälle

Mindestens folgende fachliche Fälle müssen abgedeckt sein:

1. Lager arbeitet zunächst mit EstimatedPlanning.
2. Operator schaltet bewusst auf UseActualParticipants.
3. Bei vorhandenen realen Teilnehmern werden diese verwendet.
4. Bei 0 realen Teilnehmerdaten wird EstimatedFallback verwendet.
5. Vorhandene, aber unvollständig zugeordnete reale Teilnehmer führen nicht zum Fallback.
6. Teilnehmer können ganze Tage abwesend sein.
7. Teilnehmer können an einzelnen Mahlzeiten abwesend sein.
8. Ein Teilnehmer kann pro Mahlzeit höchstens einer CookingUnit angehören.
9. Nicht zugeordnete relevante Teilnehmer werden sichtbar.
10. RequirementGroups werden automatisch und disjunkt abgeleitet.
11. Unterschiedliche Grenzwerte erzeugen unterschiedliche RequirementGroups.
12. Dieselbe Versorgungslösung kann mehrere RequirementGroups versorgen.
13. Standardgruppe besitzt keine künstliche Requirement-ID.
14. Allergene werden gegen den resultierenden Zutatenstand geprüft.
15. DietType wird über Hauptherkunft der resultierenden Zutaten geprüft.
16. Quantitative Stoffdaten werden pro Portion bewertet.
17. Qualitative Stoffdaten werden ohne erfundene Menge bewertet.
18. Unknown erzeugt offenen Prüfkonflikt.
19. Gemischte quantitative/qualitative Daten erzeugen keine scheinexakte Summe.
20. Eindeutige direkte Variante wird automatisch verwendet.
21. Mehrere direkte passende Lösungen werden nicht automatisch gewählt.
22. Mehrere eindeutige unabhängige Direktlösungen können kombiniert werden.
23. Partielle Lösung gilt nicht als vollständige Auflösung.
24. Erst nach erfolgloser direkter Lösung wird OfferGroup-Alternative geprüft.
25. Genau eine passende OfferGroup-Alternative wird automatisch gewählt.
26. Mehrere passende Alternativen bleiben offen.
27. Automatik sucht keine beliebigen Bibliotheksrezepte.
28. Operator kann manuell ein anderes veröffentlichtes Rezept wählen.
29. Manuelle Entscheidungen werden bei Neuberechnung nicht still überschrieben.
30. AlwaysTogether, Ask und NeverTogether wirken wie definiert.
31. Versorgungslösung kann RequirementGroups übererfüllen.
32. Unterdeckung wird sichtbar.
33. Überdeckung wird sichtbar.
34. Nicht zugeordnete geplante Portionen werden sichtbar.
35. Versorgungslösungen mit identischem resultierendem Zustand können zusammengeführt werden.
36. Teilmengen mit unterschiedlichen Anpassungen bleiben getrennte Versorgungslösungen.
37. Subrecipes werden transitiv geprüft.
38. Varianten in Subrecipes werden an konkreter Position gespeichert.
39. Clean Verification funktioniert nur bei konfliktfreiem aktuellem Stand.
40. VerifiedWithDeviation benötigt Begründung.
41. Verifikation sperrt spätere Bearbeitung nicht.
42. Relevante Änderung macht Verifikation Stale.
43. Neue Verifikation ersetzt den alten VerificationSnapshot.
44. Alter vollständiger VerificationSnapshot wird gelöscht.
45. Offline-Package-Roundtrip funktioniert mit Dummy-Teilnehmer-/Sonderverpflegungsdaten.
46. Entwicklungsdokumentation weist ausdrücklich darauf hin, dass dies keine Produktionsfreigabe für echte sensible Offline-Daten ist.
