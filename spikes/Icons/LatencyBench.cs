using System.Diagnostics;
using System.Globalization;
using System.IO; // not an implicit using in WPF projects
using System.Text;
using MehrBar.Spikes.Common;

namespace MehrBar.Spikes.Icons;

/// <summary>
/// Latency and threading measurement for Q6. Times are the time spent inside the extraction worker (COM
/// initialisation, SHCreateItemFromParsingName, GetImage, pixel read), which is what a dedicated IconCache
/// worker would pay per icon; creating the worker thread is not included. Wall times are used for the
/// parallel and timeout modes.
/// </summary>
internal static class LatencyBench
{
    public const string Header = "case,thread,mode,n,median_ms,p95_ms,max_ms,errors,size,hash";

    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
    private static readonly string[] ColdCases = { "exe-notepad", "folder", "lnk-store" };
    private static readonly string[] WarmCases =
        { "exe-notepad", "folder", "lnk-store", "exe-only256", "pdf", "url-plain", "url-iconfile" };
    private static readonly string[] ParallelCases =
        { "exe-notepad", "exe-only48", "exe-only256", "lnk-file", "lnk-store", "appsfolder-store", "folder", "pdf" };

    private sealed record Sample(double Ms, bool Ok, string Size, string Hash);

    public static int Run(Options o, IReadOnlyList<IconCase> catalogue)
    {
        string outPath = Path.GetFullPath(o.Out ?? Path.Combine(RepoPaths.Results, "q6-latency.csv"));
        Directory.CreateDirectory(Path.GetDirectoryName(outPath)!);
        var rows = new List<string>();
        IconCase Find(string name) => catalogue.First(c => c.Name == name);

        // Cold: the first call after process start, 5 fresh child processes per item and apartment.
        foreach (string name in ColdCases)
        {
            foreach (Apartment apartment in new[] { Apartment.Sta, Apartment.Mta })
            {
                var samples = new List<Sample>();
                for (int i = 0; i < 5; i++)
                {
                    samples.Add(ColdChild(name, apartment));
                }

                rows.Add(Row(name, apartment, "cold", samples));
                Console.WriteLine(rows[^1]);
            }
        }

        // Warm: one discarded call, then --reps timed calls in this process, per apartment.
        foreach (string name in WarmCases)
        {
            IconCase c = Find(name);
            foreach (Apartment apartment in new[] { Apartment.Sta, Apartment.Mta })
            {
                Once(c, o.Size, apartment); // warm-up
                var samples = new List<Sample>();
                for (int i = 0; i < o.Reps; i++)
                {
                    samples.Add(Once(c, o.Size, apartment));
                }

                rows.Add(Row(name, apartment, "warm", samples));
                Console.WriteLine(rows[^1]);
            }
        }

        // Parallel: eight different items at once on worker threads versus one after the other (wall time).
        var parallelCases = ParallelCases.Take(o.Parallel).Select(Find).ToList();
        foreach (Apartment apartment in new[] { Apartment.Mta, Apartment.Sta })
        {
            foreach (IconCase c in parallelCases)
            {
                Once(c, o.Size, apartment); // all warm before timing
            }

            var concurrent = new List<Sample>();
            var sequential = new List<Sample>();
            for (int rep = 0; rep < 5; rep++)
            {
                var wall = Stopwatch.StartNew();
                var tasks = parallelCases
                    .Select(c => Task.Factory.StartNew(() => Once(c, o.Size, apartment), TaskCreationOptions.LongRunning))
                    .ToArray();
                Task.WaitAll(tasks);
                concurrent.Add(new Sample(wall.Elapsed.TotalMilliseconds, tasks.All(t => t.Result.Ok), Sizes(tasks.Select(t => t.Result)), "-"));

                wall.Restart();
                var results = parallelCases.Select(c => Once(c, o.Size, apartment)).ToList();
                sequential.Add(new Sample(wall.Elapsed.TotalMilliseconds, results.All(r => r.Ok), Sizes(results), "-"));
            }

            rows.Add(Row("parallel" + parallelCases.Count, apartment, "parallel-wall", concurrent));
            rows.Add(Row("parallel" + parallelCases.Count, apartment, "sequential-wall", sequential));
            Console.WriteLine(rows[^2]);
            Console.WriteLine(rows[^1]);
        }

        // Timeout: the unreachable path is bounded by its hard timeout, and while it hangs another item is
        // requested concurrently to prove that the caller is not blocked.
        IconCase unreachable = Find("net-unreachable");
        IconCase notepad = Find("exe-notepad");
        var hung = new List<Sample>();
        var during = new Dictionary<Apartment, List<Sample>>
        {
            [Apartment.Sta] = new List<Sample>(),
            [Apartment.Mta] = new List<Sample>(),
        };
        for (int rep = 0; rep < 5; rep++)
        {
            Task<CaseOutcome> net = Task.Factory.StartNew(
                () => Program.Evaluate(unreachable, o.Size, Apartment.Mta, null), TaskCreationOptions.LongRunning);
            Thread.Sleep(50); // let the hung query start
            foreach (Apartment apartment in new[] { Apartment.Sta, Apartment.Mta })
            {
                var wall = Stopwatch.StartNew();
                Sample s = Once(notepad, o.Size, apartment);
                during[apartment].Add(s with { Ms = wall.Elapsed.TotalMilliseconds });
            }

            CaseOutcome outcome = net.Result;
            hung.Add(new Sample(outcome.Result.Ms, outcome.Result.Error == "timeout", "0x0", "-"));
        }

        // errors for the unreachable row counts runs that did NOT end in the timeout code
        rows.Add(Row("net-unreachable", Apartment.Mta, "timeout", hung));
        foreach (Apartment apartment in new[] { Apartment.Sta, Apartment.Mta })
        {
            rows.Add(Row("exe-notepad", apartment, "during-timeout", during[apartment]));
        }

        Console.WriteLine(rows[^3]);
        Console.WriteLine(rows[^2]);
        Console.WriteLine(rows[^1]);

        File.WriteAllText(outPath, Header + "\n" + string.Join("\n", rows) + "\n");
        Console.WriteLine("latency written");
        return 0;
    }

    private static Sample Once(IconCase c, int size, Apartment apartment)
    {
        string? name = c.Resolve();
        if (name == null)
        {
            return new Sample(0, false, "none", "-");
        }

        IconResult r = ShellIcon.Get(name, size, TimeSpan.FromMilliseconds(c.TimeoutMs), apartment);
        return new Sample(r.Ms, r.Ok, r.Ok ? r.Width + "x" + r.Height : "none", r.Ok ? Hash(r.Bgra!) : "-");
    }

    private static Sample ColdChild(string name, Apartment apartment)
    {
        var psi = new ProcessStartInfo(Environment.ProcessPath!)
        {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        foreach (string arg in new[] { "--case", name, "--cold", "--threads", apartment == Apartment.Sta ? "sta" : "mta" })
        {
            psi.ArgumentList.Add(arg);
        }

        using Process p = Process.Start(psi)!;
        string text = p.StandardOutput.ReadToEnd();
        p.StandardError.ReadToEnd();
        if (!p.WaitForExit(30000))
        {
            p.Kill(true);
            return new Sample(30000, false, "none", "-");
        }

        // "cold-ms=12.34 ok=true size=256x256"
        double ms = 0;
        bool ok = false;
        string size = "none";
        foreach (string part in text.Split(new[] { ' ', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.StartsWith("cold-ms=", StringComparison.Ordinal))
            {
                double.TryParse(part.AsSpan(8), NumberStyles.Float, Inv, out ms);
            }
            else if (part == "ok=true")
            {
                ok = true;
            }
            else if (part.StartsWith("size=", StringComparison.Ordinal))
            {
                size = part[5..];
            }
        }

        return new Sample(ms, ok, ok ? size : "none", "-");
    }

    private static string Hash(byte[] data)
    {
        ulong h = 14695981039346656037UL; // FNV-1a
        foreach (byte b in data)
        {
            h = (h ^ b) * 1099511628211UL;
        }

        return h.ToString("x16", Inv)[..8];
    }

    private static string Sizes(IEnumerable<Sample> samples) =>
        string.Join('|', samples.Select(s => s.Size).Distinct());

    private static string Row(string name, Apartment apartment, string mode, List<Sample> samples)
    {
        StatsSummary s = Stats.Summarize(samples.Select(x => x.Ms));
        int errors = samples.Count(x => !x.Ok);
        string Num(double v) => double.IsNaN(v) ? "" : v.ToString("F2", Inv);
        string hashes = string.Join('|', samples.Select(x => x.Hash).Distinct());
        return string.Join(
            ',',
            name,
            apartment == Apartment.Sta ? "sta" : "mta",
            mode,
            samples.Count.ToString(Inv),
            Num(s.Median),
            Num(s.P95),
            Num(s.Max),
            errors.ToString(Inv),
            Sizes(samples),
            hashes);
    }
}
