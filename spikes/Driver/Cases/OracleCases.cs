using System.Globalization;
using MehrBar.Spikes.Common;
using Windows.Win32.Foundation;

namespace MehrBar.Spikes.Driver.Cases;

/// <summary>
/// Self-test of the oracles (RESEARCH Pattern 3, assumption A1: GetPixel on the screen DC returns the
/// composed DWM image). A lying oracle would invalidate every later measurement, so the real run proves
/// both directions: the bar left uncovered reads as visible, and a witness window on top reads as covered.
///
/// The real run (no flag) minimizes all windows and sends input; it runs only in plan 01-13, after the
/// hands-off checkpoint. <c>--dry-run</c> touches nothing on the desktop: it starts the bar and a witness
/// that does not take the foreground, samples the probes and takes the snapshots once.
/// </summary>
[DriverCase("selftest")]
public sealed class OracleSelfTest : IDriverCase
{
    private const int NeededSentinelHits = 4;
    private const int SettleMs = 500;

    public int Run(CaseContext ctx) => ctx.DryRun ? RunDry(ctx) : RunReal(ctx);

    private static int RunDry(CaseContext ctx)
    {
        ForegroundInfo before = Foreground.Take();
        using HostProcess bar = ctx.StartHost("--variant H1 --sentinel on");
        using HostProcess witness = ctx.StartWitness("none", activate: false);
        Thread.Sleep(SettleMs);

        // Information only: a dry run must not change the foreground (the witness is started with --activate false).
        bool foregroundSame = Foreground.Take().Hwnd == before.Hwnd;
        Console.WriteLine($"foreground unchanged: {foregroundSame}");
        ctx.Log.Write("selftest", "dry", string.Empty, "foreground-unchanged", foregroundSame ? "true" : "false");

        (int X, int Y) origin = BarOrigin(bar);
        IReadOnlyList<(int X, int Y)> points = PixelProbe.ProbePoints(origin);
        int[] samples = PixelProbe.Sample(points);
        Report(ctx, "dry", points, samples);

        IReadOnlyList<ZEntry> snapshot = ZOrder.Snapshot(bar.Hwnd);
        string redactedPath = WriteSnapshots(ctx, "dry", snapshot);

        ZEntry? barEntry = snapshot.FirstOrDefault(e => e.IsBar);
        ZEntry? desktopHost = snapshot.FirstOrDefault(e => e.IsDesktopHost);
        bool ok = samples.Length == SpikeGeometry.ProbeOffsets.Count && barEntry != null && desktopHost != null;

        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"samples={samples.Length}/{SpikeGeometry.ProbeOffsets.Count} bar@{barEntry?.Index.ToString(CultureInfo.InvariantCulture) ?? "missing"} " +
            $"desktop-host={(desktopHost == null ? "missing" : desktopHost.ClassName + "@" + desktopHost.Index.ToString(CultureInfo.InvariantCulture))}"));
        Console.WriteLine($"redacted snapshot: {redactedPath}");

        if (!ok)
        {
            Console.WriteLine("SELFTEST DRY FAILED (probes not sampled, or the snapshot lacks the bar or the desktop host)");
            return ExitCodes.CriteriaFailed;
        }

        Console.WriteLine("SELFTEST DRY OK");
        return ExitCodes.Ok;
    }

    private static int RunReal(CaseContext ctx)
    {
        using SafetySession safety = ctx.BeginSafety(TimeSpan.FromMinutes(2));
        DesktopPrep.ClearTopArea(safety);

        using HostProcess bar = ctx.StartHost("--variant H1 --sentinel on");
        Safety.Wait(SettleMs);

        (int X, int Y) origin = BarOrigin(bar);
        IReadOnlyList<(int X, int Y)> points = PixelProbe.ProbePoints(origin);

        // (a) bar uncovered -> the sentinels must be readable.
        int[] uncovered = PixelProbe.Sample(points);
        int visible = PixelProbe.CountMatches(uncovered, SpikeGeometry.SentinelRgb);
        Report(ctx, "uncovered", points, uncovered);
        WriteSnapshots(ctx, "uncovered", ZOrder.Snapshot(bar.Hwnd));

        // (b) witness on top -> the cover colour must replace the sentinels at every probe.
        using HostProcess witness = ctx.StartWitness("full");
        bool foreground = DesktopPrep.BringToForeground(witness.Hwnd);
        ctx.Log.Write("selftest", "real", string.Empty, "witness-foreground", foreground ? "true" : "false");
        Safety.Wait(SettleMs);

        int[] covered = PixelProbe.Sample(points);
        int coveredHits = PixelProbe.CountMatches(covered, SpikeGeometry.CoverRgb);
        Report(ctx, "covered", points, covered);
        WriteSnapshots(ctx, "covered", ZOrder.Snapshot(bar.Hwnd));

        Console.WriteLine($"SELFTEST visible={visible}/5 covered={coveredHits}/5");
        ctx.Log.Write("selftest", "real", string.Empty, "result", $"visible={visible}/5 covered={coveredHits}/5 foreground={foreground}");

        bool ok = visible >= NeededSentinelHits && coveredHits == points.Count;
        if (ok)
        {
            return ExitCodes.Ok;
        }

        Console.WriteLine("SELFTEST FAILED (the pixel oracle does not separate visible from covered; see A1 in RESEARCH)");
        return ExitCodes.CriteriaFailed;
    }

    /// <summary>Screen position of the bar's top-left corner, read from the real window rectangle.</summary>
    private static (int X, int Y) BarOrigin(HostProcess bar)
    {
        ZEntry entry = ZOrder.Snapshot(bar.Hwnd).First(e => e.IsBar);
        return (entry.Rect.X, entry.Rect.Y);
    }

    /// <summary>Console + CSV rows per probe: colour read and, as an extra data point only, WindowFromPoint (assumption A2).</summary>
    private static void Report(CaseContext ctx, string phase, IReadOnlyList<(int X, int Y)> points, IReadOnlyList<int> samples)
    {
        for (int i = 0; i < points.Count; i++)
        {
            (HWND root, string className, uint pid) = ZOrder.WindowAt(points[i].X, points[i].Y);
            bool own = pid != 0 && ZOrder.IsOwnProcess(ZOrder.ProcessNameOf(pid));
            string detail = string.Create(
                CultureInfo.InvariantCulture,
                $"probe={i + 1} at={points[i].X},{points[i].Y} color={PixelProbe.ToHex(samples[i])} " +
                $"sentinel={PixelProbe.Matches(samples[i], SpikeGeometry.SentinelRgb)} cover={PixelProbe.Matches(samples[i], SpikeGeometry.CoverRgb)} " +
                $"windowfrompoint={ZOrder.SafeClass(className, own)}");
            Console.WriteLine($"{phase}: {detail}");
            ctx.Log.Write("selftest", phase, string.Empty, "probe", detail);
        }
    }

    /// <summary>Redacted snapshot next to the case log, full snapshot into the ignored private folder.</summary>
    private static string WriteSnapshots(CaseContext ctx, string phase, IReadOnlyList<ZEntry> snapshot)
    {
        string logPath = ctx.Log.Path;
        string redactedPath = Path.ChangeExtension(logPath, null) + $".zorder-{phase}.csv";
        File.WriteAllText(redactedPath, ZOrder.ToRedactedCsv(snapshot));

        string privateName = $"selftest-zorder-{phase}-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
        ZOrder.ToPrivateCsv(snapshot, privateName);
        return redactedPath;
    }
}
