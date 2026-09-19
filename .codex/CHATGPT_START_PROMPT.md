# Startnachricht für eine neue ChatGPT-Planungsphase

## Zu sendende Nachricht

```text
Du unterstützt mich bei der fachlichen und technischen Planung des Projekts
ScoutCampPlanner.

Ich habe dir die aktuelle Projektübergabe, die verbindlichen Projektregeln und
die für die nächste Phase relevanten Architektur- und Domänendokumente
hochgeladen.

Wichtige Arbeitsregeln:

- Die hochgeladenen ADRs sowie die Architektur- und Domänendokumentation sind
  verbindlicher als Annahmen aus unserem Chat.
- Die Datei CHATGPT_PROJECT_BRIEF.md ist die kompakte Zusammenfassung des
  aktuellen Implementierungsstands, ersetzt aber keine fachliche Entscheidung.
- Erfinde keine bereits fehlenden Fachregeln, medizinischen Grenzwerte oder
  Architekturentscheidungen.
- Wenn sich Dokumente widersprechen, benenne den Widerspruch und schlage eine
  konkrete Auflösung vor.
- Implementiere keinen Code.
- Entwickle mit mir zuerst eine klar abgegrenzte Phase.
- Behandle offene Entscheidungen möglichst einzeln. Erkläre jeweils kurz die
  sinnvollen Varianten, ihre Auswirkungen und deine Empfehlung, bevor du mich
  entscheiden lässt.
- Stelle nur Fragen, die den fachlichen Umfang oder die Architektur tatsächlich
  verändern. Technische Detailentscheidungen innerhalb der bestehenden Regeln
  darfst du begründet empfehlen.

Als nächste mögliche Phase soll die Mahlzeitenplanung untersucht werden. Dabei
geht es voraussichtlich um die Zuordnung veröffentlichter Rezeptrevisionen zu
konkreten Lagermahlzeiten, Catering-eigene Koch-/Verpflegungseinheiten und die
spätere Auswahl von Zutatenvarianten oder Ersatzzutaten für einen konkreten
Nutzungskontext.

Prüfe zuerst:

1. Welche Teile davon bereits verbindlich definiert oder implementiert sind.
2. Welche Fachentscheidungen vor einer Implementierung fehlen.
3. Welche Auswirkungen die Phase auf Camp, Catering, Berechtigungen,
   Offlinepakete und unveränderliche Revisionen hat.
4. Welche Punkte ausdrücklich nicht Teil des ersten Inkrements sein sollten.

Beginne mit einer kurzen Bestandsaufnahme und nenne anschließend nur die erste
offene Entscheidung. Fahre Entscheidung für Entscheidung fort.

Wenn alle wesentlichen Punkte bestätigt sind, erstelle:

1. eine kompakte Phasenspezifikation mit Ziel, Fachregeln, Nicht-Zielen,
   Akzeptanzfällen und offenen späteren Punkten;
2. die notwendigen Vorschläge für Dokumentationsänderungen oder ein neues ADR;
3. einen eigenständigen Codex-Implementierungsauftrag, der auf konkrete
   Repository-Dateien verweist, Tests und Migrationen berücksichtigt, einen
   manuellen Prüfablauf enthält und einen sinnvollen Commit-Punkt nennt.

Der Codex-Auftrag darf nur bestätigte Entscheidungen enthalten.
```

## Dokumente zum Hochladen

### Kleiner Pflichtsatz für jeden neuen Planungschat

1. `.codex/CHATGPT_PROJECT_BRIEF.md`
2. `.codex/agent-instructions.md`
3. `.codex/coding-guidelines.md`
4. `docs/architecture/architecture-overview.md`
5. `docs/architecture/dependency-matrix.md`
6. `docs/architecture/baseline-status.md`

Dieser Satz reicht für die erste Orientierung. Historische Spike-Berichte und
alte Implementierungs-Hand-offs müssen nicht standardmäßig hochgeladen werden.

### Zusätzlich für die nächste Phase „Mahlzeitenplanung“

7. `docs/domain/ingredients-and-recipes.md`
8. `docs/domain/camp-structure.md`
9. `docs/domain/roles-and-tenants.md`
10. `docs/domain/RECIPE_DOMAIN_MODEL.md`
11. `docs/domain/RECIPE_VALIDATION_AND_CALCULATION_RULES.md`
12. `docs/architecture/package-format.md`
13. `docs/decisions/adr-015-recipe-distribution-scopes.md`
14. `docs/decisions/adr-016-recipe-standard-portion-normalization.md`
15. `docs/decisions/adr-019-recipe-permissions.md`
16. `docs/decisions/adr-021-revisioned-base-ingredients.md`
17. `docs/decisions/adr-023-ingredient-variant-selection.md`
18. `docs/decisions/adr-024-nutrition-profiles.md`
19. `docs/decisions/adr-026-quantitative-intolerance-substances.md`

### Nur bei Bedarf nachreichen

- `docs/decisions/adr-018-ingredient-and-conflict-catalog-scopes.md`, wenn
  Eigentum oder Sichtbarkeit der Zutatenkataloge erneut relevant wird.
- `docs/decisions/adr-020-ingredient-management-permissions-and-search.md`, wenn
  der Plan Zutatenanlage oder Suchprioritäten verändert.
- `docs/decisions/adr-022-central-ingredient-contributions.md`, wenn die Phase
  zentrale Zutatenfreigaben berührt.
- `docs/architecture/Basiszutaten_Datenbankarchitektur.md`, wenn konkrete
  Persistenzbeziehungen zu Zutaten geplant werden.
- `docs/domain/participants-and-health-data.md`, sobald aus anonymen
  Planungszahlen personenbezogene Anforderungen werden sollen.
- `docs/decisions/adr-007-remaining-architecture-risks.md` und
  `docs/decisions/adr-012-security-audit-events.md`, sobald sensible Daten,
  Paketverschlüsselung oder neue sicherheitskritische Aktionen betroffen sind.

## Nicht standardmäßig hochladen

- `docs/spike/` – historischer Validierungsnachweis
- `.codex/architecture-spike.md` – abgeschlossener Spike-Auftrag
- `.codex/BASISZUTATEN_NEXT_STEPS.md` – historischer Implementierungsverlauf
- `docs/architecture/recipe-implementation-analysis.md` – historische
  Vorimplementierungsanalyse
- generierte Migrationen, Designer-Dateien oder Quellcode, solange eine
  fachliche Phase entwickelt wird

Wenn ChatGPT eine Behauptung zum aktuellen Code verifizieren muss, sollte nicht
vorsorglich das ganze Repository hochgeladen werden. Stattdessen wird gezielt
die betroffene API-, Domain-, Application- oder Testdatei nachgereicht.
