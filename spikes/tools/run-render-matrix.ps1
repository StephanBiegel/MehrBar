<#
.SYNOPSIS
  Runs the Q4 render-cost matrix (D-17): counts x modes x variants (plus control and fallback sizes) through
  "Driver render", one summary row per cell.

.DESCRIPTION
  Windows PowerShell 5.1, ASCII only. Reads nothing from the user's desktop and sends input only through the
  Driver render case, which runs inside a SafetySession (abort hotkey Ctrl+Alt+Q). A real run moves the mouse
  for the whole matrix (about warmup + seconds + idle + 10 s per cell): hands off the mouse and keyboard.

  Output
    spikes/results/q4-<label>-summary.csv           committed, one row per cell
    <main checkout>/spikes/results/private/q4-raw/<label>/   per-frame CSVs and PresentMon CSVs (ignored)
  With -DryRun the summary goes to spikes/results/dry/q4-<label>-summary.csv and the raw files to
  spikes/results/dry/q4-raw/<label>/ (both ignored, never committed).

  Cells
    Sizes x Counts x Modes x Variants (transparent window), plus with -Control the same drawing in a
    non-transparent window (--transparent false) for each variant at count 64, hw, first size.
    Fallback ladder (D-13): -Sizes 1900x250,1000x160.

  PresentMon (-PresentMon): needs an elevated session (ETW). The script refuses otherwise (exit 2) before it
  downloads or starts anything. It calls get-presentmon.ps1 first (SHA-256 gate), then starts PresentMon per cell
  in the background with
    --process_name WindowHost.exe --output_file <raw>/pm-<cell>.csv --timed <warmup+seconds+idle+10> --terminate_after_timed
  and records the number of data rows in the summary column pm_rows. These flag names follow the PresentMon 2.x
  command line as described in 01-RESEARCH; they have NOT been checked against "PresentMon-2.6.0-x64.exe --help"
  yet (no download in plan 01-12). If a flag is rejected, PresentMon exits at once and pm_rows is 0; check the
  console output of the first cell before trusting a whole run.

  Exit codes: 0 = all cells ran, 1 = a cell or a build failed, 2 = -PresentMon without elevation or bad arguments.

.PARAMETER DryRun
  One synthetic R1 / 20 / hw cell of about 5 s. No real input, no PresentMon, nothing committed.

.PARAMETER Yes
  Skips the confirmation prompt of a real (input sending) run. Only for use after the hands-off checkpoint.
#>
param(
    [string]$Label = (Get-Date -Format 'yyyyMMdd-HHmmss'),
    [string[]]$Counts = @('20', '64'),
    [string[]]$Modes = @('hw', 'sw'),
    [string[]]$Variants = @('R1', 'R2'),
    [switch]$Control,
    [string[]]$Sizes = @('1900x250'),
    [int]$Seconds = 30,
    [int]$Warmup = 5,
    [int]$Idle = 5,
    [switch]$DryRun,
    [switch]$PresentMon,
    [switch]$Yes
)

$ErrorActionPreference = 'Stop'

function Split-List([string[]]$Values) {
    $result = @()
    foreach ($value in $Values) {
        foreach ($part in ($value -split ',')) {
            $trimmed = $part.Trim()
            if ($trimmed.Length -gt 0) { $result += $trimmed }
        }
    }
    return , $result
}

function Test-Elevated {
    $identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $principal = New-Object Security.Principal.WindowsPrincipal($identity)
    return $principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

# 1. Refuse -PresentMon without elevation before anything else happens (no download, no build).
if ($PresentMon -and -not $DryRun) {
    if (-not (Test-Elevated)) {
        Write-Host 'REFUSED: -PresentMon needs an elevated session (PresentMon uses ETW). Start an administrator PowerShell and run again.'
        exit 2
    }
}
if ($PresentMon -and $DryRun) {
    Write-Host 'Note: -DryRun ignores -PresentMon (a dry run never starts PresentMon).'
    $PresentMon = [switch]$false
}

$countList = Split-List $Counts
$modeList = Split-List $Modes
$variantList = Split-List $Variants
$sizeList = Split-List $Sizes

if ($Label -notmatch '^[A-Za-z0-9._-]+$') { Write-Host 'Bad -Label (letters, digits, dot, underscore, dash).'; exit 2 }
foreach ($c in $countList) { if ($c -notmatch '^\d+$') { Write-Host ('Bad count: ' + $c); exit 2 } }
foreach ($m in $modeList) { if ($m -notin @('hw', 'sw')) { Write-Host ('Bad mode: ' + $m); exit 2 } }
foreach ($v in $variantList) { if ($v -notin @('R1', 'R2')) { Write-Host ('Bad variant: ' + $v); exit 2 } }
foreach ($s in $sizeList) { if ($s -notmatch '^\d{3,5}x\d{2,5}$') { Write-Host ('Bad size: ' + $s); exit 2 } }

$repoRoot = (Resolve-Path (Join-Path $PSScriptRoot '..\..')).Path
$common = $null
try { $common = (& git -C $repoRoot rev-parse --path-format=absolute --git-common-dir 2>$null) } catch { $common = $null }
if ($LASTEXITCODE -eq 0 -and $common) {
    $mainRoot = Split-Path -Parent (([string]($common | Select-Object -First 1)).Trim().TrimEnd('/', '\'))
} else {
    $mainRoot = $repoRoot
}

if ($DryRun) {
    $summaryPath = Join-Path $repoRoot ('spikes/results/dry/q4-' + $Label + '-summary.csv')
    $rawDir = Join-Path $repoRoot ('spikes/results/dry/q4-raw/' + $Label)
} else {
    $summaryPath = Join-Path $repoRoot ('spikes/results/q4-' + $Label + '-summary.csv')
    $rawDir = Join-Path $mainRoot ('spikes/results/private/q4-raw/' + $Label)
}
New-Item -ItemType Directory -Path (Split-Path -Parent $summaryPath) -Force | Out-Null
New-Item -ItemType Directory -Path $rawDir -Force | Out-Null

if (Test-Path -LiteralPath $summaryPath) {
    if ($DryRun) {
        Remove-Item -LiteralPath $summaryPath -Force
    } else {
        Write-Host ('Summary already exists: ' + $summaryPath + ' - choose another -Label.')
        exit 2
    }
}

# 2. Cells.
$cells = @()
if ($DryRun) {
    $cells += [pscustomobject]@{ Variant = 'R1'; Count = '20'; Mode = 'hw'; Transparent = 'true'; Size = $sizeList[0] }
} else {
    foreach ($size in $sizeList) {
        foreach ($count in $countList) {
            foreach ($mode in $modeList) {
                foreach ($variant in $variantList) {
                    $cells += [pscustomobject]@{ Variant = $variant; Count = $count; Mode = $mode; Transparent = 'true'; Size = $size }
                }
            }
        }
    }
    if ($Control) {
        foreach ($variant in $variantList) {
            $cells += [pscustomobject]@{ Variant = $variant; Count = '64'; Mode = 'hw'; Transparent = 'false'; Size = $sizeList[0] }
        }
    }
}

$cellSeconds = if ($DryRun) { 5 } else { $Seconds }
$cellWarmup = if ($DryRun) { 1 } else { $Warmup }
$cellIdle = if ($DryRun) { 2 } else { $Idle }
$minutes = [math]::Round($cells.Count * ($cellWarmup + $cellSeconds + $cellIdle + 10) / 60.0, 1)
Write-Host ('Matrix ' + $Label + ': ' + $cells.Count + ' cell(s), about ' + $minutes + ' min' + $(if ($DryRun) { ' (dry run, synthetic, no input)' } else { '' }))

if (-not $DryRun -and -not $Yes) {
    Write-Host 'This run MOVES THE MOUSE (SendInput) and minimizes windows (Win+M). Hands off until it ends. Abort: Ctrl+Alt+Q.'
    $answer = Read-Host 'Type GO to continue'
    if ($answer -ne 'GO') { Write-Host 'Cancelled.'; exit 2 }
}

# 3. PresentMon (hash gate first).
$pmExe = $null
if ($PresentMon) {
    $fetched = & (Join-Path $PSScriptRoot 'get-presentmon.ps1')
    if ($LASTEXITCODE -ne 0) { Write-Host 'get-presentmon.ps1 failed.'; exit 1 }
    $pmExe = [string]($fetched | Select-Object -Last 1)
    if (-not (Test-Path -LiteralPath $pmExe)) { Write-Host ('PresentMon not found at ' + $pmExe); exit 1 }
}

# 4. Build once.
Push-Location $repoRoot
try {
    & dotnet build 'spikes/WindowHost' -c Release -warnaserror --nologo -v q
    if ($LASTEXITCODE -ne 0) { Write-Host 'WindowHost build failed.'; exit 1 }
    & dotnet build 'spikes/Driver' -c Release -warnaserror --nologo -v q
    if ($LASTEXITCODE -ne 0) { Write-Host 'Driver build failed.'; exit 1 }
} finally {
    Pop-Location
}
$driver = Join-Path $repoRoot 'spikes/Driver/bin/Release/net10.0-windows/Driver.exe'
if (-not (Test-Path -LiteralPath $driver)) { Write-Host ('Driver.exe not found: ' + $driver); exit 1 }

# 5. Run the cells.
$failed = 0
$index = 0
foreach ($cell in $cells) {
    $index++
    $cellName = ('{0:00}-{1}-{2}-{3}{4}-{5}' -f $index, $cell.Variant, $cell.Count, $cell.Mode, $(if ($cell.Transparent -eq 'false') { '-opaque' } else { '' }), $cell.Size)
    $cellCsv = Join-Path $rawDir ('cell-' + $cellName + '.csv')
    if (Test-Path -LiteralPath $cellCsv) { Remove-Item -LiteralPath $cellCsv -Force }
    Write-Host ('--- cell ' + $index + '/' + $cells.Count + ': ' + $cellName)

    $driverArgs = @(
        'render',
        '--variant', $cell.Variant,
        '--count', $cell.Count,
        '--render', $cell.Mode,
        '--transparent', $cell.Transparent,
        '--size', $cell.Size,
        '--seconds', [string]$cellSeconds,
        '--warmup', [string]$cellWarmup,
        '--idle', [string]$cellIdle,
        '--label', $Label,
        '--out', $cellCsv,
        '--frames-dir', $rawDir
    )
    if ($DryRun) { $driverArgs += '--synthetic' }

    $pm = $null
    $pmCsv = Join-Path $rawDir ('pm-' + $cellName + '.csv')
    if ($PresentMon) {
        if (Test-Path -LiteralPath $pmCsv) { Remove-Item -LiteralPath $pmCsv -Force }
        $timed = [string]($cellWarmup + $cellSeconds + $cellIdle + 10)
        $pmArgs = @('--process_name', 'WindowHost.exe', '--output_file', ('"' + $pmCsv + '"'), '--timed', $timed, '--terminate_after_timed')
        $pm = Start-Process -FilePath $pmExe -ArgumentList $pmArgs -PassThru -WindowStyle Hidden
        Start-Sleep -Seconds 2
    }

    & $driver @driverArgs
    $code = $LASTEXITCODE

    $pmRows = $null
    if ($pm) {
        if (-not $pm.WaitForExit(40000)) {
            Write-Host 'PresentMon did not stop by itself; stopping it.'
            Stop-Process -Id $pm.Id -Force -ErrorAction SilentlyContinue
        }
        $pmRows = 0
        if (Test-Path -LiteralPath $pmCsv) {
            $pmRows = [math]::Max(0, @(Get-Content -LiteralPath $pmCsv).Count - 1)
        }
        Write-Host ('PresentMon rows for WindowHost.exe: ' + $pmRows)
        if ($pmRows -eq 0) { Write-Host 'WARNING: PresentMon wrote no rows (rejected flag, no presents, or started too late); check its flags.' }
    }

    if ($code -ne 0) {
        Write-Host ('cell failed: Driver exit code ' + $code)
        $failed++
    }
    if (-not (Test-Path -LiteralPath $cellCsv)) {
        Write-Host 'no summary row written for this cell'
        if ($code -eq 0) { $failed++ }
        continue
    }

    $lines = @(Get-Content -LiteralPath $cellCsv)
    if ($lines.Count -lt 2) { $failed++; continue }
    $row = $lines[$lines.Count - 1]
    if ($null -ne $pmRows) { $row = $row -replace ',n/a$', (',' + $pmRows) }
    if (-not (Test-Path -LiteralPath $summaryPath)) {
        Set-Content -LiteralPath $summaryPath -Value $lines[0] -Encoding Ascii
    }
    Add-Content -LiteralPath $summaryPath -Value $row -Encoding Ascii

    Start-Sleep -Seconds 2
}

# 6. Report.
if (Test-Path -LiteralPath $summaryPath) {
    Write-Host ''
    Write-Host ('Summary: ' + $summaryPath)
    $table = Import-Csv -LiteralPath $summaryPath |
        Format-Table label, variant, count, mode, transparent, size, frames, cost_p50, cost_p95, cadence_p95, drop_pct, cpu_total_pct, core_eq, dwm_cpu_total_pct, idle_frames, effective_mode, pm_rows -AutoSize |
        Out-String -Width 250
    Write-Host $table
}
Write-Host ('Raw files: ' + $rawDir)

if ($failed -gt 0) {
    Write-Host ($failed.ToString() + ' cell(s) failed.')
    exit 1
}
exit 0
