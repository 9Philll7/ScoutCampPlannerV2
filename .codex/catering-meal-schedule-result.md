# Ergebnis: regelmäßige und zusätzliche Mahlzeiten

Stand: 2026-11-01. Umsetzung des separat bestätigten Mahlzeitenrahmens; kein Commit erstellt.

## Implementiert

- `CampMealType.Kind` (Regular/Additional), `CampMeal.SortOrder` pro Tag und atomar geprüfte `CampMealScheduleState.Version`.
- SQLite-/PostgreSQL-Migrationen mit Erhalt vorhandener Identitäten und fachlicher ChangeVersion. Historische Arten werden Regular; die ursprüngliche Typsortierung initialisiert die Tagesreihenfolge.
- Neue autorisierte/auditierte Operationen für zusätzlichen Tages-Slot, vollständige Tagesreihenfolge und Entfernen eines Zusatzslots. Erwartete Rahmenversion für neue Operationen erforderlich; bestehende Namens-/Aktivitätsoperationen akzeptieren sie kompatibel optional.
- Schreibentscheidungen und erneute Freeze-/Zeitraumprüfung innerhalb der Audittransaktion. Konflikte erzeugen keine Teiländerungen. Restriktive Fremdschlüssel sichern zusätzlich konkurrierende Referenzerzeugung; ein solcher Konflikt wird mit `meal_in_use` zurückgewiesen.
- Referenzprüfung für Menüplankomponenten, operative Zustände inklusive Supply/Verifikation, mahlzeitbezogene Strukturzuordnungen, Teilnehmer-Abwesenheiten und alte explizite Teilnehmer-Overrides. Verwendete Zusatzslots werden deaktiviert, unbenutzte entfernt.
- Lagerzeitraum-Erweiterung erzeugt nur Regular-Slots. Bestehende Slots außerhalb des Zeitraums bleiben erhalten. Bewusste Deaktivierung bleibt bei erneuter Erweiterung bestehen.
- Paketformat 2 trägt Art, Tagesreihenfolge und Rahmenversion in beide Richtungen. Paketformat 1 wird kontrolliert aktualisiert. Lokale Lagerentfernung und Rücktransfer berücksichtigen die neue Tabelle.
- Menüplan-Tagesansicht mit kompaktem „Mahlzeiten“-Dialog: Zusatzname, Umordnung, Entfallen/Aktivieren und Entfernen. Änderungen gelten sofort lagerweit. Bei ungespeichertem Menüplan sind nur Hinzufügen und Entfernen gesperrt; Reihenfolge und Aktivität bleiben unabhängig von Komponenten bearbeitbar. Der Entwurf bleibt erhalten.
- Deaktivierte Mahlzeiten bleiben als ausgegraute Tabs mit „Entfällt“ sichtbar und werden aus aktiven Belegungszahlen ausgeschlossen. Menüpläne mit Komponenten deaktivierter Mahlzeiten bleiben speicherbar. Fehlerhafte Umlaute im Mahlzeitendialog und seinen Fehlermeldungen wurden korrigiert.
- Tage mit ausschließlich deaktivierten Slots bleiben erreichbar. Gemeinsame Tagesreihenfolge für Menüplanübersicht, operative Navigation und Teilnehmer-Anwesenheit. Lagerweite Namensverwaltung zeigt ausschließlich Regular-Arten.
- Rahmenänderungen aktualisieren auch den im App-Container geladenen Stand. Fehler und Versionskonflikte behalten den geöffneten Menüplan; kein automatischer Mutations-Retry.

## Bewusste Details gegenüber dem Vorschlag

- Eine Additional-Art bleibt auch nach Löschen ihres letzten Slots als wiederverwendbare Namensidentität gespeichert. Die optionale Bereinigung aus dem Vorschlag wurde nicht ergänzt. Die Art erzeugt keine automatischen Slots und bleibt aus der regelmäßigen Konfiguration ausgeblendet.
- Die Tagesverwaltung bündelt Aktionen in einem Dialog statt für jeden Mahlzeitentab ein separates Menü einzuführen. Es entsteht keine zusätzliche dauerhafte Editorzeile.
- Auch die Planungsübersicht liefert `scheduleVersion`, damit Tagesaktionen mit dem dort gelesenen Stand arbeiten. Die Version wird vor dem Laden der Slots gelesen; eine zwischenzeitliche Änderung führt zur Ablehnung der anschließenden Mutation.

## Prüfung

92 Platform-/HTTP-Tests, 317 Catering-Tests, 19 Pakettests, 13 SQLite-Migrationstests, 143 Frontend-Tests und Production-Build erfolgreich. Konkurrenztest: genau eine von zwei Änderungen auf gleicher Rahmenversion wird übernommen. Auditfehler: Mahlzeiten und Rahmenversion werden zusammen zurückgerollt. Nach der UI-Abnahme ergänzt: Aktivitätsänderung mit offenen Rezeptänderungen, Speicherung deaktivierter Komponenten, sichtbare inaktive Tabs und korrekte Umlaute.

Die laufende lokale Test-API wurde auf Release neu gestartet und hat die Migration auf die vorhandene Testdatenbank angewendet. Authentifizierter HTTP-Lesetest bestätigt neue Verträge. Keine fachlichen Testlagerdaten zum Demonstrieren der Funktion verändert.

Offen: visuelle Browserabnahme (Edge-Host nicht verfügbar) und echte PostgreSQL-Ausführung (Testdatenbank/Docker nicht verfügbar). Keine Screenshots behauptet. Manuelle Abnahme: [catering-additional-meals.md](../docs/testing/catering-additional-meals.md).
