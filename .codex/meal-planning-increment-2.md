# Codex Implementierungsauftrag: Mahlzeitenplanung – Inkrement 2

> Verbindliche Korrektur vom 2026-10-06: Die direkte personenbezogene CookingUnit-
> Zuordnung und deren Standard-/Meal-Overrides sind durch Camp-Strukturzuordnung,
> Anwesenheit und CookingUnit-Filter ersetzt. Die entsprechenden ursprünglichen
> Abschnitte und Prüffälle unten sind historisch, nicht mehr Implementierungsziel.
> Maßgeblich: ../docs/decisions/meal-planning-participant-structure-correction.md.

## Auftrag

Implementiere **Mahlzeitenplanung – Inkrement 2** für ScoutCampPlanner als nächsten Vertikalschnitt auf Basis des bereits implementierten Inkrement 1.

Inkrement 1 darf nicht neu entworfen oder durch parallele Strukturen ersetzt werden.

Die neue Fachspezifikation ist:

`docs/domain/catering-meal-planning-increment-2.md`

Zusätzlich ist die bestätigte Offline-/Security-Entscheidung zu berücksichtigen. Falls sie nach Repository-Konvention als ADR unter anderem Namen abgelegt wurde, ist die tatsächlich vorhandene Datei maßgeblich.

## 1. Verbindliche Quellen

Lies vor jeder Codeänderung vollständig:

- `.codex/agent-instructions.md`
- `.codex/coding-guidelines.md`
- `.codex/CHATGPT_PROJECT_BRIEF.md`
- `.codex/meal-planning-increment-1.md`
- die Ergebnis-/Abschlussdokumentation von Inkrement 1, sofern vorhanden
- `docs/domain/catering-meal-planning.md`
- `docs/domain/catering-meal-planning-increment-2.md`
- `docs/architecture/architecture-overview.md`
- `docs/architecture/baseline-status.md`
- `docs/architecture/dependency-matrix.md`
- bestehende Domain-Dokumente zu Camp-Struktur, Rezepten, Basiszutaten, Zutatenvarianten/Ersatzzutaten, Allergenen, Unverträglichkeiten, Herkunftsmerkmalen, Nutrition/Stoffdaten und Offline-Packages
- alle relevanten ADRs unter `docs/decisions/`

Prüfe insbesondere die tatsächliche bestehende ADR zur Package-Versionierung/Security.

Priorität bei Widersprüchen:

1. `docs/decisions/`
2. `docs/architecture/`
3. `docs/domain/`
4. `.codex/agent-instructions.md` und `.codex/coding-guidelines.md`
5. dieser Implementierungsauftrag

Falls eine Regel dieses Auftrags einer höher priorisierten Repository-Entscheidung widerspricht, nicht still überschreiben. Widerspruch konkret dokumentieren, alle unabhängigen Teile weiter umsetzen und die betroffene Stelle im Ergebnisbericht markieren.

## 2. Repository-Analyse vor Implementierung

Ermittle zuerst die tatsächlich vorhandenen Dateien/Projekte für:

- Camp Domain/Application/Infrastructure/API,
- Catering Domain/Application/Infrastructure/API,
- bestehende MealPlan-/CookingUnit-/SupplyPlan-Implementierung,
- Angular Camp- und Catering-Features,
- Rezept-/Zutatenmodelle,
- Varianten-/Ersatzbeziehungen,
- Allergene,
- Unverträglichkeiten und quantitative Stoffdaten,
- Herkunftsmerkmale,
- zentrale/mandantenspezifische Kataloge und Contribution-Workflow,
- Rollen/Permissions,
- PostgreSQL-Migrationen,
- SQLite-Migrationen,
- Package Export/Import/Replace,
- lokale Desktop-App,
- Package-Roundtrip- und Securitytests.

Keine neuen Parallelmodelle anlegen, wenn bestehende Konzepte erweitert werden können.

## 3. Architekturgrenzen

Bestehende Modulrichtung beibehalten:

`Platform -> Camp -> Catering`

Teilnehmer sind Camp-owned.

Catering darf Teilnehmerdaten nur über Camp-Contracts konsumieren.

Nicht erlaubt:

- direkte Catering-Abhängigkeit auf Camp Infrastructure,
- fremde DbContexts,
- Cross-Module-EF-Navigation,
- Duplizieren des Teilnehmerstamms in Catering,
- Businesslogik nur in Angular/API statt Domain/Application.

Domain-Code bleibt frameworkfrei.

Keine neuen Frameworks oder Abstraktionsschichten ohne konkreten Bedarf einführen.

## 4. Camp: minimaler Teilnehmerkern

Implementiere nur den in der Fachspezifikation benötigten Camp-owned Teilnehmerkern.

Mindestens:

- stabile Teilnehmer-ID,
- Name/Anzeigename gemäß vorhandener Naming-Konvention,
- Tagesanwesenheit,
- mahlzeitenspezifische Abwesenheit,
- optional primärer DietType,
- Allergenzuordnungen,
- Unverträglichkeitszuordnungen,
- optional individueller Grenzwert pro mengenabhängiger Unverträglichkeit,
- Grenzwert-Herkunftsmetadatum.

Kein vollständiges CRM-/Teilnehmermanagement bauen.

## 5. Tages- und Mahlzeitenanwesenheit

Implementiere ganze Tage als abwesend sowie einzelne Mahlzeiten auf aktiven Tagen als abwesend.

Effektive Teilnehmermenge muss deterministisch ableitbar sein.

Nur relevante Slots invalidieren.

## 6. CookingUnit-Teilnehmerzuordnung

Erweitere das bestehende CookingUnit-Modell um:

- Standard-Teilnehmerzuordnung,
- optionalen Override pro konkreter Mahlzeit,
- Reset auf aktuelle Standardzuordnung.

Pro konkrete Mahlzeit darf ein Teilnehmer höchstens einer CookingUnit zugeordnet sein.

Nicht zugeordnete effektiv anwesende Teilnehmer sichtbar als Planungsproblem ausweisen.

Keine automatische Zuteilung.

## 7. Lagerweite Bedarfsbasis

Implementiere einen lagerweiten, bewusst änderbaren Modus:

- `EstimatedPlanning`
- `UseActualParticipants`

Regeln:

### EstimatedPlanning

Bestehende Inkrement-1-Bedarfsbasis.

### UseActualParticipants

Wenn für den konkreten Kontext reale Teilnehmer vorhanden sind, reale Teilnehmer verwenden.

Nur bei 0 realen Teilnehmerdaten `EstimatedFallback`.

Wenn reale Teilnehmer vorhanden, aber unvollständig/falsch zugeordnet sind:

- kein Fallback,
- reale Daten bleiben maßgeblich,
- Problem/Incomplete sichtbar.

Speichere bzw. liefere für jeden SupplyPlan-Teilstand die tatsächlich verwendete Basis:

- Estimated
- ActualParticipants
- EstimatedFallback

Moduswechsel nur explizit. Rückwechsel erlaubt. Nur tatsächlich betroffene Teilstände Stale setzen.

## 8. Allergene und Unverträglichkeiten am Teilnehmer

Keinen universellen neuen RequirementType für diese beiden Kategorien bauen.

Direkte Referenzen auf die bestehenden Katalogmodelle verwenden.

Für mengenabhängige Unverträglichkeiten:

- optionaler Teilnehmergrenzwert pro Portion,
- optionaler Default im zentralen Katalog,
- Default beim Anlegen kopieren,
- spätere Default-Änderung verändert Teilnehmerwert nicht,
- Herkunft des Teilnehmergrenzwerts als Metadatum.

Änderung nur der Herkunft ohne Grenzwertänderung darf nicht Stale auslösen.

Keine medizinischen Grenzwerte erfinden.

## 9. Stoffbewertung an Zutaten

Erweitere/verwende das bestehende Zutaten-Stoffmodell so, dass pro `Ingredient × Substance` genau einer dieser fachlichen Modi abbildbar ist:

- Quantitative
- Qualitative
- Unknown

Quantitative: konkreter Gehalt pro definierter Bezugsmenge.

Qualitative: contained / not contained.

Unknown: keine belastbare Aussage.

Nicht `Unknown` als `not contained` interpretieren.

Keine fehlenden Mengen schätzen.

Bestehende Daten/Migrationen so behandeln, dass keine falschen Negativ-Aussagen entstehen.

## 10. DietType-Katalog

Implementiere/erweitere einen zentralen DietType-Katalog nach den bestehenden Scope-/Revision-/Contribution-Konventionen des Projekts.

Benötigt:

- zentrale Standardtypen,
- mandantenspezifische zusätzliche Typen,
- zentralen Contribution-Workflow für lokale Vorschläge, sofern der bestehende Katalogmechanismus dafür geeignet ist,
- Name,
- Beschreibung optional,
- Sortierung,
- Regeln pro Hauptherkunft.

Regeln pro Hauptherkunft:

- Allowed
- Excluded

Fehlende Regel:

- Unknown / review required.

Zentrale Definitionen nicht durch Tenant überschreiben.

Keine DietType-Implikationsengine bauen.

DietType-Eignung ausschließlich aus Hauptherkünften der resultierenden Zutaten ableiten.

Bestehende Zusatzflags für tierisches Fett/Lab/Gelatine/etc. nicht als zweite neue DietType-Logik verwenden.

Kein unnötiges Refactoring dieser alten Flags, sofern für Inkrement 2 nicht erforderlich.

## 11. RequirementGroups

RequirementGroups sind abgeleitete Daten für `CookingUnit × Meal`.

Keine manuelle Bearbeitung.

Ableiten aus effektiv anwesenden und effektiv dieser CookingUnit/Mahlzeit zugeordneten realen Teilnehmern.

Gruppenidentität aus:

- DietType optional,
- Allergenmenge,
- Unverträglichkeitsmenge,
- berechnungsrelevanten Grenzwerten.

Grenzwert-Quelle/Auditmetadaten dürfen Gruppen nicht trennen.

Unterschiedliche Grenzwerte müssen unterschiedliche Gruppen ergeben.

Standardgruppe:

- kein DietType,
- keine Allergene,
- keine Unverträglichkeiten.

Keine künstliche Standard-Requirement-ID.

## 12. SupplySolution / Versorgungslösung

Erweitere den bestehenden SupplyPlan um ein fachlich klares Konzept einer Versorgungslösung.

Eine Versorgungslösung umfasst:

- RecipeRevision,
- konkrete Varianten-/Ersatzentscheidungen,
- Portionsmenge,
- explizite Portionszuordnung zu RequirementGroups.

Falls der bestehende SupplyPlan bereits eine geeignete Struktur besitzt, diese erweitern statt eine parallele Aggregate-Hierarchie zu bauen.

Varianten-/Ersatzentscheidungen gelten für die gesamte Portionsmenge der Versorgungslösung.

Unterschiedliche Zubereitungsstände = unterschiedliche Versorgungslösungen.

## 13. Transitive Rezeptauswertung

Eignung über komplette RecipeRevision-Struktur inklusive Subrecipes auswerten.

Änderungen/Ersatzentscheidungen müssen an der konkreten betroffenen Position in der transitiven Rezeptstruktur eindeutig referenzierbar sein.

Bei einer Ersatzzutat ersetzt deren resultierender Beitrag den Originalbeitrag vollständig für:

- Allergene,
- Stoffdaten,
- Hauptherkunft,
- DietType.

Original und Ersatz nicht doppelt auswerten.

## 14. Eignungsbewertung

Bewerte eine Versorgungslösung pro RequirementGroup.

Eine Lösung ist nur dann vollständig geeignet, wenn alle Anforderungen der Gruppe gleichzeitig erfüllt sind.

Allergene: qualitativ über resultierenden Zutatenstand.

DietType: über Hauptherkunft aller resultierenden Zutaten und DietType-Regeln.

Mengenabhängige Unverträglichkeiten: pro Portion.

Wenn alle relevanten Stoffbeiträge quantitativ bekannt und Grenzwert vorhanden: exakte quantitative Prüfung.

Wenn Grenzwert fehlt: qualitative Präsenzprüfung plus Warnung über fehlenden Grenzwert.

Wenn mindestens ein relevanter Beitrag nur `Qualitative = contained` ist: keine exakte Gesamtsumme behaupten, qualitative Konflikt-/Warnbewertung.

Wenn relevanter Beitrag Unknown: offener DataQuality-Konflikt, Clean Verification blockiert.

## 15. Automatische Lösung

Implementiere feste Reihenfolge:

1. direkte Varianten/Ersatzzutaten,
2. OfferGroup-Alternativen,
3. offener Konflikt/manuelle Entscheidung.

Direkte Lösung nur automatisch anwenden, wenn genau eine eindeutige vollständige Lösung existiert.

Mehrere eindeutige unabhängige Direktlösungen dürfen kombiniert werden, wenn sie unterschiedliche Positionen betreffen, kompatibel sind und die Gesamtlösung alle Anforderungen erfüllt.

Keine rekursive Ersatzkette.

Partielle Lösung anzeigen, aber nicht als vollständig gelöst behandeln.

OfferGroup-Alternativen nur prüfen, wenn keine eindeutige vollständige direkte Lösung existiert.

Genau eine passende Alternative: automatisch auswählen.

Mehrere passende Alternativen: keine automatische Auswahl.

Keine Priorität aus UI-Sortierung ableiten.

Keine zukünftige `Normal -> Vegetarisch -> Vegan`-Priorität vorwegnehmen.

Automatik sucht keine beliebigen Rezepte aus der Bibliothek.

Operator darf manuell ein anderes veröffentlichtes Rezept auswählen.

## 16. Manuelle Entscheidungen

Automatisch gewählte Lösungen dürfen manuell überschrieben werden.

Manuelle Entscheidungen persistieren.

Neuberechnung: neu bewerten, nicht still ersetzen.

Wenn weiterhin geeignet: beibehalten.

Wenn nicht mehr geeignet: strukturiertes Problem, weiterhin sichtbar, Clean Verification nicht möglich, VerifiedWithDeviation möglich.

## 17. Gemeinsamkeitspräferenz

Erweitere CookingUnit um genau eine Präferenz:

- AlwaysTogether
- Ask
- NeverTogether

Kein Meal-Override.

Die Präferenz wirkt auf die gesamte Versorgungslösung.

AlwaysTogether: gemeinsame geeignete Lösung für mehrere RequirementGroups bevorzugen, solange keine Gruppe dadurch ungeeignet wird.

Ask: nur fragen, wenn sowohl gemeinsame als auch getrennte Versorgung fachlich möglich ist.

NeverTogether: Gruppen getrennt behandeln, außer sie benötigen ohnehin exakt gleiche RecipeRevision plus identische Varianten-/Ersatzentscheidungen.

Bei mehreren fachlich geeigneten gemeinsamen Lösungen keine automatische Optimierung.

Keine Kriterien wie billigste/einfachste/wenigste Varianten erfinden.

## 18. Portions- und Coverage-Zuordnung

Jede SupplySolution besitzt explizite Portionszuordnungen zu RequirementGroups.

Eine Solution darf mehrere Gruppen versorgen.

Eignung je Gruppe separat prüfen.

Übererfüllung ist erlaubt.

Rechnerische Abdeckung prüfen und strukturiert anzeigen:

- RequirementGroupUndercovered
- RequirementGroupOvercovered
- UnassignedPlannedPortions

Diese Probleme blockieren die Arbeitsplanung nicht.

Sie blockieren Clean Verification.

## 19. Zusammenführung identischer SupplySolutions

Zusammenführen nur bei:

- gleicher RecipeRevision,
- identischen Varianten-/Ersatzentscheidungen,
- sonst identischem versorgungsrelevantem Zustand.

Portionsmengen und Group-Zuordnungen addieren.

Unterschiedliche Group-Zuordnung allein verhindert Merge nicht.

## 20. Problem-/Konfliktmodell

Ein gemeinsames strukturiertes Problem-Modell verwenden, sofern dies mit bestehenden Projektmustern vereinbar ist.

Obertypen mindestens:

- Suitability
- Coverage
- DataQuality
- ManualDecision

ReasonCodes mindestens:

- NoMatchingDirectSolution
- MultipleDirectSolutions
- PartialDirectResolution
- NoMatchingOfferAlternative
- MultipleMatchingOfferAlternatives
- UnknownSubstanceData
- QualitativeOnlySubstanceData
- ManualDecisionNoLongerSuitable
- RequirementGroupUndercovered
- RequirementGroupOvercovered
- UnassignedPlannedPortions

Keine reine Freitext-Konfliktlogik.

Freitext nur ergänzend.

Probleme grundsätzlich aus dem aktuellen Stand ableiten.

## 21. Verifikation

Verifikation pro `CookingUnit × Meal`.

Separate Permission nach bestehender Konvention für MealPlanning Edit und MealPlanning Verify.

Keine feste neue Rolle erfinden.

Kein Vier-Augen-Zwang.

### Clean Verification

Nur möglich bei:

- Current,
- vollständig,
- alle RequirementGroups versorgt,
- keine offenen relevanten Konflikte,
- keine Coverage-Abweichungen,
- keine relevanten Unknown-Daten,
- alle manuellen Entscheidungen auf aktuellem Stand geeignet.

### VerifiedWithDeviation

Mit Verify-Permission möglich, wenn Clean nicht möglich ist.

Zwingend Begründung speichern.

Verifikation sperrt den SupplyPlan nicht.

Spätere relevante Änderung -> Verification stale.

## 22. VerificationSnapshot

Pro `CookingUnit × Meal` höchstens ein aktueller VerificationSnapshot.

Neue Verifikation:

- neuen Snapshot erzeugen,
- alten Snapshot ersetzen,
- alten vollständigen Snapshot löschen.

Keine Snapshot-Historie aufbauen.

Minimal persistieren/referenzieren:

- CookingUnitId,
- MealSlot,
- MealPlan-Version/Snapshot,
- effektive Bedarfsbasis,
- Gesamtbedarf,
- fachlich relevante RequirementGroup-Signaturen,
- SupplySolutions,
- RecipeRevision-Referenzen,
- Varianten-/Ersatzentscheidungen,
- Portionsmengen,
- RequirementGroup-Zuordnungen,
- relevante Problem-/Konfliktergebnisse,
- VerificationType,
- Begründung bei Abweichung,
- VerifiedBy,
- VerifiedAt.

Unveränderliche/reproduzierbare Stammdaten über IDs/Revisionen referenzieren statt vollständig duplizieren.

Audit-Metadaten separat nach bestehender Projektkonvention.

## 23. Invalidierung

Implementiere selektive Invalidierung.

Nur tatsächlich betroffene `CookingUnit × Meal`-Stände Stale setzen/erkennen.

Mindestens relevant:

- Teilnehmerzuordnung,
- Meal-Teilnehmeroverride,
- Tages-/Meal-Anwesenheit,
- DietType am Teilnehmer,
- Allergene,
- Unverträglichkeiten,
- Teilnehmergrenzwert,
- relevante DietType-Regel,
- Hauptherkunft verwendeter Zutat,
- Allergenkennzeichnung verwendeter Zutat,
- Stoffmodus,
- Stoffgehalt,
- verwendete RecipeRevision/SubrecipeRevision,
- relevante Varianten-/Ersatzdefinition,
- manuelle Ersatzentscheidung,
- Solution-Portionsmenge,
- Group-Zuordnung,
- effektiver Wechsel der Bedarfsbasis,
- relevante Regeln aus Inkrement 1.

Nicht Stale wegen reiner:

- ThresholdSource-Änderung bei identischem Grenzwert,
- Sortierung,
- Anzeigenamen,
- Beschreibungen,
- irrelevanten nicht referenzierten Stammdatenänderungen.

Bestehende Verification für denselben Kontext ebenfalls stale markieren/erkennen.

## 24. Offline und Security

Teilnehmer- und Sonderverpflegungsdaten in den bestehenden Camp-Package-Roundtrip integrieren.

Aktuelle Entwicklungsphase:

- ausschließlich Dummy-/Testdaten,
- daher Transport im bestehenden Entwicklungs-Package zulässig.

Dies darf in Code/Dokumentation nicht als Produktionsfreigabe für reale sensible Daten dargestellt werden.

Bestehendes Freeze-/Replace-Modell unverändert.

Package v2/Security nicht eigenmächtig neu definieren.

Prüfe bestehende ADRs.

Falls die bestätigte Projektentscheidung zum Entwicklungs-Transport noch nicht in `docs/decisions/` dokumentiert ist, integriere die bereitgestellte Entscheidungsnotiz nach bestehender ADR-Konvention. Keine erfundene ADR-Nummer verwenden.

## 25. PostgreSQL und SQLite

Alle neuen Persistenzänderungen für beide Provider nach bestehenden Modulkonventionen implementieren.

Migrationen erforderlich für betroffene Camp-/Catering-Daten.

Businesslogik nicht zwischen Providern duplizieren.

Migrationsupgrade vom aktuellen Inkrement-1-Baseline-Stand testen.

## 26. API und Angular

Mindestens folgende Abläufe ermöglichen:

### Teilnehmer

- minimalen Teilnehmer anlegen/bearbeiten/löschen,
- Anwesenheitstage pflegen,
- Mahlzeitenabwesenheiten pflegen,
- DietType auswählen,
- Allergene auswählen,
- Unverträglichkeiten auswählen,
- individuellen Grenzwert anzeigen/ändern,
- Herkunft des Grenzwerts nachvollziehbar anzeigen.

### DietTypes

- zentrale Typen anzeigen,
- Tenant-Typen verwalten,
- Herkunftsregeln pflegen,
- Contribution-Workflow entsprechend vorhandenem Muster nutzen.

### CookingUnit

- Standard-Teilnehmerzuordnung,
- Meal-Override,
- Reset auf Standard,
- Gemeinsamkeitspräferenz.

### Lager

- sichtbarer globaler Modus EstimatedPlanning / UseActualParticipants,
- bewusste Umschaltung,
- tatsächlich verwendete Bedarfsbasis pro relevantem SupplyPlan-Kontext anzeigen.

### SupplyPlan

- RequirementGroups anzeigen,
- automatische Lösungen anzeigen,
- Herkunft Auto/Manual einer Entscheidung sichtbar machen,
- SupplySolutions und Portionen bearbeiten,
- RequirementGroup-Zuordnung bearbeiten,
- strukturierte Probleme anzeigen,
- manuelle RecipeRevision auswählen,
- Neuberechnung,
- Current/Stale/Incomplete.

### Verification

- Clean Verification,
- VerifiedWithDeviation mit Pflichtbegründung,
- stale Verification sichtbar,
- VerifiedBy/VerifiedAt anzeigen.

Keine unnötige UI für zukünftige Einkaufs-/Logistikmodule bauen.

## 27. Tests

Mindestens automatisiert abdecken:

### Camp Domain/Application

- Tagesanwesenheit,
- Mahlzeitenabwesenheit,
- effektive Anwesenheit,
- Teilnehmer-DietType,
- Allergene,
- Unverträglichkeiten,
- Default-Grenzwert wird kopiert,
- spätere Default-Änderung verändert Teilnehmerwert nicht,
- ThresholdSource-Änderung ist nicht berechnungsrelevant.

### CookingUnit/Zuordnung

- Standard-Teilnehmerzuordnung,
- Meal-Override,
- Reset,
- Doppelzuordnung verhindert,
- nicht zugeordnete Teilnehmer erkannt.

### Bedarfsbasis

- EstimatedPlanning,
- UseActualParticipants,
- ActualParticipants,
- EstimatedFallback nur bei 0 realen Teilnehmerdaten,
- kein Fallback bei vorhandenen aber fehlerhaften realen Daten,
- reversibler Modus,
- selektive Invalidierung.

### RequirementGroups

- automatisch abgeleitet,
- disjunkt,
- Standardgruppe,
- identische Anforderungen zusammengeführt,
- unterschiedliche Grenzwerte getrennt,
- Metadata Source trennt nicht.

### DietTypes

- zentrale Regeln,
- Tenant-Typen,
- Allowed,
- Excluded,
- fehlende Regel -> Unknown,
- Hauptherkunftsänderung invalidiert relevante Stände.

### Stoffdaten

- Quantitative,
- Qualitative contained,
- Qualitative not contained,
- Unknown,
- Mischdaten,
- keine Scheingenauigkeit,
- quantitative Prüfung pro Portion,
- fehlender Grenzwert -> qualitative Fallbackbewertung + Warnung.

### Varianten/Ersatz

- eindeutige Direktlösung automatisch,
- mehrere Direktlösungen offen,
- mehrere unabhängige eindeutige Anpassungen kombiniert,
- partielle Auflösung nicht vollständig,
- keine rekursive Ersatzkette,
- vollständiger Ersatz des Originalbeitrags,
- Subrecipe-Auswertung.

### OfferGroup-Alternativen

- erst nach Direktlösung,
- genau eine -> automatisch,
- mehrere -> offen,
- keine UI-Sortierpriorität,
- keine Bibliothekssuche.

### SupplySolutions

- mehrere Groups pro Solution,
- Übererfüllung,
- Undercoverage,
- Overcoverage,
- Unassigned portions,
- Merge identischer Lösungen,
- Trennung bei unterschiedlichen Varianten,
- manuelle Entscheidungen persistieren,
- ungeeignete manuelle Entscheidung markiert.

### Gemeinsamkeitspräferenz

- AlwaysTogether,
- Ask,
- NeverTogether,
- identische Solution bei NeverTogether darf zusammengeführt werden,
- Mehrdeutigkeit führt nicht zu Auto-Optimierung.

### Verification

- Clean Voraussetzungen,
- Unknown blockiert Clean,
- Coverage-Probleme blockieren Clean,
- VerifiedWithDeviation verlangt Begründung,
- separate Permission,
- kein Lock,
- relevante Änderung -> stale,
- neue Verification ersetzt alten Snapshot,
- alter Snapshot gelöscht.

### Persistenz

Für PostgreSQL und SQLite.

### Offline Package

Roundtrip mit Dummy-Daten:

PostgreSQL -> Export -> SQLite -> Teilnehmer/Sonderverpflegung lokal ändern -> Return Package -> PostgreSQL Replace

Prüfen:

- Teilnehmer,
- Anwesenheit,
- Anforderungen,
- DietTypes soweit package-relevant,
- Zuordnungen,
- RequirementGroups/ableitbare Stände entsprechend Design,
- SupplySolutions,
- Konflikt-/Problemzustand soweit persistiert,
- VerificationSnapshot,
- Permissions/Isolation,
- atomarer Replace,
- Rollback,
- Baseline-/Transfer-Validierung.

## 28. Manueller Prüfablauf

Mindestens dokumentiert ausführen, soweit Entwicklungsumgebung verfügbar:

1. Camp mit Inkrement-1-MealPlan/CookingUnits vorbereiten.
2. Teilnehmer mit unterschiedlichen Anwesenheitstagen anlegen.
3. einzelne Mahlzeitenabwesenheiten setzen.
4. DietTypes/Allergene/Unverträglichkeiten erfassen.
5. zwei Teilnehmer mit gleicher Unverträglichkeit aber unterschiedlichen Grenzwerten anlegen.
6. Standard-Teilnehmerzuordnung setzen.
7. Meal-Override erzeugen.
8. Doppelzuordnung versuchen und Ablehnung prüfen.
9. Teilnehmer bewusst unzugeordnet lassen und Problem prüfen.
10. EstimatedPlanning berechnen.
11. auf UseActualParticipants umschalten.
12. ActualParticipants-Basis prüfen.
13. Kontext ohne reale Teilnehmer prüfen -> EstimatedFallback.
14. vorhandene, aber unvollständige reale Zuordnung prüfen -> kein Fallback.
15. RequirementGroups prüfen.
16. Rezept ohne Konflikt prüfen.
17. Rezept mit eindeutig lösbarer Direktvariante prüfen.
18. mehrere eindeutige Positionsanpassungen kombinieren.
19. partielle Lösung prüfen.
20. mehrere direkte Alternativen -> bewusste Auswahl.
21. genau eine OfferGroup-Alternative -> automatische Auswahl.
22. mehrere OfferGroup-Alternativen -> offen.
23. manuelles anderes Bibliotheksrezept wählen.
24. quantitative Stoffprüfung durchführen.
25. qualitative Stoffprüfung durchführen.
26. Unknown prüfen.
27. SupplySolution mehreren RequirementGroups zuordnen.
28. Under-/Overcoverage erzeugen.
29. Unassigned planned portions erzeugen.
30. Gemeinsamkeitspräferenz für alle drei Modi prüfen.
31. Clean Verification durchführen.
32. relevanten Teilnehmerwert ändern -> SupplyPlan/Verification stale.
33. Neuberechnen; manuelle Entscheidung muss bestehen bleiben.
34. VerifiedWithDeviation mit Begründung durchführen.
35. erneut verifizieren und prüfen, dass alter VerificationSnapshot ersetzt/gelöscht wurde.
36. Offline-Package mit Dummy-Daten exportieren.
37. lokal bearbeiten.
38. Return Package importieren.
39. Roundtrip und Freeze/Replace prüfen.
40. klar prüfen/dokumentieren, dass kein Produktionsfreigabeversprechen für echte sensible Offline-Daten besteht.

## 29. Dokumentation nach Implementierung

Mindestens aktualisieren:

- `docs/domain/catering-meal-planning-increment-2.md`
- bestehendes Mahlzeitenplanungs-Domänendokument, falls Regeln dort konsolidiert werden
- Teilnehmer-/Camp-Dokumentation
- Zutaten-/Unverträglichkeits-/Herkunftsdokumentation
- relevante Architekturübersicht
- Baseline-Status
- Package-/Offline-Dokumentation
- relevante ADR/Decision zur Dummy-Daten-Offlineentwicklung
- `.codex/CHATGPT_PROJECT_BRIEF.md`

Dokumentiere ausdrücklich:

- Inkrement 2 implementiert,
- Entwicklungs-Offline-Roundtrip mit Dummy-Daten,
- keine Produktionsfreigabe für echte sensible Offlinepakete,
- verbleibende Security-Stufe.

## 30. Ergebnisdatei

Erstelle am Ende verpflichtend:

`.codex/meal-planning-increment-2-result.md`

Diese Datei wird anschließend in ChatGPT für das fachliche/architektonische Review hochgeladen.

Sie muss auch bei PARTIALLY_COMPLETED oder BLOCKED erstellt werden.

### Pflichtstruktur

#### 1. Ergebnisstatus

Genau:

- COMPLETED
- PARTIALLY_COMPLETED
- BLOCKED

mit kurzer Begründung.

#### 2. Implementierter Umfang

Konkrete tatsächlich implementierte Funktionalität:

- Teilnehmerkern,
- Anwesenheit,
- CookingUnit-Zuordnung,
- globaler Bedarfsmodus,
- Allergene/Unverträglichkeiten,
- Stoffmodi,
- DietTypes,
- RequirementGroups,
- SupplySolutions,
- automatische Konfliktauflösung,
- manuelle Entscheidungen,
- Gemeinsamkeitspräferenz,
- Problemklassifikation,
- Verifikation,
- Offline-Package,
- Angular.

#### 3. Nicht umgesetzt

Trennen in:

- bewusst außerhalb Scope,
- offen geblieben aus Inkrement 2,
- technisch blockiert.

#### 4. Architektur-/Fachabweichungen

Für jede Abweichung:

- Soll-Regel,
- tatsächliche Umsetzung,
- Grund,
- empfohlene Entscheidung.

Oder:

`Keine bekannten Abweichungen.`

#### 5. Geänderte Repository-Bereiche

Gruppiert:

- Camp Domain/Application/Infrastructure/API,
- Catering Domain/Application/Infrastructure/API,
- Angular,
- Migrationen,
- Package/Desktop,
- Tests,
- Dokumentation.

#### 6. Datenmodell und Migrationen

PostgreSQL und SQLite getrennt:

- Migrationen,
- Tabellen/Spalten/Constraints,
- Datenmigration bestehender Stoff-/Zutatendaten,
- Upgrade-Test vom Inkrement-1-Stand.

#### 7. Teilnehmer- und Requirement-Modell

Dokumentieren:

- tatsächliches Ownership,
- Anwesenheitsmodell,
- Zuordnung,
- DietType,
- Allergene,
- Unverträglichkeiten,
- Grenzwerte,
- RequirementGroup-Ableitung.

#### 8. Eignungs-/Konfliktlogik

Dokumentieren:

- Direct -> OfferAlternative -> Manual,
- Variantenkombination,
- quantitative/qualitative/Unknown-Auswertung,
- Subrecipes,
- konkrete implementierte Problemcodes.

#### 9. Verifikation

Dokumentieren:

- Permissions,
- Clean-Regeln,
- WithDeviation,
- Snapshot-Inhalt,
- Ersetz-/Löschverhalten,
- stale-Verhalten.

#### 10. Offline/Security

Dokumentieren:

- transportierte neue Daten,
- Package-Version,
- Roundtrip,
- Freeze/Replace,
- Rollback,
- Security-Grenze,
- explizit bestätigen, dass nur Dummy-/Testdaten als Entwicklungsfreigabe gelten.

#### 11. Automatisierte Prüfungen

Tabelle:

| Prüfung | Ergebnis | Details |
|---|---|---|

Mindestens:

- .NET Build
- vollständige .NET Tests
- Camp Tests
- Catering Tests
- Architekturtests
- PostgreSQL Integration
- SQLite Integration
- Package Roundtrip
- Security/Authorization Tests
- Angular Tests
- Angular Production Build
- Desktop/Installer Build, sofern durch Änderungen betroffen

Ergebnis:

- PASS
- FAIL
- NOT_RUN

#### 12. Manueller Prüfablauf

Nur tatsächlich ausgeführte Schritte dokumentieren.

Nicht ausgeführte Schritte ausdrücklich als NOT_RUN nennen.

#### 13. Invalidierungsprüfung

Explizit dokumentieren, welche Fälle für selektives Stale getestet wurden und welche nicht.

#### 14. Dokumentationsänderungen

Alle aktualisierten Dokumente und deren Zweck.

#### 15. Offene Punkte/Risiken

Trennen:

- Blocker vor Commit,
- nicht-blockierende Grenzen,
- Produktions-Security,
- zukünftige fachliche Punkte.

#### 16. Git-/Commit-Stand

- bestehende Fremdänderungen vor Start,
- Dateien dieses Auftrags,
- Commit-Bereitschaft,
- vorgeschlagene Commit Message.

Keinen Commit durchführen, sofern nicht separat beauftragt.

Vorschlag:

`Implement meal planning increment 2`

#### 17. Review-Hinweise für ChatGPT

Maximal 10 konkrete Punkte, die beim anschließenden Review besonders geprüft werden sollen.

## 31. Abschlussprüfung

Vor Abschluss:

- vollständiger .NET Build,
- automatisierte Tests,
- PostgreSQL,
- SQLite,
- Architekturtests,
- Package-Roundtrip,
- Authorization/Security-Regressiontests,
- Angular Tests,
- Angular Production Build,
- betroffene Desktop-/Installer-Builds,
- manuellen Prüfablauf soweit möglich,
- Dokumentation,
- Ergebnisdatei.

Keine Testergebnisse erfinden.

Nicht ausgeführte Prüfungen als NOT_RUN dokumentieren.

## 32. Commit-Punkt

Ein gemeinsamer Commit ist erst sinnvoll, wenn:

- fachlicher Vertikalschnitt vollständig,
- beide DB-Provider migriert,
- Offline-Roundtrip mit Dummy-Daten funktionsfähig,
- Verifikation funktionsfähig,
- Tests erfolgreich oder bekannte Abweichungen dokumentiert,
- Dokumentation aktualisiert,
- `.codex/meal-planning-increment-2-result.md` erstellt.

Keinen Commit ohne separaten Auftrag durchführen.
