# Weitere Anpassungen: Mockup-Abgleich

Stand: 06.11.2026. Referenzen: Bilder 12 bis 15 im [Verpflegungsindex](mockups/catering/README.md).

## Gestaltungsrichtung

Ein Dialog mit Mahlzeit, Kocheinheit und Datum im Kontextstreifen. Vier Akkordeonbereiche: Versorgung und Rezeptlösungen, Personenzahl und Mengenbedarf, Strukturzuordnung, Versorgungspräferenz. Geschlossene Bereiche zeigen die aktuelle Einstellung als Badge. Versorgung initial offen. Fußleiste mit Zurücksetzen, Abbrechen und Speichern; vorhandene Berechtigungs-, Freeze- und Entwurfsprüfungen erhalten.

## Vorhandene Funktionen

- Versorgung aktiv/entfällt: `subscriptionState`.
- Rezeptlösungen bearbeiten: vorhandener Editor für eigene Teil-/Gesamtlösungen. Zurück zum Plan setzt eigene Rezepte zurück und erhält bestätigte Gemeinsamkeitsentscheidungen.
- Berechneten Bedarf verwenden: `demandOverride = null`; manuelle Personenzahl sowie optionale `offerTargets` je Komponente existieren. Keine neue Zutatenmengenfunktion aus dem Bild ableiten.
- Standard-/eigene Strukturzuordnung existiert; aktuelle Strukturnamen können aus vorhandenen Daten als Chips dargestellt werden.
- Versorgungspräferenzen werden bisher separat als komponentenbezogene Szenarioentscheidungen gespeichert, nicht im gemeinsamen Anpassungsentwurf.

## Bei Umsetzung zu berücksichtigen

- Die vier Bilder ersetzen die bisherige flache Formularanordnung. Radio-Karten, kompakte Zusammenfassungen und ein Versorgungsschalter sollen die vorhandenen Felder abbilden.
- Standard der Kocheinheit ist eine fehlende Mahlzeitenüberschreibung, kein weiterer Enumwert. Bestätigte Gemeinsamkeitsentscheidungen nicht durch bloßes Öffnen oder Schließen verändern. Unterschiedliche bestehende Komponentenpräferenzen sichtbar berücksichtigen.
- Gemeinsames Speichern von Einstellungen und Präferenz braucht eine konsistente Speicherung: aktuell nutzen sie verschiedene Aufrufe. Kein vorgetäuschtes Alles-oder-nichts-Verhalten bei teilweisem Fehler.
- Alle Anpassungen zurücksetzen muss von Zurück zum Plan unterschieden werden. Die bisherige Funktion `resetPlan()` setzt nur Versorgung/Bedarf/alte Rezeptwahl im Entwurf zurück; sie ist kein vollständiger Reset von Struktur, Präferenz und eigenen Rezeptlösungen. Ein vollständiger Reset darf daher nicht lediglich umbenannt werden.
- Struktur zurücksetzen schreibt bisher sofort; im neuen Dialog sollte ein Zurücksetzen als Entwurf bis zum Speichern behandelt werden, damit Abbrechen verlässlich funktioniert.

Die vorstehende Bestandsaufnahme beschreibt den Stand vor der Umsetzung. Der folgende Abschnitt dokumentiert das implementierte Verhalten.


## Umsetzung (06.11.2026)

- Dialog nach Referenzen 12–15 mit Kontextleiste, vier exklusiv aufklappbaren Bereichen, Status-Badges, Radio-Karten, Versorgungsschalter und gemeinsamer Fußleiste umgesetzt. Bestehender Rezepteditor bleibt separat erreichbar; offene Entwürfe müssen vorher gespeichert oder verworfen werden.
- Bedarf, Komponentenwerte, Struktur, Präferenz und vorgemerkte Rezeptresets bleiben bis zum Speichern im lokalen Entwurf. Abbrechen stellt den Ausgangszustand wieder her. Standard-Struktur setzt nun ausschließlich den Entwurf zurück, statt sofort einen separaten Aufruf auszulösen.
- Bestätigte Präferenzen je Komponente werden angezeigt; gemischte Einstellungen bleiben ohne explizite neue Auswahl unverändert. Eine neue Wahl gilt für alle Komponenten dieser Mahlzeit. Standard entfernt die Mahlzeitenüberschreibungen.
- Zurück zum Plan merkt nur den Reset eigener Rezeptlösungen vor. Bereits bestätigte Plan-Rezeptentscheidungen werden anhand des vorhandenen Planvergleichs beibehalten. Alle Anpassungen zurücksetzen setzt zusätzlich Versorgung aktiv, Bedarf automatisch, Komponentenwerte leer, Struktur auf Standard und Präferenz auf Standard; eigene Gesamtlösung und Rezeptentscheidungen werden dabei gelöscht.
- Der bestehende PUT-Aufruf für die Mahlzeit akzeptiert optional `supplyAdjustments`: `expectedVersion`, `updatePreference`, `preferenceOverride` (0/1/2 oder null für Standard), `resetRecipes`, `retainRecipeSolutionIds`. Bestehende Clients bleiben kompatibel. Beibehaltene Rezept-IDs müssen zu den gespeicherten Entscheidungen gehören. Bei Ist-Teilnehmern wird für diesen zusätzlichen Versorgungsanteil die Anforderungs-Leseberechtigung geprüft.
- Der Store speichert die gesamte Konfiguration einschließlich der Versorgungsentscheidungen in einer Datenbanktransaktion. Versorgungsstand wird anhand der beim Beginn des Entwurfs geladenen Version geschützt; 409 bei Versionskonflikt. Bei Fehlern werden auch ausgeführte Löschungen alter Zuordnungen und Zielwerte zurückgerollt. Keine Datenbankmigration.
- Normale Einstellungsänderungen setzen die Berechnung auf veraltet. Nach einem gespeicherten Rezeptreset wird automatisch neu berechnet; ein Berechnungsfehler wird ausdrücklich als bereits gespeicherter Reset gemeldet.


## Korrekturen nach UI-Abnahme (07.11.2026)

- Unverändertes Öffnen/Speichern des Rezepteditors speichert keine automatischen Rezepte als eigene Entscheidungen. Ein gespeicherter Rezeptreset berechnet die automatische Lösung unmittelbar neu, statt den alten manuellen Berechnungssnapshot weiter anzuzeigen. Bestätigte Plan-/Gemeinsamkeitsentscheidungen bleiben erhalten.
- Berechneter Bedarf sperrt Komponentenfelder. Der Wechsel zu Automatik entfernt manuelle Komponentenwerte und die Gesamtüberschreibung aus dem Entwurf. Manuell eingetippte Dezimalwerte bleiben erlaubt; Zahlenfeld-Schrittweite ist 1.
- Präferenz zuerst Standard oder Abweichung wählen. Erst bei Abweichung erscheinen Gemeinsam/Getrennt/Nachfragen. Bestehende gemischte Komponentenentscheidungen werden ohne ausdrückliche Änderung beibehalten.
- Strukturzuordnung enthält einen Mahlzeiten-Verpflegungsfilter: Standard, alle Teilnehmer, nur Sonderverpflegung, ohne Sonderverpflegung. `supplyAdjustments` ergänzt `updateParticipantFilter` und `participantFilterOverride` (null für Standard). `CookingUnitMealDocument.participantFilterOverride` liefert die gespeicherte Abweichung.
- Speicherung im bestehenden `ManualSupplyDocument`, unabhängig von Rezepten und Gemeinsamkeitsentscheidungen. Beim Rezeptreset und beim Speichern einer Rezeptlösung bleibt der Filter erhalten; All-Reset entfernt ihn. Die Ist-Teilnehmerprojektion, Überschneidungsprüfung und Berechnungsfingerprints verwenden den wirksamen Mahlzeitenfilter. Der Standard der Kocheinheit bleibt unverändert. Schätzwerte liefern weiterhin keine individuellen Filtermerkmale.
- Keine Datenbankmigration; Offline-Modulschema 8 mit Import der Versionen 1–7. Der Filter wird im bestehenden Versorgungsdokument mitgeführt und validiert.
