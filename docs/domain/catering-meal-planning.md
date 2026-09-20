# Catering Meal Planning

## Zweck

Dieses Dokument beschreibt das bestätigte Fachmodell für die Mahlzeitenplanung in ScoutCampPlanner.

Die Mahlzeitenplanung gehört zum Modul `Catering`. Sie verbindet veröffentlichte Rezeptrevisionen mit konkreten, datierten Lagermahlzeiten und übersetzt einen zentralen Speiseplan in einen operativen Verpflegungsplan für Catering-eigene Kocheinheiten.

Die Umsetzung ist in zwei Inkremente getrennt:

- **Inkrement 1:** normale Mahlzeitenplanung auf Basis der bereits vorhandenen anonymen Camp-Planungszahlen.
- **Inkrement 2:** reale Teilnehmerbasis, Sonderverpflegung, Varianten-/Ersatzauflösung und Verifikation.

Dieses Dokument beschreibt Inkrement 1 vollständig und hält die bestätigten Grenzen zu Inkrement 2 fest.

---

## 1. Begriffe und Verantwortlichkeiten

### 1.1 Camp-Struktur

Die Lagerstruktur gehört weiterhin zum Modul `Camp`.

`Catering` darf Camp-Struktur und Camp-Planungsdaten nur über definierte Contracts konsumieren und verändert keine Camp-Daten.

Camp-Struktur und Kocheinheiten sind unterschiedliche Fachkonzepte.

### 1.2 MealPlan

Ein `MealPlan` ist ein lagerbezogener, konkret datierter Speiseplan.

Er beschreibt, **was zu einer konkreten aktiven Lagermahlzeit zentral angeboten wird**.

Ein Lager darf mehrere benannte MealPlans besitzen.

MealPlans dürfen auch reine Planungsalternativen sein und müssen nicht zwingend später einer Kocheinheit zugeordnet werden.

### 1.3 CookingUnit

Eine `CookingUnit` ist ein Catering-eigenes, lagerbezogenes Objekt für Personen, die gemeinsam kochen und essen.

Sie ist nicht Teil der Camp-Struktur.

Eine zusätzliche fachliche Ebene „Küche“ oder „Produktionseinheit“ wird nicht eingeführt.

### 1.4 Verpflegungsplan

Der Verpflegungsplan beschreibt die konkrete operative Umsetzung für eine Kocheinheit.

Der MealPlan beantwortet:

> Was wird angeboten?

Der Verpflegungsplan beantwortet:

> Was kocht diese Kocheinheit bei dieser Mahlzeit in welcher Menge?

Die operative Berechnungs- und Aktualisierungseinheit ist:

`CookingUnit × konkrete Mahlzeit`

---

## 2. Lagermahlzeiten als Rahmen

Die bereits bestehende Aktivierung von Mahlzeitenarten pro Lagertag definiert den lagerweiten Rahmen.

Ein MealPlan darf ausschließlich auf diesen bereits aktivierten Mahlzeiten-Slots aufsetzen.

Es werden in diesem Inkrement keine zusätzlichen Mahlzeiten-Slots aus Catering heraus erzeugt.

Eine Kocheinheit kann für einen aktiven Slot lediglich explizit auf:

`keine Versorgung erforderlich`

gesetzt werden.

Die Mahlzeit bleibt dadurch lagerweit weiterhin aktiv.

MealPlans dürfen nur innerhalb des offiziellen Lagerzeitraums liegen.

---

## 3. MealPlan

### 3.1 Grundregeln

Ein Lager darf beliebig viele benannte MealPlans besitzen.

Ein MealPlan besitzt mindestens:

- stabile ID;
- Camp-Bezug;
- Name;
- benutzerdefinierte Sortierreihenfolge;
- monotonen fachlichen Versionsstand.

Es gibt keinen Draft-/Active-Lebenszyklusstatus.

Ein MealPlan darf unvollständig gespeichert werden.

### 3.2 Vollständigkeit

Die Vollständigkeit ist nur eine unterstützende Plausibilitätsinformation.

Eine aktive Lagermahlzeit gilt vereinfacht als abgedeckt, wenn mindestens eine sinnvoll nutzbare `MealPlanOfferGroup` mit Standardrezept vorhanden ist.

Fehlende Mahlzeiten blockieren das Speichern nicht.

`vollständig` bedeutet ausdrücklich nicht:

- fachlich geprüft;
- verifiziert;
- sonderverpflegungstauglich;
- medizinisch sicher.

### 3.3 Bearbeitungsmodus

Versorgungsrelevante Änderungen eines MealPlans werden in einem expliziten Bearbeitungsmodus gesammelt.

Mehrere Änderungen können innerhalb einer Bearbeitungssitzung vorgenommen werden.

Erst eine bewusste Speicheraktion:

- validiert den neuen fachlichen Stand;
- erhöht die MealPlan-Version genau einmal;
- erzeugt einen unveränderlichen fachlichen Snapshot;
- macht den neuen Stand für abonnierende Kocheinheiten wirksam.

Änderungen können vollständig verworfen werden.

Während einer offenen Bearbeitung bleibt die zuletzt gespeicherte Version für Abonnements und Kalkulation maßgeblich.

### 3.4 Fachliche Versionen und Snapshots

Nur versorgungsrelevante Änderungen erhöhen die fachliche MealPlan-Version.

Reine Darstellungsänderungen, z. B. Sortierung ohne fachliche Wirkung, erzeugen keine neue Version.

Fachliche MealPlan-Versionen werden als unveränderliche Snapshots erhalten.

Snapshots müssen nicht unbegrenzt aufbewahrt werden.

Ein Snapshot darf jedoch nicht bereinigt werden, solange ein relevanter gespeicherter Planungsstand darauf verweist.

Bei sehr alten oder nicht mehr sinnvoll fortführbaren Arbeitsständen darf später eine Neukalkulation verlangt werden.

Eine konkrete Retention-Dauer ist noch nicht festgelegt.

---

## 4. MealPlanOfferGroup

Jede konkrete Lagermahlzeit eines MealPlans kann mehrere `MealPlanOfferGroups` besitzen.

Eine OfferGroup ist eine eigenständige Mahlzeitenkomponente.

Beispiele:

- Hauptgericht;
- Beilage;
- Nachspeise.

Jede OfferGroup:

- besitzt eine stabile ID;
- gehört zu genau einer konkreten MealPlan-Mahlzeit;
- besitzt optional einen Anzeigenamen;
- besitzt eine Darstellungsreihenfolge;
- enthält genau ein Standard-MealPlanEntry;
- kann beliebig viele Alternativ-Entries enthalten.

Auch ein einzelnes Gericht ohne Alternative wird über eine eigene OfferGroup modelliert.

Mehrere OfferGroups werden im Verpflegungsplan parallel berechnet und nicht gegeneinander aufgerechnet.

---

## 5. MealPlanEntry

Jedes `MealPlanEntry`:

- gehört genau einer `MealPlanOfferGroup`;
- referenziert genau eine veröffentlichte, unveränderliche Rezeptrevision;
- ist innerhalb der OfferGroup entweder Standard oder Alternative;
- besitzt optional einen eigenen Anzeigenamen;
- besitzt optional einen Rollen-Qualifier;
- besitzt optional einen Freitext-Hinweis;
- besitzt eine explizite Darstellungsreihenfolge.

Rezeptentwürfe dürfen nicht direkt geplant werden.

Innerhalb derselben OfferGroup darf dieselbe `RecipeRevisionId` nur einmal vorkommen.

Dieselbe Rezeptrevision darf jedoch in mehreren OfferGroups derselben Mahlzeit verwendet werden.

Ein bestehendes MealPlanEntry darf bewusst auf eine andere veröffentlichte Rezeptrevision umgestellt werden.

Dabei bleiben lokale Eigenschaften des Entry erhalten:

- Rollen-Qualifier;
- Anzeigename;
- Hinweis.

Ein Wechsel der referenzierten Rezeptrevision ist eine versorgungsrelevante MealPlan-Änderung und erzeugt beim Speichern eine neue fachliche MealPlan-Version.

MealPlanEntries besitzen keine eigene Revisionierung oder Historie.

Historische Nachvollziehbarkeit erfolgt ausschließlich über MealPlan-Snapshots.

---

## 6. Rollen-Qualifier

Rezeptrevision und MealPlanEntry verwenden denselben systemdefinierten Rollen-Katalog.

Startkatalog:

- Hauptspeise;
- Beilage;
- Vorspeise;
- Nachspeise;
- Getränk;
- Sonstiges.

Der Qualifier ist optional.

Er beschreibt ausschließlich die Rolle innerhalb eines Menüs.

Ernährungsformen wie:

- vegetarisch;
- vegan;

sowie Allergene oder Unverträglichkeiten sind **keine Rollen**.

Beim Erzeugen eines neuen MealPlanEntry wird der Rollen-Qualifier der Rezeptrevision als initialer Wert übernommen.

Danach sind Rezeptrevision und MealPlanEntry unabhängig.

Spätere Änderungen am Rezept-Qualifier verändern bestehende MealPlanEntries nicht automatisch.

Die Rollen steuern in Inkrement 1 keine automatische Mengenlogik.

---

## 7. Lager-Rezeptbibliothek

Ein MealPlan darf jede für den Benutzer zugängliche veröffentlichte Rezeptrevision verwenden.

Wird eine veröffentlichte Rezeptrevision erstmals lagerbezogen verwendet, wird sie automatisch in die Lager-Rezeptbibliothek aufgenommen.

Solange eine lagerbezogene Referenz auf die Revision besteht, darf sie nicht aus der Lager-Rezeptbibliothek entfernt werden.

Beim blockierten Entfernen müssen die bestehenden Referenzen angezeigt werden.

Wird eine Revision später nicht mehr verwendet, bleibt sie in der Lagerbibliothek, bis sie bewusst entfernt wird.

Es gibt keine automatische Bereinigung.

Später soll eine vom Lageradmin bewusst anstoßbare Bereinigungsfunktion nicht mehr referenzierte Bibliothekseinträge erkennen und zur Prüfung anbieten.

Die vollständige Lager-Rezeptbibliothek bleibt Bestandteil des Offlinepakets.

---

## 8. CookingUnit

### 8.1 Grundregeln

Eine CookingUnit besitzt mindestens:

- stabile ID;
- Camp-Bezug;
- Name;
- benutzerdefinierte Sortierreihenfolge;
- optional genau eine CookingUnitGroup;
- optional genau einen Standard-MealPlan;
- eine Standard-Zuordnung zu einem oder mehreren Camp-Strukturknoten.

CookingUnits:

- besitzen keine eigene Revisionierung;
- besitzen keinen Aktiv/Inaktiv-Status;
- sind normale veränderliche Catering-Objekte.

Eine CookingUnit kann:

- manuell angelegt werden;
- optional aus einem Camp-Strukturknoten initialisiert werden.

Das Erzeugen aus einem Camp-Knoten übernimmt nur initial sinnvolle Daten wie Name und Strukturreferenz.

Danach bleibt die CookingUnit ein eigenständiges Catering-Objekt.

### 8.2 Löschen

Eine CookingUnit darf gelöscht werden.

Ihre unmittelbar zugehörigen veränderlichen operativen Planungsdaten werden dabei ebenfalls entfernt.

Andere fachliche Objekte werden nicht gelöscht.

Davon abhängige Kalkulationen oder Aggregate werden veraltet bzw. müssen bei nächster Verwendung neu berechnet werden.

---

## 9. CookingUnitGroup

CookingUnits dürfen optional in Catering-eigenen Gruppen organisiert werden.

Eine CookingUnit gehört höchstens einer Gruppe an.

Gruppen:

- sind einstufig;
- besitzen keine Untergruppen;
- besitzen keine Bedarfslogik;
- besitzen keine Einstellungsvererbung;
- dienen Übersicht, Filterung und Massenaktionen.

Das Löschen einer Gruppe entfernt nur die Gruppierung.

Die enthaltenen CookingUnits bleiben bestehen.

Eine separate explizite Massenfunktion zum Löschen ausgewählter CookingUnits ist zulässig.

---

## 10. Standard-MealPlan und mahlzeitenbezogene Abweichungen

Eine CookingUnit kann im ersten Inkrement höchstens einen Standard-MealPlan abonnieren.

Das Abonnement umfasst grundsätzlich die Tage und aktiven Mahlzeiten des Plans.

Für einen konkreten Slot gibt es drei fachliche Zustände:

1. `FollowStandard`
2. `Custom`
3. `NoSupplyRequired`

### FollowStandard

Der Slot folgt dem aktuell gespeicherten Stand des Standard-MealPlans.

Ändert sich der Standardplan fachlich, folgt der Slot automatisch dem neuen Stand.

### Custom

Die konkrete Mahlzeit ist bewusst individuell geplant.

Sie wird durch spätere Änderungen des Standardplans nicht überschrieben.

### NoSupplyRequired

Für diese CookingUnit ist für den konkreten aktiven Lagermahlzeiten-Slot bewusst keine Versorgung erforderlich.

Auch dieser Zustand wird durch spätere Standardplan-Änderungen nicht überschrieben.

### Zurücksetzen auf Standard

Eine bewusste Aktion `Auf Standardplan zurücksetzen` entfernt die lokale Abweichung.

Danach folgt der Slot wieder dem aktuellen Standardplan.

---

## 11. Camp-Strukturzuordnung

### 11.1 Standard-Zuordnung

Eine CookingUnit besitzt eine Standard-Zuordnung zu einem oder mehreren vollständigen Camp-Strukturknoten.

Diese Standard-Zuordnung gilt für alle aktiven Mahlzeiten, solange für einen konkreten Slot keine eigene Strukturabweichung besteht.

### 11.2 Mahlzeitenbezogene Abweichung

Eine konkrete Mahlzeit darf eine eigene Strukturzuordnung besitzen.

Spätere Änderungen der Standard-Zuordnung verändern solche lokalen Abweichungen nicht.

Eine bewusste Aktion kann die Mahlzeit wieder auf die aktuelle Standard-Zuordnung zurücksetzen.

### 11.3 Hierarchieprüfung

Innerhalb derselben effektiven Strukturzuordnung dürfen nicht gleichzeitig Strukturknoten gewählt werden, wenn einer Vorfahr oder Nachfahr eines anderen gewählten Knotens ist.

Dadurch wird eindeutige Doppelzählung innerhalb derselben CookingUnit vermieden.

Zwischen verschiedenen CookingUnits dürfen sich Strukturreferenzen überlappen.

Das System weist auf resultierende Über- oder Unterdeckung hin, blockiert die Planung aber nicht.

---

## 12. Bedarf

Der Portionsbedarf einer CookingUnit wird pro konkrete Mahlzeit standardmäßig aus folgenden Quellen abgeleitet:

- effektive Camp-Strukturzuordnung;
- vorhandene anonyme KiJu-/Leiter-Planungszahlen;
- bestehende Catering-Verpflegungsfaktoren.

In Inkrement 1 werden ausschließlich vollständige Strukturknoten als automatische Berechnungsquelle verwendet.

Teilmengen eines Strukturknotens werden noch nicht modelliert.

Für Sonderfälle ist ein manueller Portions-Override zulässig.

Dieser gilt ausschließlich für:

`CookingUnit × konkrete Mahlzeit`

Es gibt keinen dauerhaften Portions-Override auf CookingUnit-Ebene.

---

## 13. Verpflegungsplan

Der Verpflegungsplan ist ein lagerbezogener operativer Gesamtplan.

Er besteht aus separat aktualisierbaren Teilständen auf Ebene:

`CookingUnit × konkrete Mahlzeit`

Ein Teilstand muss den für seine Berechnung relevanten Datenstand nachvollziehbar machen.

Dazu gehören mindestens:

- verwendete MealPlan-Version bzw. Snapshot;
- effektive Strukturzuordnung;
- verwendete Planungszahlen;
- verwendete Verpflegungsfaktoren;
- berechneter Gesamtbedarf;
- manueller Bedarfsoverride;
- OfferGroup-Zielbedarfe und Overrides;
- konkrete Rezept-/Portionsauswahl;
- Herkunft relevanter manueller Entscheidungen.

---

## 14. OfferGroup-Zielbedarf

Jede OfferGroup besitzt im konkreten Verpflegungsplan einen eigenen Zielbedarf.

Standard:

`OfferGroupTarget = CookingUnitMealDemand`

Der Zielbedarf einer OfferGroup kann für einen konkreten Verpflegungsstand manuell überschrieben werden.

Der Override gehört ausschließlich zu:

`CookingUnit × Mahlzeit × OfferGroup`

Er gehört nicht in den zentralen MealPlan.

Innerhalb einer OfferGroup teilen Standard und Alternativen deren Zielbedarf auf.

Mehrere OfferGroups werden parallel gerechnet.

Beispiel:

- Hauptgericht: Ziel 30;
- Beilage: Ziel 30;
- Nachspeise: Ziel 30;
- zusätzliche Komponente: Ziel manuell 15.

---

## 15. Individuelle Rezeptwahl

Eine CookingUnit darf innerhalb derselben Mahlzeit:

- zentrale MealPlanEntries verwenden;
- direkt andere veröffentlichte Rezeptrevisionen wählen;
- beide Formen mischen.

Individuell gewählte Rezepte werden nicht automatisch zu globalen MealPlanEntries.

Jede individuelle Auswahl bleibt an eine konkrete datierte Lagermahlzeit gebunden.

Auch individuell verwendete veröffentlichte Rezeptrevisionen werden automatisch in die Lager-Rezeptbibliothek aufgenommen.

---

## 16. Deterministische Berechnung

Der Verpflegungsplan wird nicht bei jeder Änderung der Quelldaten automatisch neu berechnet.

Eine Berechnung erzeugt einen deterministischen Stand zu einem konkreten Datenstand.

Ändern sich relevante Eingangsdaten, bleibt der berechnete Stand bestehen und wird als veraltet markiert.

Eine Neuberechnung erfolgt bewusst.

Bei einer Neuberechnung werden automatisch abgeleitete Bestandteile aktualisiert.

Bewusste manuelle Entscheidungen werden nicht still überschrieben.

Sind manuelle Entscheidungen mit dem neuen Datenstand nicht mehr konsistent, werden sie als prüfbedürftig angezeigt.

---

## 17. Status Inkrement 1

Ein Verpflegungs-Teilstand kennt genau drei Zustände:

- `Current`
- `Stale`
- `Incomplete`

### Current

Der berechnete Stand basiert auf dem derzeit relevanten Datenstand.

Dies bedeutet nicht, dass die Planung fachlich oder medizinisch garantiert korrekt ist.

### Stale

Mindestens eine tatsächlich berechnungsrelevante Grundlage hat sich seit der letzten Berechnung geändert.

### Incomplete

Notwendige Voraussetzungen für eine vollständige Berechnung fehlen.

Über- oder Unterdeckung allein macht einen Stand nicht `Incomplete`, solange die Berechnung technisch/fachlich durchgeführt werden kann.

---

## 18. Veraltungsrelevante Änderungen

Insbesondere folgende Änderungen machen einen betroffenen Teilstand veraltet:

- relevante Camp-Strukturzuordnung geändert;
- anonyme Planungszahlen eines tatsächlich verwendeten Camp-Strukturknotens geändert;
- verwendete Catering-Verpflegungsfaktoren geändert;
- neue fachliche Version des abonnierten MealPlans, sofern der Slot noch `FollowStandard` ist;
- individuelle Rezept-/Portionsauswahl geändert;
- manueller Portions-Override gesetzt, geändert oder entfernt;
- OfferGroup-Zielbedarfs-Override geändert;
- deaktivierte Lagermahlzeit wieder aktiviert;
- Änderung des Lagerzeitraums betrifft die konkrete Mahlzeit.

Nicht veraltungsrelevant sind insbesondere:

- reine Sortierreihenfolge;
- reine UI-/Darstellungsinformationen;
- Änderungen an nicht verwendeten MealPlans;
- Änderungen an nicht referenzierten Camp-Strukturknoten;
- Veröffentlichung einer neuen Rezeptrevision, solange weiterhin die bisher gepinnte Revision verwendet wird.

---

## 19. Plausibilität und Operator-Verantwortung

Die Anwendung ist ein unterstützendes Werkzeug.

Sie soll insbesondere sichtbar machen:

- fehlende Planung;
- Überdeckung;
- Unterdeckung;
- veraltete Berechnungen;
- inkonsistente manuelle Entscheidungen.

Solche Hinweise blockieren die Arbeitskalkulation nicht automatisch.

Die Verantwortung für die fachliche Richtigkeit der Planung verbleibt beim Operator.

---

## 20. Änderungen an Mahlzeitenaktivierung und Lagerzeitraum

### Mahlzeit deaktiviert

Wird eine lagerweit aktive Mahlzeit deaktiviert:

- vorhandene Planungsdaten bleiben erhalten;
- sie sind operativ außer Wirkung;
- sie werden nicht in Vollständigkeitsprüfung oder aktuelle Kalkulation einbezogen.

### Mahlzeit reaktiviert

Wird die Mahlzeit erneut aktiviert:

- alte Planungsdaten werden wieder sichtbar;
- betroffene Verpflegungsstände gelten zunächst als veraltet bzw. prüfbedürftig.

### Lagerzeitraum verkürzt

Planungsdaten außerhalb des neuen Lagerzeitraums:

- bleiben erhalten;
- sind operativ außer Wirkung;
- werden als ungültig bzw. prüfbedürftig angezeigt.

Wird der Zeitraum später wieder erweitert, werden alte Teilstände nicht automatisch `Current`.

---

## 21. Löschen von MealPlans

Ein MealPlan darf nur gelöscht werden, wenn keine relevanten fachlichen Referenzen mehr bestehen.

Blockierende Referenzen müssen vor dem Löschen sichtbar angezeigt werden.

Es gibt keine stillen Kaskaden, die bestehende operative Planung zerstören.

---

## 22. Berechtigungen

Inkrement 1 verwendet eine gemeinsame Berechtigung für die Bearbeitung der Mahlzeitenplanung.

Eine spätere Verifikationsberechtigung wird fachlich separat behandelt.

Es wird kein verpflichtendes Vier-Augen-Prinzip eingeführt.

Es wird keine neue feste Rolle nur für diese Phase erfunden.

---

## 23. Offline

Die vollständige Funktionalität von Inkrement 1 muss online und offline bearbeitbar sein.

Neue lagerbezogene Catering-Daten werden Bestandteil des bestehenden Camp-Package-Replace-Bereichs.

Während einer aktiven Offlinephase gilt weiterhin:

1. Cloud-Daten des Lagers werden eingefroren.
2. Die lokale Instanz ist führend.
3. Es gibt keine parallele Cloud-Bearbeitung derselben Lagerdaten.
4. Es gibt keinen automatischen Merge.
5. Der Rückimport ersetzt den lagerbezogenen Stand atomar gemäß bestehender Package-Regeln.

Die vollständige Lager-Rezeptbibliothek bleibt Bestandteil des Offlinepakets.

PostgreSQL und SQLite müssen dieselbe Domain-/Application-Logik verwenden.

---

## 24. Nicht-Ziele Inkrement 1

Nicht Bestandteil von Inkrement 1 sind:

- personalisierte Teilnehmerdaten;
- Gesundheitsdaten;
- Sonderverpflegung;
- kombinierte Anforderungsgruppen;
- automatische Zutatenvarianten-/Ersatzauflösung;
- automatische Auswahl von Angebotsalternativen anhand Ernährungseigenschaften;
- Gemeinsamkeitspräferenz `Immer gemeinsam / Fragen / Nie gemeinsam`;
- vollständige Konfliktklassifikation;
- Verifikation;
- VerificationSnapshots;
- Bestellebene;
- Ausgabestellen;
- Warenlager;
- Einkauf;
- Gebinde-/Mindestmengenkalkulation;
- Bestelloptimierung;
- Lagershop/Punktesystem;
- Speiseplanvorlagen;
- komplexe Kopier-/Verschiebefunktionen;
- automatische Bereinigung der Lager-Rezeptbibliothek;
- zusätzliche Mahlzeiten außerhalb des lagerweiten Mahlzeitenrasters;
- Cloud-/Offline-Merge.

---

## 25. Späteres Inkrement 2

Inkrement 2 baut auf dem Modell aus Inkrement 1 auf.

Bestätigte fachliche Richtung:

- tatsächliche Teilnehmerdaten ersetzen bzw. präzisieren die kalkulatorische Bedarfsbasis;
- Anforderungen werden mahlzeitenaktuell aus der Zusammensetzung der CookingUnit abgeleitet;
- Anforderungsgruppen sind disjunkte Mengen mit beliebigen Kombinationen von Anforderungen;
- eindeutige direkte Zutatenvarianten/Ersatzzutaten werden automatisch angewendet;
- es wird keine automatische mehrstufige Ersatzkette aufgebaut;
- komplexe Sonderfälle werden über alternative Rezepte im konkreten Nutzungskontext gelöst;
- OfferGroup-Alternativen werden erst nach erfolgloser direkter Rezeptanpassung berücksichtigt;
- automatische Auswahl erfolgt nur bei genau einer eindeutigen passenden Lösung;
- eindeutige automatische Zuordnungen gelten direkt als geplante Auswahl;
- CookingUnits erhalten eine Gemeinsamkeitspräferenz `Immer gemeinsam / Fragen / Nie gemeinsam`;
- Konflikte blockieren später die normale Verifikation, nicht aber grundsätzlich die Kalkulation;
- der Operator kann später bewusst `verifiziert mit Abweichung` bestätigen;
- Verifikation erzeugt unveränderliche VerificationSnapshots.

---

## 26. Implementierungsstand Inkrement 1

Stand 2026-09-20 ist Inkrement 1 als durchgängiger Vertikalschnitt umgesetzt:

- mehrere lagerbezogene Mahlzeitenpläne mit Angebotsgruppen, genau einem Standardrezept je Gruppe, Alternativen und optionalen Rollen-Qualifiern;
- bewusster Save/Cancel-Bearbeitungsmodus, monotone fachliche Versionen und unveränderliche Snapshots;
- Catering-eigene Kocheinheiten und einstufige Gruppen mit Standardplan sowie Standard- und mahlzeitenbezogener Strukturzuordnung über Camp Contracts;
- die Zustände `FollowStandard`, `Custom` und `NoSupplyRequired`, explizites Zurücksetzen und manuelle Bedarfs-/Angebots-Overrides;
- explizite Berechnung aus anonymen KiJu-/Leiter-Schätzungen und Verpflegungsfaktoren, inklusive nachvollziehbarem Berechnungs-Snapshot und `Current`/`Stale`/`Incomplete`;
- automatische Aufnahme verwendeter veröffentlichter Rezeptrevisionen in die Lagerbibliothek und blockierte Entfernung mit verständlichen Referenzen;
- Lagerzeitraum und Meal-Aktivierung wirken operativ, ohne außerhalb liegende oder deaktivierte Planungsdaten zu löschen;
- PostgreSQL- und SQLite-Persistenz sowie vollständiger Camp-Package-Replace.

Reine Darstellungs- und Sortieränderungen erzeugen keine neue fachliche Planversion und machen berechnete Teilstände nicht veraltet. Rollen-Qualifier sind versorgungsrelevant und erhöhen bei Änderung die Planversion.

Das bestehende Rezept-Snapshotmodell enthält derzeit keinen eigenen Rollen-Qualifier. Deshalb kann beim erstmaligen Einfügen noch kein Rezeptwert vorgeschlagen werden; der optionale Qualifier wird am MealPlanEntry gesetzt. Diese begrenzte Abweichung ist im Ergebnisbericht dokumentiert und erfindet keine neue Rezepteigenschaft.

Personalisierte Anforderungen, automatische Ersatzauflösung, Verifikation, Bestellung und Einkauf bleiben ausdrücklich Folgeinkremente.
