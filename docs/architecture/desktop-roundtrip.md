# Desktop-Roundtrip

## Nachtrag 2026-10-11: SQLite-Importfehler nach lokalem Entfernen

Ursache auf einer Kopie der betroffenen Desktop-Datenbank reproduziert:
SQLite-Verbindungen des API-Hosts aktivierten Fremdschlüssel nicht explizit.
Der Windows-Provider führte deshalb Löschkaskaden nicht aus. Bei null Lagern
blieben unter anderem Abwesenheiten und lokale Freigaben zurück; ein neuer
Import scheiterte an `CampParticipantAbsentDays` (UNIQUE constraint).
Die unbehandelte HTTP-500-Antwort verlor CORS und erschien als Netzwerkfehler.
Die unten dokumentierte native IPC-Vermutung war **nicht** die Ursache dieses Falls.

Korrektur: `SqliteRuntimeConnection` erzwingt `Foreign Keys=True`, auch bei
abweichender Konfiguration. Datenbankfehler der Package-Endpunkte liefern einen
generischen `package_database_error` mit erhaltenem CORS; Details nur im Backendlog.
Keine automatische Importwiederholung oder Datenbereinigung beim App-Start.

Mit ausdrücklicher Zustimmung wurde die geschlossene lokale Desktop-Datenbank
gesichert, die Reparatur auf einer Kopie samt erneutem Import geprüft und erst
danach auf den Originalbestand angewendet. `tools/repair-empty-desktop-orphans.sql`
ist ein einmaliges, transaktionales Reparaturskript, keine Migration. Es verlangt
ein leeres Lager-/Teilnehmerverzeichnis, entfernt ausschließlich verwaiste Zeilen
der diagnostizierten Tabellen und prüft Fremdschlüssel und Integrität vor Commit.
Unbekannte Inkonsistenzen oder gültige restriktive Referenzen brechen die Reparatur ab.
Nicht ohne Datenbank-/Auditsicherung und neue Einzelfallprüfung wiederverwenden.

Sicherung: `%LOCALAPPDATA%/org.scoutcampplanner.desktop/backups/before-orphan-repair-20261011-210828/`
(SQLite-Datei und `audit-security`). Auditdateien sind hashgleich geblieben.
48 verwaiste Ausgangszeilen einschließlich drei nicht mehr gültiger lokaler
Freigaben sowie deren kaskadierende Unterdaten entfernt; keine vorhandenen Lager
oder gültigen Freigaben gelöscht. Wiederherstellung nur bei geschlossener App,
mit zusammengehöriger Datenbank und Auditdateien aus dieser Sicherung.
Nach Neuimport Health-/Verify-Freigaben explizit für den neuen Transfer vergeben.

PASS: zwei neue Connection-/Cascade-Regressionen, Import auf reparierter Kopie,
HTTP 500 mit Fehlercode und CORS auf unreparierter Kopie, isolierter Sidecar-Roundtrip
mit Teilnehmern einschließlich Entfernen/Reimport, Release-Build und MSI/NSIS.
Vollständige Backend-Suite: 511/511 PASS, einschließlich PostgreSQL-, Package-,
Architektur- und Sicherheitstests. `git diff --check`: PASS.
Sidecar-Publish, Tauri-Binary und Release-Sidecar sind SHA256-identisch:
`8D0D6DAE7747C74359B1D688F102C7B1C621DD13E60F0B6324B9BEB7BCD1193C`.
Installierte WebView-Abnahme dieses Fixes: **NOT_RUN**. Neues Bundle installieren,
dann Paket einmal öffnen; die alte installierte Version nicht weiterverwenden.

## Nachtrag 2026-10-11: native Paketdatei-Übergabe

Gemeldeter Fehler: `Failed to fetch` nach der Dateiauswahl, obwohl der lokale
Sidecar erreichbar war. Der bisherige native Befehl gab die gesamte Datei als
JSON-Zahlenarray zurück. Tauri 2.11.5 verwendet bei größeren Channel-Rückgaben
einen zusätzlichen Fetch-Pfad (JSON-Schwelle 8192 Bytes). Das ist ein begründeter
Verdacht für den WebView-Fehler, kein abschließend reproduzierter WebView-Nachweis.

Die Übergabe liefert jetzt eine temporäre Auswahl-ID und Dateigröße, anschließend
Blöcke von höchstens 1024 Bytes. Deren JSON bleibt unter dieser Schwelle. Nur die
explizit ausgewählte Datei ist lesbar; keine frei übergebenen Dateipfade. Maximal
100 MB einschließlich tatsächlicher Lesebegrenzung, Freigabe des Puffers nach dem
Lesen/bei Fehler, kein Backend-Import bei Abbruch oder unvollständiger Übertragung.
Dateiübergabe- und Backendfehler werden getrennt angezeigt. Keine automatischen
Import-Wiederholungen; Package-, Rechte-, Freeze-/Replace-Logik unverändert.

Prüfung: Rust 2/2 (Byte-Roundtrip mit 62411 Bytes, Blockgrenze, Auswahlwechsel,
leere Datei), Angular 53/53 einschließlich sieben neuer Übertragungs-/Importtests.
Production Build, Tauri Release/MSI/NSIS und `git diff --check`: PASS am 11.10.2026.
Installer unter `src/desktop/src-tauri/target/release/bundle/{msi,nsis}/`.
Installierte WebView-Abnahme: **NOT_RUN**. Nach Installation des neuen Bundles
dieselbe Datei erneut auswählen; einen gegebenenfalls bereits erfolgreichen
Import nicht durch Löschen des Lagers rückgängig machen. Bei sehr großen Paketen
kann die kleinteilige native Übertragung länger dauern; kein Lastnachweis bis 100 MB.
Lokale Nutzer-/Lagerdaten und Freigaben wurden für diese Korrektur nicht verändert.

## Bisheriger Basisstand

Stand: 2026-09-22. Technische Umsetzung für den passwortlosen Einzelgerätmodus;
Der unten aufgeführte Kernablauf ist manuell bestätigt; ergänzende Prüfungen
bleiben offen. Kein Produktionsrelease.
Grundlage: ADR-009 und ADR-011, bestätigte lokale Importberechtigung.

## Umsetzung

- Tauri startet einen eigenen selbstständigen ASP.NET-Core-Sidecar an einem freien
  Loopback-Port. Entwicklungsserver werden weder verwendet noch beendet.
- SQLite und Auditdateien liegen unter
  `%LOCALAPPDATA%/org.scoutcampplanner.desktop/`, getrennt von der Entwicklungsdatenbank.
- Ein kryptografisch zufälliges Token pro Start wird an den Sidecar und das eigene
  Webview übergeben. Die API verlangt Loopback und dieses Token. Das ersetzt keine
  Dateiverschlüsselung und schützt nicht vor Schadsoftware im selben Benutzerkonto.
- Die persistente Geräteidentität ist kein Cloudkonto. Beim bewussten Initialimport
  werden Lagerzugriff und Fachdaten atomar angelegt. Der Zugriff ist an Identität,
  Mandant, Lager und aktiven Transfer gebunden und wird nicht exportiert.
- Erlaubt sind ausdrücklich gelistete nicht sensible Lager-/Cateringaktionen und
  Rückexport. Keine Mandanten-/Benutzerverwaltung oder zentrale Katalogverwaltung.
- Native Dateidialoge vermeiden die Einschränkungen des eingebetteten VS-Code-Browsers.
  Abbrechen löst keinen Import aus; Rückexport friert nichts neu ein. Importlimit: 100 MiB.
- Der Browser bietet den Rückimport. Der Server prüft Lagerberechtigung, Transfer
  und Baseline. Name, Zeitraum, Struktur und enthaltene Cateringdaten werden atomar
  übernommen. Wiederholte Rückimporte werden abgelehnt.
- Neue Rust-Abhängigkeiten: `rfd` für Dateidialoge, `getrandom` für Starttoken.
  Keine neuen Backend-/Frontend-Libraries.

## Automatisierte Prüfung

- Platformtests: Token, Loopback, expliziter Modus, persistente Identität,
  Isolation nicht freigegebener/eingefrorener/veralteter Transfers, Permission-Allowlist.
- Package-Regression: Gerätezugriff atomar, keine Rückübertragung der Freigabe,
  Originalbaseline nach mehreren Transferzyklen, Rückübernahme der Grundeinstellungen.
- Frontendtests: Token nur an eigenen Sidecar, keine Wiederholung schreibender
  Requests, keine nativen Befehle im normalen Browser.
- .NET-Prüfungen einschließlich Entsperrung und lokaler Löschung: 358 Tests erfolgreich,
  davon 4 Architekturtests (Gesamtlauf am 2026-09-22).
  PostgreSQL-Livetests benötigen zusätzlich die dokumentierte Test-Konfiguration;
  der Standardlauf ersetzt diese nicht. Frontend: 14 Tests erfolgreich.
- `powershell -ExecutionPolicy Bypass -File tools/test-desktop-roundtrip.ps1` nach
  `tools/prepare-desktop.ps1`: Zwei echte API-Prozesse mit temporären SQLite-Datenbanken,
  Cloudsetup/Anmeldung, Freeze, lokaler Import/Bearbeitung, Rückexport, Cloud-Replace,
  verbotene Administration und Ablehnung erneuter Rückübertragung.
  Erfolgreich am 2026-09-21. Nur test-eigene Prozesse werden beendet; Artefakte
  verbleiben im ausgegebenen Temp-Verzeichnis. Kein Test des installierten Webviews.

## Manueller Prüfstand

Am 2026-09-22 hat der Benutzer folgenden Ablauf ausdrücklich als erfolgreich bestätigt:

1. Offlinepaket online erstellt.
2. Paket in der lokalen Desktop-App geöffnet.
3. Lagerdauer lokal geändert.
4. Rückpaket exportiert.
5. Rückpaket online erfolgreich importiert.
6. Lokales Lager entfernt.
7. Ursprüngliches Offlinepaket erneut lokal geladen.

Damit sind der Kern-Roundtrip mit Zeitraumänderung sowie lokale Bereinigung und
Wiederimport manuell nachgewiesen. Schritt 7 ist kein neuer Transferzyklus: Das
ursprüngliche Paket enthält die abgeschlossene Transfer-ID. Ein daraus erzeugtes
Rückpaket darf online nicht mehr angenommen werden. Für eine neue Offlinephase
muss online ein neues Paket erstellt werden. Diese Unterscheidung wurde vom
Benutzer bestätigt; eine manuelle Prüfung der Ablehnung ist damit nicht behauptet.

### Technischer Prüfverlauf

Nachkorrektur 2026-09-22: Die Antwort beim Speichern der Lagergrundeinstellungen
behält Transfer-ID, Baseline und Importberechtigung bei. Sonst verlor die Oberfläche
die Transfer-ID für „Lokale Kopie entfernen“. Der Package-Validator akzeptiert
erhaltene Mahlzeiten außerhalb eines geänderten Lagerzeitraums gemäß Fachregel.
Regressionen im Update- und vollständigen Package-Roundtrip-Test abgesichert.
Fehlgeschlagene Rückpaket-HTTP-Aufrufe werden nicht mehr als Dateidialogfehler angezeigt.

Technische Freigabe für den manuellen Test am 2026-09-22: .NET 358/358,
Frontend 14/14, HTTP-Roundtrip mit Entfernen/Wiederimport erfolgreich;
NSIS-/MSI-Build erfolgreich. Der Löschtest prüft auch vollständigen Rollback
bei einem erzwungenen Datenbankfehler und Erhalt eines zweiten lokalen Lagers.
Der zuvor fehlgeschlagene Test enthielt keine Pflichtstufen; die Testdaten
wurden korrigiert, ohne Produktvalidierung abzuschwächen.
Historisches NSIS-Artefakt vor dieser Nachkorrektur vom 2026-09-22, 09:31:42 (lokale Zeit), 38.388.835 Bytes,
SHA-256: `F21C83C8F7C3786B1E46B59F90D91190BAD86C95798E9F540E890F854DF101B9`.
Diese automatisierte Freigabe ist von der oben dokumentierten Benutzerprüfung
getrennt und stellt keine Produktionsfreigabe dar.

### Ergänzende manuelle Prüfungen (noch nicht ausdrücklich bestätigt)

Offen bleiben die Abbruchfälle der Dateidialoge, Zugriffsisolation, Erhalt von
Änderungen nach App-Neustart, lokale Mahlzeitenplanänderungen im Roundtrip,
Ablehnung falscher/veralteter Rückpakete und Entsperrung ohne Rückpaket einschließlich
Abbruch der Warnbestätigung. Der ursprüngliche erweiterte Prüfablauf zur Orientierung:

1. Aktuellen Installer aus
   `src/desktop/src-tauri/target/release/bundle/nsis/ScoutCampPlanner_0.1.0_x64-setup.exe`
   installieren und starten. Kein Cloudsetup/Cloudlogin erforderlich.
2. Im normalen Browser ein Testlager exportieren. Das Quelllager bleibt eingefroren.
3. Desktop: „Lagerpaket öffnen“, zunächst abbrechen, dann importieren. Nur dieses
   Lager ist sichtbar; Organisationseinstellungen sind nicht zugänglich.
4. Lagername und Mahlzeitenplanung lokal ändern, App schließen und neu starten.
   Änderungen und Zugriff bleiben erhalten; Entwicklungsserver bleibt unbeeinflusst.
5. „Rückpaket speichern“: Abbrechen prüfen, danach Datei speichern.
6. Browser: auf der eingefrorenen Lagerkarte „Rückpaket importieren“. Änderungen kontrollieren, Quelllager wieder
   bearbeitbar. Dasselbe Rückpaket erneut importieren: verständliche Ablehnung.

## Bekannte Grenzen / nächste Schritte

Für ein verlorenes Paket bietet die eingefrorene Lagerkarte jetzt „Ohne Rückpaket
entsperren“. Nach ausdrücklicher Warnbestätigung bleibt der Serverstand erhalten,
der Transfer wird ungültig und seine Rückpakete können nicht mehr eingespielt
werden. Abbrechen der Bestätigung ändert nichts. Mit einem Testlager auch prüfen:
falsches Lagerpaket wird abgelehnt; nach Entsperrung werden alte Rückpakete abgelehnt.
Diese Aktion aktualisiert nicht die getrennte lokale Kopie.

- Optionales lokales Passwort ist entschieden, aber noch nicht als Einstellung umgesetzt.
  Aktuell Windows-Benutzerzugriff ohne zusätzliche App-Sperre.
- Initialimport eines lokal vorhandenen Lagers wird abgelehnt. Die lokale Lagerkarte
  bietet „Lokale Lagerkopie entfernen“ mit Verlustwarnung. Danach kann ein Paket
  desselben Lagers erneut importiert werden. Entfernt werden lokale Planung,
  Struktur, Lagerrezeptbibliothek, lagereigene Rezepte/Zutaten und Zugriffsfreigabe
  in einer Transaktion. Gemeinsame Katalogdaten, Geräteidentität, Mandantenreferenz,
  Serverdaten und Paketdateien bleiben erhalten. Fremdreferenzen blockieren die
  Löschung vollständig. Die Aktion entsperrt nicht das Quelllager und ist im
  Servermodus gesperrt. Noch nicht exportierte lokale Änderungen sind danach verloren.
- Nach Rückimport weiß die getrennte lokale App nicht automatisch, dass die Cloud
  übernommen hat. Danach nicht lokal weiterarbeiten; keine automatische Synchronisation.
- Mutable lokale Rezeptentwürfe gehören laut Package-Vertrag nicht zum Rückimport.
  Aktueller Roundtrip insbesondere für Lager- und Mahlzeitenplanung; keine Zusage,
  dass jede mögliche Editoränderung zurückübertragen wird.
- Dedizierte Auditierung der Paket-/Gerätezugriffsereignisse nach ADR-012, optionale
  Passwortbedienung und produktive Paketabsicherung bleiben offen.
- Pakete sind prüfsummengesichert, aber nicht signiert oder verschlüsselt.
- Windows-10-Best-Effort-Unterstützung unverändert und auf echtem Gerät ungeprüft.
