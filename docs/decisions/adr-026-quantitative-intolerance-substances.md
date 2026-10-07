# ADR-026: Quantitative Gehalte unverträglichkeitsrelevanter Stoffe

## Status

Angenommen am 16.09.2026; gezielt erweitert am 23.09.2026 für Mahlzeitenplanung Inkrement 2.

## Kontext

Das bisherige Zutatenmodell behandelte Allergene und alle Unverträglichkeitsauslöser mit `contains`, `does_not_contain`, `may_contain` und `unknown`. Für dosisabhängige Stoffe ist diese Aussage zu grob: Eine spätere Bewertung hängt vom Stoffgehalt, der tatsächlich verzehrten Menge und der individuellen Anforderung ab.

## Entscheidung

Das qualitative Zustandsmodell bleibt für Allergene bestehen. Gluten wird weiterhin über `GLUTEN_CEREALS` und dessen Untertypen bewertet.

Für `LACTOSE`, `FRUCTOSE`, `FRUCTANS`, `GALACTANS`, `SORBITOL`, `MANNITOL`, `XYLITOL` und `OTHER_POLYOLS` speichert eine Zutatenrevision optionale quantitative Inhaltsstoffgehalte. Jeder Eintrag enthält Stoff-ID, Menge und Einheit, Bezugsmenge und Bezugseinheit sowie Reviewstatus. Die Quellen werden gemäß ADR-025 gemeinsam an der Zutatenrevision dokumentiert. Beispiel: `4,8 g LACTOSE pro 100 ml`.

Varianten erben diese Gehalte und können einen Gehalt vollständig überschreiben. `HISTAMINE` bleibt vorerst eine qualitative Angabe; ein universeller Histamin-Grenzwert wird nicht angenommen.

Personenbezogene Unverträglichkeiten, Grenzwerte und Toleranzen sind nicht Teil der Zutat. Sie gehören in einen separaten Anforderungs- bzw. Regelkatalog. Eine spätere Auswertung normiert den Stoffgehalt auf die tatsächlich eingesetzte Portionsmenge und vergleicht erst anschließend mit einer Anforderung. Dieses ADR legt keine medizinischen Grenzwerte fest.

## Bestehende Daten

Vorhandene qualitative Angaben zu dosisabhängigen Stoffen werden nicht automatisch in Mengen umgerechnet. Unbekannte Werte werden nicht als sichere Negativangaben migriert. Eine fachlich geprüfte quantitative Überführung muss Menge, Bezugsgröße und eine revisionsweite Quellenangabe ausdrücklich ergänzen.

## Bewertungsmodi (Erweiterung Inkrement 2)

Für jede konkrete Zutatenrevision/Stoff-Zuordnung gilt genau ein Modus:

- `Quantitative`: bevorzugt, wenn ein belastbarer Gehalt pro definierter Bezugsmenge bekannt ist.
- `Qualitative`: enthalten oder nicht enthalten, wenn nur diese Aussage belastbar bekannt ist. Auch für neue Eingaben regulär zulässig, nicht nur für Legacy-Daten.
- `Unknown`: keine belastbare Aussage; bedeutet weder enthalten noch nicht enthalten.

Fehlende Stoffmengen werden nicht erfunden oder geschätzt. Qualitative Angaben erhalten keine künstliche Menge. Allergene, Gluten, Histamin und die Trennung von personenbezogenen Grenzwerten bleiben wie oben definiert. Diese Erweiterung ersetzt ausschließlich die frühere Beschränkung neuer Eingaben auf quantitative Werte.

## Konsequenzen

- PostgreSQL und SQLite erhalten revisionsgebundene Tabellen für Stoffgehalte und Varianten-Overrides.
- Mengen sind nicht negativ; Bezugsgrößen sind positiv. Mengeneinheiten müssen Masseeinheiten sein, Bezugseinheiten müssen zur Zutatenbasiseinheit passen.
- Stoffgehalte müssen vor Veröffentlichung geprüft sein.
- Das Offline-Paket transportiert Stoffgehalte mit Schemaversion 2; Pakete der Version 1 bleiben importierbar und enthalten erwartungsgemäß noch keine quantitativen Gehalte.
- Portionsberechnung und Anforderungskatalog werden separat umgesetzt.
