<#
.SYNOPSIS
  Plan 01-17: the single hand pass H1-H7 of phase 1 (D-06, D-07) plus the borderline decisions (D-08).
.DESCRIPTION
  Windows PowerShell 5.1, ASCII only (German prompts written with ae/oe/ue). Must run on the interactive desktop
  (not in session 0): the Driver needs the abort hotkey and real input.

    handteil.ps1            interactive, all steps H1..H7, then the borderline decisions (G)
    handteil.ps1 -List      prints the steps and their commands, runs nothing
    handteil.ps1 -Step H4   runs one step (H1..H7 or G)

  Every question takes j (yes), n (no) or s (skip) plus an optional comment.
  Full answers incl. comments and the H7 network path: spikes/results/private/handteil-answers.csv (ignored).
  Committed summary: spikes/results/handteil/summary.csv with exactly step,item,result (j, n or skip).
  The chosen variants come from collect-decisions.ps1 (fallback H1/S2/M-C/R1).
#>
param(
    [switch]$List,
    [ValidateSet('H1', 'H2', 'H3', 'H4', 'H5', 'H6', 'H7', 'G')]
    [string]$Step
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$driver = Join-Path $repoRoot 'spikes/Driver/bin/Release/net10.0-windows/Driver.exe'
$hostExe = Join-Path $repoRoot 'spikes/WindowHost/bin/Release/net10.0-windows/WindowHost.exe'
$iconsExe = Join-Path $repoRoot 'spikes/Icons/bin/Release/net10.0-windows/Icons.exe'
$private = Join-Path $repoRoot 'spikes/results/private'
$answersCsv = Join-Path $private 'handteil-answers.csv'
$summaryDir = Join-Path $repoRoot 'spikes/results/handteil'
$summaryCsv = Join-Path $summaryDir 'summary.csv'

# Chosen variants from the notes.
$decisions = @(& (Join-Path $PSScriptRoot 'collect-decisions.ps1') -AsObject)
function Get-Variant([string]$note, [string]$fallback) {
    $v = ($decisions | Where-Object { $_.File -eq $note }).Variant
    if ($v) { $v } else { $fallback }
}
$hit = Get-Variant '01-SPIKE-window-hit.md' 'H1'
$guard = Get-Variant '01-SPIKE-zorder-wind.md' 'S2'
$menu = Get-Variant '01-SPIKE-menu.md' 'M-C'
$render = Get-Variant '01-SPIKE-render-cost.md' 'R1'

$steps = [ordered]@{
    H1 = @("Erscheinen ohne Aufblitzen, echte Klicks, Notabbruch ($hit)",
        "WindowHost.exe --variant $hit --exit-after-ms 1500 (10x)",
        "Driver.exe hit --variants $hit --manual --seconds 60",
        "Driver.exe zorder --guard $guard --trigger manual --seconds 30 (Strg+Alt+Q druecken)")
    H2 = @("Sperren und Entsperren ($guard)",
        "Driver.exe zorder --guard $guard --trigger lock --allow-lock")
    H3 = @("Win+D, Ecke, Win+M, Peek, Alt+Tab mit echten Tasten ($guard)",
        "Driver.exe zorder --guard $guard --trigger manual --seconds 180")
    H4 = @("Menues mit echter Tastatur und Maus, Aussehen ($menu, M-A zur Gegenprobe)",
        "Driver.exe menu --variant $menu --manual --seconds 120",
        "Driver.exe menu --variant M-A --manual --seconds 120",
        "spikes/results/private/q3-menu-*.bmp ansehen")
    H5 = @("Zoom-Last mit echter Maus: fluessig? ($render, hw, geglaettet)",
        "Driver.exe render --variant $render --count 20 --render hw --layout smooth --manual --seconds 30",
        "Driver.exe render --variant $render --count 64 --render hw --layout smooth --manual --seconds 30")
    H6 = @("VS Code: Testexplorer findet und startet die Tests",
        "code <repo>")
    H7 = @("Kontaktbogen der Symbole, optional eine Netzwerkdatei",
        "spikes/results/private/q6-contact-sheet.png ansehen",
        "Icons.exe --case net-file --netfile <Pfad> --threads sta --out spikes/results/private/q6-netfile.csv")
}

if ($List) {
    foreach ($key in $steps.Keys) {
        Write-Output ($key + ' ' + $steps[$key][0])
        foreach ($cmd in $steps[$key] | Select-Object -Skip 1) { Write-Output ('     ' + $cmd) }
    }
    Write-Output 'G  Grenzfaelle aus den Notizen entscheiden (collect-decisions.ps1)'
    exit 0
}

foreach ($exe in @($driver, $hostExe, $iconsExe)) {
    if (-not (Test-Path $exe)) {
        Write-Host ('Fehlt: ' + $exe + ' - baue die Spikes (Release) ...')
        Push-Location $repoRoot
        try {
            foreach ($p in 'spikes/WindowHost', 'spikes/Driver', 'spikes/Icons') {
                & dotnet build $p -c Release --nologo -v q
                if ($LASTEXITCODE -ne 0) { throw "build failed: $p" }
            }
        } finally { Pop-Location }
        break
    }
}
New-Item -ItemType Directory -Force -Path $private, $summaryDir | Out-Null

function Save-Answer([string]$stepName, [string]$item, [string]$result, [string]$comment) {
    [pscustomobject]@{ time = (Get-Date).ToString('s'); step = $stepName; item = $item; result = $result; comment = $comment } |
        Export-Csv -Path $answersCsv -Append -NoTypeInformation -Encoding UTF8
    $rows = @()
    if (Test-Path $summaryCsv) { $rows = @(Import-Csv $summaryCsv | Where-Object { -not ($_.step -eq $stepName -and $_.item -eq $item) }) }
    $rows += [pscustomobject]@{ step = $stepName; item = $item; result = $result }
    $rows | Sort-Object step, item | Export-Csv -Path $summaryCsv -NoTypeInformation -Encoding ASCII
}

function Ask([string]$stepName, [string]$item, [string]$question) {
    do { $a = (Read-Host ($question + ' [j/n/s]')).Trim().ToLowerInvariant() } while ($a -notin 'j', 'n', 's')
    $comment = Read-Host 'Bemerkung (Enter = keine)'
    $result = if ($a -eq 's') { 'skip' } else { $a }
    Save-Answer $stepName $item $result $comment
    return $result
}

function Pause-Go([string]$text) {
    Write-Host ''
    Write-Host $text -ForegroundColor Yellow
    return ((Read-Host 'Enter = los, s = Schritt ueberspringen').Trim().ToLowerInvariant() -ne 's')
}

# The bar sits at the very bottom of the z-order (on the desktop), so open windows hide it. Steps that need to see
# it minimize every window first (this console too) and restore them afterwards.
$shell = New-Object -ComObject Shell.Application
function Clear-Desktop {
    Write-Host 'Alle Fenster werden minimiert (auch dieses); sie kommen nach dem Schritt zurueck.' -ForegroundColor Yellow
    Start-Sleep -Milliseconds 1500
    $shell.MinimizeAll()
    Start-Sleep -Milliseconds 800
}
function Restore-Desktop {
    $shell.UndoMinimizeALL()
    Start-Sleep -Milliseconds 800
}

function Run-Driver([string[]]$driverArgs) {
    Write-Host ('> Driver.exe ' + ($driverArgs -join ' ')) -ForegroundColor DarkGray
    & $driver @driverArgs
    $code = $LASTEXITCODE
    Write-Host ('(Exit-Code ' + $code + ')') -ForegroundColor DarkGray
    return $code
}

function Step-H1 {
    if (Pause-Go ("H1a: Die Leiste ($hit) startet 10x im Abstand von 3 s, jeweils 1,5 s lang. Sie entsteht HINTER allen Fenstern, " +
            "deshalb lass ein grosses Fenster ueber dem oberen Bildschirmrand offen: Du solltest NICHTS sehen. Jedes kurze Aufleuchten oben ist ein Aufblitzen. Dauer etwa 30 s.")) {
        Run-Driver @('firstframe', '--variants', $hit, '--launches', '10', '--manual', '--seconds', '30') | Out-Null
        Ask 'H1' 'no-flash' 'Nichts aufblitzen gesehen?' | Out-Null
    } else { Save-Answer 'H1' 'no-flash' 'skip' '' }

    if (Pause-Go ("H1b: 60 s Beobachtung. Die Leiste liegt oben am Bildschirmrand; das tuerkise Fenster in der Mitte ist nur das Zeugenfenster. " +
            "Klicke links und rechts auf die Leiste, 1 px daneben und in den Ueberhang, fahre darueber. Nach 60 s kommt dieses Fenster zurueck.")) {
        Clear-Desktop
        try { Run-Driver @('hit', '--variants', $hit, '--manual', '--seconds', '60') | Out-Null }
        finally { Restore-Desktop }
        Ask 'H1' 'real-mouse' 'Klicks auf der Leiste gezaehlt (siehe Ausgabe oben), daneben gingen sie durch?' | Out-Null
    } else { Save-Answer 'H1' 'real-mouse' 'skip' '' }

    if (Pause-Go "H1c: Notabbruch. Ein 30-s-Lauf startet; druecke nach ein paar Sekunden Strg+Alt+Q (falls belegt: Strg+Alt+Umschalt+Q).") {
        $sw = [System.Diagnostics.Stopwatch]::StartNew()
        $code = Run-Driver @('zorder', '--guard', $guard, '--trigger', 'manual', '--seconds', '30')
        $sw.Stop()
        $auto = if ($code -eq 3 -and $sw.Elapsed.TotalSeconds -lt 29) { 'j' } else { 'n' }
        Write-Host ("Automatisch: Exit-Code $code nach {0:N1} s -> Abbruch erkannt: $auto" -f $sw.Elapsed.TotalSeconds)
        Save-Answer 'H1' 'abort-exit3' $auto ''
        Ask 'H1' 'abort-restore' 'Lauf brach sofort ab und alle Fenster sind wieder da?' | Out-Null
    } else { Save-Answer 'H1' 'abort-exit3' 'skip' ''; Save-Answer 'H1' 'abort-restore' 'skip' '' }
}

function Step-H2 {
    if (Pause-Go "H2: Nach dem Countdown wird der Rechner GESPERRT. Entsperre selbst und schau dann auf die Leiste.") {
        for ($i = 5; $i -ge 1; $i--) { Write-Host "Sperren in $i ..."; Start-Sleep 1 }
        Clear-Desktop
        try { Run-Driver @('zorder', '--guard', $guard, '--trigger', 'lock', '--allow-lock') | Out-Null }
        finally { Restore-Desktop }
        Ask 'H2' 'unlock-visible' 'Leiste nach dem Entsperren sichtbar und unter den Fenstern?' | Out-Null
    } else { Save-Answer 'H2' 'unlock-visible' 'skip' '' }
}

function Step-H3 {
    $key = 'HKCU:\Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced'
    $old = (Get-ItemProperty -Path $key -Name TaskbarSd -ErrorAction SilentlyContinue).TaskbarSd
    $changed = $false
    if ($old -ne 1) {
        $a = Read-Host 'Die Ecke "Desktop anzeigen" ist aus. Fuer H3 voruebergehend einschalten (TaskbarSd=1, danach zurueck)? [j/n]'
        if ($a -eq 'j') { Set-ItemProperty -Path $key -Name TaskbarSd -Value 1 -Type DWord; $changed = $true }
    }
    try {
        if (Pause-Go ("H3: 180 s Beobachtung. Nacheinander mit echten Tasten: Win+D zweimal, Ecke unten rechts zweimal klicken, " +
                "Win+M dann Win+Umschalt+M, Win+, kurz halten und loslassen, Maus in die Ecke halten (Peek), Alt+Tab (steht die Leiste in der Liste?).")) {
            Run-Driver @('zorder', '--guard', $guard, '--trigger', 'manual', '--seconds', '180') | Out-Null
            Ask 'H3' 'win-d' 'Win+D: Leiste kam zurueck, Aufblitzen stoert nicht?' | Out-Null
            Ask 'H3' 'corner' 'Ecke: Leiste kam zurueck, Aufblitzen stoert nicht?' | Out-Null
            Ask 'H3' 'win-m' 'Win+M / Win+Umschalt+M: Leiste blieb sichtbar und unten?' | Out-Null
            Ask 'H3' 'peek-key' 'Win+, : Leiste kam nach dem Loslassen SOFORT zurueck?' | Out-Null
            Ask 'H3' 'peek-corner' 'Peek ueber die Ecke: gibt es das hier, und kam die Leiste zurueck?' | Out-Null
            Ask 'H3' 'alt-tab' 'Alt+Tab: Leiste blieb unten und steht NICHT in der Liste?' | Out-Null
        } else { foreach ($i in 'win-d', 'corner', 'win-m', 'peek-key', 'peek-corner', 'alt-tab') { Save-Answer 'H3' $i 'skip' '' } }
    } finally {
        if ($changed) {
            if ($null -eq $old) { Remove-ItemProperty -Path $key -Name TaskbarSd } else { Set-ItemProperty -Path $key -Name TaskbarSd -Value $old -Type DWord }
            Write-Host 'TaskbarSd zurueckgesetzt.'
        }
    }
}

function Step-H4 {
    foreach ($v in @($menu, 'M-A') | Select-Object -Unique) {
        if (Pause-Go ("H4 ${v}: 120 s. Rechtsklick auf die Leiste, Pfeile/Enter/Escape, Klick daneben, Menue ein zweites Mal oeffnen, " +
                "'Zeuge starten' waehlen. Zum Fokus-Test vorher ein Fenster wiederherstellen und anklicken, dann erst das Menue.")) {
            Clear-Desktop
            try { Run-Driver @('menu', '--variant', $v, '--manual', '--seconds', '120') | Out-Null }
            finally { Restore-Desktop }
            Ask 'H4' "$v-close" "${v}: Menue schliesst bei Klick daneben und mit Escape?" | Out-Null
            Ask 'H4' "$v-keys" "${v}: Pfeile und Enter waehlen richtig?" | Out-Null
            Ask 'H4' "$v-foreground" "${v}: Gestartetes Programm kam sichtbar nach vorn?" | Out-Null
            Ask 'H4' "$v-reopen" "${v}: Menue oeffnet ein zweites Mal?" | Out-Null
            Ask 'H4' "$v-bar-stays-bottom" "${v}: Leiste blieb nach dem Menue UNTER den Fenstern (oder stoert es nicht)?" | Out-Null
            Ask 'H4' "$v-focus-back" "${v}: Danach war wieder das vorher aktive Fenster aktiv (s, wenn nicht getestet)?" | Out-Null
        } else { Save-Answer 'H4' "$v-close" 'skip' '' }
    }
    $bmps = Get-ChildItem $private -Filter 'q3-menu-*.bmp' -ErrorAction SilentlyContinue
    if ($bmps) {
        foreach ($b in $bmps) { Invoke-Item $b.FullName }
        Ask 'H4' 'look' 'Aussehen der Menues (Bilder geoeffnet) in Ordnung?' | Out-Null
    } else { Write-Host 'Keine q3-menu-*.bmp gefunden.'; Save-Answer 'H4' 'look' 'skip' 'no bmp' }
}

function Step-H5 {
    $out = Join-Path $private 'handteil-render.csv'
    foreach ($count in 20, 64) {
        if (Pause-Go "H5: $count Symbole, 30 s. Fahre mit der echten Maus ueber die Leiste, langsam und schnell, bis an die Enden.") {
            Clear-Desktop
            try {
                Run-Driver @('render', '--variant', $render, '--count', [string]$count, '--render', 'hw', '--layout', 'smooth',
                '--manual', '--seconds', '30', '--label', "hand-$count", '--out', $out) | Out-Null
            } finally { Restore-Desktop }
            $row = Import-Csv $out -ErrorAction SilentlyContinue | Where-Object { $_.label -eq "hand-$count" } | Select-Object -Last 1
            if ($row) { Write-Host ("Gemessen: Aufbau p95 {0} ms, Takt p95 {1} ms, Ausfall {2} %" -f $row.cost_p95, $row.cadence_p95, $row.drop_pct) }
            Ask 'H5' "smooth-$count" "$count Symbole: Zoom fluessig, aeussere Symbole ruhig?" | Out-Null
        } else { Save-Answer 'H5' "smooth-$count" 'skip' '' }
    }
}

function Step-H6 {
    if (Pause-Go 'H6: VS Code oeffnet das Repo. Warte, bis das C# Dev Kit geladen hat, dann Testexplorer oeffnen und "Alle Tests ausfuehren".') {
        Start-Process -FilePath 'code' -ArgumentList "`"$repoRoot`"" -ErrorAction SilentlyContinue
        Ask 'H6' 'sln-loads' 'MehrBar.sln laedt im C# Dev Kit ohne Fehler?' | Out-Null
        Ask 'H6' 'tests-listed' 'Testexplorer zeigt die Tests (ScaffoldTests, ZoomAnchorTests, ...)?' | Out-Null
        Ask 'H6' 'tests-green' 'Alle Tests laufen im Testexplorer gruen durch?' | Out-Null
    } else { foreach ($i in 'sln-loads', 'tests-listed', 'tests-green') { Save-Answer 'H6' $i 'skip' '' } }
}

function Step-H7 {
    $sheet = Join-Path $private 'q6-contact-sheet.png'
    if (Test-Path $sheet) {
        Invoke-Item $sheet
        Ask 'H7' 'contact-sheet' 'Symbole auf hell/dunkel/Schachbrett sauber, ohne dunklen Saum (besonders exe-only48 und .url)?' | Out-Null
    } else { Write-Host 'Kontaktbogen fehlt.'; Save-Answer 'H7' 'contact-sheet' 'skip' 'missing' }

    $net = (Read-Host 'Optional: Pfad einer Netzwerkdatei (Enter = ueberspringen)').Trim('" ')
    if ($net) {
        $out = Join-Path $private 'q6-netfile.csv'
        & $iconsExe --case net-file --netfile $net --threads sta --out $out
        if (Test-Path $out) { Import-Csv $out | Format-List | Out-String | Write-Host }
        $r = Ask 'H7' 'net-file' 'Netzwerkdatei: Symbol in Ordnung und schnell genug?'
        Add-Content -Path $answersCsv -Value ('"' + (Get-Date).ToString('s') + '","H7","net-file-path","info","' + ($net -replace '"', '') + '"')
    } else { Save-Answer 'H7' 'net-file' 'skip' '' }
}

function Step-G {
    Write-Host ''
    Write-Host 'Grenzfaelle: zu jedem Punkt deine Entscheidung als Freitext (Enter = spaeter).' -ForegroundColor Yellow
    foreach ($note in $decisions) {
        $n = 0
        foreach ($item in $note.Items) {
            $n++
            if ($item -match '^\*\*H\d') { continue }   # hand steps are covered by H1..H7
            Write-Host ''
            Write-Host ($note.File + ' #' + $n) -ForegroundColor Cyan
            Write-Host $item
            $text = Read-Host 'Entscheidung'
            $id = ($note.File -replace '^01-SPIKE-|\.md$', '') + '-' + $n
            if ($text) { Save-Answer 'G' $id 'j' $text } else { Save-Answer 'G' $id 'skip' '' }
        }
    }
}

$order = if ($Step) { @($Step) } else { @('H1', 'H2', 'H3', 'H4', 'H5', 'H6', 'H7', 'G') }
foreach ($s in $order) {
    Write-Host ''
    Write-Host ('===== ' + $s + $(if ($steps.Contains($s)) { ' - ' + $steps[$s][0] } else { ' - Grenzfaelle' })) -ForegroundColor Green
    & "Step-$s"
}
Write-Host ''
Write-Host ('Fertig. Zusammenfassung: ' + $summaryCsv)
if (Test-Path $summaryCsv) { Import-Csv $summaryCsv | Format-Table -AutoSize | Out-String | Write-Host }
