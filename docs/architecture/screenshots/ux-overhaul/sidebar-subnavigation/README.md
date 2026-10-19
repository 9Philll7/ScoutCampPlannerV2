# Navigation ohne gemeinsame Kopfzeile

Stand: 2026-10-19.

Die gemeinsame Kopfzeile mit Lagername, Breadcrumb und Umschaltung entfällt bei Struktur und Teilnehmerübersicht. In der Sidebar stehen beide Ansichten als Unterpunkte der Gruppe „Struktur & Teilnehmer“. Nur der aktive Unterpunkt trägt `aria-current`. Der Lagerkontext und ein gegebenenfalls aktiver Schreibschutz bleiben in der Sidebar sichtbar; auf schmalen Ansichten sind die Unterpunkte über das bestehende Menü erreichbar. Eine unsichtbare Hauptüberschrift erhält die Dokumentstruktur.

Edge bei 1920 × 1080 geprüft: Karten beginnen direkt am oberen Inhaltsrand, Strukturkarte füllt die frei gewordene Höhe. Wechsel über den Unterpunkt zur Teilnehmerübersicht erfolgreich. Screenshot `structure-1920.jpg`; keine Testdaten verändert, Viewport zurückgesetzt.

107 Tests und Production Build erfolgreich. Navigationstests prüfen die Sidebar-Unterpunkte, aktive Markierung und entfernte Kopfzeile.
