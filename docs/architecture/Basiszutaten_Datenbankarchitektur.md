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
    ├── ingredient_revision_origin
    ├── ingredient_revision_unit_conversion
    ├── ingredient_revision_nutrition_profile
    └── ingredient_variant_revision
        ├── ingredient_variant_allergen_override
        ├── ingredient_variant_intolerance_override
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

## Geplante Nährwertpersistenz

ADR-024 ergänzt je Zutatenrevision höchstens ein optionales Nährwertprofil und
je Variante höchstens ein vollständiges Ersatzprofil. Die Implementierung und
die provider-spezifischen Migrationen stehen noch aus.

Das Revisionsprofil enthält Bezugsmenge, Referenzeinheit, Energie in kJ, die
sechs verpflichtenden Mengenfelder des ersten Umfangs, optionale
Ballaststoffe, Quellenart, Quellenangabe und Prüfstatus. Das Variantenprofil
besitzt dieselbe fachliche Struktur. Einzelne Variantenfelder werden nicht als
Overrides persistiert.

Referenzeinheiten verweisen auf den bestehenden Einheitenkatalog und müssen mit
der Basiseinheit der zugehörigen Zutatenrevision kompatibel sein. Bestehende
Zutaten benötigen für die Migration kein Profil.

## Persistenz und Migrationen

Das Domänen- und Application-Modell ist providerunabhängig. Die produktive Persistenz wird mit EF Core und getrennten Migrationen für PostgreSQL und SQLite umgesetzt. Die direkt danebenliegende SQL-Datei beschreibt die beabsichtigte PostgreSQL-Struktur als Referenz; sie ist nicht die Quelle für produktive Migrationen.

Bestehende `BaseIngredients` werden unter Erhalt ihrer IDs migriert. Ihre bisherigen veränderlichen Felder und Zuordnungen bilden jeweils eine initiale veröffentlichte Revision. Bestehende Rezeptreferenzen werden auf diese Revision umgestellt; bereits publizierte Snapshots werden nicht nachträglich verändert.

## PostgreSQL-Referenzschema

Siehe die direkt danebenliegende Datei:

`Basiszutaten_Schema.sql`
