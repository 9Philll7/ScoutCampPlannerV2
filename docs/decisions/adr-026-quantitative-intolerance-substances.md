# ADR-026: Quantitative Gehalte unverträglichkeitsrelevanter Stoffe

## Status

Angenommen am 16.09.2026.

## Kontext

Das bisherige Zutatenmodell behandelte Allergene und alle Unverträglichkeitsauslöser mit `contains`, `does_not_contain`, `may_contain` und `unknown`. Für dosisabhängige Stoffe ist diese Aussage zu grob: Eine spätere Bewertung hängt vom Stoffgehalt, der tatsächlich verzehrten Menge und der individuellen Anforderung ab.

## Entscheidung

Das qualitative Zustandsmodell bleibt für Allergene bestehen. Gluten wird weiterhin über `GLUTEN_CEREALS` und dessen Untertypen bewertet.

Für `LACTOSE`, `FRUCTOSE`, `FRUCTANS`, `GALACTANS`, `SORBITOL`, `MANNITOL`, `XYLITOL` und `OTHER_POLYOLS` speichert eine Zutatenrevision optionale quantitative Inhaltsstoffgehalte. Jeder Eintrag enthält Stoff-ID, Menge und Einheit, Bezugsmenge und Bezugseinheit sowie Reviewstatus. Die Quellen werden gemäß ADR-025 gemeinsam an der Zutatenrevision dokumentiert. Beispiel: `4,8 g LACTOSE pro 100 ml`.

Varianten erben diese Gehalte und können einen Gehalt vollständig überschreiben. `HISTAMINE` bleibt vorerst eine qualitative Angabe; ein universeller Histamin-Grenzwert wird nicht angenommen.

Personenbezogene Unverträglichkeiten, Grenzwerte und Toleranzen sind nicht Teil der Zutat. Sie gehören in einen separaten Anforderungs- bzw. Regelkatalog. Eine spätere Auswertung normiert den Stoffgehalt auf die tatsächlich eingesetzte Portionsmenge und vergleicht erst anschließend mit einer Anforderung. Dieses ADR legt keine medizinischen Grenzwerte fest.

## Bestehende Daten

Vorhandene qualitative Angaben zu dosisabhängigen Stoffen werden nicht in Mengen umgerechnet, weil daraus kein belastbarer Gehalt abgeleitet werden kann. Sie bleiben als Legacy-Angaben erhalten. Neue Eingaben erfolgen quantitativ; eine fachlich geprüfte Überführung muss Menge und eine revisionsweite Quellenangabe ausdrücklich ergänzen.

## Konsequenzen

- PostgreSQL und SQLite erhalten revisionsgebundene Tabellen für Stoffgehalte und Varianten-Overrides.
- Mengen sind nicht negativ; Bezugsgrößen sind positiv. Mengeneinheiten müssen Masseeinheiten sein, Bezugseinheiten müssen zur Zutatenbasiseinheit passen.
- Stoffgehalte müssen vor Veröffentlichung geprüft sein.
- Das Offline-Paket transportiert Stoffgehalte mit Schemaversion 2; Pakete der Version 1 bleiben importierbar und enthalten erwartungsgemäß noch keine quantitativen Gehalte.
- Portionsberechnung und Anforderungskatalog werden separat umgesetzt.
