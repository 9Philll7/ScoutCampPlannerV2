# Basiszutaten – Datenbankarchitektur

## Scope

Dieses Schema modelliert ausschließlich:

- Zutatenidentitäten
- zentrale und lokale Zutaten
- Revisionen und Veröffentlichung
- Zutatenkategorien
- Einheiten
- zutatenspezifische Umrechnungen
- Allergene
- Unverträglichkeitsauslöser
- quantitative Gehalte unverträglichkeitsrelevanter Stoffe
- Herkunftsmerkmale
- Zutatenvarianten und Overrides
- revisionsgebundene Nährwertprofile
- Herkunft lokaler Forks und Update-Basis

Nicht enthalten:

- Rezepte
- Lager
- Einkaufsartikel
- Teilnehmer
- Kocheinheiten

## Kernmodell

```text
ingredient
└── ingredient_revision
    ├── ingredient_revision_allergen
    ├── ingredient_revision_intolerance
    ├── ingredient_revision_substance_content
    ├── ingredient_revision_origin
    ├── ingredient_revision_unit_conversion
    ├── ingredient_revision_nutrition_profile
    └── ingredient_variant_revision
        ├── ingredient_variant_allergen_override
        ├── ingredient_variant_intolerance_override
        ├── ingredient_variant_substance_content_override
        ├── ingredient_variant_origin_override
        ├── ingredient_variant_unit_conversion_override
        └── ingredient_variant_nutrition_profile
```

Persistierte Scopes sind `central`, `tenant` und `camp`. Eine Tenant- oder Lagerzutatenidentität kann über `source_ingredient_id` und `source_revision_id` auf ihren zentralen Ursprung verweisen. `local` ist nur ein fachlicher Sammelbegriff.

Rezepte referenzieren die ID einer konkreten veröffentlichten Zutatenrevision,
aber keine einzelne Variante. Der stabile `variant_key` identifiziert Varianten
innerhalb der Zutatenrevision für die spätere Verpflegungsplanung.
Eigenschaftsgruppen besitzen einen Reviewstatus, damit fehlende Angaben nicht
versehentlich als unbedenklich ausgewertet werden.

`ingredient_revision_intolerance` bleibt für qualitative Angaben wie Histamin
und für bestehende Legacy-Daten erhalten. Dosisabhängige Stoffe werden gemäß
[ADR-026](../decisions/adr-026-quantitative-intolerance-substances.md) in
`IngredientRevisionSubstanceContents` gespeichert. Menge und Bezugsmenge nutzen
`decimal(18,6)`; Mengen- und Bezugseinheit verweisen auf den Einheitenkatalog.
Die Varianten-Tabelle speichert jeweils einen vollständigen Ersatzwert.

## Nährwertpersistenz

ADR-024 ergänzt je Zutatenrevision höchstens ein optionales Nährwertprofil und
je Variante höchstens ein vollständiges Ersatzprofil. Das Modell wird in den
Tabellen `IngredientRevisionNutritionProfiles` und
`IngredientVariantNutritionProfiles` gespeichert. Getrennte EF-Core-Migrationen
für PostgreSQL und SQLite liegen vor.

Das Revisionsprofil enthält Bezugsmenge, Referenzeinheit, Energie in kJ, die
sechs verpflichtenden Mengenfelder des ersten Umfangs, optionale
Ballaststoffe und Prüfstatus. Quellen werden gemäß ADR-025 revisionsweit
zusammengefasst und nicht fachlich an jedem einzelnen Zahlenfeld geführt. Das
Variantenprofil besitzt dieselbe fachliche Struktur. Einzelne Variantenfelder
werden nicht als Overrides persistiert.

Externe Referenzdaten sind kein Bestandteil dieser Tabellen. Die vollständige
BLS-Liste beziehungsweise ein kompakter Suchindex wird read-only außerhalb der
fachlichen Produktdatenbank bereitgestellt. Open Food Facts wird ausschließlich
über einen optionalen Infrastructure-Adapter abgefragt. Aus beiden Quellen
werden nur bewusst übernommene Schätzwerte und ihre revisionsweite
Quellenzusammenfassung persistiert.

Der lokale Index wird bei Bedarf aus der offiziellen BLS-4.0-CSV erzeugt:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tools\New-BlsSuggestionIndex.ps1 `
  -CsvPath "C:\Pfad\BLS_4_0_Daten_2025_DE.csv"
```

Das Ergebnis liegt standardmäßig unter
`src/backend/ScoutCampPlanner.Api/reference-data/bls-4.0-suggestions.json` und
ist bewusst von Git ausgeschlossen. Der API-Pfad ist über
`IngredientSuggestions:BlsIndexPath` konfigurierbar. Fehlt der Index, bleibt
der Zutateneditor vollständig nutzbar und meldet nur die BLS-Suche als nicht
eingerichtet. Der aktuelle Index enthält 7.140 BLS-Referenzeinträge; diese Zahl
ist keine Anzahl von ScoutCampPlanner-Zutaten.

Die bestehenden `SourceType`- und `SourceReference`-Spalten der Nährwert- und
Stoffgehaltstabellen bleiben vorerst als kompatible technische Spiegel erhalten.
Der Editor pflegt sie nicht mehr einzeln; beim Speichern werden sie aus der
revisionsweiten Zusammenfassung abgeleitet. Ihre spätere Entfernung erfolgt erst
mit einem eigenen, aufwärtskompatiblen Bereinigungsinkrement.

Referenzeinheiten verweisen auf den bestehenden Einheitenkatalog und müssen mit
der Basiseinheit der zugehörigen Zutatenrevision kompatibel sein. Bestehende
Zutaten benötigen für die Migration kein Profil.

Entwürfe übernehmen die Profile beim Kopieren veröffentlichter Revisionen.
Zentrale Einreichungen kopieren die Werte und setzen ihren Prüfstatus wie die
übrigen prüfpflichtigen Angaben auf ungeprüft zurück. Die Eingabe im
Zutateneditor und die Rezeptberechnung folgen in getrennten Inkrementen.

## Persistenz und Migrationen

Das Domänen- und Application-Modell ist providerunabhängig. Die produktive Persistenz wird mit EF Core und getrennten Migrationen für PostgreSQL und SQLite umgesetzt. Die direkt danebenliegende SQL-Datei beschreibt die beabsichtigte PostgreSQL-Struktur als Referenz; sie ist nicht die Quelle für produktive Migrationen.

Bestehende `BaseIngredients` werden unter Erhalt ihrer IDs migriert. Ihre bisherigen veränderlichen Felder und Zuordnungen bilden jeweils eine initiale veröffentlichte Revision. Bestehende Rezeptreferenzen werden auf diese Revision umgestellt; bereits publizierte Snapshots werden nicht nachträglich verändert.

## PostgreSQL-Referenzschema

Siehe die direkt danebenliegende Datei:

`Basiszutaten_Schema.sql`
