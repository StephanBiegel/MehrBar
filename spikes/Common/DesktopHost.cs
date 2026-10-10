using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MehrBar.Spikes.Common;

/// <summary>
/// The window that owns the desktop icons: the top-level window with a SHELLDLL_DefView child
/// (<c>Progman</c> on 24H2 and later, a <c>WorkerW</c> before). <see cref="Hwnd"/> is null when no such
/// window exists (Explorer is not running).
/// </summary>
internal readonly record struct DesktopHostInfo(HWND Hwnd, string ClassName, HWND DefView)
{
    public bool Found => Hwnd != HWND.Null;
}

/// <summary>
/// Looks up the desktop host and answers "is the desktop shown over the bar?". Nothing is cached: the
/// host is searched again on every call because Explorer restarts recreate it with a new handle, and
/// because this machine has ~18 WorkerW windows, so the class name alone never identifies it.
/// </summary>
internal static unsafe class DesktopHost
{
    private const string ShellDefViewClass = "SHELLDLL_DefView";

    /// <summary>Fresh lookup of the desktop host; <see cref="DesktopHostInfo.Found"/> is false when there is none.</summary>
    public static DesktopHostInfo Find()
    {
        var windows = new List<HWND>();
        GCHandle handle = GCHandle.Alloc(windows);
        try
        {
            PInvoke.EnumWindows(&CollectWindow, (LPARAM)GCHandle.ToIntPtr(handle));
        }
        finally
        {
            handle.Free();
        }

        foreach (HWND window in windows)
        {
            HWND defView = PInvoke.FindWindowEx(window, HWND.Null, ShellDefViewClass, null);
            if (defView != HWND.Null)
            {
                return new DesktopHostInfo(window, ClassOf(window), defView);
            }
        }

        return default;
    }

    /// <summary>True when the desktop host lies above <paramref name="bar"/> (walks GW_HWNDPREV upward).</summary>
    public static bool IsAbove(HWND bar) => IsAbove(bar, Find().Hwnd);

    /// <summary>Same walk for a desktop host the caller has just looked up (saves a second window enumeration).</summary>
    public static bool IsAbove(HWND bar, HWND host)
    {
        if (host == HWND.Null)
        {
            return false;
        }

        for (HWND up = PInvoke.GetWindow(bar, GET_WINDOW_CMD.GW_HWNDPREV);
             up != HWND.Null;
             up = PInvoke.GetWindow(up, GET_WINDOW_CMD.GW_HWNDPREV))
        {
            if (up == host)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// True when an ordinary window lies above the desktop host: visible, not minimized, not cloaked, not
    /// topmost, not one of the shell's own windows and not of this process (<paramref name="ownProcessId"/>).
    /// While the desktop is shown nothing like that exists; when the windows come back it does. The bar
    /// itself is skipped, so the answer does not depend on whether the watcher has lifted the bar.
    /// </summary>
    public static bool HasOrdinaryWindowAbove(HWND host, HWND bar, uint ownProcessId)
    {
        for (HWND up = PInvoke.GetWindow(host, GET_WINDOW_CMD.GW_HWNDPREV);
             up != HWND.Null;
             up = PInvoke.GetWindow(up, GET_WINDOW_CMD.GW_HWNDPREV))
        {
            if (up == bar || !IsOrdinary(up, ownProcessId))
            {
                continue;
            }

            return true;
        }

        return false;
    }

    public static string ClassOf(HWND hwnd)
    {
        const int capacity = 256;
        char* buffer = stackalloc char[capacity];
        int length = PInvoke.GetClassName(hwnd, buffer, capacity);
        return length > 0 ? new string(buffer, 0, length) : string.Empty;
    }

    private static bool IsOrdinary(HWND window, uint ownProcessId)
    {
        if (!PInvoke.IsWindowVisible(window) || PInvoke.IsIconic(window))
        {
            return false;
        }

        uint cloaked = 0;
        HRESULT hr = PInvoke.DwmGetWindowAttribute(window, DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, sizeof(uint));
        if (hr.Succeeded && cloaked != 0)
        {
            return false;
        }

        var exStyle = (WINDOW_EX_STYLE)(uint)PInvoke.GetWindowLong(window, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        if (exStyle.HasFlag(WINDOW_EX_STYLE.WS_EX_TOPMOST))
        {
            return false;
        }

        uint pid = 0;
        PInvoke.GetWindowThreadProcessId(window, &pid);
        if (pid == ownProcessId)
        {
            return false;
        }

        string className = ClassOf(window);
        return className is not ("Progman" or "WorkerW" or "Shell_TrayWnd" or "Shell_SecondaryTrayWnd");
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static BOOL CollectWindow(HWND hwnd, LPARAM lParam)
    {
        if (GCHandle.FromIntPtr((IntPtr)lParam.Value).Target is List<HWND> windows)
        {
            windows.Add(hwnd);
        }

        return true;
    }
}
