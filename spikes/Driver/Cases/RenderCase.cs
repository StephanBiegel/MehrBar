using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MehrBar.Spikes.Common;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Dwm;
using Windows.Win32.System.Threading;

namespace MehrBar.Spikes.Driver.Cases;

/// <summary>
/// Q4 render-cost cell (D-16/D-17/D-19): starts the bar with the render workload, lets it run through warm-up, the
/// measured window and an idle phase, and condenses the host's per-frame CSV plus process-time and DWM snapshots into
/// one summary row. All phase boundaries are positions on the system-wide QueryPerformanceCounter clock, which the
/// host's frames CSV uses too, so Driver snapshots and host frames line up without any handshake.
/// <c>--synthetic</c> (also forced by <c>--dry-run</c>) lets the host move its own cursor and never touches the real
/// mouse or keyboard.
/// </summary>
[DriverCase("render")]
public sealed unsafe class RenderCase : IDriverCase
{
    public const string SummaryHeader =
        "label,variant,count,mode,transparent,size,seconds,frames,cost_p50,cost_p95,cost_p99,cadence_p50,cadence_p95,cadence_p99," +
        "drop_pct,cpu_total_pct,core_eq,dwm_cpu_total_pct,dwm_frames_dropped,dwm_frames_missed,gc0,gc1,gc2,alloc_per_frame," +
        "idle_frames,idle_cpu_pct,effective_mode,tier,pm_rows";

    /// <summary>A frame counts as dropped when the cadence exceeds 1.5 periods at 60 Hz (D-19 definition).</summary>
    internal const double DropThresholdMs = 25.0;

    /// <summary>Time between host start and the warm-up start of a synthetic run (covers READY latency).</summary>
    private const int SyntheticSlackMs = 3000;

    /// <summary>Frames right after the cursor parked (last frame, final flush) are not counted as idle.</summary>
    private const int IdleGraceMs = 500;

    /// <summary>The host flushes its frame log every 100 ms; wait this long before reading it.</summary>
    private const int FlushWaitMs = 400;

    private static readonly Regex s_effective = new(@"effective=(?<v>\w+)", RegexOptions.Compiled);
    private static readonly Regex s_tier = new(@"tier=(?<v>-?\d+)", RegexOptions.Compiled);

    public int Run(CaseContext ctx)
    {
        if (!RenderRun.TryParse(ctx, out RenderRun? parsed, out string? error))
        {
            Console.Error.WriteLine(error);
            Console.Error.WriteLine(RenderRun.Usage);
            return ExitCodes.Usage;
        }

        RenderRun run = parsed!;

        // Only the real sweep sends input, so the safety session and the desktop preparation (Win+M, restore
        // registered first) exist on that path only. Synthetic and manual runs never touch input.
        // --record-input (self-test of the real path) swaps the sink for a RecordingSink: the whole real path
        // runs, but nothing reaches the desktop and the real-input watcher stays off.
        bool sweeping = !run.Synthetic && !run.Manual;
        bool record = sweeping && ctx.GetBool("record-input");
        var sink = record ? new RecordingSink() : null;
        SafetySession? safety = null;
        if (sweeping)
        {
            safety = ctx.BeginSafety(
                TimeSpan.FromSeconds(run.WarmupSeconds + run.Seconds + run.IdleSeconds + 90), watchRealInput: !record);
        }

        using (safety)
        using (sink != null ? Input.UseSink(sink) : null)
        {
            if (safety != null)
            {
                // The bar is only realistic for DWM work when nothing covers the top area.
                DesktopPrep.ClearTopArea(safety);
            }

            int code = RunCell(ctx, run);
            if (sink == null)
            {
                return code;
            }

            IReadOnlyList<string> events = sink.Events;
            int moves = events.Count(e => e.StartsWith("move:", StringComparison.Ordinal));
            Console.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"INPUT RECORDED events={events.Count} moves={moves} first={events.FirstOrDefault(e => e.StartsWith("move:", StringComparison.Ordinal))} last={events.LastOrDefault(e => e.StartsWith("move:", StringComparison.Ordinal))} win_m={events.Contains("key:5B:down")}; nothing was sent"));
            return moves > 0 ? ExitCodes.Ok : ExitCodes.CriteriaFailed;
        }
    }

    internal static double QpcMs() => Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency;

    private int RunCell(CaseContext ctx, RenderRun run)
    {
        Directory.CreateDirectory(run.FramesDir);
        string framesPath = Path.Combine(run.FramesDir, run.FramesFileName);

        using Sweep sweep = Sweep.For(run);
        HostProcess host;
        try
        {
            host = ctx.StartHost(run.HostArguments(framesPath));
        }
        catch (Exception ex) when (ex is InvalidOperationException or TimeoutException or FileNotFoundException)
        {
            Console.WriteLine($"RENDER FAILED host did not start: {ex.Message}");
            return ExitCodes.CriteriaFailed;
        }

        sweep.Attach(host);
        (string effective, string tier) = ReadRenderMode(host);

        // Phase boundaries on the QPC clock.
        double warmupStart;
        if (run.Synthetic)
        {
            HostRow? origin = host.WaitForRow("synthetic-origin", 2000);
            double originMs = origin != null && TryReadQpc(origin.Detail, out double q) ? q : double.NaN;
            if (double.IsNaN(originMs))
            {
                Console.WriteLine("RENDER FAILED host did not report a synthetic-origin row");
                return ExitCodes.CriteriaFailed;
            }

            warmupStart = originMs + SyntheticSlackMs;
            if (QpcMs() > warmupStart)
            {
                Console.WriteLine("RENDER FAILED host started too slowly for the synthetic timeline (> 3 s to READY)");
                return ExitCodes.CriteriaFailed;
            }
        }
        else
        {
            warmupStart = QpcMs() + 300;
        }

        double measureStart = warmupStart + (run.WarmupSeconds * 1000.0);
        double measureEnd = measureStart + (run.Seconds * 1000.0);
        double idleStart = measureEnd + IdleGraceMs;
        double idleEnd = idleStart + (run.IdleSeconds * 1000.0);

        using ProcessCpu? hostCpu = ProcessCpu.Open(host.Pid);
        using ProcessCpu? dwmCpu = ProcessCpu.OpenDwm();

        sweep.Run(warmupStart, measureStart, measureEnd, idleStart, idleEnd, () => Snap.Take(hostCpu, dwmCpu));
        Snap start = sweep.Start;
        Snap end = sweep.End;
        Snap idleBegin = sweep.IdleBegin;
        Snap idleFinish = sweep.IdleEnd;

        sweep.WaitUntil(Math.Max(idleEnd, measureEnd) + FlushWaitMs);
        host.Kill();

        FrameStats frames = FrameStats.Read(framesPath, measureStart, measureEnd, idleStart, idleEnd, run.IdleSeconds > 0);
        string row = BuildRow(run, frames, start, end, idleBegin, idleFinish, effective, tier);
        AppendSummary(run.OutPath, row);

        double dropPct = frames.Cadence.Count == 0 ? double.NaN : 100.0 * frames.Dropped / frames.Cadence.Count;
        Console.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"RENDER {run.Variant} {run.Count} {run.Mode} frames={frames.Frames} cost_p95={Num(frames.Cost.P95, 3)} drop_pct={Num(dropPct, 2)} cpu_total_pct={Num(CpuPct(start.HostCpu, end.HostCpu, start.QpcMs, end.QpcMs), 2)} idle_frames={(frames.IdleFrames < 0 ? "n/a" : frames.IdleFrames.ToString(CultureInfo.InvariantCulture))}"));
        Console.WriteLine($"summary row appended to {run.OutPath}; frames: {framesPath}; effective={effective} tier={tier}");
        sweep.PrintReport(Console.WriteLine);

        return frames.Frames > 0 ? ExitCodes.Ok : ExitCodes.CriteriaFailed;
    }

    private static bool TryReadQpc(string detail, out double value)
    {
        Match m = Regex.Match(detail, @"qpc_ms=(?<v>[0-9.]+)");
        value = 0;
        return m.Success && double.TryParse(m.Groups["v"].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
    }

    private static (string Effective, string Tier) ReadRenderMode(HostProcess host)
    {
        HostRow? row = host.WaitForRow("render-mode", 2000);
        if (row == null)
        {
            return ("n/a", "n/a");
        }

        Match e = s_effective.Match(row.Detail);
        Match t = s_tier.Match(row.Detail);
        return (e.Success ? e.Groups["v"].Value : "n/a", t.Success ? t.Groups["v"].Value : "n/a");
    }

    private static string BuildRow(
        RenderRun run, FrameStats f, Snap start, Snap end, Snap idleBegin, Snap idleEnd, string effective, string tier)
    {
        double wallMs = end.QpcMs - start.QpcMs;
        double hostCpuMs = (end.HostCpu - start.HostCpu) / 10000.0;
        double dropPct = f.Cadence.Count == 0 ? double.NaN : 100.0 * f.Dropped / f.Cadence.Count;
        string dwmPct = start.DwmCpu < 0 || end.DwmCpu < 0
            ? "n/a"
            : Num(CpuPct(start.DwmCpu, end.DwmCpu, start.QpcMs, end.QpcMs), 2);
        string dwmDropped = start.DwmDropped < 0 || end.DwmDropped < 0
            ? "n/a"
            : (end.DwmDropped - start.DwmDropped).ToString(CultureInfo.InvariantCulture);
        string dwmMissed = start.DwmMissed < 0 || end.DwmMissed < 0
            ? "n/a"
            : (end.DwmMissed - start.DwmMissed).ToString(CultureInfo.InvariantCulture);
        string idleCpu = run.IdleSeconds > 0 && idleBegin.HostCpu >= 0 && idleEnd.HostCpu >= 0
            ? Num(CpuPct(idleBegin.HostCpu, idleEnd.HostCpu, idleBegin.QpcMs, idleEnd.QpcMs), 3)
            : "n/a";

        var sb = new StringBuilder();
        void Add(string value) => sb.Append(sb.Length == 0 ? string.Empty : ",").Append(value);
        Add(run.Label);
        Add(run.Variant);
        Add(run.Count.ToString(CultureInfo.InvariantCulture));
        Add(run.Mode);
        Add(run.Transparent ? "true" : "false");
        Add(run.Size);
        Add(run.Seconds.ToString(CultureInfo.InvariantCulture));
        Add(f.Frames.ToString(CultureInfo.InvariantCulture));
        Add(Num(f.Cost.Median, 3));
        Add(Num(f.Cost.P95, 3));
        Add(Num(f.Cost.P99, 3));
        Add(Num(f.Cadence.Median, 3));
        Add(Num(f.Cadence.P95, 3));
        Add(Num(f.Cadence.P99, 3));
        Add(Num(dropPct, 2));
        Add(Num(CpuPct(start.HostCpu, end.HostCpu, start.QpcMs, end.QpcMs), 2));
        Add(Num(wallMs > 0 ? hostCpuMs / wallMs : double.NaN, 3));
        Add(dwmPct);
        Add(dwmDropped);
        Add(dwmMissed);
        Add(f.Gc0.ToString(CultureInfo.InvariantCulture));
        Add(f.Gc1.ToString(CultureInfo.InvariantCulture));
        Add(f.Gc2.ToString(CultureInfo.InvariantCulture));
        Add(Num(f.AllocPerFrame, 0));
        Add(f.IdleFrames < 0 ? "n/a" : f.IdleFrames.ToString(CultureInfo.InvariantCulture));
        Add(idleCpu);
        Add(effective);
        Add(tier);
        Add("n/a"); // pm_rows: filled in by run-render-matrix.ps1 when PresentMon ran
        return sb.ToString();
    }

    /// <summary>CPU time delta as a percentage of all logical processors (the Task Manager column, D-19).</summary>
    internal static double CpuPct(long cpuStart, long cpuEnd, double qpcStartMs, double qpcEndMs)
    {
        double wallMs = qpcEndMs - qpcStartMs;
        if (cpuStart < 0 || cpuEnd < 0 || wallMs <= 0)
        {
            return double.NaN;
        }

        double cpuMs = (cpuEnd - cpuStart) / 10000.0; // FILETIME units are 100 ns
        return cpuMs / (wallMs * Environment.ProcessorCount) * 100.0;
    }

    internal static string Num(double value, int decimals) =>
        double.IsNaN(value) || double.IsInfinity(value)
            ? "n/a"
            : value.ToString("F" + decimals.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    private static void AppendSummary(string path, string row)
    {
        string full = Path.GetFullPath(path);
        string? dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        bool writeHeader = !File.Exists(full) || new FileInfo(full).Length == 0;
        var text = new StringBuilder();
        if (writeHeader)
        {
            text.Append(SummaryHeader).Append('\n');
        }

        text.Append(row).Append('\n');
        File.AppendAllText(full, text.ToString(), new UTF8Encoding(false));
    }

    /// <summary>Process-time and DWM counters at one instant. Negative values mean "not available".</summary>
    internal readonly record struct Snap(double QpcMs, long HostCpu, long DwmCpu, long DwmDropped, long DwmMissed)
    {
        public static Snap Take(ProcessCpu? host, ProcessCpu? dwm)
        {
            long dropped = -1;
            long missed = -1;
            var info = new DWM_TIMING_INFO { cbSize = (uint)sizeof(DWM_TIMING_INFO) };
            // HWND must be NULL since Windows 8.1.
            if (PInvoke.DwmGetCompositionTimingInfo(HWND.Null, &info).Succeeded)
            {
                dropped = (long)info.cFramesDropped;
                missed = (long)info.cFramesMissed;
            }

            return new Snap(RenderCase.QpcMs(), host?.Read() ?? -1, dwm?.Read() ?? -1, dropped, missed);
        }
    }

    /// <summary>Kernel plus user time of a process, read through <c>GetProcessTimes</c> (100 ns units).</summary>
    internal sealed class ProcessCpu : IDisposable
    {
        private HANDLE _handle;

        private ProcessCpu(HANDLE handle)
        {
            _handle = handle;
        }

        public static ProcessCpu? Open(int pid)
        {
            HANDLE handle = PInvoke.OpenProcess(PROCESS_ACCESS_RIGHTS.PROCESS_QUERY_LIMITED_INFORMATION, false, (uint)pid);
            return handle.IsNull ? null : new ProcessCpu(handle);
        }

        /// <summary>dwm.exe of this logon session; null when it cannot be opened at normal integrity ("n/a" in the row).</summary>
        public static ProcessCpu? OpenDwm()
        {
            try
            {
                int session = Process.GetCurrentProcess().SessionId;
                foreach (Process p in Process.GetProcessesByName("dwm"))
                {
                    using (p)
                    {
                        if (p.SessionId == session)
                        {
                            return Open(p.Id);
                        }
                    }
                }
            }
            catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // Access denied: report n/a.
            }

            return null;
        }

        public long Read()
        {
            if (_handle.IsNull)
            {
                return -1;
            }

            if (!PInvoke.GetProcessTimes(_handle, out _, out _, out var kernel, out var user))
            {
                return -1;
            }

            return Ticks(kernel) + Ticks(user);
        }

        public void Dispose()
        {
            if (!_handle.IsNull)
            {
                PInvoke.CloseHandle(_handle);
                _handle = default;
            }
        }

        private static long Ticks(System.Runtime.InteropServices.ComTypes.FILETIME t) =>
            ((long)t.dwHighDateTime << 32) | (uint)t.dwLowDateTime;
    }
}

/// <summary>Parsed command line of one render cell.</summary>
internal sealed class RenderRun
{
    public const string Usage =
        "Driver render --variant R1|R2 --count N --render hw|sw [--transparent true|false] [--size WxH] [--seconds 30] " +
        "[--warmup 5] [--idle 5] [--synthetic] [--manual --seconds N] [--label L] [--out csv] [--frames-dir dir] [--base px] [--pitch px]";

    private RenderRun()
    {
    }

    public string Variant { get; private init; } = "R1";

    public int Count { get; private init; } = 20;

    public string Mode { get; private init; } = "hw";

    public bool Transparent { get; private init; } = true;

    public string Size { get; private init; } = "1900x250";

    public int Seconds { get; private init; } = 30;

    public int WarmupSeconds { get; private init; } = 5;

    public int IdleSeconds { get; private init; } = 5;

    /// <summary>The host moves its own cursor; no real input at all.</summary>
    public bool Synthetic { get; private init; }

    /// <summary>No input from the Driver; the developer moves the real mouse (hand part H5).</summary>
    public bool Manual { get; private init; }

    public string Label { get; private init; } = "run";

    public string OutPath { get; private init; } = string.Empty;

    public string FramesDir { get; private init; } = string.Empty;

    public double Base { get; private init; }

    public double Pitch { get; private init; }

    public string FramesFileName =>
        $"{Label}-{Variant}-{Count}-{Mode}{(Transparent ? string.Empty : "-opaque")}-{Size}.csv";

    public int WindowWidth => int.Parse(Size.Split('x')[0], CultureInfo.InvariantCulture);

    public int WindowHeight => int.Parse(Size.Split('x')[1], CultureInfo.InvariantCulture);

    public static bool TryParse(CaseContext ctx, out RenderRun? run, out string? error)
    {
        run = null;
        error = null;

        string variant = (ctx.Get("variant") ?? "R1").ToUpperInvariant();
        if (variant is not ("R1" or "R2"))
        {
            error = $"--variant must be R1 or R2, not '{variant}'";
            return false;
        }

        string mode = (ctx.Get("render") ?? "hw").ToLowerInvariant();
        if (mode is not ("hw" or "sw"))
        {
            error = $"--render must be hw or sw, not '{mode}'";
            return false;
        }

        string transparentText = (ctx.Get("transparent") ?? "true").ToLowerInvariant();
        if (transparentText is not ("true" or "false"))
        {
            error = $"--transparent must be true or false, not '{transparentText}'";
            return false;
        }

        string size = ctx.Get("size") ?? "1900x250";
        if (!Regex.IsMatch(size, @"^\d{3,5}x\d{2,5}$"))
        {
            error = $"--size must be WxH, not '{size}'";
            return false;
        }

        int count = ctx.GetInt("count", 20);
        if (count is < 1 or > 256)
        {
            error = $"--count must be 1..256, not {count}";
            return false;
        }

        bool dryRun = ctx.DryRun;
        bool synthetic = dryRun || ctx.GetBool("synthetic");
        bool manual = ctx.Manual && !dryRun;
        if (synthetic && manual)
        {
            error = "--synthetic and --manual exclude each other";
            return false;
        }

        int seconds = ctx.GetInt("seconds", 30);
        int warmup = ctx.GetInt("warmup", 5);
        int idle = ctx.GetInt("idle", manual ? 0 : 5);
        if (seconds < 1 || warmup < 0 || idle < 0)
        {
            error = "--seconds must be >= 1, --warmup and --idle >= 0";
            return false;
        }

        string label = Regex.Replace(ctx.Get("label") ?? "run", @"[^A-Za-z0-9._-]", "_");
        bool dense = count > 20;
        double baseSize = double.TryParse(ctx.Get("base"), NumberStyles.Float, CultureInfo.InvariantCulture, out double b) ? b : (dense ? 24 : 32);
        double pitch = double.TryParse(ctx.Get("pitch"), NumberStyles.Float, CultureInfo.InvariantCulture, out double p) ? p : (dense ? 28 : 36);

        run = new RenderRun
        {
            Variant = variant,
            Count = count,
            Mode = mode,
            Transparent = transparentText == "true",
            Size = size,
            Seconds = seconds,
            WarmupSeconds = warmup,
            IdleSeconds = idle,
            Synthetic = synthetic,
            Manual = manual,
            Label = label,
            OutPath = ctx.OutPath ?? Path.Combine(RepoPaths.DryResults, $"q4-{label}.csv"),
            FramesDir = ctx.Get("frames-dir") ?? Path.Combine(RepoPaths.DryResults, "q4-raw"),
            Base = baseSize,
            Pitch = pitch,
        };
        return true;
    }

    /// <summary>WindowHost arguments; the host self-terminates a safe margin after the planned end (T-01-16).</summary>
    public string HostArguments(string framesPath)
    {
        int syntheticStopMs = 3000 + ((WarmupSeconds + Seconds) * 1000);
        int exitAfterMs = 3000 + ((WarmupSeconds + Seconds + IdleSeconds) * 1000) + 25000;
        var args = new StringBuilder();
        args.Append(CultureInfo.InvariantCulture, $"--variant {Variant} --count {Count} --render {Mode} ");
        args.Append(CultureInfo.InvariantCulture, $"--transparent {(Transparent ? "true" : "false")} --size {Size} ");
        args.Append(CultureInfo.InvariantCulture, $"--base {Base} --pitch {Pitch} --frames \"{framesPath}\" --exit-after-ms {exitAfterMs}");
        if (Synthetic)
        {
            args.Append(CultureInfo.InvariantCulture, $" --synthetic-cursor true --synthetic-stop-ms {syntheticStopMs}");
        }

        return args.ToString();
    }
}

/// <summary>Statistics of one host frames CSV over the measured and the idle window.</summary>
internal sealed record FrameStats(
    int Frames,
    StatsSummary Cost,
    StatsSummary Cadence,
    int Dropped,
    long Gc0,
    long Gc1,
    long Gc2,
    double AllocPerFrame,
    int IdleFrames)
{
    /// <summary>
    /// Columns: qpc_ms,rendering_time_ms,is_duplicate,handler_us,onrender_us,gc0,gc1,gc2,alloc_bytes. Rows with
    /// qpc_ms in [measureStart, measureEnd) form the measured window, rows in [idleStart, idleEnd) the idle window.
    /// Duplicate-timestamp rows are skipped for cost and cadence (they are not new frames).
    /// </summary>
    public static FrameStats Read(
        string path, double measureStart, double measureEnd, double idleStart, double idleEnd, bool hasIdle)
    {
        var cost = new List<double>();
        var cadence = new List<double>();
        int dropped = 0;
        long gcFirst0 = -1;
        long gcFirst1 = 0;
        long gcFirst2 = 0;
        long gcLast0 = 0;
        long gcLast1 = 0;
        long gcLast2 = 0;
        double allocSum = 0;
        double previousRendering = double.NaN;
        int idle = 0;

        if (File.Exists(path))
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            using var reader = new StreamReader(stream);
            _ = reader.ReadLine(); // header
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                string[] f = line.Split(',');
                if (f.Length < 9 ||
                    !double.TryParse(f[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double qpc) ||
                    !double.TryParse(f[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double rendering) ||
                    !long.TryParse(f[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out long handlerUs) ||
                    !long.TryParse(f[4], NumberStyles.Integer, CultureInfo.InvariantCulture, out long onRenderUs) ||
                    !long.TryParse(f[5], NumberStyles.Integer, CultureInfo.InvariantCulture, out long gc0) ||
                    !long.TryParse(f[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out long gc1) ||
                    !long.TryParse(f[7], NumberStyles.Integer, CultureInfo.InvariantCulture, out long gc2) ||
                    !long.TryParse(f[8], NumberStyles.Integer, CultureInfo.InvariantCulture, out long alloc))
                {
                    continue; // a line the host was still writing
                }

                bool duplicate = f[2] == "1";

                if (hasIdle && qpc >= idleStart && qpc < idleEnd)
                {
                    idle++;
                }

                if (qpc < measureStart || qpc >= measureEnd)
                {
                    continue;
                }

                if (gcFirst0 < 0)
                {
                    (gcFirst0, gcFirst1, gcFirst2) = (gc0, gc1, gc2);
                }

                (gcLast0, gcLast1, gcLast2) = (gc0, gc1, gc2);
                allocSum += alloc;
                if (duplicate)
                {
                    continue;
                }

                cost.Add((handlerUs + onRenderUs) / 1000.0);
                if (!double.IsNaN(previousRendering))
                {
                    double delta = rendering - previousRendering;
                    cadence.Add(delta);
                    if (delta > RenderCase.DropThresholdMs)
                    {
                        dropped++;
                    }
                }

                previousRendering = rendering;
            }
        }

        int frames = cost.Count;
        return new FrameStats(
            frames,
            Stats.Summarize(cost),
            Stats.Summarize(cadence),
            dropped,
            gcFirst0 < 0 ? 0 : gcLast0 - gcFirst0,
            gcFirst0 < 0 ? 0 : gcLast1 - gcFirst1,
            gcFirst0 < 0 ? 0 : gcLast2 - gcFirst2,
            frames > 0 ? allocSum / frames : double.NaN,
            hasIdle ? idle : -1);
    }
}

/// <summary>
/// Drives the phases of one cell. This base class only waits (synthetic runs: the host moves its own cursor).
/// The real sweep and the manual mode are subclasses.
/// </summary>
internal class Sweep : IDisposable
{
    public RenderCase.Snap Start { get; protected set; }

    public RenderCase.Snap End { get; protected set; }

    public RenderCase.Snap IdleBegin { get; protected set; }

    public RenderCase.Snap IdleEnd { get; protected set; }

    public static Sweep For(RenderRun run) =>
        run.Manual ? new ManualSweep(run) : run.Synthetic ? new Sweep() : new RealSweep(run);

    /// <summary>Called once the host is up (its window exists); the real sweep reads the window geometry here.</summary>
    public virtual void Attach(HostProcess host)
    {
    }

    public void Run(
        double warmupStart,
        double measureStart,
        double measureEnd,
        double idleStart,
        double idleEnd,
        Func<RenderCase.Snap> snap)
    {
        WaitUntil(measureStart);
        Start = snap();
        WaitUntil(measureEnd);
        End = snap();
        AfterMeasure();
        WaitUntil(idleStart);
        IdleBegin = snap();
        WaitUntil(idleEnd);
        IdleEnd = snap();
    }

    public void WaitUntil(double qpcMs)
    {
        double remaining;
        while ((remaining = qpcMs - RenderCase.QpcMs()) > 0)
        {
            Tick(qpcMs, remaining);
        }
    }

    public virtual void PrintReport(Action<string> write)
    {
    }

    public virtual void Dispose()
    {
    }

    /// <summary>One step of waiting; the default sleeps (at most 20 ms, so the target is hit closely).</summary>
    protected virtual void Tick(double targetQpcMs, double remainingMs) =>
        Safety.Wait((int)Math.Clamp(Math.Floor(remainingMs), 1, 20));

    /// <summary>Called once the measured window ended (the real sweep parks the mouse here).</summary>
    protected virtual void AfterMeasure()
    {
    }
}

/// <summary>
/// The real sweep (D-17): moves the mouse with SendInput on a fixed sine path (centre of the window plus
/// A = 3.5 pitches times sin(2 pi t / 2 s), at the vertical centre of the plate) every ~8 ms through warm-up and the
/// measured window, then parks the mouse below the window for the idle phase. Only constructed inside a
/// <see cref="SafetySession"/> (every <see cref="Input"/> call refuses to run without one). Waits use a high-resolution
/// waitable timer plus a short spin: <c>timeBeginPeriod(1)</c> was measured to have no effect on this machine
/// (Windows 11 ignores timer-resolution requests of windowless processes; steps came out at a 13 ms median instead of
/// 8 ms). The interval of the steps is recorded and reported (<see cref="PrintReport"/>).
/// </summary>
internal sealed unsafe class RealSweep : Sweep
{
    private const double StepMs = 8.0;
    private const double PeriodMs = 2000.0;
    private const double AmplitudePitches = 3.5;
    private const int ParkDistance = 80;

    private readonly double _amplitude;
    private readonly List<double> _intervals = [];
    private HANDLE _timer;
    private bool _sweeping;
    private double _centreX;
    private int _sweepY;
    private int _parkY;
    private double _t0;
    private double _nextStep;
    private double _lastStep;

    public RealSweep(RenderRun run)
    {
        _amplitude = AmplitudePitches * run.Pitch;
    }

    public override void Attach(HostProcess host)
    {
        PInvoke.GetWindowRect(host.Hwnd, out RECT rect);
        _centreX = (rect.left + rect.right) / 2.0;
        _sweepY = rect.top + SpikeGeometry.Plate.Y + (SpikeGeometry.Plate.Height / 2);
        int screenHeight = PInvoke.GetSystemMetrics(Windows.Win32.UI.WindowsAndMessaging.SYSTEM_METRICS_INDEX.SM_CYSCREEN);
        _parkY = Math.Min(rect.bottom + ParkDistance, screenHeight - 2);

        _timer = PInvoke.CreateWaitableTimerEx(
            null, null, PInvoke.CREATE_WAITABLE_TIMER_HIGH_RESOLUTION, (uint)SYNCHRONIZATION_ACCESS_RIGHTS.TIMER_ALL_ACCESS);
        _t0 = RenderCase.QpcMs();
        _nextStep = _t0;
        _sweeping = true;
    }

    public override void PrintReport(Action<string> write)
    {
        if (_intervals.Count == 0)
        {
            return;
        }

        double[] deviation = _intervals.Select(i => Math.Abs(i - StepMs)).ToArray();
        StatsSummary interval = Stats.Summarize(_intervals);
        write(
            string.Create(
                CultureInfo.InvariantCulture,
                $"sweep steps={_intervals.Count} interval_p50={interval.Median:F2} ms p95={interval.P95:F2} ms p99={interval.P99:F2} ms max={interval.Max:F2} ms; |interval-{StepMs:F0}| p95={Stats.Percentile(deviation, 95):F2} ms"));
    }

    public override void Dispose()
    {
        if (!_timer.IsNull)
        {
            PInvoke.CloseHandle(_timer);
            _timer = default;
        }
    }

    protected override void Tick(double targetQpcMs, double remainingMs)
    {
        if (!_sweeping)
        {
            base.Tick(targetQpcMs, remainingMs);
            return;
        }

        double now = RenderCase.QpcMs();
        if (now >= _nextStep)
        {
            double t = now - _t0;
            int x = (int)Math.Round(_centreX + (_amplitude * Math.Sin(2.0 * Math.PI * t / PeriodMs)));
            Input.MoveAbs(x, _sweepY);
            if (_lastStep > 0)
            {
                _intervals.Add(now - _lastStep);
            }

            _lastStep = now;
            _nextStep += StepMs;
            if (_nextStep < now)
            {
                _nextStep = now + StepMs; // do not replay missed steps
            }

            return;
        }

        double wait = Math.Min(_nextStep, targetQpcMs) - now;
        if (wait > 1.0)
        {
            SleepHighResolution(wait - 0.6);
        }
        else
        {
            Thread.SpinWait(100);
        }

        Safety.ThrowIfAborted();
    }

    /// <summary>Sleeps on the high-resolution timer (about 0.5 ms granularity); falls back to a spin when it is unavailable.</summary>
    private void SleepHighResolution(double milliseconds)
    {
        if (_timer.IsNull)
        {
            Thread.SpinWait(200);
            return;
        }

        long due = -(long)(milliseconds * 10000.0); // negative = relative, in 100 ns units
        if (PInvoke.SetWaitableTimer(_timer, &due, 0, null, null, false))
        {
            PInvoke.WaitForSingleObject(_timer, 50);
        }
    }

    protected override void AfterMeasure()
    {
        _sweeping = false;
        Input.MoveAbs((int)Math.Round(_centreX), _parkY);
    }
}

/// <summary>Manual mode (hand part H5): no input from the Driver; the developer moves the real mouse.</summary>
internal sealed class ManualSweep : Sweep
{
    private readonly RenderRun _run;

    public ManualSweep(RenderRun run)
    {
        _run = run;
    }

    public override void Attach(HostProcess host)
    {
        Console.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"MANUAL: move the mouse over the bar now - warm-up {_run.WarmupSeconds} s (discarded), then {_run.Seconds} s measured."));
    }

    protected override void AfterMeasure()
    {
        if (_run.IdleSeconds > 0)
        {
            Console.WriteLine($"MANUAL: measured window over - keep the mouse OUT of the bar for {_run.IdleSeconds} s (idle phase).");
        }
        else
        {
            Console.WriteLine("MANUAL: measured window over.");
        }
    }
}
