<#
.SYNOPSIS
  Plan 01-17/01-18: collects the decision line, the chosen variant and the borderline items of the seven
  phase 1 spike notes.
.DESCRIPTION
  Windows PowerShell 5.1, ASCII only (German headings are matched with "." in place of umlauts).
  Text output: per note one block with the file name, exactly one "Entscheidung:" line, the chosen variant and
  one short line per open item under "## Grenzfaelle fuer Stephan" (struck-through items are skipped).
  -AsJson: the same as JSON with the full item texts, for handteil.ps1 and 01-18.
#>
param([switch]$AsJson)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$phaseDir = Join-Path $repoRoot '.planning/phases/01-risiko-spikes'
$notes = @('01-SPIKE-window-hit.md', '01-SPIKE-zorder-wind.md', '01-SPIKE-menu.md', '01-SPIKE-render-cost.md',
    '01-SPIKE-zoom-anchor.md', '01-SPIKE-icons.md', '01-SPIKE-tooling.md')

$result = foreach ($name in $notes) {
    $path = Join-Path $phaseDir $name
    if (-not (Test-Path $path)) { throw "note missing: $name" }
    $lines = [System.IO.File]::ReadAllLines($path, [System.Text.Encoding]::UTF8)

    $decision = ($lines | Where-Object { $_ -like '`*`*Entscheidung:`*`**' } | Select-Object -First 1)
    $decisionText = if ($decision) { ($decision -replace '^\*\*Entscheidung:\*\*\s*', '').Trim() } else { '' }
    $variant = ''
    if ($decisionText -match '\b(H[1-3]|S[0-2]|M-[A-D]|R[12])\b') { $variant = $Matches[1] }

    # Items: "- ..." or "1. ..." lines below the borderline heading up to the next "## " heading.
    $items = New-Object System.Collections.Generic.List[string]
    $inside = $false
    foreach ($line in $lines) {
        if ($line -match '^## ') { $inside = ($line -match '^## Grenzf.lle f.r Stephan'); continue }
        if (-not $inside) { continue }
        if ($line -match '^\s*(-|\d+\.)\s+(.*)$') {
            $text = $Matches[2].Trim()
            if ($text.StartsWith('~~')) { continue }
            $items.Add($text)
        }
    }
    [pscustomobject]@{ File = $name; Decision = $decisionText; Variant = $variant; Items = $items.ToArray() }
}

if ($AsJson) {
    $result | ConvertTo-Json -Depth 4
    exit 0
}

foreach ($note in $result) {
    Write-Output ('== ' + $note.File)
    Write-Output ('Entscheidung: ' + $note.Decision)
    Write-Output ('Variante: ' + $(if ($note.Variant) { $note.Variant } else { '-' }))
    foreach ($item in $note.Items) {
        # Short form: the bold title if there is one, otherwise the first 100 characters.
        $short = if ($item -match '^\*\*(.+?)\*\*') { $Matches[1] } else { $item.Substring(0, [Math]::Min(100, $item.Length)) }
        Write-Output ('  * ' + ($short -replace 'Entscheidung', 'Entsch.'))
    }
}
exit 0
