# Desktop-Roundtrip: festgestellte Lücken und nächster Auftrag

Stand: 2026-09-20. Der Benutzer hat die Umsetzung einer aktuellen Desktop-App
mit bedienbarem Paketimport und Rückexport beauftragt. Die folgenden Befunde
stammen aus Codeprüfung; ein bedienbarer Desktop-Roundtrip ist noch nicht validiert.

## Verbindliche Quellen

- ADR-005: Freeze im Quellsystem, Transfer-ID und unveränderte Baseline, atomarer Replace.
- ADR-009: Einzelgerät mit optionalem lokalem Passwort, unabhängig vom Cloudkonto.
- ADR-011: expliziter Lagerzugriff; kein automatischer Zugriff allein durch Mandantenrolle.
- `docs/architecture/package-format.md`: keine Rückübertragung lokaler Platform-Berechtigungen.

## Konkrete Befunde

1. `CampPackageService.ImportInitialPackageAsync` ruft `Freeze` am neuen lokalen
   Lager auf. Fachliche API-Mutationen und UI lehnen `IsFrozen` ab. Der lokale
   führende Datenbestand wäre dadurch nicht bearbeitbar.
2. Derselbe Aufruf erhöht die neue lokale Baseline von 0 auf 1, statt die
   `Manifest.BaselineVersion` zu übernehmen. Ein Quelllager mit Baseline größer
   als 1 erzeugt dadurch ein nicht passend rückimportierbares Paket.
3. Importiert werden Tenantreferenz und Fachdaten, keine CampMembership und kein
   lokaler Berechtigungsnachweis. Bestehende Camp- und Recipe-Autorisierung setzt
   explizite Mitgliedschaften voraus. Der lokale Importeur erhält derzeit
   keinen bedienbaren Zugang zum importierten Lager.
4. Die Single-Device-Authentifizierung ist in ADR-009 entschieden; der bisherige
   Desktopstarter startet jedoch dieselbe API ohne ausgewiesenen lokalen Modus.
5. Tauri und Entwicklung verwenden beide Port 5180. Der Desktop muss einen
   eigenen eindeutig angebundenen Backendprozess erhalten; vorhandene fremde
   Prozesse dürfen nicht beendet oder versehentlich verwendet werden.
6. FileSystemFileHandle-Schreiben ist in eingebetteten Browsern nicht zuverlässig
   verfügbar. Desktop-Paketdateien benötigen einen nativen Dateidialog/-zugriff.
7. Die vorhandenen MSI/NSIS-Installer vom 2026-08-08 sind veraltet.

## Bestätigte Zugriffsentscheidung (2026-09-20)

Vorschlag: Im ausdrücklich aktivierten Einzelgerätmodus erhält der lokale
Gerätebenutzer beim bewussten Import Zugriff auf genau dieses importierte Lager
für dessen nicht sensible Fachbearbeitung und Rückexport. Das ist eine lokale
Berechtigung, keine Cloudrolle, keine Mandantenadministration und keine
Freigabe zentraler Kataloge. Sie wird niemals in die Cloud zurückübertragen.
Das bereits beschlossene optionale lokale Passwort bleibt davon unabhängig.

Der Benutzer hat diese Zuordnung ausdrücklich bestätigt; ADR-009/ADR-011 wurden
entsprechend ergänzt. Keine weitere Bestätigung hierfür erforderlich. Keine
pauschale Umgehung aller Berechtigungsprüfungen und kein automatisches Anlegen
einer Cloud-CampAdmin-Rolle als Importnebeneffekt.

## Implementierungsreihenfolge nach Klärung

Arbeitsstand: Die Domain-/Package-Korrektur für lokale Bearbeitbarkeit und
Originalbaseline ist implementiert. Die lokale Identität und Zugriffszuordnung,
Desktop-Dateidialoge, Import-/Exportoberfläche und ein neuer Installer stehen
noch aus. Die technische Transferkorrektur allein ist keine fertige Desktop-App.

1. Lokale führende Instanz von eingefrorenem Quelllager unterscheiden; Transfer-ID
   und Originalbaseline beim Import exakt erhalten. Domain-/Provider-Tests,
   insbesondere Export mit Baseline > 1 und zweiter vollständiger Transferzyklus.
2. Einzelgerätmodus, lokale Identität und lagerbegrenzten Zugriff gemäß bestätigter
   Regel implementieren; Cloud-/Local-Server-Isolation erhalten und testen.
3. Desktopstart mit eindeutigem Backend, separatem Datenverzeichnis, funktionierender
   Session und verständlichen Startfehlern vervollständigen.
4. Native Paketdateiauswahl, Initialimport, Rückexport und autorisierten Cloud-
   Rückimport bedienbar machen. Abbrechen verändert keinen Transferzustand.
5. Cloud → lokale Fachbearbeitung → Cloud über echte autorisierte API-Aufrufe testen;
   Ablehnung falscher Berechtigungen und veralteter Pakete nachweisen.
6. Sidecar und Windows-Installer neu bauen, Pfade dokumentieren, manuellen
   Roundtrip durchführen. Erst danach Desktop-Roundtrip als abgeschlossen melden.

Die bisherigen Package-Tests belegen Serialisierung und Replace, nicht die
vollständige Desktopbedienung mit lokaler Anmeldung und fachlicher API-Bearbeitung.
