# Ergebnis: UX Increment 1

Stand: 2026-10-13. Grundlage: abgeschlossener Inkrement-2-Stand (87d498a).

## Status

Navigation und Anwendungsshell implementiert; automatisierte Prüfungen grün
mit unten genannter PostgreSQL-Ausnahme. Bereit für manuelle UX-Abnahme.
Keine neue Fachlogik, Datenbankschemas, Rollen oder Berechnungsregeln.
Kein Commit erstellt.

## Bestandsaufnahme und Zuordnung

Vorher: zustandsbasierte Navigation direkt in AppComponent, kein Angular-Router,
keine URL-Deep-Links. Globale Umschaltung blieb im geöffneten Lager sichtbar.
Rezept- und Zutateneditoren lagen im Verpflegungstab; Organisationszutaten
zusammen mit Stufenvorlage und Ernährungsformen in der Organisationsansicht.

| Bestehender Screen | Neue Navigation |
| --- | --- |
| Lagerkarten, Anlegen, Paketaktionen | Organisation → Lager |
| Neu: Einstieg aus vorhandenen zugänglichen Lagern | Organisation → Start |
| Vorhandene Tenant-Rezept-Lese-API, bisher ohne eigenen Screen | Organisation → Rezepte (lesend) |
| Tenant-Zutateneditor | Organisation → Zutaten |
| Stufenvorlagen, Ernährungsformen | Organisation → Organisation, berechtigungsabhängig |
| Zentraler Zutatenstamm | Berechtigter Unterbereich von Zutaten, kein zusätzlicher globaler Hauptpunkt |
| Geöffnetes Lager | Lager → Übersicht |
| Strukturbaum und Teilnehmereditor | Lager → Struktur & Teilnehmer, zwei Unteransichten |
| Mahlzeitenplanung, Tagesplan, gewichtete Summen | Lager → Verpflegung |
| Lagerrezepteditor | Lager → Rezepte |
| Lagerzutateneditor / Auswahl von Kopiervorlagen | Lager → Zutaten |
| Bisherige Grundeinstellungen einschließlich Strukturtiefe/Stufen | Lager → Einstellungen |

Ein eigenständiger Bereich Auswertungen & Listen wurde nicht erfunden.
Die vorhandene Summenansicht bleibt unter Verpflegung.

## Umsetzung

- Aktive Organisation und geöffnetes Lager bleiben in der fixierten Kopfzeile
  sichtbar. Organisationswechsel explizit außerhalb des Lagermodus.
- Globale Navigation: Start, Lager, Rezepte, Zutaten, Organisation; Verwaltungs-
  zugänge abhängig von vorhandenen Rechten. Im Lager ausschließlich Lagernavigation
  und eindeutiger Rückweg zur Lagerübersicht, kein Lagerwechsel-Menü.
- Start zeigt tatsächlich zugängliche Lager und zuletzt geöffnete Lager.
  Letztere beruhen auf benutzerbezogenen IDs im SessionStorage und werden mit
  der aktuellen Organisations-/Zugriffsliste geschnitten. Keine erfundenen
  Aufgaben oder Statuswerte. Ohne verfügbare Organisation (etwa leere lokale
  Installation) wird ein expliziter Leerzustand statt eines fiktiven Kontexts angezeigt.
- Hash-Adressen: #/organization/{tenantId}/{Bereich} bzw.
  #/organization/{tenantId}/camps/{campId}/{Lagerbereich}.
  Reload und Browser-Rücknavigation stellen den zugänglichen Kontext wieder her.
  Nicht zugängliche Lager erzeugen keine Fremdkontext-Anfragen.
  Keine neuen Routing-Abhängigkeiten; Hash-Adressen benötigen keine Server-
  Rewrite-Regeln und sind auch für den Desktop nutzbar.
- Schutz beim Bereichs-/Organisationswechsel, Logout und Browser-Verlassen.
  Bestehende Editoren besitzen keinen gemeinsamen Dirty-State-Vertrag:
  der Schutz ist bewusst konservativ und fragt bei offenen Eingabebereichen
  gegebenenfalls auch ohne ungespeicherte Änderung oder nach dem Speichern.
- Lagerrezeptübersicht filtert den vorhandenen Katalog auf lokale Rezepte und
  verwendete Revisionen aus Plänen, individuellen Rezeptentscheidungen und
  aktiven/manuellen SupplySolutions. Die vollständige Rezeptauswahl bleibt
  unverändert; der Filter ändert weder Bibliothek noch Package-Referenzen.
- Lokale Zutatenbearbeitung bleibt lagerbezogen. Zugängliche zentrale/
  Organisationszutaten sind als aufklappbare Kopiervorlagen erreichbar;
  keine organisationsweite Bearbeitung innerhalb des Lagers.
- TenantOption exponiert zusätzlich CanManageSettings und CanCreateCamps
  aus dem bestehenden AuthorizationCatalogue. Keine neue Berechtigungsprüfung
  oder neue Rolle; vorhandene serverseitige Durchsetzung bleibt maßgeblich.
- Offlinephase/Schreibschutz sichtbar; Import, Export, Rückimport, Entsperren
  und Entfernen lokaler Kopien bleiben in der Lagerübersicht erreichbar.

## Abweichungen / Grenzen

- Organisationsrezepte sind aktuell nur lesend integriert. Im vorhandenen
  HTTP-Vertrag gibt es keinen Tenant-Rezepteditor für Erstellen/Bearbeiten/
  Veröffentlichen. Keine Camp-Endpunkte mit Tenant-IDs zweckentfremdet.
  Vollständige Organisationsrezeptverwaltung benötigt einen separat bestätigten
  funktionalen Anschluss der bestehenden Anwendungsfälle.
- Die vorhandene Tenant-Zutatenoberfläche ist ein Verwaltungseditor, kein
  allgemeiner Nur-Lese-Katalog. Zugang daher nur mit vorhandener Verwaltungs-
  berechtigung; berechtigte Zentralverwalter behalten den zentralen Zugang.
- Kein vollständiger neuer Änderungsstatus-Vertrag für alle Editoren,
  kein Editor-Redesign und kein neues Designsystem.
- Sensitive Offline-Daten bleiben Development-only mit Dummy-/Testdaten.

## Tests

- dotnet build ScoutCampPlanner.slnx -c Release --no-restore -m:1 /nr:false:
  PASS, 0 Warnungen/Fehler.
- dotnet test ScoutCampPlanner.slnx -c Release --no-build --no-restore -m:1 /nr:false:
  526 erfolgreich, 1 übersprungen, 0 fehlgeschlagen.
  Architektur 4, Catering 292, Migrationen 19 (+1 Skip), Package 18,
  Platform 109, Security 84.
- PostgreSQL-spezifischer Nachweis: NOT_RUN. Docker meldet
  „Docker Desktop is unable to start“. Providerabhängig ohne Konfiguration
  zurückkehrende Tests zählen nicht als PostgreSQL-Nachweis.
- Angular: 64/64 PASS in 15 Dateien. Acht neue Tests für Shell-/Kontextwechsel,
  Rechteanzeige, Deep Links, Navigation abbrechen, Unload, Desktop-Paketzugang
  und Rezeptanzeigeprojektion. Bestehende Editor-/Supply-/Package-Tests grün.
- Neuer Backend-Test bestätigt Navigationsfähigkeiten für Owner, Member
  und nicht zugehörigen Benutzer.
- Angular Production Build: PASS.
- git diff --check: PASS. Keine generierten Build-/Datenbankartefakte vorgemerkt.
- Erster sandboxierter Angular-Lauf konnte den Fork-Runner nicht starten;
  Wiederholung mit Freigabe erfolgreich.

## Manuelle Abnahme – NOT_RUN

1. Start mit zwei Organisationen: nur passende Lager, echte zuletzt verwendete
   Lager, expliziter Organisationswechsel, korrekte Kopfzeile.
2. Lager aus Start/Lagerliste öffnen: nur lokale Navigation; Organisation und
   Lagername sichtbar; Rückweg zur Lagerübersicht.
3. Struktur, Teilnehmer, Verpflegung, Rezepte, Zutaten und Einstellungen öffnen;
   vorhandene fachliche Abläufe unverändert durchlaufen.
4. Eingaben ändern, Navigation abbrechen, danach bewusst verlassen; Reload,
   Browser-Zurück und kopierten Deep Link einschließlich falschem Lager prüfen.
5. Mit einfachem Mitglied und Administrator Verwaltungszugänge vergleichen;
   zentrale Verwaltung bleibt außerhalb des Lagermodus.
6. Lokale App: Paket importieren, Kontext/Schreibschutz prüfen, Rückpaket
   speichern. Kein neuer Installer in diesem UX-Auftrag erstellt.
7. Schmale Ansicht und lange Organisations-/Lagernamen visuell prüfen.

## Commit-Bereitschaft

Änderungen nicht committed. Nach manueller UX-Abnahme sinnvoller Commit:
„Überarbeite Anwendungsshell und kontextbezogene Navigation“.
