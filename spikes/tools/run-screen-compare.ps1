<#
.SYNOPSIS
  Plan 01-19: measures on screen (Desktop Duplication) how the bar moves while the Driver sweeps the mouse over it:
  the spike host with the sampled and the smooth zoom layout (each also snapped to whole pixels), and optionally
  RocketDock as the reference.
.DESCRIPTION
  Windows PowerShell 5.1, ASCII only. Every cell moves the mouse (SendInput, 125 Hz sine, 2 s period) inside a
  SafetySession: hands off; Ctrl+Alt+Q or any real input aborts at once (exit 3, the remaining cells are skipped).
  The host cells minimize all windows (Win+M) and restore them at the end.
  Raw captures: spikes/results/private/q4b-raw/ (ignored). Summary: spikes/results/q4b-screen-summary.csv.
  -RocketDockRect x,y,w,h and -RocketDockRow y: the RocketDock window and the screen row through the centres of its
  resting icons (read them from the window first). Without -RocketDockRect the RocketDock cell is skipped.
#>
param(
    [int]$Seconds = 20,
    [string]$RocketDockRect,
    [int]$RocketDockRow = -1,
    [int]$RocketDockAmplitude = 150,
    [int]$RocketDockFar = 250,
    [switch]$Yes
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$driver = Join-Path $repoRoot 'spikes/Driver/bin/Release/net10.0-windows/Driver.exe'
$raw = Join-Path $repoRoot 'spikes/results/private/q4b-raw'
$summary = Join-Path $repoRoot 'spikes/results/q4b-screen-summary.csv'
New-Item -ItemType Directory -Force -Path $raw | Out-Null

Push-Location $repoRoot
try {
    & dotnet build 'spikes/WindowHost' -c Release -warnaserror --nologo -v q
    if ($LASTEXITCODE -ne 0) { Write-Host 'WindowHost build failed.'; exit 1 }
    & dotnet build 'spikes/Driver' -c Release -warnaserror --nologo -v q
    if ($LASTEXITCODE -ne 0) { Write-Host 'Driver build failed.'; exit 1 }
} finally {
    Pop-Location
}

if (-not $Yes) {
    Write-Host 'This run MOVES THE MOUSE and minimizes windows. Hands off until it ends. Abort: Ctrl+Alt+Q.'
    if ((Read-Host 'Type GO to continue') -ne 'GO') { exit 2 }
}

$cells = @(
    @{ Label = 'host-sampled'; Layout = 'sampled'; Snap = $false },
    @{ Label = 'host-sampled-snap'; Layout = 'sampled'; Snap = $true },
    @{ Label = 'host-smooth'; Layout = 'smooth'; Snap = $false },
    @{ Label = 'host-smooth-snap'; Layout = 'smooth'; Snap = $true }
)
foreach ($cell in $cells) {
    Write-Host ('--- ' + $cell.Label)
    $cellArgs = @('render', '--variant', 'R1', '--count', '20', '--render', 'hw', '--seconds', [string]$Seconds,
        '--warmup', '3', '--idle', '0', '--capture', '--layout', $cell.Layout, '--label', $cell.Label,
        '--out', (Join-Path $raw 'render-summary.csv'), '--frames-dir', $raw)
    if ($cell.Snap) { $cellArgs += '--snap' }
    & $driver @cellArgs
    if ($LASTEXITCODE -eq 3) { Write-Host 'STOPPED: abort key or real input.'; exit 3 }
    $dir = Join-Path $raw ($cell.Label + '-R1-20-hw-1900x250-screen')
    & $driver screen-analyze --dir $dir --far 345 --label $cell.Label --out $summary
}

if ($RocketDockRect) {
    Write-Host '--- rocketdock'
    $sweepArgs = @('screen-sweep', '--rect', $RocketDockRect, '--seconds', [string]$Seconds, '--warmup', '3',
        '--amplitude', [string]$RocketDockAmplitude, '--label', 'rocketdock', '--out-dir', (Join-Path $raw 'rocketdock'))
    if ($RocketDockRow -ge 0) {
        $sweepArgs += @('--sweep-y', [string]$RocketDockRow, '--rows', (($RocketDockRow - 4), $RocketDockRow, ($RocketDockRow + 4) -join ','))
    }
    & $driver @sweepArgs
    if ($LASTEXITCODE -eq 3) { Write-Host 'STOPPED: abort key or real input.'; exit 3 }
    & $driver screen-analyze --dir (Join-Path $raw 'rocketdock') --far $RocketDockFar --label rocketdock --out $summary
}

Write-Host ''
Write-Host ('Summary: ' + $summary)
Import-Csv $summary | Format-Table -AutoSize | Out-String -Width 250 | Write-Host
exit 0
