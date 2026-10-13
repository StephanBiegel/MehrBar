using System.Diagnostics;
using System.Globalization;
using System.IO; // not an implicit using in WPF projects
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using MehrBar.Core.Layout;
using MehrBar.Spikes.Common;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace MehrBar.Spikes.WindowHost.Variants;

/// <summary>
/// Q4 render-cost workload (D-16/D-17): <c>count</c> icons from frozen 256 px Pbgra32 bitmaps laid out per frame by the
/// product zoom maths (MehrBar.Core ZoomEngine, anchor cursor): neighbours give way and the bar grows, so the per-frame
/// work matches the real bar (RESEARCH anti-pattern "rebuild the zoom maths in the spike host").
/// Options (all optional): count, base, pitch, zoom, radius, synthetic-cursor, synthetic-stop-ms, frames, scaling.
/// </summary>
internal sealed class RenderCostOptions
{
    public int Count { get; private init; } = 20;

    /// <summary>Resting icon size in pixels.</summary>
    public double Base { get; private init; } = 32;

    /// <summary>Distance between resting icon centres in pixels.</summary>
    public double Pitch { get; private init; } = 36;

    /// <summary>Icon size under the cursor in pixels.</summary>
    public double Zoom { get; private init; } = 121;

    /// <summary>Zoom radius in pitches.</summary>
    public double Radius { get; private init; } = 2.5;

    public bool Synthetic { get; private init; }

    /// <summary>With a synthetic cursor: ms after the loop started at which the cursor is parked outside the band; 0 = never.</summary>
    public int SyntheticStopMs { get; private init; }

    public string? FramesPath { get; private init; }

    public BitmapScalingMode Scaling { get; private init; } = BitmapScalingMode.HighQuality;

    /// <summary>Plan 01-19: <c>sampled</c> = ZoomEngine.Compute (Cursor), <c>smooth</c> = ZoomEngine.ComputeSmooth.</summary>
    public bool Smooth { get; private init; }

    /// <summary>Plan 01-19: round icon positions and sizes to whole pixels.</summary>
    public bool Snap { get; private init; }

    /// <summary>Plan 01-19: opaque black band behind the icons (window stays layered) so screen capture reads them over any wallpaper.</summary>
    public bool Backdrop { get; private init; }

    public string ScalingName => Scaling == BitmapScalingMode.HighQuality ? "hq" : "linear";

    public static RenderCostOptions Parse(IReadOnlyDictionary<string, string> o)
    {
        int count = Int(o, "count", 20);
        if (count <= 0 || count > 256)
        {
            throw new FormatException($"--count must be 1..256, not {count}.");
        }

        // Defaults follow the plan: 20 icons at 32 px / pitch 36, 64 icons at the smaller 24 px / pitch 28 (Q18).
        bool dense = count > 20;
        string scaling = o.TryGetValue("scaling", out string? s) ? s.ToLowerInvariant() : "hq";
        if (scaling is not ("hq" or "linear"))
        {
            throw new FormatException($"--scaling must be hq or linear, not '{scaling}'.");
        }

        return new RenderCostOptions
        {
            Count = count,
            Base = Dbl(o, "base", dense ? 24 : 32),
            Pitch = Dbl(o, "pitch", dense ? 28 : 36),
            Zoom = Dbl(o, "zoom", 121),
            Radius = Dbl(o, "radius", 2.5),
            Synthetic = o.TryGetValue("synthetic-cursor", out string? syn) &&
                        syn.Equals("true", StringComparison.OrdinalIgnoreCase),
            SyntheticStopMs = Int(o, "synthetic-stop-ms", 0),
            FramesPath = o.TryGetValue("frames", out string? frames) ? frames : null,
            Scaling = scaling == "hq" ? BitmapScalingMode.HighQuality : BitmapScalingMode.Linear,
            Smooth = o.TryGetValue("layout", out string? layout) && layout.Equals("smooth", StringComparison.OrdinalIgnoreCase),
            Snap = o.TryGetValue("snap", out string? snap) && snap.Equals("true", StringComparison.OrdinalIgnoreCase),
            Backdrop = o.TryGetValue("backdrop", out string? backdrop) && backdrop.Equals("true", StringComparison.OrdinalIgnoreCase),
        };
    }

    private static int Int(IReadOnlyDictionary<string, string> o, string key, int fallback) =>
        o.TryGetValue(key, out string? v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n)
            ? n
            : fallback;

    private static double Dbl(IReadOnlyDictionary<string, string> o, string key, double fallback) =>
        o.TryGetValue(key, out string? v) && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double d)
            ? d
            : fallback;
}

/// <summary>Icon rectangles per frame. All arrays are allocated once; <see cref="Update"/> never allocates.</summary>
/// <summary>
/// Product layout from <see cref="ZoomEngine"/> (anchor <see cref="ZoomAnchorMode.Cursor"/>): the icon under the cursor
/// stays put, the neighbours give way and the bar grows in width, so every icon outside the zoom radius moves each
/// frame as well. Slots are one pitch long; an icon fills <c>Base / Pitch</c> of its displayed slot, centred.
/// </summary>
internal sealed class ZoomLayout
{
    private readonly RenderCostOptions _options;
    private readonly RestItem[] _rest;
    private readonly ZoomParams _params;

    public ZoomLayout(RenderCostOptions options, double windowWidth, double topPadding)
    {
        _options = options;
        int n = options.Count;
        _rest = new RestItem[n];
        Left = new double[n];
        Top = new double[n];
        Size = new double[n];
        double start = (windowWidth - (n * options.Pitch)) / 2.0;
        for (int i = 0; i < n; i++)
        {
            _rest[i] = new RestItem(start + (i * options.Pitch), options.Pitch, Zooms: true);
            Top[i] = topPadding;
        }

        // IconSize = Base, so a slot grows by Pitch * (Zoom / Base - 1) * f and the icon under the cursor is Zoom px.
        _params = new ZoomParams(options.Base, options.Zoom, options.Radius);
        Update(double.NegativeInfinity);
    }

    public double[] Left { get; }

    public double[] Top { get; }

    public double[] Size { get; }

    public void Update(double cursorX)
    {
        // Far outside the bar ZoomEngine yields the rest layout; a finite value keeps its arithmetic finite.
        double pointer = double.IsFinite(cursorX) ? cursorX : -1e6;
        ZoomResult result = _options.Smooth
            ? ZoomEngine.ComputeSmooth(_rest, _options.Pitch, _params, pointer)
            : ZoomEngine.Compute(_rest, _options.Pitch, _params, pointer, ZoomAnchorMode.Cursor);
        double fill = _options.Base / _options.Pitch;
        for (int i = 0; i < Size.Length; i++)
        {
            double slot = result.DisplayedLengths[i];
            double size = slot * fill;
            double left = result.DisplayedStarts[i] + ((slot - size) / 2.0);
            Size[i] = _options.Snap ? Math.Round(size) : size;
            Left[i] = _options.Snap ? Math.Round(left) : left;
        }
    }
}

/// <summary>Something the render loop updates once per non-duplicate frame.</summary>
internal interface IRenderSurface
{
    /// <summary>True when the frame is only complete after <c>OnRender</c> ran (the log row is then written there).</summary>
    bool CompletesInOnRender { get; }

    void Apply(ZoomLayout layout);
}

/// <summary>
/// Drives and measures the frames. Subscribes to <see cref="CompositionTarget.Rendering"/> only while the cursor is
/// inside the window band, so an idle bar renders nothing (idle proof, T-01-16). Per frame it writes one row to the
/// frames CSV: qpc_ms (system-wide QueryPerformanceCounter in ms, comparable with the Driver), rendering_time_ms,
/// is_duplicate, handler_us, onrender_us, gc0..gc2 (cumulative) and alloc_bytes (delta of
/// GC.GetAllocatedBytesForCurrentThread since the previous row; allocation is accounted in allocation-context chunks,
/// so only means over many frames are meaningful).
/// </summary>
internal sealed class RenderLoop
{
    public const string FramesHeader = "qpc_ms,rendering_time_ms,is_duplicate,handler_us,onrender_us,gc0,gc1,gc2,alloc_bytes";

    private const double SweepAmplitudePitches = 3.5;
    private const double SweepPeriodMs = 2000.0;
    private const int PollMs = 100;

    private readonly RenderCostOptions _options;
    private readonly ZoomLayout _layout;
    private readonly IRenderSurface _surface;
    private readonly HWND _hwnd;
    private readonly EventHandler _handler;
    private readonly Stopwatch _clock = new();
    private readonly StringBuilder _rows = new(64 * 1024);
    private readonly double _ticksToMs = 1000.0 / Stopwatch.Frequency;

    private DispatcherTimer? _timer;
    private StreamWriter? _writer;
    private bool _subscribed;
    private int _left;
    private int _top;
    private int _right;
    private int _bottom;
    private double _lastRenderingMs = double.NaN;
    private long _lastAlloc;

    private bool _hasPending;
    private double _pendingRenderingMs;
    private bool _pendingDuplicate;
    private long _pendingHandlerUs;

    public RenderLoop(RenderCostOptions options, ZoomLayout layout, IRenderSurface surface, HWND hwnd)
    {
        _options = options;
        _layout = layout;
        _surface = surface;
        _hwnd = hwnd;
        _handler = OnRendering;
    }

    /// <summary>QueryPerformanceCounter time in ms (the clock the Driver uses too).</summary>
    public static double QpcMs() => Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency;

    public double Start()
    {
        PInvoke.GetWindowRect(_hwnd, out RECT rect);
        _left = rect.left;
        _top = rect.top;
        _right = rect.right;
        _bottom = rect.bottom;

        if (_options.FramesPath != null)
        {
            string full = Path.GetFullPath(_options.FramesPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            var stream = new FileStream(full, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            _writer = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\n" };
            _writer.WriteLine(FramesHeader);
            _writer.Flush();
        }

        _lastAlloc = GC.GetAllocatedBytesForCurrentThread();
        double origin = QpcMs();
        _clock.Restart();

        _timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(PollMs) };
        _timer.Tick += OnTimer;
        _timer.Start();
        EnsureSubscription();
        return origin;
    }

    public void Stop()
    {
        _timer?.Stop();
        if (_subscribed)
        {
            CompositionTarget.Rendering -= _handler;
            _subscribed = false;
        }

        Flush();
        _writer?.Dispose();
        _writer = null;
    }

    /// <summary>Called by <c>OnRender</c> of a surface that completes the frame there.</summary>
    public void CompleteRender(long onRenderTicks)
    {
        if (_hasPending)
        {
            Commit(TicksToUs(onRenderTicks));
        }
    }

    private static long TicksToUs(long ticks) => ticks * 1_000_000L / Stopwatch.Frequency;

    private void OnTimer(object? sender, EventArgs e)
    {
        EnsureSubscription();
        Flush();
    }

    private void EnsureSubscription()
    {
        bool inBand = ReadCursor(out _);
        if (inBand && !_subscribed)
        {
            CompositionTarget.Rendering += _handler;
            _subscribed = true;
        }
        else if (!inBand && _subscribed)
        {
            CompositionTarget.Rendering -= _handler;
            _subscribed = false;
        }
    }

    private void OnRendering(object? sender, EventArgs e)
    {
        long start = Stopwatch.GetTimestamp();
        if (_hasPending)
        {
            Commit(0); // a previous frame never reached OnRender
        }

        double renderingMs = ((RenderingEventArgs)e).RenderingTime.TotalMilliseconds;
        bool duplicate = renderingMs == _lastRenderingMs;
        _lastRenderingMs = renderingMs;

        bool inBand = true;
        if (!duplicate)
        {
            inBand = ReadCursor(out double cursorX);
            if (inBand)
            {
                _layout.Update(cursorX);
                _surface.Apply(_layout);
            }
        }

        _pendingRenderingMs = renderingMs;
        _pendingDuplicate = duplicate;
        _pendingHandlerUs = TicksToUs(Stopwatch.GetTimestamp() - start);
        _hasPending = true;
        if (duplicate || !inBand || !_surface.CompletesInOnRender)
        {
            Commit(0);
        }

        if (!inBand && _subscribed)
        {
            CompositionTarget.Rendering -= _handler;
            _subscribed = false;
        }
    }

    private void Commit(long onRenderUs)
    {
        _hasPending = false;
        if (_writer == null)
        {
            return;
        }

        long alloc = GC.GetAllocatedBytesForCurrentThread();
        long delta = alloc - _lastAlloc;
        _lastAlloc = alloc;
        double qpc = Stopwatch.GetTimestamp() * _ticksToMs;
        _rows.Append(
            CultureInfo.InvariantCulture,
            $"{qpc:F3},{_pendingRenderingMs:F3},{(_pendingDuplicate ? 1 : 0)},{_pendingHandlerUs},{onRenderUs},{GC.CollectionCount(0)},{GC.CollectionCount(1)},{GC.CollectionCount(2)},{delta}\n");
    }

    private void Flush()
    {
        if (_writer != null && _rows.Length > 0)
        {
            _writer.Write(_rows);
            _rows.Clear();
            _writer.Flush();
        }

        // The timer tick and the flush allocate; they are not part of any frame.
        _lastAlloc = GC.GetAllocatedBytesForCurrentThread();
    }

    /// <summary>Cursor x in window pixels and whether the cursor is inside the window band. Synthetic or real.</summary>
    private bool ReadCursor(out double xInWindow)
    {
        double sx;
        double sy;
        if (_options.Synthetic)
        {
            double t = _clock.Elapsed.TotalMilliseconds;
            if (_options.SyntheticStopMs > 0 && t >= _options.SyntheticStopMs)
            {
                xInWindow = 0;
                return false; // parked outside the band
            }

            double centre = (_left + _right) / 2.0;
            sx = centre + (SweepAmplitudePitches * _options.Pitch * Math.Sin(2.0 * Math.PI * t / SweepPeriodMs));
            sy = _top + SpikeGeometry.Plate.Y + (SpikeGeometry.Plate.Height / 2.0);
        }
        else
        {
            PInvoke.GetCursorPos(out var point);
            sx = point.X;
            sy = point.Y;
        }

        xInWindow = sx - _left;
        return sx >= _left && sx < _right && sy >= _top && sy < _bottom;
    }
}

/// <summary>R1: one element whose OnRender redraws every icon each frame (the straightforward WPF technique).</summary>
internal sealed class RenderSurface : FrameworkElement, IRenderSurface
{
    private readonly IReadOnlyList<BitmapSource> _icons;
    private ZoomLayout _layout;
    private RenderLoop? _loop;

    public RenderSurface(IReadOnlyList<BitmapSource> icons, ZoomLayout layout, BitmapScalingMode scaling, double width, double height)
    {
        _icons = icons;
        _layout = layout;
        Width = width;
        Height = height;
        IsHitTestVisible = false;
        RenderOptions.SetBitmapScalingMode(this, scaling);
    }

    public bool CompletesInOnRender => true;

    public void Bind(RenderLoop loop) => _loop = loop;

    public void Apply(ZoomLayout layout)
    {
        _layout = layout;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        long start = Stopwatch.GetTimestamp();
        ZoomLayout layout = _layout;
        for (int i = 0; i < _icons.Count; i++)
        {
            drawingContext.DrawImage(_icons[i], new Rect(layout.Left[i], layout.Top[i], layout.Size[i], layout.Size[i]));
        }

        _loop?.CompleteRender(Stopwatch.GetTimestamp() - start);
    }
}

/// <summary>
/// R2: one retained <see cref="DrawingVisual"/> per icon, drawn once at the base size from the 256 px source. Per frame
/// only each visual's transform (a reused ScaleTransform + TranslateTransform pair) changes, and only for icons whose
/// rectangle changed. With the cursor anchor the icons outside the zoom radius shift too, so this is nearly every icon.
/// </summary>
internal sealed class IconVisualHost : FrameworkElement, IRenderSurface
{
    private readonly VisualCollection _visuals;
    private readonly ScaleTransform[] _scales;
    private readonly TranslateTransform[] _translations;
    private readonly double[] _lastSize;
    private readonly double[] _lastLeft;
    private readonly double _baseSize;

    public IconVisualHost(
        IReadOnlyList<BitmapSource> icons, double baseSize, BitmapScalingMode scaling, double width, double height)
    {
        // Created first: setting IsHitTestVisible walks VisualChildrenCount, which reads this collection.
        _visuals = new VisualCollection(this);
        _baseSize = baseSize;
        Width = width;
        Height = height;
        IsHitTestVisible = false;
        RenderOptions.SetBitmapScalingMode(this, scaling);

        _scales = new ScaleTransform[icons.Count];
        _translations = new TranslateTransform[icons.Count];
        _lastSize = new double[icons.Count];
        _lastLeft = new double[icons.Count];
        Array.Fill(_lastSize, double.NaN);
        for (int i = 0; i < icons.Count; i++)
        {
            var visual = new DrawingVisual();
            RenderOptions.SetBitmapScalingMode(visual, scaling);
            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawImage(icons[i], new Rect(0, 0, baseSize, baseSize));
            }

            var scale = new ScaleTransform(1.0, 1.0);
            var translation = new TranslateTransform(0.0, 0.0);
            var group = new TransformGroup();
            group.Children.Add(scale);
            group.Children.Add(translation);
            visual.Transform = group;
            _scales[i] = scale;
            _translations[i] = translation;
            _visuals.Add(visual);
        }
    }

    public bool CompletesInOnRender => false;

    protected override int VisualChildrenCount => _visuals.Count;

    public void Apply(ZoomLayout layout)
    {
        for (int i = 0; i < _scales.Length; i++)
        {
            double size = layout.Size[i];
            double left = layout.Left[i];
            if (size == _lastSize[i] && left == _lastLeft[i])
            {
                continue;
            }

            _lastSize[i] = size;
            _lastLeft[i] = left;
            double scale = size / _baseSize;
            _scales[i].ScaleX = scale;
            _scales[i].ScaleY = scale;
            _translations[i].X = left;
            _translations[i].Y = layout.Top[i];
        }
    }

    protected override Visual GetVisualChild(int index) => _visuals[index];
}

[HostVariant("R1", "R2")]
internal sealed class RenderCostVariant : IHostVariant
{
    private HostContext? _ctx;
    private FrameworkElement? _element;
    private RenderLoop? _loop;

    public void Attach(HostContext ctx, string name)
    {
        _ctx = ctx;
        RenderCostOptions options = RenderCostOptions.Parse(ctx.Options);
        IReadOnlyList<BitmapSource> icons = SyntheticIcons.Create(options.Count);
        foreach (BitmapSource icon in icons)
        {
            // Frozen bitmaps are shared with the render thread without cross-thread copies; an unfrozen one would skew the cost.
            if (!icon.IsFrozen)
            {
                icon.Freeze();
            }
        }

        double width = ctx.Root.Width;
        double height = ctx.Root.Height;
        var layout = new ZoomLayout(options, width, ctx.Plate.Y);

        FrameworkElement element;
        IRenderSurface surface;
        RenderSurface? redrawSurface = null;
        if (string.Equals(name, "R2", StringComparison.OrdinalIgnoreCase))
        {
            var host = new IconVisualHost(icons, options.Base, options.Scaling, width, height);
            host.Apply(layout);
            element = host;
            surface = host;
        }
        else
        {
            redrawSurface = new RenderSurface(icons, layout, options.Scaling, width, height);
            element = redrawSurface;
            surface = redrawSurface;
        }

        if (options.Backdrop)
        {
            var band = new System.Windows.Shapes.Rectangle
            {
                Width = width,
                Height = ctx.Plate.Y + options.Base + 16,
                Fill = Brushes.Black,
                IsHitTestVisible = false,
            };
            Canvas.SetLeft(band, 0);
            Canvas.SetTop(band, 0);
            ctx.Root.Children.Add(band);
        }

        _element = element;
        var loop = new RenderLoop(options, layout, surface, ctx.Hwnd);
        redrawSurface?.Bind(loop);
        _loop = loop;
        Canvas.SetLeft(element, 0);
        Canvas.SetTop(element, 0);
        ctx.Root.Children.Add(element);

        ctx.Log.Write(
            "host",
            name,
            string.Empty,
            "render-config",
            string.Create(
                CultureInfo.InvariantCulture,
                $"surface={name} count={options.Count} base={options.Base} pitch={options.Pitch} zoom={options.Zoom} radius={options.Radius} scaling={options.ScalingName} layout={(options.Smooth ? "smooth" : "sampled")} snap={options.Snap} synthetic={options.Synthetic} transparent={ctx.Window.AllowsTransparency} frames={(options.FramesPath != null)}"));

        double origin = loop.Start();
        if (options.Synthetic)
        {
            // Shared QPC clock: lets the Driver place its measurement phases exactly on the synthetic timeline.
            ctx.Log.Write("host", name, string.Empty, "synthetic-origin", string.Create(CultureInfo.InvariantCulture, $"qpc_ms={origin:F3}"));
        }
    }

    public void Detach()
    {
        _loop?.Stop();
        if (_ctx != null && _element != null)
        {
            _ctx.Root.Children.Remove(_element);
        }

        _element = null;
    }
}
