# Mahlzeitenrahmen: technische Ausarbeitung

Stand: 2026-11-01. Technischer Entwurf auf Basis der [bestätigten Fachregeln](../domain/catering-regular-and-additional-meals.md), anschließend implementiert. Die folgenden Abschnitte halten den ursprünglichen Entwurf fest; Ist-Stand und Prüfgrenzen stehen in der [Abnahme](../testing/catering-additional-meals.md). Abweichung: Unbenutzte Additional-Arten werden nicht bereinigt. Die Planungsübersicht liefert ebenfalls die Rahmenversion; Tagesaktionen liegen in einem gemeinsamen Dialog.

## Befund im Code

| Stelle | Heute | Konsequenz |
|---|---|---|
| `Catering.Domain/CampMealType.cs` | Lagerweite Bezeichnung und Sortierung, kein Typkennzeichen | Regelmäßige und zusätzliche Mahlzeiten müssen unterscheidbar werden. |
| `Catering.Domain/CampMeal.cs` | Feste ID, Datum, MealTypeId, IsActive und ChangeVersion | Identitäten erhalten; Tagessortierung ergänzen. |
| `Api/Catering/CateringPlanningService.cs` | Namenslistenabgleich erzeugt neue Arten an jedem Tag; Entfernung prüft nur OfferGroups | Zusätze vom Abgleich ausschließen; vollständige Referenzprüfung ergänzen. |
| `Api/Camps/CampManagementService.cs`, Zeitraumänderung | Fehlende Slots für sämtliche Arten werden ergänzt | Nur regelmäßige Arten automatisch ergänzen. |
| `Catering.Infrastructure/CateringDbContext.cs` | Eindeutige Typnamen und Typsortierung pro Lager; Slot eindeutig pro Lager/Datum/Typ | Vorhandene Namens- und Identitätsregeln nutzen. |
| `Api/Program.cs`, Mahlzeitenendpunkte | Lesen über Camp-Kontext; Schreiben mit Camp.Edit, Freeze-Prüfung und Audit | Gleiche Grenze für neue Schreiboperationen übernehmen. |
| `Catering.Application/MealPlanning/MealPlanningService.cs` | Übersicht kennt IsInsideCampPeriod; Fingerprint berücksichtigt ChangeVersion | Zeitraumfilter erhalten; reine Anzeigesortierung nicht mit fachlicher Änderung vermischen. |
| `Api/Camps/ParticipantEndpoints.cs` | Anwesenheitsliste sortiert nach Mahlzeitenart | Dieselbe Tagesreihenfolge wie Menü- und operative Planung verwenden. |
| `Package/CampPackageContracts.cs`, `CampPackageService.cs`, `CampPackageSerializer.cs` | Mahlzeitenrahmen liegt im äußeren Paketformat 1, nicht nur im Catering-Teilformat 4 | Paketvertrag und Kompatibilitätsstrategie ausdrücklich erweitern. |

Die genannten Modulpfade liegen unter `src/backend/ScoutCampPlanner.*`. Der aktuelle Leseweg `GetMealPlanAsync` initialisiert bei fehlenden Arten Standardmahlzeiten. Er darf bei ausschließlich zusätzlichen Arten oder nach einer Migration keine unbeabsichtigte Neuinitialisierung auslösen.

## Modellvorschlag

**CampMealType bleibt die lagerweite Namensidentität.** Ergänzung `Kind = Regular | Additional`; vorhandene Datensätze werden ausschließlich als Regular migriert. Keine Klassifizierung anhand von Namen oder vorhandenen Slotzahlen.

Eine Additional-Art wird bei erneutem Hinzufügen desselben normalisierten Namens an einem anderen Tag wiederverwendet. Sie ist kein regelmäßiger Termin und erzeugt niemals automatisch Slots. Es gibt vorerst keine eigene Stammdatenverwaltung dafür und keine Umwandlung zwischen Regular und Additional.

Die bestehende Eindeutigkeit `(CampId, NormalizedName)` bleibt bestehen. Name: trimmen, 1–100 Zeichen, Normalisierung wie bisher mit `ToUpperInvariant`. Daraus folgen diese vorgeschlagenen Regeln:

- Derselbe Zusatzname darf an verschiedenen Tagen vorkommen.
- Derselbe Name darf am selben Tag nur einmal vorkommen, auch wenn der Slot deaktiviert ist. In diesem Fall Reaktivieren anbieten.
- Ein Zusatzname darf nicht mit einer regelmäßigen Mahlzeit kollidieren. Eine neue regelmäßige Bezeichnung darf ebenfalls keine Additional-Art stillschweigend übernehmen.
- Der erste Umfang legt genau einen Slot an einem ausgewählten Tag an. Keine Mehrtagesauswahl und keine Uhrzeiten.

`CampMeal` erhält `SortOrder` für die Reihenfolge an seinem Datum. Migration: pro Datum nach bisherigem `CampMealType.SortOrder`, mit ID als stabilem Tie-Breaker initialisieren. IDs, Aktivität und ChangeVersion bleiben erhalten. Neue Zusätze werden angehängt; neue regelmäßige Slots werden vor den Zusatzslots eingefügt. Eine explizite Tagesreihenfolge darf anschließend alle Slots dieses Tages anordnen.

`CampMealType.SortOrder` bleibt die Standardreihenfolge der regelmäßigen Arten. Wegen des bestehenden eindeutigen Index umfasst die technische Nummerierung weiterhin alle Arten; Regular wird zuerst, Additional danach nummeriert. Reguläre Konfigurationsänderungen aktualisieren diese Standardreihenfolge, überschreiben aber keine bereits gespeicherte Tagesreihenfolge. Neue Tage übernehmen die aktuelle Regular-Reihenfolge. Updates mit eindeutigen Sortierindizes benötigen innerhalb einer Transaktion eine kollisionsfreie Zwischenbelegung.

Eine zusätzliche Entität `CampMealScheduleState` pro Lager mit monotoner `Version` schützt alle Änderungen am Rahmen vor verlorenen Updates. Auch Änderungen über die bisherigen Namens-/Aktivitätsverträge sowie Lagerzeitraumänderungen erhöhen diese Version. Die Prüfung muss als atomare Datenbank-Concurrency-Prüfung erfolgen; ein Vergleich nur im Speicher genügt nicht.

Die Rahmenversion ist getrennt von `CampMeal.ChangeVersion`: Umordnen ändert nur die Rahmenversion. Aktivieren/Deaktivieren verwendet weiter `SetActive` und dessen ChangeVersion. Es gibt keine automatische Neuberechnung oder Verifikation.

## Vorgeschlagener API-Vertrag

Alle Pfade beginnen mit `/api/camps/{campId}`. Schreiboperationen verlangen Camp.Edit, nicht eingefrorenes Lager, Audit und eine gemeinsame Transaktion einschließlich Versionsprüfung. Fehlende Berechtigung und fremde IDs werden wie bisher mit 404 verborgen. Keine Erweiterung von Gesundheitsrechten.

| Methode/Pfad | Eingabe | Ergebnis |
|---|---|---|
| GET `/meal-plan` | – | Bestehende Daten plus `scheduleVersion`; Typen ergänzen `kind`, Slots `sortOrder` und `changeVersion`. Bestehende Namenfelder bleiben erhalten. |
| POST `/meals/additional` | `{ date, name, expectedScheduleVersion }` | 201 mit angelegtem Slot und neuer Rahmenversion; nur innerhalb des aktuellen Lagerzeitraums. |
| PUT `/meals/days/{date}/order` | `{ mealIds, expectedScheduleVersion }` | 200 mit neuer Rahmenversion; vollständige, duplikatfreie Liste aller Slots des Tages einschließlich deaktivierter Slots. |
| DELETE `/meals/{mealId}` | `If-Match` mit gelesener Rahmenversion | 200 `{ outcome: "Deleted" | "Deactivated", scheduleVersion }`; nur Additional. Entscheidung erfolgt serverseitig anhand der Referenzen. |
| PUT `/meals/{mealId}/activity` | Bisheriges `isActive`, neuer Client ergänzt `expectedScheduleVersion` | Bestehender Vertrag bleibt nutzbar; Reaktivierung nur innerhalb des aktuellen Zeitraums. Neuer Client liest danach den Rahmen neu. |
| PUT `/meal-types` | Bisherige Namensliste, neuer Client ergänzt `expectedScheduleVersion` | Bearbeitet ausschließlich Regular-Arten. Additional-Arten bleiben erhalten und werden nicht vervielfältigt. Bestehende Antwort 204 bleibt erhalten. |

Alte Schreibclients ohne Rahmenversion behalten zunächst das bisherige Verhalten; die Operation erhöht trotzdem atomar die Rahmenversion. Die volle Schutzgarantie gilt nur für Clients mit erwartetem Stand. Diese Übergangsgrenze muss dokumentiert und getestet werden; es wird keine vorhandene API stillschweigend als bereits concurrency-sicher bezeichnet.

Fehler: 400 für ungültige Eingaben, 404 für nicht zugängliche Ressourcen, 409 `camp_frozen`, `meal_schedule_changed`, `meal_name_conflict` oder `meal_already_exists`. Konfliktantworten dürfen keine fremden Daten oder Teilnehmerreferenzen offenlegen. Kein automatisches Retry einer Mutation nach Versionskonflikt; Stand neu laden und Entwurf erhalten.

Der GET-Vertrag liefert weiterhin die Slots des aktuellen Zeitraums. Die ausführliche MealPlanning-Übersicht darf erhaltene Slots außerhalb des Zeitraums weiter mit `IsInsideCampPeriod = false` liefern; UI und Kennzahlen müssen diesen Zustand berücksichtigen.

## Löschen und Deaktivieren

Ein Slot ist erst dann unbenutzt, wenn in derselben Transaktion keine dieser Referenzen vorliegt:

- `MealPlanOfferGroup.CampMealId`, unabhängig davon, ob der Plan abonniert ist oder Rezepte enthält.
- `CookingUnitMealState.CampMealId`, einschließlich gespeicherter Berechnungen, manueller Lösungen und Verifikationen.
- Mahlzeitbezogene `CookingUnitStructureAssignment.CampMealId`.
- Teilnehmer-Abwesenheit `ParticipantAbsentMeal.MealId` im Camp-Kontext.
- Explizite Teilnehmerzuordnungen aus `ParticipantMealOverride.CampMealId`, einschließlich leerer Overrides.

Abgeleitete Ansichten und allgemeine Standardabonnements ohne konkrete Slotreferenz gelten nicht als Verwendung. Audit-Historie bleibt erhalten und verhindert allein kein Entfernen eines unbenutzten Slots. Weitere persistierte Slotreferenzen müssen bei Implementierung durch eine repositoryweite Suche in diese Prüfung aufgenommen werden.

Bei Verwendung: `SetActive(false)`, alle Referenzen behalten, Ergebnis Deactivated. Ohne Verwendung: Slot löschen. Eine danach unreferenzierte Additional-Art kann im selben Vorgang entfernt werden; globale Sortierung anschließend konsistent normalisieren. Keine Kaskadenlöschung von Teilnehmer- oder Planungsdaten. Die Referenzprüfung muss Camp- und Catering-Kontext unter derselben Transaktion sowie konkurrierende Referenzerzeugung berücksichtigen.

Auch die bisherige Entfernung regelmäßiger Arten benötigt diese vollständige Prüfung. Referenzierte regelmäßige Arten werden weiterhin nicht über den Namenslistenabgleich entfernt; die API liefert einen ausdrücklichen Konflikt, statt Daten zu löschen. Das automatische Deaktivieren betrifft im ersten Umfang nur das Entfernen zusätzlicher Tagesmahlzeiten.

## Zeitraum und fachliche Aktualität

- Verkürzen löscht oder deaktiviert keine Slots. Außerhalb liegende Slots werden aus aktueller Navigation, offenen Pflichtbelegungen und Berechnungsauswahl ausgeschlossen.
- Erneutes Erweitern stellt die vorhandenen IDs, Aktivitätszustände und Tagesreihenfolgen wieder in den gültigen Zeitraum. Bewusst deaktivierte Slots bleiben deaktiviert. Fehlende Regular-Slots werden ergänzt; Additional-Slots niemals automatisch erzeugt.
- Anwesenheit für einen neuen Slot folgt der bestehenden Teilnehmerlogik, einschließlich Tagesabwesenheit. Bestehende mahlzeitenbezogene Ausnahmen werden nicht von anderen Mahlzeiten kopiert.
- Neue aktive Slots erscheinen als offen, ohne automatisch persistierte Einzelstände für jede Kocheinheit zu erzeugen. Berechnet oder verifiziert wird ausschließlich durch die vorhandenen expliziten Aktionen.
- Aktivitätsänderungen machen vorhandene Berechnungs-/Verifikationsstände über die bestehende Fingerprint-Logik veraltet. Reine Reihenfolgeänderung verändert keine Eignung oder Mengenbasis.
- Zeitraumänderungen müssen gegen die tatsächlich verwendeten Bedarfs- und Supply-Fingerprints geprüft werden. Alte Ergebnisse dürfen nach Wiedereintritt nicht ohne Prüfung als aktuell gelten; es wird keine neue pauschale Verifikationsregel eingeführt.

## Migration und Pakete

SQLite und PostgreSQL erhalten passende Migrationen für Kind, Tagesreihenfolge und Rahmenversion. Bestehende IDs und Referenzen bleiben unverändert. Kein Inferieren historischer Zusatzmahlzeiten aus Namen.

Vorschlag: äußeres Paketformat auf 2 anheben; Format 1 kontrolliert beim Lesen auf Regular, migrierte Tagesreihenfolge und initiale Rahmenversion aktualisieren. Format 2 enthält Kind, Slotreihenfolge und Rahmenversion. Alte Anwendungen müssen neue Pakete ablehnen, damit beim Rundlauf keine Zusatzart als regelmäßige Art zurückkehrt. Das Catering-Teilformat braucht nur dann eine weitere Version, wenn sich sein eigener Payload ändert; die Rahmenfelder liegen derzeit außerhalb dieses Teilformats.

Serializer, Cloud→Local, Local→Cloud, Referenzprüfung und lokale Lagerentfernung müssen denselben Stand unterstützen. Slots außerhalb des aktuellen Zeitraums werden vollständig exportiert. Rahmenversion nach Import nicht blind zurücksetzen; vorhandene Transfer-/Baseline-Sperren und lokale Mutationen gemeinsam testen.

## Umsetzung in überprüfbaren Schritten

1. **Backend und Paketvertrag:** Modell, beide Migrationen, vollständige Referenzprüfung, Versionierung, neue Operationen und Anpassung aller Slot-Erzeuger. Bestehenden Frontendvertrag kompatibel halten. Abnahme: Zusatzslot per API nur am gewünschten Tag; Verlängern erzeugt keine Kopien; Rundlauf erhält IDs und Reihenfolge.
2. **Menüplan-UI:** „Mahlzeit hinzufügen“ in der gewählten Tagesansicht, kompakter Dialog mit Name; Tagesreihenfolge und Entfernen im Mahlzeitenmenü. Sofortige Speicherung des lagerweiten Rahmens klar von Speichern/Verwerfen des Menüplanentwurfs trennen. Rahmenaktionen bei ungespeicherten Planänderungen zunächst sperren und Speichern/Verwerfen verlangen, damit ein lokal verwendeter Slot nicht als unbenutzt gelöscht werden kann.
3. **Durchgängige Darstellung und Abnahme:** Dieselbe Reihenfolge und Aktivitäts-/Zeitraumfilter in Teilnehmer-Anwesenheit, Menüplanung, Übersicht und operativer Planung. Desktop und schmale Ansicht prüfen, keine weitere dauerhafte Kopfzeile hinzufügen.

## Notwendige Tests

- Bestehendes Lager migrieren: gleiche IDs, Namen, Aktivität und Referenzen; alle bisherigen Arten Regular.
- Zusatz an einem Tag; gleicher Zusatzname an anderem Tag; doppelt am selben Tag und Kollision mit Regular ablehnen; deaktivierten Slot gezielt reaktivieren.
- Normale Namensliste und Zeitraumverlängerung vervielfältigen keine Additional-Art.
- Umordnen verändert ausschließlich Tagesreihenfolge/Rahmenversion; andere Tage und berechnete fachliche Stände bleiben unverändert.
- Jede oben genannte Referenz separat verhindert Löschen; unreferenzierter Slot ist löschbar; paralleles Referenzanlegen darf keine verwaiste Referenz erzeugen.
- Veraltete Rahmenversion verhindert Mutation; zwei gleichzeitige Anlege-/Sortieroperationen erzeugen keine Duplikate oder Teilupdates.
- Verkürzen und erneutes Erweitern erhalten aktive sowie deaktivierte Slots, Angebote, Ausnahmen und Reihenfolge.
- Viewer, fremdes Lager, Freeze, Audit-Fehlschlag und Transaktionsrollback; keine Offenlegung von Gesundheitsdaten.
- Paket 1 importieren, Paket 2 in beide Richtungen übertragen; außerhalb liegende Slots und Teilnehmer-Ausnahmen erhalten; alte Version lehnt Paket 2 ab.
- UI mit offenem Planentwurf, Requestfehler und Versionskonflikt verliert keine ungespeicherten Änderungen. Fehlender Standard bleibt offen; NoSupplyRequired bleibt kocheinheitsbezogen.

Die Ausarbeitung selbst enthielt keine Laufzeittests. Die nachfolgende Umsetzung und ihre automatischen Nachweise sind in der [Abnahme](../testing/catering-additional-meals.md) dokumentiert; visuelle Browserabnahme und PostgreSQL-Live-Prüfung bleiben offen.
