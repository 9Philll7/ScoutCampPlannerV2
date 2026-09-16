# ADR-025: Externe Datenquellen als Vorschlagshilfe für Zutaten

## Status

Accepted

## Kontext

ScoutCampPlanner benötigt keine möglichst große Kopie externer
Lebensmitteldatenbanken. Zutaten werden in einem fachlichen Kontext für eine
Organisation oder ein Lager angelegt, geprüft und bei Bedarf an den zentralen
Zutatenstamm weitergegeben. Ein automatischer Import tausender BLS-Einträge
würde diesen Stamm unübersichtlich machen und trotzdem keine verlässlichen
Allergen-, Herkunfts- oder FODMAP-Angaben liefern.

Bei der manuellen Erfassung sind vorhandene Referenzwerte dennoch hilfreich.
Der Bundeslebensmittelschlüssel (BLS) 4.0 bietet allgemeine
Lebensmittel-Nährwerte. Open Food Facts bietet produktbezogene Angaben, deren
Vollständigkeit und Qualität je Artikel schwanken kann.

## Entscheidung

### Kein externer Zutatenimport

- BLS und Open Food Facts erzeugen weder zentrale noch lokale Zutaten.
- Es gibt keinen Batch-Import eines BLS-Kernkatalogs.
- Die ScoutCampPlanner-Datenbank speichert nur bewusst angelegte Zutaten und
  deren Revisionen.
- Eine externe Quelle darf vorhandene veröffentlichte Revisionen niemals
  automatisch aktualisieren.

### Vorschlagsworkflow

1. Eine Person legt eine Zutat selbst an oder öffnet ihren Entwurf.
2. Im Editor sucht sie gezielt in einer externen Referenzquelle.
3. Treffer werden mit Bezeichnung, Bezugsmenge, Quelle und verfügbaren Werten
   als Vorschau gezeigt.
4. Die Person wählt einen Treffer und die tatsächlich zu übernehmenden Werte
   bewusst aus.
5. Übernommene Werte werden als Schätzung in den Entwurf geschrieben. Sie
   gelten dadurch nicht als fachlich geprüft und werden nie automatisch
   veröffentlicht.

Ein fehlender externer Wert bleibt unbekannt. Er wird nicht als `0` und nicht
als `does_not_contain` interpretiert. Aus Produktnamen oder Zutatenlisten werden
keine medizinisch relevanten Stoffgehalte erfunden.

### BLS 4.0

- Die vollständige BLS-Liste dient als read-only Such- und Vorschlagsquelle,
  nicht als ScoutCampPlanner-Zutatenstamm.
- Die Originaldaten oder ein daraus erzeugter kompakter Suchindex liegen
  außerhalb der fachlichen Produktdatenbank.
- Die Vorschläge übernehmen nur tatsächlich vorhandene, eindeutig zuordenbare
  Werte samt ursprünglicher Bezugsbasis.
- Die Attribution nennt mindestens den BLS, Version 4.0, das Max
  Rubner-Institut und den BLS-Code.
- Der BLS kann lokal bereitgestellt werden und bleibt damit auch ohne
  Internetverbindung nutzbar.

Die BLS-4.0-Daten werden vom Max Rubner-Institut unter CC BY 4.0 bereitgestellt.

### Open Food Facts

- Open Food Facts ist eine optionale Online-Produktsuche, keine Voraussetzung
  für den Zutateneditor und kein Offline-Stammdatensatz.
- Gesucht wird bewusst per Suchbegriff oder Barcode. Es gibt keine Suche bei
  jedem Tastendruck.
- Der Zugriff erfolgt serverseitig mit identifizierendem `User-Agent`, kleinen
  Ergebnismengen, Cache und Beachtung der veröffentlichten Rate-Limits.
- Open-Food-Facts-Angaben gelten als produktbezogene Schätzgrundlage. Sie
  ersetzen keine fachliche Prüfung und keine Allergen- oder
  Unverträglichkeitsbewertung.
- Die dauerhafte Übernahme muss Open Food Facts als Quelle ausweisen. Vor dem
  produktiven Einsatz muss geklärt sein, wie Attribution und Share-Alike der
  ODbL für gespeicherte beziehungsweise exportierte Daten erfüllt werden.

### Quellen und Schätzstatus

- Quellen werden zusammengefasst für die gesamte Zutatenrevision geführt,
  nicht wiederholt an jedem einzelnen Nährwert- oder Inhaltsstofffeld.
- Eine Revision kann mehrere Quellen enthalten, beispielsweise BLS für
  allgemeine Nährwerte und eine Herstellerangabe für ein konkretes Produkt.
- Die Zusammenfassung muss Quelle, Referenz beziehungsweise Datensatz und bei
  externen Quellen deren Version oder Abrufzeitpunkt nachvollziehbar machen.
- Durch externe Vorschläge ausgefüllte Nährwerte und Inhaltsstoffgehalte werden
  im Editor und in daraus berechneten Rezeptinformationen sichtbar als
  Schätzung gekennzeichnet.
- `Reviewed` bedeutet, dass die Übernahme fachlich kontrolliert wurde. Es macht
  einen geschätzten Referenzwert nicht zu einer medizinisch exakten Messung.

## Konsequenzen

- Der begonnene BLS-Katalogimport und seine Importoberfläche entfallen.
- Der Zutateneditor erhält eine vom eigentlichen Speichern getrennte
  Vorschlagssuche mit selektiver Übernahme.
- BLS- und Open-Food-Facts-Adapter bleiben Infrastructure-Komponenten hinter
  einem gemeinsamen Application-Contract.
- Ein Ausfall oder eine Drosselung von Open Food Facts blockiert weder das
  Bearbeiten noch das Speichern einer Zutat.
- Quellenmetadaten werden revisionsweit modelliert und zusammen mit
  Offline-Paketen transportiert.
- Vor der Open-Food-Facts-Produktivfreigabe sind Lizenzdarstellung,
  Exporthinweise und Cache-Regeln gesondert zu verifizieren.

## Nicht entschieden

- Medizinische Grenzwerte oder individuelle Toleranzen
- automatische Ableitungen von FODMAP-Gehalten aus Zutatenlisten
- eine automatische Veröffentlichung vorgeschlagener Werte
- ein regelmäßiger Vollimport von Open Food Facts

## Referenzen

- [BLS-Download und Lizenz](https://blsdb.de/download)
- [Open Food Facts API](https://openfoodfacts.github.io/documentation/docs/Product-Opener/api/)
- [Open Food Facts Bedingungen zur API-Nutzung](https://support.openfoodfacts.org/help/en-gb/12-donnees-api/94-y-a-t-il-des-conditions-pour-utiliser-l-api)
