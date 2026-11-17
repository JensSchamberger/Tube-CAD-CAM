# Phase 2 – Ergebnis, Testübersicht, Einschränkungen

Stand: 2026-11-17 · Branch `claude/youthful-goodall-gielme`

## Umgesetzt (alle Zahlen aus echten Testläufen auf Linux, .NET 10.0.115)
| Anforderung | Stand |
|---|---|
| Profile: Rund, Quadrat, Rechteck, U, L, I, benutzerdefiniert (Länge, Außenmaße, Wand, Radien, profilspezifische Maße) | ✅ parametrisch, validiert, im Editor änderbar |
| Erweiterbare Profilbibliothek | ✅ Mechanismus (JSON-Kataloge, Pflichtfelder Norm/Quelle, Verified-Flag, Benutzerkatalog). ❗ **Es sind keine Normprofile mitgeliefert** – siehe unten |
| Parametrischer 3D-Kern mit Wandstärke/Innenkontur | ✅ geschlossener, orientierter Körper (Test: wasserdicht, Volumen = Fläche × Länge) |
| Dokumentiertes Koordinatensystem | ✅ `docs/Koordinatensystem.md`, `TubeCoordinateSystem` |
| CAD-Geometrie und CAM-Daten getrennt | ✅ Cad ohne Cam-Bezug (Architekturtest); CAM liest `TubeModel` |
| Feature-Editor: Bohrung, Rechteckausschnitt, Langloch, Schlitz, Stirnbearbeitung (Ausklinkung, Gehrung), Konturausschnitt | ✅ erstellen, ändern, duplizieren, löschen, geometrisch validieren |
| 3D-Oberfläche: Rotation, Zoom, Verschieben, Feature-Auswahl, Parametereingabe, Sofort-Update, 2D/3D-Umschaltung | ✅ gebaut (`dotnet build`), Interaktionslogik getestet; **UI nicht ausgeführt** (siehe unten) |
| Speichern/Laden des vollständigen parametrischen Modells | ✅ versioniertes JSON-Dokument, Roundtrip getestet (inkl. identischer 3D-Geometrie) |
| MCP-Vorbereitung | ✅ `IAppFunction`-Katalog: `analyze_tube_model`, `list_profile_library`, `add_hole_feature` |
| Leitzentrale-Vertrag V1 | ✅ unverändert (Architekturtest sichert Version und Methoden) |
| Lizenzierung | ✅ nicht implementiert (Test stellt sicher, dass keine `ILicenseGate`-Implementierung existiert) |

## Testübersicht (189 Tests, 189 bestanden, 0 fehlgeschlagen)
`dotnet test` (Linux): `TubeCadCam.Tests`

| Klasse | Tests | Inhalt |
|---|---:|---|
| GeometryTests / TriangulatorTests | 5 / 8 | Polygonfläche, Rundung, Triangulierung (konkav, Löcher, unterschiedliche Eckenzahl), Simple-Test, Langloch |
| ProfileTests | 22 | Analytische Fläche vs. Tessellierung, ungültige Maße/Wandstärken, Custom-Profile |
| TubeModelTests | 11 | Phase-1-Validierung (Position, Winkel, Duplikate, …) |
| CadKernelTests | 23 | Wasserdichtheit/Orientierung/Volumen für 8 Profile, Gehrungsformel exakt, Determinismus, Parameteränderungen, Koordinatensystem, Oberflächenortung |
| FeatureTests | 22 | alle 7 Feature-Typen gültig/ungültig, Kanten-/Umfangsprüfung, Überlappungswarnung, Cutter-Lage, Gehrungsgrenzen |
| CamTests | 7 | Konturen, Determinismus, NC-Freigabe-Gate (gesperrt), Maschinenprüfung |
| NestingTests | 8 | unverändert aus Phase 1 |
| ApplicationTests / DocumentAndCatalogTests | 8 / 10 | Repository, Dokument-Roundtrip aller Typen, Katalog (Pflichtfelder, Duplikate, Provider), Update-Use-Cases, MCP-Funktionen |
| ProfileEditorTests / EditorViewModelTests / OrbitCameraTests / LabelTests | 21 / 21 / 6 / 3 | Editor-Logik, Features hinzufügen/ändern/löschen, Auswahl, 2D/3D, Speichern/Laden, Bibliothek, Kamera |
| ArchitectureTests | 14 | Schichtregeln, keine Maschinen-/NC-Begriffe im CAD, Cam⇄Nesting unabhängig, V1-Vertrag, keine Lizenz-Implementierung |

Zusätzlich vorhanden, **hier nicht ausgeführt**: `tests/TubeCadCam.Host.Wpf.Tests` (WPF-Smoke-Test auf echtem Fenster inkl.
Erkennung von Binding-Fehlern). Es wird gebaut (`dotnet build tests/TubeCadCam.Host.Wpf.Tests`), lässt sich unter Linux aber nicht starten
(`Microsoft.WindowsDesktop.App` fehlt – verifiziert: „Test Run Aborted“). Es ist für `windows-latest` in `.github/workflows/ci.yml`
vorgesehen; ein CI-Ergebnis liegt noch nicht vor.

## Bekannte Einschränkungen / nicht implementiert
1. **Keine Normprofile mitgeliefert.** Normbezeichnungen/-maße lassen sich hier nicht gegen die Normen prüfen und wurden daher nicht erfunden. Die Bibliothek ist bis zum Einspielen eines geprüften Katalogs leer (`catalogs/README.md`).
2. **WPF-UI nicht ausgeführt.** Gebaut inkl. XAML-Kompilierung; Rendering, Maus-Interaktion, Picking und Bindings sind **ungetestet auf einem echten Windows-Desktop**. Die 3D-Geometrie wurde zusätzlich mit einem Software-Renderer sichtgeprüft; das ersetzt keinen UI-Test.
3. **Keine Boolesche Subtraktion im 3D-Körper.** Ausschnitte/Bohrungen/Langlöcher/Konturen/Schlitze/Ausklinkungen werden als rote Schnittvolumen **überlagert**; nur die Gehrung ist im Körper enthalten. Die UI weist darauf hin.
4. **STEP-Import weiterhin nicht implementiert** (Port + Fehlermeldung). Keine Konturerkennung aus Solids.
5. **Winkelbezug bei offenen Profilen** über Strahl vom Querschnittsursprung (siehe Koordinatensystem); kein flächenbezogenes Platzieren.
6. **CAM:** Konturen nur für Wandschnitte (Bohrung, Rechteck, Langloch, Kontur, Ausklinkung). Schlitz und Gehrung werden als „nicht erkannt“ gemeldet. Weiterhin keine Schneidreihenfolge, Kollisionsprüfung, Kinematik, Postprozessoren, Simulation → **NC-Ausgabe bleibt gesperrt**.
7. Validierungsgrenzen (`MaxEdgeTurnDeg = 20°`, `MaxWrapDeg = 60°`) sind CAD-Heuristiken, in `FeatureValidationOptions` änderbar – keine Technologiedaten. Überlappungsprüfung ist eine Näherung (nur Warnung).
8. Kein Undo/Redo, keine Mehrfachauswahl, keine Maßpfeile in 2D, keine Features-Auswahl in der 2D-Ansicht.
9. UI-Texte der Anwendung sind Deutsch; Validierungsmeldungen aus dem Kern sind Englisch.
10. Offene fachliche Punkte: Koordinatenkonvention gegenüber realen Maschinen (Winkel-0-Lage, Drehsinn) muss je Maschinenadapter abgebildet werden; Leitzentrale-Spezifikation fehlt weiterhin.
