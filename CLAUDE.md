# CLAUDE.md – Tube CAD/CAM

## Projektziel
Professionelle, eigenständige CAD/CAM-Software für das Laser- und thermische Schneiden von Rohren und Profilen.
Teil einer langfristigen CAD/CAM-Produktsuite für die Blech- und Metallindustrie.

## Technologiestack
- C# / .NET 10, WPF-Desktop-Oberfläche, MVVM
- Klare Trennung von Domain, Application, Infrastructure und Host
- Automatisierte Unit-, Integrations- und Regressionstests
- Lokale Datenhaltung mit späterer Erweiterungsmöglichkeit

## Architekturprinzipien
1. Eigenständiger Geometrie- und CAM-Kern.
2. Keine direkte Abhängigkeit vom Sheet-CAM-Kern.
3. Gemeinsame Leitzentrale über klar definierte Schnittstellen.
4. Geometrie, CAM, Nesting und NC-Ausgabe strikt trennen.
5. Maschinenabhängige Funktionen über konfigurierbare Adapter.
6. KI darf nur kontrollierte, validierte Anwendungsfunktionen aufrufen.
7. Alle Berechnungen deterministisch und testbar.
8. Keine erfundenen Schneidparameter oder Maschinenfähigkeiten.
9. Keine bestehende CAD/CAM-Software ohne ausdrückliche Freigabe verändern.
10. Lizenzierungsarchitektur berücksichtigen, aber noch nicht implementieren.

## Geometrie
Runde Rohre, Rechteck-/Quadratrohre, offene Profile (U, L, I), benutzerdefinierte Profile, STEP-Import,
parametrische Profilmodelle, 3D-Vorschau und Bearbeitung, Ausschnitte, Durchbrüche, Bohrungen.

## CAM
Rohrspezifischer CAM-Kern: Konturenerkennung, Schneidreihenfolge, An-/Ausfahrwege, Kollisionsprüfung,
Rotationsachsen und Maschinenkinematik, Spannfutter-/Halterungsberücksichtigung, maschinenabhängige
Postprozessoren, Bearbeitungssimulation.

## Nesting
Stangenbelegung, Material-/Profilkompatibilität, Restlängenverwaltung, Schnittspalt,
auftragsübergreifende Optimierung, maschinenabhängige Einschränkungen.

## Integration
Die bestehende Leitzentrale verwaltet Aufträge, Teile, Materialien und Produktionsstatus.
Tube CAD/CAM kommuniziert über versionierte Schnittstellen mit ihr.

## KI und MCP
Die Architektur muss einen späteren MCP-Server ermöglichen. KI-Funktionen dürfen Geometrie analysieren,
Teile vorbereiten, Nesting anstoßen, CAM-Ergebnisse prüfen und freigegebene Arbeitsabläufe ausführen.
NC-Erzeugung muss unabhängig von KI zuverlässig funktionieren.

## Entwicklungsregeln
- Vor jeder Implementierung vorhandenen Code analysieren.
- Kleine, klar abgegrenzte Entwicklungsphasen.
- Zu jeder Funktion passende Tests.
- Keine Platzhalter als fertige Funktionen deklarieren.
- Nach Änderungen Build und relevante Tests ausführen.
- Ergebnisse und offene Probleme dokumentieren.
- Keine Änderungen außerhalb des Projektverzeichnisses ohne Freigabe.
- Vor produktiver NC-Ausgabe vollständige technische Validierung.

## Befehle
```bash
dotnet build                 # gesamte Solution (WPF-Host baut auch unter Linux, läuft nur unter Windows)
dotnet test                  # alle Tests
```
Details: `docs/Bestandsaufnahme.md`, `docs/Architekturplan.md`.
