using System.Diagnostics;
using System.Globalization;
using System.Text;
using MehrBar.Spikes.Common;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MehrBar.Spikes.Driver.Cases;

/// <summary>
/// Reads the five sentinel probes with one BitBlt of the pixel row they share (all probes lie on the same row of the bar
/// window). GetPixel costs one display refresh per call (measured on this machine: 12 five-probe samples per second), a
/// single small BitBlt also waits for one refresh but delivers all five probes of the same instant, which gives 60 samples
/// per second on a 60 Hz display. Instances belong to the thread that created them.
/// </summary>
internal sealed unsafe class ProbeStrip : IDisposable
{
    private readonly IReadOnlyList<(int X, int Y)> _points;
    private readonly int _x0;
    private readonly int _y;
    private readonly int _width;
    private readonly byte[] _pixels;
    private HDC _screen;
    private HDC _memory;
    private HBITMAP _bitmap;
    private HGDIOBJ _previous;

    /// <param name="points">The five probe points of a bar (screen pixels); all on one row.</param>
    public ProbeStrip(IReadOnlyList<(int X, int Y)> points)
    {
        _points = points;
        _y = points[0].Y;
        _x0 = points.Min(p => p.X);
        _width = points.Max(p => p.X) - _x0 + 1;
        _pixels = new byte[_width * 4];
        _screen = PInvoke.GetDC(HWND.Null);
        _memory = PInvoke.CreateCompatibleDC(_screen);
        _bitmap = PInvoke.CreateCompatibleBitmap(_screen, _width, 1);
        _previous = PInvoke.SelectObject(_memory, _bitmap);
    }

    /// <summary>Colours (0xRRGGBB) at the probe points; <see cref="PixelProbe.Invalid"/> for every point when the copy failed.</summary>
    public int[] Sample()
    {
        var result = new int[_points.Count];
        Array.Fill(result, PixelProbe.Invalid);
        if (_screen.IsNull || _memory.IsNull || _bitmap.IsNull)
        {
            return result;
        }

        if (!PInvoke.BitBlt(_memory, 0, 0, _width, 1, _screen, _x0, _y, ROP_CODE.SRCCOPY))
        {
            return result;
        }

        var info = new BITMAPINFO();
        info.bmiHeader.biSize = (uint)sizeof(BITMAPINFOHEADER);
        info.bmiHeader.biWidth = _width;
        info.bmiHeader.biHeight = -1; // top-down
        info.bmiHeader.biPlanes = 1;
        info.bmiHeader.biBitCount = 32;
        int lines;
        fixed (byte* bits = _pixels)
        {
            lines = PInvoke.GetDIBits(_memory, _bitmap, 0, 1, bits, &info, DIB_USAGE.DIB_RGB_COLORS);
        }

        if (lines != 1)
        {
            return result;
        }

        for (int i = 0; i < _points.Count; i++)
        {
            int offset = (_points[i].X - _x0) * 4;
            result[i] = (_pixels[offset + 2] << 16) | (_pixels[offset + 1] << 8) | _pixels[offset]; // BGRA -> 0xRRGGBB
        }

        return result;
    }

    public void Dispose()
    {
        if (!_memory.IsNull)
        {
            PInvoke.SelectObject(_memory, _previous);
            PInvoke.DeleteDC(_memory);
            _memory = default;
        }

        if (!_bitmap.IsNull)
        {
            PInvoke.DeleteObject(_bitmap);
            _bitmap = default;
        }

        if (!_screen.IsNull)
        {
            PInvoke.ReleaseDC(HWND.Null, _screen);
            _screen = default;
        }
    }
}

/// <summary>Result of the gap sampler: how long the bar was gone after the trigger and whether it came back by itself.</summary>
/// <param name="Samples">Number of samples taken.</param>
/// <param name="GapStartMs">Time of the first sample without the sentinel, from the start of sampling; -1 when there was no gap.</param>
/// <param name="GapEndMs">Time of the last sample without the sentinel; -1 when there was no gap.</param>
/// <param name="GapTotalMs">Estimated time without the sentinel (first to last gap sample plus one sample interval).</param>
/// <param name="Returned">The sentinel was visible again at the end of the window (or never left).</param>
/// <param name="HiddenSamples">Samples without the sentinel.</param>
/// <param name="MaxIntervalMs">Largest distance between two samples (quality of the cadence).</param>
internal sealed record GapResult(
    int Samples, long GapStartMs, long GapEndMs, long GapTotalMs, bool Returned, int HiddenSamples, long MaxIntervalMs);

/// <summary>
/// Samples probes 1, 2 and 5 (the ones the witness does not cover) back to back on a background thread. The bar counts as
/// visible in a sample when at least two of the three probes show the sentinel colour (D-11: the "gap" is the time
/// without it). The plan asked for a 10 ms cadence; reading the screen waits for the display refresh, so the real cadence
/// is one refresh period (16.7 ms at 60 Hz) and the result reports the largest interval it saw.
/// </summary>
internal sealed class GapSampler
{
    public const int NominalPeriodMs = 10;
    private const int ReturnConfirmSamples = 3;

    private readonly IReadOnlyList<(int X, int Y)> _points;
    private readonly int _durationMs;
    private readonly List<(long Ms, bool Visible)> _samples = [];
    private readonly Thread _thread;
    private volatile bool _stop;

    private GapSampler(IReadOnlyList<(int X, int Y)> points, int durationMs)
    {
        _points = points;
        _durationMs = durationMs;
        _thread = new Thread(Loop) { IsBackground = true, Name = "ZOrderGapSampler", Priority = ThreadPriority.AboveNormal };
    }

    /// <summary>Starts sampling the probes of a bar placed at <paramref name="barOrigin"/>.</summary>
    public static GapSampler Start((int X, int Y) barOrigin, int durationMs)
    {
        var sampler = new GapSampler(PixelProbe.ProbePoints(barOrigin), durationMs);
        sampler._thread.Start();
        return sampler;
    }

    /// <summary>Waits for the end of the window and evaluates it.</summary>
    public GapResult Complete()
    {
        _thread.Join();
        lock (_samples)
        {
            return Analyse(_samples.ToArray());
        }
    }

    public void Stop() => _stop = true;

    /// <summary>
    /// First to last sample without the sentinel is the gap; the bar "returned" when at least three samples after the
    /// last gap sample show it again. Pure function, checked by the dry run.
    /// </summary>
    public static GapResult Analyse(IReadOnlyList<(long Ms, bool Visible)> samples)
    {
        int first = -1;
        int last = -1;
        int hidden = 0;
        long maxInterval = 0;
        var intervals = new List<long>();
        for (int i = 0; i < samples.Count; i++)
        {
            if (i > 0)
            {
                long interval = samples[i].Ms - samples[i - 1].Ms;
                intervals.Add(interval);
                maxInterval = Math.Max(maxInterval, interval);
            }

            if (samples[i].Visible)
            {
                continue;
            }

            hidden++;
            if (first < 0)
            {
                first = i;
            }

            last = i;
        }

        if (first < 0)
        {
            return new GapResult(samples.Count, -1, -1, 0, true, 0, maxInterval);
        }

        // One typical interval is added so that a single missing sample counts as one sample of gap, not as zero.
        long typical = intervals.Count == 0 ? NominalPeriodMs : intervals.OrderBy(v => v).ElementAt(intervals.Count / 2);
        long start = samples[first].Ms;
        long end = samples[last].Ms;
        bool returned = samples.Count - 1 - last >= ReturnConfirmSamples;
        return new GapResult(samples.Count, start, end, end - start + typical, returned, hidden, maxInterval);
    }

    private void Loop()
    {
        try
        {
            using var strip = new ProbeStrip(_points);
            var clock = Stopwatch.StartNew();
            while (!_stop && clock.ElapsedMilliseconds < _durationMs)
            {
                long ms = clock.ElapsedMilliseconds;
                int[] values = strip.Sample();
                bool visible = new[] { 0, 1, 4 }.Count(i => PixelProbe.Matches(values[i], SpikeGeometry.SentinelRgb)) >= 2;
                lock (_samples)
                {
                    _samples.Add((ms, visible));
                }
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            Console.Error.WriteLine($"gap sampler stopped: {ex.GetType().Name}: {ex.Message}");
        }
    }
}

/// <summary>Result of the cover test (D-11: the bar must not jump in front of other windows).</summary>
/// <param name="Samples">Samples taken while the witness was visible and not minimized.</param>
/// <param name="Covered">Samples that show the witness colour at probe 3 (the expected picture).</param>
/// <param name="InFront">Samples that show the sentinel instead: the bar was in front of the witness.</param>
/// <param name="InFrontMs">Time spent in front (sum of the distances to the next sample).</param>
/// <param name="ZConfirmed">In-front samples for which the window order also put the bar above the witness.</param>
/// <param name="Skipped">Samples dropped because the witness was not (yet) restored.</param>
internal sealed record CoverResult(int Samples, int Covered, int InFront, long InFrontMs, int ZConfirmed, int Skipped);

/// <summary>
/// Cover test (RESEARCH Pattern 3): samples probe 3, which only the witness covers, back to back. A sample that shows
/// the sentinel while the witness is visible counts as "bar in front"; one sample is one display refresh.
/// </summary>
internal static class CoverTest
{
    public static CoverResult Run(HWND bar, HWND witness, (int X, int Y) barOrigin, int durationMs)
    {
        using var strip = new ProbeStrip(PixelProbe.ProbePoints(barOrigin));
        var clock = Stopwatch.StartNew();
        int samples = 0;
        int covered = 0;
        int inFront = 0;
        int zConfirmed = 0;
        int skipped = 0;
        long inFrontMs = 0;
        long previousMs = 0;
        bool previousInFront = false;

        while (clock.ElapsedMilliseconds < durationMs)
        {
            Safety.ThrowIfAborted();
            long ms = clock.ElapsedMilliseconds;
            if (previousInFront)
            {
                inFrontMs += ms - previousMs;
            }

            previousMs = ms;
            previousInFront = false;

            // The witness must be restored (visible, not minimized) for a sample to mean anything.
            bool restored = PInvoke.IsWindowVisible(witness) && !PInvoke.IsIconic(witness);
            int value = strip.Sample()[2];
            if (!restored)
            {
                skipped++;
                continue;
            }

            samples++;
            if (PixelProbe.Matches(value, SpikeGeometry.SentinelRgb))
            {
                inFront++;
                previousInFront = true;
                if (IsBelow(bar, witness))
                {
                    zConfirmed++;
                }
            }
            else if (PixelProbe.Matches(value, SpikeGeometry.CoverRgb))
            {
                covered++;
            }
        }

        return new CoverResult(samples, covered, inFront, inFrontMs, zConfirmed, skipped);
    }

    /// <summary>True when <paramref name="window"/> lies below <paramref name="bar"/> in the window order.</summary>
    private static bool IsBelow(HWND bar, HWND window)
    {
        for (HWND next = PInvoke.GetWindow(bar, GET_WINDOW_CMD.GW_HWNDNEXT);
             next != HWND.Null;
             next = PInvoke.GetWindow(next, GET_WINDOW_CMD.GW_HWNDNEXT))
        {
            if (next == window)
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>Everything a trigger may use. Created per run; the notes end up in the result row.</summary>
internal sealed class ZContext
{
    public ZContext(
        CaseContext caseContext, SafetySession? safety, HostProcess bar, HostProcess witness, string guard, RunLog events,
        bool allowExplorer, bool allowDisplay, bool allowLock)
    {
        Case = caseContext;
        Safety = safety;
        Bar = bar;
        Witness = witness;
        Guard = guard;
        Events = events;
        AllowExplorer = allowExplorer;
        AllowDisplay = allowDisplay;
        AllowLock = allowLock;
    }

    public CaseContext Case { get; }

    public SafetySession? Safety { get; }

    public HostProcess Bar { get; }

    public HostProcess Witness { get; }

    public string Guard { get; }

    public RunLog Events { get; }

    public bool AllowExplorer { get; }

    public bool AllowDisplay { get; }

    public bool AllowLock { get; }

    /// <summary>Name of the trigger that is currently running (set by the case).</summary>
    public string TriggerName { get; set; } = string.Empty;

    /// <summary>Short remarks for the Bemerkung column of the result.</summary>
    public List<string> Notes { get; } = [];

    /// <summary>Writes an event row and, when <paramref name="note"/> is true, also keeps it for the result row.</summary>
    public void Note(string evt, string detail, bool note = true)
    {
        Events.Write("zorder", TriggerName, Guard, evt, detail);
        Console.WriteLine($"  {evt}: {detail}");
        if (note)
        {
            Notes.Add($"{evt} {detail}");
        }
    }
}

/// <summary>One way of making the desktop appear, minimize windows or rebuild the shell (the D-10 matrix).</summary>
internal interface IZTrigger
{
    string Name { get; }

    /// <summary>The <c>--allow-*</c> flag the trigger needs, or null.</summary>
    string? RequiredFlag { get; }

    /// <summary>True when the developer acts during the run (manual keys, unlocking after lock): the run then does not abort on real input.</summary>
    bool HumanParticipates { get; }

    /// <summary>True when sampling starts only after <see cref="Fire"/> returned (lock: after Stephan unlocked).</summary>
    bool SamplerStartsAfterFire { get; }

    /// <summary>Length of the gap sampling window in milliseconds (at least 3000 is used).</summary>
    int SampleMs { get; }

    void Fire(ZContext ctx);

    void Undo(ZContext ctx);
}

/// <summary>Row of the per-run result CSV.</summary>
internal sealed record ZRunResult(
    string Guard,
    string Trigger,
    int Rep,
    long GapStartMs,
    long GapEndMs,
    long GapTotalMs,
    bool Returned,
    int InFrontSamples,
    long InFrontMs,
    string DesktopHostClass,
    string GuardTransitions,
    string Notes);

/// <summary>
/// Q2 (SC2): does the bar stay at the bottom of the z-order and come back by itself after Win+D and its relatives
/// (D-10, D-11, D-12)? Per guard x trigger x repetition the case starts a bar with the guard and a partial witness
/// (covers probes 3 and 4), fires the trigger and measures (a) the gap at probes 1, 2 and 5 every 10 ms for 3 s and
/// (b) after the trigger's undo the time the bar sits in front of the witness (cover test, probe 3).
/// <c>--dry-run</c> sends no input at all and fires only the trigger <c>none</c>; real runs are reserved for plan 01-14.
/// </summary>
[DriverCase("zorder")]
public sealed class ZOrderCase : IDriverCase
{
    private const int CoverMs = 3000;
    private const int MinSampleMs = 3000;
    private const int SettleMs = 500;
    private static readonly int[] s_snapshotTimesMs = [0, 500, 1000, 2000];
    private static readonly string[] s_guards = ["S0", "S1", "S2"];

    private const string Usage =
        "usage: Driver zorder --guard S0|S1|S2[,..] --trigger <name>[,..]|all|none|manual --repeat N " +
        "[--allow-display] [--allow-explorer] [--allow-lock] [--seconds N (manual)] [--dry-run] [--out csv] [--summary-md md] | " +
        "Driver zorder --list-triggers";

    public int Run(CaseContext ctx)
    {
        if (ctx.GetBool("list-triggers"))
        {
            foreach (string name in ZTriggers.Names)
            {
                Console.WriteLine(name);
            }

            return ExitCodes.Ok;
        }

        string[] guards = Split(ctx.Get("guard") ?? "S1").Select(g => g.ToUpperInvariant()).ToArray();
        int repeat = ctx.GetInt("repeat", 1);
        if (guards.Length == 0 || repeat < 1 || guards.Any(g => !s_guards.Contains(g, StringComparer.Ordinal)))
        {
            Console.Error.WriteLine(Usage);
            return ExitCodes.Usage;
        }

        bool allowExplorer = ctx.GetBool("allow-explorer");
        bool allowDisplay = ctx.GetBool("allow-display");
        bool allowLock = ctx.GetBool("allow-lock");
        int manualSeconds = ctx.GetInt("seconds", 60);

        if (!ZTriggers.TryResolve(ctx.Get("trigger") ?? "none", allowExplorer, allowDisplay, allowLock, manualSeconds, out IReadOnlyList<IZTrigger> triggers, out string? error))
        {
            Console.Error.WriteLine(error);
            return ExitCodes.Usage;
        }

        if (triggers.Any(t => t.Name == "manual") && triggers.Count > 1)
        {
            Console.Error.WriteLine("trigger 'manual' must be used alone (the real keys are pressed by hand, nothing is injected)");
            return ExitCodes.Usage;
        }

        string resultPath = ctx.OutPath ?? Path.Combine(RepoPaths.DryResults, string.Create(CultureInfo.InvariantCulture, $"zorder-{DateTime.Now:yyyyMMdd-HHmmss}.csv"));
        string eventsPath = Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(resultPath))!,
            Path.GetFileNameWithoutExtension(resultPath) + "-events.csv");

        var results = new List<ZRunResult>();
        int exit;
        using (var events = new RunLog(eventsPath))
        {
            exit = ctx.DryRun
                ? RunDry(ctx, guards, triggers, events, results)
                : RunReal(ctx, guards, triggers, repeat, events, results, allowExplorer, allowDisplay, allowLock);
        }

        WriteResultCsv(resultPath, results);
        Console.WriteLine($"result: {Path.GetFullPath(resultPath)}");
        Console.WriteLine($"events: {Path.GetFullPath(eventsPath)}");
        if (ctx.SummaryMd != null)
        {
            Q2Summary.Write(ctx.SummaryMd, results, ctx.DryRun);
            Console.WriteLine($"summary: {Path.GetFullPath(ctx.SummaryMd)}");
        }

        return exit;
    }

    // ---- dry run -----------------------------------------------------------------------------------------

    private static int RunDry(
        CaseContext ctx, IReadOnlyList<string> guards, IReadOnlyList<IZTrigger> requested, RunLog events, List<ZRunResult> results)
    {
        if (requested.Any(t => t.Name != "none"))
        {
            Console.WriteLine("dry run: no input is sent, only the trigger 'none' is used");
        }

        int failures = 0;
        foreach (string guard in guards)
        {
            ForegroundInfo foregroundBefore = Foreground.Take();
            using HostProcess bar = ctx.StartHost(HostArgs(guard));
            using HostProcess witness = ctx.StartWitness("partial", activate: false);

            HostRow? start = bar.WaitForRow("guard-start", 3000);
            HostRow? host = bar.WaitForRow("desktop-host", 3000);
            bool startOk = start != null && start.Detail.Contains($"strategy={guard}", StringComparison.Ordinal);
            string hostClass = host == null ? "missing" : ClassOfHostRow(host);
            bool hostOk = hostClass is "Progman" or "WorkerW";

            // Driver-side view of the same facts, read only.
            DesktopHostInfo desktop = DesktopHost.Find();
            bool aboveBar = DesktopHost.IsAbove(bar.Hwnd);
            (_, (int X, int Y) origin) = HitCase.BarGeometry(bar);
            int[] probes = PixelProbe.Sample(PixelProbe.ProbePoints(origin));
            int[] stripProbes;
            using (var strip = new ProbeStrip(PixelProbe.ProbePoints(origin)))
            {
                stripProbes = strip.Sample();
            }

            bool stripAgrees = probes.SequenceEqual(stripProbes);

            // Exercise the sampler and the cover test for a moment; nothing is asserted about the pixels
            // (the developer's own windows may cover the top area).
            GapResult gap = GapSampler.Start(origin, 300).Complete();
            CoverResult cover = CoverTest.Run(bar.Hwnd, witness.Hwnd, origin, 100);
            bool samplerOk = gap.Samples >= 5 && stripProbes.All(v => v != PixelProbe.Invalid);
            bool analyseOk = AnalyseSelfTest();

            IReadOnlyList<ZEntry> snapshot = ZOrder.Snapshot(bar.Hwnd);
            ZEntry? barEntry = snapshot.FirstOrDefault(e => e.IsBar);
            ZEntry? hostEntry = snapshot.FirstOrDefault(e => e.IsDesktopHost);
            string snapshotPath = WriteRedactedSnapshot(snapshot, guard, "none", 1, 0, dry: true, resultDirectory: null);
            bool snapshotOk = barEntry != null && hostEntry != null && File.Exists(snapshotPath);

            ForegroundInfo foregroundAfter = Foreground.Take();
            bool foregroundSame = foregroundAfter.Hwnd == foregroundBefore.Hwnd;

            events.Write("zorder", "none", guard, "dry-sample", string.Create(
                CultureInfo.InvariantCulture,
                $"guard-start={startOk} desktop-host={hostClass} driver-host={desktop.ClassName} host-above-bar={aboveBar} probes={string.Join('/', probes.Select(PixelProbe.ToHex))} strip-agrees={stripAgrees} gap-samples={gap.Samples} cover-samples={cover.Samples} cover-skipped={cover.Skipped} bar-index={barEntry?.Index.ToString(CultureInfo.InvariantCulture) ?? "missing"} foreground-unchanged={foregroundSame}"));
            Console.WriteLine(string.Create(
                CultureInfo.InvariantCulture,
                $"{guard}: guard-start={startOk}, desktop host {hostClass} (driver sees {desktop.ClassName}), host above bar={aboveBar}, sampler {gap.Samples} samples (max interval {gap.MaxIntervalMs} ms), cover test {cover.Samples} samples + {cover.Skipped} skipped, foreground unchanged={foregroundSame}"));

            results.Add(new ZRunResult(guard, "none", 1, -1, -1, 0, true, 0, 0, hostClass, "-", "Trockenlauf, nichts gemessen"));

            if (startOk && hostOk && samplerOk && analyseOk && snapshotOk)
            {
                Console.WriteLine($"ZORDER DRY OK {guard}");
            }
            else
            {
                failures++;
                Console.WriteLine(
                    $"ZORDER DRY FAILED {guard} (guard-start={startOk} desktop-host-row={hostOk} sampler={samplerOk} analyse={analyseOk} snapshot={snapshotOk})");
            }
        }

        return failures == 0 ? ExitCodes.Ok : ExitCodes.CriteriaFailed;
    }

    /// <summary>Checks <see cref="GapSampler.Analyse"/> against a hand-made sequence (gap from 30 to 60 ms, back afterwards).</summary>
    private static bool AnalyseSelfTest()
    {
        var samples = new List<(long Ms, bool Visible)>();
        for (int i = 0; i < 20; i++)
        {
            samples.Add((i * 10L, i < 3 || i > 6));
        }

        GapResult gap = GapSampler.Analyse(samples);
        GapResult none = GapSampler.Analyse(samples.Where(s => s.Visible).ToArray());
        var stuck = new List<(long Ms, bool Visible)> { (0, true), (10, false), (20, false) };
        GapResult never = GapSampler.Analyse(stuck);
        return gap is { GapStartMs: 30, GapEndMs: 60, GapTotalMs: 40, Returned: true, HiddenSamples: 4 }
               && none is { GapStartMs: -1, GapTotalMs: 0, Returned: true }
               && never is { Returned: false, GapStartMs: 10 };
    }

    // ---- real run ----------------------------------------------------------------------------------------

    private static int RunReal(
        CaseContext ctx,
        IReadOnlyList<string> guards,
        IReadOnlyList<IZTrigger> triggers,
        int repeat,
        RunLog events,
        List<ZRunResult> results,
        bool allowExplorer,
        bool allowDisplay,
        bool allowLock)
    {
        int runs = guards.Count * triggers.Count * repeat;
        long perRunMs = 5000 + CoverMs + MinSampleMs + (2 * SettleMs) + triggers.Max(t => Math.Max(t.SampleMs, MinSampleMs));
        long estimateMs = runs * perRunMs;
        TimeSpan timeout = TimeSpan.FromMilliseconds(Math.Max(TimeSpan.FromMinutes(10).TotalMilliseconds, estimateMs + 180_000));
        bool manual = triggers.Any(t => t.Name == "manual");

        // Manual keys and unlocking are real input; every other run aborts as soon as the developer touches keyboard or mouse.
        using SafetySession safety = ctx.BeginSafety(timeout, watchRealInput: !triggers.Any(t => t.HumanParticipates));
        if (!manual)
        {
            // Registered before ClearTopArea: restore actions run newest first, so this one runs after the
            // Win+Shift+M of ClearTopArea and catches whatever that left minimized (Win+M twice overwrites the
            // shell's single undo list).
            IReadOnlyList<HWND> before = WindowRestore.CaptureVisibleWindows();
            safety.OnRestore("restore windows that were visible before the run", () => WindowRestore.RestoreIfMinimized(before));
            DesktopPrep.ClearTopArea(safety);
        }

        foreach (string guard in guards)
        {
            foreach (IZTrigger trigger in triggers)
            {
                for (int rep = 1; rep <= repeat; rep++)
                {
                    Console.WriteLine($"run guard={guard} trigger={trigger.Name} rep={rep}/{repeat}");
                    ZRunResult result = RunOne(ctx, safety, guard, trigger, rep, events, allowExplorer, allowDisplay, allowLock);
                    results.Add(result);
                    Console.WriteLine(string.Create(
                        CultureInfo.InvariantCulture,
                        $"  gap {result.GapTotalMs} ms (returned={result.Returned}), in front of witness {result.InFrontSamples} samples / {result.InFrontMs} ms"));
                }
            }
        }

        Console.WriteLine("ZORDER DONE");
        return ExitCodes.Ok;
    }

    private static ZRunResult RunOne(
        CaseContext ctx,
        SafetySession safety,
        string guard,
        IZTrigger trigger,
        int rep,
        RunLog events,
        bool allowExplorer,
        bool allowDisplay,
        bool allowLock)
    {
        bool manual = trigger.Name == "manual";
        using HostProcess bar = ctx.StartHost(HostArgs(guard));
        using HostProcess witness = ctx.StartWitness("partial", activate: !manual);
        var z = new ZContext(ctx, safety, bar, witness, guard, events, allowExplorer, allowDisplay, allowLock) { TriggerName = trigger.Name };

        if (!manual)
        {
            DesktopPrep.BringToForeground(witness.Hwnd);
        }

        Safety.Wait(SettleMs);
        (_, (int X, int Y) origin) = HitCase.BarGeometry(bar);
        string hostClass = DesktopHost.Find().ClassName;

        // Baseline: the picture the measurement assumes (sentinel at probes 1, 2, 5, witness colour at 3).
        IReadOnlyList<(int X, int Y)> points = PixelProbe.ProbePoints(origin);
        int[] baseline = PixelProbe.Sample(points);
        int sentinelVisible = new[] { 0, 1, 4 }.Count(i => PixelProbe.Matches(baseline[i], SpikeGeometry.SentinelRgb));
        bool coverVisible = PixelProbe.Matches(baseline[2], SpikeGeometry.CoverRgb);
        if (sentinelVisible < 2 || (!manual && !coverVisible))
        {
            z.Note("baseline", $"unexpected picture before the trigger: sentinel {sentinelVisible}/3, witness colour at probe 3 = {coverVisible}", note: true);
        }

        bar.ReadNewRows();
        DateTime hostStart = ProcessStart(bar);

        int windowMs = Math.Max(MinSampleMs, trigger.SampleMs);
        GapSampler? sampler = null;
        var snapshots = new List<Task>();
        DateTime fireWall = DateTime.Now;
        bool fired = false;
        try
        {
            if (!trigger.SamplerStartsAfterFire)
            {
                sampler = GapSampler.Start(origin, windowMs);
                snapshots = ScheduleSnapshots(bar, guard, trigger.Name, rep, windowMs, resultDirectory: ResultDirectory(ctx));
            }

            fired = true;
            trigger.Fire(z);

            if (trigger.SamplerStartsAfterFire)
            {
                fireWall = DateTime.Now;
                sampler = GapSampler.Start(origin, windowMs);
                snapshots = ScheduleSnapshots(bar, guard, trigger.Name, rep, windowMs, resultDirectory: ResultDirectory(ctx));
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            z.Note("fire-failed", $"{ex.GetType().Name}: {ex.Message}");
            sampler?.Stop();
        }

        GapResult gap = sampler?.Complete() ?? new GapResult(0, -1, -1, 0, false, 0, 0);
        Task.WaitAll(snapshots.ToArray(), TimeSpan.FromSeconds(10));

        if (fired)
        {
            try
            {
                trigger.Undo(z);
            }
            catch (OperationCanceledException)
            {
                // The abort path restores through the session; nothing more to do here.
                throw;
            }
            catch (Exception ex)
            {
                z.Note("undo-failed", $"{ex.GetType().Name}: {ex.Message}");
            }
        }

        CoverResult cover = manual
            ? new CoverResult(0, 0, 0, 0, 0, 0)
            : CoverTest.Run(bar.Hwnd, witness.Hwnd, origin, CoverMs);
        Safety.Wait(SettleMs);

        if (!manual)
        {
            if (cover.Covered == 0)
            {
                z.Notes.Add("cover-invalid (the witness colour was never seen at probe 3)");
            }

            if (cover.InFront > 0)
            {
                z.Notes.Add(string.Create(CultureInfo.InvariantCulture, $"vor Zeuge {cover.InFront} Abtastwerte, davon {cover.ZConfirmed} durch die Fensterreihenfolge bestätigt"));
            }
        }

        z.Notes.Add(string.Create(CultureInfo.InvariantCulture, $"{gap.Samples} Abtastwerte, größter Abstand {gap.MaxIntervalMs} ms"));

        (string transitions, string winEvents) = SummariseHostRows(bar, hostStart, fireWall);
        if (winEvents.Length > 0)
        {
            z.Notes.Add($"WinEvents {winEvents}");
        }

        string notes = string.Join("; ", z.Notes);
        events.Write("zorder", trigger.Name, guard, "run", string.Create(
            CultureInfo.InvariantCulture,
            $"rep={rep} gap-ms={gap.GapTotalMs} returned={gap.Returned} in-front={cover.InFront} in-front-ms={cover.InFrontMs} transitions={transitions}"));

        return new ZRunResult(
            guard, trigger.Name, rep, gap.GapStartMs, gap.GapEndMs, gap.GapTotalMs, gap.Returned,
            cover.InFront, cover.InFrontMs, hostClass, transitions, notes);
    }

    // ---- helpers -----------------------------------------------------------------------------------------

    internal static string HostArgs(string guard) => $"--variant H1,{guard} --log-events true";

    private static IEnumerable<string> Split(string text) =>
        text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string ClassOfHostRow(HostRow row)
    {
        const string marker = "class=";
        int at = row.Detail.IndexOf(marker, StringComparison.Ordinal);
        if (at < 0)
        {
            return "unknown";
        }

        string rest = row.Detail[(at + marker.Length)..];
        int space = rest.IndexOf(' ', StringComparison.Ordinal);
        return space < 0 ? rest : rest[..space];
    }

    private static DateTime ProcessStart(HostProcess host)
    {
        try
        {
            using Process process = Process.GetProcessById(host.Pid);
            return process.StartTime;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or ArgumentException)
        {
            return DateTime.MinValue;
        }
    }

    /// <summary>
    /// Guard transitions and WinEvent counts from the host CSV, relative to the moment the trigger fired
    /// (host row time = process start time + ts_ms, the same clock the host log uses).
    /// </summary>
    private static (string Transitions, string WinEvents) SummariseHostRows(HostProcess bar, DateTime hostStart, DateTime fireWall)
    {
        if (hostStart == DateTime.MinValue)
        {
            return ("-", string.Empty);
        }

        var transitions = new List<string>();
        var counts = new SortedDictionary<string, int>(StringComparer.Ordinal);
        foreach (HostRow row in bar.ReadAllRows())
        {
            double relMs = (hostStart.AddMilliseconds(row.TsMs) - fireWall).TotalMilliseconds;
            if (relMs < -50)
            {
                continue;
            }

            string firstWord = row.Detail.Split(' ', 2)[0];
            switch (row.Event)
            {
                case "desktop-shown" or "desktop-hidden" or "taskbar-created" or "display-change" or "session-change":
                    transitions.Add(string.Create(CultureInfo.InvariantCulture, $"{row.Event}@{relMs:F0}"));
                    break;
                case "zpos":
                    transitions.Add(string.Create(CultureInfo.InvariantCulture, $"zpos-{firstWord}@{relMs:F0}"));
                    break;
                case "desktop-host":
                    transitions.Add(string.Create(CultureInfo.InvariantCulture, $"desktop-host@{relMs:F0}"));
                    break;
                case "winevent":
                    counts[firstWord] = counts.GetValueOrDefault(firstWord) + 1;
                    break;
            }
        }

        string winEvents = string.Join(' ', counts.Select(c => $"{c.Key}x{c.Value}"));
        return (transitions.Count == 0 ? "-" : string.Join('|', transitions), winEvents);
    }

    private static string ResultDirectory(CaseContext ctx) =>
        ctx.OutPath != null ? Path.GetDirectoryName(Path.GetFullPath(ctx.OutPath))! : RepoPaths.DryResults;

    /// <summary>Takes snapshots at T+0, 0.5, 1 and 2 s of the sampling window on pool threads, so the 10 ms cadence is not disturbed.</summary>
    private static List<Task> ScheduleSnapshots(
        HostProcess bar, string guard, string trigger, int rep, int windowMs, string resultDirectory)
    {
        var tasks = new List<Task>();
        foreach (int at in s_snapshotTimesMs.Where(t => t < windowMs))
        {
            int tMs = at;
            tasks.Add(Task.Run(() =>
            {
                if (tMs > 0)
                {
                    Thread.Sleep(tMs);
                }

                IReadOnlyList<ZEntry> entries = ZOrder.Snapshot(bar.Hwnd);
                WriteRedactedSnapshot(entries, guard, trigger, rep, tMs, dry: false, resultDirectory);
                ZOrder.ToPrivateCsv(entries, SnapshotName(guard, trigger, rep, tMs));
            }));
        }

        return tasks;
    }

    private static string SnapshotName(string guard, string trigger, int rep, int tMs) =>
        string.Create(CultureInfo.InvariantCulture, $"q2-zorder-{guard}-{trigger}-{rep}-t{tMs}.csv");

    private static string WriteRedactedSnapshot(
        IReadOnlyList<ZEntry> entries, string guard, string trigger, int rep, int tMs, bool dry, string? resultDirectory)
    {
        string directory = dry || resultDirectory == null ? RepoPaths.DryResults : resultDirectory;
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, SnapshotName(guard, trigger, rep, tMs));
        File.WriteAllText(path, ZOrder.ToRedactedCsv(entries), new UTF8Encoding(false));
        return path;
    }

    private static void WriteResultCsv(string path, IReadOnlyList<ZRunResult> results)
    {
        var text = new StringBuilder();
        text.Append("guard,trigger,rep,gap_start_ms,gap_end_ms,gap_total_ms,returned,in_front_samples,in_front_ms,desktop_host_class,guard_transitions,notes\n");
        foreach (ZRunResult r in results)
        {
            text.Append(string.Join(
                ',',
                r.Guard,
                r.Trigger,
                r.Rep.ToString(CultureInfo.InvariantCulture),
                r.GapStartMs.ToString(CultureInfo.InvariantCulture),
                r.GapEndMs.ToString(CultureInfo.InvariantCulture),
                r.GapTotalMs.ToString(CultureInfo.InvariantCulture),
                r.Returned ? "true" : "false",
                r.InFrontSamples.ToString(CultureInfo.InvariantCulture),
                r.InFrontMs.ToString(CultureInfo.InvariantCulture),
                Quote(r.DesktopHostClass),
                Quote(r.GuardTransitions),
                Quote(r.Notes))).Append('\n');
        }

        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
    }

    private static string Quote(string field) =>
        field.IndexOfAny(['"', ',', '\r', '\n']) < 0 ? field : "\"" + field.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
}

/// <summary>
/// Windows that were visible before the run. The shell keeps one undo list for Win+M, so a second Win+M (the trigger
/// <c>win-m</c> after ClearTopArea) makes the final Win+Shift+M restore only the witness; this list lets the run put
/// the developer's windows back by hand.
/// </summary>
internal static class WindowRestore
{
    public static IReadOnlyList<HWND> CaptureVisibleWindows()
    {
        var windows = new List<HWND>();
        foreach (ZEntry e in ZOrder.Snapshot(HWND.Null))
        {
            bool shell = ZOrder.IsShellDesktopClass(e.ClassName) || e.ClassName is "Shell_TrayWnd" or "Shell_SecondaryTrayWnd";
            if (e.Visible && !e.Minimized && !e.Cloaked && !e.IsDesktopHost && !shell && e.Title.Length > 0 &&
                e.Rect.Width > 0 && e.Rect.Height > 0 && !ZOrder.IsOwnProcess(e.ProcessName))
            {
                windows.Add(e.Hwnd);
            }
        }

        return windows;
    }

    /// <summary>Restores the listed windows that are minimized now, bottom first so that the original top window ends up on top.</summary>
    public static void RestoreIfMinimized(IReadOnlyList<HWND> windows)
    {
        for (int i = windows.Count - 1; i >= 0; i--)
        {
            HWND window = windows[i];
            if (PInvoke.IsWindow(window) && PInvoke.IsIconic(window))
            {
                PInvoke.ShowWindow(window, SHOW_WINDOW_CMD.SW_RESTORE);
            }
        }
    }
}

/// <summary>Writes the German summary table that is pasted into the Q2 note.</summary>
internal static class Q2Summary
{
    private const string Dash = "–";

    public static void Write(string path, IReadOnlyList<ZRunResult> results, bool dry)
    {
        var text = new StringBuilder();
        text.Append("# Q2 Ebene – ").Append(dry ? "Trockenlauf (nichts gemessen)" : "Messung").Append("\n\n");
        text.Append("| Wächter | Auslöser | Läufe | max. Lücke ms | von selbst zurück | max. vor Zeuge ms | Bemerkung |\n");
        text.Append("|---|---|---|---|---|---|---|\n");
        foreach (var group in results.GroupBy(r => (r.Guard, r.Trigger)))
        {
            List<ZRunResult> runs = group.ToList();
            int returned = runs.Count(r => r.Returned);
            string remark = dry ? "Trockenlauf, nichts gemessen" : Verdict(runs);
            text.Append("| ").Append(group.Key.Guard)
                .Append(" | ").Append(group.Key.Trigger)
                .Append(" | ").Append(runs.Count.ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(dry ? Dash : runs.Max(r => r.GapTotalMs).ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(dry ? Dash : string.Create(CultureInfo.InvariantCulture, $"{returned}/{runs.Count}"))
                .Append(" | ").Append(dry ? Dash : runs.Max(r => r.InFrontMs).ToString(CultureInfo.InvariantCulture))
                .Append(" | ").Append(remark.Replace("|", "/", StringComparison.Ordinal))
                .Append(" |\n");
        }

        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
    }

    /// <summary>Einstufung nach D-11: Lücke bis 500 ms, Rückkehr von selbst, 0 Abtastwerte vor dem Zeugen; 1–2 Abtastwerte sind ein Grenzfall (D-08).</summary>
    private static string Verdict(IReadOnlyList<ZRunResult> runs)
    {
        if (runs.Any(r => !r.Returned || r.GapTotalMs > 500 || r.InFrontSamples > 2))
        {
            return "D-11 verfehlt";
        }

        if (runs.Any(r => r.InFrontSamples is 1 or 2))
        {
            return "Grenzfall (1–2 Abtastwerte vor dem Zeugen), Stephan entscheidet";
        }

        return "D-11 erfüllt";
    }
}
