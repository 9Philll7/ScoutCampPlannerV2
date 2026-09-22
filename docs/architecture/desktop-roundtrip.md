# Desktop-Roundtrip

Stand: 2026-09-22. Technische Umsetzung für den passwortlosen Einzelgerätmodus;
kein Produktionsrelease und noch keine abgeschlossene manuelle Desktopabnahme.
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

## Manuelle Abnahme (noch offen)

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
Das ist keine Produktionsfreigabe und kein Nachweis einer ausgeführten
Installation oder manuellen Webview-/Dateidialogprüfung.

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
