<#
.SYNOPSIS
  Aggregates the Q4 render-matrix summaries (run-render-matrix.ps1) and optional PresentMon CSVs into one markdown
  report with the D-18 / D-19 verdicts per cell.

.DESCRIPTION
  Windows PowerShell 5.1, ASCII only (German words are written without umlauts).

  Input
    -Summaries      one or more q4-<label>-summary.csv files (comma separated is accepted, because
                    "powershell -File" passes "a.csv,b.csv" as one string).
    -PresentMonDir  optional. Folder with the per-cell PresentMon CSVs (pm-<cell>.csv) of ONE label, or the
                    q4-raw parent that contains a folder named like the label. Only rows whose pm_rows column is
                    a number greater than 0 are looked up there; rows of runs without PresentMon never borrow
                    data from another run.
    -Out            markdown file to write (the folder is created).

  Verdict columns (RESEARCH "Auslegung der Schwellen", clarification of D-18, no change of the thresholds)
    D-18 p95        build cost p95 (cost_p95, Stopwatch around handler + OnRender) <= 16.7 ms. The PresentMon
                    p95 of the display-change interval is printed next to it when available (<= 17 ms, because
                    PresentMon intervals are quantized to refresh periods).
    D-18 Ausfaelle  share of dropped frames <= 1 %, computed ONLY from PresentMon: an interval of the display-change
                    column that is at least 2 refresh periods (minus a 5 % tolerance). Intervals above -PauseMs
                    are pauses (idle phase, start), not drops, and are excluded. Without PresentMon rows the
                    verdict is "UNKLAR (PresentMon fehlt: <reason>)".
    Ersatz, nicht D-18   host cadence drop share (cadence > 25 ms, drop_pct) and the DWM frame counters. This
                    column NEVER feeds a verdict and not D-19; it only labels what was measured instead.
    D-19            cpu_total_pct <= 15 (Task Manager share of all logical processors); core_eq and dwm_cpu_total_pct
                    are printed beside it.
    Grenzfall       sources disagree, or a value lies within 10 % of its threshold -> goes to Stephan (D-08).

  PresentMon column names are read from the CSV header at run time (case-insensitive; assumption A4 of 01-RESEARCH:
  2.x may rename them). The display-change column is searched as MsBetweenDisplayChange first, then any column whose
  name contains "BetweenDisplayChange", then MsBetweenPresents. If none exists the cell counts as having no usable
  PresentMon data and says so.

  Percentiles use nearest rank (index = ceil(p/100*n)-1), the same as spikes/Common/Stats.cs.

  Exit codes: 0 = report written, 2 = bad arguments or unreadable input.
#>
param(
    [Parameter(Mandatory = $true)][string[]]$Summaries,
    [string]$PresentMonDir,
    [Parameter(Mandatory = $true)][string]$Out,
    [double]$RefreshHz = 60.0,
    [double]$PauseMs = 250.0
)

$ErrorActionPreference = 'Stop'
$inv = [Globalization.CultureInfo]::InvariantCulture

# Thresholds (D-18, D-19) and the Grenzfall band.
$CostLimitMs = 16.7
$PmP95LimitMs = 17.0
$DropLimitPct = 1.0
$CpuLimitPct = 15.0
$NearBand = 0.10

function ConvertTo-Num([object]$Value) {
    $text = [string]$Value
    if ($null -eq $Value -or $text.Trim().Length -eq 0 -or $text.Trim() -eq 'n/a') { return [double]::NaN }
    $parsed = 0.0
    if ([double]::TryParse($text.Trim(), [Globalization.NumberStyles]::Float, $inv, [ref]$parsed)) { return $parsed }
    return [double]::NaN
}

function Get-Percentile([double[]]$Values, [double]$P) {
    if ($Values.Count -eq 0) { return [double]::NaN }
    $sorted = @($Values | Sort-Object)
    $index = [int][math]::Ceiling($P / 100.0 * $sorted.Count) - 1
    if ($index -lt 0) { $index = 0 }
    if ($index -ge $sorted.Count) { $index = $sorted.Count - 1 }
    return [double]$sorted[$index]
}

function Format-Num([double]$Value, [int]$Decimals) {
    if ([double]::IsNaN($Value) -or [double]::IsInfinity($Value)) { return 'n/a' }
    return $Value.ToString('F' + $Decimals, $inv)
}

# Within 10 % of the limit on either side.
function Test-Near([double]$Value, [double]$Limit) {
    if ([double]::IsNaN($Value)) { return $false }
    return ([math]::Abs($Value - $Limit) -le ($NearBand * $Limit))
}

function Split-Files([string[]]$Values) {
    $result = New-Object System.Collections.Generic.List[string]
    foreach ($value in $Values) {
        foreach ($part in ($value -split ',')) {
            $trimmed = $part.Trim()
            if ($trimmed.Length -gt 0) { $result.Add($trimmed) }
        }
    }
    return , $result.ToArray()
}

# Finds the pm-*.csv of a summary row inside the PresentMon folder; returns $null when absent.
function Find-PmFile([string]$Dir, [pscustomobject]$Row) {
    $base = $Dir
    $labelDir = Join-Path $Dir ([string]$Row.label)
    if (Test-Path -LiteralPath $labelDir -PathType Container) { $base = $labelDir }
    if (-not (Test-Path -LiteralPath $base -PathType Container)) { return $null }
    $opaque = ''
    if ([string]$Row.transparent -eq 'false') { $opaque = '-opaque' }
    $suffix = '-' + $Row.variant + '-' + $Row.count + '-' + $Row.mode + $opaque + '-' + $Row.size + '.csv'
    $hit = Get-ChildItem -LiteralPath $base -Filter 'pm-*.csv' -File |
        Where-Object { $_.Name -match '^pm-\d+' -and $_.Name.EndsWith($suffix, [StringComparison]::OrdinalIgnoreCase) } |
        Sort-Object Name | Select-Object -First 1
    if ($hit) { return $hit.FullName }
    return $null
}

# Reads a PresentMon CSV; the column name comes from the header, never from a fixed position.
function Read-PmStats([string]$File) {
    $result = [pscustomobject]@{ Ok = $false; Reason = ''; Column = ''; N = 0; P95 = [double]::NaN; DropPct = [double]::NaN; Pauses = 0 }
    $rows = @(Import-Csv -LiteralPath $File)
    if ($rows.Count -eq 0) { $result.Reason = 'pm-CSV ohne Zeilen'; return $result }
    $names = @($rows[0].PSObject.Properties | ForEach-Object { $_.Name })
    $column = $null
    foreach ($n in $names) { if ($n -ieq 'MsBetweenDisplayChange') { $column = $n; break } }
    if (-not $column) { foreach ($n in $names) { if ($n -match '(?i)BetweenDisplayChange') { $column = $n; break } } }
    if (-not $column) { foreach ($n in $names) { if ($n -ieq 'MsBetweenPresents') { $column = $n; break } } }
    if (-not $column) {
        $result.Reason = 'keine Intervallspalte im pm-CSV (Spalten: ' + ($names -join ' ') + ')'
        return $result
    }
    $period = 1000.0 / $RefreshHz
    $dropAt = 2.0 * $period * 0.95
    $values = New-Object System.Collections.Generic.List[double]
    $pauses = 0
    foreach ($r in $rows) {
        $v = ConvertTo-Num $r.$column
        if ([double]::IsNaN($v) -or $v -le 0) { continue }
        if ($v -gt $PauseMs) { $pauses++; continue }
        $values.Add($v)
    }
    if ($values.Count -eq 0) { $result.Reason = 'keine gueltigen Intervalle in Spalte ' + $column; return $result }
    $dropped = 0
    foreach ($v in $values) { if ($v -ge $dropAt) { $dropped++ } }
    $result.Ok = $true
    $result.Column = $column
    $result.N = $values.Count
    $result.P95 = Get-Percentile $values.ToArray() 95
    $result.DropPct = 100.0 * $dropped / $values.Count
    $result.Pauses = $pauses
    return $result
}

$files = Split-Files $Summaries
$all = New-Object System.Collections.Generic.List[object]
foreach ($f in $files) {
    if (-not (Test-Path -LiteralPath $f -PathType Leaf)) { Write-Host ('Summary not found: ' + $f); exit 2 }
    foreach ($row in @(Import-Csv -LiteralPath $f)) { $all.Add($row) }
}
if ($all.Count -eq 0) { Write-Host 'No summary rows.'; exit 2 }
if ($PresentMonDir -and -not (Test-Path -LiteralPath $PresentMonDir -PathType Container)) {
    Write-Host ('Note: PresentMon folder not found: ' + $PresentMonDir + ' (all D-18 drop cells become UNKLAR).')
}

$verdictLines = New-Object System.Collections.Generic.List[string]
$detailLines = New-Object System.Collections.Generic.List[string]
$grenz = New-Object System.Collections.Generic.List[string]

foreach ($row in $all) {
    $cell = ([string]$row.label + ' ' + $row.variant + '/' + $row.count + '/' + $row.mode + '/' + $row.size)
    if ([string]$row.transparent -eq 'false') { $cell = $cell + ' (K: nicht transparent)' }

    $costP50 = ConvertTo-Num $row.cost_p50
    $costP95 = ConvertTo-Num $row.cost_p95
    $costP99 = ConvertTo-Num $row.cost_p99
    $cadP95 = ConvertTo-Num $row.cadence_p95
    $cadDrop = ConvertTo-Num $row.drop_pct
    $cpu = ConvertTo-Num $row.cpu_total_pct
    $coreEq = ConvertTo-Num $row.core_eq
    $dwmCpu = ConvertTo-Num $row.dwm_cpu_total_pct
    $pmRows = ConvertTo-Num $row.pm_rows

    $reasons = New-Object System.Collections.Generic.List[string]

    # PresentMon data for this cell, or the reason it is missing.
    $pm = $null
    $pmReason = ''
    if ([double]::IsNaN($pmRows)) {
        $pmReason = 'kein PresentMon-Lauf fuer diese Zelle'
    } elseif ($pmRows -le 0) {
        $pmReason = 'PresentMon schrieb 0 Zeilen (sieht das Layered-Fenster nicht, oder Flag abgelehnt)'
    } elseif (-not $PresentMonDir) {
        $pmReason = 'kein -PresentMonDir angegeben'
    } else {
        $pmFile = Find-PmFile $PresentMonDir $row
        if (-not $pmFile) {
            $pmReason = 'pm-CSV nicht gefunden'
        } else {
            $pm = Read-PmStats $pmFile
            if (-not $pm.Ok) { $pmReason = $pm.Reason; $pm = $null }
        }
    }

    # D-18 p95 (build cost; PresentMon p95 beside it).
    if ([double]::IsNaN($costP95)) {
        $d18p95 = 'UNKLAR (cost_p95 fehlt)'
    } else {
        if ($costP95 -le $CostLimitMs) { $d18p95 = 'OK' } else { $d18p95 = 'FEHLT' }
        $d18p95 = $d18p95 + ' (Kosten p95 ' + (Format-Num $costP95 2) + ' ms'
        if ($pm) { $d18p95 = $d18p95 + '; PM p95 ' + (Format-Num $pm.P95 2) + ' ms' }
        $d18p95 = $d18p95 + ')'
        if (Test-Near $costP95 $CostLimitMs) { $reasons.Add('Kosten p95 nahe 16,7 ms') }
        if ($pm -and ($costP95 -le $CostLimitMs) -and ($pm.P95 -gt $PmP95LimitMs)) { $reasons.Add('Quellen uneins: Kosten bestehen, PM p95 ' + (Format-Num $pm.P95 2) + ' ms') }
        if ($pm -and (Test-Near $pm.P95 $PmP95LimitMs)) { $reasons.Add('PM p95 nahe 17 ms') }
    }

    # D-18 Ausfaelle: PresentMon only.
    if ($pm) {
        if ($pm.DropPct -le $DropLimitPct) { $d18drop = 'OK' } else { $d18drop = 'FEHLT' }
        $d18drop = $d18drop + ' (' + (Format-Num $pm.DropPct 2) + ' % von ' + $pm.N + ' Intervallen, Spalte ' + $pm.Column
        if ($pm.Pauses -gt 0) { $d18drop = $d18drop + ', ' + $pm.Pauses + ' Pausen > ' + (Format-Num $PauseMs 0) + ' ms ausgeschlossen' }
        $d18drop = $d18drop + ')'
        if (Test-Near $pm.DropPct $DropLimitPct) { $reasons.Add('PM-Ausfaelle nahe 1 %') }
        if ((-not [double]::IsNaN($cadDrop)) -and (($pm.DropPct -le $DropLimitPct) -ne ($cadDrop -le $DropLimitPct))) {
            $reasons.Add('Quellen uneins: PM-Ausfaelle ' + (Format-Num $pm.DropPct 2) + ' % gegen Host-Kadenz ' + (Format-Num $cadDrop 2) + ' %')
        }
    } else {
        $d18drop = 'UNKLAR (PresentMon fehlt: ' + $pmReason + ')'
    }

    # Labelled substitute: never a verdict.
    $subst = 'Host-Kadenz > 25 ms: ' + (Format-Num $cadDrop 2) + ' % (p95 ' + (Format-Num $cadP95 2) + ' ms); DWM verworfen ' + $row.dwm_frames_dropped + ', verpasst ' + $row.dwm_frames_missed

    # D-19.
    if ([double]::IsNaN($cpu)) {
        $d19 = 'UNKLAR (cpu_total_pct fehlt)'
    } else {
        if ($cpu -le $CpuLimitPct) { $d19 = 'OK' } else { $d19 = 'FEHLT' }
        $d19 = $d19 + ' (' + (Format-Num $cpu 2) + ' % gesamt; ' + (Format-Num $coreEq 3) + ' Kernaequiv.; dwm.exe ' + (Format-Num $dwmCpu 2) + ' %)'
        if (Test-Near $cpu $CpuLimitPct) { $reasons.Add('CPU nahe 15 %') }
    }

    $grenzText = '-'
    if ($reasons.Count -gt 0) {
        $grenzText = 'Grenzfall: ' + ($reasons -join '; ')
        $grenz.Add($cell + ' - ' + ($reasons -join '; '))
    }

    $verdictLines.Add('| ' + $cell + ' | ' + $d18p95 + ' | ' + $d18drop + ' | ' + $subst + ' | ' + $d19 + ' | ' + $grenzText + ' |')
    $detailLines.Add('| ' + $cell + ' | ' + $row.frames + ' | ' + (Format-Num $costP50 2) + ' / ' + (Format-Num $costP95 2) + ' / ' + (Format-Num $costP99 2) +
        ' | ' + $row.effective_mode + ' (tier ' + $row.tier + ') | ' + $row.idle_frames + ' Frames, ' + $row.idle_cpu_pct + ' % CPU | ' +
        $row.gc0 + '/' + $row.gc1 + '/' + $row.gc2 + ', ' + $row.alloc_per_frame + ' B/Frame | ' + $row.pm_rows + ' |')
}

$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine('# Q4 Zeichenkosten - Auswertung (render-report.ps1)')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('Eingabe: ' + ($files -join ', '))
if ($PresentMonDir) { [void]$sb.AppendLine('PresentMon-Ordner: ' + $PresentMonDir) } else { [void]$sb.AppendLine('PresentMon-Ordner: (keiner)') }
[void]$sb.AppendLine('')
[void]$sb.AppendLine('Schwellen: D-18 Kosten p95 <= 16,7 ms (PresentMon p95 <= 17 ms), Ausfaelle <= 1 % (nur PresentMon: Intervall >= 2 Perioden bei ' + (Format-Num $RefreshHz 0) + ' Hz), D-19 CPU <= 15 % gesamt. Grenzfall = Quellen uneins oder Wert innerhalb 10 % der Schwelle.')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('## Urteile je Zelle (D-18, D-19)')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('| Zelle | D-18 p95 | D-18 Ausfaelle | Ersatz, nicht D-18 | D-19 CPU | Grenzfall |')
[void]$sb.AppendLine('|---|---|---|---|---|---|')
foreach ($l in $verdictLines) { [void]$sb.AppendLine($l) }
[void]$sb.AppendLine('')
[void]$sb.AppendLine('Die Spalte "Ersatz, nicht D-18" ist ein beschrifteter Ersatz (Host-Kadenz, DWM-Zaehler) und fliesst in kein Urteil ein.')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('## Einzelwerte je Zelle')
[void]$sb.AppendLine('')
[void]$sb.AppendLine('| Zelle | Frames | Kosten p50 / p95 / p99 (ms) | Wirksamer Modus | Ruhe (Idle) | GC 0/1/2, Allokation | pm_rows |')
[void]$sb.AppendLine('|---|---|---|---|---|---|---|')
foreach ($l in $detailLines) { [void]$sb.AppendLine($l) }
[void]$sb.AppendLine('')
[void]$sb.AppendLine('## Grenzfaelle fuer Stephan (aus der Auswertung)')
[void]$sb.AppendLine('')
if ($grenz.Count -eq 0) {
    [void]$sb.AppendLine('Keine Zelle ist ein Grenzfall nach diesen Regeln.')
} else {
    foreach ($g in $grenz) { [void]$sb.AppendLine('- ' + $g) }
}

$outDir = Split-Path -Parent $Out
if ($outDir -and -not (Test-Path -LiteralPath $outDir)) { New-Item -ItemType Directory -Path $outDir -Force | Out-Null }
Set-Content -LiteralPath $Out -Value $sb.ToString() -Encoding Ascii
Write-Host ('Report: ' + $Out + ' (' + $all.Count + ' cell(s), ' + $grenz.Count + ' Grenzfall)')
exit 0
