# Lagerphasen – UX-/Domänenkonzept (zukünftiges funktionales Inkrement)

**Status:** Konzeptionell beschlossen, **nicht im Scope von UX Increment 1.2**. Vor Implementierung mit vorhandener Fachlogik und Datenmodell abgleichen.

## Lebenszyklus

1. **Grobplanung** (lange vor dem Lager): Kalkulation, Struktur, Schätzwerte der Teilnehmer, Rezepte, Mahlzeitenplan.
2. **Detailplanung** (kurz vor dem Lager): konkrete Teilnehmer, Konfliktbehebung, Einkaufslisten.
3. **Durchführung** (während des Lagers): Einkaufslisten, Kommissionierung, Faktoren anpassen, Lagermanagement, Abrechnung.
4. **Nachbereitung** (nach dem Lager): Abrechnung, Rezeptüberarbeitung und Veröffentlichung.
5. **Archivierung**: separater abschließender Zustand/Vorgang, **keine reguläre fünfte Planungsphase**.

## Festgelegte UX-Prinzipien

- Die **aktuelle Phase wird manuell** von einem berechtigten Benutzer gesetzt; niemals automatisch allein durch den Kalender.
- **Systemweite zeitliche Standardwerte** bieten Orientierung; Anpassung soll **direkt bei der Lagererstellung** und später über Lagereinstellungen möglich sein.
- Konkrete Dauer/Vorlaufparameter sind noch **nicht verbindlich** bestimmt. Die bisher diskutierte Frist „sechs Wochen“ ist nur ein Beispiel.
- Funktionen bleiben grundsätzlich phasenübergreifend zugänglich; Phase **priorisiert relevante Arbeitseinstiege**, sie sperrt keine Fachmodule.
- Keine künstlichen „X % fertig“-Aussagen; Phase ist kein Fertigstellungsgrad.
- Offene fachlich ableitbare Aufgaben bleiben unabhängig von der Phase sichtbar.
- Archivierung und ihre Auswirkungen auf Bearbeitbarkeit, Wiederöffnung, Speicherung und Berechtigungen gesondert spezifizieren.

## Offene fachliche Implementierungsfragen

- Existieren Phasenfelder, Konfigurationswerte und notwendige Anwendungsfälle bereits?
- Wie genau werden systemweite und lagerindividuelle zeitliche Vorschlagswerte modelliert?
- Welche Rechte gelten für Wechsel/Archivierung?
- Welche Anzeige entsteht aus bestehenden Fachzuständen, ohne neue Aggregation?

**Abgrenzung:** Keine stillschweigende Datenmigration, kein neuer API-Vertrag und keine automatische Phase in UX 1.2.
