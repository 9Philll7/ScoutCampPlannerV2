# ADR-024: Revisionsgebundene Nährwertprofile für Zutaten

## Status

Accepted

## Kontext

Rezepte sollen zusätzlich zu Mengen, Konflikten und Einheiten auch geschätzte
Nährwerte anzeigen können. Dafür müssen die Ausgangswerte an den unveränderlichen
Stand einer veröffentlichten Zutat gebunden sein. Ein Wert von `0` darf nicht
verwendet werden, wenn ein Nährwert lediglich unbekannt ist.

Die EU-Nährwertdeklaration verwendet als Kernwerte Energie, Fett, gesättigte
Fettsäuren, Kohlenhydrate, Zucker, Eiweiß und Salz. Angaben beziehen sich
grundsätzlich auf 100 g oder 100 ml; zusätzliche Portionsangaben sind möglich.
Dieses Format dient dem ScoutCampPlanner als fachliche Orientierung. Die
Anwendung erstellt damit keine rechtsverbindliche Lebensmittelkennzeichnung und
keine medizinische Ernährungsberatung.

## Entscheidung

### Zuordnung und Lebenszyklus

Ein Nährwertprofil gehört zu einer konkreten Zutatenrevision. Es ist optional
und nimmt am bestehenden Entwurfs-, Prüf-, Veröffentlichungs-, Fork- und
Einreichungsprozess der Zutatenrevision teil. Veröffentlichte Nährwertprofile
sind dadurch unveränderlich; Änderungen erzeugen eine neue Zutatenrevision.

Das Fehlen eines Nährwertprofils verhindert die Veröffentlichung einer Zutat
nicht. Sobald ein Profil als geprüft markiert wird, müssen alle Kernwerte
vollständig und gültig sein. Unbekannte Angaben bleiben `null` und werden nie
als `0` interpretiert.

### Bezugsmenge

Das Profil besitzt eine Bezugsmenge und eine Referenzeinheit, die mit der
Basiseinheit der Zutat kompatibel sein muss:

- Masse: standardmäßig 100 g
- Volumen: standardmäßig 100 ml
- Stück: standardmäßig 1 Stück

Bestehende allgemeine und zutatenspezifische Einheitenumrechnungen werden für
die Rezeptberechnung wiederverwendet. Eine eigene parallele
Nährwert-Umrechnungslogik wird nicht eingeführt.

### Werte

Der erste Umfang enthält:

- Energie in kJ; kcal werden für die Anzeige aus kJ berechnet
- Fett in g
- davon gesättigte Fettsäuren in g
- Kohlenhydrate in g
- davon Zucker in g
- Eiweiß in g
- Salz in g
- optional Ballaststoffe in g

Alle Werte müssen größer oder gleich `0` sein. Gesättigte Fettsäuren dürfen den
Fettwert und Zucker darf den Kohlenhydratwert nicht überschreiten.

### Herkunft und Verlässlichkeit

Ein Profil hält mindestens die Art der Quelle, eine lesbare Quellenangabe und
den Prüfstatus fest. Als Quellenarten werden zunächst Herstellerangabe,
offizielle Lebensmitteldatenbank und manuelle Schätzung unterschieden. Ein
optionales Bezugs- oder Prüfdatum darf ergänzt werden.

Importe aus externen Nährwertdatenbanken erfolgen nicht automatisch. Vor einem
späteren Import müssen Datenqualität, Versionierung und Lizenzbedingungen der
konkreten Quelle geprüft werden.

### Varianten

Eine Zutatenvariante erbt das vollständige Nährwertprofil ihrer
Zutatenrevision. Falls ihre Werte fachlich abweichen, ersetzt ein vollständiges
eigenes Variantenprofil das geerbte Profil. Einzelne Nährwertfelder werden nicht
isoliert überschrieben, damit keine rechnerische Mischung aus unterschiedlichen
Quellen entsteht.

Die Auswahl einer Variante bleibt gemäß ADR-023 Aufgabe der späteren
Verpflegungsplanung. Dort muss eine gewählte Variante auch die berechneten
Nährwerte der betroffenen Verpflegungs- oder Kocheinheit ersetzen.

### Berechnung in Rezepten

Ein Rezept speichert keine veränderlichen Nährwertsummen. Die Werte werden aus
den referenzierten Zutatenrevisionen, Rezeptmengen und Einheitenumrechnungen
berechnet. Angezeigt werden dürfen insbesondere:

- Gesamtwerte des Rezepts
- Werte pro Standardportion
- später Werte pro Verpflegungs- oder Kocheinheit

Ein Ergebnis ist nur dann vollständig, wenn alle berücksichtigten Zutaten über
ein vollständiges, geprüftes Profil und eine erforderliche Umrechnung verfügen.
Andernfalls wird das Ergebnis ausdrücklich als unvollständig gekennzeichnet und
nennt die fehlenden Zutaten. Fehlende Werte werden nicht mit `0` summiert.

Die Ergebnisse werden als Schätzung bezeichnet. Automatische gesundheitliche
Bewertungen, Ampeln, Diätvorgaben und medizinische Grenzwertwarnungen sind nicht
Teil dieser Entscheidung.

## Konsequenzen

- Zutatenrevisionen und Varianten benötigen neue, providerunabhängige
  Persistenzstrukturen und Migrationen für PostgreSQL und SQLite.
- Zutateneditor, Veröffentlichung, Forks, zentrale Einreichungen und
  Offline-Pakete müssen Nährwertprofile berücksichtigen.
- Die Rezeptberechnung benötigt einen nachvollziehbaren Vollständigkeitsstatus.
- Bestehende Zutaten bleiben gültig und besitzen zunächst kein Nährwertprofil.
- Vitamine, Mineralstoffe, Fettsäure-Untergruppen und weitere Detailwerte können
  später ergänzt werden, gehören aber nicht zum ersten Inkrement.

## Referenzen

- [EU-Kommission: Nutrition labelling](https://food.ec.europa.eu/food-safety/labelling-and-nutrition/food-information-consumers-legislation/nutrition-labelling_en)
- [Verordnung (EU) Nr. 1169/2011](https://eur-lex.europa.eu/eli/reg/2011/1169/ojv)
