# Strukturkonflikte und geführte Korrektur

Die Konfliktseite trennt Ernährung/Rezeptentscheidungen von Strukturzuordnungen. Ernährungskonflikte öffnen direkt die betroffene Mahlzeit. Mehrfachzuordnungen werden nach Strukturbereich und beteiligten Kocheinheiten über aktive Mahlzeiten zusammengefasst.

## Ablauf

1. Ursache und betroffene Kocheinheiten/Mahlzeiten prüfen.
2. Versorgende Kocheinheit bestimmen. Die Standardzuordnung oder ausgewählte Mahlzeiten anpassen.
3. Zuordnungsänderungen und Vorschau prüfen. Neue Lücken oder Mehrfachzuordnungen verhindern die Übernahme. Bereits offene, andere Fälle dürfen bestehen bleiben.
4. Änderungen ausdrücklich bestätigen. Danach betroffene Versorgung neu berechnen.

Die Korrektur wählt den betroffenen Strukturbereich bei den weiteren beteiligten Kocheinheiten ab. Wird ein Überknoten aufgeteilt, bleiben die übrigen Unterbereiche erhalten. Standardkorrekturen erhalten vorhandene Mahlzeitenabweichungen und zeigen verbleibende Konflikte in der Vorschau. Bei einer mahlzeitenspezifischen Abwahl aller Strukturbereiche wird diese Kocheinheit für die Mahlzeit als entfallend markiert, da eine leere Abweichung im bestehenden Modell die Standardzuordnung verwendet.

## API

- `GET /api/camps/{campId}/structure-conflicts` liefert gebündelte Fälle und einen aktuellen Versionstoken.
- `POST /api/camps/{campId}/structure-conflicts/preview` nimmt `expectedVersion`, `conflictId`, `keepCookingUnitId`, `useDefaults` und `mealIds` entgegen und liefert Zuordnungsänderungen sowie behobene, verbleibende und neue Konflikt-Mahlzeiten. Die Vorschau schreibt keine Daten.
- `POST /api/camps/{campId}/structure-conflicts/apply` prüft denselben Auftrag erneut und speichert alle Änderungen gemeinsam in einer serialisierbaren Datenbanktransaktion. Ein veralteter Stand wird mit `structure_conflict_changed` abgewiesen.

Die Konflikterkennung berücksichtigt Strukturhierarchie, Anwesenheit und wirksame Verpflegungsfilter. Bei tatsächlichen Teilnehmern stammen die Personenzahlen aus der minimierten Verpflegungsprojektion. Bei Schätzplanung werden keine tatsächlichen Personenzahlen behauptet. Leserechte, Bearbeitungsrechte und gegebenenfalls Rechte für Teilnehmeranforderungen werden an der API geprüft. Es ist keine Datenbankschemaänderung erforderlich.

## Prüfung

Gezielte Tests decken gebündelte Mahlzeiten, unveränderte Geschwistergruppen, vorhandene Overrides, komplementäre Filter, neue Versorgungslücken, ausgewählte Mahlzeiten, veraltete Aufträge und die gemeinsame Speicherung ab. UI-Tests prüfen den direkten Mahlzeitensprung, Filter, Bestätigung, Rechte und den Fall einer bereits gespeicherten Korrektur mit fehlgeschlagenem Neuladen.

Die visuelle Abnahme wurde bei 1920×1080 und 390×844 durchgeführt. Der Mehrfachzuordnungsfall wurde in einer separaten Datenbankkopie geprüft, einschließlich einer Auswahl von 17 aus 18 Mahlzeiten, der Erkennung einer neu entstehenden Lücke und der erfolgreichen gemeinsamen Übernahme. Der direkte Ernährungskonflikt-Einstieg öffnete die betroffene Kocheinheit und Mahlzeit.

## Fehlende Versorgung direkt zuordnen

Auch Fälle mit fehlender Versorgung starten den Assistenten. Im zweiten Schritt kann eine bestehende Kocheinheit gewählt werden. Die voreingestellte Anpassung gilt für die betroffenen Mahlzeiten. Alternativ lässt sich die Standardzuordnung ergänzen. Verpflegungsfilter und andere Mahlzeiten werden serverseitig in der Vorschau berücksichtigt.

Ist die gewählte Kocheinheit für eine betroffene Mahlzeit entfallen, zeigt die Vorschau die Wiederaufnahme der Versorgung ausdrücklich an. Bei Übernahme wird die Mahlzeit auf Plan folgen gestellt. Eigene gespeicherte Rezeptentscheidungen und Bedarfsabweichungen werden dabei erhalten. Eine wirkungslose Zuordnung kann nicht übernommen werden.

Fehlt den Personen selbst ein gültiger Strukturbereich, zeigt der Assistent die erforderliche Teilnehmerzuordnung als Ursache an. Eine Kocheinheitenzuordnung setzt diesen Strukturbereich voraus.

## Bewusst entfallene Versorgung und gemeinsame Konfliktquelle

Eine ausdrücklich als nicht versorgt gespeicherte Mahlzeit erzeugt für die betroffenen Personen keine Meldung über fehlende Strukturzuordnung. Die Ausnahme berücksichtigt den wirksamen Strukturbereich und Verpflegungsfilter. Eine gleichzeitig aktive andere Kocheinheit wird weiterhin normal geprüft. In einer Lösungsvorschau bleiben unbeabsichtigt neu entstehende Versorgungslücken sichtbar.

Übersicht und Konfliktseite verwenden denselben strukturierten Konfliktstand. Komplementäre Verpflegungsfilter ergeben zusammen eine eindeutige Abdeckung. Echte Mehrfachzuordnungen werden in beiden Ansichten angezeigt. Überdeckungen und nicht zugeordnete Personenmengen einer Rezeptlösung erscheinen als Versorgungskonflikte mit direktem Mahlzeiteneinstieg.

## Lokale und lagerweite Teilnehmerprobleme

Unzugeordnete Personen erscheinen ausschließlich in der zentralen Konfliktliste. Sie beeinflussen die Berechnungsgrundlage, den Konfliktstatus und die Verifikation fremder Kocheinheiten nicht. Mehrfachzuordnungen blockieren nur die Kocheinheiten, die die betroffenen Personen nach Struktur und Filter tatsächlich erfassen. Bereits gespeicherte globale Warntexte werden aus den operativen Mahlzeitenansichten entfernt. Alte unvollständige Berechnungen werden durch eine Neuberechnung auf den lokalen Prüfstand gebracht.

## Mehrere versorgende Kocheinheiten

Im Assistenten lassen sich mehrere Kocheinheiten auswählen. Die Auswahl zeigt deren Standard-Verpflegungsfilter an. Die Vorschau prüft die tatsächlich wirksamen Filter für jede Mahlzeit. Komplementäre Kombinationen wie Sonderverpflegung und Standardverpflegung sind möglich.

Die API erhält die Auswahl als `cookingUnitIds`. Der bisherige Einzelwert `keepCookingUnitId` bleibt für bestehende Aufrufer als Rückfall erhalten. Die ausgewählten Kocheinheiten erhalten den Strukturbereich. Bei einer Mehrfachzuordnung wird er bei den weiteren betroffenen Kocheinheiten entfernt. Alle Änderungen werden gemeinsam gespeichert.

`overlappingMeals` kennzeichnet verbleibende Mehrfachversorgung innerhalb der wirksam geänderten Mahlzeiten. Eine solche Auswahl wird beim Speichern serverseitig abgewiesen. Bestehende, unveränderte Mahlzeitenabweichungen werden weiterhin separat als verbleibende Fälle angezeigt.

Die Konfliktlösung prüft zusätzlich die mögliche Abdeckung der Verpflegungsfilter unabhängig von aktuell erfassten Personen. `overlappingFilters` sperrt die Übernahme bei Überschneidungen. Bei Standardzuordnungen werden die Standardfilter und bei betroffenen Mahlzeiten die wirksamen Filter geprüft. Nur Sonderverpflegung und nur Standardverpflegung lassen sich kombinieren. Alle Teilnehmer kann mit keinem weiteren Filter kombiniert werden.
