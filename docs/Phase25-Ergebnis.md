# Phase 2.5 – Echte 3D-Bearbeitungen: Ergebnisbericht

Stand: 2026-11-17 · Branch `claude/youthful-goodall-gielme` · Entscheidungsdokument: [Boolean-Entscheidung.md](Boolean-Entscheidung.md)

## Ergebnis in Kürze
Der 3D-Körper enthält jetzt die **tatsächlichen Öffnungen und Materialentfernungen** (Bohrung, Rechteckausschnitt, Langloch,
Konturausschnitt, Schlitz, Ausklinkung; Gehrung war bereits exakt). Die roten Schnittvolumen sind keine Bearbeitung mehr, sondern nur
noch eine **optionale transparente Vorschau**. Nicht sicher berechenbare Features werden **benannt als fehlgeschlagen** gemeldet und
sind *nicht* im Körper.

Alle Zahlen stammen aus echten Läufen unter Linux (.NET 10.0.115). Die WPF-Oberfläche wurde **nicht** unter Windows ausgeführt.

## Umgesetzt
| Anforderung | Stand |
|---|---|
| Subtraktion: Bohrung, Rechteck, Langloch, Kontur, Schlitz, Ausklinkung, Gehrung | ✅ mit Einschränkungen (siehe unten) |
| Innen-/Außenflächen, Wandstärke | ✅ je Wandpaar Außen- und Innenfläche + Öffnungswand |
| Geschlossene gültige Körper, keine doppelten/degenerierten Dreiecke | ✅ jeder gelieferte Körper besteht die Topologieprüfung |
| Alle Profiltypen | ✅ Rund, Rechteck/Quadrat (Radien 0 und >0), U, L, I, Custom – wo ein Schnitt die Wandschicht nicht eindeutig lässt, benannte Ablehnung |
| Bibliotheksentscheidung, Lizenz, Grenzen | ✅ [Boolean-Entscheidung.md](Boolean-Entscheidung.md) |
| Parametrisches Modell bleibt führend | ✅ der Körper wird bei jeder Änderung aus `TubeModel` neu abgeleitet; gespeichert wird nur das Modell |
| Feature-IDs, Flächen-/Kantenherkunft | ✅ `FaceTag` je Dreieck (Kind, Schleife, Feature-ID, Stirnende), `FeatureEdge` (obere/untere Schnittkante als geordnete Schleife) |
| Toleranzen zentral | ✅ `Geometry.Tolerance` (Raster, Schweißen, Mindestfläche, Richtungsgleichheit); Modellierungsoptionen in `SolidOptions` |
| CAD-Tessellierung = CAM-Kontur | ✅ `FeatureTessellation` (128 Segmente / 12 Eckensegmente) für Solid **und** `FeatureContourRecognizer`; getestet |
| Keine Maschinenlogik im CAD, keine NC-Freigabe | ✅ Architekturtests; `NcReleaseGate` blockiert weiterhin |
| 3D-Ansicht: bearbeiteter Körper, Vorschau optional | ✅ gebaut; Auswahl durch Klick auf die Lochwand (Dreieck → Herkunft); **nicht unter Windows ausgeführt** |

## Testübersicht (276 Tests, 276 bestanden, 0 fehlgeschlagen)
`dotnet test` (Linux). Neue/erweiterte Gebiete der Phase 2.5 **fett**.

| Klasse | Tests | Inhalt |
|---|---:|---|
| **SolidTests** | 59 | Bohrung durch Wand (exaktes Volumen, Wandfläche = Umfang × Wandstärke, Öffnung offen/Rest intakt, Wandnormalen, Schnittkanten), Rund- und Rechteckrohr; **alle Wandschnitt-Typen** analytisch; Ausklinkung/Schlitz inkl. Stirnflächenänderung; Langlöcher auf allen vier Flächen, U-Flansch/Steg, L, I (Steg/Flansch), Custom; Übergang Fläche→Radius gültig, **scharfe Kante/Steg-Übergang → benannte Ablehnung**; **Überlappungen** (gleiche Richtung vereinigt, andere Richtung abgelehnt, Loch im Langloch); **Nähe zur Stirnfläche** (0,5 mm ok, 0 und 0,5 µm abgelehnt); **Gehrung + Ausschnitte** (Volumen), Gehrung + Ausklinkung/Schlitz am selben Ende abgelehnt; Matrix Profil × Feature (Körper immer gültig, Fehlschlag ändert nichts); Herkunft/Kanten; Determinismus; Entfernen stellt Ausgang her; Parameteränderungen |
| **SolidOracleTests** | 3 | unabhängige Punkt-in-Körper-Prüfung (Tausende Zufallspunkte, 0 Abweichungen) für Rechteckrohr mit Loch, Langloch, Kontur, Seitenausschnitt, Ausklinkung, Gehrung sowie Schlitz; Schlitz mit gemeinsamer Wand wird abgelehnt |
| **SolidDocumentAndViewModelTests** | 8 | Speichern/Laden → **identische Geometrie** (Positionen, Indizes, Tags, Volumen, Kanten); Editor: Öffnung sichtbar im Körper, jede Parameteränderung rechnet neu, Löschen stellt Körper her, fehlgeschlagenes Feature markiert (✗) und als Fehler gelistet und nicht gezeichnet, Vorschau optional, Auswahl über Flächenherkunft |
| **GeometryBooleanTests** | 14 | Clipper2-Wrapper (Differenz, Union, Schnitt, Raster, Kanteneinfügung), Toleranzkonsistenz, Topologieanalyse (offen/gedreht/degeneriert/doppelt/nicht-mannigfaltig), Verschweißen, Normalenglättung, Triangulator-Qualität (flache Splitter, Delaunay-Eigenschaft) |
| ArchitectureTests | 17 | Schichten, **Clipper2 nur in Geometry**, Solid-Bau im CAD ohne Cam/Nesting/Infrastruktur, NC bleibt gesperrt, V1-Vertrag, keine Lizenz-Implementierung |
| CadKernelTests / FeatureTests / TubeModelTests / ProfileTests / GeometryTests / TriangulatorTests | 23 / 22 / 11 / 22 / 5 / 8 | unverändert aus Phase 2 |
| CamTests / NestingTests / ApplicationTests / DocumentAndCatalogTests | 7 / 8 / 8 / 10 | unverändert |
| ProfileEditorTests / EditorViewModelTests / OrbitCameraTests / LabelTests | 21 / 21 / 6 / 3 | unverändert (Ausnahmen siehe unten) |

### Änderungen an bestehenden Tests (offengelegt)
Keine Assertion wurde abgeschwächt oder entfernt.
* `scene.Features` → `scene.PreviewShapes` (reine Umbenennung der Eigenschaft an allen Zugriffsstellen in `FeatureTests`, `CadKernelTests`, `PresentationTests`, `DocumentAndCatalogTests`; die Cutter-Vorschau gibt es weiter).
* `EditorViewModelTests.Save_and_reload_restores_the_complete_parametric_model`: Schlitz und Gehrung lagen am **selben Ende**. Diese Kombination
  wird jetzt ausdrücklich als nicht berechenbar gemeldet (`solid.end-unsupported`, getestet in `SolidTests`). Der Test setzt den Schlitz an
  das andere Ende; alle sieben Feature-Typen, `HasErrors == false` und der Round-Trip bleiben unverändert geprüft.
* Windows-UI-Smoke-Test: Erwartungen an die Zahl der Vorschau-Volumen an das neue Verhalten angepasst (Vorschau nur für das ausgewählte Feature bzw. bei aktivierter Option) und um Prüfungen von Körper-Dreiecken und Fehlern ergänzt.

### Nicht ausgeführt
* `tests/TubeCadCam.Host.Wpf.Tests` (echtes Fenster, Binding-Fehler, Vorschau-/Körperzahlen) – **gebaut, aber nur unter Windows ausführbar**. Ein CI-Ergebnis liegt nicht vor.

## Laufzeit (Release, aufgewärmt)
Rohr ohne Feature 11 ms · 1 Bohrung 28–46 ms · 5 gemischte Features 41 ms · 10 Bohrungen auf einer Fläche 191 ms (5 720 Dreiecke) ·
12 Bohrungen rund um ein Rundrohr 173 ms (13 520 Dreiecke). Im Debug-Build etwa dreimal so langsam. Die Neuberechnung läuft synchron im UI-Thread.

## Bekannte Einschränkungen
Ausführlich in [Boolean-Entscheidung.md](Boolean-Entscheidung.md) §5. Wesentlich:
1. **Kein allgemeiner 3D-Boolean.** Nur Prismen senkrecht zur Rohrachse, Wandschnitt als oberste Plattenschicht je Schnittlinie.
2. **Benannt abgelehnt:** Schnitte über scharfe Kanten/Steg-Übergänge/Hohlraumränder, Überlappungen mit anderer Schnittrichtung (Reihenfolge entscheidet), Schlitz/Ausklinkung an gehrter Stirnfläche, Abstand zur Stirnfläche < 1 µm.
   Beispiel: Auf einem I-Profil scheitert ein Loch mittig über dem Steg, weil dort Steg und Flansch eine durchgehende Schicht bilden; neben dem Steg (Flanschüberstand) oder im Steg funktioniert es.
3. Die Windows-Oberfläche (Rendering, Maus, Auswahl, Bindings) ist weiterhin **ungetestet auf Windows**. Die Geometrie wurde zusätzlich mit einem Software-Z-Buffer-Renderer sichtgeprüft (zeigt echte Öffnungen, Ausklinkung, Schlitz, schräge Gehrung).
4. Netzqualität: lange, schmale Dreiecke (Extrusionsfacetten, Seitenverhältnis im Mittel um 20:1, 95-%-Quantil 2 000–5 000:1, Einzelwerte über 10⁴:1; schon das Rohr ohne Feature hat 2 000:1) – für Anzeige und Kantenextraktion geeignet, nicht für FE.
5. CAM: Konturen weiterhin nur für Wandschnitte; Schlitz und Gehrung bleiben „nicht erkannt“. **NC-Ausgabe bleibt gesperrt**, keine Postprozessoren, keine Maschinenlogik.
6. Weiter offen aus Phase 2: STEP-Import, Undo/Redo, keine Normprofile mitgeliefert, Leitzentrale-Spezifikation, Maschinenkonvention.

## Nachtrag: Bestanden / Fehlgeschlagen / Übersprungen
Der STEP-Korpus-Test (`RequiresStepCorpusFact`) wird ohne erreichbaren Korpus von xUnit als **Übersprungen** gemeldet und nie als bestanden.
Stand: 281 Tests, davon 280 bestanden, 0 fehlgeschlagen, 1 übersprungen (reale STEP-Daten nicht verfügbar). Geometrietests
(Volumen, Topologie, Orakel) laufen ausschließlich auf parametrisch erzeugten Körpern und stützen sich nicht auf den Korpus.

## Bestandsaufnahme Materialentfernung (Feature → echte Geometrie)
| Feature | Echte Öffnung | Bemerkung |
|---|---|---|
| Bohrung, Langloch, Rechteck-/Konturausschnitt | ja | Wandschnitt je Schnittlinie; Kantenüberschreitung → `solid.profile-edge` |
| Schlitz, Ausklinkung | ja | Stirnseite; Kombination mit Gehrung am selben Ende → `solid.end-unsupported` |
| Gehrung | ja | Endebenen der Extrusion, exakt |
