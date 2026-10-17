# UX Overhaul – Korrekturen und abgeschlossene Sichtprüfungen

Stand: 2026-10-16. Ergänzt die Teilprüfung unter `after-2/`. Bestehende Aufnahmen bleiben erhalten. Keine API-/Backendänderung und kein Commit. Die technische Prüfung ersetzt keine gestalterische Nutzerfreigabe.

## Behobene Punkte

- Falsche Speicherwarnung: Struktur und Teilnehmer liefern nun konkrete Entwurfszustände. Lesefelder und Listenfilter gelten nicht mehr pauschal als Bearbeitung. Der bisher konservative Guard bleibt in anderen Fachansichten erhalten. Geänderte Namen, neue Knotennamen, Verschiebeziele, Schätzmatrizen und Teilnehmerentwürfe werden weiterhin geschützt.
- Rechter Struktureditor: Name, Ebene und Knotenart als kompakte Formulargruppe nebeneinander, bei geringer Breite gestapelt; Bestätigungsaktionen unmittelbar am Namensfeld.
- Teilnehmereditor: Stammdatenfelder auf Desktop nebeneinander; Anwesenheit/Anforderungen als getrennte Abschnitte. Verschachtelten äußeren Editorrahmen entfernt. Fachfelder, Rechte und Speichervertrag bleiben unverändert.

## Automatisierte Nachweise

- PASS: 90/90 Angular-Tests in 15 Dateien. Neue Regression unterscheidet unverändertes/abgebrochenes Namensfeld von tatsächlich geändertem Entwurf.
- PASS: Production Build nach den letzten Layoutänderungen.
- PASS: `git diff --check`.

## Gerenderte Prüfung in Edge

Bestehender Prüftab war nicht mehr steuerbar („Debugger is not attached“). Frischen Tab im selben Edge-Profil geöffnet; vorhandene Anmeldung weiterverwendet. Keine Daten gespeichert, gelöscht oder Berechtigungen geändert. Nur Auswahl, Filter, Sortierung, Öffnen und Abbrechen von Editoren.

| Ablauf | Ergebnis |
|---|---|
| Struktur: Umbenennen öffnen, unverändert abbrechen, Bearbeitung beenden | PASS, keine Speicherwarnung |
| Teilnehmer: nach „TEST 01“ suchen, zurück zu Struktur wechseln | PASS, eine Trefferzeile, keine Speicherwarnung |
| Teilnehmerliste und lesende rechte Vorschau | PASS, Namen/Pfade/Anwesenheit angezeigt |
| Zweigfilter Gruppe B | PASS, 10 Teilnehmerzeilen |
| Namenssortierung absteigend und zurück aufsteigend | PASS, absteigend beginnt mit „TEST Vergleich 3/4 Milchallergie“ |
| Teilnehmereditor öffnen, unverändert abbrechen | PASS, Rückkehr zur Tabelle ohne Speicherwarnung |
| Strukturkopf bei 390px | PASS, früherer großer Leerraum beseitigt; Menübutton hell und lesbar |
| Struktur bei 1024px | PASS, Baum und Details einspaltig |
| Vorschau bei 1024/390px | PASS, überlagerndes Panel, Schließen erreichbar |
| App-Überlauf bei mobiler Vorschau | PASS, gemessene Scroll-/Clientbreite jeweils 375px bei 390px eingestelltem Viewport (Scrollleiste) |

Zunächst waren neue Teilnehmerstyles noch nicht im offenen Tab angekommen. Nach explizitem Neuladen wurden das kompaktere Layout geprüft und die Editorbilder neu erstellt. Die Bilder dokumentieren den gerenderten Stand, nicht nur CSS-Annahmen.

## Screenshots

Dateinamen benennen den eingestellten CSS-Viewport. JPEG-Aufnahmen können die Scrollleiste aussparen; keine nachträgliche Skalierung. Verschiedene vertikale Scrollpositionen dokumentieren die jeweils geprüften Inhalte.

- [Struktureditor Desktop](structure-editor-1920x1080.jpg)
- [Struktur 1024px](structure-1024x768.jpg)
- [Struktur 390px](structure-390x844.jpg)
- [Teilnehmerliste Desktop](participants-1920x1080.jpg)
- [Teilnehmerliste mobil](participants-390x844.jpg)
- [Vorschau Desktop](participant-preview-1920x1080.jpg)
- [Vorschau 1024px](participant-preview-1024x768.jpg)
- [Vorschau mobil](participant-preview-390x844.jpg)
- [Teilnehmereditor Desktop](participant-editor-1920x1080.jpg)
- [Teilnehmereditor mobil](participant-editor-390x844.jpg)

## Verbleibende Grenzen

- Stufe und KiJu-/Erwachsenenkategorie fehlen weiterhin im Teilnehmervertrag. Keine entsprechenden Spalten, Filter oder Kennzahlen erfunden.
- Keine eigenständige Teilnehmerdetailroute ergänzt; vorhandener Editor bleibt der vollständige Fachablauf.
- Mobile Tabellen und tiefe Bäume dürfen lokal horizontal scrollen. Der Baum kann daher auf kleinen Geräten rechts liegende Zahlen erst nach Scrollen zeigen.
- Keine vollständige Prüfung aller Rechte-/Offlinekombinationen oder vollständiger WCAG-/Screenreader-Audit. Tatsächliches Speichern/Löschen mit den vorhandenen Testdaten wurde in dieser UI-Prüfung nicht ausgelöst.
- Gestaltung ist näher an den Referenzen, aber nicht pixelgleich. Die aus fehlenden Fachfeldern und der bestehenden Einzelpersistenz folgenden Unterschiede bleiben bewusst bestehen.
