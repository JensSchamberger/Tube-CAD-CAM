# Phase 2 – Implementierungsplan: parametrischer Rohr-CAD-Editor

## Entscheidungen
| Thema | Entscheidung |
|---|---|
| Koordinatensystem | Rohrachse = +X, Querschnitt in der YZ-Ebene, Winkel rechtshändig um +X ab +Y (siehe `docs/Koordinatensystem.md`) |
| Normprofile | Erweiterbarer Katalog (JSON) mit Pflichtfeldern *Norm*, *Quelle*, *Verified*. **Es werden keine Normdaten mitgeliefert** (nicht verifizierbar → nicht erfunden). Benutzerkatalog wird unterstützt. |
| 3D-Körper | Eigener, deterministischer Netzgenerator (Schweep des Querschnitts inkl. Wandstärke/Innenkontur, Kappen per Ear-Clipping mit Lochbrücken, Gehrungsschnitt exakt) |
| Feature-Darstellung | Schnittvolumina (Cutter) als Overlay – **keine** Boolesche Subtraktion im Netz (ausdrücklich gekennzeichnet) |
| Feature-Typen | Bohrung, Rechteckausschnitt, Langloch, Konturausschnitt, Schlitz (stirnseitig, durch beide Wände), Ausklinkung (stirnseitig), Gehrung |
| Interaktive 3D-UI | WPF `Viewport3D`, eigene Orbit-Kamera (Logik testbar in `Presentation`), Picking per Hit-Test |
| Persistenz | Versioniertes Dokument (`SchemaVersion`), JSON |
| MCP-Vorbereitung | Weitere `IAppFunction`s (Katalog lesen, Bohrung hinzufügen) |
| Tests | xUnit (Linux/Windows); WPF-Smoke-Test nur Windows (in CI), hier nur kompiliert |

## Schritte
1. Geometrie: Polygon-Operationen, Dreiecksnetz, Triangulierung mit Löchern
2. Domain: Wandstärke-Metadaten, Profilkatalog
3. CAD: Koordinatensystem, Oberflächenortung, Features, Validierung, Körper-/Szenengenerator
4. CAM: Konturen für neue Features (CAM bleibt CAD-lesend, kein Maschinenbezug im CAD)
5. Application/Infrastructure: Dokumentenspeicher, Katalog-Provider, Update-Use-Cases, MCP-Funktionen
6. Presentation: Editor-ViewModels, Kamera, Auswahl, 2D/3D-Umschaltung
7. Host: 3D-Ansicht, Menüs, Dialoge
8. Tests, Doku, Abschlussbericht
