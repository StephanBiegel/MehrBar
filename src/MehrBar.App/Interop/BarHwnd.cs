using System.Windows.Interop;
using MehrBar.Core.Layout;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MehrBar.App.Interop;

/// <summary>
/// Win32 side of the bar window, the only place with Win32 calls for it (01-SPIKE-window-hit.md,
/// 01-SPIKE-zorder-wind.md): tool window that never activates, answers <c>WM_MOUSEACTIVATE</c> with
/// <c>MA_NOACTIVATE</c> and rewrites every z-order change to <c>HWND_BOTTOM</c> (the S0 base of the z-order keeper).
/// </summary>
internal sealed unsafe class BarHwnd
{
    private HWND _hwnd;

    /// <summary>Window handle once attached.</summary>
    public IntPtr Handle => _hwnd;

    /// <summary>
    /// When true, z-order changes pass unchanged (the guard of 02-10 lifts the bar above the desktop host while the
    /// desktop is shown). False by default: every change is rewritten to <c>HWND_BOTTOM</c>.
    /// </summary>
    public bool AllowZOrderChange { get; set; }

    /// <summary>Full rectangle of the primary monitor (<c>rcMonitor</c>), never the work area or a screen metric.</summary>
    public static RectI PrimaryMonitorRect()
    {
        HMONITOR monitor = PInvoke.MonitorFromPoint(new System.Drawing.Point(0, 0), MONITOR_FROM_FLAGS.MONITOR_DEFAULTTOPRIMARY);
        var info = new MONITORINFO { cbSize = (uint)sizeof(MONITORINFO) };
        if (!PInvoke.GetMonitorInfo(monitor, &info))
        {
            throw new InvalidOperationException("GetMonitorInfo failed for the primary monitor.");
        }

        RECT r = info.rcMonitor;
        return new RectI(r.left, r.top, r.right - r.left, r.bottom - r.top);
    }

    /// <summary>
    /// Hooks the window procedure first, then sets the styles and shows the window at <c>HWND_BOTTOM</c> in its final
    /// position, all before the first frame.
    /// </summary>
    public void Attach(HwndSource source, RectI initial)
    {
        _hwnd = new HWND(source.Handle);
        source.AddHook(WndProc);

        int exStyle = PInvoke.GetWindowLong(_hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        WINDOW_EX_STYLE wanted = WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_NOACTIVATE;
        PInvoke.SetWindowLong(_hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, exStyle | (int)wanted);

        PInvoke.SetWindowPos(
            _hwnd,
            HWND.HWND_BOTTOM,
            initial.Left,
            initial.Top,
            initial.Width,
            initial.Height,
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW);
    }

    /// <summary>Moves and sizes the window in one call without touching the z-order.</summary>
    public void SetBounds(RectI bounds) =>
        PInvoke.SetWindowPos(
            _hwnd,
            HWND.Null,
            bounds.Left,
            bounds.Top,
            bounds.Width,
            bounds.Height,
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOZORDER);

    /// <summary>Puts the window back at the bottom of the z-order.</summary>
    public void PlaceBottom() =>
        PInvoke.SetWindowPos(
            _hwnd,
            HWND.HWND_BOTTOM,
            0,
            0,
            0,
            0,
            SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE
                | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE | SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER);

    /// <summary>Lets the next launched process take the foreground although the bar never activates.</summary>
    public static void AllowLaunchedProcessForeground() => PInvoke.AllowSetForegroundWindow(PInvoke.ASFW_ANY);

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        switch ((uint)msg)
        {
            case PInvoke.WM_MOUSEACTIVATE:
                handled = true;
                return (IntPtr)PInvoke.MA_NOACTIVATE;
            case PInvoke.WM_WINDOWPOSCHANGING:
                KeepAtBottom((WINDOWPOS*)lParam);
                break;
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// Rewrites every z-order change, including WPF's own first Show, to <c>HWND_BOTTOM</c> (ZOrderGuard.cs S0); the
    /// spike's separate first-show path is not needed with this.
    /// </summary>
    private void KeepAtBottom(WINDOWPOS* position)
    {
        if (AllowZOrderChange
            || position->flags.HasFlag(SET_WINDOW_POS_FLAGS.SWP_NOZORDER)
            || position->hwndInsertAfter == HWND.HWND_BOTTOM)
        {
            return;
        }

        position->hwndInsertAfter = HWND.HWND_BOTTOM;
    }
}
