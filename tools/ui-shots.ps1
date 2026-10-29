<#
.SYNOPSIS
    Takes UI screenshots of set scenarios, each with a layout audit, for tuning the UI.
.DESCRIPTION
    Builds the solution, empties build/shots/, then runs the game once per scenario in UI shot mode
    (game/scripts/UiShot.cs): a throwaway run with a fixed seed (the real save is not touched), the
    window at -Size, the scenario staged, its steps played as real input, and the frame saved as
    build/shots/<NN>_<scenario>.png (NN = the scenario's number in the list) beside .json (layout
    issues: cut-off text, collapsed, offscreen, clipped, overlapping controls) and .tree.txt (every
    visible control with its rectangle); a "shot:<label>" step adds <NN>_<scenario>.<label>.* on the way.
    A borderless window flashes up for a few seconds per scenario; keep the mouse off it, real mouse
    moves reach the game too.
    Scenario: main | battle:<grp.*>[:<hero turns played by the AI>] | event:<evt.*>, then optional
    steps after ";": click:<target>, hover:<target>, key:<Godot Key name>, wait:<seconds>,
    idle (until the weeks or enemy turns being shown are over; input is ignored meanwhile), shot[:<label>].
    A target is a button's text, @x:y in window pixels, or hex:q:r on the battle field.
.PARAMETER Godot
    Path to the Godot .NET editor executable (the _console one prints the summary); defaults to the GODOT environment variable.
.EXAMPLE
    powershell -File tools/ui-shots.ps1 -Godot C:\tools\Godot_v4.7.2-stable_mono_win64\Godot_v4.7.2-stable_mono_win64_console.exe
.EXAMPLE
    powershell -File tools/ui-shots.ps1 -Scenario "main;click:Тестовий бій;click:Почати бій;key:Key1;hover:hex:0:-2" -Hero profile.sandbox.stage_2
#>
param(
    [string]$Godot = $env:GODOT,
    [string[]]$Scenario = @(
        'main',
        'battle:grp.beast.lone_wolf',
        'battle:grp.beast.grey_wolves',
        'battle:grp.beast.grey_wolves:3',
        'event:evt.travel.wounded_disciple'),
    [string]$Size = '1920x1080',
    [string]$Seed = '1',
    [string]$Hero = 'current'
)

$ErrorActionPreference = 'Stop'
if (-not $Godot -or -not (Test-Path $Godot)) {
    throw "Godot editor not found: pass -Godot <path to Godot_v4.7.2-stable_mono_win64_console.exe> or set GODOT."
}

$root = Split-Path $PSScriptRoot -Parent
$out = Join-Path $root 'build\shots'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
New-Item -ItemType Directory -Force $out | Out-Null

# Godot runs the C# assemblies the solution build leaves in game/.godot; it does not build them itself.
dotnet build (Join-Path $root 'IncarnationHero.sln') -v q -nologo
if ($LASTEXITCODE -ne 0) { throw "Build failed ($LASTEXITCODE)." }

# "powershell -File" hands a list over as one string; a comma starts a new scenario only before a scenario kind,
# so button texts in steps may hold commas.
$i = 0
foreach ($s in $Scenario -split ',(?=\s*(?:main|battle|event)\b)') {
    $prefix = '{0:D2}_' -f ++$i
    & $Godot --path (Join-Path $root 'game') -- --shot $s.Trim() --out $out --prefix $prefix --size $Size --seed $Seed --hero $Hero
    if ($LASTEXITCODE -ne 0) { throw "UI shot $s failed ($LASTEXITCODE)." }
}
