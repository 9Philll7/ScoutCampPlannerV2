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
- Herkunftsmerkmale
- Zutatenvarianten und Overrides
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
    ├── ingredient_revision_origin
    ├── ingredient_revision_unit_conversion
    └── ingredient_variant_revision
        ├── ingredient_variant_allergen_override
        ├── ingredient_variant_intolerance_override
        ├── ingredient_variant_origin_override
        └── ingredient_variant_unit_conversion_override
```

Persistierte Scopes sind `central`, `tenant` und `camp`. Eine Tenant- oder Lagerzutatenidentität kann über `source_ingredient_id` und `source_revision_id` auf ihren zentralen Ursprung verweisen. `local` ist nur ein fachlicher Sammelbegriff.

Rezepte referenzieren die ID einer konkreten veröffentlichten Zutatenrevision und optional den stabilen `variant_key`. Eigenschaftsgruppen besitzen einen Reviewstatus, damit fehlende Angaben nicht versehentlich als unbedenklich ausgewertet werden.

## Persistenz und Migrationen

Das Domänen- und Application-Modell ist providerunabhängig. Die produktive Persistenz wird mit EF Core und getrennten Migrationen für PostgreSQL und SQLite umgesetzt. Die direkt danebenliegende SQL-Datei beschreibt die beabsichtigte PostgreSQL-Struktur als Referenz; sie ist nicht die Quelle für produktive Migrationen.

Bestehende `BaseIngredients` werden unter Erhalt ihrer IDs migriert. Ihre bisherigen veränderlichen Felder und Zuordnungen bilden jeweils eine initiale veröffentlichte Revision. Bestehende Rezeptreferenzen werden auf diese Revision umgestellt; bereits publizierte Snapshots werden nicht nachträglich verändert.

## PostgreSQL-Referenzschema

Siehe die direkt danebenliegende Datei:

`Basiszutaten_Schema.sql`
