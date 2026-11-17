# Entscheidung: Boolesche Geometrie für den Rohrkörper

Stand: 2026-11-17 · Phase 2.5

## 1. Anforderung
Bohrungen, Rechteckausschnitte, Langlöcher, Konturausschnitte, Schlitze, Ausklinkungen und Gehrungen müssen als
**tatsächliche Öffnungen** im 3D-Körper entstehen (geschlossener Volumenkörper, keine doppelten/degenerierten Dreiecke,
saubere Schnittkanten, Wandstärke und Innenkontur, alle Profiltypen). Features behalten ihre ID, Flächen und Kanten sind
einem Feature zuordenbar, Toleranzen zentral, **keine** Maschinenlogik im CAD-Kern. Nicht sicher Berechenbares muss
ausdrücklich fehlschlagen; keine stille Ersatzdarstellung.

Zusätzliche Randbedingung dieser Umgebung: Tests laufen unter **Linux** (CI-/Entwicklungsumgebung), Produktion unter Windows.
Ein Kernel, der nur unter Windows läuft, wäre hier nicht testbar.

## 2. Untersuchte Verfahren und Bibliotheken
Geprüft anhand der tatsächlichen NuGet-Pakete (Metadaten, Paketinhalt) und – wo möglich – des Quelltexts.

| Kandidat | Art | Lizenz | Plattform | Befund |
|---|---|---|---|---|
| **ManifoldNET** 1.0.7-alpha | nativer Mesh-Boolean (Manifold) | im Paket nicht angegeben (Upstream laut meiner Kenntnis Apache-2.0, **nicht verifiziert**) | Paket enthält nur `win-x64` + `tbb12.dll` | Alpha-Stand; unter Linux nicht ladbar → nicht testbar |
| **OcctSharp** 8.0.1-preview / `occt` 7.5.0 | Open CASCADE (BRep-Boolean) | OcctSharp: MIT (laut nuspec); OCCT selbst: nicht verifiziert | „Windows x64“, Preview, bis ca. 90 MB native Binaries | Wäre fachlich der stärkste Kernel, aber unter Linux nicht testbar, Preview-Reife unklar |
| **geometry3Sharp** 1.0.324 | verwaltetes Mesh-Toolkit mit `MeshBoolean` | Boost 1.0 (im Repository geprüft) | netstandard2.0, plattformneutral | `MeshBoolean` ist laut Quelltext **experimentell**: berechnet eine Vereinigung (keine Differenz), kommentiert „tracking on-cut vertices is not working yet“ und „no good for coplanar regions“, meldet Fehler nur per `Console.WriteLine`, hängt zwei Netze zusammen statt zu verschweißen. Keine belastbare Basis. |
| **MeshLib** 3.x | natives Mesh-Toolkit | nicht geprüft | – | nicht weiter verfolgt (Lizenzbedingungen ungeklärt) |
| **CSG-/BSP-Pakete** (`Csg`, `cork` 0.1.0-alpha) | BSP-Boolean | – | – | nur Alpha-Pakete; BSP-Ergebnisse sind Polygonsuppen mit T-Stellen, nicht wasserdicht indiziert |
| **Clipper2** 1.5.4 | **2D**-Polygon-Boolean (Ganzzahl-Arithmetik) | Boost 1.0 (License.txt im Paket geprüft) | netstandard2.0, plattformneutral | sehr robust, verwaltet, ausgereift – aber nur 2D |

Keine eigene allgemeine 3D-Boolean-Engine: Das wäre ohne die obigen Garantien (Robustheit bei koplanaren Flächen,
Topologie-Garantie) nicht vertretbar.

## 3. Entscheidung
**Clipper2 für sämtliche 2D-Booleans + ein zweckgebauter Randflächen-Konstruktor für die spezielle Geometrieklasse
„Extrusionskörper minus Prismen“.** Das ist ausdrücklich keine allgemeine Boolean-Engine.

Begründung:
1. Der Rohrkörper ist eine **lineare Extrusion** des Querschnitts entlang X (Gehrungen: zwei Ebenen). Jeder Schneidkörper ist ein
   **Prisma**, dessen Achse in der Querschnittsebene liegt (senkrecht zur Rohrachse). Entlang einer solchen Achse besteht das
   Material aus einem Stapel von Platten (Wandpaaren). Dadurch lässt sich der Rand des Ergebnisses aus **exakten 2D-Problemen**
   zusammensetzen, die Clipper2 robust löst.
2. Der Konstruktor liefert die Netz-Topologie **per Konstruktion** (gemeinsame Triangulierung für Außen- und Innenfläche der Wand,
   gemeinsame Randpunkte für Wand und Flächen) und prüft sie anschließend mit einem unabhängigen Topologietest.
3. Er liefert die **Herkunft** jeder Fläche und Kante (Feature-ID) kostenlos mit – Voraussetzung für CAM.
4. Er läuft auf allen Plattformen und ist deterministisch (feste Tessellierung, Rasterkoordinaten, geordnete Verarbeitung).
5. Alles, was außerhalb dieser Klasse liegt, wird **erkannt und benannt abgelehnt** statt falsch berechnet.

Eine spätere Ergänzung um einen BRep-Kernel (OcctSharp) bleibt möglich: `TubeSolidBuilder` ist die einzige Stelle, die das
Ergebnis (`TubeSolid`) liefert; ein Kernel müsste nur diese Schnittstelle bedienen.

## 4. Verfahren (Kurzfassung)
Koordinaten: pro Schneidrichtung `N` ist `T = N` um +90° gedreht, `t = p·T`, Höhe `h = p·N`; Fußabdruck des Features in `(x, t)`.

1. **Features → Schneider:** Wandschnitte (Bohrung, Rechteck, Langloch, Kontur, Ausklinkung) mit der Flächennormalen der getroffenen
   Facette als Richtung (Modus *oberste Schicht*), Schlitz mit seiner Richtung (Modus *alle Schichten*).
2. **Gruppen:** gleiche Richtung und Modus und überlappender `t`-Bereich → ein gemeinsamer Fußabdruck (Vereinigung).
3. **Planung je Gruppe:** Streifen entlang `t` an allen Profileckpunkten; je Streifen die Facetten nach Höhe sortieren und zu Wandpaaren
   (unten/oben) zusammenfassen. Prüfungen: Normalen, Dicke > 0, Paare setzen sich von Streifen zu Streifen **stetig** fort
   (sonst quert der Schnitt eine Profilkante), jede beteiligte Facette ist in allen Streifen, die sie überspannt, Paarmitglied.
4. **Flächen:** je Streifen und Paar `Gebiet − Fußabdruck` (Clipper2, Ganzzahlraster 1e-6 mm), **eine** Triangulierung
   (Ear-Clipping mit Loch-Brücken, toleranzbewusst, dann Delaunay-Kantentausch), je einmal auf die obere und die untere Facette gehoben.
5. **Öffnungswände:** Randkanten des Gebiets, die zum Fußabdruck gehören, werden zu Vierecken zwischen oberer und unterer Facette
   (Normale in die Öffnung). Daraus entstehen auch die **Schnittkanten** (`FeatureEdge`).
6. **Stirnflächen:** aus den verfeinerten Konturen (inkl. Nahtpunkte); wo Ausklinkung/Schlitz die Stirnfläche erreichen, wird das
   Wandpaar-Trapez (um 1 µm entlang der Schnittrichtung verlängert, um deckungsgleiche Kanten zu vermeiden) abgezogen.
7. **Abschluss:** Verschweißen (`Tolerance.Weld`), Topologieprüfung (`MeshTopology`: offene Kanten, Nicht-Mannigfaltigkeit, Orientierung,
   degenerierte und doppelte Dreiecke, Volumen > 0), Normalenglättung (Knickwinkel 30°).
   Ist ein Gesamtergebnis ungültig, werden die Features einzeln hinzugefügt und das schuldige als `solid.topology` gemeldet.

## 5. Technische Grenzen (ausdrücklich)
Folgende Fälle schlagen **benannt fehl** (`TubeSolid.Failures`, Anzeige in der Validierungsliste, Feature mit ✗ markiert):

| Code | Bedeutung / typischer Fall |
|---|---|
| `solid.profile-edge` | der Schnitt quert eine Profilkante (scharfe Ecke, Steg-Flansch-Übergang, Innenhohlraum-Rand) oder liegt direkt daran |
| `solid.pairing` | die Wandschicht entlang der Schnittrichtung ist keine einfache Platte (z. B. Schnitt entlang einer Fläche) |
| `solid.interaction` | der Schnitt überlappt Flächen, die ein früheres Feature mit **anderer Schnittrichtung/-art** bereits ändert (gleiche Richtung wird vereinigt); Reihenfolge im Modell entscheidet |
| `solid.end-unsupported` | Ausklinkung/Schlitz erreicht eine **gehrte** Stirnfläche, oder ein Nicht-End-Feature ragt über die Stirnfläche |
| `solid.too-close-to-end` | Abstand zur Stirnfläche kleiner als 1 µm (Steg dünner als die Toleranz) |
| `solid.no-surface` / `solid.invalid-parameters` | Winkel trifft das Profil nicht / ungültige Maße |
| `solid.seam-too-close` | Profilpunkte liegen enger als die Schweißtoleranz neben dem Schnitt |
| `solid.internal` | interner Konsistenzfehler der Planung (Fußabdruck außerhalb des neu vernetzten Bereichs); sollte nicht auftreten |
| `solid.topology` | das Ergebnis wäre kein gültiger geschlossener Körper (Sicherheitsnetz, sollte nicht auftreten) |

Weitere Grenzen: Schnitte stehen **senkrecht zur Rohrachse** (Prismen mit Achse in der Querschnittsebene; keine Schrägschnitte der
Feature-Achse); jede Wand wird als Platte entlang der Flächennormalen der getroffenen Facette geschnitten; Netz besteht aus langen
schmalen Dreiecken (Eigenschaft der Extrusionsfacetten – für Anzeige und Kantenextraktion geeignet, **nicht** für FE-Netze).

## 6. Absicherung
* Unabhängige Prüfungen: exakte Volumendifferenzen (Fläche × Wandstärke), Umfang × Wandstärke der Lochwand, Punkt-in-Körper per
  Strahlverfolgung (Öffnung offen, Rest intakt), **Orakeltest** (Tausende Zufallspunkte gegen eine direkte Mitgliedschaftsdefinition,
  0 Abweichungen), Topologie jedes gelieferten Körpers (immer gültig), Determinismus, Speichern/Laden → identische Geometrie.
* Sichtprüfung der Geometrie mit einem eigenen Z-Buffer-Renderer (Python, nur Entwicklungswerkzeug, nicht Teil des Produkts).
