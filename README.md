# Tube CAD/CAM

CAD/CAM-Software für das Laser- und thermische Schneiden von Rohren und Profilen (C# / .NET 10, WPF, MVVM).

**Stand Phase 2.5:** parametrischer Rohr-CAD-Editor (Profile, 7 Feature-Typen, **echte Öffnungen im 3D-Körper**, Speichern/Laden). Es gibt **noch keine Schneidtechnologie und keine NC-Ausgabe** (das Freigabe-Gate blockiert sie bewusst). Die WPF-Oberfläche ist gebaut, aber noch nicht auf Windows ausgeführt/getestet; es werden keine Normprofile mitgeliefert.

```bash
dotnet build
dotnet test
```
Der WPF-Host (`src/TubeCadCam.Host.Wpf`) baut auch unter Linux, startet aber nur unter Windows.

Windows-UI-Smoke-Test (nur unter Windows ausführbar):
```bash
dotnet test tests/TubeCadCam.Host.Wpf.Tests
```

Dokumentation: [CLAUDE.md](CLAUDE.md) · [Bestandsaufnahme](docs/Bestandsaufnahme.md) · [Architekturplan](docs/Architekturplan.md) · [Phase 2](docs/Phase2-Plan.md) · [Ergebnis Phase 2](docs/Phase2-Ergebnis.md) · [Phase 2.5](docs/Phase25-Ergebnis.md) · [Boolean-Entscheidung](docs/Boolean-Entscheidung.md) · [Koordinatensystem](docs/Koordinatensystem.md) · [Profilkataloge](catalogs/README.md)
