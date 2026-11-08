# UX Verpflegung V3 – Kocheinheiteneditor

Stand: 2026-11-01. Frontend-Inkrement umgesetzt; manuelle visuelle Abnahme ausstehend. Kein Commit erstellt. Keine API-, Datenmodell-, Migrations-, Berechtigungs- oder Paketänderungen.

## Grundlage und überprüfter Bestand

- `.codex/UX_Verpflegung_Implementierung.md`, Visual Design Guide, Mockup-Index, Domain-Dokumente zur Menüplanung/Inkrement 2 und Teilnehmerstruktur-Korrektur gelesen.
- Originalbilder `06_kocheinheiten_verwaltung.png` und `07_kocheinheit_strukturzuordnung.png` visuell geöffnet. Referenz: Einheitenliste links, kompakter Detailkopf rechts, Tabs, zweispaltige Panels, feste Fußzeile; grüne Auswahlakzente und dezente Rahmen.
- Die im Auftrag genannte `.txt`-Zusammenfassung existiert nicht; stattdessen die vorhandene `docs/domain/implementierungsstand_verpflegung_vom_user.md` verwendet.
- Vorher: Gruppenverwaltung, Erstellformular, sämtliche Kocheinheiteneinstellungen und operative Mahlzeitenkarten untereinander. Direkte Mutation der Übersichtsobjekte, flache Mehrfachauswahl für Strukturen, drei tatsächlich speicherbare Filterwerte.
- Bestehende Persistenz: POST/PUT pro einzelner Kocheinheit; jeweils Name, SortOrder, GroupId, StandardMealPlanId, DefaultStructureNodeIds, ParticipantFilter und TogetherPreference. Gruppen und lagerweite Bedarfsbasis besitzen getrennte Operationen. Kein gemeinsamer Speichervorgang über alle Kocheinheiten und Einstellungen.
- Backend-Strukturvertrag verbietet gleichzeitig ausgewählte Vorfahren/Nachkommen. Elternreferenzen schließen Nachkommen fachlich bereits ein. UI-Teilzustände dürfen deshalb nicht als redundante Einzelreferenzen gespeichert werden.

## Umsetzung

- `CookingUnitsWorkspaceComponent`: organisatorischer Split-View mit Suchfeld, lokaler Sortierung, Einheitenkarten und Detailansicht. Die Sortierauswahl verändert keine gespeicherten Einheitenreihenfolgen.
- Tabs „Allgemein“ und „Struktur & Teilnehmer“. Name, organisatorische Gruppe, Standard-Menüplan und Gemeinsamkeitspräferenz verwenden die vorhandenen Felder. Keine erfundene Beschreibung, Änderungszeit oder Teilnehmerzahl.
- Ein Dropdown für genau einen tatsächlich angebundenen Filter. Die vorhandenen drei Optionen erhalten lesbare Regelzusammenfassungen; keine widersprüchliche zweite Filterauswahl.
- Nach UI-Abnahme: Filter ausschließlich im Struktur-Tab; die parallele Liste ausgewählter Strukturbereiche entfällt. Unter dem Filter steht eine live aktualisierte Vorschau erfasster Teilnehmer mit KiJu-/Leitungs-/offener Kategorie. Die aktuelle ungespeicherte Struktur-/Filterauswahl wird berücksichtigt; es ist kein mahlzeitenbezogener Anwesenheits- oder Portionsbedarf.
- Die uneingeschränkte Zählung nutzt den bestehenden anonymen `participant-counts`-Endpoint. Sonderverpflegungsfilter verwenden den vorhandenen Health-geschützten Teilnehmer-Lesezugriff und projizieren die Antwort unmittelbar auf aggregierte Struktur-/Kategoriezahlen. Namen, Profile und Quellen werden weder angezeigt noch im Vorschauzustand gespeichert. Ohne Leserecht wird „nicht verfügbar“ statt null Teilnehmern angezeigt. Keine API-Erweiterung und kein Umgehen bestehender Rechte.
- Lokaler, von der Übersicht abgetrennter Entwurf je Kocheinheit. Speichern schreibt die einzelne Einheit; Verwerfen stellt den zuletzt geladenen Stand wieder her. Fehler erhalten den Entwurf und zeigen vorhandene serverseitige Referenzhinweise.
- Erstellen/Löschen über vorhandene Endpunkte; serverseitige Überlappungs- und Referenzprüfung bleibt maßgeblich. Nach erfolgreichem Erstellen wird die tatsächliche Server-ID übernommen, auch wenn das anschließende Neuladen scheitert. Ein erneuter Versuch erzeugt dadurch keine zweite Kocheinheit.
- `CookingUnitStructure`: hierarchische Auswahl, Auf-/Zuklappen und Suche mit sichtbaren Vorfahren. Elternwahl selektiert sämtliche Nachkommen, auch bei aktiver Suche. Teilabwahl zeigt rekursiv Minus statt Häkchen.
- Gespeichert werden minimale, nicht überlappende Teilbaumwurzeln. Nicht verfügbare Referenzen bleiben erhalten, bis sie ausdrücklich entfernt werden. Widersprüchliche Strukturdaten blockieren das Speichern; keine stille Umdeutung.
- Zusammenfassung zeigt tatsächliche Strukturreferenzen, Plan, Filter und Präferenz. Die Anzahl organisatorischer Strukturbereiche ist ausdrücklich keine Teilnehmerzahl.
- Gruppen und Bedarfsbasis liegen unter „Verwaltung“. Änderungen an Gruppen und Bedarfsbasis werden separat gespeichert und nicht mit dem Einheitentwurf verwechselt. Der Bedarfsbasis-Endpoint wird erst beim Öffnen dieser Verwaltung benötigt; keine zusätzlichen Gesundheitsabfragen für die normale Einheitenliste.
- Schutz vor Verlust beim Einheitenwechsel, Neuladen, Bereichswechsel und Browser-Unload. Such-/Sortierfelder lösen keine falschen Änderungswarnungen aus. Aktive Speichervorgänge blockieren die Bereichsnavigation. Auch ungespeicherte Gruppennamen und Bedarfsbasisänderungen sind geschützt.
- Operative Bearbeitung bleibt über „Versorgung öffnen“ erreichbar. Bestehende Zielnavigation mit UnitId/MealId führt weiterhin zum vorhandenen operativen Editor; eine kompakte Mahlzeitenauswahl erhält den Zugang zu allen aktiven Slots. V4 wurde nicht als neues operatives Layout vorgezogen.
- Desktop: Liste und Details begrenzen die Höhe; lange Inhalte scrollen innerhalb ihrer Bereiche, Speichern/Verwerfen bleibt im Kartenfuß. Schmal: Liste oberhalb der Details, Panels untereinander.

## Vertragslücke: benannte Filter

`CookingUnit_Participant_Filters.md` beschreibt das Ziel benannter lagerbezogener Filter. Die bestehende Filterverwaltung und ihre Endpunkte sind vorhanden. Der tatsächliche CookingUnit-Vertrag besitzt jedoch weiterhin nur `participantFilter` als Enum mit Alle/Nur Sonderverpflegung/Ohne Sonderverpflegung. Eine gespeicherte Filter-ID, Herkunft und lesbare benannte Definition fehlen in Unit-Request und Unit-Projektion. Die vorhandene Filterverwaltung weist selbst auf die fehlende operative Anbindung hin.

Deshalb werden benannte Filter weder als auswählbar vorgetäuscht noch über neue API-Felder gespeichert. Organisationstemplates sind laut aktuellem Filterkonzept ebenfalls nicht im Umfang. Diese Anbindung benötigt einen gesonderten fachlichen/API-Auftrag; sie bleibt als offene Abweichung zu den Mockups dokumentiert.

## Qualität und Grenzen

176 Frontend-Tests in 27 Dateien erfolgreich. Regressionstests decken insbesondere Teilzustände einschließlich DOM-Checkboxen, kanonische Referenzen, Einschränkungen bei Suche, geschützte Entwürfe, vorhandene Endpunkte, Leseansicht, Requestfehler, Speicherung, Server-ID und operative Zielnavigation ab. Ergänzte Vorschautests prüfen Strukturabdeckung ohne Doppelzählung, Kategorien, Sonderverpflegungsfilter, geschützte Endpunkte, Fehlerzustände und abgebrochene alte Requests. Prüfschritte stehen in der [manuellen Abnahme](../docs/testing/catering-cooking-units-ux.md).

Edge-Versuch: `The browser access host is unavailable`. Keine gerenderte Browseransicht geprüft, keine Nachher-Screenshots erstellt und keine pixelgenaue Übereinstimmung behauptet. Desktophöhe, schmale Ansicht, Scrollverhalten und Fokusdarstellung müssen manuell abgenommen werden.

Keine Testlagerdaten verändert. Die laufende API bleibt unverändert; für dieses Frontend-Inkrement ist kein Backend-Neustart erforderlich.
