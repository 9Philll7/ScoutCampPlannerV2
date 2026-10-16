# UX Fix 2.1.1 – Structure Visual Alignment

Stand: 2026-10-16. Technische Überarbeitung implementiert; visuelle Endabnahme ausstehend. Die vorherige Darstellung wurde vom Nutzer nicht freigegeben. Kein Commit.

## Aktueller Stand – zusammenhängende Seitenkomposition

### Desktop-Referenz 1920 × 1080

Primäre Sichtprüfung nach Nutzerentscheidung bei 1920 × 1080 und 100 % Browser-Zoom. Ab 1400 CSS-px sitzt der lokale Ansichtswechsel neben dem Kopf statt in einer zusätzlichen Zeile. Flexible 38/62-Panels, 24px Innenabstand, kompakte Baumzeilen und durchgehende Abschnittstrennung. Darunter bleibt der Ansichtswechsel im Dokumentfluss. Kein CSS-Zoom und keine feste Seitenbreite.

Schätzwerte nun als zusammengehöriges Zahlenpaar mit sichtbarer Kategorienbeschriftung; semantische Einzelwerte bleiben für assistive Technik erhalten. Angular 83/83 PASS, Production Build PASS, git diff --check PASS. Gerenderte Prüfung bei 1920 × 1080 sowie Zoomvarianten: NOT_RUN; visuelle Übereinstimmung weiterhin nicht bestätigt. Kein Commit.

### Visueller Stil nach erneutem PNG-Abgleich (2026-10-16)

- Baum als ausgerichtete Datenansicht: eigene Spaltenüberschrift, feste Zahlenspalte, größere rollenbezogene Icons (Blattgruppe/Hierarchie), dezente Zeilentrennung und durchgehende Auswahlfläche. Einrückung beansprucht nur den Namensbereich; Zahlen bleiben rechts ausgerichtet.
- Detailkennzahlen mit seitlichem Icon, stärkerer Zahlenhierarchie und neutraler Oberfläche statt großer grünlicher Flächen. Titelgewicht, Tabellenköpfe, Zeilen und Aktionen an die Referenzen angeglichen.
- Bearbeitungsansicht zeigt statt Dashboard-Kennzahlen die vorhandenen allgemeinen Angaben und einen abgegrenzten Aktionsbereich. Wechsel zurück stellt die Kennzahlen wieder her; Regression dafür ergänzt. Keine Speicher-, Berechtigungs- oder Berechnungsänderung.
- Angular: **83/83 PASS** in 15 Dateien. Erster Versuch: Worker-Starttimeout, keine Tests ausgeführt; vollständiger Wiederholungslauf erfolgreich. Production Build und `git diff --check`: **PASS**.
- Visueller Browservergleich: **NOT_RUN**. Keine pixelgenaue Übereinstimmung oder manuelle Freigabe behauptet. Oberknoten, Blatt und Bearbeitung müssen noch anhand der gerenderten Ansicht abgenommen werden. Kein Commit.

- Allgemeiner Lagerkopf und zusätzlicher Strukturkopf durch einen einzigen Seitenkopf „Struktur & Teilnehmer“ mit Kontextpfad und Iconfläche ersetzt. Lokaler Betrieb und Offline-Schreibschutz bleiben sichtbar.
- Strukturansicht nutzt die verfügbare Arbeitsbreite statt der Übersichts-Maximalbreite. Baum/Detail bleiben 38/62 geteilt; schmale Ansichten einspaltig. Globale Sidebar unverändert.
- „Struktur bearbeiten“ direkt im Baumkopf; vorhandene Berechtigungen, Dirty-Guard und Einzelspeicherung unverändert. Lokale Struktur-/Teilnehmernavigation mit sichtbarer aktiver Position und aria-current.
- Panelabstände, Baum-Einrückung, Kennzahlflächen, Tabellenzeilen und Buttons verdichtet; doppelter Lagername im Wurzelpfad entfernt. Keine erfundenen Erfasst-Zahlen, Diagramme oder zusätzlichen Funktionen.
- Strukturstyles nach `src/frontend/src/app/features/camp/structure-view.scss` ausgelagert und über die bestehende Styles-Pipeline eingebunden. Bestehende uncommittete Änderungen erhalten.
- Abschließende Angular-Suite: **PASS, 83/83 Tests in 15 Dateien**. Enthält neue Regression für einzelnen Seitentitel, Bearbeitung am Baum, aktive lokale Navigation und Rückkehr zum bisherigen Lagerkopf beim Bereichswechsel.
- Angular Production Build: **PASS**. `git diff --check`: **PASS**. Git-Status: nur Frontend-/Test-/Ergebnisdateien des bestehenden UX-Arbeitsstands; keine generierten Build-Artefakte.
- Gerenderter Browservergleich dieser Überarbeitung: **NOT_RUN**. DOM-Tests und CSS-/Mockup-Abgleich sind keine visuelle Abnahme. Noch erforderlich: Oberknoten, Blatt, Bearbeitungsmodus und Schätzdialog bei Desktopbreite sowie schmale Ansicht kontrollieren.
- Commit-Bereitschaft: technisch geprüft, visuell noch nicht freigegeben. Kein Commit erstellt.

Die folgenden Abschnitte dokumentieren die vorausgegangenen Iterationen und ihre jeweiligen Prüfergebnisse.

## Referenzen und Bestandsaufnahme

Vorhanden und berücksichtigt: UX_Increment_2_Structure_Participants_Spec.md, Visual_Design_Guide.md, UX_Increment_2_1_Structure.md, ux-increment-2-1-result.md sowie structure_parent.png, structure_leaf.png, structure_editor_read.png und structure_editor_edit.png unter docs/architecture/mockups/ux-2/.

Ein separates Schätzdialog-Mockup ist dort nicht vorhanden. Dessen Layout wurde ausschließlich nach bestehendem Guide und vorhandener Tabellen-/Formularsprache gegliedert, nicht als bildgetreue Umsetzung einer fehlenden Referenz bezeichnet.

Konkrete Ausgangsabweichungen im Template/Stylesheet gegenüber den gelesenen Bildern:

1. Seitenüberschrift ohne erläuternde Hierarchiestufe; zusätzlicher äußerer Kartenrahmen.
2. 38/62-Proportion bereits vorhanden, aber ungleich ausgerichtete Panels und wenig Flächennutzung.
3. Baum ohne Verbindungslinien, gedeckelte Einrückung, fehlender Indikatorplatz bei Blattknoten.
4. Auswahl vorhanden, Knotentext und lange Schätzbeschriftung jedoch ineinander verschachtelt.
5. Details weiterhin als kleine alte Baumkarte mit schwachem Titel und redundanten Aufklappaktionen.
6. Schätzwerte als Fließtext, Unterknoten als lose Buttons und Absätze.
7. Schätzdialog mit verstreuten Kopfaktionen, separatem Gesamtsummenabsatz und unruhiger Ausrichtung.

## Durchgeführte Änderungen

- Äußeren Struktur-Kartenrahmen entfernt, Seitenkopf ergänzt; bestehende Lager-Sidebar unverändert.
- Aufeinander abgestimmte 38/62-Panels, gemeinsame Mindesthöhe, Untereinanderlayout bei schmaler Breite. Keine neue Navigation.
- Einheitliche Baumzeilen: reservierter Aufklappplatz auch bei Blättern, vorhandene SVG-Icons, 20px Einrückung pro Ebene ohne Deckelung, Verbindungslinien, lesbare Namen und Ebenenlabels. Tiefe Bäume können lokal horizontal scrollen; Einrückung wird nicht verfälscht.
- Schätzwerte als tabellarisch ausgerichtete Zahlenpaare mit expliziter gemeinsamer Kategorienbeschriftung. Keine erfassten Zahlen erfunden.
- Größerer Detailtitel, Pfad darunter, neutraler Blatt-/Oberknotenhinweis. Zwei getrennte Schätzkennzahlen, Aktionen unmittelbar darunter. Doppelte Aufklappaktionen im Detail nicht mehr dargestellt; dieselbe Funktion bleibt links verfügbar.
- Unterknoten als ruhige Zeilen mit Name, vorhandener Schätzung und Öffnen-Aktion; keine Differenzspalte.
- Bearbeitungsmodus mit hervorgehobenem Detailrahmen, bestehenden Einzelaktionen und unverändertem Hinweis zur Einzelpersistenz. Kein Sammelspeichern oder globales Zurücknehmen.
- Schätzdialog: Kopf, beschriftete Matrix, rechts ausgerichtete Zahlenfelder, Gesamtsumme im Tabellenfuß und gemeinsame Aktionsleiste. Löschen/Abbrechen/Speichern nutzen unveränderte Handler, Guards und Berechtigungsbedingungen.
- Nur betroffene Templates/Styles und Regressionstests geändert; keine neuen Dependencies, APIs, Attribute, Berechtigungen oder Berechnungen. Bestehende uncommittete UX-2.1-Änderungen erhalten.

## Vergleich je Referenz / bewusste Abweichungen

| Ansicht | Angleichung | Bewusste Grenze |
|---|---|---|
| Oberknoten | Baum links, großer Detailkopf, Kennzahlen und Unterknoten rechts | Keine erfassten Kategorien, Charts oder Differenzwerte ohne Datenbasis |
| Blattknoten | Konsistente Auswahl, Pfad, Kennzahlen, direkt zugeordnete Aktionen | Vorhandener Teilnehmer-Einstieg statt neuer Tabelle/Teilnehmerattribute |
| Editor lesend | Ruhige Detailfläche, keine zusätzlichen Editierfelder | Keine erfundenen Typ-/Beschreibungsfelder |
| Editor bearbeitend | Klarer Modus, Aktionen am Detail, markierter Rahmen | Einzelbestätigung statt transaktionalem Gesamtspeichern |
| Schätzdialog | Ausgerichtete Matrix, Summenzeile und Aktionsleiste | Kein separates Dialogbild vorhanden |

Bestehende helle Modern-Outdoor-Shell bleibt erhalten, statt die dunkle dekorative Mockup-Sidebar nachzubauen. Keine illustrative Suche, Stufen-/Teilnehmerzahlen oder zusätzlichen Funktionen eingeführt. Die bekannte fachliche Differenz zwischen vorhandenen Teilnehmerdaten und Mockup bleibt unverändert dokumentiert.

## Qualitätskontrolle

- Production Build: PASS.
- git diff --check: PASS. Keine neuen generierten Artefakte im Git-Status.
- Angular: PASS – vollständiger Wiederholungslauf, 81 Tests in 15 Dateien, davon 23 Navigationstests. Erster Lauf: Timeout starting forks runner, keine Tests gestartet; kein funktionaler Testnachweis daraus abgeleitet.
- Zusätzliche DOM-Regressionen prüfen ausgerichtete Blattplätze, ausgewählte Zeile, korrekten Pfad, ausschließliche Schätzkennzahlen und Dialog-Summen-/Aktionsstruktur. Bestehende Speicher-/Rechte-/Knoten-/Paketregressionen bleiben erhalten.
- Tatsächliche Browser-/Screenshotprüfung: NOT_RUN. In dieser Sitzung keine nutzbare Browserverbindung; keine zusätzliche Browser-App installiert (Nutzerpräferenz). Keine verifizierte visuelle Übereinstimmung behauptet. Template-/CSS-Abgleich und Tests ersetzen keine Sichtprüfung.

## Manuelle Abnahme / offene Punkte

Benötigt werden Vergleichsscreenshots bei etwa 1440px: ausgewählter Oberknoten, ausgewähltes Blatt, aktiver Bearbeitungsmodus und geöffneter Schätzdialog. Zusätzlich 390px mit langem Namen/tiefem Baum prüfen: lokale Scrollbarkeit, kein Seitenüberlauf, Aktionszugang, Dialogfokus/Escape. Knoten auswählen/aufklappen und Schätzwerte speichern/abbrechen stichprobenartig bestätigen.

Technischer Stand bereit zur Sichtprüfung. Visueller Abschluss erst nach diesen Vergleichsbildern; kein Commit erstellt.

## Nachkorrektur anhand des Nutzer-Screenshots

Der gelieferte Screenshot zeigt die Lagerwurzel: zu großer Titel, hohe Einzelkennzahlkarten, ein fast leerer Info-Aktionsbereich zwischen Trennlinien und einen dominanten technischen Hinweis. Diesen konkreten Befund nachgebessert: Titel auf Abschnittsgröße reduziert, beide Schätzkennzahlen in einer kompakten Fläche gruppiert, unnötige Aktionstrennlinie/Abstände entfernt. Die vorhandene Infoaktion heißt nun sichtbar „Nach Stufe aufschlüsseln“ (mit aria-expanded). Unterknoten sind als ausgerichtete Spalten Name/Schätzung/Aktion dargestellt, mobil als kompakte Zeilen. Der Datenhinweis bleibt vollständig zugänglich, aber standardmäßig zugeklappt.

Keine Änderungen an Daten, Summen oder Speicherhandlern. Nachkorrektur: 81/81 Angular-Tests PASS, Production Build PASS, git diff --check PASS. Der alte Nutzer-Screenshot ist ein Befundnachweis, keine Freigabe des neuen Ergebnisses; erneuter Screenshot und Browser-Sichtprüfung bleiben offen. Kein Commit.

## Neuaufbau des Detailbereichs nach Nutzerfreigabe

Die vorherige Verdichtung wurde vom Nutzer ausdrücklich als unzureichend beurteilt. Daraufhin den Detailaufbau ersetzt statt nochmals nur Größen zu ändern:

- Neue reine Präsentationskomponente `features/camp/structure-detail-header.component.ts` für Iconfläche, Titel, vollständigen Pfad, Beschreibung und Editmodus-Kennzeichnung. Alte Detail-Baumkopfstruktur samt redundanter Ausklappcontrols entfernt; Ausklappen bleibt links vollständig erhalten.
- Kennzahlen als Informationsbereich mit eigenem Titel „Geschätzte Teilnehmer“, Kategorien und optionaler Anzahl direkter Untergruppen. Fehlende Schätzwerte bleiben „Noch nicht verfügbar“. Keine neuen fachlichen Berechnungs-/Auswahlregeln.
- Oberknoten: echte semantische Tabelle mit Spaltenüberschriften, Zeilenüberschriften, Schätzwerten und Öffnen-Aktionen. Keine Diff-/Erfasst-Spalten ohne Datenbasis.
- Blattknoten: separater Abschnitt mit Teilnehmer-Einstieg und Schätzaktion, nur unter bisherigen Berechtigungs-/Blattbedingungen.
- Editmodus: Abschnitt „Allgemeine Angaben“ mit vorhandener Ebene/Knotenart und Umbenennungsfeld bei aktivierter Aktion; bestehende Anlegen-/Umbenennen-/Verschieben-/Löschhandler unverändert.
- Build PASS. Erster Testlauf 80/81: eine Erwartung bezog sich auf die alte Beschriftung „Geschätzte KiJu“; an die neue gemeinsame Kennzahlenüberschrift angepasst. Zusätzlicher Test für Header, Oberknotentabelle, Blattbereich und Editabschnitt ergänzt.
- Visuelle Endabnahme weiterhin offen: Bitte Oberknoten, Blatt und Bearbeitungsmodus nach Neuladen vergleichen. Keine Browser-/Screenshotfreigabe behauptet, kein Commit.
- Abschließender Wiederholungslauf: 82/82 Tests PASS in 15 Dateien. Production Build und git diff --check PASS. Keine generierten Artefakte im Git-Status.
