# UX Overhaul 1 – Shell und Strukturansicht

Stand: 2026-10-16. Frontend umgesetzt; visuelle Abnahme offen. Kein Commit.

## Umsetzung

- Dunkle gemeinsame Sidebar nach Strukturmockups, größere Marke/Icons, beschrifteter Organisations-/Lagerkontext und Benutzerbereich. Desktopbreite 272px bewusst im Visual Design Guide dokumentiert.
- Gemeinsame linke Inhaltskante, 32px Außenabstand, maximal 1200px für Übersichten; volle Arbeitsbreite für Struktur/Teilnehmer.
- Inline-Template nach src/frontend/src/app/app.component.html ausgelagert. Klassenrumpf und damit API-, Navigations-, Berechtigungs-, Dirty- und Speicherhandler unverändert. Noch keine vollständige Zerlegung aller Fachansichten in eigene Komponenten.
- Struktur-/Teilnehmerwechsel in gemeinsamen Seitenkopf verlegt; absolute Positionierung entfernt. Derselbe Kopf bleibt auch in Teilnehmeransicht und Bearbeitung erhalten.
- Detailkopf und Kennzahlen neu komponiert: getrennte sichtbare Werte für KiJu/Leiter, Untergruppenkennzahl, Moduskennzeichnung in eigener Zeile. Baum/Details 38/62, schmal einspaltig. Anlegeaktionen sichtbar beschriftet, Einzelpersistenz explizit erläutert.
- Shell-Regeln ersetzt, alte horizontale Toolbar-Overrides entfernt. Bestehende Fachstyles außerhalb des Scopes erhalten. Materialfarben und Buttonformen an der Quelle vereinheitlicht.
- Regression für lokalen Ansichtswechsel im Seitenkopf bei Bearbeitung und Navigation ergänzt.

## Nachweise

- PASS: Angular 84/84 Tests in 15 Dateien (npm.cmd test -- --watch=false, src/frontend).
- PASS: Production Build; Abschlusslauf gesondert vermerken.
- NOT_RUN: gerenderte Sichtprüfung und Nachher-Bilder bei 1920×1080, 1440px, 1024 und 390 CSS-px.
- NOT_RUN: vollständiger gerenderter Kontrast-/Keyboardtest und Backend-/PostgreSQL-/Installerprüfung.

Alle neun Mockups und acht Ist-Screenshots angesehen. Browser-Skill gelesen und Verbindungsaufbau versucht: No browser is available; nach Troubleshooting Browserliste leer. Keine alternativen Browser installiert oder gesteuert. Vorher-Screenshots belegen ausschließlich den Ausgangszustand. Keine visuelle Fertigstellung behauptet.

Zwischenstände: Build zunächst versehentlich im Repositoryroot aufgerufen. Beim Templateauslagern falschen Schnittindex verwendet, vor erfolgreichem Build korrigiert. Danach 82/83 Tests wegen beim Schreiben beschädigter Umlaute; Texte korrigiert, Wiederholung 84/84 PASS.

## API-/Fachlücken – nur dokumentiert

- Erfasste Teilnehmer nach Kategorie/Stufe fehlen in bestehender Strukturanbindung: ausschließlich vorhandene Schätzwerte anzeigen, keine Zahlen erfinden.
- Strukturoperationen persistieren einzeln: globales Speichern/Abbrechen benötigt separate fachliche Entscheidung und ggf. Transaktionsvertrag. Kein Umbau vorgenommen.
- Lagerweite Aufgaben/Kennzahlen nicht belastbar angebunden: Informationszustand erhalten.
- Blattdetail öffnet bestehende lagerweite Teilnehmerverwaltung mit vorbelegter Neuanlage. Direkte Liste bleibt Folgearbeit; keine neue Datenanbindung.
- Sicheres gemeinsames Speichern geschützter Teilnehmerfelder separat nachzuweisen; Teilnehmereditor unverändert.

## Manuelle Abnahme

1. Frontend neu laden, 100% Zoom. Start und Lagerübersicht gegen before vergleichen: dunkle gemeinsame Sidebar, Kontext gut erkennbar, gemeinsame linke Inhaltskante.
2. Strukturwurzel, Oberknoten und Blatt auswählen. Hierarchie, Zahlenspalten, lange Namen und Details prüfen. Keine Erfasst-Zahlen ohne Datenbasis.
3. Bearbeitung öffnen: Kopf/Ansichtswechsel/Modus dürfen nicht überlappen. Einzelpersistenz und vorhandene Anlege-/Umbenennungs-/Verschiebe-/Löschoperationen prüfen.
4. Schätzdialog: Matrix, Summen, Speichern/Abbrechen; Teilnehmer öffnen und zurück. Rechte/Offline-Schreibschutz respektieren.
5. Bei 1024 und 390 CSS-px Navigation, Escape/Fokusrückgabe, tiefe Bäume und Dialog testen. Kein App-Überlauf, lokales Scrollen erlaubt.
6. Deep Link, Reload, Browser-Zurück und Dirty-Guard prüfen.
7. Nachher-Bilder unter docs/architecture/screenshots/ux-overhaul/after ablegen: Start, Lagerübersicht, Wurzel, Oberknoten, Blatt, Bearbeitung, Schätzdialog.

Kein weiteres UX-Inkrement vor visueller Abnahme.

Abschluss: Production Build nach den letzten CSS-Bereinigungen PASS (2026-10-16); git diff --check PASS. Keine API-/Backenddateien geändert. Keine Nachher-Bilder vorhanden, visuelle Abnahme bleibt offen.
