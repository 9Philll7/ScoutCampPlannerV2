# UI-Abnahme: Teilnehmerlisten

Stand: 2026-10-31.

## Rückmeldung und Anpassung

- Eingebettete Teilnehmerliste im Strukturknoten: Schnellansicht entfernt. Zeilen-/Namensklick öffnet die vollständige Detailansicht. Als Zeilenaktion bleibt nur Details; der zusätzliche Bearbeitenbutton entfällt gemäß Folgeabnahme. Bearbeitung ist aus der vollständigen Detailansicht erreichbar. Kein zusätzlicher Vorschauplatz und keine Vorschau-ARIA-Verweise in dieser Liste.
- Allgemeine Teilnehmerübersicht: Schnellansicht bleibt bestehen. Ihre Höhe folgt der Listenspalte; ein fester Namensbereich mit maximal zwei sichtbaren Zeilen vermeidet unterschiedliche Kopfhöhen. Der volle Name bleibt als Titel verfügbar. Kopf und Aktionen bleiben sichtbar; die Inhalte dazwischen scrollen bei Bedarf innerhalb der Schnellansicht. Sehr kurze Listen erhalten im Vorschaumodus eine Mindesthöhe von 420px für beide Spalten.
- Folgeabnahme: Detailansicht, Bearbeiten und Löschen sind kompakte Icon-Buttons mit zugänglichen Beschriftungen und Tooltips im Namensbereich der Schnellansicht. Ein erneuter Klick auf dieselbe Teilnehmerzeile schließt die Vorschau, eine andere Zeile wechselt die Person. Kein eigener Schließen-Button. Die große Aktionszeile am unteren Rand entfällt; Bearbeiten/Löschen bleiben durch bestehende Rechte, Freeze und Busy-Zustand begrenzt.
- Schmale Ansicht: laut Nutzer grundsätzlich in Ordnung und damit für diesen Stand abgenommen. Kein zusätzlicher Umbau des Editors für mobile Bearbeitung beauftragt. Die bestehende schmale Vorschau bleibt ein Overlay.
- Ergänzung: Im schmalen Vorschau-Overlay (bis 1200px) gibt es wieder einen vollbreiten Schließen-Button am unteren Rand, mit Abstand und Trennlinie zu den Inhalten. Er bleibt außerhalb des scrollenden Inhalts und getrennt von den oberen Bearbeiten-/Löschen-Aktionen. Auf Desktop bleibt nur der erneute Zeilenklick zum Schließen.

## Detailansicht mit Bereichen

Die vollständige Teilnehmerdetailansicht verwendet dieselben Bereiche wie der Editor: Allgemein, Struktur & Gruppe, Ernährung und Anwesenheit. Jeweils nur der aktive Bereich ist sichtbar; die zuvor untereinanderstehenden Informationskarten und die seitliche Anwesenheitskarte entfallen. Der Bereich bleibt beim Wechsel von Details zur Bearbeitung und beim Abbrechen erhalten. Ein anderer Teilnehmer beginnt bei Allgemein. Die gemeinsame Darstellung gilt auch beim Einstieg aus der allgemeinen Liste.

## Ernährungsangaben statt Gesundheits-Checkboxsammlung

Allergene und Unverträglichkeiten gehören gemäß fachlicher Rückmeldung zur Ernährung. In Lese- und Bearbeitungsansicht sind sie deshalb gemeinsam mit der Ernährungsform dort angeordnet. Der Gesundheitstab bleibt bis zur gesonderten Einführung von Medikamenten/Vorerkrankungen ausgeblendet. Bestehende API-Felder und Health-Rechte werden dadurch nicht verändert; es handelt sich um die fachliche Zuordnung in der Oberfläche.

Im Editor werden nur ausgewählte Allergene und Unverträglichkeiten als Listen angezeigt. Hinzufügen erfolgt über eine Katalogauswahl, bereits gewählte Einträge werden ausgeschlossen. Allergene können ersetzt/entfernt werden, Unverträglichkeiten erhalten eine gezielt aufklappbare Bearbeitung für Grenzwert und Herkunft. Kein Grenzwert wird neu geraten; vorhandene Katalogübernahme bleibt unverändert. Übernehmen/Fertig betreffen den lokalen Teilnehmerentwurf, erst dessen Speichern persistiert. 115 Tests und Production Build erfolgreich, einschließlich Duplikatschutz, Ersetzen/Entfernen und Erhalt individueller Grenzwerte. Browserprüfung dieses Schritts ausstehend.

Navigation und Bereichsdefinition sind für Lese- und Bearbeitungsansicht gemeinsam implementiert. Der aktuelle Vertrag bietet keine getrennten Leserechte pro Tab; es werden deshalb keine neuen Berechtigungen aus der Darstellung abgeleitet. Bestehender Zugriff auf Teilnehmerdaten, Bearbeitungsrechte und Freeze-Prüfung bleiben unverändert. Eine zukünftige feinere Tabfreigabe muss an die autorisierten API-Daten gekoppelt werden.

114 Tests und Production Build nach dieser Ergänzung erfolgreich. Regression prüft einen sichtbaren Bereich und den erhaltenen Bereich beim Wechsel zum Editor. Keine erneute Browserabnahme durchgeführt; die Browser-Verbindung war zuletzt nicht verfügbar.

## Prüfstand der Listenanpassung

114 Frontend-Tests und Production Build erfolgreich. Neue Regressionen prüfen direkten Detail-/Editzugriff im Knoten, fehlende Schnellansicht, Leserechte und den Personenwechsel in der allgemeinen Schnellansicht. Die Pixelhöhe muss noch im Browser geprüft werden: Browserzugriff war beim Versuch nicht verfügbar. Keine Testdaten verändert und keine API-/Backendänderungen für diese Korrektur.

Die parallel vorhandenen uncommitteten Arbeiten an Teilnehmerfiltern bleiben von dieser UI-Korrektur getrennt.

## Popup-Auswahl und Allergen-Buchstaben

Allergene werden in Leseansicht und Editor pro Hauptgruppe zusammengefasst: beispielsweise „A · Glutenhaltiges Getreide“ mit „Weizen, Roggen“ darunter. Bearbeiten lädt alle ausgewählten Untergruppen dieser Hauptgruppe, Entfernen löscht den vollständigen Gruppeneintrag inklusive Untergruppen. Gespeicherte IDs und API bleiben unverändert. Regression prüft eine gemeinsame Listenzeile, Vorauswahl und Entfernen unter Erhalt anderer Allergene.

Allergene und Unverträglichkeiten werden in modalen Dialogen hinzugefügt/bearbeitet. Abbrechen und Escape übernehmen keine Änderungen; Grenzwert/Herkunft werden zunächst separat im Dialog gehalten. Übernehmen ändert nur den Teilnehmerentwurf. Native Dialoge begrenzen den Fokus auf das Popup und stellen ihn beim Schließen wieder her.

Die Allergenauswahl zeigt ausschließlich die 14 Hauptgruppen mit österreichischem Buchstabencode A, B, C, D, E, F, G, H, L, M, N, O, P, R. Grundlage: [WKO – Allergene](https://www.wko.at/ktn/tourismus-freizeitwirtschaft/gastronomie/allergene). Die Zuordnung nutzt die stabilen Katalogcodes; API und gespeicherte IDs bleiben unverändert. Bei vorhandenen Untergruppen (Getreide/Schalenfrüchte) sind mehrere Untergruppen im Popup wählbar. Ohne ausgewählte Untergruppe wird standardmäßig nur die Hauptgruppe erfasst; ein eigener Button für unbekannte Untergruppen entfällt. Auch das Abwählen der letzten Untergruppe führt zu diesem Standard. Bestehende Untergruppen werden beim Bearbeiten gemeinsam vorausgewählt; Duplikate werden vermieden. Bestehende generische/sonstige Einträge werden nicht gelöscht; bei erneuter Bearbeitung erfolgt die Auswahl nach den neuen Regeln. Die optionale Untergruppenauswahl ist eine UI-Anforderung.

117 Tests und Production Build erfolgreich. Neue Regressionen prüfen Mehrfachauswahl, unbekannte Untergruppe, leere Auswahl als Standard und Vorauswahl beim Bearbeiten, Buchstaben, Ausschluss zusätzlicher Hauptgruppen sowie das Übernehmen/Verwerfen individueller Grenzwerte. Visuelle Browserabnahme steht weiterhin aus.
