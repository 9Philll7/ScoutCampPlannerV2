# Basiszutaten – Übergabe und nächste Schritte

## Zweck

Diese Datei dient als Arbeitsübergabe für die weitere Implementierung auf einem anderen Gerät oder in einem neuen Codex-Chat.

Verbindliche Quellen:

- `.codex/BASISZUTATEN_IMPLEMENTIERUNG.md`
- `docs/domain/Basiszutaten.md`
- `docs/architecture/Basiszutaten_Datenbankarchitektur.md`
- `docs/architecture/Basiszutaten_Schema.sql`
- `docs/decisions/adr-018-ingredient-and-conflict-catalog-scopes.md`
- `docs/decisions/adr-020-ingredient-management-permissions-and-search.md`
- `docs/decisions/adr-021-revisioned-base-ingredients.md`

Die Repository-Dokumentation hat Vorrang vor dieser Übergabe und vor früheren Chatverläufen.

## Aktueller Implementierungsstand

### Bereits committet

Das erste Domain-Inkrement enthält:

- stabile `IngredientIdentity`
- getrennte Scopes `Central`, `Tenant` und `Camp`
- Draft-/Publish-Lebenszyklus
- unveränderliche veröffentlichte Revisionen
- neue Drafts auf Basis einer veröffentlichten Revision
- lokale Eigenanlagen
- Tenant- und Camp-Forks zentraler Zutaten
- `SourceIngredientId` und `SourceRevisionId`
- höchstens einen Draft je Zutatenidentität
- Archivierung auf Ebene der Zutatenidentität
- grundlegende Domain-Tests

### Ebenfalls bereits committet

Das zweite Domain-Inkrement enthält:

- `IngredientPropertyState`
  - `Contains`
  - `DoesNotContain`
  - `MayContain`
  - `Unknown`
- `IngredientPropertySource`
- `IngredientCompatibility`
- revisionsgebundene Allergene, Unverträglichkeiten und Herkunftsmerkmale
- `IngredientVariantRevision` mit stabilem `VariantKey`
- Varianten-Overrides für Eigenschaften
- effektive Vererbung `Basiswert + Override`
- Übernahme von Eigenschaften und Varianten in neue Drafts und Forks
- sichere Grundauswertung einzelner Eigenschaften
- zusätzliche Domain-Tests

Betroffene Dateien:

- `src/backend/ScoutCampPlanner.Catering.Domain/IngredientIdentity.cs`
- `src/backend/ScoutCampPlanner.Catering.Domain/IngredientRevision.cs`
- `src/backend/ScoutCampPlanner.Catering.Domain/IngredientProperties.cs`
- `tests/ScoutCampPlanner.CateringTests/IngredientPropertyTests.cs`

Letzter geprüfter Stand:

- Catering-Build erfolgreich, keine Warnungen oder Fehler
- 87 Catering-Tests bestanden
- 4 Architekturtests bestanden
- `git diff --check` erfolgreich

Zugehörige Commits:

```text
ff827d5 feat: Revisionsmodell für Basiszutaten einführen
6fd2d5e feat: Eigenschaften und Varianten für Zutatenrevisionen ergänzen
```

## Nächste Arbeitsschritte

### 1. Fachlichen Auswertungsservice implementieren

Als nächstes einen providerunabhängigen Domain-Service implementieren, der Basiszutat und optional ausgewählte Variante auswertet.

Erforderliche Resultate:

- einzelnes Allergen
- einzelner Unverträglichkeitsauslöser
- vegan
- vegetarisch
- pescetarisch
- laktosefrei
- milchfrei
- glutenfrei

Regeln:

- `Contains` führt bei einem verbotenen Merkmal zu `Incompatible`.
- `MayContain` und `Unknown` führen zu `Unknown`.
- Fehlende Daten in einer ungeprüften Eigenschaftsgruppe führen zu `Unknown`.
- Varianten-Overrides haben Vorrang vor dem Revisionsbasiswert.
- `MILK` und `LACTOSE` bleiben getrennt.
- Laktosefreie Butter kann laktosefrei, aber weiterhin milchhaltig sein.
- Herkunft `UNKNOWN_ORIGIN` verhindert eine sichere positive Ernährungsbewertung.
- Allergen-Untertypen müssen auf ihre Obergruppe wirken.

Vorher die bestehenden Katalogklassen prüfen. Die aktuellen Typen `Allergen`, `Intolerance` und `DietaryRequirement` stammen noch aus dem alten Modell und besitzen noch keine stabilen Codes beziehungsweise Hierarchieinformationen. Bestehende Konstruktoren möglichst kompatibel halten, bis die Persistenzmigration erfolgt.

Notwendige Tests entsprechen mindestens den Punkten 17 bis 23 aus `.codex/BASISZUTATEN_IMPLEMENTIERUNG.md`.

### 2. Publish-Validierung ergänzen

Publish muss anschließend zusätzlich verhindern:

- widersprüchliche Eltern-/Kindzustände bei Allergenen
- doppelte `variant_key`-Werte
- ungültige oder fehlende Pflichtfelder
- Varianten, die nicht zur Revision gehören
- Änderungen an einer bereits veröffentlichten Revision

Reviewstatus für Allergene, Unverträglichkeiten und Herkunft berücksichtigen. Die konkrete Mindestanforderung für Publish aus den Domainregeln ableiten und mit Tests absichern.

### 3. Zentrale Updates und Drei-Wege-Merge

Danach implementieren:

- zuletzt berücksichtigte zentrale Revision je lokalem Stand
- Erkennung einer neueren zentralen veröffentlichten Revision
- fachlicher Diff für Name, Kategorie, Basiseinheit, Eigenschaften, Varianten und Umrechnungen
- Merge nicht überlappender Änderungen
- Konfliktresultat bei überlappenden Änderungen
- Merge erzeugt immer einen neuen lokalen Draft
- veröffentlichte lokale Revision wird niemals direkt verändert

Keine vereinfachte Zwei-Wege-Überschreibung verwenden.

### 4. Kategorien und revisionsgebundene Umrechnungen

Noch nicht im neuen Domain-Modell umgesetzt:

- Zutatenkategorien
- zutatenspezifische, revisionsgebundene Umrechnungen
- Genauigkeit `Exact`, `Average`, `Estimated`
- Varianten-Overrides für Umrechnungen

Die bestehenden Klassen `MeasurementUnit` und `IngredientUnitConversion` werden aktuell noch von Rezepten verwendet. Die Migration muss deshalb kompatibel und schrittweise erfolgen.

### 5. EF-Core-Persistenzmodell

Erst nach Stabilisierung der Domainregeln:

- `IngredientIdentity` konfigurieren
- `IngredientRevision` konfigurieren
- Eigenschaften und Reviewstatus konfigurieren
- Varianten und Overrides konfigurieren
- Row-Version als Concurrency Token konfigurieren
- transaktionalen Publish-Use-Case im Application Layer implementieren
- getrennte Migrationen für PostgreSQL und SQLite erzeugen

`docs/architecture/Basiszutaten_Schema.sql` ist nur ein PostgreSQL-Referenzschema. Es darf nicht direkt als Produktmigration übernommen werden.

### 6. Bestehende Daten migrieren

Die vorhandenen Tabellen enthalten bereits produktnahe Zutaten- und Rezeptdaten. Migration daher ohne Löschen oder Neuerzeugen bestehender Identitäten:

1. Neue Tabellen beziehungsweise Spalten anlegen.
2. Für jede bestehende `BaseIngredient` eine initiale veröffentlichte Revision erzeugen.
3. Bestehende Namen, Herkunft, Konfliktzuordnungen, Varianten und Umrechnungen übertragen.
4. Bestehende Rezeptreferenzen auf die erzeugte Revision umstellen.
5. Veröffentlichte Rezept-Snapshots nicht nachträglich verändern.
6. PostgreSQL- und SQLite-Upgradepfade testen.

Erst nach erfolgreicher Datenübernahme dürfen alte veränderliche Zutatenfelder und alte direkte Zuordnungstabellen entfernt werden.

### 7. Stammdaten-Seeding

Danach providerunabhängige Seeds für folgende Kataloge erstellen:

- 14 EU-Hauptallergene
- definierte Untertypen für glutenhaltiges Getreide und Schalenfrüchte
- initiale Unverträglichkeitsauslöser
- nichttierische, tierische und unbekannte Herkunftsmerkmale
- notwendige Einheiten und Dimensionen

Seeds benötigen stabile, zwischen PostgreSQL, SQLite und Lagerpaketen identische IDs und Codes.

### 8. Application/API und Editor umstellen

Erst nach Domain und Migration:

- explizites Draft-Speichern
- Publish-Aktion
- optimistische Konfliktmeldung
- Fork erst beim tatsächlichen lokalen Speichern
- Anzeige verfügbarer zentraler Updates
- Konfliktauflösung für Drei-Wege-Merge
- Auswahl der Eigenschaftszustände und Quellen
- Variantenerstellung und Overrides

Kein Auto-Save einführen.

### 9. Rezeptintegration separat durchführen

ADR-021 verlangt künftig:

- Referenz auf eine konkrete veröffentlichte Zutatenrevision
- optionaler `variant_key`

Die funktionale Umstellung des Rezepteditors ist ein eigener Auftrag. Bis dahin bestehende Rezeptfunktionen nicht unkontrolliert brechen. Zentral veröffentlichte Rezepte dürfen weiterhin nur zentrale Zutatenrevisionen referenzieren.

### 10. Offline-Pakete erweitern

Lagerpakete müssen die transitive, unveränderliche Datenmenge der enthaltenen Rezeptrevisionen übernehmen:

- Zutatenidentität
- konkrete veröffentlichte Zutatenrevision
- ausgewählte Variante
- Einheiten und Umrechnungen
- benötigte Allergen-, Unverträglichkeits- und Herkunftskatalogeinträge

Offline darf keine fehlende Stammdatenreferenz aus der Cloud nachladen müssen.

## Bekannte Übergangsrisiken

- Das alte `BaseIngredient`-Modell existiert parallel zum neuen `IngredientIdentity`-/`IngredientRevision`-Modell. Nicht voreilig entfernen.
- Alte Varianten hängen direkt an `BaseIngredient`; neue Varianten gehören zu einer Revision.
- Alte Konfliktzuordnungen sind reine Ja/Nein-Beziehungen; das neue Modell benötigt Zustand und Quelle.
- Direkte Zutatenzuordnungen zu `DietaryRequirement` sollen langfristig durch berechnete Eignung ersetzt werden.
- Bestehende Rezepte referenzieren noch keine Zutatenrevision und keinen `variant_key`.
- `Guid.NewGuid()` wird derzeit beim Kopieren von Varianten in einen neuen Draft verwendet. Vor Persistenzintegration prüfen, ob IDs durch den Application Layer bereitgestellt werden sollen, damit Erzeugung und Tests vollständig deterministisch bleiben.
- Der vollständige Drei-Wege-Merge und `merged_central_revision_id` sind noch nicht implementiert.
- Die allgemeinen und zutatenspezifischen Umrechnungen sind noch nicht in das neue Revisionsmodell überführt.

## Prüfungen nach jedem Inkrement

Mindestens ausführen:

```powershell
dotnet build tests/ScoutCampPlanner.CateringTests/ScoutCampPlanner.CateringTests.csproj
dotnet test tests/ScoutCampPlanner.CateringTests/ScoutCampPlanner.CateringTests.csproj --no-build
dotnet test tests/ScoutCampPlanner.ArchitectureTests/ScoutCampPlanner.ArchitectureTests.csproj --no-build
git diff --check
```

Falls `dotnet test` während des gleichzeitigen Build-Schritts ohne Ausgabe hängen bleibt, Build und Test getrennt wie oben ausführen. Dieses Verhalten trat in der aktuellen Umgebung sporadisch auf; die getrennten Läufe waren erfolgreich.

Vor einem Persistenz-Inkrement zusätzlich ausführen:

- PostgreSQL-Migrationstests
- SQLite-Migrationstests
- Package-Kompatibilitätstests
- vollständiger Solution-Build
