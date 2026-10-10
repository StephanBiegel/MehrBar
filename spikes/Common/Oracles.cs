using System.Diagnostics;
using System.Globalization;
using System.IO; // not an implicit using in WPF projects
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MehrBar.Spikes.Common;

// Independent oracles of the Driver (RESEARCH Pattern 3): what is on screen, in which order the windows
// lie, and who has the foreground. Everything here only reads; nothing sends input or moves a window.
// All types are internal because the CsWin32 types they expose (HWND) are internal.

/// <summary>
/// Pixel probe: reads screen pixels through the screen DC (GetDC(NULL) + GetPixel), without any access to
/// the window under test. Whether GetPixel returns the composed DWM image is assumption A1; the
/// <c>selftest</c> case proves it.
/// </summary>
internal static class PixelProbe
{
    /// <summary>Value returned for a point that could not be read (outside the screen, no DC).</summary>
    public const int Invalid = -1;

    /// <summary>Screen pixels of the five sentinel probes of a bar window placed at <paramref name="barOrigin"/>.</summary>
    public static IReadOnlyList<(int X, int Y)> ProbePoints((int X, int Y) barOrigin) =>
        SpikeGeometry.ProbeOffsets.Select(p => (barOrigin.X + p.X, barOrigin.Y + p.Y)).ToArray();

    /// <summary>Colours (0xRRGGBB) at the given screen points; <see cref="Invalid"/> where a point is unreadable.</summary>
    public static int[] Sample(IReadOnlyList<(int X, int Y)> points)
    {
        var result = new int[points.Count];
        Array.Fill(result, Invalid);

        HDC dc = PInvoke.GetDC(HWND.Null);
        if (dc.IsNull)
        {
            return result;
        }

        try
        {
            for (int i = 0; i < points.Count; i++)
            {
                uint colorRef = PInvoke.GetPixel(dc, points[i].X, points[i].Y).Value;
                if (colorRef == 0xFFFFFFFF) // CLR_INVALID
                {
                    continue;
                }

                // COLORREF is 0x00BBGGRR; the harness works with 0xRRGGBB.
                result[i] = (int)(((colorRef & 0xFF) << 16) | (colorRef & 0xFF00) | ((colorRef >> 16) & 0xFF));
            }
        }
        finally
        {
            PInvoke.ReleaseDC(HWND.Null, dc);
        }

        return result;
    }

    /// <summary>True when every channel of <paramref name="value"/> is within <paramref name="tolerance"/> of <paramref name="rgb"/>.</summary>
    public static bool Matches(int value, int rgb, int tolerance = 8)
    {
        if (value < 0)
        {
            return false;
        }

        return Math.Abs(((value >> 16) & 0xFF) - ((rgb >> 16) & 0xFF)) <= tolerance &&
               Math.Abs(((value >> 8) & 0xFF) - ((rgb >> 8) & 0xFF)) <= tolerance &&
               Math.Abs((value & 0xFF) - (rgb & 0xFF)) <= tolerance;
    }

    /// <summary>Number of samples that match <paramref name="rgb"/>.</summary>
    public static int CountMatches(IReadOnlyList<int> values, int rgb, int tolerance = 8) =>
        values.Count(v => Matches(v, rgb, tolerance));

    public static string ToHex(int value) =>
        value < 0 ? "invalid" : string.Create(CultureInfo.InvariantCulture, $"#{value:X6}");
}

/// <summary>One top-level window in a z-order snapshot (index 0 is the topmost window).</summary>
internal sealed record ZEntry(
    int Index,
    HWND Hwnd,
    string ClassName,
    string ProcessName,
    bool IsBar,
    bool IsDesktopHost,
    bool Visible,
    bool Cloaked,
    bool Minimized,
    bool IntersectsBar,
    string Title,
    IntRect Rect);

/// <summary>
/// Z-order snapshot. The desktop host (the top-level window that owns SHELLDLL_DefView; Progman or a
/// WorkerW depending on the shell state) is looked up on every snapshot and never cached.
/// Committed output goes through <see cref="ToRedactedCsv"/> only; <see cref="ToPrivateCsv"/> writes
/// real titles and process names and therefore only ever into <see cref="RepoPaths.PrivateResults"/>.
/// </summary>
internal static unsafe class ZOrder
{
    public const string ShellDefViewClass = "SHELLDLL_DefView";

    private static readonly string[] s_shellClasses = ["Progman", "WorkerW", "Shell_TrayWnd"];

    [ThreadStatic]
    private static List<HWND>? s_collected;

    /// <summary>All top-level windows top to bottom, with the bar and the desktop host marked.</summary>
    public static IReadOnlyList<ZEntry> Snapshot(HWND bar)
    {
        var windows = new List<HWND>();
        s_collected = windows;
        try
        {
            PInvoke.EnumWindows(&CollectWindow, 0);
        }
        finally
        {
            s_collected = null;
        }

        IntRect barRect = RectOf(bar);
        HWND desktopHost = FindDesktopHost(windows);
        var processNames = new Dictionary<uint, string>();
        var entries = new List<ZEntry>(windows.Count);
        for (int i = 0; i < windows.Count; i++)
        {
            entries.Add(Describe(windows[i], i, bar, barRect, desktopHost, processNames));
        }

        return entries;
    }

    /// <summary>
    /// The windows below <paramref name="bar"/> in z-order, found by walking GetWindow(GW_HWNDNEXT).
    /// <see cref="ZEntry.Index"/> is the distance from the bar (1 = directly below).
    /// </summary>
    public static IReadOnlyList<ZEntry> Below(HWND bar)
    {
        IntRect barRect = RectOf(bar);
        var chain = new List<HWND>();
        for (HWND next = PInvoke.GetWindow(bar, GET_WINDOW_CMD.GW_HWNDNEXT);
             next != HWND.Null;
             next = PInvoke.GetWindow(next, GET_WINDOW_CMD.GW_HWNDNEXT))
        {
            chain.Add(next);
        }

        HWND desktopHost = FindDesktopHost(chain);
        var processNames = new Dictionary<uint, string>();
        var entries = new List<ZEntry>(chain.Count);
        for (int i = 0; i < chain.Count; i++)
        {
            entries.Add(Describe(chain[i], i + 1, bar, barRect, desktopHost, processNames));
        }

        return entries;
    }

    /// <summary>True for Progman/WorkerW (the shell desktop windows), which may legitimately lie below the bar.</summary>
    public static bool IsShellDesktopClass(string className) => className is "Progman" or "WorkerW";

    /// <summary>
    /// A window that should not lie below the bar: visible, not cloaked, not a shell desktop window and
    /// not one of our own windows.
    /// </summary>
    public static bool IsBlocker(ZEntry entry) =>
        !entry.IsBar && entry.Visible && !entry.Cloaked && !IsShellDesktopClass(entry.ClassName) &&
        !IsOwnProcess(entry.ProcessName);

    /// <summary>Own process names: the bar/witness host and the running driver.</summary>
    public static bool IsOwnProcess(string processName) =>
        processName.Equals("WindowHost", StringComparison.OrdinalIgnoreCase) ||
        processName.Equals(Process.GetCurrentProcess().ProcessName, StringComparison.OrdinalIgnoreCase);

    /// <summary>Class name that may appear in a committed file: shell classes and own windows only, everything else "other".</summary>
    public static string SafeClass(string className, bool isOwn) =>
        isOwn || s_shellClasses.Contains(className, StringComparer.Ordinal) ? className : "other";

    public static string ClassOf(HWND hwnd)
    {
        const int capacity = 256;
        char* buffer = stackalloc char[capacity];
        int length = PInvoke.GetClassName(hwnd, buffer, capacity);
        return length > 0 ? new string(buffer, 0, length) : string.Empty;
    }

    public static string TitleOf(HWND hwnd)
    {
        const int capacity = 256;
        char* buffer = stackalloc char[capacity];
        int length = PInvoke.GetWindowText(hwnd, buffer, capacity);
        return length > 0 ? new string(buffer, 0, length) : string.Empty;
    }

    public static uint ProcessIdOf(HWND hwnd)
    {
        uint pid = 0;
        PInvoke.GetWindowThreadProcessId(hwnd, &pid);
        return pid;
    }

    /// <summary>Process name for a process id; "?" when it cannot be read (exited, protected).</summary>
    public static string ProcessNameOf(uint pid)
    {
        try
        {
            using Process process = Process.GetProcessById((int)pid);
            return process.ProcessName;
        }
        catch (Exception)
        {
            return "?";
        }
    }

    /// <summary>Committed form: foreign windows are class "other" without title or process name.</summary>
    public static string ToRedactedCsv(IReadOnlyList<ZEntry> entries)
    {
        var text = new StringBuilder();
        text.Append("index,hwnd,class,process,is_bar,is_desktop_host,visible,cloaked,minimized,intersects_bar\n");
        foreach (ZEntry e in entries)
        {
            bool keep = e.IsBar || IsOwnProcess(e.ProcessName) || s_shellClasses.Contains(e.ClassName, StringComparer.Ordinal);
            text.Append(Csv(
                e.Index.ToString(CultureInfo.InvariantCulture),
                Hex(e.Hwnd),
                keep ? e.ClassName : "other",
                keep ? e.ProcessName : string.Empty,
                Flag(e.IsBar),
                Flag(e.IsDesktopHost),
                Flag(e.Visible),
                Flag(e.Cloaked),
                Flag(e.Minimized),
                Flag(e.IntersectsBar))).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>
    /// Full form with real class, process and title. It writes the file itself and always inside
    /// <see cref="RepoPaths.PrivateResults"/> (any directory part of <paramref name="fileName"/> is dropped);
    /// returns the path.
    /// </summary>
    public static string ToPrivateCsv(IReadOnlyList<ZEntry> entries, string fileName)
    {
        var text = new StringBuilder();
        text.Append("index,hwnd,class,process,title,x,y,width,height,is_bar,is_desktop_host,visible,cloaked,minimized,intersects_bar\n");
        foreach (ZEntry e in entries)
        {
            text.Append(Csv(
                e.Index.ToString(CultureInfo.InvariantCulture),
                Hex(e.Hwnd),
                e.ClassName,
                e.ProcessName,
                e.Title,
                e.Rect.X.ToString(CultureInfo.InvariantCulture),
                e.Rect.Y.ToString(CultureInfo.InvariantCulture),
                e.Rect.Width.ToString(CultureInfo.InvariantCulture),
                e.Rect.Height.ToString(CultureInfo.InvariantCulture),
                Flag(e.IsBar),
                Flag(e.IsDesktopHost),
                Flag(e.Visible),
                Flag(e.Cloaked),
                Flag(e.Minimized),
                Flag(e.IntersectsBar))).Append('\n');
        }

        string path = Path.Combine(RepoPaths.PrivateResults, Path.GetFileName(fileName));
        File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
        return path;
    }

    private static ZEntry Describe(
        HWND hwnd, int index, HWND bar, IntRect barRect, HWND desktopHost, Dictionary<uint, string> processNames)
    {
        uint pid = ProcessIdOf(hwnd);
        if (!processNames.TryGetValue(pid, out string? processName))
        {
            processName = ProcessNameOf(pid);
            processNames[pid] = processName;
        }

        bool isBar = hwnd == bar;
        IntRect rect = RectOf(hwnd);
        bool intersects = !isBar && rect.Width > 0 && rect.Height > 0 &&
                          rect.X < barRect.Right && rect.Right > barRect.X &&
                          rect.Y < barRect.Bottom && rect.Bottom > barRect.Y;
        return new ZEntry(
            index,
            hwnd,
            ClassOf(hwnd),
            processName,
            isBar,
            desktopHost != HWND.Null && hwnd == desktopHost,
            PInvoke.IsWindowVisible(hwnd),
            IsCloaked(hwnd),
            PInvoke.IsIconic(hwnd),
            intersects,
            TitleOf(hwnd),
            rect);
    }

    /// <summary>The top-level window that has a SHELLDLL_DefView child (looked up fresh on every call).</summary>
    private static HWND FindDesktopHost(IReadOnlyList<HWND> windows)
    {
        foreach (HWND window in windows)
        {
            if (PInvoke.FindWindowEx(window, HWND.Null, ShellDefViewClass, null) != HWND.Null)
            {
                return window;
            }
        }

        return HWND.Null;
    }

    private static IntRect RectOf(HWND hwnd)
    {
        if (!PInvoke.GetWindowRect(hwnd, out RECT rect))
        {
            return new IntRect(0, 0, 0, 0);
        }

        return new IntRect(rect.left, rect.top, rect.right - rect.left, rect.bottom - rect.top);
    }

    private static bool IsCloaked(HWND hwnd)
    {
        uint cloaked = 0;
        HRESULT hr = PInvoke.DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, sizeof(uint));
        return hr.Succeeded && cloaked != 0;
    }

    /// <summary>Raw handle value (CsWin32 handles wrap a pointer).</summary>
    public static nint Raw(HWND hwnd) => (nint)hwnd.Value;

    private static string Hex(HWND hwnd) => string.Create(CultureInfo.InvariantCulture, $"0x{Raw(hwnd):X}");

    private static string Flag(bool value) => value ? "1" : "0";

    private static string Csv(params string[] fields) => string.Join(',', fields.Select(Quote));

    private static string Quote(string field) =>
        field.IndexOfAny(['"', ',', '\r', '\n']) < 0 ? field : "\"" + field.Replace("\"", "\"\"") + "\"";

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static BOOL CollectWindow(HWND hwnd, LPARAM lParam)
    {
        s_collected?.Add(hwnd);
        return true;
    }
}

/// <summary>The foreground window, its class and process, and the focus window of its thread.</summary>
internal readonly record struct ForegroundInfo(HWND Hwnd, string Class, uint Pid, HWND FocusHwnd)
{
    /// <summary>Description that is safe to commit: class only for shell and own windows, otherwise "other".</summary>
    public string Redacted()
    {
        bool own = Pid != 0 && ZOrder.IsOwnProcess(ZOrder.ProcessNameOf(Pid));
        return string.Create(
            CultureInfo.InvariantCulture,
            $"hwnd=0x{ZOrder.Raw(Hwnd):X} class={ZOrder.SafeClass(Class, own)} pid={Pid} focus=0x{ZOrder.Raw(FocusHwnd):X}");
    }
}

internal static unsafe class Foreground
{
    /// <summary>Foreground window plus the focus window of the thread that owns it (GetGUIThreadInfo).</summary>
    public static ForegroundInfo Take()
    {
        HWND hwnd = PInvoke.GetForegroundWindow();
        if (hwnd == HWND.Null)
        {
            return new ForegroundInfo(HWND.Null, string.Empty, 0, HWND.Null);
        }

        uint pid = 0;
        uint threadId = PInvoke.GetWindowThreadProcessId(hwnd, &pid);

        var info = new GUITHREADINFO { cbSize = (uint)sizeof(GUITHREADINFO) };
        HWND focus = threadId != 0 && PInvoke.GetGUIThreadInfo(threadId, &info) ? info.hwndFocus : HWND.Null;
        return new ForegroundInfo(hwnd, ZOrder.ClassOf(hwnd), pid, focus);
    }
}
