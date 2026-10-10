using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MehrBar.Spikes.Common;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MehrBar.Spikes.Driver.Cases;

/// <summary>One row of the per-check CSV: variant,check,pass,ms,fg_before_class,fg_after_class,bar_zindex,notes.</summary>
internal sealed record MenuCheckResult(
    string Variant, string Check, bool Pass, long Ms, string FgBefore, string FgAfter, int BarZ, string Notes);

/// <summary>
/// Read side of a bar's host CSV for the menu case: remembers every row it has seen so that a check can wait for
/// "the next menu-open after index n" without losing rows to another reader.
/// </summary>
internal sealed class MenuTail
{
    private readonly HostProcess _host;
    private readonly List<HostRow> _rows = [];

    public MenuTail(HostProcess host) => _host = host;

    /// <summary>Number of rows seen so far; pass it as <c>since</c> to find only later rows.</summary>
    public int Count
    {
        get
        {
            Pump();
            return _rows.Count;
        }
    }

    /// <summary>True while the last menu-open/menu-close row is a menu-open.</summary>
    public bool IsOpen
    {
        get
        {
            Pump();
            for (int i = _rows.Count - 1; i >= 0; i--)
            {
                if (_rows[i].Event == "menu-open")
                {
                    return true;
                }

                if (_rows[i].Event == "menu-close")
                {
                    return false;
                }
            }

            return false;
        }
    }

    public IReadOnlyList<HostRow> Rows
    {
        get
        {
            Pump();
            return _rows;
        }
    }

    public void Pump() => _rows.AddRange(_host.ReadNewRows());

    public HostRow? Find(string evt, int since)
    {
        Pump();
        for (int i = since; i < _rows.Count; i++)
        {
            if (_rows[i].Event == evt)
            {
                return _rows[i];
            }
        }

        return null;
    }

    /// <summary>Waits until a row with <paramref name="evt"/> after index <paramref name="since"/> exists; null on timeout.</summary>
    /// <param name="clock">Stopwatch the caller started before the input, so that the time includes the input itself.</param>
    public HostRow? WaitFor(string evt, int since, Stopwatch clock, int timeoutMs)
    {
        while (true)
        {
            HostRow? row = Find(evt, since);
            if (row != null)
            {
                return row;
            }

            if (clock.ElapsedMilliseconds >= timeoutMs)
            {
                return null;
            }

            Safety.Wait(10);
        }
    }
}

/// <summary>
/// Q3 (SC3, D-15): which menu technique works at the non-activating bar. The case drives one variant at a time
/// against a real bar and a witness window and records for every point the plan requires whether it holds.
///
/// A real run (no flag) clears the desktop, sends real mouse and keyboard input and may activate windows; it runs
/// only in plan 01-15 after the hands-off checkpoint, inside a SafetySession. <c>--dry-run</c> sends nothing and
/// activates nothing: for M-A it lets the host open and close its menu by itself (<c>--selftest-menu</c>), for the
/// other variants it only starts the host and checks that the variant exists.
/// </summary>
[DriverCase("menu")]
public sealed class MenuCase : IDriverCase
{
    private const string Usage =
        "usage: Driver menu --variant M-A|M-B|M-C|M-D|all [--dry-run] [--manual --seconds N] [--restore-focus true] [--out csv] [--summary-md md]";

    private static readonly string[] s_allVariants = ["M-A", "M-B", "M-C", "M-D"];

    public int Run(CaseContext ctx)
    {
        string[] variants = ParseVariants(ctx.Get("variant"));
        if (variants.Length == 0)
        {
            Console.Error.WriteLine(Usage);
            return ExitCodes.Usage;
        }

        string resultPath = ctx.OutPath ?? Path.Combine(
            RepoPaths.DryResults, string.Create(CultureInfo.InvariantCulture, $"menu-{DateTime.Now:yyyyMMdd-HHmmss}.csv"));
        string eventsPath = Path.Combine(
            Path.GetDirectoryName(Path.GetFullPath(resultPath))!,
            Path.GetFileNameWithoutExtension(resultPath) + "-events.csv");

        var results = new List<MenuCheckResult>();
        int exit;
        using (var events = new RunLog(eventsPath))
        {
            exit = ctx.DryRun
                ? RunDry(ctx, variants, events, results)
                : RunReal(ctx, variants, events, results);
        }

        MenuResults.WriteCsv(resultPath, results);
        Console.WriteLine($"result: {Path.GetFullPath(resultPath)}");
        Console.WriteLine($"events: {Path.GetFullPath(eventsPath)}");
        if (ctx.SummaryMd != null)
        {
            MenuSummary.Write(ctx.SummaryMd, variants, results, ctx.DryRun);
            Console.WriteLine($"summary: {Path.GetFullPath(ctx.SummaryMd)}");
        }

        return exit;
    }

    private static string[] ParseVariants(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        if (text.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return s_allVariants;
        }

        string[] names = text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(n => n.ToUpperInvariant()).ToArray();
        return names.Length > 0 && names.All(n => s_allVariants.Contains(n, StringComparer.Ordinal)) ? names : [];
    }

    // ---- dry run -----------------------------------------------------------------------------------------

    private static int RunDry(CaseContext ctx, string[] variants, RunLog events, List<MenuCheckResult> results)
    {
        int failures = 0;
        foreach (string variant in variants)
        {
            MenuCheckResult result = DryVariant(ctx, events, variant);
            results.Add(result);
            if (result.Pass)
            {
                Console.WriteLine($"MENU DRY OK {variant}");
            }
            else
            {
                Console.WriteLine($"MENU DRY FAILED {variant}: {result.Notes}");
                failures++;
            }
        }

        return failures == 0 ? ExitCodes.Ok : ExitCodes.CriteriaFailed;
    }

    /// <summary>
    /// No input, nothing activated. M-A: the host opens and closes its menu on its own and the foreground window
    /// must not have become the host. Other variants: the host starts, announces <c>menu-ready</c> and refuses the self-test.
    /// </summary>
    private static MenuCheckResult DryVariant(CaseContext ctx, RunLog events, string variant)
    {
        ForegroundInfo before = Foreground.Take();
        using HostProcess bar = ctx.StartHost($"--variant H1,{variant} --selftest-menu true --exit-after-ms 2500");
        var tail = new MenuTail(bar);
        var notes = new List<string>();
        bool pass;
        long ms = 0;

        HostRow? ready = tail.WaitFor("menu-ready", 0, Stopwatch.StartNew(), 2000);
        if (ready == null)
        {
            pass = false;
            notes.Add("no menu-ready row: the variant is not registered or did not attach");
        }
        else if (variant == "M-A")
        {
            HostRow? open = tail.WaitFor("menu-open", 0, Stopwatch.StartNew(), 3000);
            HostRow? close = tail.WaitFor("menu-close", 0, Stopwatch.StartNew(), 3000);
            pass = open != null && close != null;
            ms = open != null && close != null ? close.TsMs - open.TsMs : 0;
            notes.Add(pass ? $"menu-open {open!.Detail}; menu-close {close!.Detail}" : "menu-open or menu-close row missing");
        }
        else
        {
            HostRow? refused = tail.WaitFor("menu-selftest-refused", 0, Stopwatch.StartNew(), 1500);
            bool opened = tail.Find("menu-open", 0) != null;
            pass = refused != null && !opened;
            notes.Add(pass ? "started, self-test refused (this variant activates a window), menu not opened" : "unexpected menu-open or no refusal row");
        }

        // The host must not take the foreground. Another window winning the foreground is the developer's own doing
        // and is reported, not failed; only the host itself becoming the foreground window fails the check.
        ForegroundInfo after = Foreground.Take();
        bool unchanged = after.Hwnd == before.Hwnd;
        bool hostForeground = after.Pid == bar.Pid;
        Console.WriteLine($"foreground unchanged: {unchanged}");
        if (hostForeground)
        {
            pass = false;
            notes.Add("the host became the foreground window");
        }
        else if (!unchanged)
        {
            notes.Add("foreground changed to a foreign window (developer switching windows?), not caused by the host");
        }

        var result = new MenuCheckResult(
            variant,
            "selftest",
            pass,
            ms,
            MenuRun.FgClass(before),
            MenuRun.FgClass(after),
            MenuRun.WindowsAbove(bar.Hwnd),
            string.Join("; ", notes));
        events.Write("menu", variant, "dry", pass ? "pass" : "fail", result.Notes);
        return result;
    }

    // ---- real run ----------------------------------------------------------------------------------------

    private static int RunReal(CaseContext ctx, string[] variants, RunLog events, List<MenuCheckResult> results)
    {
        using SafetySession safety = ctx.BeginSafety(TimeSpan.FromMinutes(5));
        DesktopPrep.ClearTopArea(safety);

        bool restoreFocus = ctx.Get("restore-focus") is { } value && value.Equals("true", StringComparison.OrdinalIgnoreCase);
        foreach (string variant in variants)
        {
            using MenuRun run = MenuRun.Start(ctx, events, variant, restoreFocus);
            results.AddRange(run.RunChecks());
        }

        return Evaluate(variants, results);
    }

    /// <summary>The mandatory points of D-15 (the bold ones in RESEARCH Q3); exit 0 when at least one variant passes them all.</summary>
    private static int Evaluate(string[] variants, IReadOnlyList<MenuCheckResult> results)
    {
        bool anyComplete = false;
        foreach (string variant in variants)
        {
            int mandatory = MenuRun.Mandatory.Count(c => results.Any(r => r.Variant == variant && r.Check == c && r.Pass));
            int total = results.Count(r => r.Variant == variant);
            int passed = results.Count(r => r.Variant == variant && r.Pass);
            Console.WriteLine($"MENU RESULT {variant} mandatory={mandatory}/{MenuRun.Mandatory.Length} all={passed}/{total}");
            anyComplete |= mandatory == MenuRun.Mandatory.Length;
        }

        Console.WriteLine(anyComplete
            ? "MENU DONE (at least one variant passes every mandatory point)"
            : "MENU DONE (no variant passes every mandatory point: D-15 temporary activation applies)");
        return anyComplete ? ExitCodes.Ok : ExitCodes.CriteriaFailed;
    }
}

/// <summary>
/// One variant under test: the witness window, the bar with the variant, the screen geometry derived from the bar's
/// real rectangle, and the checks. Disposing ends both processes.
/// </summary>
internal sealed class MenuRun : IDisposable
{
    /// <summary>The checks D-15 makes mandatory (RESEARCH Q3, bold points).</summary>
    public static readonly string[] Mandatory =
        ["close-desktop", "close-window", "close-bar", "close-escape", "keys-enter", "launch-front"];

    private const int OpenTimeoutMs = 1000;
    private const int CloseTimeoutMs = 1000;
    private const int NearPx = 8;

    private static readonly Regex s_position = new(@"^(?<x>-?\d+),(?<y>-?\d+)", RegexOptions.CultureInvariant);

    private readonly CaseContext _ctx;
    private readonly RunLog _events;
    private readonly string _variant;
    private readonly string _barArgs;
    private HostProcess _bar;
    private MenuTail _tail;

    private MenuRun(CaseContext ctx, RunLog events, string variant, string barArgs, HostProcess witness, HostProcess bar)
    {
        _ctx = ctx;
        _events = events;
        _variant = variant;
        _barArgs = barArgs;
        Witness = witness;
        _bar = bar;
        _tail = new MenuTail(bar);
        Origin = BarOrigin(bar);
    }

    public HostProcess Witness { get; }

    /// <summary>Screen position of the bar window's top-left corner.</summary>
    public (int X, int Y) Origin { get; private set; }

    /// <summary>Right-click point in the middle of the plate.</summary>
    public (int X, int Y) PlateCenter => (Origin.X + SpikeGeometry.Plate.X + (SpikeGeometry.Plate.Width / 2), Origin.Y + SpikeGeometry.Plate.Y + (SpikeGeometry.Plate.Height / 2));

    /// <summary>Another spot on the plate (left quarter).</summary>
    public (int X, int Y) PlateOther => (Origin.X + SpikeGeometry.Plate.X + (SpikeGeometry.Plate.Width / 6), PlateCenter.Y);

    public static MenuRun Start(CaseContext ctx, RunLog events, string variant, bool restoreFocus)
    {
        HostProcess witness = ctx.StartWitness("none");
        DesktopPrep.BringToForeground(witness.Hwnd);

        string args = $"--variant H1,{variant}" + (restoreFocus ? " --menu-restore-focus true" : string.Empty);
        HostProcess bar = ctx.StartHost(args);
        Safety.Wait(400);

        var run = new MenuRun(ctx, events, variant, args, witness, bar);

        // Registered after the host was started, so it runs first on an abort: a menu that is still open must
        // not be left on the developer's desktop (T-01-13). Killing the host afterwards would end it anyway.
        Safety.Current?.OnRestore($"close an open {variant} menu (Escape)", () =>
        {
            if (run._tail.IsOpen)
            {
                Input.Chord(Vk.Escape);
            }
        });
        return run;
    }

    public IReadOnlyList<MenuCheckResult> RunChecks() =>
    [
        Check("open", CheckOpen),
        Check("close-escape", CheckCloseEscape),
    ];

    public void Dispose()
    {
        _bar.Dispose();
        Witness.Dispose();
    }

    /// <summary>Foreground class that may be written to a committed file (own and shell classes only).</summary>
    public static string FgClass(ForegroundInfo info)
    {
        bool own = info.Pid != 0 && ZOrder.IsOwnProcess(ZOrder.ProcessNameOf(info.Pid));
        return info.Hwnd == HWND.Null ? "none" : ZOrder.SafeClass(info.Class, own);
    }

    /// <summary>Number of top-level windows above <paramref name="hwnd"/> (0 = topmost).</summary>
    public static int WindowsAbove(HWND hwnd)
    {
        int count = 0;
        for (HWND above = PInvoke.GetWindow(hwnd, GET_WINDOW_CMD.GW_HWNDPREV);
             above != HWND.Null;
             above = PInvoke.GetWindow(above, GET_WINDOW_CMD.GW_HWNDPREV))
        {
            count++;
        }

        return count;
    }

    // ---- check runner ------------------------------------------------------------------------------------

    private MenuCheckResult Check(string name, Func<(bool Pass, long Ms, string Notes)> body)
    {
        PrepareCheck();
        ForegroundInfo before = Foreground.Take();
        (bool pass, long ms, string notes) = body();
        ForegroundInfo after = Foreground.Take();
        int barZ = WindowsAbove(_bar.Hwnd);

        var result = new MenuCheckResult(_variant, name, pass, ms, FgClass(before), FgClass(after), barZ, notes);
        Console.WriteLine(string.Create(
            CultureInfo.InvariantCulture, $"{(pass ? "PASS" : "FAIL")} {_variant} {name}: {ms} ms; {notes}"));
        _events.Write("menu", _variant, string.Empty, pass ? "pass" : "fail", $"{name}: {ms} ms; {notes}");
        return result;
    }

    /// <summary>
    /// Brings the desktop into the starting state of a check: no menu open, the bar back at the bottom (the
    /// activating variants lift it, which would otherwise colour every later check), the witness in front.
    /// </summary>
    private void PrepareCheck()
    {
        ForceClose();

        if (BarLifted())
        {
            _events.Write("menu", _variant, string.Empty, "repin", "the bar was lifted by the previous check; put back to HWND_BOTTOM");
            PInvoke.SetWindowPos(
                _bar.Hwnd,
                HWND.HWND_BOTTOM,
                0,
                0,
                0,
                0,
                SET_WINDOW_POS_FLAGS.SWP_NOMOVE | SET_WINDOW_POS_FLAGS.SWP_NOSIZE | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
        }

        if (Foreground.Take().Hwnd != Witness.Hwnd)
        {
            DesktopPrep.BringToForeground(Witness.Hwnd);
        }

        Safety.Wait(150);
    }

    /// <summary>True when a visible window other than the bar lies below it, i.e. the bar was raised above it.</summary>
    private bool BarLifted() =>
        ZOrder.Below(_bar.Hwnd).Any(e => !e.IsBar && e.Visible && !e.Cloaked && !ZOrder.IsShellDesktopClass(e.ClassName));

    // ---- primitives --------------------------------------------------------------------------------------

    /// <summary>Right-clicks at <paramref name="point"/> and waits for the host's menu-open row.</summary>
    private (bool Opened, bool Near, long Ms, int Since, string Detail) OpenMenu((int X, int Y) point)
    {
        int since = _tail.Count;
        var clock = Stopwatch.StartNew();
        Input.RightClick(point.X, point.Y);
        HostRow? row = _tail.WaitFor("menu-open", since, clock, OpenTimeoutMs);
        long ms = clock.ElapsedMilliseconds;
        if (row == null)
        {
            return (false, false, ms, since, "no menu-open row within 1 s");
        }

        Match match = s_position.Match(row.Detail);
        bool near = match.Success &&
                    Math.Abs(int.Parse(match.Groups["x"].Value, CultureInfo.InvariantCulture) - point.X) <= NearPx &&
                    Math.Abs(int.Parse(match.Groups["y"].Value, CultureInfo.InvariantCulture) - point.Y) <= NearPx;
        return (true, near, ms, since, row.Detail);
    }

    /// <summary>
    /// Closes a menu a check left open (or one that refused to close): Escape, then a click on the desktop-side
    /// witness, and as the last resort a new bar. Never throws for "still open"; the check that caused it has failed already.
    /// </summary>
    private void ForceClose()
    {
        if (!_tail.IsOpen)
        {
            return;
        }

        int since = _tail.Count;
        Input.Chord(Vk.Escape);
        if (_tail.WaitFor("menu-close", since, Stopwatch.StartNew(), 500) != null)
        {
            return;
        }

        (int X, int Y) witnessCenter = WitnessCenter();
        Input.Click(witnessCenter.X, witnessCenter.Y);
        if (_tail.WaitFor("menu-close", since, Stopwatch.StartNew(), 500) != null)
        {
            return;
        }

        _events.Write("menu", _variant, string.Empty, "restart-bar", "the menu would not close; the bar is restarted");
        _bar.Kill();
        _bar = _ctx.StartHost(_barArgs);
        _tail = new MenuTail(_bar);
        Origin = BarOrigin(_bar);
    }

    public (int X, int Y) WitnessCenter()
    {
        PInvoke.GetWindowRect(Witness.Hwnd, out RECT rect);
        return ((rect.left + rect.right) / 2, (rect.top + rect.bottom) / 2);
    }

    private static (int X, int Y) BarOrigin(HostProcess bar)
    {
        PInvoke.GetWindowRect(bar.Hwnd, out RECT rect);
        return (rect.left, rect.top);
    }

    // ---- checks ------------------------------------------------------------------------------------------

    private (bool Pass, long Ms, string Notes) CheckOpen()
    {
        (bool opened, bool near, long ms, int _, string detail) = OpenMenu(PlateCenter);
        return (opened && near, ms, opened ? $"menu-open {detail}; near click point: {near}" : detail);
    }

    private (bool Pass, long Ms, string Notes) CheckCloseEscape()
    {
        (bool opened, _, _, int _, string detail) = OpenMenu(PlateCenter);
        if (!opened)
        {
            return (false, 0, "menu did not open: " + detail);
        }

        int since = _tail.Count;
        var clock = Stopwatch.StartNew();
        Input.Chord(Vk.Escape);
        HostRow? close = _tail.WaitFor("menu-close", since, clock, CloseTimeoutMs);
        return (close != null, clock.ElapsedMilliseconds, close != null ? close.Detail : "menu still open 1 s after Escape");
    }
}

/// <summary>CSV writer of the per-check rows (consumed by plan 01-15).</summary>
internal static class MenuResults
{
    public const string Header = "variant,check,pass,ms,fg_before_class,fg_after_class,bar_zindex,notes";

    public static void WriteCsv(string path, IReadOnlyList<MenuCheckResult> results)
    {
        var text = new StringBuilder();
        text.Append(Header).Append('\n');
        foreach (MenuCheckResult r in results)
        {
            text.Append(string.Join(
                ',',
                Quote(r.Variant),
                Quote(r.Check),
                r.Pass ? "true" : "false",
                r.Ms.ToString(CultureInfo.InvariantCulture),
                Quote(r.FgBefore),
                Quote(r.FgAfter),
                r.BarZ.ToString(CultureInfo.InvariantCulture),
                Quote(r.Notes))).Append('\n');
        }

        string? dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
    }

    private static string Quote(string field) =>
        field.IndexOfAny(['"', ',', '\r', '\n']) < 0 ? field : "\"" + field.Replace("\"", "\"\"") + "\"";
}

/// <summary>German summary table, one row per variant, one column per check of the interface block.</summary>
internal static class MenuSummary
{
    private const string Dash = "–";

    private static readonly (string Column, string Check)[] s_columns =
    [
        ("öffnet", "open"),
        ("Klick Desktop", "close-desktop"),
        ("Klick Fenster", "close-window"),
        ("Klick Leiste", "close-bar"),
        ("Escape", "close-escape"),
        ("Pfeile+Enter", "keys-enter"),
        ("Maus", "mouse-select"),
        ("Programm vorn", "launch-front"),
        ("2. Öffnen", "reopen"),
        ("Leiste unten", "z-during"),
        ("Fokus zurück", "focus-after"),
    ];

    public static void Write(string path, IReadOnlyList<string> variants, IReadOnlyList<MenuCheckResult> results, bool dry)
    {
        var text = new StringBuilder();
        text.Append("# Q3 Menütechnik – ").Append(dry ? "Trockenlauf (nichts gemessen)" : "Messung").Append("\n\n");
        text.Append("| Variante | ").Append(string.Join(" | ", s_columns.Select(c => c.Column))).Append(" |\n");
        text.Append("|---|").Append(string.Concat(Enumerable.Repeat("---|", s_columns.Length))).Append('\n');
        foreach (string variant in variants)
        {
            text.Append("| ").Append(variant);
            foreach ((string _, string check) in s_columns)
            {
                MenuCheckResult? r = results.FirstOrDefault(x => x.Variant == variant && x.Check == check);
                text.Append(" | ").Append(r == null ? Dash : Cell(r));
            }

            text.Append(" |\n");
        }

        text.Append('\n');
        if (dry)
        {
            foreach (MenuCheckResult r in results)
            {
                text.Append("- ").Append(r.Variant).Append(" (").Append(r.Check).Append("): ")
                    .Append(r.Pass ? "ok" : "Fehler").Append(" – ").Append(r.Notes).Append('\n');
            }
        }
        else
        {
            text.Append("✓ bestanden, ✗ nicht bestanden, Zeit bis zur Wirkung in ms. Pflichtpunkte (D-15): ")
                .Append("Klick Desktop, Klick Fenster, Klick Leiste, Escape, Pfeile+Enter, Programm vorn.\n");
        }

        string? dir = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(path, text.ToString(), new UTF8Encoding(false));
    }

    private static string Cell(MenuCheckResult r) =>
        (r.Pass ? "✓" : "✗") + (r.Ms > 0 ? string.Create(CultureInfo.InvariantCulture, $" {r.Ms} ms") : string.Empty);
}
