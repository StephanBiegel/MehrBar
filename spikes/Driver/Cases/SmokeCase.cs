using System.Globalization;
using MehrBar.Spikes.Common;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MehrBar.Spikes.Driver.Cases;

/// <summary>
/// End-to-end proof of the two-process harness without any input: the Driver launches the bar and
/// checks from outside what the host claims (READY, extended styles, rectangle, z-order, foreground,
/// host CSV rows). It only reads the desktop; nothing is sent and no window is moved.
/// </summary>
[DriverCase("smoke")]
public sealed class SmokeCase : IDriverCase
{
    public int Run(CaseContext ctx)
    {
        var failures = new List<string>();

        void Check(string name, bool ok, string detail)
        {
            Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}: {detail}");
            ctx.Log.Write("smoke", "H1", string.Empty, ok ? "pass" : "fail", $"{name}: {detail}");
            if (!ok)
            {
                failures.Add(name);
            }
        }

        ForegroundInfo before = Foreground.Take();
        Console.WriteLine($"foreground before: {before.Redacted()}");

        HostProcess host;
        try
        {
            host = ctx.StartHost("--exit-after-ms 4000");
        }
        catch (Exception ex) when (ex is InvalidOperationException or TimeoutException or FileNotFoundException)
        {
            Check("READY handshake", false, ex.Message);
            return Finish(failures);
        }

        Check(
            "READY handshake",
            host.Role == "bar" && host.Hwnd != HWND.Null && PInvoke.IsWindow(host.Hwnd),
            string.Create(CultureInfo.InvariantCulture, $"role={host.Role} hwnd=0x{ZOrder.Raw(host.Hwnd):X} pid={host.Pid}"));

        // Foreground must be untouched right after the bar appeared (NOACTIVATE, ShowActivated false).
        ForegroundInfo afterShow = Foreground.Take();
        Check(
            "foreground unchanged after show",
            afterShow.Hwnd == before.Hwnd,
            afterShow.Hwnd == before.Hwnd ? "same window" : $"now {afterShow.Redacted()}, host pid is {host.Pid}");

        uint exStyle = (uint)PInvoke.GetWindowLong(host.Hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        var style = (WINDOW_EX_STYLE)exStyle;
        Check(
            "ex-style TOOLWINDOW|NOACTIVATE",
            style.HasFlag(WINDOW_EX_STYLE.WS_EX_TOOLWINDOW) && style.HasFlag(WINDOW_EX_STYLE.WS_EX_NOACTIVATE),
            string.Create(CultureInfo.InvariantCulture, $"exstyle=0x{exStyle:X8}"));

        (int expectedX, int expectedY) = SpikeGeometry.DefaultWindowPos();
        var expected = new IntRect(expectedX, expectedY, SpikeGeometry.WindowWidth, SpikeGeometry.WindowHeight);
        IntRect actual = PInvoke.GetWindowRect(host.Hwnd, out RECT rect)
            ? new IntRect(rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top)
            : new IntRect(0, 0, 0, 0);
        Check("window rectangle equals SpikeGeometry", actual == expected, $"actual={Describe(actual)} expected={Describe(expected)}");

        // Z-order from outside: nothing visible and foreign may lie below the bar except the shell desktop.
        IReadOnlyList<ZEntry> below = ZOrder.Below(host.Hwnd);
        ZEntry? blocker = below.FirstOrDefault(ZOrder.IsBlocker);
        Check(
            "bar is below every foreign visible window",
            blocker == null,
            blocker == null
                ? $"{below.Count} windows below, none visible and foreign"
                : $"blocked by {ZOrder.SafeClass(blocker.ClassName, false)} at distance {blocker.Index}");

        IReadOnlyList<ZEntry> snapshot = ZOrder.Snapshot(host.Hwnd);
        ZEntry? bar = snapshot.FirstOrDefault(e => e.IsBar);
        string desktopHosts = string.Join(
            ';',
            snapshot.Where(e => ZOrder.IsShellDesktopClass(e.ClassName) || e.IsDesktopHost)
                .Select(e => $"{e.ClassName}@{e.Index}{(e.IsDesktopHost ? "(host)" : string.Empty)}"));
        string layout = string.Create(
            CultureInfo.InvariantCulture,
            $"bar@{bar?.Index ?? -1} of {snapshot.Count}; shell windows: {desktopHosts}");
        Console.WriteLine($"z-order: {layout}");
        ctx.Log.Write("smoke", "H1", string.Empty, "z-order", layout);
        Check("snapshot contains the bar", bar != null, bar == null ? "bar missing" : $"index {bar.Index}");

        // Host-side rows: written before READY (self-check) and with the first frame.
        HostRow? selfCheck = host.WaitForRow("self-check", 2000);
        Check(
            "host row self-check bottom=true",
            selfCheck != null && selfCheck.Detail.Contains("bottom=true", StringComparison.Ordinal),
            selfCheck?.Detail ?? "row missing");

        HostRow? firstFrame = host.WaitForRow("first-frame", 2000);
        Check("host row first-frame", firstFrame != null, firstFrame?.Detail ?? "row missing");

        ForegroundInfo afterChecks = Foreground.Take();
        Check(
            "foreground unchanged after checks",
            afterChecks.Hwnd == before.Hwnd,
            afterChecks.Hwnd == before.Hwnd ? "same window" : $"now {afterChecks.Redacted()}");

        host.Kill();
        return Finish(failures);
    }

    private static int Finish(List<string> failures)
    {
        if (failures.Count == 0)
        {
            Console.WriteLine("SMOKE OK");
            return ExitCodes.Ok;
        }

        Console.WriteLine($"SMOKE FAILED ({failures.Count}): {string.Join(", ", failures)}");
        return ExitCodes.CriteriaFailed;
    }

    private static string Describe(IntRect r) =>
        string.Create(CultureInfo.InvariantCulture, $"{r.X},{r.Y} {r.Width}x{r.Height}");
}
