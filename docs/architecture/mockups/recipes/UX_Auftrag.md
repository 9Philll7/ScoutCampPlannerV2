# Codex-Auftrag: UX-Überarbeitung des Rezepteditors

## Ziel

Überarbeite den bestehenden Rezepteditor von ScoutCampPlanner anhand der bereitgestellten Mockups.

Die Mockups sind die visuelle Zielreferenz für Layout, Informationshierarchie, Abstände, Farben und Interaktionen.

**Die bestehende Fachlogik, Datenstruktur und Architektur bleiben maßgeblich.** Die Überarbeitung soll primär die Benutzeroberfläche verbessern.

## 1. Vor der Implementierung

Analysiere zunächst:

- den bestehenden Rezepteditor und seine Komponenten
- die vorhandenen Domain-Modelle, API-Endpunkte und Services
- den aktuellen Workflow für Entwürfe, Revisionen und Veröffentlichungen
- die bereits implementierten Mengenregeln und Ernährungskonflikte
- bestehende Tests und UX-Konventionen

Verwende vorhandene Komponenten und Design-Tokens, soweit sinnvoll.

Erfinde keine Fachfunktionen, Datenfelder oder API-Endpunkte allein aufgrund der Mockups.

## 2. Zielstruktur

Der Rezepteditor erhält vier Tabs:

### Grunddaten
- Rezeptname
- Beschreibung und Zubereitung
- optionale Quelle
- Standardportionen
- vorhandene Stufenfaktoren

### Zutaten
- Zutaten suchen und hinzufügen
- Mengen und Einheiten bearbeiten
- frei benennbare Zutatengruppen verwalten
- Zutaten und Gruppen umordnen
- Mengenregeln pro Zutat konfigurieren
- Herkunftsbereich und Zutatenrevision anzeigen
- Ernährungskonflikte erkennen
- Zutatenvarianten und Ersatzzutaten verwalten
- verfügbare Zutatenaktualisierungen gezielt übernehmen

**Wichtig:** Im Zutaten-Tab keine Nährwertauswertung anzeigen.

Mengenregeln und Ersatzzutaten werden über kompakte, kontextbezogene Dialoge bearbeitet.

### Nährwerte
- schreibgeschützte Nährwertauswertung
- Werte pro Standardportion und für das gesamte Rezept
- Berechnungsgrundlage aus gespeicherten Zutatenrevisionen
- fehlende Nährwertinformationen und deren Ursachen
- erkennbare Kennzeichnung unvollständiger Schätzungen

Nährwerte werden nicht unabhängig vom Rezept manuell überschrieben.

### Prüfung & Veröffentlichung
- Veröffentlichungsprüfung ausführen
- Fehler, Warnungen und erfolgreiche Prüfungen getrennt anzeigen
- direkte Navigation zum betroffenen Eintrag
- notwendige Warnungsbestätigungen
- Veröffentlichung eines gültigen Entwurfs
- vorhandene Revisionsinformationen anzeigen

## 3. Bearbeitungsmodell

Orientiere dich am bereits umgesetzten Teilnehmer- und Zutateneditor:

- zunächst Ansichtsmodus
- explizites „Bearbeiten“
- gemeinsame Speicherung der Änderungen
- Abbrechen verwirft ungespeicherte Änderungen
- Tabwechsel erhält den Bearbeitungszustand
- bei ungespeicherten Änderungen vor dem Verlassen warnen
- veröffentlichte Revisionen bleiben unveränderlich
- Änderungen erfolgen über einen neuen Entwurf

Speichern und Veröffentlichen bleiben eigenständige Aktionen.

## 4. Fachliche Grenzen

Die bestehende Rezeptlogik darf nicht vereinfacht oder durch eine neue ersetzt werden.

Insbesondere:

- Mengenregeln müssen unverändert korrekt berechnet werden.
- Zutaten referenzieren die jeweils ausgewählte veröffentlichte Revision.
- Neue Zutatenrevisionen werden niemals automatisch übernommen.
- Ersatzzutaten bleiben ihren jeweiligen Ernährungskonflikten zugeordnet.
- Vermeidbare und nicht vermeidbare Ernährungskonflikte bleiben unterscheidbar.
- Das Speichern eines Entwurfs darf nicht mit dessen Veröffentlichung gleichgesetzt werden.

Rezeptbilder, Tags, interne Notizen und Unterrezepte dürfen nicht allein aufgrund ihrer Darstellung in einem Mockup neu implementiert werden. Wenn entsprechende Funktionen noch fehlen, dokumentiere dies als spätere Erweiterung.

## 5. Visuelle Umsetzung

Orientiere dich an den bereitgestellten Mockups und dem bestehenden Modern-Outdoor-Design.

Prioritäten:

1. Klare Informationshierarchie
2. Kompakte, gut lesbare Zutatenlisten
3. Möglichst viel Platz für die eigentliche Bearbeitung
4. Einheitliche Abstände, Buttons und Statusanzeigen
5. Konsistente Dialoge
6. Eindeutige Fehler- und Warnungszustände
7. Responsives Verhalten ohne Verlust wichtiger Aktionen

Beispielrezepte, Rezeptbilder, Mengen und Statuszahlen aus den Mockups sind keine verbindlichen Test- oder Stammdaten.

## 6. Umsetzung in Inkrementen

Arbeite in dieser Reihenfolge:

1. Gemeinsames Editorlayout, Kopfbereich und Grunddaten
2. Zutatentab einschließlich Gruppen und Mengenregeln
3. Ernährungskonflikte, Ersatzzutaten und Zutatenaktualisierungen
4. Nährwerttab
5. Prüfung und Veröffentlichung
6. Visuelle Konsistenz, Responsive-Verhalten und Regressionstests

**Nach jedem Inkrement:** Implementierung testen, Ergebnisse dokumentieren und auf Freigabe warten, bevor das nächste beginnt.

Beginne ausschließlich mit der Bestandsanalyse und Inkrement 1.

## 7. Abnahmekriterien

- Alle vier Tabs sind erreichbar.
- Das Layout orientiert sich an den Mockups.
- Der Bearbeitungszustand bleibt beim Tabwechsel erhalten.
- Zutaten und Gruppen lassen sich gemäß vorhandener Fachlogik bearbeiten.
- Mengenregeln und Ersatzkonfigurationen funktionieren weiterhin.
- Zutatenrevisionen werden nur explizit aktualisiert.
- Nährwerte erscheinen ausschließlich im Nährwerttab.
- Veröffentlichung prüft weiterhin alle bestehenden Voraussetzungen.
- Bestehende Tests bleiben erfolgreich.
- Neue oder angepasste UI-Funktionen erhalten geeignete Tests.

## 8. Ergebnisdokumentation

Dokumentiere für jedes Inkrement:

- geänderte Dateien
- umgesetzte UX-Funktionen
- bewusst nicht umgesetzte Mockup-Elemente
- Testergebnisse
- offene Punkte
- bekannte Abweichungen von den Mockups

Ändere keine fachliche Logik ohne ausdrückliche Freigabe.