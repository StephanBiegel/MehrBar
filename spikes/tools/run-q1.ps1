<#
.SYNOPSIS
  Runs the whole Q1 measurement (hit area, click pass-through, first frame, focus) in one command.

.DESCRIPTION
  Windows PowerShell 5.1, ASCII only. Builds WindowHost and Driver (Release) once and then runs, in this order:

    selftest     oracle self-test and D-12 baseline (bar visible at HWND_BOTTOM above the desktop)
    firstframe   first frame already at the bottom of the z-order (launches per variant)
    hit          capture rate inside the plate, pass-through outside (4 sides + zoom-overhang gap), mouse
                 moves, focus kept at the click, H2 region churn, H3 entry latency
    focus-witness  focus kept on an active witness window (left click, right click, move)
    focus-notepad  focus kept on a Notepad window with an empty file

  Per step the Driver writes <OutDir>/<step>.csv; the per-step summary parts go to the ignored scratch folder
  spikes/results/dry/q1-parts/ and are concatenated into <OutDir>/summary.md under per-step headings.

  A real run (no -DryRun) SENDS REAL INPUT: it minimizes all windows (Win+M), clicks on the bar and opens a Notepad
  window. It must only be started after Stephan confirmed the hands-off window (plan 01-13, checkpoint). The script
  asks for confirmation ("GO") unless -Yes is given. Emergency abort: Ctrl+Alt+Q (fallback Ctrl+Alt+Shift+Q).
  Any real input during the run aborts it (exit code 3 of the step); the script then stops and names the step.
  It never repeats an input-sending step by itself.

  -DryRun runs the same steps in the same order with --dry-run (firstframe with 2 launches): no clicks, no Win+M,
  no Notepad window, no foreground change; the output goes to spikes/results/dry/q1 (ignored, never committed).

  Exit codes
    0  all steps ran (in a real run a failed criterion is a result and is only reported in the overview;
       in a dry run any failing step is a harness error and gives 1)
    1  dry run: a step failed or its output is missing; real run: an output file is missing
    2  bad arguments, build failure or a step with a usage error, or the confirmation was declined
    3  a step was stopped (abort key or real input); the script stops right there

.PARAMETER DryRun
  Pipeline check without touching the desktop.

.PARAMETER Seed
  Seed of the click plan (default 4711), passed to "Driver hit" and "Driver focus".

.PARAMETER OutDir
  Result folder (default spikes/results/q1). Forced to spikes/results/dry/q1 with -DryRun.

.PARAMETER Variants
  Hit-area variants to measure in every step (default H1,H2,H3, D-14).

.PARAMETER Yes
  Skips the confirmation prompt of a real run. Only for use after the hands-off checkpoint.
#>
param(
    [switch]$DryRun,
    [int]$Seed = 4711,
    [string]$OutDir = '',
    [string]$Variants = 'H1,H2,H3',
    [switch]$Yes
)

$ErrorActionPreference = 'Stop'

if ($Variants -notmatch '^H[1-3](,H[1-3])*$') { Write-Host ('Bad -Variants: ' + $Variants); exit 2 }

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$partsDir = Join-Path $repoRoot 'spikes/results/dry/q1-parts'
if ($DryRun) {
    $outDirFull = Join-Path $repoRoot 'spikes/results/dry/q1'
} elseif ($OutDir.Length -gt 0) {
    $outDirFull = $ExecutionContext.SessionState.Path.GetUnresolvedProviderPathFromPSPath($OutDir)
} else {
    $outDirFull = Join-Path $repoRoot 'spikes/results/q1'
}
New-Item -ItemType Directory -Path $outDirFull -Force | Out-Null
New-Item -ItemType Directory -Path $partsDir -Force | Out-Null

$ae = [string][char]0xE4
$titles = @{
    'selftest'      = 'Orakel-Selbsttest und Basislinie (selftest)'
    'firstframe'    = 'Erstes Bild ganz unten (firstframe)'
    'hit'           = ('Trefferfl' + $ae + 'che und Durchlass (hit)')
    'focus-witness' = 'Fokus, aktives Pr' + [char]0xFC + 'ffenster (focus-witness)'
    'focus-notepad' = 'Fokus, Notepad (focus-notepad)'
}

$launches = '10'
if ($DryRun) { $launches = '2' }

$steps = @(
    @{ Name = 'selftest';      Args = @('selftest') },
    @{ Name = 'firstframe';    Args = @('firstframe', '--launches', $launches, '--variants', $Variants) },
    @{ Name = 'hit';           Args = @('hit', '--variants', $Variants, '--clicks', '100', '--seed', [string]$Seed) },
    @{ Name = 'focus-witness'; Args = @('focus', '--target', 'witness', '--cycles', '100', '--variants', $Variants, '--seed', [string]$Seed) },
    @{ Name = 'focus-notepad'; Args = @('focus', '--target', 'notepad', '--cycles', '20', '--variants', $Variants, '--seed', [string]$Seed) }
)

if ($DryRun) {
    Write-Host 'Q1 run: DRY RUN (no input, no Win+M, no Notepad, no foreground change)'
} else {
    Write-Host 'Q1 run: REAL RUN. It SENDS REAL INPUT (clicks), minimizes all windows (Win+M) and opens a Notepad window.'
    Write-Host 'Hands off mouse and keyboard until it ends. Emergency abort: Ctrl+Alt+Q (fallback Ctrl+Alt+Shift+Q).'
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

$utf8 = New-Object System.Text.UTF8Encoding($false)
$overview = @()
$problems = 0

# 2. Steps, in order.
foreach ($step in $steps) {
    $name = $step.Name
    $csv = Join-Path $outDirFull ($name + '.csv')
    $part = Join-Path $partsDir ($name + '.md')
    foreach ($old in @($csv, $part)) {
        if (Test-Path -LiteralPath $old) { Remove-Item -LiteralPath $old -Force }
    }

    $driverArgs = @($step.Args) + @('--out', $csv, '--summary-md', $part)
    if ($DryRun) { $driverArgs += '--dry-run' }

    Write-Host ''
    Write-Host ('--- step ' + $name + ': Driver ' + ($driverArgs -join ' '))

    # stdout only (stderr is left alone: with ErrorActionPreference=Stop a redirect would turn it into an error)
    $lines = @(& $driver @driverArgs | ForEach-Object { Write-Host $_; [string]$_ })
    $code = $LASTEXITCODE

    if ($code -eq 3) {
        Write-Host ''
        Write-Host ('STOPPED in step ' + $name + ' (exit code 3: abort key or real input). Not repeating; ask for a new confirmation first.')
        exit 3
    }
    if ($code -eq 2) {
        Write-Host ('Step ' + $name + ' reported a usage error (exit code 2).')
        exit 2
    }

    # selftest and firstframe have no --summary-md of their own: build the part from the console lines.
    if ($name -eq 'selftest') {
        $text = New-Object System.Text.StringBuilder
        [void]$text.Append("| Messung | Wert |`n|---|---|`n")
        $real = $lines | Where-Object { $_ -match '^SELFTEST visible=(\d+)/5 covered=(\d+)/5' } | Select-Object -Last 1
        if ($real) {
            $m = [regex]::Match($real, '^SELFTEST visible=(\d+)/5 covered=(\d+)/5')
            [void]$text.Append('| Leiste unverdeckt, Sentinels sichtbar (D-12-Basislinie, a) | ' + $m.Groups[1].Value + "/5 |`n")
            [void]$text.Append('| Pr' + [char]0xFC + 'ffenster dar' + [char]0xFC + 'ber, Deckfarbe gelesen (Orakel A1, b) | ' + $m.Groups[2].Value + "/5 |`n")
        } else {
            $dryLine = $lines | Where-Object { $_ -match '^SELFTEST DRY' } | Select-Object -Last 1
            [void]$text.Append('| Trockenlauf (nichts gemessen) | ' + $(if ($dryLine) { $dryLine } else { 'kein Ergebnis' }) + " |`n")
        }
        [System.IO.File]::WriteAllText($part, $text.ToString(), $utf8)
    }
    if ($name -eq 'firstframe') {
        $text = New-Object System.Text.StringBuilder
        [void]$text.Append("| Variante | Start unten (Host) | gemessen | von au" + [char]0xDF + "en unten bei READY |`n|---|---|---|---|`n")
        $any = $false
        foreach ($l in $lines) {
            $m = [regex]::Match($l, '^FIRSTFRAME bottom=(\d+)/(\d+) variant=(H\d) measured=(\d+) driver-bottom-at-ready=(\d+)')
            if ($m.Success) {
                $any = $true
                [void]$text.Append('| ' + $m.Groups[3].Value + ' | ' + $m.Groups[1].Value + '/' + $m.Groups[2].Value + ' | ' + $m.Groups[4].Value + ' | ' + $m.Groups[5].Value + "/" + $m.Groups[4].Value + " |`n")
            }
        }
        if (-not $any) { [void]$text.Append("| - | kein Ergebnis | - | - |`n") }
        if ($DryRun) { [void]$text.Append("`nTrockenlauf mit 2 Starts je Variante (nur Pipeline-Pr" + [char]0xFC + "fung).`n") }
        [System.IO.File]::WriteAllText($part, $text.ToString(), $utf8)
    }

    $keyPattern = switch ($name) {
        'selftest'   { '^SELFTEST' }
        'firstframe' { '^FIRSTFRAME' }
        'hit'        { '^(H\d: captured|HIT DRY OK|HIT FAILED)' }
        default      { '^(FOCUS )' }
    }
    $key = @($lines | Where-Object { $_ -match $keyPattern })
    $keyText = 'no result line'
    if ($key.Count -gt 0) { $keyText = ($key -join ' || ') }

    $missing = @()
    foreach ($f in @($csv, $part)) {
        if (-not (Test-Path -LiteralPath $f)) { $missing += (Split-Path -Leaf $f) }
    }
    if ($missing.Count -gt 0) {
        Write-Host ('MISSING output of step ' + $name + ': ' + ($missing -join ', '))
        $problems++
    }
    if ($DryRun -and $code -ne 0) { $problems++ }

    $overview += ('{0,-14} exit {1}  {2}' -f $name, $code, $keyText)
}

# 3. Combined summary.
$summary = New-Object System.Text.StringBuilder
$kind = 'Messung'
if ($DryRun) { $kind = 'Trockenlauf (nichts gemessen)' }
[void]$summary.Append('# Q1 Gesamtauswertung ' + [char]0x2013 + ' ' + $kind + "`n`n")
[void]$summary.Append('Seed ' + $Seed + ', Varianten ' + $Variants + ', erzeugt von spikes/tools/run-q1.ps1 am ' + (Get-Date -Format 'yyyy-MM-dd HH:mm') + ". Einzelwerte je Schritt: die CSV-Dateien daneben.`n`n")
$index = 0
foreach ($step in $steps) {
    $index++
    $part = Join-Path $partsDir ($step.Name + '.md')
    [void]$summary.Append('## ' + $index + '. ' + $titles[$step.Name] + "`n`n")
    if (Test-Path -LiteralPath $part) {
        $body = [System.IO.File]::ReadAllText($part, [System.Text.Encoding]::UTF8)
        $body = ($body -split "`r?`n" | ForEach-Object { if ($_ -match '^# ') { '### ' + $_.Substring(2) } else { $_ } }) -join "`n"
        [void]$summary.Append($body.TrimEnd() + "`n`n")
    } else {
        [void]$summary.Append("(kein Ergebnis)`n`n")
    }
}
$summaryPath = Join-Path $outDirFull 'summary.md'
[System.IO.File]::WriteAllText($summaryPath, $summary.ToString(), $utf8)

# 4. Overview.
Write-Host ''
Write-Host '=== Q1 overview ==='
foreach ($line in $overview) { Write-Host $line }
Write-Host ('Summary: ' + $summaryPath)
Write-Host ('Results: ' + $outDirFull)

if ($problems -gt 0) {
    Write-Host ($problems.ToString() + ' problem(s): a step failed or an output file is missing.')
    exit 1
}
exit 0
