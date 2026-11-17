# Reproduzierbares Beta-Paket (Windows x64, self-contained). Aufruf im Repo-Root:  pwsh tools/build-beta.ps1
$ErrorActionPreference = 'Stop'
$out = Join-Path $PSScriptRoot '..\dist\TubeCadCam-Beta-win-x64'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
dotnet publish "$PSScriptRoot\..\src\TubeCadCam.Host.Wpf" -c Release -r win-x64 --self-contained true -o $out
Copy-Item "$PSScriptRoot\..\docs\Beta-Testanleitung.md" $out
Compress-Archive -Path "$out\*" -DestinationPath "$out.zip" -Force
(Get-FileHash "$out.zip" -Algorithm SHA256).Hash | Out-File "$out.zip.sha256"
Write-Host "Paket: $out.zip"
