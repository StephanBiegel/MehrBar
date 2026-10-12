using System.Globalization;
using System.Windows.Interop;
using MehrBar.Spikes.Common;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MehrBar.Spikes.WindowHost.Variants;

/// <summary>
/// An invisible top-level window (never shown, 0x0, WS_EX_TOOLWINDOW) that owns a native menu in variant M-D, so
/// that the bar itself is never made the foreground window. Created once on the UI thread, which TrackPopupMenuEx
/// requires (the owner must belong to the calling thread).
/// </summary>
internal sealed class HelperOwnerWindow : IDisposable
{
    private const int WsPopup = unchecked((int)0x80000000);
    private const int WsExToolWindow = 0x80;

    private readonly HwndSource _source;

    public HelperOwnerWindow()
    {
        var parameters = new HwndSourceParameters("MehrBar menu owner")
        {
            WindowStyle = WsPopup, // no WS_VISIBLE: the window stays hidden
            ExtendedWindowStyle = WsExToolWindow,
            PositionX = 0,
            PositionY = 0,
            Width = 0,
            Height = 0,
        };
        _source = new HwndSource(parameters);
        Hwnd = new HWND(_source.Handle);
    }

    public HWND Hwnd { get; }

    public void Dispose() => _source.Dispose();
}

/// <summary>
/// Variants M-C and M-D: a native popup menu (CreatePopupMenu, TrackPopupMenuEx with TPM_RETURNCMD).
/// <list type="bullet">
/// <item><description>M-C: the bar is the owner. Per the TrackPopupMenu remarks the owner must be the foreground window
/// first (otherwise the menu does not close on an outside click), and WM_NULL is posted afterwards (otherwise the
/// second open flashes and closes).</description></item>
/// <item><description>M-D: the same sequence with an invisible helper window as owner, so the bar is not touched.</description></item>
/// </list>
/// <c>--menu-restore-focus true</c> hands the foreground back to the previous window after the menu (fallback
/// measurement for 01-15; off by default so the pure technique is measured). Native menus are modal and activate the
/// owner, so <c>--selftest-menu</c> is refused: a dry run must never activate anything.
/// </summary>
[HostVariant("M-C", "M-D")]
internal sealed class MenuNativeVariant : IHostVariant
{
    private const int ItemRectDelayMs = 200;

    private readonly object _gate = new();
    private HostContext? _ctx;
    private string _name = string.Empty;
    private HelperOwnerWindow? _helper;
    private bool _restoreFocus;
    private bool _tracking;
    private HMENU _menu;
    private HWND _owner;

    public void Attach(HostContext ctx, string name)
    {
        _ctx = ctx;
        _name = name;
        _restoreFocus = MenuCommon.IsOption(ctx, "menu-restore-focus");
        if (name.Equals("M-D", StringComparison.OrdinalIgnoreCase))
        {
            _helper = new HelperOwnerWindow();
        }

        _owner = _helper?.Hwnd ?? ctx.Hwnd;
        ctx.AddHook(Hook);
        Log("menu-ready", $"variant={name} activates=True owner={MenuCommon.Hex(_owner)} restore-focus={_restoreFocus}");

        if (MenuCommon.IsOption(ctx, "selftest-menu"))
        {
            Log("menu-selftest-refused", "native menus are modal and activate the owner; the self-test never activates anything");
        }
    }

    public void Detach()
    {
        _helper?.Dispose();
        _helper = null;
    }

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if ((uint)msg != PInvoke.WM_RBUTTONUP || _ctx == null)
        {
            return IntPtr.Zero;
        }

        (int x, int y) = MenuCommon.MessagePosition();
        RECT plate = _ctx.ScreenPlate;
        if (x >= plate.left && x < plate.right && y >= plate.top && y < plate.bottom)
        {
            Track(x, y);
        }
        else
        {
            Log("menu-ignored", string.Create(CultureInfo.InvariantCulture, $"{x},{y} outside the plate"));
        }

        return IntPtr.Zero;
    }

    private void Track(int x, int y)
    {
        if (_ctx == null || _tracking)
        {
            return;
        }

        _tracking = true;
        HMENU menu = PInvoke.CreatePopupMenu();
        try
        {
            foreach ((int itemId, string text) in MenuCommon.Items)
            {
                PInvoke.AppendMenu(menu, MENU_ITEM_FLAGS.MF_STRING, (nuint)itemId, text);
            }

            HWND foregroundBefore = PInvoke.GetForegroundWindow();
            Log("fg-before", $"hwnd={MenuCommon.Hex(foregroundBefore)}");

            // Mandatory per TrackPopupMenu: the owner must be the foreground window before the menu is shown.
            bool ok = PInvoke.SetForegroundWindow(_owner);
            Log("fg-owner-activated", $"ok={ok} foreground={MenuCommon.Hex(PInvoke.GetForegroundWindow())}");

            Log("menu-open", string.Create(CultureInfo.InvariantCulture, $"{x},{y} variant={_name}"));
            lock (_gate)
            {
                _menu = menu;
            }

            using var z = new MenuZWatcher(_ctx, _name);
            z.Start("open");
            using var rectTimer = new Timer(_ => LogItemRects(), null, ItemRectDelayMs, Timeout.Infinite);

            BOOL command = PInvoke.TrackPopupMenuEx(
                menu,
                (uint)(TRACK_POPUP_MENU_FLAGS.TPM_RETURNCMD | TRACK_POPUP_MENU_FLAGS.TPM_RIGHTBUTTON | TRACK_POPUP_MENU_FLAGS.TPM_NONOTIFY),
                x,
                y,
                _owner,
                null);

            lock (_gate)
            {
                _menu = default;
            }

            // Without this the next open of the menu appears and immediately disappears.
            PInvoke.PostMessage(_owner, PInvoke.WM_NULL, default, default);

            int id = command.Value;
            if (id != 0)
            {
                Log("menu-item", $"id={id}");
                if (id == MenuCommon.LaunchItemId)
                {
                    MenuCommon.LaunchWitness(_ctx, _name);
                }
            }

            Log("menu-close", $"reason={(id != 0 ? "item" : "dismissed")}");
            z.Stop("after");

            if (_restoreFocus && foregroundBefore != HWND.Null && foregroundBefore != _owner)
            {
                bool restored = PInvoke.SetForegroundWindow(foregroundBefore);
                Log("fg-restored", $"hwnd={MenuCommon.Hex(foregroundBefore)} ok={restored}");
            }
        }
        finally
        {
            PInvoke.DestroyMenu(menu);
            _tracking = false;
        }
    }

    /// <summary>Item rectangles from a worker thread (the UI thread is inside the menu loop).</summary>
    private void LogItemRects()
    {
        try
        {
            var rects = new List<string>();
            lock (_gate)
            {
                if (_menu == default)
                {
                    return;
                }

                for (uint i = 0; i < MenuCommon.Items.Length; i++)
                {
                    if (!PInvoke.GetMenuItemRect(_owner, _menu, i, out RECT rect))
                    {
                        Log("menu-items-failed", $"GetMenuItemRect failed for item index {i}");
                        return;
                    }

                    rects.Add(string.Create(CultureInfo.InvariantCulture, $"{rect.left},{rect.top},{rect.right},{rect.bottom}"));
                }
            }

            Log("menu-items", string.Join(';', rects));
        }
        catch (ObjectDisposedException)
        {
            // host shutting down
        }
    }

    private void Log(string evt, string detail) =>
        _ctx?.Log.Write("host", _name, string.Empty, evt, detail);
}
