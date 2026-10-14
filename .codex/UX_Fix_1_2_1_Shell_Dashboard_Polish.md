# ScoutCampPlanner – UX Fix 1.2.1: Shell & Dashboard Polish

## Kontext

UX Increment 1.2 hat die linke Sidebar und die Trennung von Organisations- und Lagermodus implementiert. Die automatisierten Frontend-Tests und der Build waren erfolgreich. Die manuelle Sichtabnahme des Startbereichs und der Lagerübersicht zeigt jedoch konkrete Darstellungs- und Kompositionsprobleme.

**Verbindliche Grundlagen:** `docs/architecture/Visual_Design_Guide.md`, `docs/architecture/Mockup_Reference_UX_1_2.md`, vorhandene Mockup-Bilder und die bestehenden Implementierungen aus UX 1, 1.1 und 1.2. Das tatsächliche Verhalten des Repositories hat bei Detailfragen Vorrang vor fiktiven Mockup-Daten. Keine neue Fachlogik implementieren.

## Ziel

Gezielte visuelle Nachbesserung von **Startbereich**, **Lagerübersicht** und kleinen unmittelbar betroffenen Shell-Elementen. Bestehende Sidebar-Navigation, Berechtigungen, Hash-Routen, Offline-Paketaktionen und Datenflüsse bleiben funktional unverändert.

## 1. Bestandsaufnahme vor Änderung

- Ursache des auf dem Startscreen sichtbaren **dunkelgrünen, unbeschrifteten Rechtecks** identifizieren. Prüfen, ob Text/Icon unsichtbar, Templateinhalt leer, Rechtebedingung fehlerhaft oder CSS verantwortlich ist. Keine bloße optische Kaschierung ohne Ursachenklärung.
- Herkunft und Renderbedingung von „Weitere zugängliche Lager“ samt Leerhinweis prüfen.
- Vorhandene Layout-, Typografie- und Komponenten-Styles sowie Berechtigungsbedingungen prüfen.
- Bestätigen, welche Daten der Lagerübersicht tatsächlich vorliegen; keine Kennzahlen/Aufgaben aus fehlenden Daten ableiten.

## 2. Startbereich

- Unbeschriftete interaktive Fläche beheben: entweder korrekte, berechtigte Aktion mit sichtbarem Namen oder nicht benötigtes Element entfernen. Niemals ein blindes, fokussierbares Element belassen.
- Abschnitt „Weitere zugängliche Lager“ **vollständig ausblenden, wenn keine weiteren Lager existieren**. Die Liste der zuletzt verwendeten Lager darf keine Duplikate enthalten. Bei 0, 1 und mehreren Lagern sinnvoller, datengetriebener Zustand.
- Lagerkarten in konsistenter visueller Hierarchie: Name, ggf. realer Zeitraum und klar beschriftete Aktion „Lager öffnen“. Keine Fantasiedaten, keine obligatorischen Landschaftsbilder.
- Kopfbereich, Überschriften, Metadaten, Karten, Leerzustände und Abstände gemäß Design Guide verfeinern. Keine doppelte dominierende Anzeige desselben Organisationsnamens in Sidebar und Hauptinhalt; die aktive Organisation muss jedoch eindeutig erkennbar bleiben.
- Keine redundanten Kacheln als vollständige Kopie der Sidebar hinzufügen.

## 3. Lagerübersicht

- **Keine vier gleichwertigen großen Karten für überwiegend nicht vorhandene Inhalte.** Informationen nach tatsächlichem Nutzen ordnen.
- Kompakter Lagerkopf: Name, vorhandener Zeitraum, tatsächlicher Betriebs-/Offline-/Schreibschutzstatus, eindeutiger Kontext.
- Echte verfügbare Arbeitsbereiche als kompakte, verständliche Einstiege anbieten, insbesondere Struktur & Teilnehmer, Verpflegung, Rezepte, Zutaten und Einstellungen, sofern berechtigt und vorhanden.
- Fehlende lagerweite Kennzahlenaggregation und fehlende Aufgabenquelle **nicht durch große Platzhalterkarten dominieren lassen**. Falls ein Hinweis sinnvoll bleibt, einmalig kurz und klar als „noch nicht verfügbar“ formulieren; nicht „keine offenen Aufgaben“ behaupten.
- Paketfunktionen bleiben über die bestehende Lagerübersicht (`Alle Lager`) erreichbar. Den vorhandenen Pfad bei Bedarf kompakt erklären, keine Export-/Importfunktion neu implementieren.
- Keine Prozentwerte, Fortschrittsbalken, simulierten Aufgaben, Lagerphasen, zusätzlichen Module oder frei berechneten Kennzahlen.
- Keine neue Dashboarddatenquelle oder API.

## 4. Shell-Feinschliff

- Linke Sidebar beibehalten (Organisations- und Lagermodus mit jeweils eigener Navigation). **Keine horizontale Hauptnavigation wieder einführen.**
- Den aktiven Organisations- bzw. Lagerkontext eindeutig und sparsam darstellen. Lange Namen sinnvoll umbrechen/abschneiden, ohne Kontext unverständlich zu machen.
- Benutzerkennung und Logout auf normaler Desktopbreite lesbar; definierte Focus-/Hover-/Aktivzustände.
- Keine Änderungen an Kontextwechsel-, Dirty-State-Warn-, Deep-Link-, Authentifizierungs- oder Autorisierungslogik, außer eng begründeter Bugfix nach Rückfrage.

## 5. Gestaltung

Modern Outdoor gemäß Design Guide: warme neutrale Flächen, zurückhaltendes Waldgrün, moderne Sans-Serif-Typografie, dezente Rahmen und klare Abstands-/Schrift-Hierarchie. Keine neuen unabhängigen CSS-Stile oder Hardcoded-Farben, wenn semantische Tokens existieren. Desktop vorrangig; bei 1024px und 390px keine Überläufe oder unbedienbaren Elemente.

## 6. Nicht im Umfang

- Keine Backend-, Datenbank-, Domänen- oder Berechtigungsänderungen.
- Keine neuen Kennzahlen, Aufgabenverwaltung, Phasenlogik, Archivierung oder Fachmodule.
- Kein umfassender Umbau von Rezept-, Zutaten-, Teilnehmer- oder Verpflegungseditoren.
- Keine neue Komponentenbibliothek und keine zusätzlichen Dependencies ohne zwingenden Grund.
- Keine neuen Mockup-Demodaten in der Anwendung.

## 7. Tests und manuelle Abnahme

1. Angular-Tests und Production Build ausführen; bestehende Regressionstests erhalten.
2. Gezielt testen: Start bei 0/1/mehreren Lagern, Verlauf ohne Duplikate, Abwesenheit leerer Zusatzabschnitte und unbeschrifteter interaktiver Elemente.
3. Prüfen: Lager öffnen und verlassen, Sidebar-Moduswechsel, Rechte, Deep-Links, Browser-Zurück, Warnschutz, Offline-/Paketzugang.
4. Prüfen: Lagerübersicht mit vorhandenen Daten, ohne Kennzahlen-/Aufgabenquelle, ohne falsche Fertigstellungsbehauptungen.
5. **Sichtprüfung** soweit Tooling vorhanden bei 1440×900, 1024×768, 390×844; falls kein Browser verfügbar, ausdrücklich `NOT_RUN` dokumentieren und manuelle Screenshots anfordern. Tests nicht als visuelle Freigabe ausgeben.
6. Keyboard-Fokus, semantische Aktionsnamen, ausreichende Kontraste und lange Bezeichnungen prüfen.

## 8. Ergebnis und Abgabe

- Kurz dokumentieren: Ursache des leeren grünen Elements, vorher/nachher pro Problem, geänderte Dateien, Tests und offene Punkte.
- Ergebnis nach `.codex/ux-fix-1-2-1-result.md` schreiben.
- **Keinen Commit erstellen.** Vorhandene uncommittete Änderungen nicht zurücksetzen oder überschreiben.
- Bei fehlender Datenbasis oder erforderlicher fachlicher Änderung stoppen und die Abweichung melden, statt Regeln zu erfinden.
