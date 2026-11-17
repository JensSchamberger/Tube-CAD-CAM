# Tube CAD/CAM

CAD/CAM-Software für das Laser- und thermische Schneiden von Rohren und Profilen (C# / .NET 10, WPF, MVVM).

**Stand Phase 1:** kompilierbares, getestetes Gerüst. Es gibt **noch keine Schneidtechnologie und keine NC-Ausgabe** (das Freigabe-Gate blockiert sie bewusst).

```bash
dotnet build
dotnet test
```
Der WPF-Host (`src/TubeCadCam.Host.Wpf`) baut auch unter Linux, startet aber nur unter Windows.

Dokumentation: [CLAUDE.md](CLAUDE.md) · [Bestandsaufnahme](docs/Bestandsaufnahme.md) · [Architekturplan](docs/Architekturplan.md)
