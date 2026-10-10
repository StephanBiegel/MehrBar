using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using MehrBar.Spikes.Common;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MehrBar.Spikes.WindowHost.Variants;

/// <summary>
/// The two hit-area techniques that compete with the alpha-1 plate of H1 (D-14). Both draw the same plate
/// and (unless <c>--sentinel off</c>) the same sentinel squares as H1, so the Driver measures them with the
/// same click plan.
/// <list type="bullet">
/// <item><description>H2: the window region is cut to the plate with SetWindowRgn, so clicks outside the region
/// reach the window below. <c>--region-churn true</c> additionally re-applies a slightly changing region on every
/// WPF frame for five seconds and logs <c>region-cost us=&lt;n&gt;</c> per call, which is the price of following a
/// zoomed outline each frame.</description></item>
/// <item><description>H3: a timer polls the cursor every <c>--poll-ms</c> (default 16) milliseconds and sets
/// WS_EX_TRANSPARENT while the cursor is outside the plate and removes it while it is inside. Every change is
/// logged as <c>h3-transparent</c> (detail <c>on</c>/<c>off</c> plus the cursor), so the Driver can compute the
/// entry latency from the host clock.</description></item>
/// </list>
/// </summary>
[HostVariant("H2", "H3")]
internal sealed class HitRegionVariant : IHostVariant
{
    private const int ChurnDurationMs = 5000;
    private const int ChurnSpreadPx = 8;
    private const int DefaultPollMs = 16;

    private static readonly int s_transparent = (int)WINDOW_EX_STYLE.WS_EX_TRANSPARENT;
    private static readonly int s_layered = (int)WINDOW_EX_STYLE.WS_EX_LAYERED;

    private IReadOnlyList<UIElement> _elements = Array.Empty<UIElement>();
    private HostContext? _ctx;
    private string _name = string.Empty;

    // H2 region churn
    private Stopwatch? _churnClock;
    private bool _churning;
    private long _lastRenderingTicks = -1;
    private int _churnCalls;

    // H3 transparent toggle
    private DispatcherTimer? _timer;
    private bool? _transparent;

    public void Attach(HostContext ctx, string name)
    {
        _ctx = ctx;
        _name = name;
        _elements = PlateDrawing.Add(ctx);

        if (name.Equals("H2", StringComparison.OrdinalIgnoreCase))
        {
            AttachRegion(ctx);
        }
        else
        {
            AttachTransparentToggle(ctx);
        }
    }

    public void Detach()
    {
        if (_ctx == null)
        {
            return;
        }

        StopChurn(logDone: false);

        if (_timer != null)
        {
            _timer.Stop();
            _timer = null;
            SetTransparent(_ctx, false);
        }

        if (_name.Equals("H2", StringComparison.OrdinalIgnoreCase))
        {
            PInvoke.SetWindowRgn(_ctx.Hwnd, HRGN.Null, true);
        }

        foreach (UIElement element in _elements)
        {
            _ctx.Root.Children.Remove(element);
        }

        _elements = Array.Empty<UIElement>();
    }

    // ---- H2: SetWindowRgn ------------------------------------------------------------------------------

    private void AttachRegion(HostContext ctx)
    {
        double costUs = ApplyRegion(ctx, 0, out int result);

        // Host-side check of what the system now holds as the window region (window coordinates).
        HRGN probe = PInvoke.CreateRectRgn(0, 0, 0, 0);
        GDI_REGION_TYPE type = PInvoke.GetWindowRgn(ctx.Hwnd, probe);
        PInvoke.GetRgnBox(probe, out RECT box);
        PInvoke.DeleteObject(probe);

        ctx.Log.Write(
            "host",
            _name,
            string.Empty,
            "region",
            string.Create(
                CultureInfo.InvariantCulture,
                $"result={result} type={type} left={box.left} top={box.top} right={box.right} bottom={box.bottom} cost-us={costUs:F1}"));

        bool churn = ctx.Options.TryGetValue("region-churn", out string? value) &&
                     string.Equals(value, "true", StringComparison.OrdinalIgnoreCase);
        if (churn)
        {
            _churnClock = Stopwatch.StartNew();
            _churning = true;
            CompositionTarget.Rendering += OnChurnRendering;
        }
    }

    /// <summary>
    /// Replaces the window region with the plate rectangle widened by <paramref name="spread"/> pixels on both
    /// sides. Returns the time the two calls took in microseconds. The system owns the region after a successful
    /// SetWindowRgn; a failed call leaves it with us, so it is deleted here.
    /// </summary>
    private static double ApplyRegion(HostContext ctx, int spread, out int result)
    {
        Rect plate = ctx.Plate;
        long start = Stopwatch.GetTimestamp();
        HRGN region = PInvoke.CreateRectRgn(
            (int)plate.Left - spread,
            (int)plate.Top,
            (int)plate.Right + spread,
            (int)plate.Bottom);
        result = PInvoke.SetWindowRgn(ctx.Hwnd, region, true);
        long end = Stopwatch.GetTimestamp();
        if (result == 0)
        {
            PInvoke.DeleteObject(region);
        }

        return (end - start) * 1_000_000.0 / Stopwatch.Frequency;
    }

    private void OnChurnRendering(object? sender, EventArgs e)
    {
        if (_ctx == null || !_churning || _churnClock == null)
        {
            return;
        }

        // WPF may raise Rendering more than once for one frame; one region change per frame is what is measured.
        if (e is RenderingEventArgs args)
        {
            long ticks = args.RenderingTime.Ticks;
            if (ticks == _lastRenderingTicks)
            {
                return;
            }

            _lastRenderingTicks = ticks;
        }

        if (_churnClock.ElapsedMilliseconds >= ChurnDurationMs)
        {
            StopChurn(logDone: true);
            return;
        }

        int spread = _churnCalls % (ChurnSpreadPx + 1);
        double costUs = ApplyRegion(_ctx, spread, out int result);
        _churnCalls++;
        _ctx.Log.Write(
            "host",
            _name,
            string.Empty,
            "region-cost",
            string.Create(CultureInfo.InvariantCulture, $"us={costUs:F1}"));
        if (result == 0)
        {
            _ctx.Log.Write("host", _name, string.Empty, "region-error", "SetWindowRgn failed during churn");
        }
    }

    private void StopChurn(bool logDone)
    {
        if (!_churning || _ctx == null)
        {
            return;
        }

        _churning = false;
        CompositionTarget.Rendering -= OnChurnRendering;

        // Back to the plain plate region, so that the hit area is the same as before the churn.
        ApplyRegion(_ctx, 0, out _);
        if (logDone)
        {
            _ctx.Log.Write(
                "host",
                _name,
                string.Empty,
                "region-churn-done",
                string.Create(CultureInfo.InvariantCulture, $"calls={_churnCalls}"));
        }
    }

    // ---- H3: WS_EX_TRANSPARENT toggled by a cursor poll ------------------------------------------------

    private void AttachTransparentToggle(HostContext ctx)
    {
        int pollMs = DefaultPollMs;
        if (ctx.Options.TryGetValue("poll-ms", out string? value) &&
            (!int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out pollMs) || pollMs < 1))
        {
            pollMs = DefaultPollMs;
        }

        _timer = new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromMilliseconds(pollMs) };
        _timer.Tick += (_, _) => Poll();
        ctx.Log.Write(
            "host",
            _name,
            string.Empty,
            "h3-poll",
            string.Create(CultureInfo.InvariantCulture, $"poll-ms={pollMs}"));

        // Establish and log the initial state before the first tick.
        Poll();
        _timer.Start();
    }

    private void Poll()
    {
        if (_ctx == null || !PInvoke.GetCursorPos(out var cursor))
        {
            return;
        }

        RECT plate = _ctx.ScreenPlate;
        bool inside = cursor.X >= plate.left && cursor.X < plate.right &&
                      cursor.Y >= plate.top && cursor.Y < plate.bottom;
        bool wantTransparent = !inside;
        if (_transparent == wantTransparent)
        {
            return;
        }

        _transparent = wantTransparent;
        SetTransparent(_ctx, wantTransparent);
        _ctx.Log.Write(
            "host",
            _name,
            string.Empty,
            "h3-transparent",
            string.Create(CultureInfo.InvariantCulture, $"{(wantTransparent ? "on" : "off")} cursor={cursor.X}:{cursor.Y}"));
    }

    private static void SetTransparent(HostContext ctx, bool on)
    {
        int exStyle = PInvoke.GetWindowLong(ctx.Hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        exStyle = on ? exStyle | s_transparent | s_layered : exStyle & ~s_transparent;
        PInvoke.SetWindowLong(ctx.Hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE, exStyle);
    }
}
