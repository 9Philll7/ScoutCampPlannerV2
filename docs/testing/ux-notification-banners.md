# Globale Meldungsbanner

Stand: 2026-10-19.

Die globalen Hinweise und Fehler der angemeldeten Anwendung erscheinen als fixierte Banner oben in der Fenstermitte. Sie beanspruchen keinen Platz im Seitenlayout. Hinweise verschwinden nach acht Sekunden, Fehler nach zwölf Sekunden. Mauszeiger oder Tastaturfokus im Banner pausieren die Ausblendung; nach Verlassen beginnt die Anzeigedauer erneut. Jede Meldung lässt sich manuell schließen. Neue Meldungstexte starten die Dauer neu; beim Entfernen des Banners wird der Timer aufgeräumt.

Screenreader erhalten Hinweise als Status und Fehler als Alert. Feldbezogene Validierung, Anmelde-/Einrichtungsfehler und lokale Fachkomponenten behalten ihre bisherigen Rückmeldungen; diese Änderung betrifft die globalen Meldungen der Anwendung.

Prüfung: 105 Frontend-Tests in 21 Dateien bestanden, Production Build erfolgreich. Die neuen Tests prüfen Zeitablauf, Timerneustart, Pause bei Fokus und manuelles Schließen. Keine neue Browserabnahme dieses Banner-Schritts erfolgt.
