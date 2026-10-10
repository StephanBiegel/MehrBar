<#
.SYNOPSIS
    Machine check of the Phase 1 spike notes (D-09).

.DESCRIPTION
    Checks that each spike note in .planning/phases/01-risiko-spikes/ has the
    mandatory headings, exactly one decision line and (for spike notes) a
    reference to spikes/results/ in the Messwerte section.

    Default (no arguments): checks the seven spike notes plus the summary note
    01-SPIKE-ERGEBNISSE.md. A missing file is a failure, never a pass.

    -Note <name,...>  check only these notes in the phase directory. A value
                      containing commas is split, because "powershell -File"
                      passes "-Note a.md,b.md" as one string.
    -Path <file>      check an arbitrary file (summary rules if the file name
                      is 01-SPIKE-ERGEBNISSE.md, spike rules otherwise).
    -SelfTest         prove that the checker accepts valid and rejects broken
                      notes; prints SELFTEST OK and exits 0, else exits 1.

    Output: one line per note, "OK <name>" or "FAIL <name>: <reason>".
    Exit code: 0 when everything passes, 1 otherwise.

    Windows PowerShell 5.1 compatible. This file is ASCII-only on purpose:
    Windows PowerShell reads BOM-less scripts as ANSI, so German headings are
    matched with "." in place of umlauts.
#>
[CmdletBinding()]
param(
    [string[]]$Note,
    [string]$Path,
    [switch]$SelfTest
)

$ErrorActionPreference = 'Stop'

$SummaryNoteName = '01-SPIKE-ERGEBNISSE.md'

$SpikeNoteNames = @(
    '01-SPIKE-window-hit.md',
    '01-SPIKE-zorder-wind.md',
    '01-SPIKE-menu.md',
    '01-SPIKE-render-cost.md',
    '01-SPIKE-zoom-anchor.md',
    '01-SPIKE-icons.md',
    '01-SPIKE-tooling.md'
)

# Label is what the note must literally contain; Pattern tolerates umlauts as "."
$SpikeHeadings = @(
    @{ Label = '## Frage';                  Pattern = '^## Frage\s*$' },
    @{ Label = '## Aufbau';                 Pattern = '^## Aufbau\s*$' },
    @{ Label = '## Messwerte';              Pattern = '^## Messwerte\s*$' },
    @{ Label = '## Entscheidung';           Pattern = '^## Entscheidung\s*$' },
    @{ Label = '## Folgen f<ue>r Phase 2/3'; Pattern = '^## Folgen f.r Phase 2/3\s*$' },
    @{ Label = '## Grenzf<ae>lle f<ue>r Stephan'; Pattern = '^## Grenzf.lle f.r Stephan\s*$' }
)

$SummaryHeadings = @(
    @{ Label = '## Ergebnisse je Spike';             Pattern = '^## Ergebnisse je Spike\s*$' },
    @{ Label = '## <Ae>nderungen am Umfang von Phase 2'; Pattern = '^## .nderungen am Umfang von Phase 2\s*$' },
    @{ Label = '## Offen vor Phase 3';               Pattern = '^## Offen vor Phase 3\s*$' },
    @{ Label = '## Entscheidung';                    Pattern = '^## Entscheidung\s*$' }
)

# Any line that claims to be a decision line (used to count them)
$DecisionClaimPattern = '^\*\*Entscheidung:\*\*'
# A well-formed decision line: verdict, then a non-empty strategy text
$DecisionFullPattern = '^\*\*Entscheidung:\*\* (GO|NO-GO|UNKLAR)\b\s*\S'

function Get-NoteLines([string]$File) {
    $text = Get-Content -LiteralPath $File -Encoding UTF8 -Raw
    if ($null -eq $text -or $text.Trim().Length -eq 0) { return $null }
    return @($text -split "`r?`n")
}

# Returns $null when the note passes, otherwise a one-line failure reason.
function Test-NoteFile([string]$File, [string]$Kind) {
    if (-not (Test-Path -LiteralPath $File -PathType Leaf)) {
        return 'file not found'
    }
    $lines = Get-NoteLines $File
    if ($null -eq $lines) {
        return 'file is empty'
    }

    $headings = $SpikeHeadings
    if ($Kind -eq 'summary') { $headings = $SummaryHeadings }

    $missing = @()
    foreach ($h in $headings) {
        $found = $false
        foreach ($line in $lines) {
            if ($line -match $h.Pattern) { $found = $true; break }
        }
        if (-not $found) { $missing += $h.Label }
    }
    if ($missing.Count -gt 0) {
        return ('missing heading(s): ' + ($missing -join '; '))
    }

    $claims = @($lines | Where-Object { $_ -match $DecisionClaimPattern })
    if ($claims.Count -ne 1) {
        return ('expected exactly one "**Entscheidung:**" line, found ' + $claims.Count)
    }
    if ($claims[0] -notmatch $DecisionFullPattern) {
        return 'decision line must read "**Entscheidung:** GO|NO-GO|UNKLAR <strategy>" with a non-empty strategy'
    }

    if ($Kind -ne 'summary') {
        $inSection = $false
        $section = New-Object System.Collections.Generic.List[string]
        foreach ($line in $lines) {
            if ($line -match '^## Messwerte\s*$') { $inSection = $true; continue }
            if ($inSection -and $line -match '^## ') { break }
            if ($inSection) { $section.Add($line) }
        }
        $sectionText = $section -join "`n"
        if ($sectionText -notmatch 'spikes/results/\S+') {
            return 'Messwerte section does not reference a path starting with spikes/results/'
        }
    }

    return $null
}

function Invoke-SelfTest {
    $chUe = [string][char]0xFC
    $chAeLower = [string][char]0xE4
    $chAeUpper = [string][char]0xC4

    function Build-SpikeText([string]$OmitPattern, [string[]]$DecisionLines, [string]$ResultsRef) {
        $out = New-Object System.Collections.Generic.List[string]
        $heads = @(
            '## Frage',
            '## Aufbau',
            '## Messwerte',
            '## Entscheidung',
            ('## Folgen f' + $chUe + 'r Phase 2/3'),
            ('## Grenzf' + $chAeLower + 'lle f' + $chUe + 'r Stephan')
        )
        foreach ($h in $heads) {
            if ($OmitPattern -and $h -match $OmitPattern) { continue }
            $out.Add($h)
            $out.Add('')
            if ($h -eq '## Messwerte') {
                $out.Add('| Lauf | Wert |')
                $out.Add('|------|------|')
                $out.Add('| 1 | ' + $ResultsRef + ' |')
            } elseif ($h -eq '## Entscheidung') {
                foreach ($d in $DecisionLines) { $out.Add($d) }
            } else {
                $out.Add('Text.')
            }
            $out.Add('')
        }
        return ($out -join "`r`n")
    }

    function Build-SummaryText([string[]]$DecisionLines) {
        $out = New-Object System.Collections.Generic.List[string]
        $heads = @(
            '## Ergebnisse je Spike',
            ('## ' + $chAeUpper + 'nderungen am Umfang von Phase 2'),
            '## Offen vor Phase 3',
            '## Entscheidung'
        )
        foreach ($h in $heads) {
            $out.Add($h)
            $out.Add('')
            if ($h -eq '## Entscheidung') {
                foreach ($d in $DecisionLines) { $out.Add($d) }
            } else {
                $out.Add('Text.')
            }
            $out.Add('')
        }
        return ($out -join "`r`n")
    }

    $good = '**Entscheidung:** GO - variant H1 (alpha-1 plate)'
    $ref = 'spikes/results/example-run.csv'

    $cases = @(
        @{ Name = 'valid-spike';          Kind = 'spike';   Expect = $true;  Text = (Build-SpikeText '' @($good) $ref) },
        @{ Name = 'valid-spike-no-go';    Kind = 'spike';   Expect = $true;  Text = (Build-SpikeText '' @('**Entscheidung:** NO-GO - fall back to X') $ref) },
        @{ Name = 'valid-summary';        Kind = 'summary'; Expect = $true;  Text = (Build-SummaryText @($good)) },
        @{ Name = 'missing-heading';      Kind = 'spike';   Expect = $false; Text = (Build-SpikeText '^## Aufbau' @($good) $ref) },
        @{ Name = 'two-decision-lines';   Kind = 'spike';   Expect = $false; Text = (Build-SpikeText '' @($good, '**Entscheidung:** UNKLAR - second line') $ref) },
        @{ Name = 'no-results-reference'; Kind = 'spike';   Expect = $false; Text = (Build-SpikeText '' @($good) 'see lab notebook') },
        @{ Name = 'bad-verdict';          Kind = 'spike';   Expect = $false; Text = (Build-SpikeText '' @('**Entscheidung:** MAYBE - not sure') $ref) },
        @{ Name = 'empty-strategy';       Kind = 'spike';   Expect = $false; Text = (Build-SpikeText '' @('**Entscheidung:** GO') $ref) },
        @{ Name = 'summary-two-decisions'; Kind = 'summary'; Expect = $false; Text = (Build-SummaryText @($good, $good)) }
    )

    $dir = Join-Path $env:TEMP ('gsd-check-notes-selftest-' + [guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $dir | Out-Null
    $problems = New-Object System.Collections.Generic.List[string]
    try {
        foreach ($c in $cases) {
            $file = Join-Path $dir ($c.Name + '.md')
            Set-Content -LiteralPath $file -Value $c.Text -Encoding UTF8
            $reason = Test-NoteFile $file $c.Kind
            $passed = ($null -eq $reason)
            if ($passed -ne $c.Expect) {
                if ($c.Expect) {
                    $problems.Add('valid note rejected: ' + $c.Name + ' (' + $reason + ')')
                } else {
                    $problems.Add('broken note accepted: ' + $c.Name)
                }
            }
        }
        $missingFile = Test-NoteFile (Join-Path $dir 'does-not-exist.md') 'spike'
        if ($null -eq $missingFile) {
            $problems.Add('missing file accepted')
        }
    }
    finally {
        Remove-Item -LiteralPath $dir -Recurse -Force -ErrorAction SilentlyContinue
    }

    if ($problems.Count -eq 0) {
        Write-Host 'SELFTEST OK'
        return 0
    }
    foreach ($p in $problems) { Write-Host ('SELFTEST FAIL: ' + $p) }
    return 1
}

if ($SelfTest) {
    $rc = Invoke-SelfTest
    exit $rc
}

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$phaseDir = Join-Path $repoRoot '.planning\phases\01-risiko-spikes'

$targets = New-Object System.Collections.Generic.List[object]

$explicit = $false
if ($Note) {
    $explicit = $true
    foreach ($entry in $Note) {
        foreach ($name in ($entry -split ',')) {
            $name = $name.Trim()
            if ($name.Length -eq 0) { continue }
            $kind = 'spike'
            if ($name -eq $SummaryNoteName) { $kind = 'summary' }
            $targets.Add([pscustomobject]@{ Name = $name; File = (Join-Path $phaseDir $name); Kind = $kind })
        }
    }
}
if ($Path) {
    $explicit = $true
    $leaf = Split-Path -Leaf $Path
    $kind = 'spike'
    if ($leaf -eq $SummaryNoteName) { $kind = 'summary' }
    $targets.Add([pscustomobject]@{ Name = $leaf; File = $Path; Kind = $kind })
}
if (-not $explicit) {
    foreach ($name in $SpikeNoteNames) {
        $targets.Add([pscustomobject]@{ Name = $name; File = (Join-Path $phaseDir $name); Kind = 'spike' })
    }
    $targets.Add([pscustomobject]@{ Name = $SummaryNoteName; File = (Join-Path $phaseDir $SummaryNoteName); Kind = 'summary' })
}

$failures = 0
foreach ($t in $targets) {
    $reason = Test-NoteFile $t.File $t.Kind
    if ($null -eq $reason) {
        Write-Output ('OK ' + $t.Name)
    } else {
        Write-Output ('FAIL ' + $t.Name + ': ' + $reason)
        $failures++
    }
}

if ($failures -gt 0) { exit 1 }
exit 0
