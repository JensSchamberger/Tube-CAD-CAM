# Architekturplan

## Schichten und erlaubte Abhängigkeiten
```
Host.Wpf ─► Presentation ─► Application ─► Cam ─► Cad ─► Domain ─► Geometry
   │                            │   └────► Nesting ─► Domain
   └────► Infrastructure ───────┘
```
| Projekt | Verantwortung | Abhängig von |
|---|---|---|
| `Geometry` | Vektoren, Polygone, Dreiecksnetz, Triangulierung – keine Fachlogik | – |
| `Domain` | Profildefinitionen (parametrisch), Material, Teil, Auftrag, Status, `Result` | Geometry |
| `Cad` | Features (7 Typen), `TubeModel`, Validierung, Koordinatensystem, Querschnitts- und 3D-Körper-/Szenengenerator | Domain, Geometry |
| `Cam` | Konturen, Planer, Maschinenmodell/-adapter, Postprozessor-Schnittstelle, **NC-Freigabe-Gate** | Cad, Domain, Geometry |
| `Nesting` | 1D-Stangenbelegung mit Schnittspalt/Restlänge | Domain |
| `Application` | Use Cases, Ports (Repository, STEP, Lizenz), Leitzentrale-Vertrag V1, **Funktionskatalog** (MCP-Basis) | Cam, Cad, Nesting, Domain |
| `Infrastructure` | JSON-Dateispeicher, Adapter für Ports | Application, Cad, Domain |
| `Presentation` | MVVM-ViewModels, Orbit-Kamera, Auswahl (plattformunabhängig, testbar) | Application, Cad, Domain |
| `Host.Wpf` | Composition Root, Views, 3D-Rendering (net10.0-windows) | Presentation, Infrastructure |

Die Regeln sind als Test festgeschrieben (`ArchitectureTests`). Cam und Nesting kennen sich nicht (Prinzip 4).

## Umsetzung der Prinzipien
- **Eigener Kern / keine Sheet-CAM-Abhängigkeit:** Geometry/Cad/Cam sind in sich geschlossen.
- **Leitzentrale:** `IControlCenterGateway` + DTOs mit `ControlCenterApi.Version = "1.0"`; Implementierung folgt, sobald die Spezifikation vorliegt.
- **Maschinenadapter:** `MachineDefinition` enthält nur konfigurierte Werte; fehlende Angabe = nicht fähig (z. B. `machine.length-unknown`).
- **KI/MCP:** Nur `IAppFunction`s im `AppFunctionCatalog` (feste Namen, deklarierte Argumente, `IsReadOnly`, Eingabevalidierung). Ein MCP-Server wäre ein dünner Adapter darüber. Die NC-Erzeugung hängt nicht daran.
- **NC-Sicherheit:** `NcReleaseGate` blockiert NC-Ausgabe, solange ein Plan nicht `Validated` ist. In Phase 1 kann kein Plan `Validated` werden → NC-Ausgabe ist gesperrt.
- **Determinismus:** feste Tessellierung, ordinale Tie-Breaker im Nesting, Test auf Eingabereihenfolge-Unabhängigkeit.
- **Lizenzierung:** nur Seam `ILicenseGate`, keine Implementierung.

## Phasenplan
| Phase | Inhalt | Status |
|---|---|---|
| 1 | Gerüst, Profilmodelle, Features, Konturen aus Features, 1D-Nesting-Baseline, JSON-Speicher, Funktionskatalog, WPF-Profileditor | **erledigt** |
| 2 | Profilbibliothek (Mechanismus), 3D-Kern, Feature-Editor, interaktive 3D-UI, Dokument speichern/laden – siehe `docs/Phase2-Ergebnis.md` | **erledigt** (UI nur gebaut, nicht ausgeführt; keine Normdaten) |
| 3 | STEP-Import (Bibliothek wählen), Konturerkennung aus Solids | offen |
| 4 | Maschinenkonfiguration (Datei), Kinematikmodell, Schneidreihenfolge, An-/Ausfahrwege | offen |
| 5 | Kollisions-/Spannfutterprüfung, Simulation | offen |
| 6 | Postprozessor je Maschine, Validierung → erst dann Freigabe des Gates | offen |
| 7 | Leitzentrale-Anbindung (V1), auftragsübergreifendes Nesting, MCP-Server | offen |

## Offene Punkte / Entscheidungen
1. Spezifikation der Leitzentrale-Schnittstelle (Transport, Format, Auth).
2. Erste Zielmaschine(n) und Kinematik; Quelle der Technologiedaten.
3. STEP-Bibliothek (z. B. Open CASCADE über Wrapper) – Lizenz und Verteilung prüfen.
4. Konvention für das Rohr-Koordinatensystem (Nullpunkt, Drehsinn, Winkel-0-Lage) mit Fachleuten abstimmen; aktuell: Winkel in Grad [0,360), axiale Position ab Stirnfläche.
5. Nesting über 1D-Baseline hinaus (Optimalität, Auftragsübergreifend, Restlagerbestand).
