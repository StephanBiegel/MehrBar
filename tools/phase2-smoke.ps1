<#
.SYNOPSIS
  Plan 02-03: desktop smoke test of the bar window (styles, bottom of the z-order, no focus taken, clean exit).
.DESCRIPTION
  Windows PowerShell 5.1, ASCII only. Must run on the interactive desktop; in a Hivemo session (Windows session 0)
  it prints SMOKE SKIP and exits 2 without starting anything.
  Per run: starts MehrBar with --exit-after-ms, waits for its visible window, checks WS_EX_TOOLWINDOW, WS_EX_NOACTIVATE
  and WS_EX_LAYERED set and WS_EX_TOPMOST not set, that no visible, uncloaked window other than Progman/WorkerW lies
  below it, that it is not the foreground window, and that the process exits with code 0.
  Exit: 0 = SMOKE OK, 1 = SMOKE FAIL, 2 = SMOKE SKIP.
#>
param(
    [string]$Exe,
    [switch]$NoBuild,
    [int]$Runs = 1,
    [int]$ExitAfterMs = 4000,
    [string[]]$AppArgs = @()
)
$ErrorActionPreference = 'Stop'
$repoRoot = Split-Path -Parent $PSScriptRoot
if (-not $Exe) { $Exe = Join-Path $repoRoot 'src/MehrBar.App/bin/Debug/net10.0-windows/MehrBar.exe' }

if ((Get-Process -Id $PID).SessionId -eq 0 -or -not [Environment]::UserInteractive) {
    Write-Output 'SMOKE SKIP: no interactive desktop (session 0) - run from the desktop'
    exit 2
}

if (-not $NoBuild) {
    & dotnet build (Join-Path $repoRoot 'src/MehrBar.App') -c Debug --nologo -v q
    if ($LASTEXITCODE -ne 0) { Write-Output 'SMOKE FAIL: build failed'; exit 1 }
}
if (-not (Test-Path $Exe)) { Write-Output ('SMOKE FAIL: exe not found: ' + $Exe); exit 1 }

Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

public static class SmokeWin32
{
    public delegate bool EnumProc(IntPtr hwnd, IntPtr lParam);

    [DllImport("user32.dll")] public static extern bool EnumWindows(EnumProc proc, IntPtr lParam);
    [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hwnd, out uint pid);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern int GetWindowLong(IntPtr hwnd, int index);
    [DllImport("user32.dll")] public static extern IntPtr GetWindow(IntPtr hwnd, uint cmd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern int GetClassName(IntPtr hwnd, StringBuilder name, int max);
    [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
    [DllImport("dwmapi.dll")] public static extern int DwmGetWindowAttribute(IntPtr hwnd, int attr, out int value, int size);

    public static IntPtr FindVisibleWindow(uint pid)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((h, l) =>
        {
            uint owner;
            GetWindowThreadProcessId(h, out owner);
            if (owner == pid && IsWindowVisible(h)) { found = h; return false; }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    public static string ClassOf(IntPtr hwnd)
    {
        var sb = new StringBuilder(256);
        GetClassName(hwnd, sb, sb.Capacity);
        return sb.ToString();
    }

    public static bool IsCloaked(IntPtr hwnd)
    {
        int cloaked;
        return DwmGetWindowAttribute(hwnd, 14, out cloaked, 4) == 0 && cloaked != 0;
    }

    // "Bottom" as in the spikes: no visible, uncloaked ordinary window below the bar.
    public static string FirstBlockingBelow(IntPtr hwnd)
    {
        IntPtr next = GetWindow(hwnd, 2);
        while (next != IntPtr.Zero)
        {
            if (IsWindowVisible(next) && !IsCloaked(next))
            {
                string cls = ClassOf(next);
                if (cls != "Progman" && cls != "WorkerW") { return cls; }
            }
            next = GetWindow(next, 2);
        }
        return "none";
    }
}
'@

$failures = New-Object System.Collections.Generic.List[string]
for ($run = 1; $run -le $Runs; $run++) {
    $argList = @('--exit-after-ms', [string]$ExitAfterMs) + $AppArgs
    $process = Start-Process -FilePath $Exe -ArgumentList $argList -PassThru
    try {
        $hwnd = [IntPtr]::Zero
        $clock = [Diagnostics.Stopwatch]::StartNew()
        while ($clock.ElapsedMilliseconds -lt 3000 -and $hwnd -eq [IntPtr]::Zero) {
            Start-Sleep -Milliseconds 50
            $hwnd = [SmokeWin32]::FindVisibleWindow([uint32]$process.Id)
        }
        if ($hwnd -eq [IntPtr]::Zero) {
            $failures.Add("run $run`: no visible window within 3 s")
            continue
        }

        Start-Sleep -Milliseconds 300
        $ex = [SmokeWin32]::GetWindowLong($hwnd, -20)
        $stylesOk = (($ex -band 0x80) -ne 0) -and (($ex -band 0x08000000) -ne 0) -and (($ex -band 0x80000) -ne 0) -and (($ex -band 0x8) -eq 0)
        $blocking = [SmokeWin32]::FirstBlockingBelow($hwnd)
        $bottom = $blocking -eq 'none'
        $fgOk = [SmokeWin32]::GetForegroundWindow() -ne $hwnd

        $exited = $process.WaitForExit($ExitAfterMs + 5000)
        $code = if ($exited) { $process.ExitCode } else { 'timeout' }
        Write-Output ('SMOKE RUN {0}: exstyle=0x{1:X8} bottom={2} fg-ok={3} exit={4}' -f $run, $ex, $bottom.ToString().ToLower(), $fgOk.ToString().ToLower(), $code)

        if (-not $stylesOk) { $failures.Add(('run {0}: styles 0x{1:X8}' -f $run, $ex)) }
        if (-not $bottom) { $failures.Add("run $run`: window below the bar of class $blocking") }
        if (-not $fgOk) { $failures.Add("run $run`: the bar is the foreground window") }
        if ($code -ne 0) { $failures.Add("run $run`: exit $code") }
    } finally {
        if (-not $process.HasExited) { $process.Kill() }
    }
}

if ($failures.Count -eq 0) {
    Write-Output 'SMOKE OK'
    exit 0
}
Write-Output ('SMOKE FAIL: ' + ($failures -join '; '))
exit 1
