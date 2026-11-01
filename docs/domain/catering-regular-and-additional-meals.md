# Regelmäßige Mahlzeiten und Zusatzmahlzeiten

Stand: 2026-11-01. Separates Folgeinkrement nach abgeschlossener UX-V2-Abnahme implementiert. Ergebnis und Prüfgrenzen: [Abnahme](../testing/catering-additional-meals.md).

## Zielbild

Ein Lager besitzt konfigurierbare regelmäßige Mahlzeiten, beispielsweise Frühstück, Mittagessen und Abendessen. Diese sind an den Lagertagen grundsätzlich vorgesehen und müssen durch Angebote abgedeckt oder ausdrücklich ausgenommen werden.

Zusätzlich können an einzelnen Tagen weitere Mahlzeiten angelegt werden, beispielsweise ein Mitternachtssnack. Dafür darf keine zunächst lagerweit aktive Mahlzeitenart angelegt und anschließend an allen anderen Tagen deaktiviert werden müssen.

Beide verwenden dieselben bestehenden fachlichen Abläufe: aktive Lagermahlzeit, Menüplankomponenten, Standardrezept/Alternativen und operative Planung pro Kocheinheit. Zusatzmahlzeiten sind keine neue Rezeptrolle oder Komponente einer anderen Mahlzeit.

## Bestehender Stand

- Lageranlage erzeugt Frühstück, Mittagessen und Abendessen an jedem Tag des Lagerzeitraums.
- Die Mahlzeitenarten sind lagerweit konfigurierbar. Eine neue Art erzeugt derzeit an jedem Lagertag einen aktiven Slot.
- Ein bestehender Slot kann lagerweit deaktiviert werden (`CampMeal.IsActive`).
- Pro Kocheinheit gibt es unabhängig davon den Planmodus `NoSupplyRequired`.
- Es gibt aktuell keinen Vertrag zum gezielten Anlegen einer zusätzlichen Tagesmahlzeit.

Geprüfte Stellen: `CampManagementService`, `CateringPlanningService.UpdateMealTypesAsync`, `CampMeal` und die bestehenden Mahlzeiten-/Planungsendpunkte.

## Bestätigte fachliche Unterscheidung

| Fall | Wirkung |
|---|---|
| Regelmäßige Mahlzeit | Standardmäßig an jedem Lagertag vorgesehen. |
| Zusätzliche Tagesmahlzeit | Wird bewusst für einen ausgewählten Tag ergänzt. An anderen Tagen entsteht keine Planungspflicht. |
| Mahlzeit entfällt lagerweit | Der konkrete Slot ist ausdrücklich deaktiviert; keine neue Gleichsetzung mit einem unvollständigen Menüplan. |
| Eine Kocheinheit benötigt keine Versorgung | Nur ihr operativer Teilstand verwendet `NoSupplyRequired`; die Lagermahlzeit bleibt für andere Einheiten aktiv. |

Eine aktive, aber unbelegte Mahlzeit bleibt sichtbar unvollständig. Fehlendes Angebot darf nicht automatisch als „keine Versorgung“ gelten. Auch ein unvollständiger Menüplan bleibt gemäß bestehendem Vertrag speicherbar.

## Bestätigte Regeln für Tageszuordnung und Lebenszyklus

- Eine Zusatzmahlzeit gehört zum ausdrücklich ausgewählten Lagertag. Auch ein Mitternachtssnack wird nicht automatisch dem Folgetag zugeordnet; es wird keine Uhrzeitlogik eingeführt.
- Neue Zusatzmahlzeiten stehen zunächst hinter den regelmäßigen Mahlzeiten. Die Reihenfolge kann pro Tag geändert werden und gilt lagerweit für die Planungsansichten dieses Tages.
- Unbenutzte Zusatzmahlzeiten dürfen gelöscht werden. Bereits verwendete Zusatzmahlzeiten werden stattdessen deaktiviert; vorhandene Zuordnungen und Referenzen bleiben erhalten. Welche Referenzen eine Mahlzeit als verwendet kennzeichnen, muss die Vertragsarbeit vollständig bestimmen.
- Wird der Lagerzeitraum verkürzt, bleiben Mahlzeiten außerhalb des neuen Zeitraums samt Referenzen erhalten, werden aber für die aktuelle Planung nicht berücksichtigt. Die technische Abbildung und das Verhalten bei erneuter Erweiterung sind noch zu konkretisieren.

Diese Regeln wurden vom Nutzer bestätigt und im Folgeinkrement umgesetzt.

## Vorgesehene UX

- Regelmäßige Mahlzeiten bleiben in der Lagerkonfiguration verwaltbar.
- Die Tagesansicht erhält „Mahlzeit hinzufügen“ für Name und ausgewählten Lagertag.
- Eine Zusatzmahlzeit erscheint nur am entsprechenden Tag in der Mahlzeitennavigation und in den relevanten Planungsansichten.
- Eine regelmäßige Mahlzeit kann an einem Tag ausdrücklich als entfallend markiert werden.
- Lagerweite Deaktivierung und „keine Versorgung erforderlich“ für eine Kocheinheit sind eindeutig getrennt beschriftet.
- Keine zusätzliche Menüplan-spezifische Slot-Erzeugung: Der lagerweite Rahmen bleibt Grundlage aller Menüpläne. Nicht jeder Menüplan muss unmittelbar vollständig sein.

## Vertragsarbeit als Grundlage der Umsetzung

1. Regelmäßige Mahlzeitendefinition und gezielt ergänzten Tages-Slot unterscheiden. Vorhandene `CampMealType`-/`CampMeal`-Identitäten und Referenzen erhalten; keine ungesicherte Umdeutung bestehender Datensätze.
2. Autorisierten Anlegevertrag für Zusatzmahlzeiten mit Datum innerhalb des Lagerzeitraums und Name ergänzen. Namens-/Sortier-/Duplikatregeln festlegen. Kein konkreter neuer Endpoint oder DB-Typ gilt mit diesem Dokument bereits als beschlossen.
3. Bestehenden Namenslistenvertrag prüfen: Einmalige Zusätze dürfen nicht versehentlich durch einen späteren Aufruf der lagerweiten Mahlzeitenartenverwaltung auf alle Tage vervielfältigt werden.
4. Auswirkungen auf Mahlzeitenbelegung, Planversionen, Berechnungsaktualität, Verifikation und Anwesenheit fachlich prüfen. Bestehende Mechanismen verwenden, keine stillen Neuberechnungen oder automatischen Verifikationen.
5. Freeze, Berechtigungen, Paketexport/-import und lokalen Betrieb mitführen. Paket-/Migrationsbedarf vor Implementierung bestimmen.
6. Die bestätigten Regeln für Entfernung, Deaktivierung und Zeitraumänderungen technisch abbilden. Insbesondere alle relevanten Referenzen und die Abgrenzung „unbenutzt“ bestimmen sowie erhaltene Slots bei erneuter Zeitraum-Erweiterung behandeln.

## Technische Ausarbeitung

Der [Vertrags- und Umsetzungsvorschlag](../architecture/catering-meal-schedule-contract.md) konkretisiert Datenmodell, API, Referenzschutz, Zeitraumverhalten, Paketmigration und Tests. Wiederverwendbare Additional-Arten, eindeutige Namen pro Tag und ein einzelner ausgewählter Tag wurden umgesetzt. Unbenutzte Additional-Arten bleiben nach Entfernung ihres letzten Slots gespeichert; sie erzeugen keine regelmäßigen Slots. Prüfstand und manuelle Schritte stehen in der [Abnahme](../testing/catering-additional-meals.md).

## Im technischen Vorschlag konkretisierte Fragen

- Dürfen mehrere gleich benannte Zusatzmahlzeiten am selben Tag existieren?
- Darf ein Zusatz für mehrere ausgewählte Tage in einer Aktion erstellt werden? Der bestätigte Mindestumfang ist ein einzelner ausgewählter Tag.
- Welche Referenzen verhindern das Löschen als „unbenutzt“, und wie werden erhaltene Slots bei erneuter Zeitraum-Erweiterung berücksichtigt?
- Bleibt eine zusätzliche Mahlzeitenart wiederverwendbar oder wird sie nur für diesen Slot benötigt? Erst nach Prüfung des bestehenden Modells entscheiden.

## Abnahmekriterien

- Regelmäßige Mahlzeiten bleiben an allen vorgesehenen Tagen sichtbar; unbelegte aktive Slots bleiben offen.
- Ein Mitternachtssnack am gewählten Tag erzeugt keine Slots oder offenen Belegungen an anderen Tagen.
- Die Tageszuordnung bleibt auch beim Namen „Mitternachtssnack“ unverändert; Zusatzmahlzeiten erscheinen zunächst am Ende und lassen sich pro Tag umsortieren.
- Unbenutzte Zusatzmahlzeiten können entfernt werden; bei verwendeten bleibt beim Deaktivieren jede Zuordnung erhalten.
- Mahlzeiten außerhalb eines verkürzten Lagerzeitraums bleiben gespeichert und werden in der aktuellen Planung nicht mitgezählt.
- Zusatzmahlzeiten sind in mehreren Menüplänen nutzbar und verwenden normale Komponenten und Rezeptrevisionen.
- Ein ausdrücklich deaktivierter Slot zählt nicht als fehlendes Standardangebot.
- `NoSupplyRequired` einer Kocheinheit deaktiviert keinen Slot für andere Einheiten.
- Relevante Änderungen machen betroffene Berechnungen/Verifikationen gemäß bestehender Semantik veraltet.
- Referenzen, Rechte, Freeze und lokaler Paketrundlauf bleiben korrekt.

Grundlagen: [Menüplanung](catering-meal-planning.md), [Sonderverpflegung und Verifikation](catering-meal-planning-increment-2.md). Die bisherige Inkrementgrenze „keine zusätzlichen Slots aus Catering“ wird durch diesen separaten Auftrag gezielt erweitert; kein pauschaler Ersatz des übrigen Fachmodells.
