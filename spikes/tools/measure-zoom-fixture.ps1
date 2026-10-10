# measure-zoom-fixture.ps1 -- Phase 1 / Q5: measure the RocketDock reference screenshots.
#
# Windows PowerShell 5.1, ASCII only. Reads samples/reference/rocketdock-zoom-1..5.png IN PLACE
# (gitignored, third-party icons); the screenshots are never copied. Raw column profiles and the
# per-icon boxes go to spikes/results/private/ (gitignored). Only the numbers that are typed into
# src/MehrBar.Tests/Fixtures/RocketDockReference.cs are meant to be committed.
#
# What it measures (nothing here reads the bar edges listed in docs/PROJEKT_START.md "Nachtrag zu Q12"):
#   1. Rest geometry from image 1: separator lines, slot pitch, separator length, margins.
#   2. Alignment of images 2..5 to image 1 by correlating the sky background below the bar.
#   3. (-Glyphs) glyph boxes of the cursor icon and its neighbours at rest and zoomed.
#
# Usage:
#   powershell -NoProfile -ExecutionPolicy Bypass -File spikes/tools/measure-zoom-fixture.ps1 [-Glyphs] [-Band 3,33]
param(
    [string]$RefDir = '',
    [int[]]$Band = @(3, 33),     # icon rows of the bar (image 1); default = rows where icons sit
    [switch]$Glyphs,
    [int]$SkyThreshold = 30       # summed per-channel difference vs the aligned rest image; higher = ignore soft shadows
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing

$cs = @"
using System;
using System.Drawing;
using System.Drawing.Imaging;
public static class Zm {
  public static byte[] Load(string path, out int w, out int h) {
    using (Bitmap b = new Bitmap(path)) {
      w = b.Width; h = b.Height;
      BitmapData d = b.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
      byte[] buf = new byte[d.Stride * h];
      System.Runtime.InteropServices.Marshal.Copy(d.Scan0, buf, 0, buf.Length);
      b.UnlockBits(d);
      byte[] o = new byte[w * h * 4];
      for (int y = 0; y < h; y++) Buffer.BlockCopy(buf, y * d.Stride, o, y * w * 4, w * 4);
      return o;
    }
  }
  public static int[] Lum(byte[] b, int w, int h) {
    int[] l = new int[w * h];
    for (int i = 0; i < w * h; i++) l[i] = (int)(0.114 * b[i*4] + 0.587 * b[i*4+1] + 0.299 * b[i*4+2]);
    return l;
  }
  // Find (dx,dy) such that img(x+dx, y+dy) == ref(x, y) over reference rows ry0..ry1 (sky below the bar).
  // Returns {dx, dy, inliers, runnerUp}.
  public static int[] FindShift(int[] rf, int rw, int rh, int[] im, int iw, int ih, int ry0, int ry1,
                                int dxMin, int dxMax, int dyMin, int dyMax, int tol) {
    int best = -1, bdx = 0, bdy = 0, second = -1;
    int[,] score = new int[dxMax - dxMin + 1, dyMax - dyMin + 1];
    for (int dx = dxMin; dx <= dxMax; dx++) for (int dy = dyMin; dy <= dyMax; dy++) {
      int ok = 0;
      for (int y = ry0; y <= ry1; y += 2) {
        int yy = y + dy; if (yy < 0 || yy >= ih) continue;
        for (int x = 0; x < rw; x += 2) {
          int xx = x + dx; if (xx < 0 || xx >= iw) continue;
          int d = rf[y * rw + x] - im[yy * iw + xx]; if (d < 0) d = -d;
          if (d <= tol) ok++;
        }
      }
      score[dx - dxMin, dy - dyMin] = ok;
      if (ok > best) { best = ok; bdx = dx; bdy = dy; }
    }
    for (int dx = dxMin; dx <= dxMax; dx++) for (int dy = dyMin; dy <= dyMax; dy++) {
      if (Math.Abs(dx - bdx) <= 1 && Math.Abs(dy - bdy) <= 1) continue;
      if (score[dx - dxMin, dy - dyMin] > second) second = score[dx - dxMin, dy - dyMin];
    }
    return new int[] { bdx, bdy, best, second };
  }
  static int Sad(byte[] b, int w, int x, int y, int x2, int y2) {
    int i = (y * w + x) * 4, j = (y2 * w + x2) * 4;
    return Math.Abs(b[i] - b[j]) + Math.Abs(b[i+1] - b[j+1]) + Math.Abs(b[i+2] - b[j+2]);
  }
  // Pixels inside the bar that are not plain bar glass. The glass has a vertical gradient and a slow
  // horizontal one, so the expected glass colour at (x,y) is predicted from the row below the icons at
  // the same column plus the gradient of an empty column: bg(x,y) = c(x,refRow) + c(refCol,y) - c(refCol,refRow).
  public static bool IsIcon(byte[] b, int w, int x, int y, int refRow, int refCol, int thr) {
    int i = (y * w + x) * 4, r = (refRow * w + x) * 4, g = (y * w + refCol) * 4, o = (refRow * w + refCol) * 4;
    int s = 0;
    for (int k = 0; k < 3; k++) s += Math.Abs(b[i+k] - (b[r+k] + b[g+k] - b[o+k]));
    return s > thr;
  }
  // column counts of icon pixels in rows y0..y1
  public static int[] BarColumns(byte[] b, int w, int h, int y0, int y1, int refRow, int refCol, int thr) {
    int[] c = new int[w];
    for (int x = 0; x < w; x++) for (int y = y0; y <= y1; y++) if (IsIcon(b, w, x, y, refRow, refCol, thr)) c[x]++;
    return c;
  }
  // bbox {minX,maxX,minY,maxY,count} of icon pixels in a window of the bar
  public static int[] BarBox(byte[] b, int w, int h, int x0, int x1, int y0, int y1, int refRow, int refCol, int thr) {
    int mnx = int.MaxValue, mxx = -1, mny = int.MaxValue, mxy = -1, n = 0;
    for (int y = y0; y <= y1; y++) for (int x = x0; x <= x1; x++) {
      if (!IsIcon(b, w, x, y, refRow, refCol, thr)) continue;
      n++; if (x < mnx) mnx = x; if (x > mxx) mxx = x; if (y < mny) mny = y; if (y > mxy) mxy = y;
    }
    return new int[] { mnx, mxx, mny, mxy, n };
  }
  // bbox of pixels that differ from the aligned rest image (sky only, rows below the bar)
  public static int[] SkyBox(byte[] b, int w, int h, byte[] r, int rw, int rh, int dx, int x0, int x1, int y0, int y1, int thr) {
    int mnx = int.MaxValue, mxx = -1, mny = int.MaxValue, mxy = -1, n = 0;
    for (int y = y0; y <= y1; y++) {
      if (y >= rh || y >= h) break;
      for (int x = x0; x <= x1; x++) {
        int x1r = x - dx; if (x1r < 0 || x1r >= rw || x < 0 || x >= w) continue;
        if (Sad2(b, w, x, y, r, rw, x1r, y) <= thr) continue;
        n++; if (x < mnx) mnx = x; if (x > mxx) mxx = x; if (y < mny) mny = y; if (y > mxy) mxy = y;
      }
    }
    return new int[] { mnx, mxx, mny, mxy, n };
  }
  static int Sad2(byte[] a, int aw, int ax, int ay, byte[] c, int cw, int cx, int cy) {
    int i = (ay * aw + ax) * 4, j = (cy * cw + cx) * 4;
    return Math.Abs(a[i] - c[j]) + Math.Abs(a[i+1] - c[j+1]) + Math.Abs(a[i+2] - c[j+2]);
  }
}
"@
Add-Type -TypeDefinition $cs -ReferencedAssemblies System.Drawing

# --- locate the reference folder (current checkout, else the main checkout of a worktree) -----------
function Resolve-RefDir([string]$given) {
    if ($given -ne '' -and (Test-Path -LiteralPath $given)) { return (Resolve-Path -LiteralPath $given).Path }
    $root = (& git rev-parse --show-toplevel).Trim()
    $c = Join-Path $root 'samples\reference'
    if (Test-Path -LiteralPath (Join-Path $c 'rocketdock-zoom-1.png')) { return $c }
    $common = (& git rev-parse --path-format=absolute --git-common-dir).Trim()
    $main = Split-Path -Parent $common
    $c = Join-Path $main 'samples\reference'
    if (Test-Path -LiteralPath (Join-Path $c 'rocketdock-zoom-1.png')) { return $c }
    throw 'samples/reference/rocketdock-zoom-1.png not found (gitignored, local only)'
}
$ref = Resolve-RefDir $RefDir
$repo = (& git rev-parse --show-toplevel).Trim()
$priv = Join-Path $repo 'spikes\results\private'
New-Item -ItemType Directory -Force -Path $priv | Out-Null
Write-Host "reference folder: $ref"
Write-Host "private output : $priv"

$img = @{}
foreach ($n in 1..5) {
    $w = 0; $h = 0
    $b = [Zm]::Load((Join-Path $ref "rocketdock-zoom-$n.png"), [ref]$w, [ref]$h)
    $img[$n] = @{ B = $b; W = $w; H = $h; L = [Zm]::Lum($b, $w, $h) }
    Write-Host ("image {0}: {1}x{2}" -f $n, $w, $h)
}

# --- 1. per-column profiles (private) --------------------------------------------------------------
$y0 = $Band[0]; $y1 = $Band[1]
foreach ($n in 1..5) {
    $d = $img[$n]; $lines = @('x,meanLum,sdLum')
    for ($x = 0; $x -lt $d.W; $x++) {
        $s = 0.0; $s2 = 0.0; $c = 0
        for ($y = $y0; $y -le $y1; $y++) { $l = $d.L[$y * $d.W + $x]; $s += $l; $s2 += $l * $l; $c++ }
        $m = $s / $c; $sd = [math]::Sqrt([math]::Max(0, $s2 / $c - $m * $m))
        $lines += ('{0},{1:N1},{2:N1}' -f $x, $m, $sd)
    }
    Set-Content -LiteralPath (Join-Path $priv "zoom-fixture-profile-$n.csv") -Value $lines -Encoding ASCII
}
Write-Host "column profiles written (rows $y0..$y1)"

# --- 2. rest geometry from image 1 ------------------------------------------------------------------
$d1 = $img[1]
# empty bar column (left of the first icon) supplies the vertical glass gradient; row 35 lies below the icons
$refCol = 28; $refRow = 35
$cols = [Zm]::BarColumns($d1.B, $d1.W, $d1.H, $y0, $y1, $refRow, $refCol, 50)
# a separator is a thin vertical line: many icon-like rows in one column, empty columns beside it
$seps = @()
for ($x = 24; $x -lt 604; $x++) {
    if ($cols[$x] -ge 18 -and $cols[$x-1] -le 2 -and $cols[$x+1] -le 2 -and $cols[$x-2] -le 2 -and $cols[$x+2] -le 2) { $seps += $x }
}
Write-Host ("separator line columns (image 1 px): " + ($seps -join ', '))
if ($seps.Count -ne 3) { Write-Warning "expected 3 separator lines, found $($seps.Count)" }
$s1 = $seps[0] + 0.5; $s2 = $seps[1] + 0.5; $s3 = $seps[2] + 0.5
$d12 = $s2 - $s1; $d23 = $s3 - $s2
$pitch = ($d12 - $d23) / 3.0          # 6p+q and 3p+q
$sepLen = $d23 - 3 * $pitch
Write-Host ("line spacing 1-2 = {0}, 2-3 = {1}  =>  pitch = {2:N2}, separator length = {3:N2}" -f $d12, $d23, $pitch, $sepLen)
# slot rows from the line positions (group 1 = 6 icons left of line 1, group 4 = 1 icon right of line 3)
$p = [math]::Round($pitch); $q = [math]::Round($sepLen)
$g1start = $s1 - $q / 2 - 6 * $p
$g4end = $s3 + $q / 2 + $p
Write-Host ("first slot starts at x1 = {0}, last slot ends at x1 = {1}" -f $g1start, $g4end)
# bar edges on a mid-icon row: first/last pixel that is bar glass (not sky) -- read at row 20
function Sky([byte[]]$b, [int]$w, [int]$x, [int]$y) { $i = ($y * $w + $x) * 4; return ($b[$i] + $b[$i+1] + $b[$i+2]) -lt 200 }
$barL = -1; for ($x = 0; $x -lt 60; $x++) { if (-not (Sky $d1.B $d1.W $x 20)) { $barL = $x; break } }
$barR = -1; for ($x = $d1.W - 1; $x -gt 560; $x--) { if (-not (Sky $d1.B $d1.W $x 20)) { $barR = $x; break } }
Write-Host ("bar glass spans x1 = {0}..{1} (first/last non-sky pixel at row 20)" -f $barL, $barR)
Write-Host ("leading margin = {0}, trailing margin = {1}  (bar edge to first/last slot, using bar edge {2} / {3})" -f `
    ($g1start - $barL), ((($barR + 1) - $g4end)), $barL, ($barR + 1))

# --- 3. alignment of images 2..5 to image 1 (sky background only) ----------------------------------
$shift = @{}
foreach ($n in 2..5) {
    $d = $img[$n]
    $r = [Zm]::FindShift($d1.L, $d1.W, $d1.H, $d.L, $d.W, $d.H, 60, 100, -150, 350, -20, 120, 2)
    $shift[$n] = $r
    Write-Host ("image {0} -> image 1: dx = {1}, dy = {2}  (inliers {3}, runner-up {4})" -f $n, $r[0], $r[1], $r[2], $r[3])
}
$shiftCsv = @('image,dx,dy,inliers,runnerUp')
foreach ($n in 2..5) { $shiftCsv += ('{0},{1},{2},{3},{4}' -f $n, $shift[$n][0], $shift[$n][1], $shift[$n][2], $shift[$n][3]) }
Set-Content -LiteralPath (Join-Path $priv 'zoom-fixture-shift.csv') -Value $shiftCsv -Encoding ASCII

if (-not $Glyphs) { return }

# --- 4. glyph boxes ---------------------------------------------------------------------------------
# slot index (0..15) -> slot start in image-1 px, from the measured geometry above
function SlotStart([int]$k) {
    $grp = 0; if ($k -ge 6) { $grp = 1 }; if ($k -ge 12) { $grp = 2 }; if ($k -ge 15) { $grp = 3 }
    return $g1start + $k * $p + $grp * $q
}
$names = 'FileZilla','VS Code','Visual Studio','Arduino','Screen','A+','Cube','Ring','P-touch','PDF','a','Picture','Visio','Word','Publisher','Excel'
$rows = @('image,icon,slot,mode,minX,maxX,minY,maxY,width,centreX,pixels')
function Box([string]$imgId, [string]$icon, [int]$slot, [string]$mode, [int[]]$bx) {
    if ($bx[4] -le 0) { Write-Host ("  {0} {1}: no pixels" -f $imgId, $icon); return }
    $wd = $bx[1] - $bx[0] + 1; $cx = ($bx[0] + $bx[1] + 1) / 2.0
    Write-Host ("  img {0} {1,-14} slot {2,2} {3,-4} x {4}..{5}  width {6}  centre {7}  rows {8}..{9}" -f $imgId, $icon, $slot, $mode, $bx[0], $bx[1], $wd, $cx, $bx[2], $bx[3])
    $script:rows += ('{0},{1},{2},{3},{4},{5},{6},{7},{8},{9},{10}' -f $imgId, $icon, $slot, $mode, $bx[0], $bx[1], $bx[2], $bx[3], $wd, $cx, $bx[4])
}
Write-Host 'rest glyph boxes (image 1, icon pixels inside each 32-px slot, rows band):'
foreach ($k in 0..15) {
    $a = [int][math]::Floor((SlotStart $k)); $b = $a + $p - 1
    Box '1' $names[$k] $k 'bar' ([Zm]::BarBox($d1.B, $d1.W, $d1.H, $a, $b, $y0, $y1, $refRow, $refCol, 50))
}

# zoomed boxes: sky = pixels differing from the aligned rest image below the bar (rows 40..104),
# bar = icon pixels in the bar rows. Windows (image n px) lie between the visible gaps of the neighbours.
$zoom = @(
    @{ n = 2; icon = 'Cube';      slot = 6;  mode = 'bar'; x0 = 244; x1 = 284 },
    @{ n = 2; icon = 'Ring';      slot = 7;  mode = 'sky'; x0 = 283; x1 = 380 },
    @{ n = 2; icon = 'P-touch';   slot = 8;  mode = 'sky'; x0 = 380; x1 = 495 },
    @{ n = 2; icon = 'PDF';       slot = 9;  mode = 'sky'; x0 = 495; x1 = 592 },
    @{ n = 2; icon = 'a';         slot = 10; mode = 'bar'; x0 = 591; x1 = 630 },
    @{ n = 3; icon = 'VS Code';   slot = 1;  mode = 'sky'; x0 = 153; x1 = 267 },
    @{ n = 4; icon = 'Publisher'; slot = 14; mode = 'sky'; x0 = 548; x1 = 664 },
    @{ n = 5; icon = 'Excel';     slot = 15; mode = 'sky'; x0 = 626; x1 = 740 }
)
Write-Host 'zoomed glyph boxes:'
foreach ($z in $zoom) {
    $d = $img[$z.n]
    if ($z.mode -eq 'sky') {
        $bx = [Zm]::SkyBox($d.B, $d.W, $d.H, $d1.B, $d1.W, $d1.H, $shift[$z.n][0], $z.x0, $z.x1, 40, 104, $SkyThreshold)
    } else {
        $bx = [Zm]::BarBox($d.B, $d.W, $d.H, $z.x0, $z.x1, $y0, $y1, $refRow, 28, 50)
    }
    Box ([string]$z.n) $z.icon $z.slot $z.mode $bx
}
Set-Content -LiteralPath (Join-Path $priv 'zoom-glyphs.csv') -Value $rows -Encoding ASCII
Write-Host "glyph boxes written to spikes/results/private/zoom-glyphs.csv"
