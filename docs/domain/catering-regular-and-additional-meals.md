# Regelmäßige Mahlzeiten und Zusatzmahlzeiten

Stand: 2026-11-01. Fachliche Richtung vom Nutzer bestätigt; separates Folgeinkrement. Noch nicht implementiert. Keine Änderung an der laufenden UX-V2-Abnahme.

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

## Vorgesehene UX

- Regelmäßige Mahlzeiten bleiben in der Lagerkonfiguration verwaltbar.
- Die Tagesansicht erhält „Mahlzeit hinzufügen“ für Name und ausgewählten Lagertag.
- Eine Zusatzmahlzeit erscheint nur am entsprechenden Tag in der Mahlzeitennavigation und in den relevanten Planungsansichten.
- Eine regelmäßige Mahlzeit kann an einem Tag ausdrücklich als entfallend markiert werden.
- Lagerweite Deaktivierung und „keine Versorgung erforderlich“ für eine Kocheinheit sind eindeutig getrennt beschriftet.
- Keine zusätzliche Menüplan-spezifische Slot-Erzeugung: Der lagerweite Rahmen bleibt Grundlage aller Menüpläne. Nicht jeder Menüplan muss unmittelbar vollständig sein.

## Erforderliche Vertragsarbeit – noch nicht umgesetzt

1. Regelmäßige Mahlzeitendefinition und gezielt ergänzten Tages-Slot unterscheiden. Vorhandene `CampMealType`-/`CampMeal`-Identitäten und Referenzen erhalten; keine ungesicherte Umdeutung bestehender Datensätze.
2. Autorisierten Anlegevertrag für Zusatzmahlzeiten mit Datum innerhalb des Lagerzeitraums und Name ergänzen. Namens-/Sortier-/Duplikatregeln festlegen. Kein konkreter neuer Endpoint oder DB-Typ gilt mit diesem Dokument bereits als beschlossen.
3. Bestehenden Namenslistenvertrag prüfen: Einmalige Zusätze dürfen nicht versehentlich durch einen späteren Aufruf der lagerweiten Mahlzeitenartenverwaltung auf alle Tage vervielfältigt werden.
4. Auswirkungen auf Mahlzeitenbelegung, Planversionen, Berechnungsaktualität, Verifikation und Anwesenheit fachlich prüfen. Bestehende Mechanismen verwenden, keine stillen Neuberechnungen oder automatischen Verifikationen.
5. Freeze, Berechtigungen, Paketexport/-import und lokalen Betrieb mitführen. Paket-/Migrationsbedarf vor Implementierung bestimmen.
6. Referenzierte Zusatzmahlzeiten nicht unkontrolliert löschen. Verhalten bei Deaktivierung, Entfernung und Änderung des Lagerzeitraums vorab festlegen.

## Vor der Implementierung konkretisieren

- Dürfen mehrere gleich benannte Zusatzmahlzeiten am selben Tag existieren?
- Wie werden Zusatzmahlzeiten einsortiert, insbesondere ein Mitternachtssnack? Die derzeitigen Mahlzeiten haben keine Uhrzeit; keine Zeitlogik aus Mockups ableiten.
- Darf ein Zusatz für mehrere ausgewählte Tage in einer Aktion erstellt werden? Der bestätigte Mindestumfang ist ein einzelner ausgewählter Tag.
- Wie verhalten sich referenzierte oder bereits berechnete Slots bei Entfernung und Zeitraumänderungen?
- Bleibt eine zusätzliche Mahlzeitenart wiederverwendbar oder wird sie nur für diesen Slot benötigt? Erst nach Prüfung des bestehenden Modells entscheiden.

## Abnahmekriterien

- Regelmäßige Mahlzeiten bleiben an allen vorgesehenen Tagen sichtbar; unbelegte aktive Slots bleiben offen.
- Ein Mitternachtssnack am gewählten Tag erzeugt keine Slots oder offenen Belegungen an anderen Tagen.
- Zusatzmahlzeiten sind in mehreren Menüplänen nutzbar und verwenden normale Komponenten und Rezeptrevisionen.
- Ein ausdrücklich deaktivierter Slot zählt nicht als fehlendes Standardangebot.
- `NoSupplyRequired` einer Kocheinheit deaktiviert keinen Slot für andere Einheiten.
- Relevante Änderungen machen betroffene Berechnungen/Verifikationen gemäß bestehender Semantik veraltet.
- Referenzen, Rechte, Freeze und lokaler Paketrundlauf bleiben korrekt.

Grundlagen: [Menüplanung](catering-meal-planning.md), [Sonderverpflegung und Verifikation](catering-meal-planning-increment-2.md). Die bisherige Inkrementgrenze „keine zusätzlichen Slots aus Catering“ wird durch diesen separaten Auftrag gezielt erweitert; kein pauschaler Ersatz des übrigen Fachmodells.
