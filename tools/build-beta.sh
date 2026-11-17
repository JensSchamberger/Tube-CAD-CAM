#!/usr/bin/env bash
# Wie build-beta.ps1, fuer Linux/macOS (Cross-Publish; das Ergebnis laeuft nur unter Windows x64).
set -euo pipefail
cd "$(dirname "$0")/.."
out=dist/TubeCadCam-Beta-win-x64
rm -rf "$out" "$out.zip" "$out.zip.sha256"
dotnet publish src/TubeCadCam.Host.Wpf -c Release -r win-x64 --self-contained true -o "$out"
cp docs/Beta-Testanleitung.md "$out/"
(cd dist && zip -qr TubeCadCam-Beta-win-x64.zip TubeCadCam-Beta-win-x64 && sha256sum TubeCadCam-Beta-win-x64.zip > TubeCadCam-Beta-win-x64.zip.sha256)
echo "Paket: $PWD/$out.zip"
