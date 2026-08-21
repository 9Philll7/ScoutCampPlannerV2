# Codex-Auftrag: Basiszutaten

## Ziel

Implementiere die Domäne **Basiszutaten** gemäß:

- `docs/domain/Basiszutaten.md`
- `docs/architecture/Basiszutaten_Datenbankarchitektur.md`
- `docs/architecture/Basiszutaten_Schema.sql`

Der Auftrag ist bewusst auf die Basiszutaten-Domäne begrenzt.

Nicht implementieren:

- neue Rezeptfachlogik oder Erweiterungen des Rezepteditors
- Lager
- Einkaufsartikel
- Teilnehmer
- Kocheinheiten
- globale Ersatzbeziehungen

Die für stabile bestehende Referenzen notwendige Datenmigration und der Contract für `ingredient_revision_id` plus optionalem `variant_key` sind dennoch als Integrationsgrenze zu berücksichtigen. Die funktionale Umstellung des Rezepteditors erfolgt in einem getrennten Auftrag.

## Fachliche Kernanforderungen

### Zentrale und lokale Zutaten

Implementiere:

- zentrale Zutaten
- lokale Eigenanlagen
- lokale Forks zentraler Zutaten
- keine lokale Kopie bei reiner Verwendung
- lokale Kopie erst bei tatsächlicher Änderung

Ein lokaler Fork muss speichern:

- `source_ingredient_id`
- `source_revision_id`

Persistierte Scopes sind:

- `central`
- `tenant`
- `camp`

`local` ist nur der Sammelbegriff für Tenant- und Lagerdaten.

### Revisionen

Fachlich veränderliche Daten müssen revisionsgebunden sein.

Eine Zutatenidentität selbst darf keine fachlich veränderlichen Felder wie Name, Kategorie oder Basiseinheit enthalten.

Revisionen:

- `draft`
- `published`

Archivierung betrifft die stabile Zutatenidentität, nicht eine veröffentlichte Revision.

Eine veröffentlichte Revision ist unveränderlich.

Änderungen an einer veröffentlichten Zutat erzeugen immer einen neuen Draft.

Jeder gespeicherte Draft benötigt Name, Kategorie und Basiseinheit. Eigenschaften, Varianten und Umrechnungen dürfen bis zur Veröffentlichung unvollständig sein.

### Explizites Speichern

Bearbeitung arbeitet nicht mit Auto-Save.

Speichern ist eine explizite Aktion.

Verwende optimistische Nebenläufigkeitskontrolle, damit parallele Bearbeitungen nicht still überschrieben werden.

### Veröffentlichung

Implementiere einen transaktionalen Publish-Vorgang.

Publish darf nur erfolgreich sein, wenn alle fachlichen Invarianten erfüllt sind.

Nach Publish:

- Revision ist `published`
- `published_at` und `published_by` sind gesetzt
- `current_published_revision_id` zeigt auf die neue Revision
- Revision kann nicht mehr verändert werden

### Zentrale Updates

Für lokale Forks muss erkennbar sein, ob eine neuere zentrale veröffentlichte Revision existiert.

Ermögliche einen Drei-Wege-Vergleich:

- Base = zuletzt lokal berücksichtigte zentrale Revision
- Local = lokale Revision / lokaler Draft
- Remote = aktuelle zentrale Revision

Der Vergleich soll mindestens Änderungen an folgenden Bereichen erkennen:

- Name
- Kategorie
- Basiseinheit
- Allergene
- Unverträglichkeitsauslöser
- Herkunftsmerkmale
- Varianten
- Umrechnungen

Nicht überlappende Änderungen dürfen zusammengeführt werden.

Überlappende Änderungen erzeugen einen Konflikt und dürfen nicht automatisch aufgelöst werden.

Die Übernahme eines zentralen Updates erzeugt einen neuen lokalen Draft und verändert keine veröffentlichte lokale Revision direkt.

## Basiszutatenmodell

Eine Zutatenrevision umfasst:

- Name
- Kategorie
- Basiseinheit
- Allergene
- Unverträglichkeitsauslöser
- Herkunftsmerkmale
- zutatenspezifische Umrechnungen
- Varianten

### Basiseinheit

Jede Revision besitzt genau eine Basiseinheit.

Varianten können die Basiseinheit nicht überschreiben.

### Varianten

Varianten gehören zu einer konkreten Zutatenrevision.

Jede fachliche Variante besitzt einen stabilen `variant_key`, der über Revisionen beibehalten wird.

Varianten erben:

- Allergene
- Unverträglichkeiten
- Herkunft
- zutatenspezifische Umrechnungen

Varianten dürfen gezielt Overrides speichern.

Varianten dürfen Kategorie und Basiseinheit nicht überschreiben.

Eine Rezeptposition referenziert eine konkrete veröffentlichte Zutatenrevision und optional einen `variant_key`. Ohne Variante gilt die Basisform.

## Eigenschaften

Verwende:

`property_state`

- `contains`
- `does_not_contain`
- `may_contain`
- `unknown`

und:

`property_source`

- `inherent`
- `derived`
- `manually_verified`
- `article_dependent`

Fehlende Eigenschaftsdaten dürfen niemals automatisch als `does_not_contain` interpretiert werden.

Allergen-, Unverträglichkeits- und Herkunftsdaten besitzen jeweils einen Reviewstatus `unreviewed` oder `reviewed`. Fehlende Werte einer ungeprüften Gruppe ergeben `unknown`. Publish muss widersprüchliche Eltern-/Kindangaben bei Allergenen ablehnen.

## Auswertungsservice

Implementiere einen Domain-Service, der effektive Eigenschaften einer Basiszutat bzw. Variante berechnet.

Für Varianten:

`effective = revision base properties + variant overrides`

Der Service soll mindestens folgende Resultate liefern können:

- `compatible`
- `incompatible`
- `unknown`

für:

- einzelne Allergene
- einzelne Unverträglichkeitsauslöser
- vegan
- vegetarisch
- pescetarisch
- laktosefrei
- milchfrei
- glutenfrei

Diese Eignungen werden berechnet und nicht direkt als Zutatenmerkmale gespeichert. Stabile Anforderungscodes bleiben für Teilnehmer- und Rezeptregeln zulässig.

Die Regeln stehen in `docs/domain/Basiszutaten.md`.

## Stammdaten

Erstelle Seed-Daten für:

### Allergene

Die 14 EU-Hauptgruppen plus relevante Untertypen für:

- glutenhaltige Getreide
- Schalenfrüchte

### Unverträglichkeiten

Initial:

- LACTOSE
- FRUCTOSE
- SORBITOL
- HISTAMINE
- GLUTEN
- FRUCTANS
- GALACTANS
- MANNITOL
- XYLITOL
- OTHER_POLYOLS

### Herkunftsmerkmale

Nichttierisch:

- PLANT
- FUNGI
- MINERAL
- SYNTHETIC
- MICROBIAL

Tierisch:

- MEAT
- POULTRY
- FISH
- CRUSTACEAN
- MOLLUSC
- DAIRY
- EGG
- HONEY
- INSECT
- ANIMAL_FAT
- GELATIN
- ANIMAL_RENNET
- OTHER_ANIMAL_DERIVED

Zusätzlich:

- UNKNOWN_ORIGIN

## Tests

Mindestens folgende Tests sind erforderlich:

1. Eine zentrale Zutat kann mit Draft angelegt und veröffentlicht werden.
2. Published Revisionen sind unveränderlich.
3. Eine Änderung einer Published Revision erzeugt einen neuen Draft.
4. Eine lokale Verwendung erzeugt keinen Fork.
5. Eine lokale Änderung einer zentralen Zutat erzeugt einen Fork.
6. Fork referenziert zentrale Zutat und Ausgangsrevision.
7. Zentrale spätere Revision überschreibt lokalen Fork nicht.
8. Update-Verfügbarkeit wird korrekt erkannt.
9. Nicht überlappende zentrale/lokale Änderungen können gemerged werden.
10. Überlappende Änderungen erzeugen einen Konflikt.
11. Merge erzeugt einen lokalen Draft.
12. Variante erbt Eigenschaften der Zutatenrevision.
13. Varianten-Override hat Vorrang.
14. Variante kann Kategorie nicht ändern.
15. Variante kann Basiseinheit nicht ändern.
16. `variant_key` bleibt über Revisionen stabil.
17. Laktosefreie Butter bleibt wegen `MILK` milchhaltig.
18. Tierische Herkunft führt bei vegan zu `incompatible`.
19. Unbekannte relevante Herkunft führt zu `unknown`.
20. Fehlende Eigenschaftsdaten gelten nicht als `does_not_contain`.
21. Tenant- und Camp-Scopes bleiben getrennt.
22. Ein ungeprüfter Eigenschaftsbereich liefert bei fehlenden Angaben `unknown`.
23. Widersprüchliche Allergen-Hierarchien verhindern Publish.
24. Eine Rezeptreferenz kann eine Variante über ihren stabilen `variant_key` auswählen.
25. Bestehende Zutaten-IDs und publizierte Rezept-Snapshots bleiben bei der Migration erhalten.

## Architektur

Halte Domain-Logik von Persistenz und UI getrennt.

Die Datenbank schützt grundlegende Integrität, aber folgende Logik gehört primär in die Domain-/Application-Schicht:

- Draft-Erstellung
- Publish
- Fork-Erstellung
- Update-Erkennung
- Drei-Wege-Diff
- Merge
- Konflikterkennung
- effektive Variantenwerte
- Ernährungsregelauswertung

Persistenz wird über ein gemeinsames EF-Core-Modell und getrennte PostgreSQL-/SQLite-Migrationen umgesetzt. `docs/architecture/Basiszutaten_Schema.sql` ist ein PostgreSQL-Referenzschema und keine direkt auszuführende Produktmigration.

Keine zusätzlichen Features außerhalb dieses Auftrags implementieren.

Wenn ein Detail nicht explizit definiert ist, bevorzuge die einfachste Lösung, die die beschriebenen Invarianten erhält. Dokumentiere notwendige Annahmen.
