# Technische Bestandsaufnahme (Phase 0)

Stand: 2026-11-17

## Repository
- Ein einziger Commit („Initial commit“); einzige Datei war `README.md` (nur Titel).
- Kein vorhandener Quellcode, keine Solution, keine Tests, keine CI → **Greenfield**. Es gab nichts zu migrieren und keine bestehende CAD/CAM-Software, die verändert werden könnte.

## Toolchain
| Punkt | Befund |
|---|---|
| .NET SDK in der Entwicklungsumgebung | war nicht installiert; `dotnet-sdk-10.0` (10.0.115) per apt aus den Ubuntu-24.04-Quellen installiert |
| WPF | baut unter Linux via `EnableWindowsTargeting`; **starten/UI-Test nur unter Windows** möglich |
| Test-Framework | xUnit 2.9.3 (zentrale Paketverwaltung) |

## Nicht vorhanden / außerhalb des Repos (Annahmen, noch zu klären)
- Leitzentrale: keine Spezifikation, keine API-Beschreibung → Schnittstelle v1 ist ein **Entwurf** (`Application/Contracts/ControlCenter/V1`).
- Zielmaschinen, Kinematik, Technologie-/Schneiddaten: keine Quellen. Gemäß Prinzip 8 wurde nichts erfunden.
- STEP-Bibliothek: noch nicht ausgewählt (Lizenz/Technologie offen).
- Sheet-CAM-Kern: bewusst keine Abhängigkeit.
