using System.Diagnostics;
using System.Globalization;
using System.Text;
using MehrBar.Core.Layout;
using MehrBar.Spikes.Common;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MehrBar.Spikes.CompositionHost;

/// <summary>
/// Plan 01-19 task 3: the Q4 render workload drawn without WPF, on a DirectComposition visual whose content is a
/// flip-model swap chain with premultiplied alpha, drawn with Direct2D and paced by the swap chain's frame-latency
/// waitable object. Speaks the WindowHost protocol the Driver's render case expects (READY line on stdout, host CSV with
/// render-config / render-mode rows, frames CSV with the RenderLoop columns), so the same Driver cell and the same
/// screen capture measure it. Arguments not listed here are accepted and ignored.
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows8.1")]
internal static unsafe class Program
{
    private const string Variant = "DC";

    [STAThread]
    private static int Main(string[] args)
    {
        Options options;
        try
        {
            options = Options.Parse(args);
        }
        catch (FormatException ex)
        {
            Console.Error.WriteLine(ex.Message);
            return 2;
        }

        using var log = new RunLog(options.LogPath ?? Path.Combine(RepoPaths.DryResults, $"comp-{DateTime.Now:yyyyMMdd-HHmmss}.csv"));
        try
        {
            using var window = new BarWindow(options.Width, options.Height);
            using var renderer = new CompositionRenderer(window.Hwnd, options);
            var layout = new BarLayout(options, options.Width, SpikeGeometry.Plate.Y);
            log.Write("host", Variant, string.Empty, "render-config", string.Create(
                CultureInfo.InvariantCulture,
                $"surface=DC count={options.Count} base={options.Base} pitch={options.Pitch} zoom={options.Zoom} radius={options.Radius} layout={(options.Smooth ? "smooth" : "sampled")} snap={options.Snap} backdrop={options.Backdrop} frames={(options.FramesPath != null)}"));
            log.Write("host", Variant, string.Empty, "render-mode", "effective=dcomp tier=n/a");

            renderer.Draw(layout); // rest layout, so READY means "content on screen"
            Console.WriteLine(string.Create(
                CultureInfo.InvariantCulture, $"READY hwnd=0x{(nint)window.Hwnd.Value:X} pid={Environment.ProcessId} role=bar"));
            Console.Out.Flush();

            var loop = new FrameLoop(options, window, renderer, layout);
            loop.Run();
            log.Write("host", Variant, string.Empty, "exit", string.Create(CultureInfo.InvariantCulture, $"frames={loop.FrameCount}"));
            return 0;
        }
        catch (Exception ex)
        {
            log.Write("host", Variant, string.Empty, "error", ex.GetType().Name + ": " + ex.Message);
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}

/// <summary>Command line of the composition host (same keys as the WindowHost render variant).</summary>
internal sealed class Options
{
    public int Count { get; private init; } = 20;

    public double Base { get; private init; } = 32;

    public double Pitch { get; private init; } = 36;

    public double Zoom { get; private init; } = 121;

    public double Radius { get; private init; } = 2.5;

    public bool Smooth { get; private init; }

    public bool Snap { get; private init; }

    public bool Backdrop { get; private init; }

    public string? FramesPath { get; private init; }

    public string? LogPath { get; private init; }

    public int ExitAfterMs { get; private init; }

    public int Width { get; private init; } = SpikeGeometry.WindowWidth;

    public int Height { get; private init; } = SpikeGeometry.WindowHeight;

    public static Options Parse(string[] args)
    {
        var o = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal))
            {
                throw new FormatException($"unexpected argument '{args[i]}'");
            }

            string key = args[i][2..];
            o[key] = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal) ? args[++i] : "true";
        }

        int count = Int(o, "count", 20);
        bool dense = count > 20;
        int width = SpikeGeometry.WindowWidth;
        int height = SpikeGeometry.WindowHeight;
        if (o.TryGetValue("size", out string? size))
        {
            string[] parts = size.Split('x');
            width = int.Parse(parts[0], CultureInfo.InvariantCulture);
            height = int.Parse(parts[1], CultureInfo.InvariantCulture);
        }

        return new Options
        {
            Count = count,
            Base = Dbl(o, "base", dense ? 24 : 32),
            Pitch = Dbl(o, "pitch", dense ? 28 : 36),
            Zoom = Dbl(o, "zoom", 121),
            Radius = Dbl(o, "radius", 2.5),
            Smooth = o.TryGetValue("layout", out string? layout) && layout.Equals("smooth", StringComparison.OrdinalIgnoreCase),
            Snap = Bool(o, "snap"),
            Backdrop = Bool(o, "backdrop"),
            FramesPath = o.TryGetValue("frames", out string? frames) ? frames : null,
            LogPath = o.TryGetValue("log", out string? logPath) ? logPath : null,
            ExitAfterMs = Int(o, "exit-after-ms", 0),
            Width = width,
            Height = height,
        };
    }

    private static bool Bool(Dictionary<string, string> o, string key) =>
        o.TryGetValue(key, out string? v) && v.Equals("true", StringComparison.OrdinalIgnoreCase);

    private static int Int(Dictionary<string, string> o, string key, int fallback) =>
        o.TryGetValue(key, out string? v) && int.TryParse(v, NumberStyles.Integer, CultureInfo.InvariantCulture, out int n) ? n : fallback;

    private static double Dbl(Dictionary<string, string> o, string key, double fallback) =>
        o.TryGetValue(key, out string? v) && double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out double d) ? d : fallback;
}

/// <summary>Icon rectangles per frame from the product zoom maths; the same layout as the WindowHost ZoomLayout.</summary>
internal sealed class BarLayout
{
    private readonly Options _options;
    private readonly RestItem[] _rest;
    private readonly ZoomParams _params;

    public BarLayout(Options options, double windowWidth, double topPadding)
    {
        _options = options;
        int n = options.Count;
        _rest = new RestItem[n];
        Left = new double[n];
        Size = new double[n];
        Top = topPadding;
        double start = (windowWidth - (n * options.Pitch)) / 2.0;
        for (int i = 0; i < n; i++)
        {
            _rest[i] = new RestItem(start + (i * options.Pitch), options.Pitch, Zooms: true);
        }

        _params = new ZoomParams(options.Base, options.Zoom, options.Radius);
        Update(double.NegativeInfinity);
    }

    public double[] Left { get; }

    public double[] Size { get; }

    public double Top { get; }

    public void Update(double cursorX)
    {
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

/// <summary>
/// Frame loop: while the cursor is inside the window band, wait on the swap chain's frame-latency object, read the
/// cursor, lay out, draw and present (vsync). Outside the band it draws the rest layout once and then only polls the
/// cursor every 100 ms, so an idle bar presents nothing. Frames CSV columns as the WindowHost RenderLoop writes them.
/// </summary>
[System.Runtime.Versioning.SupportedOSPlatform("windows8.1")]
internal sealed class FrameLoop
{
    private const string FramesHeader = "qpc_ms,rendering_time_ms,is_duplicate,handler_us,onrender_us,gc0,gc1,gc2,alloc_bytes";
    private const uint PollMs = 100;

    private readonly Options _options;
    private readonly BarWindow _window;
    private readonly CompositionRenderer _renderer;
    private readonly BarLayout _layout;
    private readonly StringBuilder _rows = new(64 * 1024);
    private readonly double _ticksToMs = 1000.0 / Stopwatch.Frequency;

    public FrameLoop(Options options, BarWindow window, CompositionRenderer renderer, BarLayout layout)
    {
        _options = options;
        _window = window;
        _renderer = renderer;
        _layout = layout;
    }

    public int FrameCount { get; private set; }

    public void Run()
    {
        StreamWriter? writer = null;
        if (_options.FramesPath != null)
        {
            string full = Path.GetFullPath(_options.FramesPath);
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            var stream = new FileStream(full, FileMode.Create, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete);
            writer = new StreamWriter(stream, new UTF8Encoding(false)) { NewLine = "\n" };
            writer.WriteLine(FramesHeader);
            writer.Flush();
        }

        PInvoke.GetWindowRect(_window.Hwnd, out RECT rect);
        var clock = Stopwatch.StartNew();
        long lastFlush = 0;
        long lastAlloc = GC.GetAllocatedBytesForCurrentThread();
        bool atRest = true;
        try
        {
            while (!_window.Closed && (_options.ExitAfterMs <= 0 || clock.ElapsedMilliseconds < _options.ExitAfterMs))
            {
                if (clock.ElapsedMilliseconds - lastFlush >= PollMs)
                {
                    Flush(writer);
                    lastFlush = clock.ElapsedMilliseconds;
                    lastAlloc = GC.GetAllocatedBytesForCurrentThread();
                }

                bool inBand = InBand(rect, out _);
                if (!inBand && atRest)
                {
                    _window.WaitForMessages(PollMs);
                    continue;
                }

                if (!_renderer.WaitForFrame(PollMs))
                {
                    _window.Pump();
                    continue;
                }

                _window.Pump();
                long start = Stopwatch.GetTimestamp();
                inBand = InBand(rect, out double cursorX);
                _layout.Update(inBand ? cursorX - rect.left : double.NegativeInfinity);
                _renderer.Draw(_layout);
                atRest = !inBand;
                long end = Stopwatch.GetTimestamp();
                FrameCount++;
                long alloc = GC.GetAllocatedBytesForCurrentThread();
                _rows.Append(
                    CultureInfo.InvariantCulture,
                    $"{end * _ticksToMs:F3},{start * _ticksToMs:F3},0,{(end - start) * 1_000_000L / Stopwatch.Frequency},0,{GC.CollectionCount(0)},{GC.CollectionCount(1)},{GC.CollectionCount(2)},{alloc - lastAlloc}\n");
                lastAlloc = alloc;
            }
        }
        finally
        {
            Flush(writer);
            writer?.Dispose();
        }
    }

    private static bool InBand(RECT rect, out double cursorX)
    {
        PInvoke.GetCursorPos(out System.Drawing.Point point);
        cursorX = point.X;
        return point.X >= rect.left && point.X < rect.right && point.Y >= rect.top && point.Y < rect.bottom;
    }

    private void Flush(StreamWriter? writer)
    {
        if (writer != null && _rows.Length > 0)
        {
            writer.Write(_rows);
            _rows.Clear();
            writer.Flush();
        }
    }
}
