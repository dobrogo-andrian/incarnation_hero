<#
.SYNOPSIS
    Exports the Windows release build and zips it for testers.
.DESCRIPTION
    Runs the "Windows Desktop" preset of game/export_presets.cfg headless: a single
    IncarnationHero.exe with the game, the C# assemblies and the content (embedded in Core)
    inside. The zip lands in build/ as IncarnationHero-<version>-<commit>.zip, where the version
    is application/config/version from game/project.godot.
    Needs the Godot 4.7.2 .NET editor and its export templates (Editor > Manage Export Templates).
.PARAMETER Godot
    Path to the Godot .NET editor executable; defaults to the GODOT environment variable.
#>
param([string]$Godot = $env:GODOT)

$ErrorActionPreference = 'Stop'
if (-not $Godot -or -not (Test-Path $Godot)) {
    throw "Godot editor not found: pass -Godot <path to Godot_v4.7.2-stable_mono_win64.exe> or set GODOT."
}

$root = Split-Path $PSScriptRoot -Parent
$game = Join-Path $root 'game'
$outDir = Join-Path $root 'build\windows'
$exe = Join-Path $outDir 'IncarnationHero.exe'

$version = (Select-String -Path (Join-Path $game 'project.godot') -Pattern '^config/version="(.+)"').Matches[0].Groups[1].Value
$commit = (git -C $root rev-parse --short HEAD).Trim()
if (git -C $root status --porcelain) { $commit += '-dirty' }
$zip = Join-Path $root "build\IncarnationHero-$version-$commit.zip"

if (Test-Path $outDir) { Remove-Item $outDir -Recurse -Force }
New-Item -ItemType Directory -Force $outDir | Out-Null

# A fresh checkout has no .godot/ import cache yet; the export needs it.
& $Godot --headless --path $game --import
if ($LASTEXITCODE -ne 0) { throw "Godot import failed ($LASTEXITCODE)." }
& $Godot --headless --path $game --export-release 'Windows Desktop' $exe
if ($LASTEXITCODE -ne 0 -or -not (Test-Path $exe)) { throw "Godot export failed ($LASTEXITCODE)." }

if (Test-Path $zip) { Remove-Item $zip -Force }
# Compress-Archive of Windows PowerShell takes minutes on a 200 MB exe; ZipFile takes seconds.
Add-Type -AssemblyName System.IO.Compression.FileSystem
[System.IO.Compression.ZipFile]::CreateFromDirectory($outDir, $zip, 'Optimal', $false)
Write-Host "Exported: $zip"
