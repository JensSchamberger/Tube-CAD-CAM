# Tube CAD/CAM – Beta-Testanleitung (Windows x64)

Voraussetzung: Windows 10/11 x64, Desktop. Kein .NET nötig (self-contained). NC-Ausgabe ist gesperrt und nicht Teil der Beta.

## Start
1. `TubeCadCam-Beta-win-x64.zip` entpacken (SHA-256 mit `.sha256` vergleichen), `TubeCadCam.exe` starten.
2. Erwartung: Fenster mit Bauteil-/Profil-/Feature-Panel links und 3D-Ansicht. Bei Absturz: Meldung/Windows-Ereignisanzeige notieren.

## Bedienung (laut Code)
- Orbit: linke Maustaste ziehen. Pan: mittlere/rechte Maustaste ziehen. Zoom: Mausrad.
- Feature-Picking: linker **Klick ohne Ziehen** (>3 px Bewegung = Orbit). Treffer auf Lochwand oder Vorschau wählt das Feature; Klick ins Leere/auf Außenfläche hebt die Auswahl auf.
- Ansichten: Isometrie / Seite / Oben / Stirn / Einpassen, „2D / 3D“-Umschalter. Datei: Strg+N / O / S.

## Testfälle (jeweils Ergebnis eintragen: bestanden / fehlgeschlagen / übersprungen)
| Nr | Schritte | Erwartung |
|---|---|---|
| T1 | Profil nacheinander Rund, Rechteck, Quadrat, U, L, I wählen | 3D-Rohr jeweils korrekt, Wandstärke sichtbar, Querschnitt passt zu 2D |
| T2 | Je Profil „Bohrung“ hinzufügen | Echte Öffnung, durch sie sieht man die Innenseite; keine roten Körper (Checkbox „Feature-Vorschau“ aus) |
| T3 | Langloch, Rechteckausschnitt, Konturausschnitt | Echte Öffnungen; Konturtext editierbar, Änderung rechnet neu |
| T4 | Schlitz, Ausklinkung (Start/Ende) | Echte Öffnung an der Stirnseite |
| T5 | Gehrung (Start/Ende, Winkel ändern) | Schräge Stirnfläche, Winkeländerung wirkt sofort |
| T6 | Unzulässig: Bohrung auf scharfe Profilkante / I-Steg-Übergang; Schlitz + Gehrung am selben Ende | Feature mit ✗ markiert, roter Fehlertext (`solid.*`), Körper bleibt gültig, kein stilles Ersatzbild |
| T7 | Orbit, Pan, Zoom, „Einpassen“, Preset-Ansichten | flüssig, kein Springen/Verschwinden des Körpers |
| T8 | Klick auf Innenwand einer Bohrung (heranzoomen, ggf. durch Orbit von schräg) | Genau dieses Feature wird in der Liste markiert; Klick auf Außenfläche hebt Auswahl auf |
| T9 | Parameter (Länge, Durchmesser, Position) ändern, Feature löschen/duplizieren | Körper rechnet zuverlässig neu; Löschen stellt Ausgangszustand her |
| T10 | Speichern unter, Programm schließen, Öffnen | Identisches Modell inkl. aller Features, Körper gleich |
| T11 | Viele Features (>20) mit Parameteränderung | Reaktionszeit notieren (Neuberechnung läuft im UI-Thread) |

## Berichten
Pro Fehler: Testfall-Nr, Profil/Parameter, Screenshot, gespeicherte Modelldatei (.json). Bitte keine STEP-Dateien ins Repository.

## Automatische Windows-Tests
```
dotnet test tests/TubeCadCam.Host.Wpf.Tests -c Release
```
Startet das echte Hauptfenster (2 Tests: Szene/Feature-Bindings, Matrix aller Profile × Feature-Arten) und schlägt bei WPF-Bindungsfehlern fehl.
Nicht automatisiert: Maus-Orbit/Pan/Zoom und das Picking – nur manuell (T7, T8).
