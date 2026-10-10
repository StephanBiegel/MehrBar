<#
.SYNOPSIS
    Generates the Q6 icon fixtures (phase 1, plan 01-06) into spikes/fixtures/ (gitignored).

.DESCRIPTION
    Everything is created here from scratch; nothing third-party enters the repository:

      icons/Only48.ico, icons/Only256.ico   single-frame PNG icons (soft-edged circle, alpha gradient)
      icons/bin48/Only48.exe                tiny console program whose only icon is the 48 px one
      icons/bin256/Only256.exe              the same with the 256 px icon
      icons/notepad.lnk                     shortcut to notepad.exe
      icons/plain.url, icons/iconfile.url   internet shortcuts without and with IconFile
      icons/sample.pdf                      minimal valid PDF
      icons/Ordner/                         plain folder
      icons/store-aumid.txt                 AUMID of Calculator, else the first AUMID containing '!'

    The Store shortcut (icons/store.lnk) is created by Icons.exe itself (IShellLinkW + SetIDList).

    Windows PowerShell 5.1 compatible and ASCII-only on purpose (BOM-less files are read as ANSI).
    Safe to re-run: existing fixtures are overwritten.
#>
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'

$repoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$fixtures = Join-Path $repoRoot 'spikes\fixtures'
$iconDir = Join-Path $fixtures 'icons'
$srcDir = Join-Path $fixtures 'src'
foreach ($d in @($fixtures, $iconDir, $srcDir)) {
    New-Item -ItemType Directory -Force -Path $d | Out-Null
}

Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @"
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

public static class IconFixtureDrawer
{
    // Soft-edged circle: alpha 255 inside, falling linearly to 0 over a soft rim, so the PNG has real
    // partial-alpha edge pixels. Colour changes with position so a premultiplication error shows up.
    public static byte[] Png(int size)
    {
        using (Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
        {
            double c = (size - 1) / 2.0;
            double outer = size * 0.44;
            double soft = size * 0.12;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    double dx = x - c, dy = y - c;
                    double d = Math.Sqrt(dx * dx + dy * dy);
                    double a = Math.Max(0.0, Math.Min(1.0, (outer - d) / soft));
                    int r = (int)Math.Round(255.0 * x / (size - 1));
                    int b = (int)Math.Round(255.0 * (1.0 - (double)y / (size - 1)));
                    bmp.SetPixel(x, y, Color.FromArgb((int)Math.Round(a * 255.0), r, 120, b));
                }
            }
            using (MemoryStream ms = new MemoryStream())
            {
                bmp.Save(ms, ImageFormat.Png);
                return ms.ToArray();
            }
        }
    }
}
"@

# ICO container with exactly one PNG frame (width/height byte 0 means 256).
function New-IcoBytes([byte[]]$Png, [int]$Size) {
    $ms = New-Object System.IO.MemoryStream
    $bw = New-Object System.IO.BinaryWriter($ms)
    $dim = 0
    if ($Size -lt 256) { $dim = $Size }
    $bw.Write([uint16]0)          # reserved
    $bw.Write([uint16]1)          # type: icon
    $bw.Write([uint16]1)          # one image
    $bw.Write([byte]$dim)         # width
    $bw.Write([byte]$dim)         # height
    $bw.Write([byte]0)            # palette colours
    $bw.Write([byte]0)            # reserved
    $bw.Write([uint16]1)          # planes
    $bw.Write([uint16]32)         # bits per pixel
    $bw.Write([uint32]$Png.Length)
    $bw.Write([uint32]22)         # offset: 6 + 16
    $bw.Write($Png)
    $bw.Flush()
    $bytes = $ms.ToArray()
    $bw.Close()
    return ,$bytes
}

function New-OnlyExe([int]$Size) {
    $name = 'Only' + $Size
    $png = [IconFixtureDrawer]::Png($Size)
    $ico = New-IcoBytes $png $Size
    $icoPath = Join-Path $iconDir ($name + '.ico')
    [System.IO.File]::WriteAllBytes($icoPath, $ico)

    $projDir = Join-Path $srcDir $name
    New-Item -ItemType Directory -Force -Path $projDir | Out-Null
    $csproj = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <AssemblyName>$name</AssemblyName>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <ApplicationIcon>..\..\icons\$name.ico</ApplicationIcon>
  </PropertyGroup>
</Project>
"@
    [System.IO.File]::WriteAllText((Join-Path $projDir ($name + '.csproj')), $csproj, [System.Text.Encoding]::ASCII)
    [System.IO.File]::WriteAllText((Join-Path $projDir 'Program.cs'), ('Console.WriteLine("' + $name + '");' + "`r`n"), [System.Text.Encoding]::ASCII)

    $outDir = Join-Path $iconDir ('bin' + $Size)
    & dotnet build (Join-Path $projDir ($name + '.csproj')) -c Release -o $outDir --nologo -v q | Out-Host
    if ($LASTEXITCODE -ne 0) { throw ('dotnet build failed for ' + $name) }
    $exe = Join-Path $outDir ($name + '.exe')
    if (-not (Test-Path -LiteralPath $exe)) { throw ('missing ' + $exe) }
}

New-OnlyExe 48
New-OnlyExe 256

# Shortcut to notepad.exe
$wsh = New-Object -ComObject WScript.Shell
$lnk = $wsh.CreateShortcut((Join-Path $iconDir 'notepad.lnk'))
$lnk.TargetPath = Join-Path $env:SystemRoot 'System32\notepad.exe'
$lnk.Save()

# Internet shortcuts
$plain = "[InternetShortcut]`r`nURL=https://example.org/`r`n"
[System.IO.File]::WriteAllText((Join-Path $iconDir 'plain.url'), $plain, [System.Text.Encoding]::ASCII)
$withIcon = $plain + "IconFile=C:\Windows\System32\shell32.dll`r`nIconIndex=13`r`n"
[System.IO.File]::WriteAllText((Join-Path $iconDir 'iconfile.url'), $withIcon, [System.Text.Encoding]::ASCII)

# Minimal valid PDF with correct xref offsets (all ASCII, LF line ends)
$stream = 'BT /F1 18 Tf 20 100 Td (MehrBar icon fixture) Tj ET'
$objects = @(
    "1 0 obj`n<< /Type /Catalog /Pages 2 0 R >>`nendobj`n",
    "2 0 obj`n<< /Type /Pages /Kids [3 0 R] /Count 1 >>`nendobj`n",
    "3 0 obj`n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>`nendobj`n",
    ("4 0 obj`n<< /Length " + $stream.Length + " >>`nstream`n" + $stream + "`nendstream`nendobj`n"),
    "5 0 obj`n<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>`nendobj`n"
)
$pdf = "%PDF-1.4`n"
$offsets = @()
foreach ($o in $objects) {
    $offsets += $pdf.Length
    $pdf += $o
}
$xrefAt = $pdf.Length
$pdf += "xref`n0 6`n0000000000 65535 f `n"
foreach ($off in $offsets) { $pdf += ("{0:D10} 00000 n `n" -f $off) }
$pdf += "trailer`n<< /Size 6 /Root 1 0 R >>`nstartxref`n$xrefAt`n%%EOF`n"
[System.IO.File]::WriteAllBytes((Join-Path $iconDir 'sample.pdf'), [System.Text.Encoding]::ASCII.GetBytes($pdf))

# Plain folder
New-Item -ItemType Directory -Force -Path (Join-Path $iconDir 'Ordner') | Out-Null

# Store app AUMID: Calculator when installed, else the first one containing '!'
$apps = @(Get-StartApps)
$aumid = $null
$calc = $apps | Where-Object { $_.AppID -eq 'Microsoft.WindowsCalculator_8wekyb3d8bbwe!App' } | Select-Object -First 1
if ($calc) {
    $aumid = $calc.AppID
} else {
    $first = $apps | Where-Object { $_.AppID -like '*!*' } | Select-Object -First 1
    if ($first) { $aumid = $first.AppID }
}
if (-not $aumid) { throw 'no Store AUMID found via Get-StartApps' }
[System.IO.File]::WriteAllText((Join-Path $iconDir 'store-aumid.txt'), $aumid, [System.Text.Encoding]::ASCII)

Write-Output ('fixtures written to ' + $iconDir)
Get-ChildItem -LiteralPath $iconDir | ForEach-Object { Write-Output ('  ' + $_.Name) }
exit 0
