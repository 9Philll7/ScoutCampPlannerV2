# Gemeinsamkeitspräferenzen: Zielverhalten und verbleibende Berechnungslücken

Stand: 2026-11-01. Die Backend-/API-Anpassung ist inzwischen ausdrücklich freigegeben und umgesetzt. Aktueller Vertrag: `docs/architecture/cooking-together-api.md`. Die unten aufgeführten Lücken beschreiben den vorherigen Analysezustand und dienen als Abnahmefälle; sie sind keine aktuelle Beschränkung mehr.

## Zielverhalten

- **Gemeinsam:** Pro Komponente möglichst eine gemeinsame Versorgung anbieten. Ein gemeinsames Rezept bzw. eine gemeinsame Zutatenentscheidung deckt alle betroffenen Anforderungen ab. Gemeinsame Alternativen werden auf Komponentenebene gewählt, ohne den dadurch ersetzten Standardanteil als zusätzliche Lösung darzustellen. Möglichst viel zusammenfassen. Ein echter Gruppenkonflikt liegt vor, wenn für diese Gruppe weder Standard noch Variante noch Alternative verfügbar ist.
- **Getrennt:** Das Standardrezept dort beibehalten, wo es passt. Eine eindeutig passende Variante oder Alternative verwenden. Bleiben mehrere Möglichkeiten, pro Anforderungsgruppe entscheiden lassen; ohne mögliche Lösung einen Gruppenkonflikt darstellen. Unabhängige Gruppenentscheidungen bleiben unabhängig, auch bei identischen Rezeptoptionen. Identische bestätigte Rezepte dürfen gemäß bestehender Merge-Semantik zusammen gespeichert werden, ohne die Gruppenzuordnungen zu verlieren.
- **Fragen:** Nur bei unterschiedlichen Ergebnissen nach der Richtung fragen. Bei identischen Rezepten, Portionen, Anpassungen und Auswahlräumen entfällt die Abfrage. Unterschiedliche Wege mit kurzer Vorschau und ausdrücklicher Bestätigung anbieten. Nach Bestätigung verschwindet die Versorgungsart aus der Liste; ändern nur unter „Weitere Anpassungen“. Der Rezeptreset erhält die gewählte Präferenz. Bestätigung speichert und berechnet automatisch neu; Verifikation bleibt separat.

## Frontend-Stand

- Die tatsächlich konfigurierte Präferenz wird im Versorgungsbereich angezeigt.
- Gemeinsam geprüfte, im Berechnungsergebnis gelieferte Alternativen besitzen eine Komponentenentscheidung; ersetzte Standardanteile werden nicht parallel als aktive Lösung angezeigt.
- Bei „Getrennt“ werden passende Rezeptalternativen nicht mehr zu einer gruppenübergreifenden Auswahl zusammengezogen. Eindeutige, bereits berechnete Lösungen bleiben sichtbar; Probleme sind nach Anforderungen zusammengefasst.
- Vorhandene `commonSupplyChoices` werden direkt mit gemeinsam/getrennt, Rezepten, Portionen und Anpassungsanzahl dargestellt. Ohne manuelle Entscheidung ist kein Radio vorausgewählt. Umschalten ersetzt exklusiv den betroffenen Pfad; Bestätigung speichert und berechnet neu. Der allgemeine Lösungseditor wird dafür nicht benötigt.
- Speichern vereinigt nur identische Revision, Komponente, Zutatenentscheidungen und Herkunft. Verschiedene Portionenzuordnungen bleiben als Allokationen erhalten.

## Backend-Lücken für vollständige Erfüllung

1. **„Fragen“ ist derzeit bedingt.** `OperationalSupplyCalculator` erzeugt `CommonSupplyChoices` nur in bestimmten Fällen: beispielsweise unterschiedliche geeignete Einzellösungen und eine passende gemeinsame Lösung. Bei identischen Lösungen, nur einer vollständig geeigneten Richtung, nur einer Anforderungsgruppe oder noch offenen Einzelentscheidungen wird keine vollständige Abfrage geliefert. Teilweise wird unter „Ask“ bereits eine gemeinsame Lösung gewählt (`common.Selected` bei nicht geeigneter getrennter Versorgung). Diese Auswahl darf nicht durch reines Verstecken im Frontend als „noch nicht entschieden“ ausgegeben werden: gespeicherter Status, Mengen und Verifikation würden widersprechen.
2. **Variantenkandidaten sind nicht zuverlässig klassifiziert.** `SupplySolutionResolver` liefert in `Candidates` gemeinsam geeignete direkte Varianten, geeignete Rezeptalternativen und nur teilweise verbesserte Varianten. Der Vertrag kennzeichnet die Eignung einzelner Kandidaten nicht. Bei direkter Variantenmehrdeutigkeit stoppt die Suche nach zwei Treffern; die Liste muss daher auch nicht vollständig sein. Das Frontend kann aus `MultipleDirectSolutions` allein nicht sicher alle Kandidaten als passende Varianten ausgeben. Für die direkte Auswahl bestätigter Rezeptalternativen werden deshalb explizite Planalternativen und deren Mehrdeutigkeitsdiagnose verwendet; unklassifizierte Varianten bleiben im allgemeinen Editor.
3. **„Gemeinsam“ braucht eine definierte verbleibende Gruppierung.** Existiert keine einzige Lösung für alle, bleiben derzeit vorhandene Einzellösungen stehen. Ein maximal gemeinsamer Versorgungsplan für kompatible Teilmengen wird nicht ausdrücklich geliefert. Der gewünschte Fall „möglichst viel gemeinsam, Konflikt nur für tatsächlich unversorgbare Gruppen“ verlangt eine Berechnungsregel, einschließlich Umgang mit widersprüchlichen Anforderungen und mehreren gleichwertigen Gruppierungen.
4. **Vergleichsszenarien fehlen in Problemfällen.** Für eine immer verfügbare Frage muss das Backend gemeinsame und getrennte Szenarien auch bei offenen Entscheidungen liefern: geeignete Optionen, betroffene Gruppen, verbleibende Konflikte und gegebenenfalls nicht verfügbare Richtung. Keine fehlenden Szenarien oder Eignung im Frontend erfinden.

## Backend-Abnahmefälle

- Gemeinsam: Standard passt für sieben Portionen; zwei Alternativen passen für alle zehn. Eine gemeinsame Auswahl für zehn, kein zusätzlicher Standardanteil.
- Gemeinsam: eine Gruppe ist mit keiner Variante/Alternative versorgbar; übrige Gruppen werden möglichst gemeinsam geplant, genau die unversorgbare Gruppe bleibt als Konflikt.
- Getrennt: Standard passt für sieben; für zwei besteht eine eindeutige Variante und für eine zwei geeignete Alternativen. Standard + eindeutige Variante + eine Auswahl für die verbleibende Gruppe.
- Getrennt: zwei Gruppen haben dieselben zwei geeigneten Alternativen. Zwei unabhängige Entscheidungen; bei gleichem gewähltem Rezept ein gespeicherter Rezepteintrag mit beiden Allokationen.
- Fragen: beide Richtungen eindeutig; beide identisch; nur gemeinsam möglich; nur getrennt möglich; eine Gruppe; mehrere offene Varianten; keine Richtung vollständig möglich. Jeweils keine automatische fachliche Wahl vor Bestätigung, verständliche Vorschau und ehrliche verbleibende Konflikte.
- Manuelle Entscheidungen, ursprüngliche Supply-Version, Freeze, Health-/Verifikationsrechte und Verifikationsveraltung unverändert schützen.
