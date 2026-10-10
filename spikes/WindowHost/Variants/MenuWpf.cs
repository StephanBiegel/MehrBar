using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using MehrBar.Spikes.Common;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace MehrBar.Spikes.WindowHost.Variants;

/// <summary>
/// Pieces shared by the four menu variants (Q3): the identical item list, the helper that starts the witness for
/// item 3, the cursor position of a mouse message and the z-position watcher that answers assumption A5 (does the
/// menu or its activation lift the non-activating bar?).
/// </summary>
internal static unsafe class MenuCommon
{
    /// <summary>Items 1..4, identical in every variant. Item 3 launches a witness window; item 4 only logs.</summary>
    public static readonly (int Id, string Text)[] Items =
    [
        (1, "Eintrag 1"),
        (2, "Eintrag 2"),
        (3, "Zeuge starten"),
        (4, "Schließen"),
    ];

    public const int LaunchItemId = 3;

    /// <summary>Safety net: a launched witness ends by itself if the Driver never kills it.</summary>
    private const int LaunchedLifetimeMs = 60000;

    public static string Hex(HWND hwnd) => string.Create(CultureInfo.InvariantCulture, $"0x{(nint)hwnd.Value:X}");

    /// <summary>Screen position of the message that is currently being processed.</summary>
    public static (int X, int Y) MessagePosition()
    {
        uint packed = PInvoke.GetMessagePos();
        return ((short)(packed & 0xFFFF), (short)((packed >> 16) & 0xFFFF));
    }

    /// <summary>
    /// Item 3: starts <c>WindowHost.exe --role witness --title Launched --cover none</c> through the shell, i.e. the way
    /// the product will start a program from the bar. The target is this very executable (fixed spike path,
    /// normal integrity, no user-supplied input); the pid is logged so the Driver can check the foreground owner.
    /// </summary>
    public static void LaunchWitness(HostContext ctx, string variant)
    {
        string exe = Environment.ProcessPath ?? RepoPaths.HostExe;
        string arguments = string.Create(
            CultureInfo.InvariantCulture,
            $"--role witness --title Launched --cover none --exit-after-ms {LaunchedLifetimeMs}");
        try
        {
            using Process? started = Process.Start(new ProcessStartInfo(exe, arguments) { UseShellExecute = true });
            string pid = started == null ? "none" : started.Id.ToString(CultureInfo.InvariantCulture);
            ctx.Log.Write("host", variant, string.Empty, "menu-launch", $"pid={pid}");
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            ctx.Log.Write("host", variant, string.Empty, "menu-launch", $"failed={ex.GetType().Name}");
        }
    }

    public static bool IsOption(HostContext ctx, string key) =>
        ctx.Options.TryGetValue(key, out string? value) &&
        string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Logs the bar's z-position (<c>menu-zpos index=N phase=…</c>) every 50 ms while a menu is open, but only when it
/// changes. Runs on a thread-pool timer because a native menu loop blocks the UI thread.
/// </summary>
internal sealed class MenuZWatcher : IDisposable
{
    private const int PeriodMs = 50;

    private readonly HostContext _ctx;
    private readonly string _variant;
    private readonly object _gate = new();
    private Timer? _timer;
    private string _phase = "open";
    private int _last = int.MinValue;

    public MenuZWatcher(HostContext ctx, string variant)
    {
        _ctx = ctx;
        _variant = variant;
    }

    public void Start(string phase)
    {
        lock (_gate)
        {
            _phase = phase;
            _last = int.MinValue;
            _timer?.Dispose();
            _timer = new Timer(_ => Tick(), null, 0, PeriodMs);
        }
    }

    /// <summary>Stops the timer after one last sample under <paramref name="phase"/>.</summary>
    public void Stop(string phase)
    {
        lock (_gate)
        {
            _timer?.Dispose();
            _timer = null;
            _phase = phase;
        }

        Tick();
    }

    public void Dispose() => Stop("after");

    private void Tick()
    {
        try
        {
            lock (_gate)
            {
                (int index, string _) = ZOrderProbe.Position(_ctx.Hwnd);
                if (index == _last)
                {
                    return;
                }

                _last = index;
                _ctx.Log.Write("host", _variant, string.Empty, "menu-zpos", string.Create(CultureInfo.InvariantCulture, $"index={index} phase={_phase}"));
            }
        }
        catch (ObjectDisposedException)
        {
            // The host is shutting down and the log is already closed.
        }
    }
}

/// <summary>
/// Variants M-A and M-B: a WPF <see cref="ContextMenu"/> opened on right-button-up inside the plate.
/// <list type="bullet">
/// <item><description>M-A: nothing special. The bar never activates, so WPF has to cope with a menu that belongs to a
/// window that is not the foreground window (the point of the Q3 comparison).</description></item>
/// <item><description>M-B: temporary activation (D-15). The foreground window is remembered, the bar is made the
/// foreground window for the menu's duration and the remembered window gets the foreground back on close.</description></item>
/// </list>
/// Both log <c>menu-open</c>, <c>menu-items</c>, <c>menu-item</c>, <c>menu-close</c> and <c>menu-zpos</c>.
/// <c>--selftest-menu true</c> (M-A only) opens the menu 500 ms after start at the plate centre and closes it 400 ms
/// later from code; it never activates anything. M-B refuses the self-test because it activates by design.
/// </summary>
[HostVariant("M-A", "M-B")]
internal sealed class MenuWpfVariant : IHostVariant
{
    private const int SelfTestOpenDelayMs = 500;
    private const int SelfTestOpenForMs = 400;
    private const int LateGeometryMs = 250;

    private HostContext? _ctx;
    private string _name = string.Empty;
    private bool _activates;
    private ContextMenu? _menu;
    private MenuZWatcher? _z;
    private DispatcherTimer? _selfTestTimer;
    private bool _itemChosen;
    private bool _closedByCode;
    private bool _escape;
    private (int X, int Y) _openedAt;
    private HWND _foregroundBefore;

    public void Attach(HostContext ctx, string name)
    {
        _ctx = ctx;
        _name = name;
        _activates = name.Equals("M-B", StringComparison.OrdinalIgnoreCase);
        _z = new MenuZWatcher(ctx, name);
        ctx.AddHook(Hook);
        Log("menu-ready", $"variant={name} activates={_activates}");

        if (!MenuCommon.IsOption(ctx, "selftest-menu"))
        {
            return;
        }

        if (_activates)
        {
            Log("menu-selftest-refused", "M-B activates the bar by design; the self-test never activates anything");
            return;
        }

        _selfTestTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(SelfTestOpenDelayMs) };
        _selfTestTimer.Tick += (_, _) =>
        {
            _selfTestTimer!.Stop();
            RECT plate = ctx.ScreenPlate;
            int x = (plate.left + plate.right) / 2;
            int y = (plate.top + plate.bottom) / 2;
            if (ctx.Options.TryGetValue("selftest-menu-at", out string? at) && at.Split(',') is [var px, var py] &&
                int.TryParse(px, NumberStyles.Integer, CultureInfo.InvariantCulture, out int atX) &&
                int.TryParse(py, NumberStyles.Integer, CultureInfo.InvariantCulture, out int atY))
            {
                (x, y) = (atX, atY);
            }

            OpenMenu(x, y, programmatic: true);

            var closeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(SelfTestOpenForMs) };
            closeTimer.Tick += (_, _) =>
            {
                closeTimer.Stop();
                if (_menu is { IsOpen: true })
                {
                    _closedByCode = true;
                    _menu.IsOpen = false;
                }
            };
            closeTimer.Start();
        };
        _selfTestTimer.Start();
    }

    public void Detach()
    {
        _selfTestTimer?.Stop();
        if (_menu is { IsOpen: true })
        {
            _closedByCode = true;
            _menu.IsOpen = false;
        }

        _z?.Dispose();
        _z = null;
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
            OpenMenu(x, y, programmatic: false);
        }
        else
        {
            Log("menu-ignored", string.Create(CultureInfo.InvariantCulture, $"{x},{y} outside the plate"));
        }

        return IntPtr.Zero;
    }

    private void OpenMenu(int x, int y, bool programmatic)
    {
        if (_ctx == null || _menu is { IsOpen: true })
        {
            return;
        }

        _itemChosen = false;
        _closedByCode = false;
        _escape = false;
        _openedAt = (x, y);

        if (_activates)
        {
            // D-15: temporary activation. Remember who had the foreground so that it can be handed back.
            _foregroundBefore = PInvoke.GetForegroundWindow();
            Log("fg-before", $"hwnd={MenuCommon.Hex(_foregroundBefore)}");
            bool ok = PInvoke.SetForegroundWindow(_ctx.Hwnd);
            Log("fg-bar-activated", $"ok={ok} foreground={MenuCommon.Hex(PInvoke.GetForegroundWindow())}");
        }

        _menu = BuildMenu();
        _menu.PlacementTarget = _ctx.Root;
        if (programmatic)
        {
            // Opening from code has no mouse message; place at the requested screen pixel, expressed as an offset from
            // the top-left of the placement target (Root fills the window, which starts at the window's screen origin).
            PInvoke.GetWindowRect(_ctx.Hwnd, out RECT window);
            double scale = (PresentationSource.FromVisual(_ctx.Window) as HwndSource)?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
            _menu.Placement = PlacementMode.Relative;
            _menu.HorizontalOffset = (x - window.left) / scale;
            _menu.VerticalOffset = (y - window.top) / scale;
        }
        else
        {
            _menu.Placement = PlacementMode.MousePoint;
        }

        _menu.IsOpen = true;
    }

    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu { StaysOpen = false };
        foreach ((int id, string text) in MenuCommon.Items)
        {
            int itemId = id;
            var item = new MenuItem { Header = text };
            item.Click += (_, _) =>
            {
                _itemChosen = true;
                Log("menu-item", $"id={itemId}");
                if (itemId == MenuCommon.LaunchItemId && _ctx != null)
                {
                    MenuCommon.LaunchWitness(_ctx, _name);
                }
            };
            menu.Items.Add(item);
        }

        menu.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                _escape = true;
            }
        };
        menu.Opened += (_, _) => OnOpened(menu);
        menu.Closed += (_, _) => OnClosed();
        return menu;
    }

    private void OnOpened(ContextMenu menu)
    {
        Log("menu-open", string.Create(CultureInfo.InvariantCulture, $"{_openedAt.X},{_openedAt.Y} variant={_name}"));
        _z?.Start("open");

        // The item rectangles are only valid once the popup has been laid out, and WPF may still move the popup after
        // that (placement is finished once the size is known), so the geometry is logged twice: right after layout and
        // 250 ms later. The Driver always uses the newest menu-items row.
        menu.Dispatcher.BeginInvoke(DispatcherPriority.Loaded, (Action)(() => LogGeometry(menu, "loaded")));
        var late = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(LateGeometryMs) };
        late.Tick += (_, _) =>
        {
            late.Stop();
            if (menu.IsOpen)
            {
                LogGeometry(menu, "late");
            }
        };
        late.Start();
    }

    /// <summary>
    /// Logs <c>menu-window</c> (the popup's Win32 rectangle) and <c>menu-items</c> (each item's screen rectangle,
    /// popup window origin plus the item's offset inside the popup).
    /// </summary>
    private void LogGeometry(ContextMenu menu, string phase)
    {
        MenuItem[] items = menu.Items.OfType<MenuItem>().ToArray();
        if (items.Length == 0 || PresentationSource.FromVisual(items[0]) is not HwndSource popup || popup.RootVisual is not Visual root)
        {
            return;
        }

        try
        {
            if (!PInvoke.GetWindowRect(new HWND(popup.Handle), out RECT window))
            {
                return;
            }

            double scale = popup.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
            var rects = new List<string>();
            foreach (MenuItem item in items)
            {
                Point offset = item.TransformToAncestor(root).Transform(new Point(0, 0));
                int left = window.left + (int)Math.Round(offset.X * scale);
                int top = window.top + (int)Math.Round(offset.Y * scale);
                rects.Add(string.Create(
                    CultureInfo.InvariantCulture,
                    $"{left},{top},{left + (int)Math.Round(item.ActualWidth * scale)},{top + (int)Math.Round(item.ActualHeight * scale)}"));
            }

            Log("menu-items", string.Join(';', rects));
            Log("menu-window", string.Create(CultureInfo.InvariantCulture, $"{window.left},{window.top},{window.right},{window.bottom} phase={phase}"));
        }
        catch (InvalidOperationException)
        {
            // closed again before it was laid out
        }
    }

    private void OnClosed()
    {
        string reason = _itemChosen ? "item" : _closedByCode ? "programmatic" : _escape ? "escape" : "dismissed";
        Log("menu-close", $"reason={reason}");
        _z?.Stop("after");

        if (_activates && _ctx != null && _foregroundBefore != HWND.Null)
        {
            bool ok = PInvoke.SetForegroundWindow(_foregroundBefore);
            Log("fg-restored", $"hwnd={MenuCommon.Hex(_foregroundBefore)} ok={ok}");
        }
    }

    private void Log(string evt, string detail) =>
        _ctx?.Log.Write("host", _name, string.Empty, evt, detail);
}
