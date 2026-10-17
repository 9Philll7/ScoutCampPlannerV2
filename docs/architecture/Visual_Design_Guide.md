# ScoutCampPlanner – Visual Design Guide

## Ergänzung UX Overhaul 1 (2026-10-16)

Für den beauftragten Neuaufbau sind große Änderungen an Templates, Komponentenaufteilung und visuellem Stil ausdrücklich freigegeben. Frühere Einschränkungen auf minimale Anpassungen gelten hierfür nicht. API, Fachlogik, Rechte und Speichersemantik bleiben unverändert.

Die gemeinsame Shell erhält in Organisations- und Lagermodus eine dunkle waldgrüne Sidebar nach den Strukturreferenzen. Dies ersetzt die bisherige helle Sidebar als Gestaltungsziel; es ist kein Dark Theme der Anwendung. Arbeitsflächen bleiben hell. Navigationstokens: Hintergrund `#12392F`, Text `#F3F7F3`, Sekundärtext `#BCCEC5`, Auswahl `#2B5545`, Fokus/Auswahlkante `#C6D9A8`, Trennlinien `#38594E`.

Die Desktop-Sidebar verwendet 272px als bewusste Abweichung vom bisherigen Richtwert, um Marke und lange Kontextnamen ausreichend Raum zu geben. Gemeinsame Inhaltsabstände betragen 32px, schmal 16px; Übersichtscontainer maximal 1200px, links an derselben Inhaltskante wie breite Arbeitsansichten. Der lokale Struktur-/Teilnehmerwechsel steht im Seitenkopf im Dokumentfluss, auch im Bearbeitungsmodus. Große Änderungen sind erst nach gerendertem Bildvergleich visuell abgenommen.

**Status:** Verbindliche UX-/UI-Gestaltungsgrundlage (Version 1.0, 2026-10-13)  
**Designrichtung:** Modern Outdoor  
**Geltung:** Web-Anwendung und Desktop-/Offline-Oberfläche  
**Ablage:** `docs/Architecture/Visual_Design_Guide.md`

## 1. Ziel und Prioritäten

ScoutCampPlanner ist ein produktives Planungstool für Pfadfinderlager. Sein Interface soll ruhig, modern, zugänglich und effizient sein. „Outdoor“ wird über warme, natürliche Neutralfarben und zurückhaltende Waldgrün-Akzente vermittelt, **nicht** über Holztexturen, dekorative Zelte oder verspielte Illustrationen.

Prioritäten (in dieser Reihenfolge):
1. Orientierung und verständliche Informationshierarchie
2. Lesbarkeit, Tastaturbedienung und klare Interaktionszustände
3. Effizienz bei Tabellen, Formularen und Planungsabläufen
4. Einheitliches Erscheinungsbild
5. Dezente visuelle Identität

Keine UI-Änderung darf vorhandene Fachlogik, Domänenmodelle, Datenverträge, Berechtigungen, Offline-Flows oder Speichersemantik verändern.

## 2. Design Tokens (verbindliche Ausgangswerte)

Token-Namen beschreiben semantische Funktionen. Komponenten **verwenden Tokens statt verstreuter Literalwerte**. Bestehende CSS-/Theme-Strukturen bevorzugen; bei abweichender Namenskonvention semantisch gleichwertig abbilden.

### 2.1 Farben – Light Theme

| Token | Wert | Verwendung |
|---|---|---|
| `--color-canvas` | `#F6F7F3` | Seitenhintergrund |
| `--color-surface` | `#FFFFFF` | Panels, Menüs, Karten |
| `--color-surface-muted` | `#EFF2EC` | Unterlegte Bereiche, inaktive Flächen |
| `--color-text` | `#203029` | Primärtext |
| `--color-text-muted` | `#526158` | Sekundärtext |
| `--color-border` | `#D8E0D8` | Rahmen, Trennlinien |
| `--color-primary` | `#315B45` | Primäraktionen, Akzent |
| `--color-primary-hover` | `#264936` | Hover auf Primäraktionen |
| `--color-primary-soft` | `#E1EBE2` | Aktive Navigation, subtile Hervorhebung |
| `--color-on-primary` | `#FFFFFF` | Text auf Primärflächen |
| `--color-focus` | `#276D80` | Gut sichtbarer Fokusindikator |
| `--color-danger` | `#A33D36` | Fehler und destruktive Aktionen |
| `--color-warning` | `#835A16` | Warnungen |
| `--color-info` | `#256A79` | Neutrale Systemhinweise |
| `--color-success` | `#326B4A` | Erfolg (nicht als einziger Informationsträger) |

**Regeln:**
- Farbe bezeichnet Rolle oder Zustand, nie rein dekorative Wichtigkeit.
- Niemals Grün für alle Links, Status und Buttons gleichermaßen verwenden.
- Interaktive Elemente sind auch ohne Farbwahrnehmung unterscheidbar.
- Für Text, Icons und Statusindikatoren Kontrast prüfen: normaler Text mindestens WCAG AA 4,5:1; großer Text 3:1; wesentliche Bedienelemente/Fokusindikatoren 3:1 gegen angrenzende Farben.
- Die Tabelle beschreibt **Light Theme**. Ein Dark Theme ist derzeit nicht Teil des Auftrags. Keine ungetesteten automatischen Farbumkehrungen.

### 2.2 Typografie

- UI-Schrift: `Inter`, **nur wenn bereits verfügbar oder datenschutzkonform lokal eingebunden**; ansonsten `system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif`. Keine neue externe Font-CDN-Abhängigkeit.
- Eine einzige Sans-Serif-Familie für Navigation, Überschriften, Tabellen, Formulare und Buttons. Keine Serifentitel.
- Standardtext: 16px, Zeilenhöhe 1.5; kompakte Tabellen/Metainformationen bei Bedarf 14px, nicht unter 12px.
- Seitenüberschrift: 28–32px / 1.2, 600–700; Abschnitt: 20–24px / 1.3, 600; Unterabschnitt: 16–18px / 1.4, 600.
- Navigation: 14–15px, 500 (aktiv 600). Labels: 14px, 500. Unterstützende Angaben: 13–14px.
- Zahlen in Tabellen bei Bedarf `font-variant-numeric: tabular-nums`.
- Lange Organisationsnamen dürfen umbrechen oder kontrolliert gekürzt werden; vollständiger Name muss zugänglich bleiben.

### 2.3 Abstände und Geometrie

- Basisraster 4px; bevorzugte Spacing-Tokens: 4 / 8 / 12 / 16 / 24 / 32 / 48px.
- Karten/Abschnitte: 16–24px Innenabstand; Zwischenräume zwischen Hauptabschnitten: 24–32px.
- Border-Radius: 6px (klein), 10px (Controls), 12px (Panels), 16px (große Karten). Pill nur für kleine Chips, wenn fachlich sinnvoll.
- Rahmen 1px; Schatten nur bei schwebenden Ebenen (Popover, Dialog), nicht als Standard für jede Karte.
- Klick-/Touchziele ausreichend groß gestalten; als Richtwert mindestens 40×40px, auf Touch-Oberflächen 44×44px anstreben.

## 3. Layout und Anwendungsshell

### 3.1 Grundaufbau

- Klarer Kopfbereich mit Marke, aktivem Kontext und Benutzeraktionen; keine zusammengewürfelte einzeilige Linkleiste.
- Navigationsflächen eindeutig von der Inhaltsfläche getrennt; aktive Position klar erkennbar.
- Start- und Übersichtsseiten: Inhaltscontainer maximal ca. 1120px; Formulare/Leseseiten bei Bedarf enger. Tabellen und Planung dürfen die verfügbare Breite nutzen.
- Desktop-Innenabstand typischerweise 24–32px; schmale Ansichten 16px. Keine großen unstrukturierten Leerräume durch rein textuelle Links.
- Seite beginnt mit Kontextangabe (falls nötig), einem **einzigen** primären Seitentitel und anschließend klaren Abschnitten.

### 3.2 Kontext- und Navigationsmodell (bereits festgelegt)

- Es gibt eine aktive Organisation; der persönliche Start bezieht sich auf diese Organisation.
- **Organisationsmodus:** Start, Lager, Rezepte, Zutaten, Organisation.
- **Lagermodus:** Die globale Navigation wird ersetzt durch Übersicht, Struktur & Teilnehmer, Verpflegung, Rezepte, Zutaten und Einstellungen. Weitere Bereiche nur, wenn funktional vorhanden.
- Organisation und geöffnetes Lager bleiben erkennbar; kein direktes Wechselmenü zwischen Lagern. Lagerwechsel über Start/Lagerübersicht.
- Organisationswechsel explizit außerhalb des Lagers.
- Navigation ist nicht mit Datenbearbeitung gleichzusetzen: Rezept- und Zutatenkontexte strikt auseinanderhalten.
- Aktive Navigation: dezenter `primary-soft`-Hintergrund **plus** stärkeres Textgewicht bzw. Orientierungselement; `aria-current` oder äquivalente Semantik.
- Rückwege, Browser-History und bestehende Hash-Deep-Links dürfen nicht verloren gehen.

## 4. Komponenten- und Interaktionsregeln

### Buttons und Links

- Primärbutton: gefülltes Waldgrün mit weißem Text; **eine dominante Aktion pro Bereich**.
- Sekundärbutton: neutrale Oberfläche mit Rahmen; tertiäre Aktion: klarer Textbutton.
- Links nur für Navigation; Aktionen als Buttons. Link-Interaktion nicht nur durch Grün kennzeichnen (z. B. Unterstreichung oder konsistente Form).
- Hover, Fokus, Disabled und Loading klar unterscheiden; niemals deaktiviert als einzige Erklärung anbieten.

### Listen und Karten

- Lager sind eindeutig klickbare Zeilen oder Karten mit Titel, wenigen Metadaten und erkennbarer Öffnungsaktion; keine lose Linkliste.
- Karten sparsam einsetzen. Nicht jeden Abschnitt in weitere verschachtelte Karten packen.
- Leere Zustände enthalten eine kurze Erklärung und nur dann eine Handlungsaufforderung, wenn die Aktion tatsächlich verfügbar und erlaubt ist.

### Tabellen

- Klare Kopfzeilen, ruhige horizontale Trennung, saubere Zahlenausrichtung; kompakt aber mit ausreichender Zeilenhöhe.
- Suche/Filter kontextbezogen und sichtbar; Spalten nicht unkontrolliert umbrechen.
- Bei schmaler Breite lieber horizontal scrollen als Daten unlesbar verdichten.
- Sensible Angaben, Spalten, Suchoptionen, Filter und Exporte nur mit effektiver Berechtigung; UI-Ausblendung ersetzt nie Backend-Autorisierung.

### Formulare und Editoren

- Felder logisch gruppieren; sichtbare Labels, kurze Hilfetexte und feldnahe Validierung.
- Explizites Speichern und Abbrechen respektieren. Ungespeicherte Änderungen bei Navigation berücksichtigen.
- Anzeigen und Bearbeiten unterscheidbar halten; keine unaufgeforderte automatische Speicherung einführen.
- Dialoge für fokussierte Entscheidungen, nicht für umfangreiche Fachprozesse.

### Status und Meldungen

- Fehler: verständlicher Grund + nächster Schritt; technische Details nur nachgeordnet.
- Unterscheidung zwischen Information, Warnung, Fehler, Erfolg, Entwurf, lokal/zentral und Offline-/Schreibschutzstatus konsistent.
- Konflikte, Ernährungseignung und Prüfstatus nie ausschließlich über Farbe vermitteln.
- Nur reale Daten und vorhandene Zustände zeigen; keine erfundenen Fortschrittswerte oder Aufgaben.

## 5. Spezifische Seitenmuster

### Organisations-Start

1. Kompakter Seitenkopf mit aktivem Organisationskontext.
2. Zuletzt verwendete Lager als gut erkennbare Einträge; bei leerem Verlauf sauberer Leerzustand.
3. Zugängliche Lager als klare Liste oder Karten mit Öffnungsaktion.
4. Weitere Arbeitsbereiche bei Bedarf kompakt verlinken; keine doppelte Navigationsleiste im Inhalt.

### Lagerübersicht

- Lagerkopf, kompakter belastbarer Planungsstand, lagerweite **nur fachlich ableitbare** Aufgaben mit direktem Einstieg. Keine erfundenen Prozentzahlen.

### Struktur & Teilnehmer

- Struktur als navigierbare Hierarchie; eigener Strukturbearbeitungsmodus.
- Teilnehmer tabellarisch mit separaten Details; Stufe und KiJu/Leiter-Erwachsene sichtbar, soweit berechtigt.
- Schätzwerte nur auf teilnehmerführenden Blattknoten, getrennt nach KiJu und Leiter/Erwachsenen; optionale Stufenauswertung.
- Gesundheitsbezogene Angaben durch Berechtigungen schützen, auch in Filtern und Aggregationen.

### Verpflegung

- Aufgabengerechte Orientierung zwischen Mahlzeitenplan, Versorgungsentscheidungen, Konflikten und Verifikation; nicht technische interne Zustände dominieren lassen.
- Fachlogik unverändert; Designänderungen dürfen keine Versorgungsentscheidung vorwegnehmen.

### Rezepte und Zutaten

- Konsistente Bearbeitungsmuster; Herkunft, lokal/zentral, Revisionen und Änderungsstatus nachvollziehbar.
- Mandantenpflege außerhalb des Lagers; lagerbezogene Bearbeitung im Lager.

## 6. Barrierefreiheit und responsive Verhalten

- Tastaturbedienung, Fokus-Reihenfolge und sichtbarer Fokus für alle interaktiven Elemente.
- Labels, Rollen, Überschriftenhierarchie und Statusmeldungen semantisch verständlich.
- Kontrastziele aus Abschnitt 2 einhalten; Status niemals nur farblich markieren.
- Desktop-first, aber nutzbar bei schmalem Fenster. Kein horizontaler Scroll der **gesamten App**; lokale Tabellen dürfen scrollen.
- Testbreiten als Richtwert: ca. 1440px, 1024px und 390px; gegebenenfalls abweichende Projekt-Breakpoints übernehmen.
- Reduzierte Bewegung beachten; keine Animation als Voraussetzung zur Bedienung.

## 7. Definition of Done für visuelle Änderungen

1. Vorhandene Token-/Style-Infrastruktur geprüft; semantische Tokens zentral definiert.
2. Betroffene Screens nutzen dieselben Typografie-, Abstands-, Button-, Link- und Navigationsregeln.
3. Keine losen Grün-Textlinks als Ersatz für Lagerkarten oder primäre Aktionen.
4. Organisation, Lager und aktueller Bereich sind verständlich und konsistent bezeichnet.
5. Leerzustände, Hover, Fokus, Disabled und Fehlermeldungen geprüft.
6. Desktop und schmale Viewports visuell geprüft; keine problematischen Überläufe.
7. Vorhandene Fachflows, Offline-Funktionen, Rechte und Navigationstests bleiben intakt.
8. Änderungen anhand von Vorher-/Nachher-Screenshots und Testnachweisen dokumentiert.

## 8. Grenzen und Änderungsprozess

Dieser Guide ist verbindlich für **neue oder visuell überarbeitete** Oberflächen, keine Freigabe für ein vollständiges Rewrite. Falls bestehende Komponenten technische Grenzen setzen, diese dokumentieren und den kleinsten sauberen gemeinsamen Ansatz wählen. Neue visuelle Muster oder Token-Abweichungen bewusst und zentral dokumentieren, statt lokale Sonderstile zu etablieren.

**Nicht durch diesen Guide festgelegt:** neue Funktionen, zusätzliche Rollen/Rechte, neue Datenbankfelder, neue Berechnungsregeln, ein Dark Theme, ein neues Frontend-Framework oder ein kompletter Editorumbau.

## 9. Ergänzung v1.1 – verbindliche Referenz für UX Increment 1.2

**Beschluss:** Für Desktop wird eine **linke Sidebar** anstelle der horizontalen Hauptnavigation verwendet. Diese Entscheidung ersetzt widersprechende ältere Layoutvorschläge in diesem Dokument. Die bestehende fachliche Trennung von Organisationsmodus und Lagermodus gilt unverändert.

### 9.1 Referenzmockups und ihr Status

- `docs/architecture/mockups/organization-start-reference.png`: visuelle Referenz für Organisationsmodus und Startseite.
- `docs/architecture/mockups/camp-overview-reference.png`: visuelle Referenz für Lagermodus und Lagerübersicht.
- `docs/architecture/Mockup_Reference_UX_1_2.md`: legt verbindliche und illustrative Anteile der Bilder fest.

Beide Bilder sind **16:9-nahe Desktop-Referenzen**, keine pixelgenauen Spezifikationen. Mockup-Beispielzahlen, Beispiel-Lager, erfundene Module, Aufgaben, Statuswerte und evtl. Fortschrittsanzeiger dürfen **nicht** als funktionale Anforderungen übernommen werden. Das schriftliche Fach-/Navigationskonzept und tatsächlich vorhandene Daten haben Vorrang vor Bilddetails.

### 9.2 Desktop-Shell

- Desktop: linke Sidebar als primäre Navigation, Richtbreite **240–260 px**; bewusst kompakter Header-/Utility-Bereich. Die Seite nutzt den übrigen Raum, mit wohldefinierten Inhaltsmaximalbreiten für Übersichten (ca. **1120–1200 px**, nicht erzwungen für Tabellen).
- Sidebar: App-Identität; eindeutige **aktive Organisation**; kontextbezogene Navigationspunkte; unten Benutzer-/Verbindungsinformationen, soweit technisch vorhanden. Keine zusätzliche horizontale Hauptnavigation.
- Organisationsmodus: `Start`, `Lager`, `Rezepte`, `Zutaten`, `Organisation` (je nach Berechtigung).
- Lagermodus: `Übersicht`, `Struktur & Teilnehmer`, `Verpflegung`, `Rezepte`, `Zutaten`, `Einstellungen` sowie nur tatsächlich vorhandene weitere Fachbereiche. Deutlicher Lagername und expliziter Weg **„Alle Lager“** zurück; kein Direktwechsel zwischen Lagern.
- Der Organisationswechsel bleibt explizit und ist während des Lagermodus nicht direkt auszuführen. Die Organisation ist auch im Lagermodus erkennbar.
- Sidebar und Inhaltsfläche behalten über beide Modi hinweg dieselbe Gestaltsprache und räumliche Orientierung.
- Für schmale Viewports: adaptive bzw. einklappbare Navigation mit Keyboard- und Screenreader-Unterstützung; kein permanenter horizontaler Scroll der gesamten Anwendung; Hauptkontext und Rückweg dürfen nicht verschwinden.

### 9.3 Seitenkomposition

- Kopf- bzw. Kontextbereich und Seiteninhalt folgen einer gemeinsamen horizontalen Ausrichtung; keine isolierten Titel oder lose Linklisten.
- Hauptabschnitte erhalten eine erkennbare Hierarchie durch **Abstände und Typografie**; nicht jede Untergruppe braucht eine Karte.
- Illustrationen oder Landschaftsfotos sind **optional**, niemals notwendige Voraussetzung oder verpflichtender Header. Bevorzugt werden text- und datenorientierte, ruhige Start- und Lagerübersichten ohne dekorative Bilder.
- Keine Dopplung sämtlicher Sidebar-Punkte als „Weitere Arbeitsbereiche“-Karten. Nur besonders hilfreiche Einstiege, wenn sie gegenüber der Sidebar Mehrwert bieten.
- Bei einer einzelnen zugänglichen Lagerkarte keine leeren Mehrfachraster künstlich auffüllen. Kein doppeltes Anzeigen desselben Lagers in „Zuletzt verwendet“ und „Weitere Lager“.

### 9.4 Lagerübersicht – fachlicher Informationsvertrag

Die Lagerübersicht besteht konzeptionell aus: **Lagerkopf**, **kompaktem, belastbarem Planungsstand** und **lagerweiten, aus existierenden Fachzuständen abgeleiteten offenen Aufgaben**. Zusätzliche kontextbezogene Einstiege sind zulässig, wenn sie real verfügbar und hilfreich sind.

**Explizit untersagt:** allgemeine Fertigstellungsprozente, Progress-Ringe, Fortschrittsbalken „Planung zu X %“, erfundene To-dos, synthetische Fertigstellungszustände und berechnete Planungsgrade ohne fachlich definiertes Modell. Das gilt auch, wenn solche Elemente in Referenzbildern erscheinen.

- In Kennzahlendarstellungen nur reale, korrekt beschriftete Daten anzeigen; unterscheiden zwischen **geschätzten** und **erfassten** Teilnehmerzahlen, soweit vorhanden.
- Planungsstand möglichst fachbereichsweise in **knappen, inhaltlich belastbaren** Zeilen/Karten zeigen; pro Bereich maximal wenige aussagekräftige Zahlen und keine Redundanz zur offenen Aufgabenliste.
- Erweiterbarkeit für zukünftige Bereiche (z. B. Kalkulation, Programm) im Layout vorsehen, diese **nicht als funktionierende Module oder Platzhalter** simulieren, solange sie nicht existieren.
- Wenn keine fachlich belastbaren Zusammenfassungswerte oder offenen Aufgaben verfügbar sind, einen kompakten, ehrlichen Leer-/Informationszustand zeigen. Keine neue Aggregationslogik auf Verdacht implementieren.
- Fachlich berechtigte Daten- und Aufgabenanzeigen müssen bestehenden Berechtigungen entsprechen, insbesondere bei sensiblen Teilnehmerinformationen.

### 9.5 Neue Funktionalität: Phasen nur dokumentieren

Die diskutierten Lagerphasen `Grobplanung`, `Detailplanung`, `Durchführung` und `Nachbereitung` mit **manueller Phase**, **systemweiten Zeit-Vorschlagswerten**, **individueller Anpassung bei der Lagererstellung** und **gesonderter Archivierung** sind ein späteres fachliches Inkrement. Sie werden in `docs/domain/Camp_Lifecycle_Phases_UX_Concept.md` dokumentiert. UX Increment 1.2 darf **keine Phasenfelder, Zeitautomatismen, Status-APIs, Berechtigungen oder Archivierungsfunktionen** neu einführen. Eine Phasenanzeige darf nur erscheinen, falls dies bereits eine vorhandene echte Funktion ist; sonst wird sie vorerst weggelassen.

### 9.6 Priorität bei Widersprüchen

1. Bestehende Fachlogik, Berechtigungen, tatsächlich verfügbare Funktionen und Daten.
2. Verabschiedete Informationsarchitektur und die Regeln dieses Guides.
3. Schriftlich erläuterte Mockup-Referenzen in `Mockup_Reference_UX_1_2.md`.
4. Visuelle Details der PNG-Mockups als **inspirierende Referenz**, nicht als fachliches Pflichtenheft.

### 9.7 Zusätzliche visuelle Abnahme für UX 1.2

- Manuelle Sichtkontrolle bei ungefähr **1440 × 900**, **1024 × 768** und **390 × 844** (oder abweichenden projektspezifischen Breakpoints); Screenshots und bekannte Abweichungen dokumentieren.
- Organisations- und Lagermodus auf denselben Shell-Abständen, Sidebar-Breiten, Icongrößen, aktiven Navigationszuständen und Schriftstufen prüfen.
- Zustände mit **0, 1 und mehreren** zugänglichen Lagern; lange Organisations-/Lagernamen; leere Aufgabenliste; fehlende Kennzahlen; Offline-/Schreibschutzanzeigen prüfen.
- Navigationswechsel, URL/Hash-Deep-Links, Browser-Zurück, Login/Logout, Rechte und konservativen Unsaved-Changes-Schutz regressionstestieren.
