# Tube-CAD-CAM

CAD/CAM-Kern für die Rohr- und Profilbearbeitung (Python-Bibliothek).

## Aktueller Stand
- Rohrprofile: `RoundTube`, `RectangularTube` (Querschnittsfläche, Validierung)

## Entwicklung
```bash
python -m venv .venv && source .venv/bin/activate
pip install -e ".[dev]"
pytest
```

## Roadmap (Vorschlag)
1. Weitere Profile (Oval, L/U/T)
2. 3D-Rohrmodell und Verschneidungen
3. Schnittpfade und G-Code-Ausgabe
