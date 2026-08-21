# Basiszutaten – Domänenbeschreibung

## Zweck und Scope

Dieses Dokument beschreibt ausschließlich die Domäne **Basiszutaten** des ScoutCampPlanner.

Nicht Bestandteil dieses Dokuments sind die fachliche Modellierung von Rezepten, Lagerbeständen, Einkaufsartikeln, Teilnehmern oder Kocheinheiten. Diese Domänen dürfen Basiszutaten referenzieren, werden hier aber nicht weiter modelliert.

Ziel ist eine zentrale, revisionsfähige Zutatendatenbank mit lokalen Forks für mandanten- und lagerspezifische Änderungen. Verbindliche Architekturentscheidung ist [ADR-021](../decisions/adr-021-revisioned-base-ingredients.md).

---

## 1. Zentrale Grundbegriffe

### 1.1 Basiszutat

Eine Basiszutat beschreibt eine fachlich eigenständige Zutat, unabhängig von Hersteller, Marke oder Gebinde.

Beispiele:

- Butter
- Kuhmilch
- Haferdrink
- Weizenmehl
- Reis
- Ei

Eine Basiszutat besitzt eine stabile Identität. Änderbare fachliche Daten werden revisionsgebunden gespeichert.

### 1.2 Zutatenvariante

Eine Variante bleibt fachlich dieselbe Basiszutat und verändert nur einzelne Eigenschaften.

Beispiele:

- Butter → laktosefreie Butter
- Kuhmilch → laktosefreie Kuhmilch
- Butter → gesalzene Butter
- Reis → Vollkornreis

Eine andere Rohstoffbasis ist im Regelfall **keine** Variante.

Beispiele:

- Haferdrink ist keine Variante von Kuhmilch.
- Margarine ist keine Variante von Butter.
- Sojajoghurt ist keine Variante von Milchjoghurt.

### 1.3 Ersatzbeziehungen

Globale Ersatzbeziehungen gehören **nicht** zur Basiszutaten-Domäne.

Ob eine andere Zutat als Ersatz geeignet ist, wird im jeweiligen Rezept definiert. Spätere Vorschlagslogik darf aus Nutzungsdaten ableiten, welche Zutaten häufig als Ersatz gewählt werden, darf daraus aber keine automatische fachliche Austauschbarkeit ableiten.

---

## 2. Zentrale und lokale Zutaten

### 2.1 Zentrale Zutaten

Zentrale Zutaten bilden den gemeinsamen Stammdatenbestand.

Eine zentrale Zutat:

- ist für alle Mandanten grundsätzlich verfügbar,
- besitzt veröffentlichte Revisionen,
- kann direkt referenziert werden,
- wird durch lokale Nutzung nicht kopiert.

### 2.2 Lokale Kopie / Fork

Eine lokale Kopie entsteht erst, wenn ein Mandant eine zentrale Zutat tatsächlich lokal verändern und speichern möchte.

Der lokale Datensatz erhält:

- eine eigene stabile ID,
- eine Referenz auf die zentrale Ursprungszutat,
- eine Referenz auf die zentrale Revision, von der der Fork erzeugt wurde,
- eine eigene Revisionshistorie.

Eine reine Verwendung einer zentralen Zutat erzeugt **keine** lokale Kopie.

### 2.3 Lokale Eigenanlage

Ein Mandant oder Lager kann eine eigene lokale Basiszutat anlegen, die keinen zentralen Ursprung besitzt.

Diese lokale Zutat folgt denselben Revisions- und Veröffentlichungsregeln wie ein Fork.

---

## 3. Identität und Revisionsmodell

Die stabile Identität und der fachliche Inhalt werden getrennt.

### 3.1 Ingredient

Die stabile Identität enthält insbesondere:

- `id`
- `scope_type` (`central`, `tenant`, `camp`)
- `scope_id` für Tenant- und Lagerzutaten
- `source_ingredient_id` bei lokalen Forks
- `source_revision_id` als Fork-Basis
- `current_published_revision_id`
- `status`
- Auditdaten

### 3.2 IngredientRevision

Alle fachlich veränderlichen Daten liegen in Revisionen:

- `id`
- `ingredient_id`
- `revision_number`
- `state`
- `based_on_revision_id`
- `merged_central_revision_id`
- `name`
- `category_id`
- `base_unit_id`
- Audit- und Veröffentlichungsdaten

Revisionszustände:

- `draft`
- `published`

Archivierung betrifft die stabile Zutatenidentität. Historische veröffentlichte Revisionen bleiben unverändert im Zustand `published`.

### 3.3 Veröffentlichte Revisionen

Eine veröffentlichte Revision ist unveränderlich.

Eine spätere Änderung erzeugt eine neue Entwurfsrevision. Erst die Veröffentlichung macht diese Revision zur aktuellen veröffentlichten Revision.

---

## 4. Bearbeitung und Veröffentlichung

### 4.1 Entwurf

Ein Entwurf:

- darf bearbeitet werden,
- wird explizit gespeichert,
- benötigt beim Speichern bereits Name, Kategorie und Basiseinheit,
- darf bei Eigenschaften, Varianten und Umrechnungen noch unvollständig sein,
- ersetzt die veröffentlichte Revision noch nicht.

### 4.2 Veröffentlichung

Beim Veröffentlichen:

1. wird der Entwurf validiert,
2. erhält er den Zustand `published`,
3. wird er unveränderlich,
4. wird `current_published_revision_id` aktualisiert,
5. bleibt die vorherige Revision erhalten.

### 4.3 Parallele Bearbeitung

Es ist keine zwingende exklusive Sperre erforderlich.

Das System soll:

- anzeigen können, dass ein Entwurf gerade von einer anderen Person bearbeitet wird,
- konkurrierende Speichervorgänge erkennen,
- stille Überschreibungen verhindern.

Für die technische Umsetzung ist optimistische Nebenläufigkeitskontrolle vorgesehen.

### 4.4 Vollständigkeit und Review

Die Eigenschaftsgruppen Allergene, Unverträglichkeitsauslöser und Herkunft besitzen jeweils einen Reviewstatus:

- `unreviewed`
- `reviewed`

Fehlende Werte einer nicht geprüften Gruppe werden immer als `unknown` ausgewertet. Erst eine fachlich als `reviewed` markierte Gruppe darf fehlende Einträge nach den dokumentierten Regeln als nicht enthalten behandeln. Die Veröffentlichung lehnt widersprüchliche Eltern- und Kindangaben im Allergenkatalog ab.

---

## 5. Zentrale Aktualisierungen und lokale Forks

Eine zentrale Aktualisierung darf einen lokalen Fork niemals automatisch überschreiben.

Beispiel:

- lokaler Fork basiert auf zentraler Revision 4,
- zentral ist inzwischen Revision 6 veröffentlicht,
- das System zeigt eine verfügbare zentrale Aktualisierung an.

### 5.1 Update-Erkennung

Ein lokaler Fork gilt als updatefähig, wenn die aktuelle zentrale veröffentlichte Revision neuer ist als die zuletzt lokal berücksichtigte zentrale Revision.

### 5.2 Änderungsvergleich

Der Vergleich soll die fachlich geänderten Felder zeigen:

- Name
- Kategorie
- Basiseinheit
- Allergene
- Unverträglichkeitsauslöser
- Herkunftsmerkmale
- Varianten
- zutatenspezifische Umrechnungen

Listen werden auf Eintragsebene verglichen.

### 5.3 Drei-Wege-Vergleich

Für lokale Forks wird ein Drei-Wege-Vergleich verwendet:

- **Base:** zentrale Revision, auf der der lokale Stand basiert
- **Local:** aktuelle lokale veröffentlichte Revision bzw. lokaler Entwurf
- **Remote:** aktuelle zentrale veröffentlichte Revision

Nicht überlappende Änderungen können zusammengeführt werden.

Wenn dasselbe fachliche Feld zentral und lokal seit der gemeinsamen Basis verändert wurde, entsteht ein Konflikt.

### 5.4 Übernahme eines Updates

Die Übernahme zentraler Änderungen erzeugt immer eine neue lokale Entwurfsrevision.

Die bestehende veröffentlichte lokale Revision bleibt unverändert, bis der neue Entwurf veröffentlicht wird.

---

## 6. Grundfelder einer Basiszutat

Fachlich enthält eine Zutatenrevision:

- `name`
- `category_id`
- `base_unit_id`
- Allergene
- Unverträglichkeitsauslöser
- Herkunftsmerkmale
- zutatenspezifische Umrechnungen
- Varianten

### 6.1 Name

Der Name bezeichnet die fachliche Zutat.

Nicht enthalten sein dürfen:

- Marke
- Hersteller
- Gebindegröße
- konkrete Produktbezeichnung

Zentrale Zutaten sollen global möglichst eindeutig benannt sein.

### 6.2 Kategorie

Eine Basiszutat gehört genau einer fachlichen Kategorie an.

Kategorien dienen Suche und Gruppierung und definieren keine Austauschbarkeit.

### 6.3 Status

Für die stabile Zutatenidentität:

- `active`
- `archived`

Archivierte Zutaten bleiben für bestehende Referenzen erhalten.

---

## 7. Mengen- und Einheitensystem

Jede Basiszutat besitzt genau eine feste Basiseinheit.

Beispiele:

| Zutat | Basiseinheit |
|---|---|
| Mehl | g |
| Butter | g |
| Milch | ml |
| Öl | ml |
| Ei | Stück |

Die Basiseinheit wird von Varianten geerbt und kann durch Varianten nicht überschrieben werden.

### 7.1 Allgemeine Umrechnungen

Allgemeine Umrechnungen liegen im zentralen Einheitensystem, z. B.:

- 1 kg = 1000 g
- 1 l = 1000 ml

### 7.2 Zutatenspezifische Umrechnungen

Zutatenspezifische Umrechnungen werden revisionsgebunden gespeichert.

Beispiele:

- 1 EL Mehl = 10 g
- 1 Stück Zwiebel = 100 g
- 1 Bund Petersilie = 30 g

Genauigkeit:

- `exact`
- `average`
- `estimated`

Varianten können solche Umrechnungen gezielt überschreiben oder ergänzen.

---

## 8. Eigenschaftsmodell

Allergene, Unverträglichkeitsauslöser und Herkunftsmerkmale werden getrennt gespeichert.

Zustände:

- `contains`
- `does_not_contain`
- `may_contain`
- `unknown`

Quellen:

- `inherent`
- `derived`
- `manually_verified`
- `article_dependent`

Wichtige Regel:

> Fehlende Informationen bedeuten nicht `does_not_contain`.

Bei sicherheitsrelevanten Prüfungen gilt:

> `unknown` ist nicht gleich `compatible`.

---

## 9. Allergenkatalog

Die Anwendung verwendet die EU-Hauptallergene als zentral gepflegten Katalog.

### Hauptgruppen

- `GLUTEN_CEREALS` – Glutenhaltiges Getreide
- `CRUSTACEANS` – Krebstiere
- `EGGS` – Eier
- `FISH` – Fisch
- `PEANUTS` – Erdnüsse
- `SOYBEANS` – Sojabohnen
- `MILK` – Milch
- `TREE_NUTS` – Schalenfrüchte
- `CELERY` – Sellerie
- `MUSTARD` – Senf
- `SESAME` – Sesamsamen
- `SULPHUR_DIOXIDE_AND_SULPHITES` – Schwefeldioxid und Sulfite
- `LUPIN` – Lupinen
- `MOLLUSCS` – Weichtiere

### Untertypen glutenhaltigen Getreides

- `WHEAT`
- `RYE`
- `BARLEY`
- `OATS`
- `SPELT`
- `KHORASAN_WHEAT`
- `HYBRID_STRAINS`

### Untertypen der Schalenfrüchte

- `ALMONDS`
- `HAZELNUTS`
- `WALNUTS`
- `CASHEWS`
- `PECANS`
- `BRAZIL_NUTS`
- `PISTACHIOS`
- `MACADAMIA_NUTS`

Ein enthaltener Untertyp impliziert die übergeordnete Gruppe.

Die umgekehrte Richtung gilt nicht.

Spurenhinweise sind nicht Bestandteil der Basiszutat, da sie typischerweise vom konkreten Produkt und Herstellungsprozess abhängen.

---

## 10. Katalog der Unverträglichkeitsauslöser

Es gibt keine mit den EU-Hauptallergenen vergleichbare abschließende amtliche Gesamtliste.

Der Katalog ist daher fachlich gepflegt und erweiterbar.

Initial:

- `LACTOSE`
- `FRUCTOSE`
- `SORBITOL`
- `HISTAMINE`
- `GLUTEN`
- `FRUCTANS`
- `GALACTANS`
- `MANNITOL`
- `XYLITOL`
- `OTHER_POLYOLS`

Einige Unverträglichkeiten sind mengen-, verarbeitungs- oder produktspezifisch. In solchen Fällen darf die Basiszutat `unknown` oder `article_dependent` verwenden.

Milch und Laktose müssen getrennt bleiben:

- laktosefreie Butter kann `LACTOSE = does_not_contain` haben,
- das Allergen `MILK` bleibt dennoch `contains`.

---

## 11. Herkunftsmerkmale

Herkunftsmerkmale dienen insbesondere der regelbasierten Auswertung von vegan, vegetarisch und pescetarisch.

### Nichttierische Herkunft

- `PLANT`
- `FUNGI`
- `MINERAL`
- `SYNTHETIC`
- `MICROBIAL`

### Tierische Herkunft

- `MEAT`
- `POULTRY`
- `FISH`
- `CRUSTACEAN`
- `MOLLUSC`
- `DAIRY`
- `EGG`
- `HONEY`
- `INSECT`
- `ANIMAL_FAT`
- `GELATIN`
- `ANIMAL_RENNET`
- `OTHER_ANIMAL_DERIVED`

### Ungeklärte Herkunft

- `UNKNOWN_ORIGIN`

Mehrfachzuordnungen sind zulässig.

---

## 12. Varianten und Vererbung

Varianten sind Teil einer konkreten Zutatenrevision.

Eine veröffentlichte Zutatenrevision beschreibt damit immer einen vollständigen konsistenten Stand inklusive Varianten.

Jede fachliche Variante besitzt über Revisionen hinweg einen stabilen `variant_key`.

Beispiel:

- Revision 1: `variant_key = lactose_free`
- Revision 2: dieselbe fachliche Variante behält `variant_key = lactose_free`

### 12.1 Vererbung

Varianten erben:

- Kategorie
- Basiseinheit
- Allergene
- Unverträglichkeitsauslöser
- Herkunftsmerkmale
- zutatenspezifische Umrechnungen

### 12.2 Nicht überschreibbar

Varianten dürfen nicht überschreiben:

- Kategorie
- Basiseinheit

### 12.3 Überschreibbar

Varianten können gezielt überschreiben:

- Allergenzustände
- Unverträglichkeitszustände
- Herkunftsmerkmale
- zutatenspezifische Umrechnungen

Effektiver Wert:

`Basiswert der Zutatenrevision + Varianten-Override`

### 12.4 Verwendung in Rezepten

Eine Rezeptposition referenziert immer eine konkrete veröffentlichte Zutatenrevision. Optional kann sie den stabilen `variant_key` einer Variante dieser Revision auswählen. Ohne `variant_key` wird die Basisform der Zutat verwendet. Dadurch können beispielsweise normale und laktosefreie Butter fachlich eindeutig unterschieden werden.

Eine neue Zutatenrevision verändert bestehende veröffentlichte Rezeptrevisionen nicht. Beim bewussten Aktualisieren eines Rezeptentwurfs muss eine weiterhin vorhandene Variante anhand ihres stabilen `variant_key` zugeordnet werden.

---

## 13. Regelkatalog zur Auswertung

Die Eignungen vegan, vegetarisch, pescetarisch, laktosefrei, milchfrei und glutenfrei werden aus den folgenden Eigenschaften berechnet. Sie werden nicht als direkte Eigenschaften einer Basiszutat gespeichert. Stabile Anforderungscodes dürfen weiterhin in Teilnehmer- und Rezeptregeln verwendet werden.

### 13.1 Ergebniszustände

- `compatible`
- `incompatible`
- `unknown`

### 13.2 Allergien

- `contains` → `incompatible`
- `unknown` → `unknown`
- `may_contain` → mindestens `unknown` / Review erforderlich
- `does_not_contain` → hinsichtlich dieses Merkmals `compatible`

Untertypen wirken auf ihre Obergruppe.

### 13.3 Unverträglichkeiten

Grundsätzlich dieselbe Zustandslogik wie bei Allergenen.

Mengenabhängige Schwellenwerte können später ergänzt werden.

### 13.4 Vegan

Verboten:

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

Ist ein verbotenes Merkmal `contains` → `incompatible`.

Ist eine relevante Herkunft unbekannt → `unknown`.

### 13.5 Vegetarisch

Standardmäßig verboten:

- MEAT
- POULTRY
- FISH
- CRUSTACEAN
- MOLLUSC
- INSECT
- ANIMAL_FAT
- GELATIN
- ANIMAL_RENNET
- OTHER_ANIMAL_DERIVED

Standardmäßig erlaubt:

- DAIRY
- EGG
- HONEY

### 13.6 Pescetarisch

Standardmäßig verboten:

- MEAT
- POULTRY
- INSECT
- ANIMAL_FAT
- GELATIN
- ANIMAL_RENNET
- OTHER_ANIMAL_DERIVED

Standardmäßig erlaubt:

- FISH
- CRUSTACEAN
- MOLLUSC
- DAIRY
- EGG
- HONEY

### 13.7 Laktosefrei

- `LACTOSE = contains` → `incompatible`
- `LACTOSE = does_not_contain` → hinsichtlich Laktose `compatible`
- `LACTOSE = unknown/may_contain` → `unknown`

`MILK` allein entscheidet nicht über Laktosefreiheit.

### 13.8 Milchfrei / Milchallergie

- `MILK = contains` → `incompatible`

Laktosefreiheit bedeutet nicht Milchfreiheit.

### 13.9 Glutenfrei

- `GLUTEN = contains` → `incompatible`
- `GLUTEN_CEREALS = contains` → `incompatible`
- relevante Information unbekannt → `unknown`

### 13.10 Religiöse oder zertifizierungsabhängige Anforderungen

Halal, koscher, Bio, Fair Trade oder ähnliche Eigenschaften werden nicht als pauschale Basiszutaten-Eignung modelliert.

Sie können von konkretem Produkt, Herstellungsprozess oder späteren Domänen abhängen.

---

## 14. Integritätsregeln

1. Jede Zutatenidentität besitzt eine stabile ID.
2. Fachlich veränderliche Inhalte liegen ausschließlich in Revisionen.
3. Veröffentlichte Revisionen sind unveränderlich.
4. Jede Zutat hat höchstens einen aktuellen veröffentlichten Stand.
5. Jede Basiszutat besitzt genau eine Basiseinheit je Revision.
6. Varianten können nicht ohne ihre Zutatenrevision existieren.
7. Varianten dürfen Kategorie und Basiseinheit nicht überschreiben.
8. Varianten behalten über Revisionen hinweg einen stabilen `variant_key`.
9. Fehlende Eigenschaftsdaten sind nicht gleich `does_not_contain`.
10. `unknown` gilt bei sicherheitsrelevanten Prüfungen nicht als geeignet.
11. Eine lokale Kopie wird erst bei tatsächlicher lokaler Änderung erzeugt.
12. Ein lokaler Fork verändert niemals die zentrale Zutat.
13. Zentrale Updates überschreiben lokale Forks niemals automatisch.
14. Die Übernahme zentraler Updates erzeugt immer einen neuen lokalen Entwurf.
15. Konflikte zwischen lokalen und zentralen Änderungen müssen explizit aufgelöst werden.
16. Globale Ersatzbeziehungen sind nicht Teil der Basiszutaten-Domäne.
17. Persistierte Scopes sind `central`, `tenant` und `camp`; `local` ist nur ein Sammelbegriff.
18. Jede gespeicherte Revision besitzt Name, Kategorie und Basiseinheit.
19. Ein kompatibles Ergebnis setzt ausreichend geprüfte Eigenschaftsgruppen voraus.
20. Rezeptpositionen referenzieren eine veröffentlichte Zutatenrevision und optional einen `variant_key`.
21. Archivierung betrifft die Zutatenidentität und verändert keine veröffentlichte Revision.
