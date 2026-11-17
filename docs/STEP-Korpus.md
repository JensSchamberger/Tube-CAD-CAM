# STEP-Referenzkorpus (Rohre/Profile)

Status: **Infrastruktur vorbereitet, reale Daten nicht verfuegbar.**

## Datenquelle
`C:\Users\JSFot\OneDrive\Dokumente\Schneiddaten\STEP_FILES\STEP-Tubes` (Windows-Pfad des Nutzers).
Die Ausfuehrungsumgebung dieser Sitzung ist ein Linux-Container ohne `C:`-Laufwerk; es war kein Nutzerrechner
angebunden und eine Suche nach `*.step`/`*.stp` im Container ergab keine Treffer. Der Ordner war daher **nicht erreichbar**.
Es wurden keine Daten erfunden und keine Ergebnisse simuliert.

## Was vorbereitet ist
- `TubeCadCam.Infrastructure.StepCorpus.StepCorpusScanner`: rekursive, read-only Erfassung (`*.step`, `*.stp`,
  Gross/Kleinschreibung egal), sortiert, SHA-256, Groesse, Part21-Header (Schema, Dateiname, Hersteller),
  Entitaetsstatistik (u. a. `ADVANCED_FACE`, `CYLINDRICAL_SURFACE`, `PLANE`, `CIRCLE`) und Hinweise
  (`step.no-part21-header`, `step.truncated`, `step.no-faces`, ...). Das Manifest enthaelt keine Dateiinhalte
  und keine absoluten Pfade und ist reproduzierbar (gleiche Dateien -> gleiches Manifest).
- Tests `StepCorpusTests`: Scanner auf kleinen Text-Fixtures (nur Parser, keine Rohrdaten), Determinismus,
  Quelldateien unveraendert, fehlender Ordner = expliziter Fehler `corpus.root-missing`.
  Der Test `Real_corpus_scan_has_no_read_errors` liest `TUBECAD_STEP_DIR`; ohne Daten gibt er
  `SKIPPED (Daten fehlen)` aus und **zaehlt trotzdem als bestanden** (xUnit ohne Skip-Erweiterung) – er belegt dann nichts.
- `.gitignore` schliesst `*.step`, `*.stp` und `step-corpus/` aus. Es werden keine STEP-Dateien committet.

## Nicht umgesetzt (blockiert durch fehlende Daten bzw. fehlenden Importer)
- Punkt 3-4 (Geometrieanalyse, Erkennung von Bohrungen/Ausschnitten/Gehrungen): braucht einen STEP-Importer
  (B-Rep-Parser, Flaechen-/Kantentopologie). `NotSupportedStepImporter` liefert bisher `step.not-implemented`.
  Ohne reale Dateien waere jede Erkennungslogik ungetestet geraten.
- Punkt 6 (Vergleich mit eigener Geometrie) und Punkt 7 (Fehler-/Abweichungsbericht): erst nach Importer und Daten.
  Vorgesehene Kennzahlen: Volumen, Bounding Box, Schnittprofil-Flaeche, Anzahl Oeffnungen, Abweichung Hausdorff-Naeherung.

## Vorgehen, sobald Daten da sind
1. Lokal ausfuehren: `TUBECAD_STEP_DIR="C:\...\STEP-Tubes" dotnet test --filter StepCorpusTests`.
2. Manifest erzeugen (Scanner) und ausserhalb des Repos ablegen; nur Hashes/Statistik duerfen ins Repo (nach Freigabe).
3. Danach Importer (Phase 3) gegen den Korpus entwickeln. NC-Ausgabe bleibt gesperrt.
