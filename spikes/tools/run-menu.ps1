<#
.SYNOPSIS
  Runs the Q3 measurement (menu technique at the non-activating bar, D-15) in one command.

.DESCRIPTION
  Windows PowerShell 5.1, ASCII only. Builds WindowHost and Driver (Release) once and then runs the Driver case
  "menu" for the variants

    M-A  WPF ContextMenu on the bar (simplest)
    M-C  native TrackPopupMenu
    M-B  temporary activation of the bar while the menu is open   (D-15 fallback)
    M-D  helper owner window                                      (D-15 fallback)

  with the check list open, close-desktop, close-window, close-bar, close-escape, keys-enter, mouse-select,
  launch-front, reopen, z-during, focus-after. The bar is the H1 variant (hit technique chosen in Q1); the menu
  case needs no layer guard. The Driver writes <OutDir>/menu.csv (one row per check), <OutDir>/menu-events.csv
  (host events) and the German variant x check table <OutDir>/menu.md. Screenshots go to the ignored folder
  spikes/results/private/ and never enter the repo.

  A real run (no -DryRun) SENDS REAL INPUT: it minimizes windows (Win+M) and restores them at the end, opens menus
  at the top edge, takes the foreground (M-B, M-C, M-D), sends Escape, arrow and Enter keys, clicks, and launches
  test windows (title "Launched") that are closed again. It must only be started after Stephan confirmed the
  hands-off window (plan 01-15, checkpoint). The script asks for "GO" unless -Yes is given. Emergency abort:
  Ctrl+Alt+Q (fallback Ctrl+Alt+Shift+Q). Any real input during the run aborts it (exit code 3 of the step); the
  script then stops. It never repeats the step by itself. Close RocketDock and other tools at the top edge first.

  The script can be started from any directory (paths come from the script location). It never overwrites an
  existing result: if <OutDir>/menu.csv, menu.md or menu-events.csv already exist, a real run stops with exit 2
  unless -Overwrite is given. A run for only some variants (-Variants M-B) writes menu-m-b.csv, menu-m-b.md and
  menu-m-b-events.csv instead, so a follow-up never replaces the full result set.

  -DryRun runs "menu --variant M-A --dry-run": the host opens and closes its menu by itself, no input, no Win+M,
  no foreground change. Output goes to spikes/results/dry/q3 (ignored, never committed). Expected line:
  "MENU DRY OK M-A".

  Exit codes
    0  the run finished (in a real run a failed D-15 point is a result and is only reported)
    1  dry run: the step failed or its output is missing; real run: an output file is missing
    2  bad arguments, build failure, usage error of the step, existing result without -Overwrite, or the
       confirmation was declined
    3  the step was stopped (abort key or real input); the script stops right there

.PARAMETER DryRun
  Pipeline check without touching the desktop.

.PARAMETER OutDir
  Result folder (default spikes/results/q3). Relative paths are taken relative to the current directory.
  Ignored with -DryRun (forced to spikes/results/dry/q3).

.PARAMETER Variants
  "all" (default) or a comma separated list such as M-B or M-A,M-C. Not used by -DryRun (always M-A).

.PARAMETER Overwrite
  Allows a real run to replace existing result files of the same name.

.PARAMETER Yes
  Skips the confirmation prompt of a real run. Only for use after the hands-off checkpoint.
#>
param(
    [switch]$DryRun,
    [string]$OutDir = '',
    [string]$Variants = 'all',
    [switch]$Overwrite,
    [switch]$Yes
)

$ErrorActionPreference = 'Stop'

if ($Variants -notmatch '^(all|M-[A-D](,M-[A-D])*)$') { Write-Host ('Bad -Variants: ' + $Variants); exit 2 }

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$startedAt = Get-Date

if ($DryRun) {
    $outDirFull = Join-Path $repoRoot 'spikes/results/dry/q3'
} elseif ($OutDir.Length -gt 0) {
    $outDirFull = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutDir)
} else {
    $outDirFull = Join-Path $repoRoot 'spikes/results/q3'
}

# File names: the full set is menu.*, a partial set gets the variants as suffix (never replaces the full set).
$tag = ''
if ((-not $DryRun) -and ($Variants -ne 'all')) { $tag = '-' + $Variants.ToLowerInvariant().Replace(',', '-') }
$csv = Join-Path $outDirFull ('menu' + $tag + '.csv')
$md = Join-Path $outDirFull ('menu' + $tag + '.md')
$evt = Join-Path $outDirFull ('menu' + $tag + '-events.csv')

if (-not $DryRun) {
    $existing = @(@($csv, $md, $evt) | Where-Object { Test-Path -LiteralPath $_ })
    if (($existing.Count -gt 0) -and (-not $Overwrite)) {
        Write-Host 'Result files already exist and would be replaced:'
        foreach ($f in $existing) { Write-Host ('  ' + $f) }
        Write-Host 'Nothing was run. Use -Overwrite to replace them, or -OutDir for another folder.'
        exit 2
    }
}
New-Item -ItemType Directory -Path $outDirFull -Force | Out-Null

if ($DryRun) {
    Write-Host 'Q3 run: DRY RUN (no input, no Win+M, no foreground change)'
} else {
    Write-Host 'Q3 run: REAL RUN. It SENDS REAL INPUT (menus, Escape/arrow/Enter keys, clicks), minimizes windows (Win+M)'
    Write-Host '  and opens test windows titled "Launched". Variants M-B, M-C, M-D take the foreground.'
    Write-Host 'Hands off mouse and keyboard until it ends. Emergency abort: Ctrl+Alt+Q (fallback Ctrl+Alt+Shift+Q).'
    Write-Host 'Close RocketDock and other tools at the top edge first (they tint the pixel probes).'
    if (-not $Yes) {
        $answer = Read-Host 'Type GO to continue'
        if ($answer -ne 'GO') { Write-Host 'Cancelled.'; exit 2 }
    }
}

# 1. Build once.
Push-Location $repoRoot
try {
    & dotnet build 'spikes/WindowHost' -c Release -warnaserror --nologo -v q
    if ($LASTEXITCODE -ne 0) { Write-Host 'WindowHost build failed.'; exit 2 }
    & dotnet build 'spikes/Driver' -c Release -warnaserror --nologo -v q
    if ($LASTEXITCODE -ne 0) { Write-Host 'Driver build failed.'; exit 2 }
} finally {
    Pop-Location
}
$driver = Join-Path $repoRoot 'spikes/Driver/bin/Release/net10.0-windows/Driver.exe'
if (-not (Test-Path -LiteralPath $driver)) { Write-Host ('Driver.exe not found: ' + $driver); exit 2 }

# 2. The step.
$variantArg = 'all'
if ($DryRun) { $variantArg = 'M-A' } elseif ($Variants -ne 'all') { $variantArg = $Variants }
$driverArgs = @('menu', '--variant', $variantArg, '--out', $csv, '--summary-md', $md)
if ($DryRun) { $driverArgs += '--dry-run' }

Write-Host ''
Write-Host ('--- step menu: Driver ' + ($driverArgs -join ' '))

# stdout only (stderr is left alone: with ErrorActionPreference=Stop a redirect would turn it into an error)
$lines = @(& $driver @driverArgs | ForEach-Object { Write-Host $_; [string]$_ })
$code = $LASTEXITCODE

if ($code -eq 3) {
    Write-Host ''
    Write-Host 'STOPPED in step menu (exit code 3: abort key or real input). Not repeating; ask for a new confirmation first.'
    exit 3
}
if ($code -eq 2) {
    Write-Host 'Step menu reported a usage error (exit code 2).'
    exit 2
}

# 3. Outputs and overview.
$problems = 0
foreach ($f in @($csv, $md)) {
    if (-not (Test-Path -LiteralPath $f)) {
        Write-Host ('MISSING output: ' + (Split-Path -Leaf $f))
        $problems++
    }
}
if ($DryRun) {
    if ($code -ne 0) { $problems++ }
    $ok = @($lines | Where-Object { $_ -match '^MENU DRY OK M-A' })
    if ($ok.Count -eq 0) { Write-Host 'MISSING line "MENU DRY OK M-A".'; $problems++ }
}

Write-Host ''
Write-Host '=== Q3 overview ==='
$key = @($lines | Where-Object { $_ -match '^(MENU RESULT |MENU DONE|MENU DRY )' })
if ($key.Count -eq 0) { Write-Host 'no result line' }
foreach ($l in $key) { Write-Host $l }
Write-Host ('Driver exit code: ' + $code + ' (real run: 1 means no variant passes every mandatory point, a result)')

if (-not $DryRun) {
    $left = @(Get-Process -Name 'WindowHost' -ErrorAction SilentlyContinue)
    if ($left.Count -gt 0) {
        Write-Host ('WARNING: ' + $left.Count + ' WindowHost process(es) still running (test windows not closed). Check: tasklist /v /fi "imagename eq WindowHost.exe"')
    } else {
        Write-Host 'No WindowHost process left running.'
    }
}

$elapsed = [int]((Get-Date) - $startedAt).TotalSeconds
Write-Host ('Duration: ' + $elapsed + ' s')
Write-Host ('Table:   ' + $md)
Write-Host ('Rows:    ' + $csv)
Write-Host ('Results: ' + $outDirFull)

if ($problems -gt 0) {
    Write-Host ($problems.ToString() + ' problem(s): the step failed or an output is missing.')
    exit 1
}
exit 0
