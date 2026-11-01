# UX-Verpflegung V1 – Implementierung und Prüfstand

Stand: 2026-11-01. Nur V1 umgesetzt; kein Commit. V2–V4 warten auf Abnahme.

## Bestandsanalyse und Eingaben

- Vorher eine lange Inline-Komponente mit Menüplänen samt Editor, Kocheinheitengruppen, Kocheinheiten und operativen Mahlzeitenkarten. Darunter Tagesplan-Aktivierung und gewichtete Schätzwerte. Kein eigenständiger Verpflegungsüberblick.
- Gemeinsame Anwendungshülle: dunkle grüne Sidebar, helle Arbeitsfläche, semantische Design-Tokens, Hash-Navigation mit Browser-History und bestehendem Verlassen-Guard. Die Shell begrenzte alle allgemeinen Seiten auf 1200 px; der äußere Camp-Kartenrahmen verbrauchte zusätzlich Platz.
- Verbindliche Domainquellen geprüft: `docs/domain/catering-meal-planning.md`, `catering-meal-planning-increment-2.md`, `docs/decisions/meal-planning-participant-structure-correction.md`. Die neuere Filtererweiterung `CookingUnit_Participant_Filters.md` relativiert die drei historischen Enum-Filter; V1 ändert keine Filterlogik.
- Die angegebene Nutzerzusammenfassung `.txt` fehlt; vorhandene `.md` gelesen. Die Zusammenfassung enthält noch die drei historischen Filter. Keine neuen Filterfähigkeiten daraus abgeleitet.
- Tatsächliche Verträge geprüft: `MealPlanningService.cs`, `MealPlanningRequirements.cs`, `OperationalSupply.cs`, API-Overview-Endpunkt und `MealVerificationService.cs` sowie Angular-API-Service. Mahlzeiten, Planversion/fehlende Angebote, Kocheinheiten, Teilstände, Probleme, Ask-Auswahlmöglichkeiten und Verifikation sind verfügbar. Kein passender Änderungszeitpunkt für „zuletzt bearbeitet“, keine redaktionelle Historie, kein berechtigtes Fortschrittsprozent vorhanden.

## Originalbilder: tatsächlich visuell geprüft

Der Index stimmt nicht mit den gelieferten Dateiinhalten überein. Dateien unverändert belassen:

| Datei | Tatsächlicher Inhalt |
|---|---|
| 01 | Kocheinheit: Strukturzuordnung und Filter |
| 02 | Kein PNG: enthält Markdown (`# Mockup-Index Verpflegung`), Bildöffnung fehlgeschlagen |
| 03 | Verpflegungsübersicht mit vier Kennzahlenkarten, Handlungsbedarf und „zuletzt bearbeitet“ |
| 04 | Operative Mahlzeitenplanung, eine Einheit mit Mahlzeitennavigation |
| 05 | Menüplanübersicht mit breiter Planliste und Tagesdetails |
| 06 | Collage mit Rezeptauswahldialog und Komponentenworkflow |
| 07 | Menüplaneditor, Standard und Alternativen zweispaltig |
| 08 | Menüplaneditor mit kompakten Alternativen |
| 09 | Kocheinheiten-Split-View mit operativer Mahlzeit |
| 10 | Identisch mit 09 (SHA-256 geprüft) |

V1 verwendet deshalb den Inhalt von **03** als visuelle Referenz. Gegenüber vorher: klare Seitentitel, vier farblich zurückhaltende Karten, darunter zweispaltige Arbeitsfläche mit priorisierten Aufgaben links und Prüfstand rechts. Keine neue Designrichtung, weiterhin bestehende Tokens und Iconsystem. Die Illustrationsfotos/Beispielzahlen wurden nicht übernommen.

## Änderungen

- Neue read-only `CateringOverviewComponent` mit Vertragsprojektion `cateringSummary`.
- Kennzahlen: Menüpläne, aktive Mahlzeiten, fehlende Belegungen über alle Pläne, Kocheinheiten mit Standardplan, betroffene Mahlzeiten mit Konflikten, offene Ask-Entscheidungen und ausstehende Verifikationen.
- Handlungsbedarf priorisiert: Konflikte, Ask, fehlende Standardangebote, Berechnungsstand, Verifikation. Übersicht maximal fünf Aufgaben; vollständige Ansicht mit Kategorienfilter und Seiten.
- Zielaktionen öffnen den betroffenen Plan oder die bestehende Kocheinheitenansicht, eingeschränkt auf die betreffende Einheit/Mahlzeit. Ein Rückweg zeigt alle Kocheinheiten. Detailauswahl ist aktuell UI-Zustand; der Hash speichert die Bereichsroute, nicht die konkreten Objekt-IDs.
- Bestehende Routen erweitert: `catering` (Übersicht), `catering-plans`, `catering-units`, `catering-issues`. Sidebar nur mit diesen implementierten Zielen; keine Platzhalter für Versorgung/Einkauf.
- Bestehende Editoren lediglich nach Bereich getrennt; keine V2-Menüplan-Neugestaltung, kein V3-Strukturbaum und keine V4-Neuimplementierung. Tagesplan-Aktivierung und gewichtete Schätzwerte bleiben unter Menüplänen erreichbar.
- Überholter Hinweis zur angeblich unvollständigen Integration der Versorgungseignung entfernt.
- Folgekorrektur zur Breite/Höhe: Verpflegung nutzt die volle Arbeitsbreite statt 1200 px. Äußerer Camp-Kartenrahmen und dessen Innenabstände entfallen nur hier. Kennzahlen, Abstände, Aufgabenzeilen und Prüfstandszeilen verdichtet. Lange Aufgabenbeschreibungen werden auf breitem Desktop nach zwei Zeilen gekürzt, bleiben als Tooltip und am Ziel verfügbar. Schmale Ansichten stapeln weiterhin regulär; kein Abschneiden des gesamten Seiteninhalts und kein globales Scroll-Verbot.

## Fachliche Grenzen

- Keine API-, Domain-, DB-, Auth- oder Offline-Paketänderungen. Vorhandene API-Basis/Anmeldeoptionen gelten auch lokal. Freeze und bestehende Schreibschutz-Eingaben bleiben bestehen.
- Camp.View sowie Health-Zugriff werden weiterhin serverseitig geprüft; Verifikation erfordert weiterhin separates Recht. Keine zusätzlichen personenbezogenen Daten geladen und keine neuen Gesundheitsrechte abgeleitet.
- Keine Menüplan-Tagesvorschau oder fingierte Bearbeitungshistorie. Stattdessen belegbarer Prüfstand.
- Konfliktzahl bezeichnet **betroffene Kocheinheit×Mahlzeit-Teilstände**, nicht Personen oder Schweregrad. Ask wird separat gezählt; die alte Problemcode-Zahl 19 entspricht dem geprüften Enum `CommonSupplyDecisionRequired`. Ask-Auswahlmöglichkeiten und Ask-Probleme werden pro Teilstand nicht additiv doppelt gezählt.
- Fehlende Teilstände gelten nicht als aktuell/verifiziert. Berechnungsaktualität und Verifikation werden getrennt ausgewiesen. Auch aktuelle Verifikation mit begründeter Abweichung wird nicht als Konfliktfreiheit bezeichnet.
- Ausstehende Verifikation zählt gespeicherte Berechnungen mit Versorgungsergebnis ohne aktuelle Verifikation. Noch nicht berechnete Kontexte werden separat angezeigt. Keine Summierung von Portionsbedarfen additiver Komponenten.
- Konflikte/Entscheidungen stammen aus gespeicherten Teilständen; ein Datenhinweis begrenzt ihre Aussagekraft. Veraltete betroffene Aufgaben sind zusätzlich markiert.

## Qualitätssicherung und offene Abnahme

- Production Build nach der Breiten-/Höhenkorrektur erfolgreich.
- Bestehende Tests ausgeführt; die Regression zum Schutz ungespeicherter Versorgungsentscheidungen benötigte den neuen expliziten Kontext `section = units`. Der Schutz selbst blieb erhalten. Abschließender Gesamtlauf: **117 Tests in 22 Dateien erfolgreich**, `git diff --check` erfolgreich.
- Edge-Browser-Connector: `The browser access host is unavailable`. Der native Computer-Use-Versuch fand das ScoutCampPlanner-Edge-Fenster, wurde aber vor der UI-Erfassung vom Werkzeug beendet: aktuelle Browser-URL nicht ausreichend sicher bestimmbar. Daraufhin wurde dieser Durchlauf beendet.
- **Keine gerenderten Desktop-/Mobil-Screenshots erzeugt und keine Scrollfreiheit bei 1920×1080 behauptet.** Die spätere Benutzerabnahme meldete zu geringe Breite und zu große Höhe; die oben beschriebene Korrektur behebt die Ursachen im Layout, ist noch visuell zu bestätigen.
- Tastatur: native Buttons, beschrifteter Kategorienfilter, sichtbarer Fokus; Aufgaben direkt über Buttons erreichbar. V1 ergänzt keinen Dialog/Tri-State. Schmale Layouts über Media Queries, noch ohne Browserabnahme.
- Offen: Desktop 1920×1080 und schmale Ansicht im Browser vergleichen; Aufgaben mit langen Texten und veraltetem Stand prüfen; Bereichswechsel/History lokal und online manuell abnehmen. Spätere Detailinkremente bleiben ausdrücklich außerhalb V1.
