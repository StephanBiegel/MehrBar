using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using MehrBar.Spikes.Common;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
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

        if (ctx.Manual)
        {
            if (variants.Length != 1)
            {
                Console.Error.WriteLine("--manual needs exactly one variant");
                return ExitCodes.Usage;
            }

            using var manualEvents = new RunLog(eventsPath);
            return RunManual(ctx, variants[0], ctx.GetInt("seconds", 60), manualEvents);
        }

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

    // ---- manual observation ------------------------------------------------------------------------------

    /// <summary>
    /// Hand part (D-07): nothing is injected. The host runs with the variant while Stephan uses the real mouse and
    /// keyboard; every menu event of the host and every foreground change is printed and logged.
    /// </summary>
    private static int RunManual(CaseContext ctx, string variant, int seconds, RunLog events)
    {
        bool restoreFocus = ctx.Get("restore-focus") is { } value && value.Equals("true", StringComparison.OrdinalIgnoreCase);
        Console.WriteLine($"manual observation of {variant} for {seconds} s: right-click the bar and use the menu yourself");
        using HostProcess bar = ctx.StartHost($"--variant H1,{variant}" + (restoreFocus ? " --menu-restore-focus true" : string.Empty));
        var tail = new MenuTail(bar);
        var clock = Stopwatch.StartNew();
        ForegroundInfo last = Foreground.Take();
        int seen = 0;
        while (clock.Elapsed.TotalSeconds < seconds)
        {
            IReadOnlyList<HostRow> rows = tail.Rows;
            for (; seen < rows.Count; seen++)
            {
                HostRow row = rows[seen];
                if (row.Event.StartsWith("menu-", StringComparison.Ordinal) || row.Event.StartsWith("fg-", StringComparison.Ordinal))
                {
                    Console.WriteLine($"{row.TsMs} ms {row.Event} {row.Detail}");
                    events.Write("menu", variant, "manual", row.Event, row.Detail);
                }
            }

            ForegroundInfo now = Foreground.Take();
            if (now.Hwnd != last.Hwnd)
            {
                string detail = $"foreground now {MenuRun.FgClass(now)} pid={now.Pid}";
                Console.WriteLine(detail);
                events.Write("menu", variant, "manual", "foreground-change", detail);
                last = now;
            }

            Thread.Sleep(40);
        }

        Console.WriteLine($"MENU MANUAL DONE {variant}");
        return ExitCodes.Ok;
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
    private const int LaunchForegroundMs = 2000;
    private const int ReopenStayOpenMs = 500;
    private const int MenuItemCount = 4;
    private const ushort VkDown = 0x28;

    private static readonly Regex s_position = new(@"^(?<x>-?\d+),(?<y>-?\d+)", RegexOptions.CultureInvariant);

    private readonly CaseContext _ctx;
    private readonly RunLog _events;
    private readonly string _variant;
    private readonly string _barArgs;
    private readonly List<int> _launched = [];
    private HostProcess _bar;
    private MenuTail _tail;
    private int _lastOpenSince;
    private (int X, int Y) _lastOpenPoint;

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

    /// <summary>A desktop spot well away from the bar and the witness (right of the centre, below the middle).</summary>
    public static (int X, int Y) DesktopPoint =>
        (PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSCREEN) - 300, (PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYSCREEN) / 2) + 150);

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
        Safety.Current?.OnRestore("kill witnesses launched by menu item 3", run.KillLaunched);
        return run;
    }

    public IReadOnlyList<MenuCheckResult> RunChecks() =>
    [
        Check("open", CheckOpen),
        Check("close-desktop", () => CheckCloseByClick(DesktopPoint, "desktop")),
        Check("close-window", () => CheckCloseByClick(WitnessCenter(), "witness window")),
        Check("close-bar", () => CheckCloseByClick(PlateOther, "another spot on the bar")),
        Check("close-escape", CheckCloseEscape),
        Check("keys-enter", CheckKeysEnter),
        Check("mouse-select", CheckMouseSelect),
        Check("launch-front", CheckLaunchFront),
        Check("reopen", CheckReopen),
        Check("z-during", CheckZDuring),
        Check("focus-after", CheckFocusAfter),
    ];

    public void Dispose()
    {
        KillLaunched();
        _bar.Dispose();
        Witness.Dispose();
    }

    /// <summary>Ends every witness the menu launched (registered as a restore step, also run on Dispose).</summary>
    public void KillLaunched()
    {
        int[] pids;
        lock (_launched)
        {
            pids = _launched.ToArray();
            _launched.Clear();
        }

        foreach (int pid in pids)
        {
            try
            {
                using Process process = Process.GetProcessById(pid);
                // Only ever our own host: the menu starts nothing else.
                if (process.ProcessName.Equals("WindowHost", StringComparison.OrdinalIgnoreCase))
                {
                    process.Kill(entireProcessTree: true);
                    process.WaitForExit(2000);
                }
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
            {
                // already gone
            }
        }
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
        _lastOpenSince = since;
        _lastOpenPoint = point;
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
        if (!opened)
        {
            return (false, ms, detail);
        }

        // One picture of every open menu for Stephan's look at the optics (H4); private folder only.
        Safety.Wait(350);
        (int X, int Y) at = _lastOpenPoint;
        string file = $"q3-menu-{_variant}.bmp";
        bool saved = ScreenGrab.SaveBmp(Path.Combine(RepoPaths.PrivateResults, file), at.X - 220, at.Y - 20, 440, 260);
        return (near, ms, $"menu-open {detail}; near click point: {near}; screenshot {(saved ? file : "failed")}");
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

    private (bool Pass, long Ms, string Notes) CheckCloseByClick((int X, int Y) target, string what)
    {
        (bool opened, _, _, int _, string detail) = OpenMenu(PlateCenter);
        if (!opened)
        {
            return (false, 0, "menu did not open: " + detail);
        }

        // Let the popup settle (fade-in) so that the click is a click on the desktop, not on a half-drawn menu.
        Safety.Wait(300);
        int since = _tail.Count;
        var clock = Stopwatch.StartNew();
        Input.Click(target.X, target.Y);
        HostRow? close = _tail.WaitFor("menu-close", since, clock, CloseTimeoutMs);
        long ms = clock.ElapsedMilliseconds;
        return (close != null, ms, close != null ? $"closed by a click on the {what}: {close.Detail}" : $"menu still open 1 s after a click on the {what}");
    }

    private (bool Pass, long Ms, string Notes) CheckKeysEnter()
    {
        (bool opened, _, _, int _, string detail) = OpenMenu(PlateCenter);
        if (!opened)
        {
            return (false, 0, "menu did not open: " + detail);
        }

        Safety.Wait(300);
        int since = _tail.Count;
        var clock = Stopwatch.StartNew();
        Input.Chord(VkDown);
        Safety.Wait(80);
        Input.Chord(VkDown);
        Safety.Wait(80);
        Input.Chord(Vk.Return);
        HostRow? item = _tail.WaitFor("menu-item", since, clock, CloseTimeoutMs);
        long ms = clock.ElapsedMilliseconds;
        if (item == null)
        {
            return (false, ms, "no menu-item row after Down, Down, Return (the keys did not reach the menu)");
        }

        return (item.Detail == "id=2", ms, $"menu-item {item.Detail} (expected id=2)");
    }

    private (bool Pass, long Ms, string Notes) CheckMouseSelect()
    {
        (bool opened, _, _, int _, string detail) = OpenMenu(PlateCenter);
        if (!opened)
        {
            return (false, 0, "menu did not open: " + detail);
        }

        Safety.Wait(300);
        ((int X, int Y) point, string source) = ItemCenter(0);
        int since = _tail.Count;
        var clock = Stopwatch.StartNew();
        Input.Click(point.X, point.Y);
        HostRow? item = _tail.WaitFor("menu-item", since, clock, CloseTimeoutMs);
        long ms = clock.ElapsedMilliseconds;
        if (item == null)
        {
            return (false, ms, $"no menu-item row after a click on item 1 at {point.X},{point.Y} (rectangle: {source})");
        }

        return (item.Detail == "id=1", ms, $"menu-item {item.Detail} (expected id=1; rectangle: {source})");
    }

    private (bool Pass, long Ms, string Notes) CheckLaunchFront()
    {
        (bool opened, _, _, int _, string detail) = OpenMenu(PlateCenter);
        if (!opened)
        {
            return (false, 0, "menu did not open: " + detail);
        }

        Safety.Wait(300);
        ((int X, int Y) point, string source) = ItemCenter(2);
        int since = _tail.Count;
        var clock = Stopwatch.StartNew();
        Input.Click(point.X, point.Y);

        HostRow? launch = _tail.WaitFor("menu-launch", since, clock, CloseTimeoutMs);
        if (launch == null)
        {
            return (false, clock.ElapsedMilliseconds, $"item 3 did not launch anything (rectangle: {source})");
        }

        Match pidMatch = Regex.Match(launch.Detail, @"pid=(?<pid>\d+)", RegexOptions.CultureInvariant);
        if (!pidMatch.Success)
        {
            return (false, clock.ElapsedMilliseconds, "launch failed: " + launch.Detail);
        }

        int pid = int.Parse(pidMatch.Groups["pid"].Value, CultureInfo.InvariantCulture);
        lock (_launched)
        {
            _launched.Add(pid);
        }

        // The launched program has to come to the front within 2 s of the click.
        while (clock.ElapsedMilliseconds < LaunchForegroundMs)
        {
            if (Foreground.Take().Pid == (uint)pid)
            {
                long ms = clock.ElapsedMilliseconds;
                KillLaunched();
                return (true, ms, $"launched pid {pid} is the foreground window after {ms} ms (rectangle: {source})");
            }

            Safety.Wait(25);
        }

        ForegroundInfo now = Foreground.Take();
        KillLaunched();
        return (false, clock.ElapsedMilliseconds, $"launched pid {pid} did not reach the foreground within 2 s; foreground is {FgClass(now)} (rectangle: {source})");
    }

    private (bool Pass, long Ms, string Notes) CheckReopen()
    {
        (bool opened, _, _, int _, string detail) = OpenMenu(PlateCenter);
        if (!opened)
        {
            return (false, 0, "first open failed: " + detail);
        }

        long lastMs = 0;
        for (int attempt = 2; attempt <= 3; attempt++)
        {
            ForceClose();
            Safety.Wait(150);
            (bool again, _, long ms, int since, string againDetail) = OpenMenu(PlateCenter);
            lastMs = ms;
            if (!again)
            {
                return (false, ms, $"open #{attempt} failed: {againDetail}");
            }

            Safety.Wait(ReopenStayOpenMs);
            if (_tail.Find("menu-close", since) != null)
            {
                return (false, ms, $"open #{attempt} closed again within {ReopenStayOpenMs} ms (appears and disappears)");
            }
        }

        return (true, lastMs, $"opens #2 and #3 stayed open for at least {ReopenStayOpenMs} ms");
    }

    private (bool Pass, long Ms, string Notes) CheckZDuring()
    {
        int aboveBefore = WindowsAbove(_bar.Hwnd);
        bool liftedBefore = BarLifted();
        (bool opened, _, _, int _, string detail) = OpenMenu(PlateCenter);
        if (!opened)
        {
            return (false, 0, "menu did not open: " + detail);
        }

        Safety.Wait(300);
        int aboveDuring = WindowsAbove(_bar.Hwnd);
        bool liftedDuring = BarLifted();
        string hostZ = string.Join(" > ", _tail.Rows.Skip(_lastOpenSince).Where(r => r.Event == "menu-zpos").Select(r => r.Detail));

        ForceClose();
        Safety.Wait(300);
        bool liftedAfter = BarLifted();
        int aboveAfter = WindowsAbove(_bar.Hwnd);
        bool pass = !liftedBefore && !liftedDuring && !liftedAfter;
        string notes = string.Create(
            CultureInfo.InvariantCulture,
            $"windows-above-bar before={aboveBefore} during={aboveDuring} after={aboveAfter}; bar lifted above a visible window: before={liftedBefore} during={liftedDuring} after={liftedAfter}; host z-positions: {hostZ}");
        return (pass, 0, notes);
    }

    private (bool Pass, long Ms, string Notes) CheckFocusAfter()
    {
        (bool opened, _, _, int _, string detail) = OpenMenu(PlateCenter);
        if (!opened)
        {
            return (false, 0, "menu did not open: " + detail);
        }

        Safety.Wait(300);
        string closer = "escape";
        int since = _tail.Count;
        Input.Chord(Vk.Escape);
        if (_tail.WaitFor("menu-close", since, Stopwatch.StartNew(), 600) == null)
        {
            // Escape did not reach the menu; select item 1 with the mouse. A click elsewhere would move the focus by itself.
            closer = "click on item 1";
            ((int X, int Y) point, string _) = ItemCenter(0);
            Input.Click(point.X, point.Y);
            _tail.WaitFor("menu-close", since, Stopwatch.StartNew(), CloseTimeoutMs);
        }

        Safety.Wait(400);
        ForegroundInfo after = Foreground.Take();
        bool restored = after.Hwnd == Witness.Hwnd;
        return (restored, 0, $"closed by {closer}; foreground afterwards is {FgClass(after)} (expected: the witness window)");
    }

    /// <summary>
    /// Screen centre of menu item <paramref name="index"/>. Preferred source is the newest menu-items row the host logged
    /// at least 150 ms after the menu opened; if it is missing or implausible (off screen, far from the click) the
    /// native menu window (#32768) is split into equal rows, and as the last resort the position is estimated.
    /// </summary>
    private ((int X, int Y) Point, string Source) ItemCenter(int index)
    {
        var clock = Stopwatch.StartNew();
        HostRow? open = _tail.Rows.Skip(_lastOpenSince).FirstOrDefault(r => r.Event == "menu-open");
        long minTs = open == null ? 0 : open.TsMs + 150;
        HostRow? items = null;
        while (items == null && clock.ElapsedMilliseconds < 800)
        {
            items = _tail.Rows.Skip(_lastOpenSince).LastOrDefault(r => r.Event == "menu-items" && r.TsMs >= minTs);
            if (items == null)
            {
                Safety.Wait(25);
            }
        }

        items ??= _tail.Rows.Skip(_lastOpenSince).LastOrDefault(r => r.Event == "menu-items");
        if (items != null)
        {
            string[] rects = items.Detail.Split(';');
            if (index < rects.Length && TryParseRect(rects[index], out (int L, int T, int R, int B) rect))
            {
                (int x, int y) = ((rect.L + rect.R) / 2, (rect.T + rect.B) / 2);
                bool onScreen = rect.T >= 0 && rect.L >= 0 && x < PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSCREEN) && y < PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYSCREEN);
                bool near = Math.Abs(x - _lastOpenPoint.X) < 400 && Math.Abs(y - _lastOpenPoint.Y) < 400;
                if (onScreen && near)
                {
                    return ((x, y), "host menu-items row");
                }
            }
        }

        HWND menuWindow = PInvoke.FindWindow("#32768", null);
        if (menuWindow != HWND.Null && PInvoke.GetWindowRect(menuWindow, out RECT window) && window.bottom > window.top)
        {
            int height = window.bottom - window.top;
            return (((window.left + window.right) / 2, window.top + (height * ((2 * index) + 1) / (2 * MenuItemCount))), "estimate from the native menu window");
        }

        return ((_lastOpenPoint.X + 60, _lastOpenPoint.Y + 14 + (index * 24)), "estimate from the click point");
    }

    private static bool TryParseRect(string text, out (int L, int T, int R, int B) rect)
    {
        string[] parts = text.Split(',');
        rect = default;
        if (parts.Length != 4)
        {
            return false;
        }

        var values = new int[4];
        for (int i = 0; i < 4; i++)
        {
            if (!int.TryParse(parts[i], NumberStyles.Integer, CultureInfo.InvariantCulture, out values[i]))
            {
                return false;
            }
        }

        rect = (values[0], values[1], values[2], values[3]);
        return true;
    }
}

/// <summary>
/// Screenshot of a screen rectangle as a 32 bit BMP (BitBlt + GetDIBits, hand-written header, no imaging package).
/// Used for Stephan's look at the menu (H4); the files go to the ignored private results folder only.
/// </summary>
internal static unsafe class ScreenGrab
{
    private const int FileHeaderSize = 14;
    private const int InfoHeaderSize = 40;

    /// <summary>Saves the rectangle (clamped to the primary screen); returns false when nothing could be captured.</summary>
    public static bool SaveBmp(string path, int x, int y, int width, int height)
    {
        int screenWidth = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSCREEN);
        int screenHeight = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYSCREEN);
        x = Math.Clamp(x, 0, Math.Max(0, screenWidth - 1));
        y = Math.Clamp(y, 0, Math.Max(0, screenHeight - 1));
        width = Math.Min(width, screenWidth - x);
        height = Math.Min(height, screenHeight - y);
        if (width <= 0 || height <= 0)
        {
            return false;
        }

        HDC screen = PInvoke.GetDC(HWND.Null);
        HDC memory = PInvoke.CreateCompatibleDC(screen);
        HBITMAP bitmap = PInvoke.CreateCompatibleBitmap(screen, width, height);
        HGDIOBJ previous = PInvoke.SelectObject(memory, bitmap);
        try
        {
            if (!PInvoke.BitBlt(memory, 0, 0, width, height, screen, x, y, ROP_CODE.SRCCOPY | ROP_CODE.CAPTUREBLT))
            {
                return false;
            }

            var info = new BITMAPINFO();
            info.bmiHeader.biSize = (uint)sizeof(BITMAPINFOHEADER);
            info.bmiHeader.biWidth = width;
            info.bmiHeader.biHeight = -height; // top-down
            info.bmiHeader.biPlanes = 1;
            info.bmiHeader.biBitCount = 32;
            var pixels = new byte[width * height * 4];
            int lines;
            fixed (byte* bits = pixels)
            {
                lines = PInvoke.GetDIBits(memory, bitmap, 0, (uint)height, bits, &info, DIB_USAGE.DIB_RGB_COLORS);
            }

            if (lines != height)
            {
                return false;
            }

            for (int i = 3; i < pixels.Length; i += 4)
            {
                pixels[i] = 0xFF; // GDI leaves alpha at 0; viewers that honour it would show nothing
            }

            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryWriter(stream);
            writer.Write((byte)'B');
            writer.Write((byte)'M');
            writer.Write(FileHeaderSize + InfoHeaderSize + pixels.Length);
            writer.Write(0);
            writer.Write(FileHeaderSize + InfoHeaderSize);
            writer.Write(InfoHeaderSize);
            writer.Write(width);
            writer.Write(-height);
            writer.Write((short)1);
            writer.Write((short)32);
            writer.Write(0); // BI_RGB
            writer.Write(pixels.Length);
            writer.Write(2835);
            writer.Write(2835);
            writer.Write(0);
            writer.Write(0);
            writer.Write(pixels);
            return true;
        }
        finally
        {
            PInvoke.SelectObject(memory, previous);
            PInvoke.DeleteDC(memory);
            PInvoke.DeleteObject(bitmap);
            PInvoke.ReleaseDC(HWND.Null, screen);
        }
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
