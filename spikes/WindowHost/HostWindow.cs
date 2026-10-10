using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using MehrBar.Spikes.Common;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MehrBar.Spikes.WindowHost;

/// <summary>Thread-safe counters for the messages the Driver oracles care about.</summary>
internal sealed class MessageCounters
{
    private long _move;
    private long _posChanging;
    private long _lButtonDown;
    private long _rButtonDown;
    private long _rButtonUp;
    private long _mouseActivate;

    public long Move => Interlocked.Read(ref _move);

    public long PosChanging => Interlocked.Read(ref _posChanging);

    public long LButtonDown => Interlocked.Read(ref _lButtonDown);

    public long RButtonDown => Interlocked.Read(ref _rButtonDown);

    public long RButtonUp => Interlocked.Read(ref _rButtonUp);

    public long MouseActivate => Interlocked.Read(ref _mouseActivate);

    /// <summary>Counts a message; returns false for messages that are not tracked.</summary>
    public bool Count(uint msg)
    {
        switch (msg)
        {
            case PInvoke.WM_MOUSEMOVE:
                Interlocked.Increment(ref _move);
                return true;
            case PInvoke.WM_WINDOWPOSCHANGING:
                Interlocked.Increment(ref _posChanging);
                return true;
            case PInvoke.WM_LBUTTONDOWN:
                Interlocked.Increment(ref _lButtonDown);
                return true;
            case PInvoke.WM_RBUTTONDOWN:
                Interlocked.Increment(ref _rButtonDown);
                return true;
            case PInvoke.WM_RBUTTONUP:
                Interlocked.Increment(ref _rButtonUp);
                return true;
            case PInvoke.WM_MOUSEACTIVATE:
                Interlocked.Increment(ref _mouseActivate);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Aggregate row detail; the aggregated rows only change when move or poschanging change.</summary>
    public string Snapshot() => string.Create(
        CultureInfo.InvariantCulture,
        $"move={Move} poschanging={PosChanging} lbuttondown={LButtonDown} rbuttondown={RButtonDown} rbuttonup={RButtonUp} mouseactivate={MouseActivate}");
}

/// <summary>
/// The bar under test: a transparent window that sits at HWND_BOTTOM, never takes activation and
/// logs everything the Driver oracles need (message counters, self-check, z-order at the first frame).
/// Styles and z-order are set in <see cref="OnSourceInitialized"/>, i.e. after the HWND exists but
/// before the first pixel is drawn (RESEARCH Pattern 1, Pitfall 5).
/// </summary>
internal sealed unsafe class HostWindow : Window
{
    private readonly HostOptions _options;
    private readonly RunLog _log;
    private readonly IReadOnlyList<(string Name, IHostVariant Variant)> _variants;
    private readonly MessageCounters _counters = new();
    private readonly Canvas _root = new();
    private readonly string _variantLabel;
    private HwndSource? _source;
    private DispatcherTimer? _counterTimer;
    private string _lastAggregate = string.Empty;
    private bool _firstFrameLogged;
    private bool _pinDuringShow;

    public HostWindow(HostOptions options, RunLog log, IReadOnlyList<(string Name, IHostVariant Variant)> variants)
    {
        _options = options;
        _log = log;
        _variants = variants;
        _variantLabel = string.Join('+', variants.Select(v => v.Name));

        WindowStyle = WindowStyle.None;
        AllowsTransparency = options.Transparent;
        Background = options.Transparent
            ? Brushes.Transparent
            : new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20));
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = false;
        ResizeMode = ResizeMode.NoResize;
        Title = options.Title;
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;

        // Provisional size (100 % scaling); corrected to exact pixels once the DPI is known.
        Width = options.Width;
        Height = options.Height;

        _root.Width = options.Width;
        _root.Height = options.Height;
        Content = _root;
    }

    public HWND Hwnd { get; private set; }

    public MessageCounters Counters => _counters;

    public void AddHook(HwndSourceHook hook) => _source?.AddHook(hook);

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        _source = (HwndSource)PresentationSource.FromVisual(this)!;
        Hwnd = new HWND(_source.Handle);

        // Window size in device-independent units that equals the requested pixel size.
        double scale = _source.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        Width = _options.Width / scale;
        Height = _options.Height / scale;
        _root.LayoutTransform = new ScaleTransform(1.0 / scale, 1.0 / scale);

        // Styles first, then the z-order, both before anything is drawn.
        int exStyle = PInvoke.GetWindowLong(Hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        WINDOW_EX_STYLE wanted = WINDOW_EX_STYLE.WS_EX_TOOLWINDOW | WINDOW_EX_STYLE.WS_EX_NOACTIVATE;
        PInvoke.SetWindowLong(Hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, exStyle | (int)wanted);

        // Pitfall 5: WPF's own Show() raises the window above its siblings, which the first-frame row
        // proved on build 26200. Default (--show-mode swp): show the window ourselves, directly at
        // HWND_BOTTOM, and rewrite WPF's later initial-show call (see PinBottomDuringInitialShow).
        // --show-mode wpf keeps the plain WPF behaviour as the baseline for the Q1 note.
        _pinDuringShow = !string.Equals(
            _options.All.TryGetValue("show-mode", out string? mode) ? mode : "swp", "wpf", StringComparison.OrdinalIgnoreCase);
        SET_WINDOW_POS_FLAGS flags = SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE;
        if (_pinDuringShow)
        {
            flags |= SET_WINDOW_POS_FLAGS.SWP_SHOWWINDOW;
        }

        PInvoke.SetWindowPos(Hwnd, HWND.HWND_BOTTOM, _options.X, _options.Y, _options.Width, _options.Height, flags);

        _source.AddHook(WndProc);

        var windowPlate = SpikeGeometry.Plate;
        var plate = new Rect(windowPlate.X, windowPlate.Y, windowPlate.Width, windowPlate.Height);
        var screenPlate = new RECT
        {
            left = _options.X + windowPlate.X,
            top = _options.Y + windowPlate.Y,
            right = _options.X + windowPlate.Right,
            bottom = _options.Y + windowPlate.Bottom,
        };
        var context = new HostContext(this, Hwnd, _options.All, _log, plate, screenPlate, _root, _counters);
        foreach ((string name, IHostVariant variant) in _variants)
        {
            variant.Attach(context, name);
        }

        LogSelfCheck();

        // Logged once with the first frame that carries content, then unsubscribed.
        CompositionTarget.Rendering += OnFirstRendering;

        _counterTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(250) };
        _counterTimer.Tick += (_, _) => LogAggregate();
        _counterTimer.Start();
    }

    protected override void OnClosed(EventArgs e)
    {
        _counterTimer?.Stop();
        foreach ((string _, IHostVariant variant) in _variants)
        {
            variant.Detach();
        }

        base.OnClosed(e);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        uint message = (uint)msg;
        bool tracked = _counters.Count(message);
        if (!tracked)
        {
            return IntPtr.Zero;
        }

        switch (message)
        {
            case PInvoke.WM_WINDOWPOSCHANGING:
                PinBottomDuringInitialShow((WINDOWPOS*)lParam);
                break;
            case PInvoke.WM_LBUTTONDOWN:
                LogAtMessagePosition("lbuttondown");
                break;
            case PInvoke.WM_RBUTTONDOWN:
                LogAtMessagePosition("rbuttondown");
                break;
            case PInvoke.WM_RBUTTONUP:
                LogAtMessagePosition("rbuttonup");
                break;
            case PInvoke.WM_MOUSEACTIVATE:
                LogAtMessagePosition("mouseactivate");
                handled = true;
                return (IntPtr)PInvoke.MA_NOACTIVATE;
        }

        return IntPtr.Zero;
    }

    /// <summary>
    /// WPF's own Show() issues SetWindowPos(SWP_SHOWWINDOW) with a hwndInsertAfter that lifts the bar above
    /// every other window (measured on build 26200: z-index 21, Chrome below the bar). Until the first frame
    /// has been logged this rewrites such calls to HWND_BOTTOM; later z-order changes are left alone so that
    /// the Q2 guard variants see the unmodified system behaviour.
    /// </summary>
    private void PinBottomDuringInitialShow(WINDOWPOS* position)
    {
        if (!_pinDuringShow || _firstFrameLogged || position->flags.HasFlag(SET_WINDOW_POS_FLAGS.SWP_NOZORDER))
        {
            return;
        }

        if (position->hwndInsertAfter != HWND.HWND_BOTTOM)
        {
            position->hwndInsertAfter = HWND.HWND_BOTTOM;
            _log.Write("host", _variantLabel, string.Empty, "pinned", "initial show rewritten to HWND_BOTTOM");
        }
    }

    private void LogAtMessagePosition(string evt)
    {
        uint packed = PInvoke.GetMessagePos();
        int x = (short)(packed & 0xFFFF);
        int y = (short)((packed >> 16) & 0xFFFF);
        _log.Write("host", _variantLabel, string.Empty, evt, string.Create(CultureInfo.InvariantCulture, $"{x},{y}"));
    }

    private void LogAggregate()
    {
        string aggregate = string.Create(
            CultureInfo.InvariantCulture,
            $"move={_counters.Move} poschanging={_counters.PosChanging}");
        if (aggregate == _lastAggregate)
        {
            return;
        }

        _lastAggregate = aggregate;
        _log.Write("host", _variantLabel, string.Empty, "counters", aggregate);
    }

    private void LogSelfCheck()
    {
        int exStyle = PInvoke.GetWindowLong(Hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        var style = (WINDOW_EX_STYLE)(uint)exStyle;
        var flags = new StringBuilder();
        if (style.HasFlag(WINDOW_EX_STYLE.WS_EX_TOOLWINDOW))
        {
            flags.Append("TOOLWINDOW");
        }

        if (style.HasFlag(WINDOW_EX_STYLE.WS_EX_NOACTIVATE))
        {
            flags.Append(flags.Length > 0 ? "|" : string.Empty).Append("NOACTIVATE");
        }

        if (style.HasFlag(WINDOW_EX_STYLE.WS_EX_LAYERED))
        {
            flags.Append(flags.Length > 0 ? "|" : string.Empty).Append("LAYERED");
        }

        string below = ZOrderProbe.FirstBlockingWindowBelow(Hwnd);
        _log.Write(
            "host",
            _variantLabel,
            string.Empty,
            "self-check",
            string.Create(
                CultureInfo.InvariantCulture,
                $"exstyle=0x{(uint)exStyle:X8} flags={flags} bottom={(below == ZOrderProbe.None ? "true" : "false")} below={below}"));
    }

    private void OnFirstRendering(object? sender, EventArgs e)
    {
        if (_firstFrameLogged)
        {
            return;
        }

        _firstFrameLogged = true;
        CompositionTarget.Rendering -= OnFirstRendering;

        (int index, string next) = ZOrderProbe.Position(Hwnd);
        string blocking = ZOrderProbe.FirstBlockingWindowBelow(Hwnd);
        _log.Write(
            "host",
            _variantLabel,
            string.Empty,
            "first-frame",
            string.Create(
                CultureInfo.InvariantCulture,
                $"zindex={index} below={next} bottom={(blocking == ZOrderProbe.None ? "true" : "false")} blocking={blocking}"));
    }
}

/// <summary>Z-order helpers for the self-check and the first-frame row.</summary>
internal static unsafe class ZOrderProbe
{
    public const string None = "none";

    private const string ProgmanClass = "Progman";
    private const string WorkerWClass = "WorkerW";

    [ThreadStatic]
    private static List<HWND>? s_collected;

    public static string ClassName(HWND hwnd)
    {
        const int capacity = 256;
        char* buffer = stackalloc char[capacity];
        int length = PInvoke.GetClassName(hwnd, buffer, capacity);
        return length > 0 ? new string(buffer, 0, length) : string.Empty;
    }

    public static bool IsShellDesktop(string className) =>
        className == ProgmanClass || className == WorkerWClass;

    /// <summary>
    /// Class of the first window below <paramref name="self"/> that is visible, not cloaked and not
    /// the shell desktop (Progman/WorkerW); "none" when only the desktop lies below the bar.
    /// </summary>
    public static string FirstBlockingWindowBelow(HWND self)
    {
        for (HWND next = PInvoke.GetWindow(self, GET_WINDOW_CMD.GW_HWNDNEXT);
             next != HWND.Null;
             next = PInvoke.GetWindow(next, GET_WINDOW_CMD.GW_HWNDNEXT))
        {
            if (!PInvoke.IsWindowVisible(next) || IsCloaked(next))
            {
                continue;
            }

            string className = ClassName(next);
            if (!IsShellDesktop(className))
            {
                return className.Length > 0 ? className : "?";
            }
        }

        return None;
    }

    /// <summary>Position of <paramref name="self"/> in EnumWindows order and the class of the next window.</summary>
    public static (int Index, string Next) Position(HWND self)
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

        int index = windows.IndexOf(self);
        if (index < 0)
        {
            return (-1, None);
        }

        string next = index + 1 < windows.Count ? ClassName(windows[index + 1]) : None;
        return (index, next.Length > 0 ? next : "?");
    }

    [UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvStdcall) })]
    private static BOOL CollectWindow(HWND hwnd, LPARAM lParam)
    {
        s_collected?.Add(hwnd);
        return true;
    }

    private static bool IsCloaked(HWND hwnd)
    {
        uint cloaked = 0;
        HRESULT hr = PInvoke.DwmGetWindowAttribute(hwnd, DWMWINDOWATTRIBUTE.DWMWA_CLOAKED, &cloaked, sizeof(uint));
        return hr.Succeeded && cloaked != 0;
    }
}
