using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Threading;
using MehrBar.Spikes.Common;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Accessibility;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MehrBar.Spikes.WindowHost.Variants;

/// <summary>
/// The three layer strategies of the Q2 spike (D-12: the bar stays at the bottom of the z-order, topmost is only a
/// temporary tool of the watcher). The procedure is re-implemented from the description in RESEARCH Pattern 2;
/// no third-party source was consulted (the reference implementation is GPL-2.0, MehrBar is meant for a later
/// public release).
/// <list type="bullet">
/// <item><description>S0: every WM_WINDOWPOSCHANGING that would change the z-order is rewritten to HWND_BOTTOM.</description></item>
/// <item><description>S1: S0 plus a watcher (EVENT_SYSTEM_FOREGROUND WinEvent hook on the UI thread and a 250 ms timer,
/// 100 ms while the desktop is shown). While the desktop host lies above the bar the bar is lifted to topmost; when the
/// windows are back it returns to the bottom.</description></item>
/// <item><description>S2: the same detection, but instead of topmost the bar is re-inserted directly above the desktop host.</description></item>
/// </list>
/// Option <c>--log-events true</c> additionally logs every EVENT_SYSTEM_* and EVENT_OBJECT_REORDER event (logging only),
/// to learn which signal Win+D really sends on this build. Option <c>--guard-drop direct</c> drops the bar with one
/// HWND_BOTTOM call instead of the default two steps (HWND_NOTOPMOST, then HWND_BOTTOM).
/// Host log events: guard-start, desktop-host, desktop-shown, desktop-hidden, zpos, winevent, enforce-bottom,
/// taskbar-created, display-change, session-change.
/// </summary>
[HostVariant("S0", "S1", "S2")]
internal sealed unsafe class ZOrderGuardVariant : IHostVariant
{
    private const int FastIntervalMs = 100;
    private const int SlowIntervalMs = 250;
    private const int MaxEnforceLogRows = 50;

    private const SET_WINDOW_POS_FLAGS PlaceFlags =
        SET_WINDOW_POS_FLAGS.SWP_NOMOVE |
        SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
        SET_WINDOW_POS_FLAGS.SWP_NOOWNERZORDER |
        SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE |
        SET_WINDOW_POS_FLAGS.SWP_NOSENDCHANGING;

    // Event numbers of winuser.h for the log text only; the hook ranges below use the typed constants.
    private static readonly Dictionary<uint, string> s_eventNames = new()
    {
        [0x01] = "SYSTEM_SOUND",
        [0x02] = "SYSTEM_ALERT",
        [0x03] = "SYSTEM_FOREGROUND",
        [0x04] = "SYSTEM_MENUSTART",
        [0x05] = "SYSTEM_MENUEND",
        [0x06] = "SYSTEM_MENUPOPUPSTART",
        [0x07] = "SYSTEM_MENUPOPUPEND",
        [0x08] = "SYSTEM_CAPTURESTART",
        [0x09] = "SYSTEM_CAPTUREEND",
        [0x0A] = "SYSTEM_MOVESIZESTART",
        [0x0B] = "SYSTEM_MOVESIZEEND",
        [0x0E] = "SYSTEM_DRAGDROPSTART",
        [0x0F] = "SYSTEM_DRAGDROPEND",
        [0x10] = "SYSTEM_DIALOGSTART",
        [0x11] = "SYSTEM_DIALOGEND",
        [0x14] = "SYSTEM_SWITCHSTART",
        [0x15] = "SYSTEM_SWITCHEND",
        [0x16] = "SYSTEM_MINIMIZESTART",
        [0x17] = "SYSTEM_MINIMIZEEND",
        [0x20] = "SYSTEM_DESKTOPSWITCH",
        [0x8004] = "OBJECT_REORDER",
    };

    // The OS calls the hook procedures through function pointers to static methods, which are never collected
    // (RESEARCH Pitfall 7); this field only tells the static methods which instance to talk to.
    private static ZOrderGuardVariant? s_active;

    private HostContext? _ctx;
    private string _name = string.Empty;
    private bool _watcher;
    private bool _reinsertAboveHost;
    private bool _directDrop;
    private bool _logEvents;
    private bool _active;
    private bool _lifted;
    private bool _wtsRegistered;
    private uint _taskbarCreated;
    private int _enforceRows;
    private HWND _lastLoggedHost = HWND.Null;
    private DispatcherTimer? _timer;
    private HWINEVENTHOOK _foregroundHook;
    private HWINEVENTHOOK _systemHook;
    private HWINEVENTHOOK _reorderHook;

    public void Attach(HostContext ctx, string name)
    {
        _ctx = ctx;
        _name = name.ToUpperInvariant();
        _watcher = _name is "S1" or "S2";
        _reinsertAboveHost = _name == "S2";
        _directDrop = Option("guard-drop", "two-step") == "direct";
        _logEvents = Option("log-events", "false") == "true";
        _active = true;
        s_active = this;

        Log(
            "guard-start",
            string.Create(
                CultureInfo.InvariantCulture,
                $"strategy={_name} watcher={_watcher} log-events={_logEvents} drop={(_directDrop ? "direct" : "two-step")}"));
        LogHostIfChanged(DesktopHost.Find(), force: true);

        _taskbarCreated = PInvoke.RegisterWindowMessage("TaskbarCreated");
        ctx.AddHook(WndProc);
        _wtsRegistered = PInvoke.WTSRegisterSessionNotification(ctx.Hwnd, PInvoke.NOTIFY_FOR_THIS_SESSION);

        if (_watcher)
        {
            // The hook is delivered through this thread's message loop (WINEVENT_OUTOFCONTEXT), so the
            // callback runs on the UI thread and may touch WPF and the window directly.
            _foregroundHook = PInvoke.SetWinEventHook(
                PInvoke.EVENT_SYSTEM_FOREGROUND,
                PInvoke.EVENT_SYSTEM_FOREGROUND,
                HMODULE.Null,
                &ForegroundEventProc,
                0,
                0,
                PInvoke.WINEVENT_OUTOFCONTEXT | PInvoke.WINEVENT_SKIPOWNPROCESS);
            if (_foregroundHook.IsNull)
            {
                Log("guard-error", "SetWinEventHook(EVENT_SYSTEM_FOREGROUND) failed; the timer is the only trigger");
            }

            _timer = new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromMilliseconds(SlowIntervalMs) };
            _timer.Tick += (_, _) => Evaluate("timer");
            _timer.Start();
        }

        if (_logEvents)
        {
            _systemHook = PInvoke.SetWinEventHook(
                PInvoke.EVENT_SYSTEM_SOUND,
                PInvoke.EVENT_SYSTEM_END,
                HMODULE.Null,
                &LogOnlyEventProc,
                0,
                0,
                PInvoke.WINEVENT_OUTOFCONTEXT | PInvoke.WINEVENT_SKIPOWNPROCESS);
            _reorderHook = PInvoke.SetWinEventHook(
                PInvoke.EVENT_OBJECT_REORDER,
                PInvoke.EVENT_OBJECT_REORDER,
                HMODULE.Null,
                &LogOnlyEventProc,
                0,
                0,
                PInvoke.WINEVENT_OUTOFCONTEXT | PInvoke.WINEVENT_SKIPOWNPROCESS);
        }
    }

    public void Detach()
    {
        if (_ctx == null || !_active)
        {
            return;
        }

        _active = false;
        _timer?.Stop();
        _timer = null;
        Unhook(ref _foregroundHook);
        Unhook(ref _systemHook);
        Unhook(ref _reorderHook);
        if (_wtsRegistered)
        {
            PInvoke.WTSUnRegisterSessionNotification(_ctx.Hwnd);
            _wtsRegistered = false;
        }

        if (_lifted)
        {
            _lifted = false;
            Drop("detach");
        }

        if (ReferenceEquals(s_active, this))
        {
            s_active = null;
        }
    }

    // ---- hook procedures (static, called by the OS) ----------------------------------------------------

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void ForegroundEventProc(
        HWINEVENTHOOK hook, uint evt, HWND hwnd, int idObject, int idChild, uint eventThread, uint eventTime)
    {
        try
        {
            s_active?.Evaluate("foreground");
        }
        catch
        {
            // An exception must never leave an UnmanagedCallersOnly method.
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static void LogOnlyEventProc(
        HWINEVENTHOOK hook, uint evt, HWND hwnd, int idObject, int idChild, uint eventThread, uint eventTime)
    {
        try
        {
            s_active?.LogWinEvent(evt, hwnd, idObject);
        }
        catch
        {
            // An exception must never leave an UnmanagedCallersOnly method.
        }
    }

    // ---- window messages -------------------------------------------------------------------------------

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (!_active)
        {
            return IntPtr.Zero;
        }

        uint message = (uint)msg;
        if (message == PInvoke.WM_WINDOWPOSCHANGING)
        {
            EnforceBottom((WINDOWPOS*)lParam);
        }
        else if (message == PInvoke.WM_DISPLAYCHANGE)
        {
            Log("display-change", string.Create(CultureInfo.InvariantCulture, $"bpp={(long)wParam} size={(long)lParam & 0xFFFF}x{((long)lParam >> 16) & 0xFFFF}"));
            Reassert("display-change");
        }
        else if (message == PInvoke.WM_WTSSESSION_CHANGE)
        {
            Log("session-change", string.Create(CultureInfo.InvariantCulture, $"code={(long)wParam}"));
        }
        else if (_taskbarCreated != 0 && message == _taskbarCreated)
        {
            Log("taskbar-created", string.Empty);
            Reassert("taskbar-created");
        }

        return IntPtr.Zero;
    }

    /// <summary>S0 (and the base of S1/S2): any z-order change of the bar is turned into HWND_BOTTOM, except while the watcher holds it lifted.</summary>
    private void EnforceBottom(WINDOWPOS* position)
    {
        if (_lifted || position->flags.HasFlag(SET_WINDOW_POS_FLAGS.SWP_NOZORDER) || position->hwndInsertAfter == HWND.HWND_BOTTOM)
        {
            return;
        }

        position->hwndInsertAfter = HWND.HWND_BOTTOM;
        if (_enforceRows++ < MaxEnforceLogRows)
        {
            Log("enforce-bottom", "WM_WINDOWPOSCHANGING rewritten to HWND_BOTTOM");
        }
    }

    // ---- watcher ---------------------------------------------------------------------------------------

    /// <summary>
    /// Looks at the desktop host and moves the bar when the state changed. Called for every foreground event and
    /// every timer tick; the host is searched again each time.
    /// </summary>
    private void Evaluate(string source)
    {
        if (_ctx == null || !_active || !_watcher)
        {
            return;
        }

        DesktopHostInfo host = DesktopHost.Find();
        LogHostIfChanged(host, force: false);

        bool shown;
        if (_lifted)
        {
            // The bar is above the host now, so "host above bar" is false by construction. The desktop counts as
            // shown for as long as no ordinary window lies above the host.
            shown = host.Found && !DesktopHost.HasOrdinaryWindowAbove(host.Hwnd, _ctx.Hwnd, (uint)Environment.ProcessId);
        }
        else
        {
            shown = host.Found && DesktopHost.IsAbove(_ctx.Hwnd, host.Hwnd);
        }

        if (shown && !_lifted)
        {
            _lifted = true;
            Log("desktop-shown", $"source={source}");
            Lift(host);
            SetInterval(FastIntervalMs);
        }
        else if (!shown && _lifted)
        {
            _lifted = false;
            Log("desktop-hidden", $"source={source}");
            Drop(source);
            SetInterval(SlowIntervalMs);
        }
    }

    private void Lift(DesktopHostInfo host)
    {
        if (!_reinsertAboveHost)
        {
            Place(HWND.HWND_TOPMOST, "topmost");
            return;
        }

        // Directly above the desktop host without becoming topmost: insert after the closest non-topmost window
        // that lies above the host; when there is none the host is the top of the non-topmost group and
        // HWND_NOTOPMOST (top of that group) is exactly "directly above the host".
        HWND insertAfter = HWND.HWND_NOTOPMOST;
        for (HWND up = PInvoke.GetWindow(host.Hwnd, GET_WINDOW_CMD.GW_HWNDPREV);
             up != HWND.Null;
             up = PInvoke.GetWindow(up, GET_WINDOW_CMD.GW_HWNDPREV))
        {
            var exStyle = (WINDOW_EX_STYLE)(uint)PInvoke.GetWindowLong(up, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
            if (up != _ctx!.Hwnd && !exStyle.HasFlag(WINDOW_EX_STYLE.WS_EX_TOPMOST))
            {
                insertAfter = up;
                break;
            }
        }

        Place(insertAfter, "above-host", $"after=0x{(nint)insertAfter.Value:X}");
    }

    private void Drop(string reason)
    {
        if (_reinsertAboveHost || _directDrop)
        {
            Place(HWND.HWND_BOTTOM, "bottom", $"reason={reason}");
            return;
        }

        Place(HWND.HWND_NOTOPMOST, "notopmost");
        Place(HWND.HWND_BOTTOM, "bottom", $"reason={reason}");
    }

    /// <summary>Explorer restart or display change: find the host again and put the bar back where it belongs.</summary>
    private void Reassert(string reason)
    {
        if (_ctx == null)
        {
            return;
        }

        LogHostIfChanged(DesktopHost.Find(), force: true);
        if (!_lifted)
        {
            Place(HWND.HWND_BOTTOM, "bottom", $"reason={reason}");
        }

        Evaluate(reason);
    }

    private void Place(HWND insertAfter, string label, string extra = "")
    {
        if (_ctx == null)
        {
            return;
        }

        bool ok = PInvoke.SetWindowPos(_ctx.Hwnd, insertAfter, 0, 0, 0, 0, PlaceFlags);
        string detail = extra.Length == 0 ? label : $"{label} {extra}";
        Log("zpos", ok ? detail : $"{detail} FAILED error={Marshal.GetLastWin32Error()}");
    }

    private void SetInterval(int milliseconds)
    {
        if (_timer != null)
        {
            _timer.Interval = TimeSpan.FromMilliseconds(milliseconds);
        }
    }

    // ---- logging ---------------------------------------------------------------------------------------

    private void LogHostIfChanged(DesktopHostInfo host, bool force)
    {
        if (!force && host.Hwnd == _lastLoggedHost)
        {
            return;
        }

        _lastLoggedHost = host.Hwnd;
        Log(
            "desktop-host",
            host.Found
                ? $"class={host.ClassName} hwnd=0x{(nint)host.Hwnd.Value:X}"
                : "class=none hwnd=0x0");
    }

    private void LogWinEvent(uint evt, HWND hwnd, int idObject)
    {
        if (!_active)
        {
            return;
        }

        string name = s_eventNames.TryGetValue(evt, out string? known) ? known : "OTHER";
        string className = hwnd == HWND.Null ? "-" : ZOrder.SafeClass(DesktopHost.ClassOf(hwnd), isOwn: false);
        Log(
            "winevent",
            string.Create(
                CultureInfo.InvariantCulture,
                $"0x{evt:X4} {name} hwnd=0x{(nint)hwnd.Value:X} class={className} object={idObject}"));
    }

    private void Log(string evt, string detail) =>
        _ctx?.Log.Write("host", _name, _name, evt, detail);

    private string Option(string key, string fallback) =>
        _ctx != null && _ctx.Options.TryGetValue(key, out string? value) ? value.ToLowerInvariant() : fallback;

    private static void Unhook(ref HWINEVENTHOOK hook)
    {
        if (!hook.IsNull)
        {
            PInvoke.UnhookWinEvent(hook);
            hook = HWINEVENTHOOK.Null;
        }
    }
}
