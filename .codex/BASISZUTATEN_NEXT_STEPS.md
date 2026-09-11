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
- `docs/decisions/adr-022-central-ingredient-contributions.md`
- `docs/decisions/adr-023-ingredient-variant-selection.md`

Die Repository-Dokumentation hat Vorrang vor dieser Übergabe und vor früheren Chatverläufen.

## Aktueller Implementierungsstand

### Aktuelles Integrationsinkrement, noch zu committen

- Veröffentlichte Mandanten- und Lagerrevisionen können direkt zur zentralen
  Prüfung eingereicht werden.
- Eine Revision kann nur einmal eingereicht werden; der lokale veröffentlichte
  Stand bleibt unverändert.
- Plattform-Administratoren sehen offene Einreichungen, können sie ablehnen,
  als neue zentrale Zutat annehmen oder einer vorhandenen zentralen Identität
  zuordnen.
- Annahme erzeugt immer einen zentralen Draft und setzt Allergene,
  Unverträglichkeiten und Herkunft auf `Unreviewed` zurück.
- Vor lokaler Veröffentlichung werden zentrale Treffer mit gleichem
  normalisiertem Namen angeboten.
- Eine lokale Zutat kann kontrolliert durch einen zentralen Treffer abgelöst
  werden. Die lokale Identität wird archiviert; historische Rezeptrevisionen
  bleiben unverändert.
- Bei Veröffentlichung eines angenommenen zentralen Entwurfs wird die
  unveränderte lokale Ausgangsidentität automatisch abgelöst. Neuere lokale
  Revisionen oder Entwürfe verhindern diese automatische Archivierung.
- SQLite- und PostgreSQL-Migrationen sowie Persistenztests sind enthalten.
- Noch offen bleibt der Transport revisionsfähiger Zutaten im Lagerpaket.

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

### Derzeit im Working Tree, noch zu committen

Das dritte Domain-Inkrement enthält:

- `IngredientPropertyDefinition` mit stabilem Code und optionaler Hierarchie
- `IngredientSuitabilityEvaluator`
- Auswertung einzelner Allergene und Unverträglichkeitsauslöser
- Auswertung von vegan, vegetarisch und pescetarisch
- Auswertung von laktosefrei, milchfrei und glutenfrei
- Berücksichtigung von Varianten-Overrides
- Vererbung von Allergen-Untertypen auf ihre Obergruppe
- sichere Behandlung ungeprüfter und unbekannter Angaben
- Tests für Milch/Laktose, tierische Herkunft, unbekannte Herkunft, Glutenhierarchie und `MayContain`
- `IngredientRevisionPublicationValidator`
- verpflichtenden Review aller drei Eigenschaftsgruppen vor Publish
- Prüfung hierarchischer Allergenwidersprüche für Basiswerte und Varianten
- Publish bleibt bei Validierungsfehlern vollständig im Draft-Zustand

Letzter geprüfter Stand dieses Inkrements:

- Catering-Build erfolgreich, keine Warnungen oder Fehler
- 100 Catering-Tests bestanden

Empfohlene Commit-Message:

```text
feat: Eignungsprüfung und Publish-Validierung für Basiszutaten ergänzen
```

## Nächste Arbeitsschritte

### Herkunftseingabe im Revisionseditor

Die Herkunftseingabe wurde fachlich vereinfacht:

- genau eine Hauptherkunft je Basiszutat
- `UNKNOWN_ORIGIN` als Ausgangswert neuer, noch ungeklärter Zutaten
- automatische Ableitung der nicht ausgewählten Hauptherkünfte als `does_not_contain`
- separate, kombinierbare Angaben für tierisches Fett, Gelatine, tierisches Lab
  und sonstige tierische Bestandteile
- fehlende Zusatzmerkmale werden erst bei ausdrücklich vollständiger Prüfung
  als `does_not_contain` abgeleitet

Die Persistenz bleibt beim bestehenden flexiblen Herkunftsmerkmalsmodell; die
exklusive Hauptherkunft ist eine fachliche Editorregel.

### 1. Fachlichen Auswertungsservice implementieren – umgesetzt

Der providerunabhängige Domain-Service ist im dritten Domain-Inkrement umgesetzt.

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

### 2. Publish-Validierung ergänzen – umgesetzt

Publish verhindert nun:

- widersprüchliche Eltern-/Kindzustände bei Allergenen
- doppelte `variant_key`-Werte
- ungültige oder fehlende Pflichtfelder
- Varianten, die nicht zur Revision gehören
- Änderungen an einer bereits veröffentlichten Revision

Alle drei Eigenschaftsgruppen müssen vor Publish als `Reviewed` markiert sein. Die Domain-Tests decken ungeprüfte Revisionen sowie Widersprüche der Basiswerte und effektiven Variantenwerte ab.

### 3. Zentrale Updates und Drei-Wege-Merge

Teilweise umgesetzt:

- zuletzt berücksichtigte zentrale Revision je lokalem Stand über `MergedCentralRevisionId`
- Erkennung einer neueren zentralen veröffentlichten Revision
- Drei-Wege-Vergleich für Name, Kategorie, Basiseinheit, Reviewstatus, Eigenschaften und Varianten
- Merge nicht überlappender Änderungen in einen neuen lokalen Draft
- Konfliktpfade bei überlappenden Änderungen
- veröffentlichte lokale Revision wird nicht verändert

Noch offen:

- Umrechnungen in Diff und Merge aufnehmen, sobald sie revisionsgebunden modelliert sind
- Workflow für einen bereits vorhandenen lokalen Draft festlegen; aktuell wird nur von einer veröffentlichten lokalen Revision in einen neuen Draft gemerged
- Varianten werden derzeit auf Ebene des gesamten `variant_key` verglichen; bei Bedarf später feinere Konfliktpfade für Name und einzelne Overrides ergänzen

Keine vereinfachte Zwei-Wege-Überschreibung verwenden.

### 4. Kategorien und revisionsgebundene Umrechnungen

Im Domain-Modell umgesetzt:

- Zutatenkategorien mit stabilem Code und optionaler Elternkategorie
- zutatenspezifische, revisionsgebundene Umrechnungen
- Genauigkeit `Exact`, `Average`, `Estimated`
- Varianten-Overrides und effektive Vererbung für Umrechnungen
- Einbeziehung der Umrechnungen in Drei-Wege-Merge und Konflikterkennung

Die bestehenden Klassen `MeasurementUnit` und `IngredientUnitConversion` werden aktuell noch von Rezepten verwendet. Die Migration muss deshalb kompatibel und schrittweise erfolgen.

### 5. EF-Core-Persistenzmodell

Providerunabhängiges Mapping umgesetzt:

- Infrastructure-Records für Zutatenidentitäten und Revisionen
- Kategorien und Eigenschaftskataloge
- Eigenschaften und Reviewstatus
- Varianten und Overrides
- revisionsgebundene Umrechnungen
- Row-Version als Concurrency Token
- Datenbankregel für höchstens einen Draft je Zutatenidentität
- SQLite-Roundtrip-Test des vollständigen Graphen

Noch offen:

- unveränderliche Published-Graphen zusätzlich auf Persistenzebene schützen

Der transaktionale Revisionsworkflow ist umgesetzt:

- explizites Speichern eines Drafts ohne Auto-Save
- gemeinsame Domain-Normalisierung des Draft-Inhalts
- Scope-basierte Autorisierung für zentrale, Mandanten- und Lagerzutaten
- optimistische Versionsprüfung über `RowVersion`
- unterscheidbare Ergebnisse für fehlende Revisionen, Published-Stände,
  Versionskonflikte, fehlende Berechtigung und ungültige Inhalte
- Publish-Validierung anhand des persistierten Revisionsgraphen
- atomare Veröffentlichung von Revision und
  `CurrentPublishedRevisionId` innerhalb einer Datenbanktransaktion
- SQLite-Integrationstests für Speichern, Versionskonflikt,
  Veröffentlichung und Rollback bei ungültigem Publish
- vollständige Ladeprojektion des Revisionsgraphen einschließlich Eigenschaften,
  Umrechnungen, Varianten und Overrides
- authentifizierte REST-Endpunkte zum Laden, Speichern und Veröffentlichen
- HTTP-409-Antworten mit aktuellem Versionsstand bei Konflikten
- transaktionales Anlegen neuer zentraler, Mandanten- und Lagerzutaten
  als Revision-1-Draft mit ungeprüften Eigenschaftsgruppen
- REST-Endpunkte zum Anlegen der drei Scope-Varianten

`docs/architecture/Basiszutaten_Schema.sql` ist nur ein PostgreSQL-Referenzschema. Es darf nicht direkt als Produktmigration übernommen werden.

### 6. Bestehende Daten migrieren

Die vorhandenen Tabellen enthalten bereits produktnahe Zutaten- und Rezeptdaten. Migration daher ohne Löschen oder Neuerzeugen bestehender Identitäten:

1. Neue Tabellen beziehungsweise Spalten anlegen.
2. Für jede bestehende `BaseIngredient` eine initiale veröffentlichte Revision erzeugen.
3. Bestehende Namen, Herkunft, Konfliktzuordnungen, Varianten und Umrechnungen übertragen.
4. Bestehende Rezeptreferenzen auf die erzeugte Revision umstellen.
5. Veröffentlichte Rezept-Snapshots nicht nachträglich verändern.
6. PostgreSQL- und SQLite-Upgradepfade testen.

Die Migrationen `AddRevisionedIngredients` für SQLite und PostgreSQL sind erstellt. Die Übergangslogik:

- erhält die IDs bestehender Basiszutaten und verwendet sie auch für die initiale Revision,
- übernimmt Name und Scope,
- übernimmt Varianten mit einem stabil abgeleiteten Legacy-`variant_key`,
- übernimmt vorhandene Allergen- und Unverträglichkeitszuordnungen als positive, abgeleitete Angaben,
- wählt eine vorhandene Umrechnungseinheit als Basiseinheit oder verwendet eine explizite Legacy-Platzhaltereinheit,
- markiert Eigenschaftsgruppen als ungeprüft und Herkunft als unbekannt,
- belässt die alten Tabellen vorerst für bestehende Rezeptfunktionen im Schema.

Die Upgrade- und Datenerhaltungstests sind für SQLite und PostgreSQL erfolgreich.

Erst nach erfolgreicher Datenübernahme auf beiden Providern und Umstellung aller Leser dürfen alte veränderliche Zutatenfelder und direkte Zuordnungstabellen entfernt werden.

### 7. Stammdaten-Seeding – Eigenschaftskataloge umgesetzt

Provideridentische Seeds sind umgesetzt für:

- 14 EU-Hauptallergene
- definierte Untertypen für glutenhaltiges Getreide und Schalenfrüchte
- initiale Unverträglichkeitsauslöser
- nichttierische, tierische und unbekannte Herkunftsmerkmale
- stabile IDs und Codes in PostgreSQL, SQLite und neuen `EnsureCreated`-Testdatenbanken

Die 14 EU-Hauptallergene, die dokumentierten Getreide- und
Schalenfrucht-Untertypen, 10 Unverträglichkeitsauslöser sowie 19
Herkunftsmerkmale werden durch Migrationen angelegt. Die bereits von der
Kompatibilitätsmigration erzeugte `UNKNOWN_ORIGIN`-Identität wird dabei
weiterverwendet.

Zusätzlich stellt `/api/ingredient-reference-data` die aktiven Kataloge,
vorhandenen Kategorien und Maßeinheiten für den Editor bereit. Ein initialer,
flacher Grundkatalog mit 18 Zutatenkategorien und stabilen IDs/Codes wird für
SQLite und PostgreSQL angelegt. Die optionale Elternbeziehung bleibt für eine
spätere Hierarchisierung erhalten.

Noch offen:

- fachliche Festlegung, welche Unverträglichkeitsauslöser als
  mengenabhängig markiert werden; bis dahin bleibt der dokumentierte
  Schema-Standard `false`

Seeds benötigen stabile, zwischen PostgreSQL, SQLite und Lagerpaketen identische IDs und Codes.

### 8. Application/API und Editor umstellen

Erst nach Domain und Migration:

- explizites Draft-Speichern – Application-, Persistenz- und erster
  Lagereditor-Workflow umgesetzt
- Publish-Aktion – Application-, Persistenz-, API- und erster
  Lagereditor-Workflow umgesetzt
- optimistische Konfliktmeldung – technischer Ergebnisstatus, API-Mapping
  und Neuladen mit UI-Hinweis umgesetzt
- Anlegen, Auflisten und Wiederöffnen revisionsfähiger Lagerzutaten – umgesetzt
- Auswahl von Name, Kategorie und Basiseinheit – umgesetzt
- explizite Bestätigung der drei fachlichen Eigenschaftsgruppen – umgesetzt
- Schutz vor Veröffentlichung noch nicht gespeicherter UI-Änderungen – umgesetzt
- Auswahl und transaktionales Speichern konkreter Allergen-, Unverträglichkeits-
  und Herkunftszustände – umgesetzt
- manuelle Änderungen werden mit Quelle `ManuallyVerified` gespeichert und
  setzen die betroffene Gruppe wieder auf `Unreviewed` – umgesetzt
- Allergene werden im Lagereditor über die 14 österreichischen Hauptgruppen
  `A` bis `R` erfasst; Gluten- und Schalenfrucht-Untertypen erscheinen nur bei
  `Contains` als eigene Detailauswahl – umgesetzt
- bei anderen Zuständen übernehmen die Untertypen den Hauptgruppenzustand mit
  Quelle `Derived`; bei `Contains` starten noch unbestimmte Details als
  `Unknown` – umgesetzt
- häufige Unverträglichkeiten werden direkt, weitere FODMAP-bezogene Einträge
  in einem Detailbereich angezeigt – umgesetzt
- Gluten wird nicht mehr doppelt als Unverträglichkeit erfasst; Glutenfreiheit
  wird ausschließlich aus Allergen A und dessen Untertypen berechnet – umgesetzt
- Laktose, Fruktose und Histamin starten als `Unknown`; fehlende erweiterte
  Unverträglichkeiten werden erst bei bestätigtem Review als `DoesNotContain`
  mit Quelle `Derived` ergänzt – umgesetzt
- revisionsgebundene Umrechnungen im Editor inklusive Basiseinheit,
  Faktor und Genauigkeit – umgesetzt
- Standard-Einheiten `g`, `kg`, `ml`, `l`, `Stk.`, `TL`, `EL`, `Prise`
  und `Bund` werden per Migration bereitgestellt – umgesetzt
- Folgedraft aus einer veröffentlichten Revision mit vollständiger Kopie der
  Eigenschaften, Umrechnungen, Varianten und Overrides – umgesetzt
- Varianten im Editor anlegen, umbenennen sowie aktiv/inaktiv setzen – umgesetzt
- stabile `variant_key`-Werte werden beim erstmaligen Anlegen automatisch
  erzeugt und können nach dem Speichern nicht mehr verändert werden – umgesetzt
- eine manuelle Variantenreihenfolge ist fachlich nicht erforderlich; die
  Anlagereihenfolge bleibt lediglich als technische Sortierung erhalten
- Eigenschafts-Overrides von Varianten mit expliziter Vererbung über
  „Wie Basis“ und den vereinfachten Allergen-, Unverträglichkeits- und
  Herkunftsregeln – umgesetzt
- Einheiten-Overrides von Varianten für bereits auf der Basisrevision
  vorhandene Umrechnungen – umgesetzt
- Lager-Fork einer zentralen Zutat erst beim ersten tatsächlich veränderten
  Speichern; reine Vorschau erzeugt keine lokale Kopie – umgesetzt
- Anzeige verfügbarer zentraler Updates
- Konfliktauflösung für Drei-Wege-Merge

Kein Auto-Save einführen.

### 9. Rezeptintegration schrittweise fortführen

Umgesetzt:

- Rezeptpositionen referenzieren eine konkrete veröffentlichte
  Zutatenrevision statt der alten `BaseIngredient`-Identität.
- Bestehende Daten bleiben erhalten, weil die initial erzeugte Revision dieselbe
  ID wie die bisher referenzierte Basiszutat besitzt.
- SQLite und PostgreSQL besitzen provider-spezifische Migrationen für die
  geänderten Fremdschlüssel.
- Der erste Lager-Rezepteditor kann Entwürfe anlegen, öffnen und explizit
  speichern sowie veröffentlichte Zutaten mit Menge und Einheit hinzufügen.
- Die Zutatensuche priorisiert Lager, danach Mandant und zuletzt den zentralen
  Katalog.
- Rezeptpositionen wählen gemäß ADR-023 keine Variante. Die konkrete Auswahl
  aus der referenzierten Zutatenrevision gehört in die spätere
  Verpflegungsplanung.
- Innerhalb einer Rezeptgruppe darf dieselbe Zutatenrevision nur einmal
  vorkommen.

Noch offen:

- Ersatzregeln und Unterrezepte in der Oberfläche
- Publikationsworkflow und Validierungsanzeige im Rezepteditor
- Aktualisierung eines Entwurfs auf eine neuere Zutatenrevision als bewusster
  Benutzerschritt
- Auswahl einer geeigneten Zutatenvariante pro Verpflegungs- oder Kocheinheit
  unter Berücksichtigung ihrer effektiven Konflikte und Umrechnungen

Zentral veröffentlichte Rezepte dürfen weiterhin nur zentrale
Zutatenrevisionen referenzieren.

### 10. Offline-Pakete erweitern

Lagerpakete müssen die transitive, unveränderliche Datenmenge der enthaltenen Rezeptrevisionen übernehmen:

- Zutatenidentität
- konkrete veröffentlichte Zutatenrevision
- alle Varianten und Overrides der referenzierten Zutatenrevision
- Einheiten und Umrechnungen
- benötigte Allergen-, Unverträglichkeits- und Herkunftskatalogeinträge

Offline darf keine fehlende Stammdatenreferenz aus der Cloud nachladen müssen.

## Bekannte Übergangsrisiken

- Das alte `BaseIngredient`-Modell existiert parallel zum neuen `IngredientIdentity`-/`IngredientRevision`-Modell. Nicht voreilig entfernen.
- Alte Varianten hängen direkt an `BaseIngredient`; neue Varianten gehören zu einer Revision.
- Alte Konfliktzuordnungen sind reine Ja/Nein-Beziehungen; das neue Modell benötigt Zustand und Quelle.
- Direkte Zutatenzuordnungen zu `DietaryRequirement` sollen langfristig durch berechnete Eignung ersetzt werden.
- Rezeptpositionen referenzieren Zutatenrevisionen, aber keine Variante. Die
  Variantenauswahl ist gemäß ADR-023 Aufgabe der späteren Verpflegungsplanung.
- `Guid.NewGuid()` wird derzeit beim Kopieren von Varianten in einen neuen Draft verwendet. Vor Persistenzintegration prüfen, ob IDs durch den Application Layer bereitgestellt werden sollen, damit Erzeugung und Tests vollständig deterministisch bleiben.
- Der vollständige Drei-Wege-Merge und `merged_central_revision_id` sind noch nicht implementiert.
- Allgemeine Einheiten, revisionsgebundene Zutatenumrechnungen sowie
  Eigenschafts- und Einheiten-Overrides von Varianten sind in das neue
  Revisionsmodell und den Editor überführt.

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
