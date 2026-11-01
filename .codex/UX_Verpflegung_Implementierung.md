# Codex-Auftrag – UX Verpflegung (schrittweise Umsetzung)

## Auftrag und Ziel

Überarbeite die UX des bestehenden ScoutCampPlanner-Bereichs **Verpflegung** anhand der mitgelieferten Mockups und der bestehenden Fachmodelle. **Struktur & Teilnehmer ist bereits fertig und nicht Gegenstand dieses Auftrags.** Keine pauschale Neuentwicklung oder neue Geschäftslogik. Implementiere **in getrennten Inkrementen**, sodass jedes manuell geprüft werden kann. **Beginne nur mit Bestandsanalyse und UX-V1**; folgende Inkremente nicht stillschweigend gleichzeitig umsetzen.

### Verbindliche Eingaben

1. `docs/architecture/mockups/catering/README.md` lesen. Jedes zugehörige Original-PNG **visuell öffnen** und mit der existierenden UI vergleichen, statt allein Dateinamen oder diese Beschreibung zu verwenden.
2. `docs/domain/implementierungsstand_verpflegung_vom_user.txt` als vom Nutzer gelieferte Zusammenfassung des Implementierungsstands lesen.
3. Im Repository die originären Domain-/Decision-Dokumente und tatsächlichen API-Verträge überprüfen, insbesondere (Pfade nach Existenz prüfen): `docs/domain/catering-meal-planning.md`, `docs/domain/catering-meal-planning-increment-2.md`, `docs/decisions/meal-planning-participant-structure-correction.md`.
4. Bestehende Design-Tokens, Angular-Komponenten, Berechtigungslogik, Offline-/Synchronisierungskonzept, Tests und UI-Routing überprüfen. Fehlende Dateien oder Widersprüche dokumentieren.

## Fachliche Trennung (nicht verhandelbar)

**Menüplanung = was angeboten wird.** Lager kann mehrere benannte Menüpläne haben, auch ungenutzte. Menüplan → aktive Lagermahlzeit → eine oder mehrere **additive Komponenten** → je ein Standardrezept plus beliebig viele Optionen/Alternativen. Es werden **veröffentlichte unveränderliche Rezeptrevisionen** zugeordnet. Neue Revisionen ersetzen bestehende Zuordnungen nicht automatisch. Eine Menüplanänderung wird gesammelt und durch explizites **Speichern/Verwerfen** übernommen. Ein unvollständiger Menüplan darf gespeichert werden. Verfügbare optionale Rolle/Anzeigename/Freitext gemäß Domainmodell nutzen; keine neue Typisierung erfinden.

**Kocheinheitenverwaltung = für wen grundsätzlich gekocht wird.** Kocheinheit besitzt Name, optional organisatorische Gruppe, Zuordnung zu einem/mehreren Strukturknoten, **genau einen** wiederverwendbaren Filter (aus für Lager/Organisation verfügbaren Filtern; Bedingungen kombinieren Stufe, Teilnehmerkategorie und Ernährung), optional abonnierten Standard-Menüplan sowie Gemeinsamkeitspräferenz. Filterauswahl ist **ein Dropdown**, kein komplexer Filtereditor. Filterherkunft/-beschreibung kann read-only angezeigt werden. Organisatorische Kocheinheitengruppe hat keine eigene Mengenberechnung oder Vererbung. Der Kocheinheiten-Split-View dient raschem Wechsel zwischen Einheiten.

**Strukturauswahl:** Elternknoten anwählen wählt alle Nachkommen; wird ein Nachkomme abgewählt, zeigen alle betroffenen Vorfahren einen teilweisen Zustand (Minus), kein Häkchen. Voll = alle Nachkommen voll; leer = keiner; teilweise = einige. Dies ist die *UX-Auswahlsemantik*; prüfe vor Implementierung, wie effektive Auswahlen aktuell gespeichert werden und welche Backend-Vorgaben gelten. Kein stiller Schemaumbau. Nicht notwendigerweise alle übergeordneten und nachgeordneten Knoten als unabhängige Duplikatzuordnungen speichern.

**Operative Versorgungsplanung = was konkrete Kocheinheit an konkreter Mahlzeit tatsächlich ausgibt.** Kerneinheit Kocheinheit × Datum × Mahlzeit. Menüplan wird **abonniert**, ist kein Container für Kocheinheiten. Planmodi: Standardplan folgen, individuell planen, keine Versorgung erforderlich. Mahlzeitbezogene Overrides für Strukturzuordnung, wirksamen Bedarf, komponentenweisen Zielbedarf etc. sind selten und unter **„Weitere Anpassungen“** erreichbar. In der normalen Ansicht nur Kontext, geplantes Angebot, wirksamer Bedarf, automatisch erzeugte Versorgungslösungen, offene Probleme und die *getrennten* Statuswerte zeigen. **Keine dauerhafte zweite Kocheinheitenliste** in der operativen Planung: Benutzer arbeitet die Mahlzeiten **einer** gewählten Kocheinheit nacheinander ab.

**Sonderverpflegung und Konflikte:** Anforderungsgruppen sind fachliche Gruppen von Personen mit gleichen relevanten Anforderungen. In der UI beschreibende Anforderungen (etwa „3 Personen mit laktosefreier Ernährung“) anzeigen; **niemals interne Anforderungsgruppen-IDs wie „Gruppe 3“**. Eindeutige vollständige Varianten-/Ersatzlösung automatisch sichtbar machen, ohne Dialogöffnung. Alternativen derselben Komponente nach den bestehenden Regeln prüfen; bei mehrdeutigen Fällen Benutzer entscheiden lassen. „Nachfragen“ erfordert *vor* dem Erzeugen der Lösungen explizit „Gemeinsam“ oder „Getrennt“; alte Alternativlösungen nicht parallel belassen. Konflikt-/Ask-Editor als großer Dialog oder eigene Detailseite nur bei tatsächlichem Handlungsbedarf, mit bestehenden Berechtigungen und transparenter Wirkung.

**Bedarf und Status:** Bedarfsbasis Schätzung/reale Teilnehmer/Fallback, berechnet/manuell/wirksam getrennt; berechnete Mengen nicht ohne fachliche Grundlage addieren. Unterdeckung, Überdeckung und nicht zugeordnete Portionen unterscheiden. Berechnung bewusst auslösen; relevante Änderungen markieren Berechnung veraltet. Eignung/Konflikte, Aktualität und Verifikation eigenständig abbilden. Verifikation ohne Abweichung / mit Begründung bei Abweichung nur gemäß vorhandenen Rechte-/Statusmodellen. Automatisch gelöst ≠ verifiziert.

## UX-Inkremente und Screens

### UX-V1 – Verpflegungsübersicht (jetzt umsetzen)

Referenz `01_verpflegung_uebersicht.png`. Lage der Module und Stil nahe am Original. Zeige ausschließlich **belegbare** Daten zu Menüplanung, Kocheinheiten, offenen Konflikten, Ask-Entscheidungen und ausstehenden Verifikationen. Priorisierte Handlungsbedarfe mit Links zum fachlich richtigen Ziel. **Keine Menüplan-Tagesvorschau**. „Zuletzt bearbeitet“ nur wenn erforderliche Änderungsdaten existieren; andernfalls weglassen. Keine erfundenen Prozentsätze/Fertigstellungswerte. Sidebar-Einträge nur für tatsächlich vorhandene/implementierte Routen: Verpflegung → Übersicht, Menüpläne, Kocheinheiten, Konflikte & Entscheidungen. „Versorgung“ und „Einkauf“ sind eigenständige Lager-Sidebar-Bereiche und dürfen nicht als fertige Features erfunden werden. Bestehende Camp-Navigation respektieren.

### UX-V2 – Menüpläne (erst nach Abnahme V1)

Referenzen `02`, `03`, `04`, `05`. Menüplanübersicht als **eigenständige Liste** mehrerer Pläne mit Aktionen; keine dauerhaft breite Planliste im Editor. Editor mit kompakter Planauswahl im Kopf, links Tagenavigation, oben aktive Mahlzeiten, Hauptfläche mit Komponenten. **Standardrezept prominent links, Alternativen kompakt rechts** (bei schmaler Breite untereinander). Erste Rezeptauswahl einer leeren Komponente wird Standard; weitere werden zunächst Alternative. **„Als Standard setzen“ nur als separate Komponentenaktion**, nicht als Wahl beim Hinzufügen. Rezeptauswahl: durchsuchbare zugängliche veröffentlichte Revisionen mit Detailvorschau. Ganze Planbearbeitung Speichern/Verwerfen; unvollständiger Stand zulässig. Optionale Rezeptrollen nicht als Ernährungs-/Mengenregeln missbrauchen. Entfernte/referenzierte Pläne gemäß vorhandener Validierung handhaben.

### UX-V3 – Kocheinheitenverwaltung (nach Abnahme V2)

Referenzen `06`, `07`. Organisatorischer Split-View: Einheitenliste + Details, schnelles Umschalten. Einstellen von Name, Strukturknoten mit **korrekter Tri-State-Hierarchie**, genau einem gespeicherten organisations-/lagerweiten Filter per Dropdown, Standard-Menüplan und Gemeinsamkeitspräferenz. Filterdefinition nicht hier bearbeiten. Vorhandene Persistenzsemantik (Einzelspeicherung oder Entwurf) **vorab prüfen und ehrlich in der UI ausdrücken**; keine falsche Sammelspeicherung aus Bildern erfinden. Keine vermuteten Teilnehmerzahlen anzeigen.

### UX-V4 – Operative Versorgungsplanung (nach Abnahme V3)

Referenzen `08`, `09`, `10`. Einstieg in Versorgung einer *ausgewählten* Kocheinheit; einfache Mahlzeitennavigation für diese Einheit. Standardmodus kompakt und überwiegend read-only: Angebot, automatisch geeignete Lösungen, Portionsbedarf, Status. Offene Probleme gezielt bearbeitbar. Seltene manuelle Planmodi, Overrides, individuelle Struktur/Mengen/Rezepte unter „Weitere Anpassungen“, nicht im Normalbild. Kein struktureller Kocheinheiteneditor und keine zweite Kocheinheitenliste. Vollständige Korrektheit der Lösungs-/Ask-/Verifikationslogik gegenüber der bestehenden Domäne bewahren. Keine internen Anforderungsgruppennummern darstellen.

## Implementierungsverfahren

1. **Ist-Analyse**: Screens/Routen, Datenverträge, Rechte, Status, verfügbare Felder, vorhandene Tests und konkrete visuelle Unterschiede auflisten. Keine Featurefähigkeit aus Bildern ableiten.
2. **Scope-Verifikation**: Nur UX-V1 umsetzen; wenn notwendige Daten nicht im Vertrag sind, reduzierten ehrlichen UI-Zustand anzeigen und Abweichung dokumentieren. Domain-/API-/DB-/Auth-Änderungen nicht eigenmächtig ergänzen.
3. **Visuelle Angleichung**: Original-PNG direkt inspizieren. Typografie, Raster, Abstände, Farben, Sidebar und Hierarchie exakt so weit wie technisch und fachlich möglich angleichen; keine neue Designrichtung.
4. **Qualitätssicherung**: Vorhandene Angular-Tests und Production Build ausführen. Visuelle Screenshots in Desktopauflösung und schmaler Ansicht erstellen, wenn möglich; nicht behaupten, diese geprüft zu haben, wenn Browser nicht verfügbar. Accessibility (Tastatur, Fokus, Dialog, Labels, Tri-State) und regressionsrelevante Routing-/Offline-Flows beachten.
5. **Bericht**: `.codex/ux-verpflegung-v1-result.md` mit Änderungen, fachlichen Abweichungen, Testresultaten, visuellen Vergleichsergebnissen, unverifizierten Punkten und offenen Aufgaben erstellen. **Kein Commit.** Auf Abnahme warten, bevor UX-V2 beginnt.

## Abnahmetests (über Inkremente verteilt)

- Übersichtskennzahlen tatsächlich herleitbar, kein implizites „automatisch verifiziert“, keine fiktiven Prozentwerte.
- Mehrere Menüpläne, unvollständige Pläne, Standard-/Alternativwechsel, immutable Recipe Revision, Speichern/Verwerfen.
- Strukturbaum: vollständiges Häkchen nur falls alle Nachkommen ausgewählt; Teilabwahl → rekursives Minus; Filter = genau einer; Berechtigungen beachten.
- Kein Nutzer doppelt durch Überschneidung von Kocheinheiten bei derselben Mahlzeit, oder vorhandene Konflikterkennung deutlich anzeigen; keine neue Verteilungsregel.
- Standardplan/individuell/keine Versorgung; manuelle Overrides und Reset; Schätzung/real/Fallback richtig kennzeichnen.
- Automatisch vollständig gelöste Anforderungen sichtbar ohne Öffnen des Konflikteditors; manuelle Lösungen bleiben gemäß Domain erhalten.
- Ask erzeugt **erst nach Auswahl** gemeinsame oder getrennte Lösungen und ersetzt die andere Möglichkeit statt zu duplizieren.
- Mengenunter-/überdeckung und unzugeordnete Portionen unterscheidbar; Berechnung veraltet nach relevanten Änderungen; Verifikation getrennt und begründete Abweichung nur mit Berechtigung.
- Keine internen Gruppenkennungen in Nutzertexten, keine sensiblen Gesundheitsinformationen ohne entsprechende Rechte.
- Visuelle Übereinstimmung pro zugehörigem Originalbild anhand echter Screenshots; Test und Build vollständig grün oder konkrete Fehler dokumentiert.
