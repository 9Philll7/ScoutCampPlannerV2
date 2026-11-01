# Mockup-Index Verpflegung

Bilder sind **visuelle Referenzen**, keine Datenmodell-/API-Spezifikationen. Im Konfliktfall gilt die vorhandene Fachdokumentation des Repositories, danach die bestätigten UX-Entscheidungen. Technische Abweichungen dokumentieren, nicht eigenmächtig Fachlogik erfinden.

| Bild | Verwendung | Zu beachten |
|---|---|---|
| 01_verpflegung_uebersicht.png | **Freigegebene Richtung**: Planungsstatus, Handlungsbedarf, optional zuletzt bearbeitet | Keine Menüplanvorschau; nur vorhandene Kennzahlen, keine erfundenen Prozentwerte; „Zuletzt bearbeitet“ nur bei echten Daten; keine doppelte Navigation. |
| 02_menueplan_uebersicht.png | **Gestaltungsreferenz** für mehrere Pläne | Enthält noch zu viel Editorfläche. Tatsächliche Übersicht als eigenständige Planliste, Editor separat. „Aktiv verwendet“ nur falls faktisch belegt, nicht Plan-Aktivierungsstatus. |
| 03_menueplan_editor.png | **Freigegebenes Hauptlayout**: kompakter Plankopf, Tagesliste, Mahlzeitentabs | Komponentenkarten durch Bild 05 ersetzen; keine neuen Funktionen wie Optimierung. |
| 04_rezeptauswahl_workflow.png | **Referenz für Rezeptauswahldialog**, Liste + Revision-Detail rechts | Bild-Collage enthält überholte Aktion zur Standardfestlegung im Dialog: verwerfen. Erstes Rezept wird Standard, weiteres Alternative; Standardwechsel **nur in Komponente**. |
| 05_komponenten_zweispaltig.png | **Freigegebene visuelle Referenz** für Komponenten | Standard links, Alternativen kompakt rechts; nicht 1:1 Beispielzahlen/Labels übernehmen; bei schmalem Viewport stapeln. |
| 06_kocheinheiten_verwaltung.png | **Freigegebenes Split-View-Grundlayout** für organisatorische Konfiguration | Ein Filter pro Kocheinheit, Strukturknoten, Standard-Menüplan, Gemeinsamkeitspräferenz; Beispiel-Teilnehmerzahlen nicht als Fakt behandeln. |
| 07_kocheinheit_strukturzuordnung.png | **Freigegebene Gestaltung mit einer wichtigen Bildkorrektur** | Elternknoten mit nur teilweise gewählten Kindern muss **indeterminate / Minus**, nicht Häkchen zeigen (auch „Gruppe 1“ im Bild). Auswahl eines Elternknotens selektiert alle Nachkommen. |
| 08_operative_mahlzeit_automatisch_geloest.png | **Bevorzugte einfache Normalansicht** | Automatisch aufgelöste Anforderungen direkt sichtbar; die Bild-Reiter nicht zwingend übernehmen; Verifikation separat; keine internen Gruppennummern anzeigen. |
| 09_operative_versorgungsplanung.png | **Referenz für Mahlzeitennavigation je Kocheinheit** | Linke Spalte navigiert **Mahlzeiten der aktuellen Kocheinheit**, nicht Kocheinheiten. Rechts konkrete Mahlzeit; Struktur-Editor separat. |
| 10_operative_mahlzeit_offener_konflikt.png | **Ergänzende Referenz** für offene Konflikte im Grundeditor | Stammdaten-/Kocheinheitenliste links im Bild ist veraltet; nur Konflikt- und Statusdarstellung berücksichtigen. |

## Verbindliche, bildübergreifende Korrekturen

- Menüplan enthält aktive Mahlzeiten → additive Komponenten → je Standardrezept und optionale Rezeptalternativen. **Menüpläne enthalten keine Kocheinheiten**.
- Kocheinheit referenziert Strukturknoten, besitzt genau **einen vordefinierten Verpflegungsfilter** und kann einen Standard-Menüplan abonnieren. Die Filterdefinition wird **nicht** im Kocheinheiteneditor bearbeitet.
- Die **organisatorische Kocheinheitenverwaltung** (schneller Wechsel zwischen Kocheinheiten) ist getrennt von der **operativen Versorgungsplanung** (eine Kocheinheit, ihre Mahlzeiten nacheinander).
- Automatisch eindeutige Lösungen erscheinen im normalen Mahlzeitendetail; offene Ask-/Alternativentscheidungen und Konflikte haben eine gezielte Aktion. Manuelle Spezialfunktionen unter „Weitere Anpassungen“.
- Interne Gruppennummern („Gruppe 3“) niemals als UI-Beschreibung von **Anforderungsgruppen** verwenden; fachliche Anforderungsbeschreibung anzeigen.
- Berechnungsaktualität, fachliche Konfliktfreiheit und Verifikation **getrennt** anzeigen.
- Angaben wie Teilnehmerzahlen, ausgewiesene Ernährungs-Labels, Rezeptbilder, neue Tabs oder nicht belegte Funktionen sind illustrativ.
