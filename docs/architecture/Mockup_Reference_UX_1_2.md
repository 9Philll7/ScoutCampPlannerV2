# Mockup-Referenzen für UX Increment 1.2

**Stand:** 2026-10-13  
**Status:** Visuelle Referenz, kein fachliches Datenmodell  
**Bildformat:** getrennte Desktopansichten, ca. 16:9

## Referenzen

| Kontext | Datei | Zweck |
|---|---|---|
| Organisationsmodus – Start | `mockups/organization-start-reference.png` | Sidebar, Markenkontext, Startseiten-Komposition, Lagerkarten, Typografierhythmus |
| Lagermodus – Lagerübersicht | `mockups/camp-overview-reference.png` | Dieselbe Shell im Lagermodus, Lagerkopf, kompakter Informationsstand, Aufgaben-/Aktionsstruktur |

## Visuell verbindlich

- Ruhige **Modern-Outdoor**-Ästhetik gemäß `Visual_Design_Guide.md`.
- **Linke Sidebar**, kein horizontaler zweiter Hauptnavigationsbereich.
- Eindeutige aktive Organisation; im Lagermodus auch Name des geöffneten Lagers und „Alle Lager“ als Rückweg.
- Wiedererkennbare Shell über beide Modi; klare Ausrichtung von Überschrift, Inhaltsbreite und Karten/Listen.
- Leichte Flächenkontraste, großzügige Abschnittsabstände, sparsame Akzentfarbe, konsistente Controls.
- Desktop-first, responsive ohne Verlust wichtiger Kontextinformationen.

## Nicht verbindlich / ausdrücklich nicht übernehmen

- Beispielnamen, Beispieltermine, Teilnehmerzahlen, Mahlzeitenzahlen und sämtliche Mockup-Testdaten.
- **Prozentuale Planungsgrade, Progress-Balken, Progress-Ringe und „Abgeschlossen“-Behauptungen** ohne Fachgrundlage.
- Module und Aktionen, die die aktuelle Anwendung nicht anbietet (z. B. Kalkulation oder Programm als voll funktionsfähiger Bereich).
- Phasensteuerung oder Archivierungslogik; ausschließlich gesondert dokumentiertes Zukunftskonzept.
- Dekorative Landschaftsfotos oder Bildbanner: optional und möglichst zurückhaltend, keine Pflicht.
- Exakte Bildpixel als CSS-Vorgabe; die Screenshots sind keine responsive Spezifikation.

## Inhaltsmodell Organisations-Start

1. Organisationskontext in Sidebar eindeutig anzeigen.
2. Titel und kurze Orientierung zum Startbereich.
3. **Zuletzt verwendete Lager**, sofern tatsächlich vorhanden.
4. **Weitere zugängliche Lager**, ohne Duplikate des zuletzt verwendeten Bereichs.
5. Ein sachlicher Leerzustand, falls keine Lager existieren; nur verfügbare, erlaubte Aktionen anbieten.
6. Schnellzugriffe nur dann, wenn sie den Nutzerweg gegenüber der globalen Navigation tatsächlich verbessern.

## Inhaltsmodell Lagerübersicht

1. Lagername, Zeitraum und Organisation als Kontext.
2. Kompakte, echte fachliche Zusammenfassungen; **keine fiktiven Planungsprozente**.
3. **Offene Aufgaben lagerweit**, aber zunächst nur aus bereits verfügbaren Fachdaten und Zuständen. Keine neue Aufgabenverwaltung.
4. Je Aufgabe: verständlicher Name, Fachkontext, direkter Einstieg, keine sensible Information für Unberechtigte.
5. Platz für zukünftige Fachbereiche konzeptionell vorsehen; ohne implementierte Funktion nichts anzeigen.

## Arbeitsanweisung für Codex

Bei Konflikten zwischen Screenshot und fachlich bestätigtem Verhalten gilt der Fach-/Guide-Text. Bilder sollen Größenverhältnisse, Komposition und Anmutung transportieren; **keine Daten, API oder Produktfunktionen begründen**. Abweichungen zu Screenshots für Responsivität, Rechteschutz oder echten Datenstand transparent dokumentieren.
