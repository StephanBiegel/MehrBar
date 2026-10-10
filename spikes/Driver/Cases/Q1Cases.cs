using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MehrBar.Spikes.Common;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MehrBar.Spikes.Driver.Cases;

/// <summary>One planned click of the hit measurement; <see cref="Category"/> is "inside" or one of the outside categories.</summary>
internal sealed record PlannedClick(int Seq, bool Inside, string Category, int X, int Y);

/// <summary>
/// Builds the click plan of the hit measurement (RESEARCH Q1). Inside points are uniform in the plate, 2 px away
/// from its border. Outside points are split evenly over five categories: the pixel column or row directly
/// next to the plate on its four sides and the zoom-overhang gap (alpha 0 area below the plate where a zoomed
/// icon would stand). Inside and outside clicks alternate, and two consecutive outside clicks are never close
/// enough to be a double-click on the desktop (T-01-10).
/// </summary>
internal static class ClickPlanner
{
    public static readonly IReadOnlyList<string> OutsideCategories = ["left", "right", "above", "below", "gap"];

    /// <summary>Window-relative rectangle of the future zoom overhang: x 650..1250, y 70..240, alpha 0.</summary>
    public static IntRect OverhangGap { get; } = new(650, 70, 600, 170);

    /// <param name="screenPlate">The plate in screen pixels.</param>
    /// <param name="barOrigin">Screen position of the bar window's top-left corner (the gap is window-relative).</param>
    /// <param name="perGroup">Number of inside clicks; the same number of outside clicks is planned.</param>
    /// <param name="seed">Seed of the point generator; the same seed gives the same plan.</param>
    /// <param name="minDx">Minimum horizontal distance (SM_CXDOUBLECLK) between consecutive outside clicks...</param>
    /// <param name="minDy">...or the minimum vertical distance (SM_CYDOUBLECLK); one of the two suffices.</param>
    public static IReadOnlyList<PlannedClick> Build(
        IntRect screenPlate, (int X, int Y) barOrigin, int perGroup, int seed, int minDx, int minDy)
    {
        var rng = new Random(seed);

        var inside = new List<(int X, int Y)>(perGroup);
        for (int i = 0; i < perGroup; i++)
        {
            inside.Add((
                rng.Next(screenPlate.X + 2, screenPlate.Right - 2),
                rng.Next(screenPlate.Y + 2, screenPlate.Bottom - 2)));
        }

        var outside = new List<PlannedClick>(perGroup);
        for (int i = 0; i < perGroup; i++)
        {
            string category = OutsideCategories[i % OutsideCategories.Count];
            (int x, int y) = OutsidePoint(category, screenPlate, barOrigin, rng);
            outside.Add(new PlannedClick(0, false, category, x, y));
        }

        // Fisher-Yates, so that the categories do not appear in a fixed rotation.
        for (int i = outside.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (outside[i], outside[j]) = (outside[j], outside[i]);
        }

        IReadOnlyList<PlannedClick> spaced = SpaceOut(outside, minDx, minDy);

        var plan = new List<PlannedClick>(perGroup * 2);
        int seq = 1;
        for (int i = 0; i < perGroup; i++)
        {
            plan.Add(new PlannedClick(seq++, true, "inside", inside[i].X, inside[i].Y));
            plan.Add(spaced[i] with { Seq = seq++ });
        }

        return plan;
    }

    /// <summary>True when every pair of consecutive outside clicks differs by at least minDx or minDy.</summary>
    public static bool OutsideSpacingHolds(IReadOnlyList<PlannedClick> plan, int minDx, int minDy)
    {
        PlannedClick? previous = null;
        foreach (PlannedClick click in plan.Where(c => !c.Inside))
        {
            if (previous != null && !Far(previous, click, minDx, minDy))
            {
                return false;
            }

            previous = click;
        }

        return true;
    }

    private static (int X, int Y) OutsidePoint(string category, IntRect plate, (int X, int Y) origin, Random rng)
    {
        switch (category)
        {
            case "left":
                return (plate.X - 1, rng.Next(plate.Y, plate.Bottom));
            case "right":
                return (plate.Right, rng.Next(plate.Y, plate.Bottom));
            case "above":
                return (rng.Next(plate.X, plate.Right), plate.Y - 1);
            case "below":
                return (rng.Next(plate.X, plate.Right), plate.Bottom);
            default:
                IntRect gap = OverhangGap;
                return (
                    origin.X + rng.Next(gap.X, gap.Right),
                    origin.Y + rng.Next(gap.Y, gap.Bottom));
        }
    }

    private static List<PlannedClick> SpaceOut(List<PlannedClick> points, int minDx, int minDy)
    {
        var remaining = new List<PlannedClick>(points);
        var result = new List<PlannedClick>(points.Count);
        PlannedClick? previous = null;
        while (remaining.Count > 0)
        {
            int index = 0;
            if (previous != null)
            {
                PlannedClick last = previous;
                int found = remaining.FindIndex(candidate => Far(last, candidate, minDx, minDy));
                index = found < 0 ? 0 : found;
            }

            previous = remaining[index];
            result.Add(previous);
            remaining.RemoveAt(index);
        }

        return result;
    }

    private static bool Far(PlannedClick a, PlannedClick b, int minDx, int minDy) =>
        Math.Abs(a.X - b.X) >= minDx || Math.Abs(a.Y - b.Y) >= minDy;
}

/// <summary>Counters and notes of one variant in the hit case (feeds the summary markdown).</summary>
internal sealed class VariantResult
{
    public VariantResult(string variant)
    {
        Variant = variant;
    }

    public string Variant { get; }

    public bool Dry { get; set; }

    public int InsidePlanned { get; set; }

    public int InsideClicked { get; set; }

    public int Captured { get; set; }

    public int OutsidePlanned { get; set; }

    public int OutsideClicked { get; set; }

    public int PassedThrough { get; set; }

    /// <summary>Outside clicks after which the foreground had left the witness (positive evidence of the pass-through).</summary>
    public int PassEvidence { get; set; }

    /// <summary>Inside clicks that started with the witness in the foreground (denominator of "Fokus gehalten").</summary>
    public int FocusMeasured { get; set; }

    public int FocusKept { get; set; }

    public bool MovesMeasured { get; set; }

    public long MovesInside { get; set; }

    public long MovesOutside { get; set; }

    /// <summary>Per outside category: (clicks sent, clicks that passed through).</summary>
    public Dictionary<string, (int Clicked, int Passed)> ByCategory { get; } = new(StringComparer.Ordinal);

    public List<string> Notes { get; } = [];
}

/// <summary>Writes the German summary table that is pasted into the Q1 note.</summary>
internal static class Q1Summary
{
    private const string Dash = "–";

    public static void Write(string path, IReadOnlyList<VariantResult> results, int clicks, int seed, bool dry)
    {
        var text = new StringBuilder();
        text.Append("# Q1 Trefferfläche – ").Append(dry ? "Trockenlauf (nichts gemessen)" : "Messung").Append('\n');
        text.Append('\n');
        text.Append(string.Create(
            CultureInfo.InvariantCulture,
            $"Klicks je Gruppe: {clicks} (innen) und {clicks} (außen), Seed {seed}.\n\n"));
        text.Append("| Variante | innen geklickt | gefangen | außen geklickt | durchgelassen | Bewegung erkannt | Fokus gehalten | Bemerkung |\n");
        text.Append("|---|---|---|---|---|---|---|---|\n");
        foreach (VariantResult r in results)
        {
            text.Append("| ").Append(r.Variant);
            text.Append(" | ").Append(r.Dry ? Planned(r.InsideClicked, r.InsidePlanned) : r.InsideClicked.ToString(CultureInfo.InvariantCulture));
            text.Append(" | ").Append(r.Dry ? Dash : Ratio(r.Captured, r.InsideClicked));
            text.Append(" | ").Append(r.Dry ? Planned(r.OutsideClicked, r.OutsidePlanned) : r.OutsideClicked.ToString(CultureInfo.InvariantCulture));
            text.Append(" | ").Append(r.Dry ? Dash : Ratio(r.PassedThrough, r.OutsideClicked));
            text.Append(" | ").Append(r.MovesMeasured
                ? string.Create(CultureInfo.InvariantCulture, $"innen {r.MovesInside}, außen {r.MovesOutside}")
                : Dash);
            text.Append(" | ").Append(r.Dry ? Dash : Ratio(r.FocusKept, r.FocusMeasured));
            text.Append(" | ").Append(Escape(string.Join("; ", r.Notes)));
            text.Append(" |\n");
        }

        if (results.Any(r => r.ByCategory.Count > 0))
        {
            text.Append("\n### Außen je Kategorie (durchgelassen / geklickt)\n\n");
            text.Append("| Variante | links | rechts | oben | unten | Überhang |\n");
            text.Append("|---|---|---|---|---|---|\n");
            foreach (VariantResult r in results)
            {
                text.Append("| ").Append(r.Variant);
                foreach (string category in ClickPlanner.OutsideCategories)
                {
                    text.Append(" | ").Append(r.ByCategory.TryGetValue(category, out (int Clicked, int Passed) c)
                        ? Ratio(c.Passed, c.Clicked)
                        : Dash);
                }

                text.Append(" |\n");
            }
        }

        string? directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
    }

    private static string Ratio(int part, int whole) =>
        string.Create(CultureInfo.InvariantCulture, $"{part}/{whole}");

    private static string Planned(int sent, int planned) =>
        string.Create(CultureInfo.InvariantCulture, $"{sent} (geplant {planned})");

    private static string Escape(string note) =>
        note.Length == 0 ? Dash : note.Replace("|", "/", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
}

/// <summary>
/// Q1 hit-area measurement (SC1, D-14): which clicks does the bar catch and which pass through to the desktop
/// beneath it? Real runs send input and therefore run only in plan 01-13, after the hands-off checkpoint;
/// <c>--dry-run</c> builds the plan and exercises the pipeline without any input, <c>--manual</c> only observes.
/// </summary>
[DriverCase("hit")]
public sealed class HitCase : IDriverCase
{
    private const int ClickSettleMs = 250;
    private const int SweepSteps = 24;
    private const int SweepSettleMs = 700;
    private const int ClickPositionTolerance = 2;
    private const int ExtraDoubleClickGapMs = 100;

    private static readonly Regex s_moveCount = new(@"move=(\d+)", RegexOptions.CultureInvariant);

    private static readonly string[] s_supportedVariants = ["H1"];

    public int Run(CaseContext ctx)
    {
        string[] variants = (ctx.Get("variants") ?? "H1")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(v => v.ToUpperInvariant())
            .ToArray();
        int clicks = ctx.GetInt("clicks", 100);
        int seed = ctx.GetInt("seed", 1);
        if (variants.Length == 0 || clicks < 1)
        {
            Console.Error.WriteLine("usage: Driver hit --variants H1,H2,H3 --clicks N --seed N [--dry-run] [--manual --seconds N] [--out csv] [--summary-md md]");
            return ExitCodes.Usage;
        }

        foreach (string variant in variants)
        {
            if (!s_supportedVariants.Contains(variant, StringComparer.Ordinal))
            {
                Console.Error.WriteLine($"variant '{variant}' is not supported by the hit case; supported: {string.Join(", ", s_supportedVariants)}");
                return ExitCodes.Usage;
            }
        }

        if (ctx.Manual)
        {
            return RunManual(ctx, variants[0], ctx.GetInt("seconds", 60));
        }

        return RunAll(ctx, variants, clicks, seed);
    }

    /// <summary>WindowHost arguments of a bar under test; the sentinel squares are off, the plate is invisible.</summary>
    internal static string HostArgs(string variant) => $"--variant {variant} --sentinel off";

    private static int RunAll(CaseContext ctx, IReadOnlyList<string> variants, int clicks, int seed)
    {
        bool real = !ctx.DryRun;
        int minDx = Math.Max(1, PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXDOUBLECLK));
        int minDy = Math.Max(1, PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYDOUBLECLK));
        int spacingMs = checked((int)PInvoke.GetDoubleClickTime()) + ExtraDoubleClickGapMs;
        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"double-click rectangle {minDx}x{minDy} px, double-click time {spacingMs - ExtraDoubleClickGapMs} ms; click spacing {spacingMs} ms"));

        var results = new List<VariantResult>();
        SafetySession? safety = null;
        try
        {
            if (real)
            {
                // Per click: spacing + settle + foreground re-assert; per variant: start-up and two sweeps.
                long estimateMs = variants.Count * ((2L * clicks * (spacingMs + ClickSettleMs + 400)) + 30_000);
                TimeSpan timeout = TimeSpan.FromMilliseconds(Math.Max(TimeSpan.FromMinutes(10).TotalMilliseconds, estimateMs + 120_000));
                safety = ctx.BeginSafety(timeout);
                DesktopPrep.ClearTopArea(safety);
            }

            // The witness stands for "another application" that keeps the foreground. Dry runs show it without activating it.
            using HostProcess witness = ctx.StartWitness("none", activate: real);
            if (real && !DesktopPrep.BringToForeground(witness.Hwnd))
            {
                Console.WriteLine("WARNING witness did not become the foreground window; focus numbers will be incomplete");
            }

            foreach (string variant in variants)
            {
                VariantResult result = real
                    ? MeasureVariant(ctx, variant, clicks, seed, witness, minDx, minDy, spacingMs)
                    : DryVariant(ctx, variant, clicks, seed, witness, minDx, minDy);
                results.Add(result);
                if (result.Notes.Any(n => n.StartsWith("HARNESS", StringComparison.Ordinal)))
                {
                    Console.WriteLine($"HIT FAILED {variant} ({string.Join("; ", result.Notes)})");
                    return ExitCodes.CriteriaFailed;
                }
            }
        }
        finally
        {
            safety?.Dispose();
        }

        if (ctx.SummaryMd != null)
        {
            Q1Summary.Write(ctx.SummaryMd, results, clicks, seed, ctx.DryRun);
            Console.WriteLine($"summary: {Path.GetFullPath(ctx.SummaryMd)}");
        }

        Console.WriteLine($"log: {ctx.Log.Path}");
        return ExitCodes.Ok;
    }

    private static VariantResult DryVariant(
        CaseContext ctx, string variant, int clicks, int seed, HostProcess witness, int minDx, int minDy)
    {
        var result = new VariantResult(variant) { Dry = true, InsidePlanned = clicks, OutsidePlanned = clicks };

        ForegroundInfo foregroundBefore = Foreground.Take();
        using HostProcess bar = ctx.StartHost(HostArgs(variant));
        Thread.Sleep(300);

        (IntRect plate, (int X, int Y) origin) = BarGeometry(bar);
        IReadOnlyList<PlannedClick> plan = ClickPlanner.Build(plate, origin, clicks, seed, minDx, minDy);
        foreach (PlannedClick click in plan)
        {
            ctx.Log.Write("hit", variant, click.Category, "planned-click", ClickDetail(click));
        }

        result.InsideClicked = 0;
        result.OutsideClicked = 0;

        // One foreground and one z-order sample; nothing is sent and no window is changed.
        ForegroundInfo foreground = Foreground.Take();
        IReadOnlyList<ZEntry> snapshot = ZOrder.Snapshot(bar.Hwnd);
        ZEntry? barEntry = snapshot.FirstOrDefault(e => e.IsBar);
        ZEntry? desktopHost = snapshot.FirstOrDefault(e => e.IsDesktopHost);
        bool foregroundSame = foreground.Hwnd == foregroundBefore.Hwnd;
        ctx.Log.Write("hit", variant, string.Empty, "dry-sample", string.Create(
            CultureInfo.InvariantCulture,
            $"foreground={FgClass(foreground)} unchanged={foregroundSame} bar-index={barEntry?.Index.ToString(CultureInfo.InvariantCulture) ?? "missing"} witness=0x{ZOrder.Raw(witness.Hwnd):X}"));

        int insideCount = plan.Count(c => c.Inside);
        int outsideCount = plan.Count - insideCount;
        int categories = plan.Where(c => !c.Inside).Select(c => c.Category).Distinct(StringComparer.Ordinal).Count();
        bool spacing = ClickPlanner.OutsideSpacingHolds(plan, minDx, minDy);
        bool ok = barEntry != null && desktopHost != null && spacing && insideCount == clicks && outsideCount == clicks;

        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{variant}: planned {insideCount} inside + {outsideCount} outside clicks in {categories} outside categories, outside spacing ok={spacing}, bar@{barEntry?.Index.ToString(CultureInfo.InvariantCulture) ?? "missing"}, foreground unchanged={foregroundSame}"));
        result.Notes.Add(string.Create(CultureInfo.InvariantCulture, $"Trockenlauf, {categories} Außenkategorien geplant"));

        if (!ok)
        {
            result.Notes.Add("HARNESS: dry-run sample incomplete (bar or desktop host missing, or plan malformed)");
            return result;
        }

        Console.WriteLine($"HIT DRY OK {variant}");
        return result;
    }

    private static VariantResult MeasureVariant(
        CaseContext ctx, string variant, int clicks, int seed, HostProcess witness, int minDx, int minDy, int spacingMs)
    {
        var result = new VariantResult(variant) { InsidePlanned = clicks, OutsidePlanned = clicks };

        using HostProcess bar = ctx.StartHost(HostArgs(variant));
        Safety.Wait(500);

        (IntRect plate, (int X, int Y) origin) = BarGeometry(bar);
        IReadOnlyList<PlannedClick> plan = ClickPlanner.Build(plate, origin, clicks, seed, minDx, minDy);
        var sinceLastClick = new Stopwatch();

        foreach (PlannedClick click in plan)
        {
            // Wait first: any two clicks are at least double-click time + 100 ms apart, so no pair can ever be a double-click.
            if (sinceLastClick.IsRunning)
            {
                Safety.Wait(spacingMs - (int)sinceLastClick.ElapsedMilliseconds);
            }

            bool foregroundBefore = DesktopPrep.BringToForeground(witness.Hwnd);
            bar.ReadNewRows();

            string wfp = string.Empty;
            if (!click.Inside)
            {
                // Extra data point only (assumption A2): which top-level window does WindowFromPoint report here?
                (_, string className, uint pid) = ZOrder.WindowAt(click.X, click.Y);
                wfp = ZOrder.SafeClass(className, pid != 0 && ZOrder.IsOwnProcess(ZOrder.ProcessNameOf(pid)));
            }

            Input.Click(click.X, click.Y);
            sinceLastClick.Restart();
            Safety.Wait(ClickSettleMs);

            IReadOnlyList<HostRow> rows = bar.ReadNewRows();
            bool arrived = rows.Any(r => r.Event == "lbuttondown" && IsNear(r.Detail, click.X, click.Y));
            ForegroundInfo foregroundAfter = Foreground.Take();
            bool witnessStillForeground = foregroundAfter.Hwnd == witness.Hwnd;

            if (click.Inside)
            {
                result.InsideClicked++;
                if (arrived)
                {
                    result.Captured++;
                }

                if (foregroundBefore)
                {
                    result.FocusMeasured++;
                    if (witnessStillForeground)
                    {
                        result.FocusKept++;
                    }
                }
            }
            else
            {
                result.OutsideClicked++;
                bool passed = !arrived;
                (int clicked, int passedCount) = result.ByCategory.GetValueOrDefault(click.Category);
                result.ByCategory[click.Category] = (clicked + 1, passedCount + (passed ? 1 : 0));
                if (passed)
                {
                    result.PassedThrough++;
                    if (!witnessStillForeground)
                    {
                        result.PassEvidence++;
                    }
                }
            }

            ctx.Log.Write("hit", variant, click.Category, "click", string.Create(
                CultureInfo.InvariantCulture,
                $"{ClickDetail(click)} fg-before={foregroundBefore} arrived={arrived} fg-after={FgClass(foregroundAfter)} witness-foreground={witnessStillForeground} windowfrompoint={(wfp.Length == 0 ? "-" : wfp)}"));
        }

        (long inside, long outside) = Sweep(bar, plate);
        result.MovesMeasured = true;
        result.MovesInside = inside;
        result.MovesOutside = outside;
        ctx.Log.Write("hit", variant, string.Empty, "sweep", string.Create(CultureInfo.InvariantCulture, $"moves-inside={inside} moves-outside={outside}"));

        result.Notes.Add(string.Create(
            CultureInfo.InvariantCulture,
            $"Durchlass durch Vordergrundwechsel bestätigt {result.PassEvidence}/{result.PassedThrough}"));
        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture,
            $"{variant}: captured {result.Captured}/{result.InsideClicked}, passed through {result.PassedThrough}/{result.OutsideClicked} (foreground evidence {result.PassEvidence}), focus kept {result.FocusKept}/{result.FocusMeasured}, moves inside {inside} outside {outside}"));
        return result;
    }

    /// <summary>Mouse moves across the plate and, separately, along the overhang gap below it; counts host WM_MOUSEMOVE deltas.</summary>
    private static (long Inside, long Outside) Sweep(HostProcess bar, IntRect plate)
    {
        long start = MoveCount(bar);

        int yInside = plate.Y + (plate.Height / 2);
        for (int step = 0; step < SweepSteps; step++)
        {
            Input.MoveAbs(plate.X + 10 + ((plate.Width - 20) * step / (SweepSteps - 1)), yInside);
            Safety.Wait(20);
        }

        Safety.Wait(SweepSettleMs);
        long afterInside = MoveCount(bar);

        int yOutside = plate.Bottom + 40;
        for (int step = 0; step < SweepSteps; step++)
        {
            Input.MoveAbs(plate.X + 10 + ((plate.Width - 20) * step / (SweepSteps - 1)), yOutside);
            Safety.Wait(20);
        }

        Safety.Wait(SweepSettleMs);
        long afterOutside = MoveCount(bar);
        return (afterInside - start, afterOutside - afterInside);
    }

    private static int RunManual(CaseContext ctx, string variant, int seconds)
    {
        Console.WriteLine($"manual observation of {variant} for {seconds} s; nothing is injected, use the mouse yourself");
        using HostProcess bar = ctx.StartHost(HostArgs(variant));
        using HostProcess witness = ctx.StartWitness("none", activate: false);

        int left = 0;
        int right = 0;
        int activate = 0;
        var clock = Stopwatch.StartNew();
        while (clock.Elapsed.TotalSeconds < seconds)
        {
            foreach (HostRow row in bar.ReadNewRows())
            {
                if (row.Event is not ("lbuttondown" or "rbuttondown" or "mouseactivate"))
                {
                    continue;
                }

                // The foreground at the moment the host reported the click (read right after the row appeared).
                ForegroundInfo foreground = Foreground.Take();
                switch (row.Event)
                {
                    case "lbuttondown":
                        left++;
                        break;
                    case "rbuttondown":
                        right++;
                        break;
                    default:
                        activate++;
                        break;
                }

                string detail = string.Create(
                    CultureInfo.InvariantCulture,
                    $"{row.Event} at={row.Detail.Replace(',', ':')} foreground={FgClass(foreground)} witness-foreground={foreground.Hwnd == witness.Hwnd} totals left={left} right={right} mouseactivate={activate}");
                Console.WriteLine(detail);
                ctx.Log.Write("hit", variant, string.Empty, "manual-click", detail);
            }

            Thread.Sleep(40);
        }

        Console.WriteLine($"HIT MANUAL DONE {variant} left={left} right={right} mouseactivate={activate}");
        return ExitCodes.Ok;
    }

    /// <summary>Bar origin and plate in screen pixels, read from the real window rectangle.</summary>
    private static (IntRect Plate, (int X, int Y) Origin) BarGeometry(HostProcess bar)
    {
        if (!PInvoke.GetWindowRect(bar.Hwnd, out RECT rect))
        {
            (int fallbackX, int fallbackY) = SpikeGeometry.DefaultWindowPos();
            return (SpikeGeometry.Plate.Offset(fallbackX, fallbackY), (fallbackX, fallbackY));
        }

        return (SpikeGeometry.Plate.Offset(rect.left, rect.top), (rect.left, rect.top));
    }

    private static bool IsNear(string position, int x, int y)
    {
        string[] parts = position.Split(',');
        return parts.Length == 2 &&
               int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int px) &&
               int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int py) &&
               Math.Abs(px - x) <= ClickPositionTolerance && Math.Abs(py - y) <= ClickPositionTolerance;
    }

    /// <summary>Latest WM_MOUSEMOVE total the host reported in its aggregate rows.</summary>
    private static long MoveCount(HostProcess bar)
    {
        HostRow? last = bar.ReadAllRows().LastOrDefault(r => r.Event == "counters");
        if (last == null)
        {
            return 0;
        }

        Match match = s_moveCount.Match(last.Detail);
        return match.Success ? long.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : 0;
    }

    private static string ClickDetail(PlannedClick click) =>
        string.Create(
            CultureInfo.InvariantCulture,
            $"seq={click.Seq} kind={(click.Inside ? "inside" : "outside")} category={click.Category} x={click.X} y={click.Y}");

    /// <summary>Class of the foreground window in a form that may be committed (foreign windows are "other").</summary>
    private static string FgClass(ForegroundInfo foreground) =>
        ZOrder.SafeClass(
            foreground.Class,
            foreground.Pid != 0 && ZOrder.IsOwnProcess(ZOrder.ProcessNameOf(foreground.Pid)));
}
