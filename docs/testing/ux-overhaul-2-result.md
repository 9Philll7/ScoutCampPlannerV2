# UX Overhaul 2 – Bearbeitung und Listen

**Nachtrag:** Die ursprünglich offene Sichtprüfung wurde nach weiteren Korrekturen fortgesetzt. Aktueller Nachweis mit 90 bestandenen Tests, erfolgreichem Build, korrigiertem Dirty-Guard und Desktop-/Mobilbildern: [after-3/README.md](../architecture/screenshots/ux-overhaul/after-3/README.md). Die folgenden Angaben dokumentieren den vorherigen Implementierungsstand.

Stand: 2026-10-16. Implementierung und automatisierte Prüfung; gerenderte Abnahme ausstehend. Keine API-/Backendänderungen, kein Commit.

## Umsetzung

- Struktureditor: Kontextmenü am jeweiligen Baumknoten für vorhandene Anlegen-/Umbenennen-/Verschieben-/Löschoperationen. Nur bei bestehendem Schreibrecht und ohne Offline-Schreibschutz. Knotenwahl läuft weiterhin durch den bisherigen Dirty-Guard; bei abgelehntem Wechsel darf eine Menüaktion nicht den anderen Knoten bearbeiten.
- Untere allgemeine Iconleiste entfernt. Rechts beschriftetes Namensfeld, expliziter Einstieg zum Ändern, Namen speichern/Abbrechen direkt am Feld. Vorhandene Anlegen-/Verschiebeeditoren weiterverwendet. Bestehende Einzelpersistenz unverändert; kein globales Sammelspeichern suggeriert.
- Untergruppenliste verdichtet, Spalten für Name und Aktion begrenzt, geringere Zeilenhöhe. Keine zusätzliche Differenz-/Erfasstspalte ohne Datenbasis.
- Teilnehmer-Kartenraster durch semantische Tabelle ersetzt: Name mit Initialen, vollständiger Strukturpfad, Vorschauaktion. Suche ausschließlich über Namen, Sortierung nach Namen, Filter über Strukturzweige inklusive Nachkommen und nicht zugeordnete Teilnehmer, passende Leerzustände. Keine Änderung der ursprünglichen Reihenfolge im geladenen Datenbestand.
- Rechte Schnellvorschau zeigt vorhandene Strukturzuordnung und Anwesenheit (standardmäßig anwesend, explizite Tages-/Mahlzeitenausnahmen). Keine Gesundheits-/Ernährungsdaten in neue Suche, Filter oder Vorschau aufgenommen. Bei schmaler Breite liegt die Vorschau als nichtmodales Panel über der Tabelle.
- Bearbeiten öffnet den vorhandenen fachlichen Teilnehmereditor als zusammenhängenden Bereich anstelle der Liste. Felder und vollständiger bestehender Daten-/StateToken-Vertrag bleiben bestehen. Filter/Sortierung bleiben beim Schließen erhalten; bei Lagerwechsel zurückgesetzt. Neue lokale Verwerfungswarnung schützt Entwürfe beim Schließen, Wechsel der Vorschau und manuellem Neuladen. Konflikte behalten den Entwurf. Online eingefrorene Lager sind auch im Save-/Remove-Handler geschützt.
- Sidebar und mobile Utility-Leiste setzen die Material-Textbuttonfarbe explizit auf hell. Mobiler Seitenkopf verwendet keine Desktop-Flexbasis von 420px mehr. Baum/Details wechseln bereits unter 1100px auf eine Spalte.

## Dateien

- `src/frontend/src/app/app.component.html` und `.ts`: Material-Menü und Editoraktionen.
- `src/frontend/src/app/features/camp/structure-view.scss`: Listen-/Editorlayout und responsive Korrekturen.
- `src/frontend/src/visual-foundation.scss`: Material-Textbuttonfarbe für dunkle Navigation.
- `src/frontend/src/app/features/camp/participants.component.ts`, `.html`, `.scss`: getrennte Logik, Template und Styles; Tabelle/Vorschau/Editor.
- Zugehörige Komponenten-/Navigationstests.

## Grenzen und dokumentierte Fachlücken

1. Der bestehende Teilnehmervertrag enthält Anzeigename, Strukturzuordnung, Ernährung und Abwesenheitsausnahmen, aber keine Stufe und KiJu-/Erwachsenenkategorie. Diese Mockupspalten, Filter und Teilzählungen werden nicht erfunden. Vorhandene Vertragslücke separat zu entscheiden; keine API-Erweiterung.
2. Die neue Vorschau ist eine lesende Teilansicht des bereits autorisierten Teilnehmerdokuments. Backendautorisierung und bisheriger geschützter Editor bleiben maßgeblich. Kein neues Modell feldweiser Rechte eingeführt und keine unbekannten Felder mit erfundenen Werten überschrieben.
3. Es gibt in diesem Schritt keine neue Teilnehmerdetailroute und keine neue Speicherschnittstelle. „Bearbeiten/Details anzeigen“ öffnet denselben bestehenden Editor innerhalb der Seite; Browser-History und Shell-Dirty-Guard bleiben bestehen. Eine eigenständige lesende Detailseite ist Folgearbeit.
4. Bearbeitungsmodus der Struktur verändert weiterhin jede bestätigte Operation einzeln. Abbrechen am Namensfeld verwirft nur diesen Entwurf, nicht zuvor gespeicherte Operationen.
5. Bestehende Warnung zur ausschließlichen Verwendung von Dummy-Daten bleibt erhalten; dies ist keine Freigabe für echte Gesundheitsdaten.

## Prüfung

- Production Build: PASS.
- Angular-Abschlusslauf nach letzten Änderungen: PASS, 89/89 Tests in 15 Dateien. `git diff --check`: PASS.
- Neue Nachweise: Filter/Sortierung und unveränderte Quelldaten, vollständige Strukturpfade/Zweigfilter, lesende Vorschau ohne Mutation, Read-only-Anzeige, Entwurfsschutz, eingefrorener Schreibschutz, Knotenmenü bis Namenseditor ohne Persistenz.
- Zwischenfehler: einmal Worker-Starttimeout ohne ausgeführte Tests; einmal Sandbox-Prozessstart `spawn EPERM`, mit angeforderter Eskalation wiederholt. Beim neuen Menütest war zunächst eine zu frühe ngModel-Erwartung fehlerhaft; Stabilisierung des Fixtures und präziser Feldselektor korrigiert.
- Browser: bestehende Edge-Verbindung und lokaler Tab erreichbar. Die Testsitzung ist jedoch abgelaufen; nach Reload zeigt localhost die Anmeldeseite. Nutzer über die asynchrone Rückfrage zur erneuten Anmeldung aufgefordert. Keine Zugangsdaten eingegeben, keine neuen Nachher-Screenshots oder visuelle Freigabe behauptet.

## Gerenderte Abnahme nach Anmeldung

Bei 1920×1080 Oberknotenliste, Blatt im Bearbeitungsmodus, geöffnetes Knotenmenü, Namensfeld, Teilnehmerliste und rechte Vorschau mit den Referenzen unter `docs/architecture/mockups/ux-2/` vergleichen. Neue Bilder getrennt unter `docs/architecture/screenshots/ux-overhaul/after-2/` ablegen, bisherige Aufnahmen erhalten.

Zusätzlich 1024px und 390px prüfen: helle inaktive Navigation und Menübutton, kein großer Leerraum im Kopf, vollständig nutzbarer Baum, lokal scrollbar bleibende Tabelle, überlagernde Vorschau und zugänglicher Schließen-Button. Escape/Fokus am Material-Menü, Filter-/Auswahlzustand bei Rückkehr aus dem Editor, Abbruch ungespeicherter Änderungen und Rechte/Offline-Zustände prüfen. Noch keine gerenderte WCAG-/Responsivitätsabnahme.
