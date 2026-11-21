# Rezepteditor Nährwert Testdaten

Stand: 20.11.2026. Im bestehenden Lager `TEST Versorgung - nur Dummy-Daten` stehen acht zusätzliche Zutaten und sechs Rezepte für die UI-Abnahme bereit. Alle neuen Namen beginnen mit `TEST Nährwerte`. Die Nährwerte und Deklarationen sind synthetische Testangaben.

## Testfälle

| Rezept nach dem gemeinsamen Namenspräfix | Zutaten | Zustand und Erwartung |
| --- | ---: | --- |
| 01 Haferbrei vollständig | 5 | Veröffentlicht, zwei Zutatengruppen, vollständige Profile: 3.080 kcal insgesamt, 308 kcal pro Standardportion, 62 g Ballaststoffe insgesamt |
| 02 Haferbrei unvollständig | 7 | Veröffentlicht, zwei Zutatengruppen, vorläufig 3.080 kcal insgesamt und 308 kcal pro Standardportion aus fünf auswertbaren Zutaten; zwei fehlende Beiträge: Gewürz ohne Profil und Joghurt mit ungeprüftem Profil |
| 03 Obstsalat Ballaststoffe offen | 3 | Veröffentlicht, Kernwerte vollständig: 1.240 kcal insgesamt und 124 kcal pro Standardportion; Ballaststoffe unbekannt statt null Gramm |
| 04 Mengenregeln | 4 | Veröffentlicht, linear mit Minimum/Maximum, fixe Ölmenge und Zucker in festen Schritten; 2.700 kcal bei zehn Standardportionen |
| 05 Entwurf mit Fehlern | 3 | Unveröffentlichter Entwurf: Milchmenge fehlt. Veröffentlichungsversuch liefert einen Fehler und vier Warnungen. „Zum Eintrag“ markiert die Milchposition |
| 06 Entwurf mit Warnungen | 3 | Unveröffentlichter Entwurf: keine Fehler, vier Warnungen. Die Bestätigung muss vor einer Veröffentlichung aktiv gewählt werden |

Alle Rezepte beziehen sich auf zehn Standardportionen. Die vier Warnungen der beiden Entwürfe sind fehlende Beschreibung, fehlende Quelle sowie nicht durch Ersatz abgedeckte Milch- und Laktosekonflikte. Die beiden Entwürfe bleiben absichtlich unveröffentlicht.

## Zutaten und Mengen

Die Zutaten sind Haferflocken, Milch, Apfel, Rapsöl, Zucker, Zucker ohne Ballaststoffangabe, Gewürz ohne Profil und Joghurt ungeprüft. Sie besitzen jeweils eine veröffentlichte Zutatenrevision. Sechs Profile sind als geprüfte Testprofile gespeichert, eines ist ungeprüft, eines fehlt absichtlich. Die Bezugsmenge ist jeweils 100 g.

Der vollständige Haferbrei enthält 500 g Haferflocken, 1.000 g Milch, 500 g Apfel, 20 g Rapsöl und 50 g Zucker. Pro Standardportion ergeben sich 308 kcal, 9,1 g Fett, 3,04 g gesättigte Fettsäuren, 44,8 g Kohlenhydrate, 15,3 g Zucker, 10,05 g Eiweiß, 0,11 g Salz und 6,2 g Ballaststoffe.

Im Mengenregel-Rezept gelten für Haferflocken 300 g Mindest- und 700 g Höchstmenge. Milch rechnet linear, Rapsöl bleibt bei 20 g und Zucker verwendet 10 g je angefangene fünf Standardportionen.

## Wiederholbar ergänzen

```powershell
./tools/dev-meal-planning.ps1 AddNutrition
```

Der Befehl ergänzt ausschließlich die aktivierte SQLite-Testgeneration aus `.dev-data/meal-planning-increment-2/current.json`. Development/Test, der Testlager- und Benutzerbezug sowie der Testdateiname werden geprüft. Vor dem Lauf wird eine konsistente SQLite-Sicherung mit dem Namensbestandteil `before-nutrition` angelegt. Ein eigener Datenbankpfad kann nicht übergeben werden.

Bestehende Zutaten, Rezepte, Zuordnungen und manuelle Anpassungen werden nicht ersetzt. Stabile IDs verhindern Duplikate; ein zweiter Lauf hat **0 Zutaten und 0 Rezepte** hinzugefügt. Das reguläre Seed/Reset-Verhalten bleibt erhalten. Nach einem Reset kann `AddNutrition` erneut ausgeführt werden.

Die Ergänzung nutzt die regulären Zutaten- und Rezept-Veröffentlichungsfunktionen. Neue Rezepte werden der Lagerbibliothek hinzugefügt, aber nicht in bestehende Menüpläne oder Versorgungslösungen eingesetzt. Manuell bearbeitete Ergänzungsrezepte bleiben bei Wiederholung erhalten.

## Prüfergebnisse

- Werkzeugbuild bestanden, keine Warnungen/Fehler.
- Persistierte veröffentlichte Rezepte über den regulären Rechner geprüft: 3.080, 1.240 und 2.700 kcal; der unvollständige Fall hat genau zwei fehlende Beiträge.
- Abgleich mit der Sicherung vor der Ergänzung: **904 bestehende Datensätze in 67 Tabellen unverändert erhalten**.
- Browser bestätigt vollständige Werte und fünf Zutatenzeilen, beide Ursachen fehlender Profile sowie unbekannte Ballaststoffe.
- Browser-Veröffentlichungsversuch beim Fehlerentwurf liefert den erwarteten Fehler; Navigation fokussiert und markiert die Milchzeile.
- Browser-Veröffentlichungsversuch beim Warnungsentwurf liefert vier Warnungen. Der abschließende Button ist ohne Checkbox gesperrt und mit Checkbox aktiv. Er wurde nicht ausgelöst.
- Beide Entwürfe bleiben mit Status Entwurf, Entwurfsversion 0 und **ohne veröffentlichte Revision** in der Datenbank.

## Nachher Bilder

- [Vollständige Nährwerte](../architecture/screenshots/recipe-editor/nutrition-fixtures/recipe-nutrition-fixture-complete.jpg)
- [Rezept mit fünf Zutaten](../architecture/screenshots/recipe-editor/nutrition-fixtures/recipe-nutrition-fixture-ingredients.jpg)
- [Unvollständige Profile](../architecture/screenshots/recipe-editor/nutrition-fixtures/recipe-nutrition-fixture-incomplete.jpg)
- [Fehlerentwurf](../architecture/screenshots/recipe-editor/nutrition-fixtures/recipe-nutrition-fixture-validation-error.jpg)
- [Warnungsbestätigung](../architecture/screenshots/recipe-editor/nutrition-fixtures/recipe-nutrition-fixture-validation-warnings.jpg)


Der unvollständige Fall zeigt inzwischen einen deutlich als unvollständig gekennzeichneten Zwischenstand. Die zwei nicht auswertbaren Zutaten werden nicht eingerechnet und bleiben mit ihren Ursachen sichtbar. [Browserbild](../architecture/screenshots/recipe-editor/nutrition-fixtures/recipe-nutrition-partial-1920.jpg).
