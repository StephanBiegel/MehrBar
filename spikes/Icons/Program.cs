using System.Globalization;
using System.IO; // not an implicit using in WPF projects
using System.Text;
using MehrBar.Spikes.Common;

namespace MehrBar.Spikes.Icons;

/// <summary>One item type the bar must show, resolved to a shell parsing name.</summary>
/// <param name="Name">Case name as written to the CSV (never contains a path).</param>
/// <param name="Kind">Item type: exe, lnk, store, folder, url, pdf or net.</param>
/// <param name="Resolve">Returns the parsing name, or null when the fixture is missing.</param>
/// <param name="TimeoutMs">Hard per-item timeout.</param>
/// <param name="Private">True when the parsing name is a company/network path: it only goes to the private CSV.</param>
internal sealed record IconCase(string Name, string Kind, Func<string?> Resolve, int TimeoutMs, bool Private = false);

/// <summary>Everything measured for one case.</summary>
internal sealed record CaseOutcome(
    IconCase Case,
    string? ParsingName,
    int Requested,
    Apartment Apartment,
    IconResult Result,
    AlphaReport? Alpha,
    string Note);

internal sealed record Options
{
    public string? Case { get; init; }
    public bool All { get; init; }
    public int Size { get; init; } = 256;
    public string Threads { get; init; } = "mta";
    public int Reps { get; init; } = 20;
    public int Parallel { get; init; } = 8;
    public int? TimeoutMs { get; init; }
    public string? NetFile { get; init; }
    public string? Out { get; init; }
    public bool Sheet { get; init; }
    public bool Cold { get; init; }
    public bool Latency { get; init; }
}

internal static class Program
{
    public const string Header =
        "case,kind,requested,width,height,all_opaque,corner_alpha_max,straight_ratio,fringe,ms,thread,ok,note";

    private const string PrivateHeader = "case,path," +
        "kind,requested,width,height,all_opaque,corner_alpha_max,straight_ratio,fringe,ms,thread,ok,note";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    public static int Main(string[] args)
    {
        try
        {
            Options? options = ParseArgs(args);
            if (options == null)
            {
                return 2;
            }

            return Run(options);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("crash: " + ex.GetType().Name);
            return 2;
        }
    }

    private static int Run(Options o)
    {
        var catalogue = IconCases.Build(o.NetFile);

        if (o.Case != null)
        {
            IconCase? c = catalogue.FirstOrDefault(x => string.Equals(x.Name, o.Case, StringComparison.OrdinalIgnoreCase));
            if (c == null)
            {
                Console.Error.WriteLine("unknown case; known: " + string.Join(", ", catalogue.Select(x => x.Name)));
                return 2;
            }

            Apartment apartment = o.Threads.Equals("sta", StringComparison.OrdinalIgnoreCase) ? Apartment.Sta : Apartment.Mta;
            CaseOutcome outcome = Evaluate(c, o.Size, apartment, o.TimeoutMs);
            if (o.Cold)
            {
                Console.WriteLine(
                    "cold-ms=" + outcome.Result.Ms.ToString("F2", Inv) +
                    " ok=" + outcome.Result.Ok.ToString().ToLowerInvariant() +
                    " size=" + outcome.Result.Width + "x" + outcome.Result.Height);
            }
            else
            {
                Console.WriteLine(Describe(outcome));
            }

            if (o.Out != null)
            {
                WriteResults(new[] { outcome }, o, overwrite: false);
            }

            return outcome.Result.Ok ? 0 : 1;
        }

        Console.Error.WriteLine("nothing to do: use --case <name>");
        return 2;
    }

    /// <summary>Extracts one case and measures the alpha quality of the returned bitmap.</summary>
    public static CaseOutcome Evaluate(IconCase c, int size, Apartment apartment, int? timeoutOverrideMs)
    {
        string? name = c.Resolve();
        if (name == null)
        {
            return new CaseOutcome(c, null, size, apartment, new IconResult(0, 0, null, 0, "no-fixture"), null, "no-fixture");
        }

        int timeout = timeoutOverrideMs ?? c.TimeoutMs;
        IconResult result = ShellIcon.Get(name, size, TimeSpan.FromMilliseconds(timeout), apartment);
        if (!result.Ok)
        {
            string note = ClassifyError(c, result.Error);
            return new CaseOutcome(c, name, size, apartment, result, null, note);
        }

        AlphaReport alpha = IconMetrics.Measure(result.Bgra!, result.Width, result.Height);
        return new CaseOutcome(c, name, size, apartment, result, alpha, AlphaNote(result, alpha));
    }

    /// <summary>
    /// Error codes stay short. A network drive root that the shell reports as missing is recorded as
    /// not-connected (and never retried).
    /// </summary>
    private static string ClassifyError(IconCase c, string? error)
    {
        error ??= "unknown";
        if (c.Private && c.Name.StartsWith("net-root", StringComparison.Ordinal) &&
            (error == "hr=0x80070003" || error == "hr=0x8007000F" || error == "hr=0x80070002" ||
             error == "hr=0x800704C3" || error == "hr=0x8007052E"))
        {
            return "not-connected";
        }

        return error;
    }

    private static string AlphaNote(IconResult r, AlphaReport a)
    {
        var codes = new List<string>();
        if (r.BitsNote != null)
        {
            codes.Add(r.BitsNote);
        }

        if (a.AllZeroAlpha)
        {
            codes.Add("all-zero-alpha");
        }
        else if (a.AllOpaque)
        {
            codes.Add("opaque");
        }

        if (a.CornerAlphaMax > 0)
        {
            codes.Add("corner-box");
        }

        codes.Add(a.StraightAlphaRatio > 0 ? "straight" : "no-straight-evidence");
        codes.Add("edge=" + a.EdgeShare.ToString("F3", Inv));
        codes.Add("rawfringe=" + a.RawFringeScore.ToString("F1", Inv));
        return string.Join(' ', codes);
    }

    private static string Describe(CaseOutcome r)
    {
        var sb = new StringBuilder();
        sb.Append(r.Case.Name).Append(' ').Append(r.Apartment.ToString().ToUpperInvariant());
        sb.Append(r.Result.Ok ? " ok " : " FAIL ");
        sb.Append(r.Result.Width).Append('x').Append(r.Result.Height).Append(' ');
        sb.Append(r.Result.Ms.ToString("F1", Inv)).Append(" ms ").Append(r.Note);
        return sb.ToString();
    }

    private static string Bool(bool b) => b ? "true" : "false";

    private static string ThreadName(Apartment a) => a == Apartment.Sta ? "sta" : "mta";

    private static string FormatRow(CaseOutcome r)
    {
        AlphaReport? a = r.Alpha;
        return string.Join(
            ',',
            r.Case.Name,
            r.Case.Kind,
            r.Requested.ToString(Inv),
            r.Result.Width.ToString(Inv),
            r.Result.Height.ToString(Inv),
            a == null ? "" : Bool(a.AllOpaque),
            a == null ? "" : a.CornerAlphaMax.ToString(Inv),
            a == null ? "" : a.StraightAlphaRatio.ToString("F4", Inv),
            a == null ? "" : a.FringeScore.ToString("F3", Inv),
            r.Result.Ms.ToString("F2", Inv),
            ThreadName(r.Apartment),
            Bool(r.Result.Ok),
            r.Note);
    }

    /// <summary>
    /// Writes the redacted rows to <c>--out</c> and, for cases that touch network paths, the full parsing
    /// names to the private CSV. The committed CSV never carries a path column.
    /// </summary>
    public static void WriteResults(IReadOnlyList<CaseOutcome> outcomes, Options o, bool overwrite)
    {
        string path = Path.GetFullPath(o.Out ?? Path.Combine(RepoPaths.Results, "q6-icons.csv"));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        bool needHeader = overwrite || !File.Exists(path) || new FileInfo(path).Length == 0;
        var sb = new StringBuilder();
        if (needHeader)
        {
            sb.Append(Header).Append('\n');
        }

        foreach (CaseOutcome r in outcomes)
        {
            sb.Append(FormatRow(r)).Append('\n');
        }

        if (overwrite)
        {
            File.WriteAllText(path, sb.ToString());
        }
        else
        {
            File.AppendAllText(path, sb.ToString());
        }

        // Full paths (company shares, UNC names, user folders) live only in the ignored private folder.
        string privatePath = Path.Combine(RepoPaths.PrivateResults, "q6-icons-private.csv");
        bool privateHeader = overwrite || !File.Exists(privatePath) || new FileInfo(privatePath).Length == 0;
        var psb = new StringBuilder();
        if (privateHeader)
        {
            psb.Append(PrivateHeader).Append('\n');
        }

        foreach (CaseOutcome r in outcomes)
        {
            string row = FormatRow(r);
            int firstComma = row.IndexOf(',');
            string q = '"' + (r.ParsingName ?? "").Replace("\"", "\"\"") + '"';
            psb.Append(row, 0, firstComma).Append(',').Append(q).Append(row, firstComma, row.Length - firstComma).Append('\n');
        }

        if (overwrite)
        {
            File.WriteAllText(privatePath, psb.ToString());
        }
        else
        {
            File.AppendAllText(privatePath, psb.ToString());
        }
    }

    private static Options? ParseArgs(string[] args)
    {
        var o = new Options();
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            string? Next() => i + 1 < args.Length ? args[++i] : null;
            switch (a)
            {
                case "--case": o = o with { Case = Next() }; break;
                case "--all": o = o with { All = true }; break;
                case "--size": o = o with { Size = int.Parse(Next() ?? "256", Inv) }; break;
                case "--threads": o = o with { Threads = Next() ?? "mta" }; break;
                case "--reps": o = o with { Reps = int.Parse(Next() ?? "20", Inv) }; break;
                case "--parallel": o = o with { Parallel = int.Parse(Next() ?? "8", Inv) }; break;
                case "--timeout-ms": o = o with { TimeoutMs = int.Parse(Next() ?? "0", Inv) }; break;
                case "--netfile": o = o with { NetFile = Next() }; break;
                case "--out": o = o with { Out = Next() }; break;
                case "--sheet": o = o with { Sheet = true }; break;
                case "--cold": o = o with { Cold = true }; break;
                case "--latency": o = o with { Latency = true }; break;
                default:
                    Console.Error.WriteLine("unknown argument: " + a);
                    return null;
            }
        }

        return o;
    }
}

/// <summary>The case catalogue. Fixtures are generated by spikes/tools/make-icon-fixtures.ps1.</summary>
internal static class IconCases
{
    public static string FixtureDir => Path.Combine(RepoPaths.RepoRoot, "spikes", "fixtures", "icons");

    public static IReadOnlyList<IconCase> Build(string? netFile)
    {
        return new List<IconCase>
        {
            new("exe-notepad", "exe", () => Path.Combine(Environment.SystemDirectory, "notepad.exe"), 10000),
        };
    }
}
