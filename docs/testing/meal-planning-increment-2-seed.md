# Development-Testdaten: Mahlzeitenplanung Inkrement 2

## Status: READY

Stand 09.10.2026. Der bestehende Snapshot-Codec konstruiert `ConflictReference`
jetzt über einen lokalen JSON-Converter: Typ und ID bleiben erhalten, fehlende
oder ungültige Referenzen werden abgewiesen. JSON-Format, Schema und Fachregeln
unverändert; keine Migration oder neue Dependency. Bereits mit leeren IDs
gespeicherte defekte Snapshots werden nicht automatisch umgeschrieben.

- Seed-Erzeugung und vollständige Fixture-Prüfung: **PASS**. 32 Teilnehmer,
  16 veröffentlichte Rezepte, 18 Mahlzeiten, 6 Küchen, 108 berechnete Teilstände.
  Die zusätzlichen drei Vergleichsküchen werden einschließlich Lösungsanzahl
  und Ask-Entscheidungsproblem automatisch geprüft.
- Idempotenz: **PASS**, Aktivierungsdatei und Datenbankhash bleiben identisch.
- Reset: **PASS**, neue geprüfte Generation aktiviert, vorherige Datenbank per
  Hash nachweislich unverändert. Vorhandene API-SQLite-Datenbanken und Backups
  ebenfalls per Vorher-/Nachher-Hash unverändert. Kein Produktionsserver adressiert.
- Testlogin: **PASS** im ursprünglichen 20-Personen-Datensatz, echtes HTTP-Login
  sowie Tenant-/Camp-Zugriff und Abruf seiner 54 Planungsstände. Login unverändert.
- Production-Sperre: **PASS**. Kein Commit. Fallzuordnung unverändert.
- Codec-Regressionen: **8 PASS**, einschließlich Allergenen, Stoffen, DietTypes,
  aller Snapshot-Referenzpositionen, ungültiger Referenzen und regulär
  veröffentlichter Varianten-/Ersatzrezepte. Jede Seed-Rezeptveröffentlichung
  prüft zusätzlich vollständiges Serialize → Deserialize → Serialize.
- Solution-Build: **PASS**, 0 Warnungen/Fehler; `git diff --check`: **PASS**.
- Manuelle UI-/Desktop-Endabnahme: **NOT_RUN**, jetzt mit dem Seed durchführbar.

Die aktuelle Generation steht in `.dev-data/meal-planning-increment-2/current.json`.
Bestehende manuelle Testdaten wurden nicht ersetzt. Für die neuen Vergleichsküchen
nach Beenden des Testbackends ausdrücklich `Reset` ausführen; `Seed` bleibt No-op.
Alte und unaktivierte Versuchsdateien wurden nicht gelöscht.

## Start und Reset

Windows, .NET SDK und Repository erforderlich. Im Repository-Terminal:

```powershell
./tools/dev-meal-planning.ps1 Seed
./tools/dev-meal-planning.ps1 Start
```

Das zweite Kommando startet das normale Backend im Vordergrund auf `localhost:5180`.
Ein bereits laufendes Backend auf diesem Port vorher in dessen Terminal beenden;
das Skript beendet keine fremden Prozesse. Das vorhandene Angular-Frontend wie
gewohnt starten und **abmelden/neu anmelden**, damit keine alte Sitzung verwendet wird.
Alternativ `Start -Port 5182`; dann muss auch das Frontend diesen API-Port verwenden.

Login ausschließlich für diese isolierte Testinstanz:
`acceptance@example.test` / `TEST-only-MealPlanning-2026!`.
Der Dummy-Administrator hat explizite Health-/Verify-Rechte. Diese Rechte werden
weiterhin nicht ins Offlinepaket übertragen; lokale Freigabe wie bisher separat.

Reset: Testbackend mit Strg+C beenden, dann:

```powershell
./tools/dev-meal-planning.ps1 Reset
./tools/dev-meal-planning.ps1 Start
```

`Seed` mit vorhandenem Datensatz ist ein No-op. `Reset` erzeugt eine neue Generation
mit den aktuellen fachlichen Fixtures; der bisherige Stand wird **nicht gelöscht**.
Veröffentlichte Revisions-IDs, automatisch erzeugte IDs und Passwort-Salts können
zwischen Generationen variieren. Daten und Erwartungsfälle sind reproduzierbar.

## Sicherheitsgrenze

- Separates Werkzeug unter `tools/`, nicht Teil des API-/Desktop-Publishs.
- Explizit nur `Development` oder `Test`; Production/Staging wird abgewiesen.
- Keine frei wählbare ConnectionString und kein Zugriff auf vorhandene Serverdatenbanken.
- Ausschließlich SQLite-Generationen unter `.dev-data/meal-planning-increment-2/`.
- `current.json` wird erst nach erfolgreicher Erstellung und Fixture-Prüfung ersetzt.
- Fehlgeschlagene Generationen bleiben unaktiviert; ältere Generationen bleiben erhalten.
- Normale Migrationen, Domain-Objekte, Ingredient-Publish, Recipe-Publish und
  MealPlanningService; keine neue Fachlogik, kein SQL-Seed gegen Produktivdaten.
- Die isolierte Instanz bindet nur localhost. Keine echten Daten dort erfassen und
  keine TEST-Pakete in ein echtes Lager importieren. Reset ist kein Cloud-Unlock.
- Sämtliche Stoffmengen und Teilnehmergrenzen sind synthetische Testzahlen,
  keine medizinischen oder lebensmittelkundlichen Empfehlungen.

## Datensatz

`TEST Versorgung - nur Dummy-Daten`, 01.–06.07.2027. Zwei ursprüngliche Blattgruppen
mit jeweils zehn Teilnehmern und drei Vergleichsgruppen mit je vier Teilnehmern
unter einem gemeinsamen Strukturknoten, insgesamt 32.

Je Blattgruppe: zwei Standardpersonen, zwei mit Milchallergie, eine mit zentralem
TEST-Pflanzen-DietType, eine mit Tenant-DietType Pflanze/Milch, zwei mit synthetischem
Laktosegrenzwert 0,5 g, eine mit 2 g und eine mit Fruktoseanforderung ohne Schwelle.
Identische Profile über verschiedene Personen/Knoten; unterschiedliche Schwellen
trennen Gruppen. Personen 02/12 fehlen am 02.07., Personen 04/14 beim Frühstück
am 03.07. Die Grenzwerte sind nicht im globalen Stoffkatalog als Default eingetragen.

Küchen: Gruppe A → `All / AlwaysTogether`; Gruppe B → komplementär
`SpecialCateringOnly / Ask` und `WithoutSpecialCatering / NeverTogether`.
Kein überlappender All-Filter auf derselben Gruppe. Reale Bedarfsbasis aktiviert.
Zusätzlich drei getrennte Küchen `TEST Vergleich AlwaysTogether / Ask / NeverTogether`
mit identischen Profilen: je zwei Standardpersonen und zwei Milchallergiker.
Am **01.07., Mittagessen**, Rezept 03: AlwaysTogether liefert eine gemeinsame
milchfreie Variante für vier Personen; Ask zeigt zunächst nur die Entscheidung
„Gemeinsam versorgen“ / „Getrennt versorgen“, noch keine aktiven Lösungen.
Nach Auswahl, Speichern und Neuberechnen existiert ausschließlich die gewählte
Versorgung (eine gemeinsame oder zwei getrennte manuelle Lösungen). Beide
Wechselrichtungen auch nach Reload prüfen; insgesamt vier Portionen, keine
überlappenden Zuordnungen. NeverTogether liefert zwei getrennte Lösungen
ohne diese Frage. Die Strukturgruppen überlappen nicht.

Zutaten: konfliktfreie Pflanze, zweite Ersatzpflanze, Milchallergen mit/ohne
Varianten, quantitative Laktose, qualitative Präsenz/Abwesenheit, bewusst unbekannte
Stoffdaten sowie Fischherkunft. Variante `free` entfernt nur das Milchallergen;
`inherit` erbt die Basis. Herkunft und Stoffe werden dadurch nicht heimlich verändert.

## Fallzuordnung im TEST-Mahlzeitenplan

Die Resolver-Erwartungen zu Milch gelten für die **Milchallergie-Gruppe**, nicht
pauschal für alle DietTypes oder für eine erzwungene gemeinsame Versorgung.
Die übrigen Gruppen dienen gezielt zum Vergleich. Alle Rezepte sind veröffentlicht.

| Tag | Frühstück | Mittagessen | Abendessen |
|---|---|---|---|
| 01.07. | 01 Standard konfliktfrei | 03 eindeutige Variante | 04 eindeutiger Ersatz |
| 02.07. | 05 zwei unabhängige Änderungen | 06 Variante **oder** Ersatz (mehrdeutig) | 07 nur partielle Lösung |
| 03.07. | 08 mit genau einer passenden OfferGroup-Alternative | 08 mit zwei passenden Alternativen | 10 verschachteltes Subrecipe |
| 04.07. | 11 dasselbe verschachtelte Subrecipe zweimal | 12 Subrecipe-Ersatz | 13 Fixed-/Stepwise-Mengen |
| 05.07. | 14 qualitativ enthalten | 15 Unknown | 16 quantitative lineare Menge |
| 06.07. | 08 ohne Alternative, keine Lösung | 01 konfliktfrei für Verifikation | NoSupplyRequired |

Recipe 09 ist das Teilrezept mit Variante, Recipe 02 die zweite konfliktfreie
Alternative. Wiederholte Positionen stehen in separaten Rezeptgruppen, wie von
der bestehenden Veröffentlichung gefordert. Fall 06 nutzt Variante plus Ersatz,
keine unzulässigen doppelten Ersatzregeln für denselben Konflikt.

Für Coverage/Manual-Abnahme: eine vorgeschlagene Lösung übernehmen, Portionen bzw.
Allokationen absichtlich unter-/überdecken oder Zuordnung entfernen. Speichern,
Reload und Recalc prüfen. Keine manuellen Entscheidungen oder Verifikationen
werden vorweggenommen; sie sollen in der Abnahme bewusst erfolgen.

Für Invalidierung: Teilnehmerprofil/Anwesenheit oder Versorgung ändern, relevante
und nicht betroffene Slots vergleichen. 06.07. Abend liefert NoSupplyRequired.
Für Offline: das TEST-Lager über den normalen Paketworkflow aus dieser isolierten
Cloud-artigen Instanz exportieren und das Rückpaket wieder **hier** importieren.
Dies ist ein SQLite-Dev-Seed, kein zusätzlicher PostgreSQL-Provider-Nachweis.

## Prüfung

Jede frische Generation prüft veröffentlichte Resolver-Fälle, quantitative
Schwellenunterscheidung, Unknown und den vollständigen operativen Datenumfang.
Die manuelle UI-/Desktop-Endabnahme bleibt eine eigene Prüfung.

Zusätzlicher isolierter Integrationstest (aktiviert keine neue Generation):

```powershell
$env:DOTNET_ENVIRONMENT = 'Development'
dotnet run --project tools/ScoutCampPlanner.MealPlanningSeed -- --probe
```

**PASS:** frisches Paket → frische SQLite-DB → manuelle Auswahl mit gemischter
Auto-/Manual-Herkunft → Reload/Recalc → Clean-Verifikation eines konfliktfreien
Falls trotz Stoffanforderung ohne Grenzwert → Return → Cloud-artiger SQLite-Replace.
Mehrstufige Struktur und vollständige Supply-/Verification-JSONs bleiben erhalten;
keine Permission-Grants importiert. Keine bestehenden Datenbanken verändert.

Fixed/Stepwise: 04.07. Frühstück, Rezept 13. Berechnete Zutatenmengen und Mengenregel
stehen an der Lösung (letzter Berechnungsstand). Parameter im Rezepteditor ändern,
speichern, neue Revision veröffentlichen, im Plan auswählen und neu berechnen.
Veröffentlichte Revisionen werden nicht nachträglich geändert.

Desktop: Die zuvor installierte App vom 07.10. lehnt aktuelle Pakete ab. Für die
erneute manuelle Prüfung den aktuellen Installer verwenden; nicht die lokale DB
löschen oder Paketversionen heruntersetzen. `tools/test-seed-sidecar.ps1 -Package
<Datei>` prüft ein Paket gegen eine eigene frische Sidecar-DB, ohne Nutzerdaten zu ändern.
