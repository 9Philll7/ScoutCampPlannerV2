Für die Mockups ist die wichtigste Trennung: **Menüplanung legt fest, was angeboten wird. Die operative Verpflegungsplanung legt fest, wer welches Gericht in welcher Menge bekommt.** Beides gehört aktuell zum Bereich „Verpflegung“, sollte aber visuell klar getrennt werden.

Die folgende Beschreibung basiert auf der bestehenden Implementierung und den bestätigten Fachregeln. Die vorgeschlagene Aufteilung in Ansichten ist eine UX-Empfehlung, keine neue Fachfunktion.

## 1. Menüpläne: Was wird angeboten?

Ein Lager kann mehrere benannte Menüpläne besitzen, etwa „Standardplan“ und „Alternativplanung“. Ein Plan muss nicht verwendet werden; er kann zunächst als Planungsalternative bestehen bleiben.

Ein Menüplan enthält Angebote für konkrete Mahlzeiten des Lagers:

- Datum und Mahlzeitenart, beispielsweise „01.07. – Mittagessen“.
- Ausschließlich bereits aktivierte Lagermahlzeiten innerhalb des Lagerzeitraums.
- Je Mahlzeit eine oder mehrere Komponenten.
- Je Komponente ein Standardrezept und optional Alternativen.

**Beispiel:**

| Mahlzeit | Komponente | Standard | Alternativen |
|---|---|---|---|
| 01.07. Mittag | Hauptgericht | Gemüseauflauf | Veganer Gemüseauflauf |
| 01.07. Mittag | Beilage | Blattsalat | — |
| 01.07. Mittag | Nachspeise | Milchreis | Obstsalat |

Die Komponenten werden **zusätzlich zueinander** angeboten. Die Alternativen sind hingegen **Auswahlmöglichkeiten innerhalb derselben Komponente**.

### Verfügbare Aktionen

- Menüplan anlegen und benennen.
- Bestehenden Plan öffnen und bearbeiten.
- Reihenfolge der Pläne ändern.
- Komponenten für eine Mahlzeit hinzufügen oder entfernen.
- Standardrezept und Alternativen auswählen.
- Ein anderes Rezept zum Standard machen.
- Plan speichern oder Änderungen verwerfen.
- Plan löschen; bestehende Referenzen können das verhindern.

### Angaben pro Rezeptangebot

- Veröffentlichte Rezeptrevision.
- Standard oder Alternative.
- Optionaler eigener Anzeigename.
- Optionale Rolle: Hauptspeise, Beilage, Vorspeise, Nachspeise, Getränk, Sonstiges.
- Optionaler Freitexthinweis.

Die Rolle ist eine Beschreibung, **keine automatische Mengenregel**. „Vegan“ oder „vegetarisch“ sind keine Rollen.

### Speichern und Vollständigkeit

Ein Menüplan darf unvollständig gespeichert werden. Sichtbar sein sollte, welche aktiven Mahlzeiten noch kein Standardangebot haben.

„Alle Mahlzeiten belegt“ bedeutet nicht „für alle Teilnehmer geeignet“ oder „verifiziert“.

Änderungen werden gesammelt und erst beim Speichern wirksam. Versorgungsrelevante Änderungen erzeugen einen neuen fachlichen Planstand. Bis dahin gilt der zuletzt gespeicherte Stand.

## 2. Rezeptauswahl

Geplant werden veröffentlichte, unveränderliche Rezeptrevisionen, keine Entwürfe.

Für die Auswahl sind die zugänglichen veröffentlichten Rezepte verfügbar. Verwendete Revisionen werden in die Lager-Rezeptbibliothek aufgenommen.

Wichtig für die Darstellung:

- Rezeptname und Revision müssen unterscheidbar sein.
- Eine neue Veröffentlichung ersetzt eine bereits geplante Revision nicht automatisch.
- Der bewusste Wechsel einer Revision ist eine Planänderung.
- Varianten und Ersatzzutaten werden bei der konkreten Versorgung behandelt, nicht als beliebig viele zusätzliche Menüplan-Einträge benötigt.

## 3. Kocheinheiten: Für wen wird gekocht?

Eine Kocheinheit beschreibt Personen, die gemeinsam kochen und essen. Sie ist **nicht identisch mit einem Lagerstrukturknoten**.

Eine Kocheinheit besitzt:

- Name.
- Optional eine organisatorische Kocheinheitengruppe.
- Optional einen Standard-Menüplan.
- Eine Zuordnung zu einem oder mehreren Lagerstrukturknoten.
- Einen Verpflegungsfilter.
- Eine Gemeinsamkeitspräferenz.

Die verfügbaren Filter sind:

- Alle.
- Nur Personen mit Sonderverpflegung.
- Personen ohne Sonderverpflegung.

Teilnehmer werden nicht einzeln in der Menüplanung verteilt. Sie ergeben sich aus ihrer Lagerstrukturzuordnung, ihrer Anwesenheit bei der Mahlzeit und dem Filter.

Eine anwesende Person darf pro Mahlzeit nicht mehreren Kocheinheiten zugeordnet sein. Fehlende oder widersprüchliche Zuordnungen müssen als Problem sichtbar werden.

Kocheinheitengruppen dienen nur der Organisation. Sie besitzen keine eigene Bedarfsberechnung oder Einstellungsvererbung.

## 4. Konkrete Mahlzeit einer Kocheinheit

Die zentrale Arbeitseinheit lautet:

**Kocheinheit × Datum × Mahlzeit**

Beispielsweise „Gruppe A · 01.07. · Mittagessen“.

Für diesen Kontext gibt es drei Planmodi:

| Modus | Wirkung |
|---|---|
| Standardplan folgen | Verwendet den gespeicherten Standard-Menüplan der Kocheinheit. |
| Individuell planen | Eigene Rezeptauswahl für diese konkrete Mahlzeit. |
| Keine Versorgung erforderlich | Diese Kocheinheit benötigt bei dieser Mahlzeit keine Versorgung. |

Individuelle Planung und „Keine Versorgung“ werden durch spätere Änderungen des Standardplans nicht überschrieben.

Zusätzlich sind möglich:

- Eigene Strukturzuordnung nur für diese Mahlzeit.
- Rücksetzen der Strukturzuordnung auf den Standard.
- Manueller Portionsbedarf.
- Eigener Zielbedarf je Menükomponente.
- Rücksetzen auf den Standard-Menüplan.

**Beispiel:** Hauptgericht für 30 Portionen, Beilage für 30, Nachspeise nur für 20. Die Komponenten werden nicht zu einem gemeinsamen Bedarf von 80 Personen addiert.

## 5. Bedarfsbasis und Berechnung

Lagerweit wird zwischen zwei Betriebsarten gewählt:

- Schätzplanung.
- Reale Teilnehmer verwenden.

Der konkrete Berechnungsstand weist aus, welche Basis tatsächlich verwendet wurde:

- Schätzung.
- Reale Teilnehmer.
- Schätzung als Fallback.

Der Fallback ist nur vorgesehen, wenn im betreffenden Kontext keine realen Teilnehmerdaten vorliegen. Vorhandene, aber fehlerhaft zugeordnete Teilnehmer dürfen nicht durch eine Schätzung verdeckt werden.

Die Berechnung berücksichtigt die bestehenden Bedarfs- und Mengenregeln. Die zuletzt ergänzte Stufen-/KiJu-/Leiterzuordnung darf im Mockup nicht als neue, automatisch wirksame Berechnungsregel dargestellt werden.

### Sichtbare Informationen

- Berechneter Bedarf.
- Manueller Override, falls vorhanden.
- Wirksamer Bedarf.
- Tatsächlich verwendete Bedarfsbasis.
- Zuordnungsprobleme.
- Berechnungsstatus.

Eine Berechnung wird bewusst ausgelöst. Änderungen relevanter Grundlagen markieren betroffene Ergebnisse als veraltet; sie überschreiben nicht unbemerkt den gespeicherten Berechnungsstand.

## 6. Konkrete Versorgung und Sonderverpflegung

Teilnehmer mit gleichen relevanten Anforderungen werden zu **Anforderungsgruppen** zusammengefasst. Unterschiedliche Grenzwerte können unterschiedliche Gruppen erzeugen.

Eine konkrete Versorgungslösung enthält:

- Rezeptrevision.
- Gewählte Varianten oder Ersatzentscheidungen.
- Portionsmenge.
- Zuordnung dieser Portionen zu Anforderungsgruppen.
- Herkunft: automatisch oder manuell.

Eine Lösung kann mehrere Anforderungsgruppen versorgen.

Die Eignung wird anhand des tatsächlich resultierenden Gerichts geprüft, einschließlich verschachtelter Unterrezepte:

- Allergene.
- Ernährungsformen anhand der Zutatenherkunft.
- Quantitative Stoffgehalte.
- Qualitative Angaben.
- Unbekannte relevante Daten.

Ein Ersatz ersetzt den ursprünglichen Beitrag vollständig. Unbekannte Daten dürfen nicht als „unbedenklich“ erscheinen.

### Automatische Auflösung

Die Reihenfolge ist fest:

1. Geeignete direkte Varianten-/Ersatzlösung suchen.
2. Passende Alternative derselben Menükomponente prüfen.
3. Operatorentscheidung verlangen.

Dabei gilt:

- Eine eindeutige vollständige Direktlösung kann automatisch verwendet werden.
- Mehrere unabhängige eindeutige Anpassungen können kombiniert werden.
- Eine Teillösung bleibt offen.
- Genau eine passende Menüalternative kann automatisch gewählt werden.
- Mehrere passende Alternativen verlangen eine Entscheidung.
- Keine automatische Suche in der gesamten Rezeptbibliothek.
- Keine rekursiven Ersatzketten.

Für Mockups sollten Konflikte deshalb nachvollziehbar benannt werden: betroffene Gruppe, Rezeptposition, Ursache und verfügbare Handlung.

## 7. Gemeinsam oder getrennt versorgen

Die Gemeinsamkeitspräferenz beeinflusst geeignete Lösungen, darf aber keine Eignungsprüfung überstimmen.

| Einstellung | Verhalten |
|---|---|
| Gemeinsam bevorzugen | Eine geeignete gemeinsame Lösung für mehrere Gruppen wird bevorzugt. |
| Nachfragen | Wenn gemeinsame und getrennte Versorgung gültig sind, entscheidet der Benutzer. |
| Getrennt bevorzugen | Gruppen bleiben getrennt; exakt identische Rezeptrevisionen und Anpassungen dürfen zusammengeführt werden. |

Bei „Nachfragen“ braucht es eine echte Entscheidung:

- Gemeinsam versorgen.
- Getrennt versorgen.

Erst danach werden die entsprechenden Lösungen erzeugt. Ein Wechsel ersetzt die betroffenen Lösungen, statt zusätzliche parallele Lösungen anzulegen.

## 8. Manuelle Bearbeitung und Mengenabdeckung

Der Benutzer kann:

- Eine eigene Versorgungslösung hinzufügen.
- Rezeptrevision und Portionsmenge ändern.
- Varianten oder Ersatz pro konkreter Rezeptposition wählen.
- Portionen Anforderungsgruppen zuordnen.
- Zuordnungen oder Lösungen entfernen.
- Manuelle Entscheidungen speichern oder zurücksetzen.

Manuelle Entscheidungen bleiben bei Neuberechnung erhalten. Werden sie ungeeignet, müssen sie sichtbar und prüfbedürftig bleiben.

Die Oberfläche muss drei unterschiedliche Mengenprobleme unterscheiden:

- **Unterdeckung:** Eine Gruppe erhält zu wenige Portionen.
- **Überdeckung:** Einer Gruppe sind zu viele Portionen zugeordnet.
- **Nicht zugeordnete Portionen:** Portionen sind geplant, aber keiner Anforderungsgruppe zugeteilt.

Berechnete Zutatenmengen gehören zur konkreten Lösung. Lineare, fixe und stufenweise Mengen dürfen dabei nicht visuell gleichgesetzt werden.

## 9. Berechnungsstatus und Verifikation

Diese Informationen müssen getrennt dargestellt werden:

| Ebene | Zustände |
|---|---|
| Menüplanbelegung | Mahlzeiten belegt / Angebote fehlen |
| Berechnungsstand | Aktuell / Veraltet / Unvollständig |
| Fachliche Probleme | Konflikte, unbekannte Daten, Mengenabweichungen |
| Verifikation | Nicht verifiziert / ohne Abweichung / mit Abweichung / veraltet |

„Aktuell“ bedeutet nur, dass die Berechnung auf den relevanten aktuellen Grundlagen beruht.

Für die Verifikation gibt es:

- **Ohne Abweichung verifizieren:** Voraussetzungen erfüllt, keine relevanten offenen Probleme.
- **Mit Abweichung verifizieren:** Bewusste Entscheidung mit Pflichtbegründung.

Gespeichert werden Prüfstand, prüfende Person und Zeitpunkt. Relevante Änderungen machen die Verifikation veraltet. Eine neue Verifikation ersetzt die alte; es gibt weder Bearbeitungssperre noch eine Verifikationshistorie.

Verifizieren benötigt eine separat vergebene Berechtigung.

## 10. Empfohlene Mockup-Ansichten

Diese Aufteilung bildet bestehende Funktionen ab, ohne neue Fachmodule einzuführen:

1. **Menüplanübersicht**  
   Pläne, Namen, Versionen, fehlende Mahlzeiten und Aktionen.

2. **Menüplan bearbeiten**  
   Tage/Mahlzeiten als zentrale Übersicht; Detailbereich für Komponenten, Standard und Alternativen; Speichern/Verwerfen.

3. **Kocheinheiten**  
   Name, Strukturbezug, Filter, Standardplan und Gemeinsamkeitspräferenz.

4. **Versorgungsübersicht**  
   Kocheinheiten und Mahlzeiten mit Bedarf, Aktualität, Konflikten und Verifikation.

5. **Mahlzeitendetail**  
   Planmodus, Bedarfsbasis, Anforderungen, konkrete Lösungen, Portionen, Anpassungen und Zutatenmengen.

6. **Entscheidungs- und Verifikationsbereich**  
   Gemeinsam/getrennt, mehrdeutige Alternativen, manuelle Entscheidungen und begründete Verifikation.

Ein Kalender-/Rasterlayout, Filter oder Detailpanel sind dabei Gestaltungsvorschläge — keine Behauptung, dass diese Präsentationsformen bereits existieren.

## Grenzen für die Mockups

Nicht als vorhandene Funktionen darstellen:

- Automatische Einkaufsplanung oder Warenlagerverwaltung.
- Kostenoptimierung.
- Produktionsküchen oder Ausgabelogistik.
- Automatische medizinische Freigabe.
- Neue Lagerphasen oder künstliche Fortschrittsprozente.
- Automatische Umstellung auf neuere Rezeptrevisionen.
- Produktiv freigegebenen Offline-Transport sensibler Teilnehmerdaten.

Im aktuellen Frontend steht außerdem noch der veraltete Hinweis „Die Versorgungseignung ist noch nicht vollständig integriert“. Dieser widerspricht dem abgeschlossenen Inkrement-2-Stand und sollte **nicht als fachliche Vorgabe in die Mockups übernommen werden**.

Grundlagen: [Menüplanung](D:/GitLab/ScoutCampPlannerV2/docs/domain/catering-meal-planning.md), [Sonderverpflegung und Verifikation](D:/GitLab/ScoutCampPlannerV2/docs/domain/catering-meal-planning-increment-2.md), [strukturbezogene Teilnehmerzuordnung](D:/GitLab/ScoutCampPlannerV2/docs/decisions/meal-planning-participant-structure-correction.md).