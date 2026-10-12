<#
.SYNOPSIS
  Fetches PresentMon 2.6.0 (x64 console build) into the git-ignored spikes/tools/presentmon/ folder of the main checkout.

.DESCRIPTION
  Spike tooling for Q4 (frame presentation measurement, D-16). Windows PowerShell 5.1, ASCII only.

  Trust chain (threat T-01-15): the only thing ever downloaded is the release asset
  PresentMon-2.6.0-x64.exe of GameTechDev/PresentMon over HTTPS, and it is kept only when its SHA-256 equals the
  digest recorded in 01-RESEARCH (taken from the GitHub release). On a mismatch the file is deleted and the
  script exits with 1. The download folder contains nothing else and is ignored by git. This script never
  executes the downloaded file; run-render-matrix.ps1 does that, and only after this check passed.

  Exit codes: 0 = verified file present (its path is the last output line), 1 = download or hash failure,
  3 = -CheckOnly and the file is missing.

.PARAMETER CheckOnly
  No network access: only verifies an already downloaded file (exit 0 when present and valid, 3 when missing,
  1 when present with a wrong hash).
#>
param(
    [switch]$CheckOnly
)

$ErrorActionPreference = 'Stop'

$ExpectedName = 'PresentMon-2.6.0-x64.exe'
$ExpectedSha256 = 'b2a706bc6ad475749e3b7e3409263aa1e6906d45bdcf993f6dbc0f660188f1af'
$Repo = 'GameTechDev/PresentMon'
$DownloadPrefix = 'https://github.com/GameTechDev/PresentMon/releases/download/'

function Get-MainRoot {
    # The main checkout is the parent of the common git dir, also when this runs inside a worktree.
    $common = $null
    try {
        $common = (& git -C $PSScriptRoot rev-parse --path-format=absolute --git-common-dir 2>$null)
    } catch {
        $common = $null
    }
    if ($LASTEXITCODE -ne 0 -or -not $common) {
        throw 'git rev-parse --git-common-dir failed; run this script from inside the MehrBar checkout.'
    }
    $text = ([string]($common | Select-Object -First 1)).Trim().TrimEnd('/', '\')
    return (Split-Path -Parent $text)
}

function Get-Sha256([string]$Path) {
    return (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash.ToLowerInvariant()
}

$mainRoot = Get-MainRoot
$folder = Join-Path $mainRoot 'spikes/tools/presentmon'
$target = Join-Path $folder $ExpectedName

if (Test-Path -LiteralPath $target) {
    $actual = Get-Sha256 $target
    if ($actual -eq $ExpectedSha256) {
        Write-Output $target
        exit 0
    }
    if ($CheckOnly) {
        Write-Output ('HASH MISMATCH for ' + $target + ': ' + $actual)
        exit 1
    }
    Write-Warning ('Existing file has the wrong SHA-256 (' + $actual + '); deleting it and downloading again.')
    Remove-Item -LiteralPath $target -Force
}

if ($CheckOnly) {
    Write-Output ('MISSING ' + $target + ' (run without -CheckOnly to download it)')
    exit 3
}

# Windows PowerShell 5.1 defaults to TLS 1.0/1.1 on older builds; GitHub needs 1.2.
[Net.ServicePointManager]::SecurityProtocol = [Net.ServicePointManager]::SecurityProtocol -bor [Net.SecurityProtocolType]::Tls12

$headers = @{ 'User-Agent' = 'MehrBar-spike-tooling'; 'Accept' = 'application/vnd.github+json' }
$releases = Invoke-RestMethod -Uri ('https://api.github.com/repos/' + $Repo + '/releases?per_page=30') -Headers $headers -UseBasicParsing

$asset = $null
foreach ($release in $releases) {
    foreach ($candidate in $release.assets) {
        if ($candidate.name -eq $ExpectedName) {
            $asset = $candidate
            break
        }
    }
    if ($asset) { break }
}
if (-not $asset) {
    Write-Error ('No release of ' + $Repo + ' has an asset named ' + $ExpectedName)
    exit 1
}

$url = [string]$asset.browser_download_url
if (-not $url.StartsWith($DownloadPrefix, [StringComparison]::Ordinal)) {
    Write-Error ('Refusing to download from an unexpected location: ' + $url)
    exit 1
}

New-Item -ItemType Directory -Path $folder -Force | Out-Null
$partial = $target + '.download'
if (Test-Path -LiteralPath $partial) { Remove-Item -LiteralPath $partial -Force }

try {
    Invoke-WebRequest -Uri $url -OutFile $partial -Headers @{ 'User-Agent' = 'MehrBar-spike-tooling' } -UseBasicParsing
} catch {
    if (Test-Path -LiteralPath $partial) { Remove-Item -LiteralPath $partial -Force }
    Write-Error ('Download failed: ' + $_.Exception.Message)
    exit 1
}

$hash = Get-Sha256 $partial
if ($hash -ne $ExpectedSha256) {
    Remove-Item -LiteralPath $partial -Force
    Write-Error ('SHA-256 mismatch for ' + $ExpectedName + ': expected ' + $ExpectedSha256 + ' but got ' + $hash + '. The file was deleted.')
    exit 1
}

Move-Item -LiteralPath $partial -Destination $target -Force
Write-Output $target
exit 0
