# Koordinatensystem des CAD-Kerns

Implementiert und dokumentiert in `TubeCadCam.Cad.TubeCoordinateSystem`. Es ist **rein geometrisch**; die Abbildung auf
Maschinenachsen ist Aufgabe der Maschinenadapter im CAM-Layer (siehe offene Punkte).

| Größe | Definition |
|---|---|
| Rohrachse | **+X**. Stirnfläche (Start) bei `x = 0`, Ende bei `x = Länge` (Hüllmaß, siehe Gehrung) |
| Querschnitt | liegt in der **Y/Z-Ebene**: Profilpunkt `(u, v)` ↦ Weltpunkt `(x, y, z) = (x, u, v)` |
| Winkel | **rechtshändig um +X**, `0° = +Y`, `90° = +Z`, `180° = −Y`, `270° = −Z`; Bereich `[0°, 360°)` |
| Nullpunkt Querschnitt | Standardprofile: Mittelpunkt der äußeren Bounding-Box; benutzerdefinierte Profile: wie gezeichnet |
| Einheiten | Millimeter, Grad |

Das Weltsystem ist rechtshändig (`X × Y = Z`). Die 3D-Ansicht nutzt **+Z als „oben“**.

## Feature-Positionierung
* Ein Feature mit axialer Position `x` und Winkel `θ` sitzt dort, wo der Strahl vom Querschnittsursprung in Richtung `θ`
  die Außenkontur **zuletzt** trifft (äußerste Fläche). Trifft der Strahl die Kontur nicht (z. B. zur offenen Seite eines
  U-Profils), meldet die Validierung `feature.no-surface`.
* **Lokales Feature-System** (`u, v`): `u` entlang +X (axial), `v` tangential in Richtung wachsenden Winkels. Der
  Ausschnitt wird entlang der Flächennormalen der getroffenen Facette durch die Wand projiziert.
  Das Dreibein `(u, v, Normale nach außen)` ist **linkshändig**; `(u, v, Normale nach innen)` rechtshändig.
* `Rotation` dreht die Kontur in der Flächenebene gegen den Uhrzeigersinn von `u` nach `v`.
* Stirnseitige Features (Schlitz, Ausklinkung, Gehrung) beziehen sich auf `TubeEnd.Start` (x = 0) oder `TubeEnd.End` (x = Länge).
* **Schlitz:** schmaler Schnitt der Breite `Width` und Tiefe `Depth` ab der Stirnfläche, in Richtung `θ` durch den Querschnittsursprung, d. h. durch **beide** Wände.
* **Gehrung:** ebener Schnitt, dessen Ebene die Querschnittsrichtung `TiltAxisAngle` enthält und um `TiltAngle` gegen die
  Senkrechte zur Rohrachse geneigt ist. Positiver Winkel entfernt mehr Material auf der Seite `b` = Achsrichtung + 90°.
  Der kürzeste Punkt der Stirnfläche bleibt bei `x = 0` bzw. `x = Länge`, d. h. `Länge` ist das **Hüllmaß**.

## Bewusste Vereinfachungen
* Winkel bei offenen/nicht-konvexen Profilen beziehen sich auf den Strahl vom Querschnittsursprung, nicht auf eine
  Profilfläche. Ein flächenbezogenes Platzieren (Feature auf „Flansch 1“) ist noch nicht implementiert.
* Die Flächennormale ist die der getroffenen Facette der tesselierten Kontur (Abweichung beim Rundrohr ≤ ca. 0,7°).
