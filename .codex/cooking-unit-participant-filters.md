# Implementierungsvorbereitung: konfigurierte Teilnehmerfilter

Stand: 2026-10-20. READY_FOR_IMPLEMENTATION – einschließlich Schätzplanung/Fallback fachlich bestätigt. Keine Codeänderungen. Kein Commit beauftragt.

## Verbindliche Grundlagen

- `.codex/agent-instructions.md`, `.codex/coding-guidelines.md`
- `docs/domain/CookingUnit_Participant_Filters.md` (bestätigtes Zielbild einschließlich Fallback-Grenze)
- `docs/decisions/meal-planning-participant-structure-correction.md`
- `docs/domain/catering-meal-planning.md`, `docs/domain/catering-meal-planning-increment-2.md`
- ADR-008 (Migrationen), ADR-011 (Rechte), ADR-012 (Audit)
- `docs/decisions/meal-planning-increment-2-offline-sensitive-data.md`
- bestehender Visual Design Guide; kein allgemeiner UX-Umbau.

## Tatsächlicher Ausgangsstand

- Arbeitsbaum bei Vorbereitung sauber; UX-Checkpoint vom Benutzer committed.
- `Catering.Domain/CookingUnitParticipantFilter.cs`: ausschließlich All/SpecialCateringOnly/WithoutSpecialCatering.
- `Catering.Domain/MealPlanning.cs`: CookingUnit speichert ParticipantFilter und TogetherPreference; keine gespeicherten benannten Filter.
- `Catering.Application/MealPlanning/StructureParticipantProjection.cs`: gemeinsame Struktur-/Anwesenheits-/Filterauswahl und Überlappungserkennung.
- `MealPlanningService.cs` verwendet diese Projektion beim Konfigurieren und Berechnen; `MealPlanningParticipants.cs` bildet RequirementGroups, Fallback und relevante Signaturen.
- `Camp.Contracts/ParticipantContracts.cs`: ParticipantPlanningData enthält CampStageId/Category bereits. Die minimierte CateringParticipantData-Projektion enthält beide noch nicht. Diese erweitern, nicht Gesundheitsdossiers statt des Contracts laden.
- Die bisherige reale Nachfrage wird durch Teilnehmerzählung gebildet. Neue Filter dürfen keine neue Gewichtung nach Kategorie/Stufe einführen.
- Teilnehmerstufen und Kategorien sind bereits für beide Provider persistent und im Teilnehmer-Paket integriert. Keine Parallelfelder oder neue Kategorien anlegen.
- Frontend: `meal-planning.component.ts`, `participant-planning.component.ts`, `meal-planning-api.service.ts`; SupplySolutions und Eignungslogik weiterverwenden.

## Vor Implementierungsbeginn

Die Schätzplanung/Fallback-Grenze aus Abschnitt 7 ist vom Benutzer bestätigt: neue frei konfigurierte Filter nur mit realer Teilnehmerbasis; bei Schätzplanung oder erforderlichem Fallback ausdrücklich nicht auswertbar/unvollständig statt ungefilterter Ersatzmengen. Die bisherigen drei Auswahlen behalten ihr Bestandsverhalten. Keine bereits bestätigten UND/ODER-, Struktur- oder Fallback-Entscheidungen erneut öffnen. Technische JSON-/Tabellenform nach vorhandenen Persistence-Konventionen wählen, ohne generisches Regel-Framework.

## Vertikalschnitt

1. **Domain/Contract:** typisierte Bedingungen mit erlaubten Merkmalen/Operatoren, OR-of-AND-Gruppen, kanonischer semantischer Darstellung, eindeutigem Umgang mit Null/Negation. Lagerfilter mit stabiler ID, CampId, Name und Concurrency-Version. Genau eine effektive Auswahl je CookingUnit. Minimierten Camp-Contract und dessen Projektion um vorhandene Stufe/Kategorie erweitern.
2. **Application/Persistenz:** lagerbezogene Verwaltung, kopieren über Neuanlage, Referenzprüfung, atomarer Update mit Prüfung aller betroffenen Einheiten. Vorhandene Stores/Transaktionen/Audit/Freeze nutzen. Beide Provider migrieren und Bestandsauswahl beweisen. Keine fremden DbContexts im Catering-Modul.
3. **Operative Integration:** StructureParticipantProjection statt separater UI-/Backend-Filterlogik erweitern. Alle Aufrufstellen (Unit, Meal-Strukturabweichung, Reset, Berechnung, Vorschau, Importvalidierung) abdecken. RequirementGroups erst nach endgültiger Auswahl; keine Filterung einzelner Anforderungen innerhalb eines Personenprofils.
4. **Rechte/API:** bestehende MealPlanning-Lese-/Bearbeitungsrechte und explizite Health-Leserechte wiederverwenden. Vor Implementierung konkrete Endpoint-Matrix anhand bestehender Endpoints dokumentieren. Gesundheitsbasierte Vorschau auch als reine Anzahl Health-schützen; keine Verbreiterung des allgemeinen participant-counts-Endpunkts. CRUD camp-/tenant-isoliert, erwartete Version, verständliche Fehler und Referenzen. Kein neuer Name-/Gesundheitsdetailendpoint, keine neuen Rollen oder automatische Rechte.
5. **Invalidierung:** kanonische Filtersemantik und relevante Auswahldaten in vorhandene Signaturen/Snapshots integrieren. Geteilte Filteränderungen atomar, Überlappungen aktuell prüfen; irrelevante Namensänderungen ohne Stale. Historischer Prüfstand bleibt nachvollziehbar, bis eine neue Verifikation ihn ersetzt; keine Snapshot-Historie hinzufügen.
6. **UI:** kompakte Filterwahl im Unit-Editor, ausgelagerter lagerbezogener Filtereditor/Verwaltung; Mehrfachwerte, UND-Bedingungen, ODER-Gruppen, explizite Negation und nicht zugeordnet. Lesbare Zusammenfassung, kontextbezogene Vorschau, Verwendungsauskunft vor gemeinsamem Speichern, Speichern/Verwerfen und Schutz ungespeicherter Eingaben. Keine komplexe Fachsyntax als einzige Darstellung. Fehlende Berechtigung, leere Auswahl und Ladefehler unterscheiden.
7. **Package:** Filterdefinitionen/Referenzen in vorhandenen Roundtrip aufnehmen; Paketversion und Legacy-Transformation explizit festlegen. Vollständige Referenzvalidierung, Replace/Rollback und Freeze erhalten. Alte Filter ohne Personenmengenänderung übernehmen. Keine neue produktive Freigabe sensibler Daten.

## Testmatrix / Abnahme

- Domain: OR-Werte, AND-Bedingungen, OR-Gruppen, Ausschlüsse (insbesondere NOT-any), Nullstufe/-kategorie, fehlende DietType, mehrwertige Allergen-/Stoffanforderungen, leere/ungültige Gruppen, unbekannte Operatoren, doppelte Treffer, kanonisch identische Ausdrücke.
- Beispiele aus dem Zielbild als reproduzierbare Fixtures; gleiche Filter in unterschiedlichen Strukturgebieten; abwesende Personen und Mahlzeitenabweichungen.
- Integration: Überlappung atomar abweisen; Unterdeckung anzeigen; gemeinsam genutzte Filteränderung; spätere Profil-/Anwesenheitsänderung; null Treffer nicht als Fallback; bestätigte Estimated-/Fallback-Policy; keine Berechnungsregeländerung.
- Positive/negative Invalidierung: relevante/irrelevante Stufe, Kategorie, Profil, Struktur, Anwesenheit, Ausdruck, Filtername; verwendeter/unverwendeter Filter; NoSupplyRequired; Verifikation; manuelle Lösungen erhalten.
- Rechte/Scope: Read/Edit/Health einzeln und kombiniert, fehlende Rechte, fremdes Camp/Tenant, Cloud/lokal, eingefrorenes Lager, Vorschau ohne Datenleck, stale Version, gleichzeitige Änderung zwischen Vorschau und Speichern.
- Persistenz: SQLite/PostgreSQL Upgrade mit den drei Altfiltern; Zuordnungen/semantische Auswahl unverändert; Reload, Referenzschutz und transaktionaler Fehlerfall.
- Package: PostgreSQL → Paket → SQLite → Filter bearbeiten → Rückpaket → PostgreSQL; Definitionen/Referenzen/Versorgungsdaten vergleichen. Ungültige Referenz/Version und erzwungener Replace-Fehler rollen vollständig zurück; Rechte unverändert.
- Angular: Editor roundtrip ohne Bedeutungsverlust, Null/Negation, mehrere OR-Gruppen, Kontextwechsel, Vorschaufehler, Shared-Edit-Hinweis, Concurrency-Fehler mit erhaltenen Eingaben, Abbrechen und Dirty-Schutz.
- Danach vollständige relevante .NET-Suite mit dediziertem PostgreSQL, Angular-Tests/Production Build, Sidecar-/Desktop-Build und installierter Dummy-Roundtrip soweit möglich. Nicht ausgeführte Prüfungen NOT_RUN, niemals alte PASS-Zahlen als Nachweis neuer Funktion verwenden.

## Ergebnis / Grenzen

Vorgemerkter Folgeauftrag (Benutzerwunsch 2026-10-20): konfigurierbare Schätzwerte auch für frei konfigurierte Filter zur ersten Planung/Kalkulation ohne ausreichende reale Teilnehmerdaten. Siehe Abschnitt 8 in `docs/domain/CookingUnit_Participant_Filters.md`. Nicht im aktuellen Schnitt umsetzen; Bezug, Überschneidungen und Übergang auf reale Daten erst separat spezifizieren. Die bestätigte aktuelle Fallback-Grenze bleibt unverändert.

Ergebnis später unter `.codex/cooking-unit-participant-filters-result.md`: Status, umgesetzt, Migrationen/Kompatibilität, Testbefehle und Resultate, manuelle Abnahme, offene Punkte, Risiken, Commit-Bereitschaft. Kein Commit ohne Auftrag.

Nicht im Umfang: Organisationsvorlagen, Filterreferenzen auf Filter, Prioritäten, automatische Auflösung überlappender Einheiten, personenbezogene Stammzuordnung, neue medizinische Regeln, Anpassung der SupplySolution-Eignung, Einkaufs-/Ausgabelogistik, allgemeines UX-Redesign.

Vorbereitungsprüfung: ausschließlich Repository-Analyse und Dokumentation; keine Builds/Tests ausgeführt, da keine Anwendung geändert wurde. `git diff --check` vor Übergabe ausführen.
