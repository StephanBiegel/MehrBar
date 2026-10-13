using System.Globalization;
using System.Runtime.Versioning;
using System.Text;
using MehrBar.Spikes.Common;

namespace MehrBar.Spikes.Driver.Cases;

/// <summary>
/// Plan 01-19: captures a bar on screen (any program, e.g. RocketDock, or the spike host) with Desktop Duplication
/// while the Driver moves the mouse on the Q4 sine path over it (or the developer moves it with <c>--manual</c>).
/// Writes meta.txt, frames.csv and rows.bin into the output folder; <c>screen-analyze</c> evaluates them.
/// The real sweep runs inside a SafetySession (abort Ctrl+Alt+Q, real input aborts). No window is touched unless
/// <c>--clear-top</c> asks for Win+M (restored at the end).
/// </summary>
[DriverCase("screen-sweep")]
[SupportedOSPlatform("windows8.0")]
public sealed class ScreenSweepCase : IDriverCase
{
    private const string Usage =
        "Driver screen-sweep --rect x,y,w,h [--sweep-y y] [--rows y1,y2,..] [--centre x] [--amplitude px] " +
        "[--seconds 20] [--warmup 2] [--manual] [--clear-top] [--label L] [--out-dir dir]";

    public int Run(CaseContext ctx)
    {
        int[]? rect = Ints(ctx.Get("rect"));
        if (rect is not { Length: 4 })
        {
            Console.Error.WriteLine(Usage);
            return ExitCodes.Usage;
        }

        int x = rect[0], y = rect[1], w = rect[2], h = rect[3];
        int sweepY = ctx.GetInt("sweep-y", y + (h / 2));
        int[] rows = Ints(ctx.Get("rows")) ?? [sweepY - 6, sweepY - 3, sweepY, sweepY + 3, sweepY + 6];
        double centre = ctx.GetInt("centre", x + (w / 2));
        double amplitude = ctx.GetInt("amplitude", 150);
        int seconds = ctx.GetInt("seconds", 20);
        int warmup = ctx.GetInt("warmup", 2);
        bool manual = ctx.Manual;
        string label = ctx.Get("label") ?? "screen";
        string outDir = ctx.Get("out-dir") ?? Path.Combine(RepoPaths.PrivateResults, "q4b-raw", label);
        Directory.CreateDirectory(outDir);

        SafetySession? safety = manual ? null : ctx.BeginSafety(TimeSpan.FromSeconds(warmup + seconds + 60));
        using (safety)
        using (var capture = new ScreenCapture(x, w, rows))
        using (RealSweep? sweep = manual ? null : new RealSweep(amplitude))
        {
            if (safety != null && ctx.GetBool("clear-top"))
            {
                DesktopPrep.ClearTopArea(safety);
            }

            capture.Start();
            if (capture.Error != null)
            {
                Console.WriteLine("CAPTURE FAILED " + capture.Error);
                return ExitCodes.CriteriaFailed;
            }

            double now = RenderCase.QpcMs();
            double measureStart = now + (warmup * 1000.0);
            double measureEnd = measureStart + (seconds * 1000.0);
            double origin = double.NaN;
            if (sweep != null)
            {
                sweep.Begin(centre, sweepY, y + h + 80);
                origin = sweep.OriginMs;
                sweep.Run(now, measureStart, measureEnd, measureEnd, measureEnd + 500, () => default);
                sweep.PrintReport(Console.WriteLine);
            }
            else
            {
                Console.WriteLine($"MANUAL: move the mouse over the bar now; warm-up {warmup} s, then {seconds} s captured.");
                Thread.Sleep((int)(measureEnd + 500 - now));
            }

            capture.Stop();
            if (capture.Error != null)
            {
                Console.WriteLine("CAPTURE FAILED " + capture.Error);
                return ExitCodes.CriteriaFailed;
            }

            Save(capture, outDir, x, rows, sweepY, centre, amplitude, origin, measureStart, measureEnd, manual);
            Console.WriteLine($"SCREEN captured {capture.Frames.Count} frames into {outDir}");
        }

        return ExitCodes.Ok;
    }

    /// <summary>Writes meta.txt, frames.csv and rows.bin of a capture (shared with the render case's --capture).</summary>
    internal static void Save(
        ScreenCapture capture, string outDir, int x, int[] rows, int sweepY, double centre, double amplitude,
        double origin, double measureStart, double measureEnd, bool manual)
    {
        Directory.CreateDirectory(outDir);
        var meta = new StringBuilder();
        void Meta(string key, object value) => meta.Append(CultureInfo.InvariantCulture, $"{key}={value}\n");
        Meta("x", x);
        Meta("width", capture.Width);
        Meta("rows", string.Join(';', rows));
        Meta("sweep_y", sweepY);
        Meta("centre", centre.ToString(CultureInfo.InvariantCulture));
        Meta("amplitude", amplitude.ToString(CultureInfo.InvariantCulture));
        Meta("period_ms", 2000);
        Meta("origin_ms", origin.ToString("F3", CultureInfo.InvariantCulture));
        Meta("measure_start_ms", measureStart.ToString("F3", CultureInfo.InvariantCulture));
        Meta("measure_end_ms", measureEnd.ToString("F3", CultureInfo.InvariantCulture));
        Meta("manual", manual);
        File.WriteAllText(Path.Combine(outDir, "meta.txt"), meta.ToString());

        var frames = new StringBuilder("index,qpc_ms,accumulated\n");
        using (FileStream bin = File.Create(Path.Combine(outDir, "rows.bin")))
        {
            for (int i = 0; i < capture.Frames.Count; i++)
            {
                CapturedFrame f = capture.Frames[i];
                frames.Append(CultureInfo.InvariantCulture, $"{i},{f.QpcMs:F3},{f.Accumulated}\n");
                bin.Write(f.Pixels);
            }
        }

        File.WriteAllText(Path.Combine(outDir, "frames.csv"), frames.ToString());
    }

    internal static int[]? Ints(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? null
            : text.Split(',', ';').Select(t => int.Parse(t.Trim(), CultureInfo.InvariantCulture)).ToArray();
}

/// <summary>
/// Evaluates a screen-sweep folder: frame pacing of the bar (intervals between frames in which the captured rows
/// changed) and the motion of the bar far from the pointer (sub-pixel shift of fixed windows of the row between
/// consecutive frames). Items outside the zoom radius should move steadily or stand still; back-and-forth motion shows
/// up as reversals and as path length well above the net travel.
/// </summary>
[DriverCase("screen-analyze")]
public sealed class ScreenAnalyzeCase : IDriverCase
{
    private const int MaxShift = 8;

    public int Run(CaseContext ctx)
    {
        string? dir = ctx.Get("dir");
        if (dir == null || !File.Exists(Path.Combine(dir, "meta.txt")))
        {
            Console.Error.WriteLine("Driver screen-analyze --dir <screen-sweep folder> [--row i] [--far px] [--hz 60] [--window 48] [--label L] [--out csv]");
            return ExitCodes.Usage;
        }

        Dictionary<string, string> meta = File.ReadAllLines(Path.Combine(dir, "meta.txt"))
            .Where(l => l.Contains('='))
            .ToDictionary(l => l[..l.IndexOf('=')], l => l[(l.IndexOf('=') + 1)..]);
        int width = int.Parse(meta["width"], CultureInfo.InvariantCulture);
        int x0 = int.Parse(meta["x"], CultureInfo.InvariantCulture);
        int rowCount = meta["rows"].Split(';').Length;
        double centre = D(meta["centre"]);
        double amplitude = D(meta["amplitude"]);
        double origin = D(meta["origin_ms"]);
        double start = D(meta["measure_start_ms"]);
        double end = D(meta["measure_end_ms"]);
        bool manual = bool.Parse(meta["manual"]);
        int row = ctx.GetInt("row", rowCount / 2);
        double far = ctx.GetInt("far", 250);
        double hz = ctx.GetInt("hz", 60);
        int window = ctx.GetInt("window", 48);
        double period = 1000.0 / hz;

        double[] times = File.ReadAllLines(Path.Combine(dir, "frames.csv")).Skip(1)
            .Select(l => D(l.Split(',')[1])).ToArray();
        byte[] bin = File.ReadAllBytes(Path.Combine(dir, "rows.bin"));
        int frameBytes = width * 4 * rowCount;

        // Luminance of the chosen row for every frame inside the measured window.
        var lum = new List<double[]>();
        var t = new List<double>();
        for (int i = 0; i < times.Length; i++)
        {
            if (times[i] < start || times[i] > end)
            {
                continue;
            }

            var line = new double[width];
            int offset = (i * frameBytes) + (row * width * 4);
            for (int px = 0; px < width; px++)
            {
                int o = offset + (px * 4);
                line[px] = (bin[o] + bin[o + 1] + bin[o + 2]) / 3.0;
            }

            lum.Add(line);
            t.Add(times[i]);
        }

        // Frames in which the bar row changed, and their intervals.
        var changedTimes = new List<double>();
        var changedLines = new List<double[]>();
        for (int i = 0; i < lum.Count; i++)
        {
            if (i == 0 || !lum[i].AsSpan().SequenceEqual(lum[i - 1]))
            {
                changedTimes.Add(t[i]);
                changedLines.Add(lum[i]);
            }
        }

        var intervals = new List<double>();
        for (int i = 1; i < changedTimes.Count; i++)
        {
            intervals.Add(changedTimes[i] - changedTimes[i - 1]);
        }

        var paced = intervals.Where(v => v < 250).ToList();
        int skipped = paced.Count(v => v >= 1.5 * period);
        double missedPeriods = paced.Sum(v => Math.Max(0, Math.Round(v / period) - 1));
        double allPeriods = paced.Sum(v => Math.Max(1, Math.Round(v / period)));

        // Motion far from the pointer: per window, shift between consecutive changed frames.
        // Without a known pointer path (manual), --far is the half-width of a band around --centre that is excluded.
        double CursorX(double ms) => manual ? centre - x0 : centre - x0 + (amplitude * Math.Sin(2.0 * Math.PI * (ms - origin) / 2000.0));
        var windowStarts = new List<int>();
        for (int s = MaxShift; s + window + MaxShift <= width; s += window / 2)
        {
            windowStarts.Add(s);
        }

        var path = new double[windowStarts.Count];
        var net = new double[windowStarts.Count];
        var reversals = new int[windowStarts.Count];
        var lastSign = new int[windowStarts.Count];
        var used = new int[windowStarts.Count];
        var shiftsAbs = new List<double>();
        var relative = new List<double>();
        var motion = new StringBuilder("qpc_ms,window_x,shift_px,cursor_x\n");
        for (int i = 1; i < changedLines.Count; i++)
        {
            double cx0 = CursorX(changedTimes[i - 1]);
            double cx1 = CursorX(changedTimes[i]);
            double? previousShift = null;
            int previousIndex = -10;
            for (int wi = 0; wi < windowStarts.Count; wi++)
            {
                int ws = windowStarts[wi];
                double wc = ws + (window / 2.0);
                if (Math.Abs(wc - cx0) < far || Math.Abs(wc - cx1) < far)
                {
                    previousShift = null;
                    continue;
                }

                double? shift = Shift(changedLines[i - 1], changedLines[i], ws, window);
                if (shift is not double s)
                {
                    previousShift = null;
                    continue;
                }

                motion.Append(CultureInfo.InvariantCulture, $"{changedTimes[i]:F3},{wc + x0:F0},{s:F3},{cx1 + x0:F1}\n");
                shiftsAbs.Add(Math.Abs(s));
                path[wi] += Math.Abs(s);
                net[wi] += s;
                used[wi]++;
                int sign = Math.Abs(s) < 0.15 ? 0 : Math.Sign(s);
                if (sign != 0)
                {
                    if (lastSign[wi] != 0 && sign != lastSign[wi])
                    {
                        reversals[wi]++;
                    }

                    lastSign[wi] = sign;
                }

                // Neighbouring windows on the same side of the pointer should move together.
                if (previousShift is double ps && previousIndex == wi - 1 && Math.Sign(wc - cx1) == Math.Sign(wc - (window / 2.0) - cx1))
                {
                    relative.Add(Math.Abs(s - ps));
                }

                previousShift = s;
                previousIndex = wi;
            }
        }

        double seconds = (end - start) / 1000.0;
        int[] active = Enumerable.Range(0, windowStarts.Count).Where(i => used[i] > 20).ToArray();
        double meanPath = active.Length == 0 ? double.NaN : active.Average(i => path[i]) / seconds;
        double meanNet = active.Length == 0 ? double.NaN : active.Average(i => Math.Abs(net[i])) / seconds;
        double meanReversals = active.Length == 0 ? double.NaN : active.Average(i => reversals[i]) / seconds;

        string label = ctx.Get("label") ?? Path.GetFileName(dir.TrimEnd('\\', '/'));
        string header = "label,frames,bar_frames,interval_p50,interval_p95,skipped_pct,missed_periods_pct,far_windows,far_shift_abs_p95,far_path_px_s,far_net_px_s,far_reversals_s,neighbour_diff_p95";
        string summary = string.Create(
            CultureInfo.InvariantCulture,
            $"{label},{lum.Count},{changedTimes.Count},{P(intervals, 50):F2},{P(intervals, 95):F2},{(paced.Count == 0 ? double.NaN : 100.0 * skipped / paced.Count):F2},{(allPeriods == 0 ? double.NaN : 100.0 * missedPeriods / allPeriods):F2},{active.Length},{P(shiftsAbs, 95):F3},{meanPath:F2},{meanNet:F2},{meanReversals:F2},{P(relative, 95):F3}");
        Console.WriteLine(header);
        Console.WriteLine(summary);
        File.WriteAllText(Path.Combine(dir, "motion.csv"), motion.ToString());
        string? outPath = ctx.OutPath;
        if (outPath != null)
        {
            bool fresh = !File.Exists(outPath);
            File.AppendAllText(outPath, (fresh ? header + "\n" : string.Empty) + summary + "\n");
        }

        return ExitCodes.Ok;
    }

    private static double D(string s) => double.Parse(s, CultureInfo.InvariantCulture);

    private static double P(List<double> values, double p) => values.Count == 0 ? double.NaN : Stats.Percentile(values, p);

    /// <summary>
    /// Shift (px) of the window [start, start + length) from line a to line b: integer search of the sum of squared
    /// differences over +-8 px, refined with a parabola. Null for a window without structure.
    /// </summary>
    private static double? Shift(double[] a, double[] b, int start, int length)
    {
        double mean = 0;
        for (int i = start; i < start + length; i++)
        {
            mean += a[i];
        }

        mean /= length;
        double variance = 0;
        for (int i = start; i < start + length; i++)
        {
            variance += (a[i] - mean) * (a[i] - mean);
        }

        if (variance / length < 25)
        {
            return null;
        }

        var ssd = new double[(2 * MaxShift) + 1];
        for (int s = -MaxShift; s <= MaxShift; s++)
        {
            double sum = 0;
            for (int i = start; i < start + length; i++)
            {
                double d = b[i + s] - a[i];
                sum += d * d;
            }

            ssd[s + MaxShift] = sum;
        }

        int best = 0;
        for (int k = 1; k < ssd.Length; k++)
        {
            if (ssd[k] < ssd[best])
            {
                best = k;
            }
        }

        if (best == 0 || best == ssd.Length - 1)
        {
            return null;
        }

        double l = ssd[best - 1], c = ssd[best], r = ssd[best + 1];
        double denominator = l - (2 * c) + r;
        double offset = denominator > 0 ? 0.5 * (l - r) / denominator : 0;
        return best - MaxShift + offset;
    }
}
