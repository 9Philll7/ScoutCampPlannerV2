# Codex Implementierungsauftrag: Mahlzeitenplanung – Inkrement 1

## Auftrag

Implementiere den ersten Vertikalschnitt der Mahlzeitenplanung für ScoutCampPlanner.

Der fachliche Umfang ist in:

`docs/domain/catering-meal-planning.md`

verbindlich beschrieben.

Implementiere ausschließlich **Inkrement 1**.

Funktionen aus dem dort beschriebenen Inkrement 2 dürfen nicht vorgezogen werden.

---

## Verbindliche Quellen

Vor Beginn vollständig lesen:

- `.codex/agent-instructions.md`
- `.codex/coding-guidelines.md`
- `.codex/CHATGPT_PROJECT_BRIEF.md`
- `docs/domain/catering-meal-planning.md`
- `docs/architecture/architecture-overview.md`
- `docs/architecture/baseline-status.md`
- `docs/architecture/dependency-matrix.md`
- bestehende Catering-Rezept-/Zutaten-Domänendokumente
- bestehende ADRs zu:
  - Modularchitektur;
  - Camp-Packages;
  - Migrationen;
  - Authentifizierung/Autorisierung;
  - Rollen und Permissions.

Die Repository-Dokumentation hat Vorrang vor Annahmen dieses Auftrags.

Falls der Auftrag einer bestehenden ADR oder Domain-Regel widerspricht:

1. Widerspruch dokumentieren;
2. betroffene Datei/Entscheidung nennen;
3. an dieser Stelle nicht stillschweigend weiterimplementieren.

Keine Architekturänderung still einführen.

---

## Vor der ersten Codeänderung

Ermittle die tatsächlich vorhandenen Repository-Dateien und Projekte für:

- Catering Domain;
- Catering Application;
- Catering Infrastructure;
- Catering API/Endpoints;
- Angular Catering Features;
- Camp Contracts für Struktur und Planungszahlen;
- Permission-/Authorization-Katalog;
- PostgreSQL Catering-Migrationen;
- SQLite Catering-Migrationen;
- Camp-Package Export;
- Camp-Package Import/Replace;
- Package-Roundtrip-Tests;
- bestehende Rezeptbibliotheks- und Rezeptrevisions-Use-Cases.

Keine Pfade oder Schichten neu erfinden, wenn dafür bereits bestehende Konventionen vorhanden sind.

---

## Architekturregeln

Bestehende Modulrichtung beibehalten:

`Platform → Camp → Catering`

Catering darf Camp ausschließlich über definierte Contracts konsumieren.

Nicht erlaubt:

- direkter Zugriff auf Camp Infrastructure;
- fremde DbContexts;
- Cross-Module-EF-Navigation;
- neue Modulabhängigkeiten entgegen der bestehenden Dependency Matrix.

Domain-Code bleibt frei von:

- ASP.NET Core;
- Entity Framework;
- Datenbankdetails;
- UI-Abhängigkeiten.

Keine neuen Framework-Abstraktionen ohne konkreten Bedarf einführen.

Insbesondere nicht automatisch einführen:

- MediatR;
- AutoMapper;
- CQRS;
- generische Repositorys;
- Event Bus.

PostgreSQL und SQLite müssen unterstützt werden.

Businesslogik darf nicht providerabhängig doppelt implementiert werden.

---

# 1. Domain-Modell

## 1.1 MealPlan

Implementiere ein Catering-owned, Camp-scoped `MealPlan`.

Mindestens:

- stabile ID;
- CampId;
- Name;
- Sortierreihenfolge;
- monotone fachliche Version.

Ein Lager darf mehrere MealPlans besitzen.

Kein Draft-/Active-Status.

MealPlans dürfen unvollständig gespeichert werden.

## 1.2 MealPlan-Bearbeitungsmodus

Fachliche Änderungen werden gesammelt und erst mit bewusster Save-Aktion übernommen.

Save:

- validiert den neuen Gesamtstand;
- erzeugt genau eine neue fachliche Version;
- erzeugt einen unveränderlichen fachlichen Snapshot;
- macht diesen Stand operativ wirksam.

Cancel:

- verwirft alle noch nicht gespeicherten Änderungen.

Kein Autosave fachlich relevanter Änderungen.

Während einer offenen Bearbeitung bleibt der zuletzt gespeicherte Stand maßgeblich.

## 1.3 MealPlan-Version/Snapshot

Versorgungsrelevante Änderungen erhöhen die fachliche Version.

Reine Darstellungsänderungen erhöhen sie nicht.

Ein Verpflegungs-Teilstand muss dauerhaft auf den verwendeten MealPlan-Snapshot bzw. dessen eindeutigen fachlichen Stand referenzieren können.

Keine unbegrenzte Retention erzwingen.

Referenzierte Snapshots dürfen nicht gelöscht werden.

Keine konkrete Retention-Dauer erfinden.

---

# 2. MealPlanOfferGroup

Implementiere eine `MealPlanOfferGroup` pro konkrete MealPlan-Mahlzeit.

Mindestens:

- stabile ID;
- MealPlan-Bezug;
- konkreter Meal-Slot;
- optionaler Anzeigename;
- Sortierreihenfolge.

Jede OfferGroup muss genau ein Standard-MealPlanEntry besitzen.

Weitere Entries sind Alternativen.

Auch Einzelangebote ohne Alternativen werden über eine OfferGroup modelliert.

---

# 3. MealPlanEntry

Mindestens:

- stabile ID;
- OfferGroupId;
- veröffentlichte RecipeRevisionId;
- Standard-/Alternative-Kennzeichnung;
- optionaler Anzeigename;
- optionaler Rollen-Qualifier;
- optionaler Hinweis;
- Sortierreihenfolge.

Regeln:

- nur veröffentlichte unveränderliche Rezeptrevisionen;
- keine RecipeDrafts;
- dieselbe RecipeRevisionId innerhalb derselben OfferGroup höchstens einmal;
- dieselbe RecipeRevisionId in verschiedenen OfferGroups derselben Mahlzeit erlaubt;
- bestehendes Entry darf auf andere veröffentlichte Revision umgestellt werden;
- lokale Angaben des Entry bleiben beim Wechsel erhalten;
- keine eigene Entry-Revisionierung.

---

# 4. Rollen-Qualifier

Verwende das bestehende Katalog-/Enum-Muster des Projekts.

Startwerte:

- MainDish / Hauptspeise
- SideDish / Beilage
- Starter / Vorspeise
- Dessert / Nachspeise
- Beverage / Getränk
- Other / Sonstiges

Optional.

Keine Ernährungsformen aufnehmen.

Insbesondere nicht:

- vegetarian;
- vegan.

Beim Erzeugen eines MealPlanEntry den Rollen-Qualifier der Rezeptrevision nur als initialen Wert übernehmen.

Spätere Änderungen der Rezeptrevision dürfen bestehende MealPlanEntries nicht still verändern.

Keine Mengenlogik an den Rollen-Qualifier hängen.

---

# 5. Lager-Rezeptbibliothek

Ein MealPlan darf jede zugängliche veröffentlichte Rezeptrevision verwenden.

Beim erstmaligen lagerbezogenen Einsatz einer Revision:

- Revision automatisch in die Camp-Rezeptbibliothek aufnehmen.

Solange eine lagerbezogene Referenz besteht:

- Entfernung aus der Camp-Rezeptbibliothek blockieren;
- blockierende Referenzen verständlich zurückgeben.

Nicht mehr verwendete Revisionen:

- nicht automatisch entfernen.

Keine automatische Bibliotheksbereinigung implementieren.

Die vollständige Camp-Rezeptbibliothek bleibt Offline-Closure-Root.

---

# 6. CookingUnit

Implementiere `CookingUnit` als Catering-owned, Camp-scoped Objekt.

Mindestens:

- stabile ID;
- CampId;
- Name;
- Sortierreihenfolge;
- optional CookingUnitGroupId;
- optional StandardMealPlanId;
- Standard-Strukturzuordnung.

Keine eigene Revisionierung.

Kein Active-Flag.

Use Cases mindestens:

- anlegen;
- ändern;
- sortieren;
- löschen;
- optional aus Camp-Strukturknoten initialisieren.

Beim Erzeugen aus Camp-Struktur:

- Name und initiale Strukturreferenz dürfen übernommen werden;
- danach keine versteckte dauerhafte Kopplung erzeugen.

Löschen:

- zugehörige mutable operative CookingUnit-Planungsdaten entfernen;
- andere fachliche Objekte nicht löschen;
- betroffene Kalkulationen/Aggregate veralten lassen bzw. entsprechend erkennen.

---

# 7. CookingUnitGroup

Implementiere einstufige organisatorische Gruppen.

Mindestens:

- stabile ID;
- CampId;
- Name;
- Sortierreihenfolge.

Regeln:

- keine Untergruppen;
- CookingUnit höchstens in einer Gruppe;
- keine Bedarfslogik;
- keine Einstellungsvererbung.

Gruppenlöschung:

- entfernt nur Gruppe/Zuweisung;
- löscht keine CookingUnit.

Eine separate explizite Massenlöschung ausgewählter CookingUnits darf vorgesehen werden.

---

# 8. Camp-Strukturzuordnung

## 8.1 Standard

Eine CookingUnit besitzt eine Standard-Zuordnung zu einem oder mehreren vollständigen Camp-Strukturknoten.

Camp-Contracts verwenden.

Keine direkte Camp-Infrastructure referenzieren.

## 8.2 Mahlzeitenbezogene Abweichung

Für eine konkrete Mahlzeit darf eine eigene Strukturzuordnung existieren.

Slots ohne Override folgen der aktuellen Standardzuordnung.

Slots mit Override bleiben von späteren Standardänderungen unberührt.

Expliziten Use Case zum Zurücksetzen auf Standard vorsehen.

## 8.3 Hierarchieprüfung

Innerhalb derselben effektiven Zuordnung Parent+Child-Kombinationen verhindern.

Überlappungen zwischen verschiedenen CookingUnits zulassen.

Nicht automatisch auflösen.

Hinweise auf resultierende Über-/Unterdeckung ermöglichen.

---

# 9. MealPlan-Abonnement

Eine CookingUnit kann höchstens einen Standard-MealPlan abonnieren.

Pro konkrete Mahlzeit exakt folgende fachliche Zustände unterstützen:

- FollowStandard
- Custom
- NoSupplyRequired

FollowStandard:

- folgt der aktuellen gespeicherten MealPlan-Version automatisch.

Custom:

- bleibt bei späteren Standardplanänderungen unverändert.

NoSupplyRequired:

- bleibt ebenfalls unverändert.

Expliziten Use Case `ResetToStandardPlan` oder äquivalente bestehende Konvention vorsehen.

Keine stillen Überschreibungen lokaler Abweichungen.

---

# 10. Bedarf

Berechne für `CookingUnit × Meal` standardmäßig den Bedarf aus:

- effektiver Camp-Strukturzuordnung;
- vorhandenen anonymen KiJu-/Leiter-Planungszahlen;
- vorhandenen Catering-Verpflegungsfaktoren.

Nur vollständige Camp-Strukturknoten verwenden.

Keine prozentualen/teilweisen Knotenzuordnungen implementieren.

Optionaler manueller Bedarfsoverride ausschließlich pro konkreter Mahlzeit.

Kein Standard-Override auf CookingUnit-Ebene.

---

# 11. Verpflegungsplan

Implementiere einen lagerbezogenen operativen Verpflegungsplan.

Teilstände:

`CookingUnit × konkrete aktive Mahlzeit`

Ein Teilstand muss mindestens nachvollziehen können:

- verwendeten MealPlan-Snapshot bzw. Versionsstand;
- effektive Strukturzuordnung;
- relevante verwendete Planungsdaten;
- relevante Verpflegungsfaktoren;
- berechneten Gesamtbedarf;
- manuellen Bedarfsoverride;
- OfferGroup-Zielbedarfe;
- OfferGroup-Zielbedarfs-Overrides;
- konkrete Rezept-/Portionsauswahl;
- individuelle RecipeRevision-Auswahl;
- relevante manuelle Entscheidungen;
- Berechnungszeitpunkt oder äquivalenten Datenstandsmarker.

Keine Live-/Background-Neuberechnung auf jede Datenänderung.

Berechnung über expliziten Use Case.

---

# 12. Status

Ein Verpflegungs-Teilstand kennt genau:

- Current
- Stale
- Incomplete

Semantik gemäß `docs/domain/catering-meal-planning.md`.

Keine Verified-Zustände.

Keine VerificationSnapshots.

---

# 13. Neuberechnung

Explizite Neuberechnung:

- aktualisiert automatisch abgeleitete Bestandteile;
- überschreibt keine bewussten manuellen Entscheidungen still.

Sind manuelle Entscheidungen nach der Neuberechnung auffällig oder inkonsistent:

- Hinweis/Conflict erzeugen;
- manuelle Entscheidung bestehen lassen.

---

# 14. OfferGroup-Zielbedarf

Für jede OfferGroup im konkreten Verpflegungsplan:

Default:

`OfferGroupTarget = CookingUnitMealDemand`

Optionaler Override auf Ebene:

`CookingUnit × Meal × OfferGroup`

Der Override gehört nicht in den zentralen MealPlan.

Innerhalb einer OfferGroup teilen Standard und Alternativen den Zielbedarf.

Mehrere OfferGroups parallel berechnen.

Keine automatische Sonderverpflegungsverteilung implementieren.

---

# 15. Individuelle Rezeptwahl

Custom-Mahlzeiten dürfen:

- zentrale MealPlanEntries verwenden;
- direkt andere veröffentlichte RecipeRevisions verwenden;
- beide Formen mischen.

Individuelle RecipeRevisions ebenfalls automatisch in Camp-Rezeptbibliothek aufnehmen.

Alle Auswahlen bleiben an den konkreten Meal-Slot gebunden.

Keine individuelle Auswahl automatisch in den globalen MealPlan schreiben.

---

# 16. Vollständigkeit und Plausibilität

MealPlan-Vollständigkeit als einfache Plausibilitätsauswertung.

Aktive Lagermahlzeit ohne sinnvoll nutzbare OfferGroup mit StandardEntry als fehlend anzeigen.

Speichern nicht blockieren.

Verpflegungs-Teilstand:

- `Incomplete` nur, wenn notwendige Berechnungsvoraussetzungen fehlen;
- Über-/Unterdeckung bleibt berechenbar;
- Über-/Unterdeckung als Hinweis anzeigen.

Keine Aussage über medizinische oder fachliche Sicherheit ableiten.

---

# 17. Meal Activation und Camp-Zeitraum

Nur bestehende lagerweit aktive Meal-Slots verwenden.

Keine zusätzlichen Catering-Meal-Slots.

Deaktivierter Slot:

- Daten nicht löschen;
- operativ ignorieren.

Reaktivierter Slot:

- Daten wieder sichtbar;
- betroffene Teilstände Stale.

Camp-Zeitraum verkürzt:

- außerhalb liegende Daten nicht löschen;
- operativ ignorieren;
- sichtbar als ungültig/prüfbedürftig darstellen.

Spätere Wiederaufnahme in den Zeitraum macht alten Stand nicht automatisch Current.

---

# 18. Invalidierungslogik

Mindestens folgende Änderungen machen einen betroffenen Teilstand Stale:

- relevante Strukturzuordnung geändert;
- Planungszahlen eines verwendeten Camp-Knotens geändert;
- verwendete Verpflegungsfaktoren geändert;
- neue fachliche MealPlan-Version bei FollowStandard;
- individuelle Rezept-/Portionsauswahl geändert;
- Bedarfsoverride geändert;
- OfferGroup-Zielbedarfs-Override geändert;
- Meal reaktiviert;
- Camp-Zeitraumänderung betrifft den Slot.

Nicht Stale wegen:

- reiner Sortierung;
- reiner Darstellung;
- Änderungen an nicht verwendeten MealPlans;
- Änderungen an nicht referenzierten Camp-Knoten;
- Veröffentlichung einer neuen RecipeRevision, solange weiterhin die bisher gepinnte Revision verwendet wird.

---

# 19. Löschen

## MealPlan

Nur löschen, wenn keine relevanten fachlichen Referenzen bestehen.

Blockierende Referenzen verständlich anzeigen.

Keine stillen Cascades.

## CookingUnit

Löschen erlaubt.

Zugehörige mutable operative Planungsdaten entfernen.

Andere fachliche Objekte nicht löschen.

## CookingUnitGroup

Löschen entfernt nur Gruppe/Zuweisungen.

CookingUnits bleiben bestehen.

---

# 20. Berechtigungen

Inkrement 1 benötigt eine gemeinsame Permission für Mahlzeitenplanung bearbeiten.

Bestehendes Permission-Katalogmuster verwenden.

Keine neue feste Rolle erfinden.

Noch keine funktionale Verify-Permission einführen, wenn dafür kein bestehender Use Case vorhanden ist.

Alle mutierenden Use Cases müssen bestehende Tenant-/Camp-Isolation und Authorization-Regeln einhalten.

---

# 21. Offline-Package

Die neuen mutable Catering-Daten vollständig in Camp-Package Export/Import und atomaren Replace integrieren.

Mindestens:

- MealPlans;
- MealPlan-Versionen/Snapshots;
- OfferGroups;
- Entries;
- CookingUnits;
- CookingUnitGroups;
- Standard-MealPlan-Abonnements;
- Meal-Slot-Abweichungen;
- Strukturzuordnungen;
- Struktur-Overrides;
- Bedarfsoverrides;
- OfferGroup-Zielbedarfs-Overrides;
- Verpflegungsplan-Teilstände.

Die vollständige Camp-Rezeptbibliothek bleibt Package-Closure-Root.

MealPlan-Nutzung erweitert die Bibliothek automatisch.

Keine zweite parallele Rezept-Closure einführen, sofern bestehende ADRs dies nicht verlangen.

Bestehendes Verhalten unverändert lassen:

- Freeze;
- TransferId;
- Baseline;
- Validierung;
- atomarer Replace;
- Rollback;
- kein Merge.

Package-Version nicht eigenmächtig auf Version 2 erhöhen.

Version 2 ist für die bereits definierte Encryption-/Signature-Richtung reserviert.

Falls bestehende ADRs für Schemaänderungen eine andere Regel vorgeben, haben die ADRs Vorrang.

---

# 22. Persistenz und Migrationen

Für neue Catering-Entitäten:

- PostgreSQL Catering-Migration;
- SQLite Catering-Migration.

Bestehende Modul-/Tabellen-/Schema-Konventionen verwenden.

Keine Cross-Module-EF-Navigation.

Gemeinsame Domain-/Application-Logik.

Bestehende Migrationsstrategie einhalten.

---

# 23. API und Angular

Vorhandene Catering-Feature-Struktur verwenden.

Mindestens folgende UI-/API-Abläufe ermöglichen:

1. MealPlans auflisten;
2. MealPlan erstellen;
3. MealPlan sortieren;
4. MealPlan löschen;
5. Bearbeitungsmodus öffnen;
6. Änderungen speichern;
7. Änderungen verwerfen;
8. aktive Lagermahlzeiten anzeigen;
9. OfferGroups bearbeiten;
10. Standard-/Alternativ-Entries bearbeiten;
11. veröffentlichte Rezeptrevisionen auswählen;
12. Vollständigkeit anzeigen;
13. CookingUnits verwalten;
14. CookingUnitGroups verwalten;
15. CookingUnit aus Camp-Knoten erzeugen;
16. Standard-Strukturzuordnung setzen;
17. Strukturabweichung pro Meal setzen;
18. Strukturabweichung auf Standard zurücksetzen;
19. Standard-MealPlan setzen;
20. Meal-Slot auf Custom setzen;
21. Meal-Slot auf NoSupplyRequired setzen;
22. Meal-Slot auf FollowStandard zurücksetzen;
23. Bedarf berechnen;
24. Bedarfsoverride setzen/entfernen;
25. OfferGroup-Zielbedarfs-Override setzen/entfernen;
26. Verpflegungsplan berechnen;
27. Verpflegungsplan neu berechnen;
28. Current/Stale/Incomplete anzeigen;
29. Über-/Unterdeckung anzeigen;
30. Löschblockaden samt Referenzen anzeigen.

Keine UI für Inkrement-2-Funktionen implementieren.

---

# 24. Tests

## Domain/Application

Mindestens testen:

- mehrere MealPlans pro Camp;
- unvollständiger MealPlan speicherbar;
- mehrere OfferGroups pro Meal;
- genau ein StandardEntry pro OfferGroup;
- doppelte RecipeRevision innerhalb einer OfferGroup abgelehnt;
- gleiche RecipeRevision in zwei OfferGroups erlaubt;
- unveröffentlichte RecipeRevision abgelehnt;
- Qualifier optional;
- Recipe-Qualifier nur initial übernommen;
- Entry-Revisionwechsel behält lokale Angaben;
- Bearbeitung mit mehreren Änderungen erzeugt nur eine neue MealPlan-Version;
- Cancel verwirft Bearbeitung;
- Parent/Child-Strukturquellen innerhalb derselben effektiven Zuordnung abgelehnt;
- Standard-Strukturänderung beeinflusst Slots ohne Override;
- Struktur-Override bleibt bestehen;
- ResetToDefaultStructure funktioniert;
- FollowStandard folgt neuer MealPlan-Version;
- Custom bleibt unverändert;
- NoSupplyRequired bleibt unverändert;
- ResetToStandardPlan funktioniert;
- Bedarf aus anonymen Camp-Daten;
- manueller Bedarfsoverride;
- OfferGroup-Zielbedarf Default;
- OfferGroup-Zielbedarfs-Override;
- mehrere OfferGroups parallel;
- Über-/Unterdeckung nur Hinweis;
- Incomplete nur bei fehlenden Voraussetzungen;
- relevante Änderung macht Stale;
- irrelevante Änderung lässt Current;
- Neuberechnung erhält manuelle Entscheidungen;
- deaktivierter Meal-Slot;
- reaktivierter Meal-Slot;
- Camp-Zeitraum-Verkürzung;
- MealPlan-Löschblockade;
- CookingUnit-Löschung;
- CookingUnitGroup-Löschung.

## Persistenz/Integration

Für PostgreSQL und SQLite:

- CRUD;
- MealPlan-Version/Snapshot-Persistenz;
- referentielle Integrität;
- Recipe-Library-Autoaufnahme;
- Löschblockaden;
- Migration vom bisherigen Datenbankstand.

## Offline Package

Bestehenden Roundtrip-Test erweitern:

PostgreSQL
→ Export
→ SQLite Import
→ Offline-Bearbeitung
→ Return Package
→ PostgreSQL Replace

Prüfen:

- IDs erhalten;
- Beziehungen erhalten;
- MealPlan-Versionen erhalten;
- Snapshots erhalten;
- Recipe-Library-Closure vollständig;
- FollowStandard/Custom/NoSupplyRequired erhalten;
- Strukturzuordnungen erhalten;
- Overrides erhalten;
- Verpflegungsplanstände erhalten;
- atomarer Replace;
- invalides Baseline-/Transfer-Paket weiterhin abgelehnt;
- Rollback bei Fehlern.

## Architekturtests

Sicherstellen:

- Catering greift nicht auf Camp Infrastructure zu;
- Domain bleibt frameworkfrei;
- keine Cross-Module-DbContext-/EF-Navigation;
- bestehende Dependency-Richtung bleibt unverändert.

## Frontend

Mindestens relevante Tests für:

- Edit/Cancel/Save;
- Vollständigkeit;
- FollowStandard/Custom/NoSupplyRequired;
- Reset auf Standard;
- Struktur-Override und Reset;
- Current/Stale/Incomplete;
- Löschblockaden;
- Referenzanzeige.

---

# 25. Manueller Prüfablauf

Nach automatisierten Tests mindestens manuell prüfen:

1. Lager mit mehreren Tagen und aktivem Frühstück/Mittag/Abend öffnen.
2. Zwei MealPlans erstellen.
3. Einen Plan unvollständig speichern und Hinweis prüfen.
4. Mehrere fachliche Änderungen in einem Bearbeitungszyklus durchführen.
5. Speichern und prüfen, dass nur eine neue fachliche Version entsteht.
6. Neue Bearbeitung starten und verwerfen.
7. Zwei CookingUnits erstellen.
8. Eine davon aus Camp-Strukturknoten initialisieren.
9. Beide einem Standard-MealPlan zuordnen.
10. Standardbedarf aus Camp-Planungszahlen berechnen.
11. Eine konkrete Mahlzeit Custom setzen.
12. Eine konkrete Mahlzeit NoSupplyRequired setzen.
13. Standard-MealPlan fachlich ändern und speichern.
14. Prüfen:
    - FollowStandard folgt neuem Stand;
    - Custom bleibt unverändert;
    - NoSupplyRequired bleibt unverändert.
15. Custom bewusst auf Standard zurücksetzen.
16. Standard-Strukturzuordnung ändern.
17. Prüfen, dass Struktur-Override unverändert bleibt.
18. Struktur-Override auf Standard zurücksetzen.
19. Manuellen Bedarfsoverride setzen.
20. OfferGroup-Zielbedarfs-Override setzen.
21. Überdeckung erzeugen und Hinweis prüfen.
22. Unterdeckung erzeugen und Hinweis prüfen.
23. Planungszahl eines verwendeten Camp-Knotens ändern.
24. Prüfen, dass nur betroffene Teilstände Stale werden.
25. Neu berechnen.
26. Prüfen, dass manuelle Entscheidungen erhalten bleiben.
27. Aktive Mahlzeit deaktivieren und reaktivieren.
28. Lagerzeitraum verkürzen und wieder erweitern.
29. Externe veröffentlichte Rezeptrevision in MealPlan verwenden.
30. Prüfen, dass sie automatisch in die Camp-Rezeptbibliothek aufgenommen wird.
31. Entfernung der verwendeten Revision aus der Bibliothek versuchen.
32. Blockierende Referenzanzeige prüfen.
33. Offlinepaket exportieren.
34. Lokal mit SQLite MealPlan/CookingUnit/Verpflegungsplan ändern.
35. Rückpaket importieren.
36. Vollständigen Stand und atomaren Replace prüfen.

---

# 26. Dokumentation nach Implementierung

Mindestens aktualisieren:

- `docs/domain/catering-meal-planning.md`
- relevante bestehende Catering-/Recipe-Domänendokumente;
- `docs/architecture/architecture-overview.md`
- zuständige Offline-/Camp-Package-Dokumentation bzw. ADR;
- `docs/architecture/baseline-status.md`
- `.codex/CHATGPT_PROJECT_BRIEF.md`

Keine neue ADR nur für das Fachmodell erzeugen, sofern keine neue Architekturentscheidung erforderlich wird.

Falls die bestehende Package-ADR eine explizite neue Entscheidung verlangt, diese nicht umgehen.

---

# 27. Abschlussprüfung

Ausführen:

- vollständiger .NET Solution Build;
- vollständige Tests;
- Catering-Tests;
- Architekturtests;
- PostgreSQL-Integrationstests;
- SQLite-Integrationstests;
- Package-Roundtrip-Tests;
- Angular Tests;
- Angular Production Build.

Probleme vollständig dokumentieren.

---

# 28. Commit-Punkt

Ein gemeinsamer Commit ist sinnvoll, wenn:

- Domain/Application/API/UI für Inkrement 1 vollständig funktionieren;
- PostgreSQL- und SQLite-Migrationen vorhanden sind;
- Offline-Roundtrip erfolgreich ist;
- automatisierte Tests erfolgreich sind;
- manueller Prüfablauf abgeschlossen ist;
- Dokumentation aktualisiert ist.

Vorgeschlagene Commit Message:

`Implement meal planning increment 1`

Kein Teil von Mahlzeitenplanung Inkrement 2 darf Bestandteil dieses Commits sein.
