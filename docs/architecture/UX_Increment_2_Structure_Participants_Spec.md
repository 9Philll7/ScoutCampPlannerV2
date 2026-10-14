# ScoutCampPlanner – UX Increment 2: Struktur & Teilnehmer

**Status:** Konsolidierte UX-Spezifikation / Grundlage für die Inkremente 2.1 und 2.2  
**Design:** `docs/architecture/Visual_Design_Guide.md` (bestehenden Guide nicht ersetzen)  
**Mockups:** `docs/architecture/mockups/ux-2/`  
**Voraussetzung:** UX 1–1.2.1 funktional als Ausgangsbasis; visuelle Feinabstimmung weiter möglich.

## 1. Ziele und verbindliche Grenzen

Der Lager-Navigationspunkt heißt **„Struktur & Teilnehmer“**. Im Lagermodus gibt es nur die **lokale linke Sidebar**, keine parallele horizontale Hauptnavigation. Die Seite besitzt zwei fachliche Ansichten: **Struktur** und **Teilnehmer**. Innerhalb des Struktur-Tabs bleibt auf Desktop die zweigeteilte Arbeitsfläche (Strukturbaum links, ausgewählter Knoten rechts) sichtbar. Tabs für lokale Unteransichten sind zulässig, aber keine zweite Hauptnavigation.

Dies ist primär ein UX-Redesign. **Bestehende Fachlogik, Datenmodelle, API-Verträge, Validierung, Zugriffsrechte, Kalkulationsregeln und Offline-Verhalten bleiben unverändert.** Wo eine beschlossene UX-Funktion noch keine tragfähige Implementierung hat, die Lücke dokumentieren statt stillschweigend Backend- oder Berechtigungslogik hinzuzufügen.

## 2. Strukturmodell – keine Verwechslung mit Stufen

- **Strukturknoten:** frei benanntes Hierarchieelement; bei definierter Struktur gelten deren Ebenenregeln. Knotennamen wie „GuSp“ dürfen nicht als Stufenkennung interpretiert werden.
- **Stufe:** eigenständige Eigenschaft eines Teilnehmers bzw. einer Schätzposition. In einem Blattknoten können **mehrere Stufen** vorkommen.
- **Kategorie:** **KiJu** oder **Leiter/Erwachsene**, unabhängig von Stufe und Strukturknoten.
- Konkrete Teilnehmer **nur an Blattknoten**. Ebenso nur dort direkte **Schätzwerte**.
- Schätzwerte pro Blattknoten in Matrix **Stufe × Kategorie** erfassen; Gesamtsummen aus den Zellen ableiten. Die verfügbaren Stufen kommen aus den bestehenden Definitionen, nicht aus fixen UI-Listen.
- Übergeordnete Knoten zeigen ausschließlich **aggregierte**, nicht direkt editierbare Teilnehmer- und Schätzzahlen. Auch ungleich tiefe freie Zweige korrekt behandeln, soweit das bestehende Modell sie unterstützt.
- Schätzung und konkrete Erfassung getrennt halten; niemals ungeprüft addieren oder in der Verpflegung eine neue automatische Auswahlregel erfinden.
- Optional „Nach Stufe aufschlüsseln“, standardmäßig zugeklappt; keine verpflichtenden Diagramme.

## 3. Strukturansicht (UX 2.1)

**Desktop:** Strukturbaum etwa 35–40 % des Inhalts, Details etwa 60–65 %. Baum zeigt klare Hierarchie, Aus-/Einklappen, Auswahlmarkierung, lesbare Aggregation nach KiJu/Erw. für geschätzt und erfasst; bei Bedarf Suche. Im rechten Panel Name, vollständiger Pfad und auf Knotentyp abgestimmte Informationen.

**Übergeordneter Knoten:** Untergruppenübersicht und aggregierte Zahlen; **kein** „Teilnehmer hinzufügen“, **kein** „Schätzung bearbeiten“. Untergruppenliste ohne Differenzspalte. Stufenverteilung erst nach explizitem Aufklappen.

**Blattknoten:** Kompakte geschätzte/erfasste Zahlen nach KiJu/Erw.; direkte Teilnehmerliste und „Teilnehmer hinzufügen“ mit bereits vorgegebener Blattzuordnung; Schätzwerte ausschließlich über eigenen **Dialog** bearbeiten (Matrix je Stufe und Kategorie, Summen errechnet, Speichern/Abbrechen). Keine Teilnehmerverwaltung im eigentlichen Struktureditor.

**Struktureditor:** Normalmodus lesend. Berechtigte Benutzer können „Struktur bearbeiten“ aktivieren; erst dann erscheinen zulässige Änderungsaktionen. Berechtigungen werden nicht über die sichtbare Schaltfläche allein durchgesetzt. Im Bearbeitungsmodus klare Kennzeichnung, explizit Speichern und Abbrechen *soweit die bestehenden Persistenzverträge das ermöglichen*. Falls Änderungen heute sofort persistieren, vor Umbau auf Sammelspeichern eine separate fachliche Entscheidung einholen. Der Editor beachtet freie bzw. definierte Struktur und erklärt unzulässige Änderungen (insbesondere Teilnehmer an Blattknoten) konkret. Keine pauschalen Lösch-/Verschieberegeln aus Mockups ableiten.

## 4. Teilnehmeransicht (UX 2.2)

- **Lagerweite Tabelle** mit Suche, Sortierung und Filtern nach Strukturzweig, Stufe und Kategorie. Ernährungs- und Gesundheitsfilter nur bei wirksamer Berechtigung.
- Spalten mindestens: Name, Stufe, Kategorie, Gruppe/Strukturzuordnung. Optional weitere **existierende** Datenfelder, berechtigungsabhängig. Zuordnung bei Bedarf als ganzer Pfad; gleichnamige Knoten differenzierbar.
- Kompakte Kennzahlen **Gesamt / KiJu / Leiter-Erwachsene**, keine erfundenen Statuswerte oder Gesundheitszählungen.
- Tabellenauswahl öffnet **rechte Schnellvorschau**, vorrangig lesend; separate **vollständige Teilnehmerdetailseite** über „Details öffnen“. Tabellenposition, Filter, Sortierung und Auswahl möglichst bewahren. Vorschau bei geringer Breite als überlagerndes Panel statt Tabelle unlesbar zu stauchen.
- Teilnehmererfassung sowohl aus lagerweiter Tabelle (Blattknoten wählen) als auch aus Blattknotendetails (**Gruppenzuordnung vorbelegt**). Beide Wege führen zum selben Fachablauf.
- Vollständige Detailseite **eine zusammenhängende Seite**, keine Tab-Sammlung. Ansicht standardmäßig lesend; gemeinsamer „Bearbeiten“-Modus aktiviert nur berechtigt editierbare Felder, gemeinsamer Speichervorgang und Abbrechen, sofern bestehende API dies sicher trägt. Kein Datenverlust bei Navigationswechsel.
- Stufe, Kategorie, Strukturzuordnung und **Anwesenheit** ausdrücklich in Detailseite und optional Schnellvorschau. Anwesenheit ist nicht automatisch identisch mit aktiv/inaktiv. **Werte/Form und Auswirkungen ausschließlich aus vorhandenem Domänenmodell**; fehlende Fähigkeit separat ausweisen.
- Keine frei erfundenen Stammdatenfelder, Fotos, Notfallkontakte, Notizsysteme, Änderungshistorien, Status-Typen oder Teilnehmerrechte aus den Illustrationen implementieren.

## 5. Datenschutz, Berechtigungen, Offline

- Sensible Angaben zu Ernährung/Gesundheit nur nach effektiver Leseberechtigung; Änderbarkeit zusätzlich nach Schreibberechtigung. Listen, Filter, Kennzahlen, Schnellvorschau, Exporte und Offline-Daten dürfen keinen indirekten Zugriff ermöglichen.
- Ausblenden ist nur UX; Berechtigung muss im zuständigen Backend/Datenzugriff erzwungen sein. Unbekannte oder nicht geladene geschützte Felder dürfen beim gemeinsamen Speichern **nicht überschrieben** werden. Fehlende sichere API-Unterstützung als **Blocker/Follow-up** dokumentieren.
- „Struktur bearbeiten“ für rein lesende Benutzer weglassen; nicht für alle einen deaktivierten Button erzwingen.
- Delegierte Gruppenverantwortliche mit Zugriff nur auf bestimmte Lager/Blattgruppen, eventuell ohne volle Organisationsmitgliedschaft, sind **Zukunftskonzept**, keine implizit bereits existierende Rechtefunktion. Keine neuen Rollen in UX 2.
- Desktop-/Offline-Paket, Tenant-Kontext und bisherige Berechtigungen erhalten.

## 6. Mockup-Verbindlichkeit

| Datei | Referenz für | Nicht daraus übernehmen |
|---|---|---|
| `structure_parent.png` | Übergeordneter Knoten, Baum + Aggregation | Differenzspalte, sofort sichtbare Stufencharts, Beispielwerte |
| `structure_leaf.png` | Blattknoten, Teilnehmerliste | „Untergruppe hinzufügen“ bei belegtem Blatt, widersprüchliche Summen |
| `structure_editor_read.png` | Struktureditor-Lesemodus | Nicht vorhandene Felder/Typen |
| `structure_editor_edit.png` | Visueller Bearbeitungsmodus | Neue persistente Edit-Semantik, neue Strukturregeln |
| `participants_table.png` | Tabelle, Filter, rechte Schnellvorschau | Nicht bestätigte Attribute, sensible Spalten ohne Berechtigung, überflüssige Kennzahlen |
| `participant_detail_reference.png` | Ruhige Detailabschnitte | Erfundenes Feldinventar, Status, Änderungsverlauf |
| `participant_edit_reference.png` | Bearbeitungsdarstellung | Zusätzliche Tabs, nicht bestätigte Stammdaten |

**Bilder sind visuelle Referenzen, keine funktionalen oder zahlenmäßigen Testorakel.** Die dort verwendeten Namen, Zahlen, Labels und Icon-Semantiken sind illustrativ. Keine überzähligen Icons/Navigationseinträge für zukünftige Module.

## 7. Reihenfolge und Abnahme

1. UX 2.1: Strukturansicht, hierarchische Navigation, Knotendetails, Bearbeitungseinstieg und Schätzwertdialog **nur bei vorhandener sicherer Funktion**.
2. UX 2.2: Teilnehmertabelle, Schnellvorschau, Detailseite und gemeinsamer Bearbeitungsmodus, Berechtigungsprüfung und Anwesenheit.
3. Nach jedem Inkrement: automatisierte Tests, manueller Desktop-Test (mind. 1440px), schmale Ansichten soweit möglich, Rechtefälle sowie Erhalt der Verpflegungs-/Offline-Funktionalität.
4. Kein Commit ohne ausdrückliche Abnahme.

## 8. Offene technische Nachweise (nicht als erledigt behandeln)

- Tatsächliche Frontend-Komponenten, bestehende Teilnehmer-/Schätzfelder, Anwesenheitsrepräsentation und Berechtigungsgranularität.
- Möglichkeit für gemeinsames Speichern in Struktur und Teilnehmereditor ohne Semantikänderung.
- Schutz vor Überschreiben nicht zugänglicher sensibler Daten; API-Projektionen.
- Verhalten definierter Strukturen und freier, unterschiedlich tiefer Zweige.
- Im Repository vorhandene Test- und Integrationsabdeckung der jeweiligen Flüsse.
