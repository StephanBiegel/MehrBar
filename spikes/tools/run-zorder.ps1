<#
.SYNOPSIS
  Runs the whole Q2 matrix (layer bottom vs. Win+D and relatives, D-10/D-11) in one command.

.DESCRIPTION
  Windows PowerShell 5.1, ASCII only. Builds WindowHost and Driver (Release) once and then runs, per guard
  (S0 = no watcher, S1 = event-driven watcher, S2 = watcher plus poll), the Driver case "zorder":

    base matrix  win-d, corner, win-m, peek, peek-key, alt-tab      (-Repeat repetitions, default 5)
    explorer     Explorer restart                                    (3 repetitions, only with -AllowExplorer)
    display      resolution change and back                          (3 repetitions, only with -AllowDisplay)

  Guards come from -Guards (default S0,S1); -IncludeS2 adds S2. S2 runs the triggers listed in -S2Triggers
  (default: the same set as the other guards), so that it can be restricted to the triggers S1 failed.

  The Driver writes one CSV per call into the ignored scratch folder spikes/results/dry/q2-parts/. The script merges
  them into <OutDir>/runs.csv and <OutDir>/events.csv, writes the per-guard tables <OutDir>/<guard>.md and the overall
  table <OutDir>/summary.md. Per run: gap in ms, returned by itself, samples in front of the covering witness (D-11).

  A follow-up run that does not contain both S0 and S1 (for example -Guards S2 -IncludeS2 -S2Triggers win-d) never
  replaces an existing S0/S1 result set: its merged files get the guard suffix (runs-s2.csv, events-s2.csv,
  summary-s2.md), and only the per-guard tables of the guards it actually ran are rewritten or removed.

  A real run (no -DryRun) SENDS REAL INPUT: Win+D, Win+M, Alt+Tab, clicks on the taskbar corner, mouse hovers. With
  -AllowExplorer it kills and restarts explorer.exe, with -AllowDisplay it switches the resolution and back. It must
  only be started after Stephan confirmed the hands-off window (plan 01-14, checkpoint). The script asks for "GO"
  unless -Yes is given. Emergency abort: Ctrl+Alt+Q (fallback Ctrl+Alt+Shift+Q). Any real input during the run aborts
  it (exit code 3 of the step); the script then stops and names the step. It never repeats a step by itself.

  The base steps never pass --allow-explorer: if the corner click does not show the desktop, the Driver notes
  "corner-inactive" instead of restarting Explorer on its own (the explicit explorer step is the consented restart).

  -DryRun uses guards S0,S1 with --trigger none --repeat 1 --dry-run: no input, no Win+M, no foreground change; the
  output goes to spikes/results/dry/q2 (ignored, never committed).

  Before and after a real run the script prints TaskbarSd, whether explorer.exe runs and the display mode, so the
  restoration can be checked (RESEARCH Pitfall 3).

  Exit codes
    0  all steps ran (in a real run a failed D-11 criterion is a result; in a dry run a failing step gives 1)
    1  dry run: a step failed or its output is missing; real run: an output file is missing
    2  bad arguments, build failure, a usage error of a step, or the confirmation was declined
    3  a step was stopped (abort key or real input); the script stops right there

.PARAMETER DryRun
  Pipeline check without touching the desktop.

.PARAMETER Guards
  Guards to measure (default S0,S1).

.PARAMETER Repeat
  Repetitions of the base matrix (default 5).

.PARAMETER AllowExplorer
  Also run the Explorer restart trigger (3 repetitions per guard).

.PARAMETER AllowDisplay
  Also run the resolution change trigger (3 repetitions per guard).

.PARAMETER IncludeS2
  Also measure guard S2 (only where S1 fails: restrict with -S2Triggers).

.PARAMETER S2Triggers
  Triggers for guard S2, comma separated (default: the base matrix plus explorer/display where allowed).

.PARAMETER OutDir
  Result folder (default spikes/results/q2). Forced to spikes/results/dry/q2 with -DryRun.

.PARAMETER Yes
  Skips the confirmation prompt of a real run. Only for use after the hands-off checkpoint.
#>
param(
    [switch]$DryRun,
    [string]$Guards = 'S0,S1',
    [int]$Repeat = 5,
    [switch]$AllowExplorer,
    [switch]$AllowDisplay,
    [switch]$IncludeS2,
    [string]$S2Triggers = '',
    [string]$OutDir = '',
    [switch]$Yes
)

$ErrorActionPreference = 'Stop'

$known = @('win-d', 'corner', 'win-m', 'peek', 'peek-key', 'alt-tab', 'explorer', 'display')
$baseSet = @('win-d', 'corner', 'win-m', 'peek', 'peek-key', 'alt-tab')

if ($Guards -notmatch '^S[0-2](,S[0-2])*$') { Write-Host ('Bad -Guards: ' + $Guards); exit 2 }
if ($Repeat -lt 1) { Write-Host 'Bad -Repeat'; exit 2 }
$s2List = @()
if ($S2Triggers.Length -gt 0) {
    $s2List = @($S2Triggers.Split(',') | ForEach-Object { $_.Trim().ToLowerInvariant() } | Where-Object { $_.Length -gt 0 })
    foreach ($t in $s2List) {
        if ($known -notcontains $t) { Write-Host ('Bad -S2Triggers entry: ' + $t); exit 2 }
    }
}

$guardList = @($Guards.Split(',') | ForEach-Object { $_.Trim() })
if ($IncludeS2 -and ($guardList -notcontains 'S2')) { $guardList += 'S2' }
if ($DryRun) { $guardList = @('S0', 'S1') }

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$partsDir = Join-Path $repoRoot 'spikes/results/dry/q2-parts'
if ($DryRun) {
    $outDirFull = Join-Path $repoRoot 'spikes/results/dry/q2'
} elseif ($OutDir.Length -gt 0) {
    $outDirFull = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutDir)
} else {
    $outDirFull = Join-Path $repoRoot 'spikes/results/q2'
}
New-Item -ItemType Directory -Path $outDirFull -Force | Out-Null
if (Test-Path -LiteralPath $partsDir) { Remove-Item -LiteralPath $partsDir -Recurse -Force }
New-Item -ItemType Directory -Path $partsDir -Force | Out-Null

# --- plan of steps -----------------------------------------------------------------------------------------
$steps = @()
if ($DryRun) {
    foreach ($g in $guardList) {
        $steps += @{ Guard = $g; Label = 'none'; Args = @('--trigger', 'none', '--repeat', '1') }
    }
} else {
    foreach ($g in $guardList) {
        $wanted = $null
        if ($g -eq 'S2' -and $s2List.Count -gt 0) { $wanted = $s2List }

        $base = @($baseSet | Where-Object { ($null -eq $wanted) -or ($wanted -contains $_) })
        if ($base.Count -gt 0) {
            $steps += @{ Guard = $g; Label = 'base'; Args = @('--trigger', ($base -join ','), '--repeat', [string]$Repeat) }
        }

        $wantExplorer = ($null -eq $wanted) -or ($wanted -contains 'explorer')
        if ($wantExplorer -and $AllowExplorer) {
            $steps += @{ Guard = $g; Label = 'explorer'; Args = @('--trigger', 'explorer', '--allow-explorer', '--repeat', '3') }
        } elseif (($wanted -contains 'explorer') -and -not $AllowExplorer) {
            Write-Host ('Guard ' + $g + ': trigger explorer skipped (needs -AllowExplorer).')
        }

        $wantDisplay = ($null -eq $wanted) -or ($wanted -contains 'display')
        if ($wantDisplay -and $AllowDisplay) {
            $steps += @{ Guard = $g; Label = 'display'; Args = @('--trigger', 'display', '--allow-display', '--repeat', '3') }
        } elseif (($wanted -contains 'display') -and -not $AllowDisplay) {
            Write-Host ('Guard ' + $g + ': trigger display skipped (needs -AllowDisplay).')
        }
    }
}
if ($steps.Count -eq 0) { Write-Host 'Nothing to run.'; exit 2 }

# --- restoration probe -------------------------------------------------------------------------------------
function Get-SessionState {
    $sd = 'absent'
    try {
        $p = Get-ItemProperty -Path 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced' -Name 'TaskbarSd' -ErrorAction Stop
        $sd = [string]$p.TaskbarSd
    } catch { $sd = 'absent' }
    $ex = @(Get-Process -Name explorer -ErrorAction SilentlyContinue).Count
    $mode = 'unknown'
    try {
        Add-Type -AssemblyName System.Windows.Forms -ErrorAction Stop
        $b = [System.Windows.Forms.Screen]::PrimaryScreen.Bounds
        $mode = ([string]$b.Width + 'x' + [string]$b.Height)
    } catch { $mode = 'unknown' }
    return ('TaskbarSd=' + $sd + ' explorer.exe=' + $ex + ' primary=' + $mode)
}

if ($DryRun) {
    Write-Host 'Q2 run: DRY RUN (no input, no Win+M, no Explorer or display change, no foreground change)'
} else {
    Write-Host 'Q2 run: REAL RUN. It SENDS REAL INPUT (Win+D, Win+M, Alt+Tab, taskbar corner, hover).'
    if ($AllowExplorer) { Write-Host '  -AllowExplorer: explorer.exe is killed and restarted.' }
    if ($AllowDisplay) { Write-Host '  -AllowDisplay: the resolution is switched and back.' }
    Write-Host 'Hands off mouse and keyboard until it ends. Emergency abort: Ctrl+Alt+Q (fallback Ctrl+Alt+Shift+Q).'
    Write-Host 'Close RocketDock and other tools at the top edge first (they tint the pixel probes).'
    if (-not $Yes) {
        $answer = Read-Host 'Type GO to continue'
        if ($answer -ne 'GO') { Write-Host 'Cancelled.'; exit 2 }
    }
}
$stateBefore = Get-SessionState
Write-Host ('Session before: ' + $stateBefore)

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

$utf8 = New-Object System.Text.UTF8Encoding($false)
$overview = @()
$problems = 0
$index = 0
$csvParts = @()
$eventParts = @()
$tableRows = @{}   # guard -> list of table rows
$tableHeader = @()

# 2. Steps, in order.
foreach ($step in $steps) {
    $index++
    $guard = $step.Guard
    $tag = ('{0:D2}-{1}-{2}' -f $index, $guard, $step.Label)
    $csv = Join-Path $partsDir ($tag + '.csv')
    $evt = Join-Path $partsDir ($tag + '-events.csv')
    $part = Join-Path $partsDir ($tag + '.md')

    $driverArgs = @('zorder', '--guard', $guard) + @($step.Args) + @('--out', $csv, '--summary-md', $part)
    if ($DryRun) { $driverArgs += '--dry-run' }

    Write-Host ''
    Write-Host ('--- step ' + $tag + ': Driver ' + ($driverArgs -join ' '))

    # stdout only (stderr is left alone: with ErrorActionPreference=Stop a redirect would turn it into an error)
    $lines = @(& $driver @driverArgs | ForEach-Object { Write-Host $_; [string]$_ })
    $code = $LASTEXITCODE

    if ($code -eq 3) {
        Write-Host ''
        Write-Host ('STOPPED in step ' + $tag + ' (exit code 3: abort key or real input). Not repeating; ask for a new confirmation first.')
        Write-Host ('Session now: ' + (Get-SessionState))
        exit 3
    }
    if ($code -eq 2) {
        Write-Host ('Step ' + $tag + ' reported a usage error (exit code 2).')
        exit 2
    }

    $missing = @()
    foreach ($f in @($csv, $part)) {
        if (-not (Test-Path -LiteralPath $f)) { $missing += (Split-Path -Leaf $f) }
    }
    if ($missing.Count -gt 0) {
        Write-Host ('MISSING output of step ' + $tag + ': ' + ($missing -join ', '))
        $problems++
    } else {
        $csvParts += $csv
        if (Test-Path -LiteralPath $evt) { $eventParts += $evt }
        if (-not $tableRows.ContainsKey($guard)) { $tableRows[$guard] = @() }
        foreach ($l in ([System.IO.File]::ReadAllText($part, [System.Text.Encoding]::UTF8) -split "`r?`n")) {
            if ($l -match '^(\| W|\|---)') { if ($tableHeader.Count -lt 2) { $tableHeader += $l } }
            elseif ($l -match '^\| S[0-2] \|') { $tableRows[$guard] += $l }
        }
    }
    if ($DryRun -and $code -ne 0) { $problems++ }

    $key = @($lines | Where-Object { $_ -match '^(ZORDER |S[0-2]: )' })
    $keyText = 'no result line'
    if ($key.Count -gt 0) { $keyText = ($key -join ' || ') }
    $overview += ('{0,-22} exit {1}  {2}' -f $tag, $code, $keyText)
}

# 3. Merge the per-call files.
$suffix = ''
if ((-not $DryRun) -and (($guardList -notcontains 'S0') -or ($guardList -notcontains 'S1'))) {
    $suffix = '-' + (($guardList -join '-').ToLowerInvariant())
}
$runsPath = Join-Path $outDirFull ('runs' + $suffix + '.csv')
$eventsPath = Join-Path $outDirFull ('events' + $suffix + '.csv')
$runs = New-Object System.Text.StringBuilder
$first = $true
foreach ($f in $csvParts) {
    $rows = @([System.IO.File]::ReadAllText($f, [System.Text.Encoding]::UTF8) -split "`r?`n" | Where-Object { $_.Length -gt 0 })
    if ($first) { [void]$runs.Append($rows[0] + "`n"); $first = $false }
    for ($i = 1; $i -lt $rows.Count; $i++) { [void]$runs.Append($rows[$i] + "`n") }
}
if (-not $first) { [System.IO.File]::WriteAllText($runsPath, $runs.ToString(), $utf8) }

$events = New-Object System.Text.StringBuilder
$first = $true
foreach ($f in $eventParts) {
    $rows = @([System.IO.File]::ReadAllText($f, [System.Text.Encoding]::UTF8) -split "`r?`n" | Where-Object { $_.Length -gt 0 })
    if ($rows.Count -eq 0) { continue }
    if ($first) { [void]$events.Append($rows[0] + "`n"); $first = $false }
    for ($i = 1; $i -lt $rows.Count; $i++) { [void]$events.Append($rows[$i] + "`n") }
}
if (-not $first) { [System.IO.File]::WriteAllText($eventsPath, $events.ToString(), $utf8) }

# 4. Per-guard tables and the overall table.
$kind = 'Messung'
if ($DryRun) { $kind = 'Trockenlauf (nichts gemessen)' }
$dash = [string][char]0x2013
$ue = [string][char]0xFC
$ae = [string][char]0xE4
if ($tableHeader.Count -lt 2) {
    $tableHeader = @(
        ('| W' + $ae + 'chter | Ausl' + [char]0xF6 + 'ser | L' + $ae + 'ufe | max. L' + [char]0xFC + 'cke ms | von selbst zur' + $ue + 'ck | max. vor Zeuge ms | Bemerkung |'),
        '|---|---|---|---|---|---|---|')
}

foreach ($g in @('S0', 'S1', 'S2')) {
    $path = Join-Path $outDirFull ($g + '.md')
    if ($tableRows.ContainsKey($g) -and $tableRows[$g].Count -gt 0) {
        $t = New-Object System.Text.StringBuilder
        [void]$t.Append('# Q2 Ebene ' + $dash + ' W' + $ae + 'chter ' + $g + ' ' + $dash + ' ' + $kind + "`n`n")
        [void]$t.Append($tableHeader[0] + "`n" + $tableHeader[1] + "`n")
        foreach ($r in $tableRows[$g]) { [void]$t.Append($r + "`n") }
        [System.IO.File]::WriteAllText($path, $t.ToString(), $utf8)
    } elseif ((Test-Path -LiteralPath $path) -and ($guardList -contains $g)) {
        Remove-Item -LiteralPath $path -Force
    }
}

$summary = New-Object System.Text.StringBuilder
[void]$summary.Append('# Q2 Gesamtauswertung ' + $dash + ' ' + $kind + "`n`n")
[void]$summary.Append('W' + $ae + 'chter ' + ($guardList -join ', ') + ', erzeugt von spikes/tools/run-zorder.ps1 am ' + (Get-Date -Format 'yyyy-MM-dd HH:mm') + ". Einzell" + $ae + "ufe: runs" + $suffix + ".csv, Ereignisse: events" + $suffix + ".csv.`n`n")
[void]$summary.Append("Sitzung vorher: $stateBefore`n`n")
[void]$summary.Append($tableHeader[0] + "`n" + $tableHeader[1] + "`n")
foreach ($g in @('S0', 'S1', 'S2')) {
    if ($tableRows.ContainsKey($g)) { foreach ($r in $tableRows[$g]) { [void]$summary.Append($r + "`n") } }
}
$summaryPath = Join-Path $outDirFull ('summary' + $suffix + '.md')
[System.IO.File]::WriteAllText($summaryPath, $summary.ToString(), $utf8)

if (-not (Test-Path -LiteralPath $runsPath)) { Write-Host ('MISSING output: ' + (Split-Path -Leaf $runsPath)); $problems++ }

# 5. Overview and restoration check.
Write-Host ''
Write-Host '=== Q2 overview ==='
foreach ($line in $overview) { Write-Host $line }
$stateAfter = Get-SessionState
Write-Host ('Session before: ' + $stateBefore)
Write-Host ('Session after:  ' + $stateAfter)
if ($stateBefore -ne $stateAfter) { Write-Host 'WARNING: session state differs from the state before the run (check by hand).' }
Write-Host ('Summary: ' + $summaryPath)
Write-Host ('Results: ' + $outDirFull)

if ($problems -gt 0) {
    Write-Host ($problems.ToString() + ' problem(s): a step failed or an output file is missing.')
    exit 1
}
exit 0
