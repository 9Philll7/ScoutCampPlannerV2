-- ScoutCampPlanner
-- Basiszutaten-Modul
-- PostgreSQL
-- Referenzschema; produktive Migrationen werden providerabhängig mit EF Core erzeugt.
--
-- Scope:
--   - zentrale und lokale Zutaten
--   - Revisionen / Draft / Publish
--   - Kategorien und Einheiten
--   - Allergene
--   - Unverträglichkeiten
--   - Herkunftsmerkmale
--   - Varianten und Overrides
--
-- Nicht enthalten:
--   - Rezepte
--   - Lager
--   - Einkaufsartikel
--   - Teilnehmer
--   - Kocheinheiten

CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- =========================================================
-- ENUMS
-- =========================================================

CREATE TYPE ingredient_scope_type AS ENUM (
    'central',
    'tenant',
    'camp'
);

CREATE TYPE ingredient_status AS ENUM (
    'active',
    'archived'
);

CREATE TYPE revision_state AS ENUM (
    'draft',
    'published'
);

CREATE TYPE property_review_state AS ENUM (
    'unreviewed',
    'reviewed'
);

CREATE TYPE property_state AS ENUM (
    'contains',
    'does_not_contain',
    'may_contain',
    'unknown'
);

CREATE TYPE property_source AS ENUM (
    'inherent',
    'derived',
    'manually_verified',
    'article_dependent'
);

CREATE TYPE conversion_precision AS ENUM (
    'exact',
    'average',
    'estimated'
);

-- =========================================================
-- EINHEITEN
-- =========================================================

CREATE TABLE unit_dimension (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(50) NOT NULL UNIQUE,
    name VARCHAR(100) NOT NULL UNIQUE
);

CREATE TABLE unit (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    dimension_id UUID NOT NULL REFERENCES unit_dimension(id),
    code VARCHAR(30) NOT NULL UNIQUE,
    name VARCHAR(100) NOT NULL,
    symbol VARCHAR(20),
    factor_to_reference NUMERIC(18, 8),
    is_reference_unit BOOLEAN NOT NULL DEFAULT FALSE,
    status ingredient_status NOT NULL DEFAULT 'active',
    CONSTRAINT unit_factor_positive
        CHECK (factor_to_reference IS NULL OR factor_to_reference > 0)
);

CREATE UNIQUE INDEX uq_unit_reference_per_dimension
    ON unit(dimension_id)
    WHERE is_reference_unit = TRUE;

-- =========================================================
-- ZUTATENKATEGORIEN
-- =========================================================

CREATE TABLE ingredient_category (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    parent_category_id UUID REFERENCES ingredient_category(id),
    code VARCHAR(80) NOT NULL UNIQUE,
    name VARCHAR(150) NOT NULL UNIQUE,
    status ingredient_status NOT NULL DEFAULT 'active'
);

-- =========================================================
-- STABILE ZUTATENIDENTITÄT
-- =========================================================

CREATE TABLE ingredient (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),

    scope_type ingredient_scope_type NOT NULL,
    scope_id UUID,

    source_ingredient_id UUID REFERENCES ingredient(id),
    source_revision_id UUID,

    current_published_revision_id UUID,

    status ingredient_status NOT NULL DEFAULT 'active',

    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by UUID,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by UUID,

    CONSTRAINT ingredient_scope_consistency CHECK (
        (scope_type = 'central' AND scope_id IS NULL)
        OR
        (scope_type IN ('tenant', 'camp') AND scope_id IS NOT NULL)
    ),

    CONSTRAINT ingredient_source_consistency CHECK (
        (source_ingredient_id IS NULL AND source_revision_id IS NULL)
        OR
        (source_ingredient_id IS NOT NULL AND source_revision_id IS NOT NULL)
    )
);

COMMENT ON TABLE ingredient IS
'Stabile Identität einer zentralen oder lokalen Basiszutat. Fachlich veränderliche Inhalte liegen in ingredient_revision.';

-- =========================================================
-- ZUTATENREVISIONEN
-- =========================================================

CREATE TABLE ingredient_revision (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ingredient_id UUID NOT NULL REFERENCES ingredient(id),

    revision_number INTEGER NOT NULL,
    state revision_state NOT NULL,

    based_on_revision_id UUID REFERENCES ingredient_revision(id),
    merged_central_revision_id UUID REFERENCES ingredient_revision(id),

    name VARCHAR(200) NOT NULL,
    normalized_name VARCHAR(200) NOT NULL,
    category_id UUID NOT NULL REFERENCES ingredient_category(id),
    base_unit_id UUID NOT NULL REFERENCES unit(id),

    allergen_review_state property_review_state NOT NULL DEFAULT 'unreviewed',
    intolerance_review_state property_review_state NOT NULL DEFAULT 'unreviewed',
    origin_review_state property_review_state NOT NULL DEFAULT 'unreviewed',

    row_version BIGINT NOT NULL DEFAULT 1,

    created_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    created_by UUID,
    updated_at TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    updated_by UUID,

    published_at TIMESTAMPTZ,
    published_by UUID,

    CONSTRAINT ingredient_revision_number_positive
        CHECK (revision_number > 0),

    CONSTRAINT ingredient_revision_name_not_blank
        CHECK (BTRIM(name) <> ''),

    CONSTRAINT ingredient_revision_normalized_name_not_blank
        CHECK (BTRIM(normalized_name) <> ''),

    CONSTRAINT ingredient_revision_publish_consistency CHECK (
        (state = 'published' AND published_at IS NOT NULL)
        OR
        (state <> 'published')
    ),

    CONSTRAINT uq_ingredient_revision_number
        UNIQUE (ingredient_id, revision_number)
);

CREATE UNIQUE INDEX uq_one_draft_per_ingredient
    ON ingredient_revision(ingredient_id)
    WHERE state = 'draft';

-- Die Eindeutigkeit des Namens einer aktuell veröffentlichten Revision innerhalb
-- ihres Scopes wird transaktional in der Application-Schicht geprüft. Ein partieller
-- Index auf ingredient_revision kann dies nicht korrekt ausdrücken, weil Scope und
-- aktuelle Revision in ingredient liegen und historische Revisionen published bleiben.

-- PostgreSQL erlaubt keine FK auf eine noch nicht deklarierte Spalte
-- mit zyklischer Struktur direkt bei CREATE TABLE ingredient.
ALTER TABLE ingredient
    ADD CONSTRAINT fk_ingredient_source_revision
    FOREIGN KEY (source_revision_id)
    REFERENCES ingredient_revision(id);

ALTER TABLE ingredient
    ADD CONSTRAINT fk_ingredient_current_published_revision
    FOREIGN KEY (current_published_revision_id)
    REFERENCES ingredient_revision(id);

-- =========================================================
-- VALIDIERUNG CURRENT_PUBLISHED_REVISION
-- =========================================================

CREATE OR REPLACE FUNCTION validate_current_published_revision()
RETURNS TRIGGER AS $$
BEGIN
    IF NEW.current_published_revision_id IS NOT NULL
       AND NOT EXISTS (
            SELECT 1
            FROM ingredient_revision r
            WHERE r.id = NEW.current_published_revision_id
              AND r.ingredient_id = NEW.id
              AND r.state = 'published'
       )
    THEN
        RAISE EXCEPTION
            'current_published_revision_id must reference a published revision of the same ingredient';
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_validate_current_published_revision
BEFORE INSERT OR UPDATE OF current_published_revision_id
ON ingredient
FOR EACH ROW
EXECUTE FUNCTION validate_current_published_revision();

-- =========================================================
-- ALLERGENKATALOG
-- =========================================================

CREATE TABLE allergen (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    parent_allergen_id UUID REFERENCES allergen(id),
    code VARCHAR(100) NOT NULL UNIQUE,
    name VARCHAR(150) NOT NULL UNIQUE,
    is_eu_major_allergen BOOLEAN NOT NULL DEFAULT FALSE,
    status ingredient_status NOT NULL DEFAULT 'active'
);

CREATE TABLE ingredient_revision_allergen (
    ingredient_revision_id UUID NOT NULL
        REFERENCES ingredient_revision(id) ON DELETE CASCADE,
    allergen_id UUID NOT NULL REFERENCES allergen(id),
    state property_state NOT NULL,
    source property_source NOT NULL,
    PRIMARY KEY (ingredient_revision_id, allergen_id)
);

-- =========================================================
-- UNVERTRÄGLICHKEITSAUSLÖSER
-- =========================================================

CREATE TABLE intolerance_trigger (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(100) NOT NULL UNIQUE,
    name VARCHAR(150) NOT NULL UNIQUE,
    is_quantity_dependent BOOLEAN NOT NULL DEFAULT FALSE,
    status ingredient_status NOT NULL DEFAULT 'active'
);

CREATE TABLE ingredient_revision_intolerance (
    ingredient_revision_id UUID NOT NULL
        REFERENCES ingredient_revision(id) ON DELETE CASCADE,
    intolerance_id UUID NOT NULL REFERENCES intolerance_trigger(id),
    state property_state NOT NULL,
    source property_source NOT NULL,
    PRIMARY KEY (ingredient_revision_id, intolerance_id)
);

-- =========================================================
-- HERKUNFTSMERKMALE
-- =========================================================

CREATE TABLE origin_property (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    code VARCHAR(100) NOT NULL UNIQUE,
    name VARCHAR(150) NOT NULL UNIQUE,
    is_animal_origin BOOLEAN NOT NULL DEFAULT FALSE,
    status ingredient_status NOT NULL DEFAULT 'active'
);

CREATE TABLE ingredient_revision_origin (
    ingredient_revision_id UUID NOT NULL
        REFERENCES ingredient_revision(id) ON DELETE CASCADE,
    origin_property_id UUID NOT NULL REFERENCES origin_property(id),
    state property_state NOT NULL,
    source property_source NOT NULL,
    PRIMARY KEY (ingredient_revision_id, origin_property_id)
);

-- =========================================================
-- ZUTATENSPEZIFISCHE EINHEITENUMRECHNUNGEN
-- =========================================================

CREATE TABLE ingredient_revision_unit_conversion (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ingredient_revision_id UUID NOT NULL
        REFERENCES ingredient_revision(id) ON DELETE CASCADE,
    source_unit_id UUID NOT NULL REFERENCES unit(id),
    factor_to_base_unit NUMERIC(18, 8) NOT NULL,
    precision conversion_precision NOT NULL,
    CONSTRAINT ingredient_revision_conversion_factor_positive
        CHECK (factor_to_base_unit > 0),
    CONSTRAINT uq_ingredient_revision_unit_conversion
        UNIQUE (ingredient_revision_id, source_unit_id)
);

-- =========================================================
-- VARIANTEN EINER ZUTATENREVISION
-- =========================================================

CREATE TABLE ingredient_variant_revision (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    ingredient_revision_id UUID NOT NULL
        REFERENCES ingredient_revision(id) ON DELETE CASCADE,

    variant_key VARCHAR(100) NOT NULL,
    name VARCHAR(200) NOT NULL,
    normalized_name VARCHAR(200) NOT NULL,
    status ingredient_status NOT NULL DEFAULT 'active',

    sort_order INTEGER NOT NULL DEFAULT 0,

    CONSTRAINT ingredient_variant_key_not_blank
        CHECK (BTRIM(variant_key) <> ''),

    CONSTRAINT ingredient_variant_name_not_blank
        CHECK (BTRIM(name) <> ''),

    CONSTRAINT uq_variant_key_per_revision
        UNIQUE (ingredient_revision_id, variant_key),

    CONSTRAINT uq_variant_name_per_revision
        UNIQUE (ingredient_revision_id, normalized_name)
);

COMMENT ON COLUMN ingredient_variant_revision.variant_key IS
'Stabiler fachlicher Schlüssel einer Variante über Zutatenrevisionen hinweg.';

-- =========================================================
-- VARIANTEN-OVERRIDES
-- =========================================================

CREATE TABLE ingredient_variant_allergen_override (
    variant_revision_id UUID NOT NULL
        REFERENCES ingredient_variant_revision(id) ON DELETE CASCADE,
    allergen_id UUID NOT NULL REFERENCES allergen(id),
    state property_state NOT NULL,
    source property_source NOT NULL,
    PRIMARY KEY (variant_revision_id, allergen_id)
);

CREATE TABLE ingredient_variant_intolerance_override (
    variant_revision_id UUID NOT NULL
        REFERENCES ingredient_variant_revision(id) ON DELETE CASCADE,
    intolerance_id UUID NOT NULL REFERENCES intolerance_trigger(id),
    state property_state NOT NULL,
    source property_source NOT NULL,
    PRIMARY KEY (variant_revision_id, intolerance_id)
);

CREATE TABLE ingredient_variant_origin_override (
    variant_revision_id UUID NOT NULL
        REFERENCES ingredient_variant_revision(id) ON DELETE CASCADE,
    origin_property_id UUID NOT NULL REFERENCES origin_property(id),
    state property_state NOT NULL,
    source property_source NOT NULL,
    PRIMARY KEY (variant_revision_id, origin_property_id)
);

CREATE TABLE ingredient_variant_unit_conversion_override (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    variant_revision_id UUID NOT NULL
        REFERENCES ingredient_variant_revision(id) ON DELETE CASCADE,
    source_unit_id UUID NOT NULL REFERENCES unit(id),
    factor_to_base_unit NUMERIC(18, 8) NOT NULL,
    precision conversion_precision NOT NULL,

    CONSTRAINT ingredient_variant_conversion_factor_positive
        CHECK (factor_to_base_unit > 0),

    CONSTRAINT uq_variant_unit_conversion_override
        UNIQUE (variant_revision_id, source_unit_id)
);

-- =========================================================
-- SCHUTZ VERÖFFENTLICHTER REVISIONEN
-- =========================================================

CREATE OR REPLACE FUNCTION prevent_published_revision_mutation()
RETURNS TRIGGER AS $$
DECLARE
    target_revision_id UUID;
    target_state revision_state;
BEGIN
    IF TG_TABLE_NAME = 'ingredient_revision' THEN
        IF TG_OP = 'INSERT' THEN
            target_revision_id := NEW.id;
        ELSE
            target_revision_id := OLD.id;
        END IF;
    ELSIF TG_TABLE_NAME = 'ingredient_revision_allergen'
       OR TG_TABLE_NAME = 'ingredient_revision_intolerance'
       OR TG_TABLE_NAME = 'ingredient_revision_origin'
       OR TG_TABLE_NAME = 'ingredient_revision_unit_conversion' THEN
        IF TG_OP = 'INSERT' THEN
            target_revision_id := NEW.ingredient_revision_id;
        ELSE
            target_revision_id := OLD.ingredient_revision_id;
        END IF;
    ELSIF TG_TABLE_NAME = 'ingredient_variant_revision' THEN
        IF TG_OP = 'INSERT' THEN
            target_revision_id := NEW.ingredient_revision_id;
        ELSE
            target_revision_id := OLD.ingredient_revision_id;
        END IF;
    ELSE
        IF TG_OP = 'INSERT' THEN
            SELECT v.ingredient_revision_id INTO target_revision_id
              FROM ingredient_variant_revision v
             WHERE v.id = NEW.variant_revision_id;
        ELSE
            SELECT v.ingredient_revision_id INTO target_revision_id
              FROM ingredient_variant_revision v
             WHERE v.id = OLD.variant_revision_id;
        END IF;
    END IF;

    SELECT state
      INTO target_state
      FROM ingredient_revision
     WHERE id = target_revision_id;

    IF target_state = 'published' THEN
        RAISE EXCEPTION 'Published ingredient revisions are immutable';
    END IF;

    IF TG_OP = 'DELETE' THEN
        RETURN OLD;
    END IF;

    RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER trg_protect_ingredient_revision
BEFORE UPDATE OR DELETE ON ingredient_revision
FOR EACH ROW EXECUTE FUNCTION prevent_published_revision_mutation();

CREATE TRIGGER trg_protect_ingredient_revision_allergen
BEFORE INSERT OR UPDATE OR DELETE ON ingredient_revision_allergen
FOR EACH ROW EXECUTE FUNCTION prevent_published_revision_mutation();

CREATE TRIGGER trg_protect_ingredient_revision_intolerance
BEFORE INSERT OR UPDATE OR DELETE ON ingredient_revision_intolerance
FOR EACH ROW EXECUTE FUNCTION prevent_published_revision_mutation();

CREATE TRIGGER trg_protect_ingredient_revision_origin
BEFORE INSERT OR UPDATE OR DELETE ON ingredient_revision_origin
FOR EACH ROW EXECUTE FUNCTION prevent_published_revision_mutation();

CREATE TRIGGER trg_protect_ingredient_revision_unit_conversion
BEFORE INSERT OR UPDATE OR DELETE ON ingredient_revision_unit_conversion
FOR EACH ROW EXECUTE FUNCTION prevent_published_revision_mutation();

CREATE TRIGGER trg_protect_ingredient_variant_revision
BEFORE INSERT OR UPDATE OR DELETE ON ingredient_variant_revision
FOR EACH ROW EXECUTE FUNCTION prevent_published_revision_mutation();

CREATE TRIGGER trg_protect_ingredient_variant_allergen_override
BEFORE INSERT OR UPDATE OR DELETE ON ingredient_variant_allergen_override
FOR EACH ROW EXECUTE FUNCTION prevent_published_revision_mutation();

CREATE TRIGGER trg_protect_ingredient_variant_intolerance_override
BEFORE INSERT OR UPDATE OR DELETE ON ingredient_variant_intolerance_override
FOR EACH ROW EXECUTE FUNCTION prevent_published_revision_mutation();

CREATE TRIGGER trg_protect_ingredient_variant_origin_override
BEFORE INSERT OR UPDATE OR DELETE ON ingredient_variant_origin_override
FOR EACH ROW EXECUTE FUNCTION prevent_published_revision_mutation();

CREATE TRIGGER trg_protect_ingredient_variant_unit_conversion_override
BEFORE INSERT OR UPDATE OR DELETE ON ingredient_variant_unit_conversion_override
FOR EACH ROW EXECUTE FUNCTION prevent_published_revision_mutation();

-- =========================================================
-- EMPFOHLENE INDIZES
-- =========================================================

CREATE INDEX idx_ingredient_scope
    ON ingredient(scope_type, scope_id);

CREATE INDEX idx_ingredient_source
    ON ingredient(source_ingredient_id, source_revision_id);

CREATE INDEX idx_ingredient_revision_ingredient
    ON ingredient_revision(ingredient_id, revision_number DESC);

CREATE INDEX idx_ingredient_revision_state
    ON ingredient_revision(state);

CREATE INDEX idx_ingredient_revision_category
    ON ingredient_revision(category_id);

CREATE INDEX idx_variant_revision_parent
    ON ingredient_variant_revision(ingredient_revision_id);

-- =========================================================
-- HINWEIS ZUR VERÖFFENTLICHUNG
-- =========================================================
--
-- Publish sollte transaktional in der Anwendung / Domain-Schicht erfolgen:
--
-- 1. Draft vollständig validieren.
-- 2. row_version / Optimistic Concurrency prüfen.
-- 3. Draft auf state='published' setzen,
--    published_at / published_by setzen.
-- 4. ingredient.current_published_revision_id auf die Revision setzen.
-- 5. COMMIT.
--
-- Die Domain-Schicht muss außerdem sicherstellen:
-- - current_published_revision gehört zur selben ingredient-ID.
-- - source_ingredient_id zeigt bei lokalen Forks auf eine zentrale Zutat.
-- - source_revision_id gehört zu source_ingredient_id.
-- - merged_central_revision_id gehört zum zentralen Ursprung.
-- - Varianten behalten über Revisionen stabile variant_key-Werte.
-- - Namen aktueller Revisionen sind innerhalb des Scopes eindeutig.
-- - Allergengruppen und Untertypen enthalten keine widersprüchlichen Zustände.
-- - Reviewstatus und Eigenschaftsdaten erlauben keine fälschliche Compatible-Auswertung.
--
-- Das entsprechende SQLite-Schema verwendet keine PostgreSQL-ENUMs, Extensions,
-- partiellen PostgreSQL-Indizes oder PL/pgSQL-Trigger. Dieselben Invarianten werden
-- providerunabhängig in Domain/Application und soweit möglich per EF-Migration geschützt.
