# Ergebnis: UX Increment 1.1 – Visual Foundation

Stand: 2026-10-13. Implementiert; visuelle manuelle Abnahme noch offen.
Kein Commit erstellt.

## Grundlage und Bestandsaufnahme

Verwendet wurden die tatsächlich vorhandenen Dateien
.codex/UX_Increment_1_1_Visual_Foundation.md und
docs/architecture/Visual_Design_Guide.md.

Vorhanden: Angular Material mit Green-Palette, globale styles.scss mit festen
Farb-/Abstandswerten sowie lokale Editor-Styles. Keine semantische Tokenbasis.
Shell und Start liegen in AppComponent; Hash-Navigation, Organisations-/Lager-
kontext, Rechteauswertung und konservativer Wechselwarnschutz aus UX 1 bleiben
unverändert. Dessen uncommittete Änderungen wurden erhalten.

## Umsetzung

- Zentrale Light-Tokens für alle Guide-Farben, System-Sans-Schrift, Abstände,
  Radien, Inhaltsbreite und Touch-Ziele in visual-foundation.scss.
- Material-Typografie auf System-Sans vereinheitlicht, keine externen Fonts
  und keine neuen Abhängigkeiten.
- Ruhiger Header mit getrennten Bereichen für Marke, Kontext, Benutzer und
  Navigation. Lange Namen dürfen umbrechen; Benutzerkennung wird nicht auf
  schmalen Ansichten versteckt.
- Aktiver Navigationspunkt: dezente Fläche, stärkeres Gewicht, Unterkante und
  aria-current=page. Eine kleine Directive spiegelt ausschließlich den
  bestehenden Aktivzustand, ohne neue Navigation einzuführen.
- Start: Lager als Einträge mit Name, vorhandenem Zeitraum, echtem Status und
  expliziter, zugänglich beschrifteter Öffnungsaktion. Verlauf mit Leerzustand,
  klar getrennte Abschnitte und kompakte weiterführende Aktionen.
- Gemeinsame Fokus-, Button-, Badge-, Meldungs- und Tabellenbasis. Vorhandene
  Material-Felder bleiben erhalten; Shell-Themefarben an Tokens angebunden.
- Kein Tenant-Rezepteditor, keine neuen Listen, Fachaktionen oder Beispieldaten.
  Keine Änderungen an API, Backend, Package, Rechten oder Speichersemantik
  durch dieses Inkrement.

## Geänderte Dateien dieses Inkrements

- src/frontend/src/visual-foundation.scss (neu)
- src/frontend/src/styles.scss
- src/frontend/src/app/app.component.ts (Template/Directive-Import)
- src/frontend/src/app/shared/current-navigation.directive.ts (neu)
- src/frontend/src/app/app-navigation.spec.ts
- .codex/ux-increment-1-1-result.md (neu)

Weitere im Git-Status sichtbare Änderungen stammen aus UX 1 bzw. den vom
Benutzer hinzugefügten Auftrags-/Guide-Dateien, nicht aus neuen Backendarbeiten.

## Grenzen / Abweichungen

- Bestehende Komponenten-/Editor-Literalwerte bleiben außerhalb dieses Scopes.
  Die zentrale Foundation wird nach den Legacy-Styles eingebunden; sie ist
  eine schrittweise Migration derselben Material-Oberfläche, kein zweiter
  Komponentenbaukasten. Insbesondere Editor-Karten, komplexe Tabellen und
  Dialoge wurden nicht flächendeckend neu gestaltet.
- Bei höchstens 600px ist der Header nicht sticky, damit der mehrzeilige
  Kontext-/Navigationsbereich nicht dauerhaft die Inhaltsfläche verdeckt.
  Kontextinformationen werden nicht entfernt.
- Die vorgegebenen Tokens wurden unverändert übernommen. Zusätzliche
  semantische Größen-Tokens bilden die Guide-Richtwerte ab.
- Bestehende Einschränkungen aus UX 1 (Tenant-Rezepte nur lesend,
  konservativer Warnschutz) bleiben bestehen.

## Prüfstand

- Angular Tests: PASS, 65/65 in 15 Dateien.
  Befehl: npm.cmd test -- --watch=false (src/frontend).
  Neuer Test prüft benannte Lageröffnung und genau einen aria-current-Punkt
  bei Start, Lageröffnung und Rückkehr; bestehende Navigation-, History-,
  Rechte-, Desktop-, Editor- und Supply-Tests bleiben grün.
- Angular Production Build: PASS, npm.cmd run build (src/frontend).
  Ein sandboxierter Wiederholungslauf scheiterte mit spawn EPERM; erneuter
  Lauf mit Freigabe erfolgreich.
- git diff --check: PASS.
- Rechnerische Token-Kontraste auf primary-soft: Primärtext 11,33:1,
  Sekundärtext 5,35:1, Primary 6,33:1, Fokus 4,79:1, Gefahr 5,24:1,
  Warnung 5,00:1, Info 5,03:1, Erfolg 5,15:1.
  Weiße bzw. hellere Canvas-Flächen liefern höhere Kontraste.
  Dies ist keine vollständige gerenderte WCAG-Prüfung aller Fachkomponenten.
- .NET/PostgreSQL/Desktop-Installer: NOT_RUN für dieses rein visuelle
  Inkrement; keine neuen Nachweise aus den vorherigen Tests abgeleitet.
- Sichtprüfung 1440/1024/390px und Vorher-/Nachher-Screenshots: NOT_RUN.
  Browser-Verbindung versucht; „No browser is available“, Browserliste leer.

## Manuelle Abnahme

1. Web-Frontend und Backend starten; mit vorhandenem Testkonto anmelden.
2. Start bei 1440, 1024 und 390px prüfen: Schrift, Karten, vollständige
   Namen, Benutzerbereich, keine horizontale App-Scrollleiste.
3. Leeren und gefüllten Verlauf sowie lange Organisations-/Lagernamen prüfen.
4. Mit Tab alle Navigations- und Öffnungsaktionen erreichen; Fokus und aktive
   Unterkante prüfen. Lager öffnen, zurück, Hash-Link/Reload/Browser-Zurück.
5. Organisationswechsel, eingeschränkte Rechte und Offline-/Paketzugänge
   prüfen; Editoränderung beginnen und Navigation abbrechen.
6. Screenshots von Start und geöffnetem Lager in allen drei Breiten ablegen.

Vor weiteren fachlichen UX-Inkrementen visuell abnehmen. Kein Commit.
