# Korrektur Teilnehmerstruktur / CookingUnit

Status: bestätigt durch den Implementierungsauftrag vom 2026-10-06.
Ersetzt die direkte Catering-Teilnehmerzuordnung aus Inkrement 2 einschließlich
personenbezogener Standard-/Meal-Overrides; alle übrigen Entscheidungen bleiben bestehen.

- Camp besitzt Teilnehmer, Strukturzuordnung, Anwesenheit, Verpflegungs- und
  Gesundheitsprofil sowie Schätzungen. Teilnehmer gehören an zulässige Blattknoten.
- Catering besitzt CookingUnits und deren bestehende Strukturreferenzen.
  Effektive Personen ergeben sich aus Struktur, Mahlzeitenanwesenheit und Filter:
  alle / nur Sonderverpflegung / ohne Sonderverpflegung.
- Sonderverpflegung wird aus DietType, Allergenen oder Unverträglichkeiten abgeleitet;
  kein manuelles Personenflag.
- Die normale Camp-Catering-Projektion enthält technische Personenreferenz,
  Strukturreferenz, effektive Mahlzeitenanwesenheit und strukturiertes Verpflegungsprofil.
  Keine Namen, Gesundheitsdossiers oder Quellen-/Auditmetadaten.
- Pro Mahlzeit höchstens eine CookingUnit je anwesender Person. Überlappungen
  bei Konfigurationsänderung abweisen; bei inkonsistentem Bestand Incomplete mit
  strukturiertem Problem, niemals eine Kocheinheit still bevorzugen.
- RequirementGroups und SupplySolutions bleiben die Planungs-/Berechnungsbasis.

## Personenbezug bei der Ausgabe

`MealServingAssignment` bezeichnet eine separate operative Verknüpfung von
Personenreferenz, Mahlzeit, CookingUnit, RequirementGroup-Signatur und SupplySolution.
Diese Verknüpfung steuert **nicht** den Bedarf und ist keine alternative
Teilnehmer-CookingUnit-Stammzuordnung. Namen dürfen ausschließlich in einer
gezielt Health-/Serving-geschützten Ausgabeprojektion aufgelöst werden.
Keine QR-, Check-out- oder Ausgabelogistik. Solange SupplySolutions fehlen, wird
kein personenbezogener Ausgabeendpoint freigeschaltet; bestehende Health-Rechte
werden nicht automatisch vergeben oder erweitert.

## Übergang von Entwicklungsdaten

Eindeutige Altzuordnungen können atomar auf einen Camp-Blattknoten übertragen
werden. Keine automatische Erfindung von Knoten oder Auflösung widersprüchlicher
personenbezogener Meal-Overrides. Nicht eindeutig übertragbare Altinformationen
bleiben ausschließlich als Migrationsrest erhalten und werden operativ nicht
verwendet. Sie blockieren Vollständigkeit sowie den Export im neuen Paketschema.
Sie dürfen weder durch normales Speichern noch Unit-Löschung verschwinden.
Eingefrorene Cloud-Lager werden beim Start nicht umgeschrieben.
Migration alter Pakete folgt denselben Regeln oder weist den Import ohne Teiländerung ab.

Dies bleibt eine Entwicklungsfunktion für Dummy-/Testdaten, keine neue Freigabe
für produktive sensible Offline-Daten.
