using System.Reflection;
using MehrBar.Spikes.Common;

namespace MehrBar.Spikes.Driver;

/// <summary>Exit codes shared by every case: 0 ok, 1 criteria failed, 2 usage error, 3 aborted.</summary>
public static class ExitCodes
{
    public const int Ok = 0;
    public const int CriteriaFailed = 1;
    public const int Usage = 2;
    public const int Aborted = 3;
}

/// <summary>One test case of the driver; return an exit code from <see cref="ExitCodes"/>.</summary>
public interface IDriverCase
{
    int Run(CaseContext ctx);
}

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class DriverCaseAttribute : Attribute
{
    public DriverCaseAttribute(string name) => Name = name;

    public string Name { get; }
}

/// <summary>
/// Parsed command line of one case run. Later harness plans extend this class only in their own
/// partial files (CaseContext.Safety.cs, CaseContext.Host.cs) so parallel plans never edit the same file.
/// </summary>
public sealed partial class CaseContext
{
    public CaseContext(string name, IReadOnlyDictionary<string, string> options)
    {
        Name = name;
        Options = new Dictionary<string, string>(options, StringComparer.OrdinalIgnoreCase);
    }

    public string Name { get; }

    /// <summary>Options without the leading dashes; flags map to "true". Keys are case-insensitive.</summary>
    public IReadOnlyDictionary<string, string> Options { get; }

    public string? OutPath => Get("out");

    public string? SummaryMd => Get("summary-md");

    public bool DryRun => GetBool("dry-run");

    public bool Manual => GetBool("manual");

    public string? Get(string key) => Options.TryGetValue(key, out string? value) ? value : null;

    public int GetInt(string key, int fallback) =>
        int.TryParse(Get(key), out int value) ? value : fallback;

    public bool GetBool(string key) =>
        Options.TryGetValue(key, out string? value) &&
        (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1");
}

public static class Program
{
    public static int Main(string[] args)
    {
        var registry = BuildRegistry();

        if (args.Length == 0)
        {
            Console.Error.WriteLine("usage: Driver --list | Driver <case> [--key value | --flag]...");
            return ExitCodes.Usage;
        }

        if (args[0] == "--list")
        {
            foreach (string name in registry.Keys.OrderBy(n => n, StringComparer.Ordinal))
            {
                Console.WriteLine(name);
            }

            return ExitCodes.Ok;
        }

        string caseName = args[0];
        if (!registry.TryGetValue(caseName, out Type? caseType))
        {
            Console.Error.WriteLine($"unknown case '{caseName}'; use --list");
            return ExitCodes.Usage;
        }

        if (!TryParseOptions(args.AsSpan(1), out var options, out string? error))
        {
            Console.Error.WriteLine(error);
            return ExitCodes.Usage;
        }

        var ctx = new CaseContext(caseName, options);
        var instance = (IDriverCase)Activator.CreateInstance(caseType)!;

        try
        {
            return instance.Run(ctx);
        }
        catch (OperationCanceledException ex)
        {
            Console.WriteLine($"ABORTED {ex.Message}");
            return ExitCodes.Aborted;
        }
    }

    private static Dictionary<string, Type> BuildRegistry()
    {
        var registry = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
        foreach (Type type in Assembly.GetExecutingAssembly().GetTypes())
        {
            var attribute = type.GetCustomAttribute<DriverCaseAttribute>();
            if (attribute == null || !typeof(IDriverCase).IsAssignableFrom(type) || type.IsAbstract)
            {
                continue;
            }

            registry.Add(attribute.Name, type);
        }

        return registry;
    }

    private static bool TryParseOptions(
        ReadOnlySpan<string> args, out Dictionary<string, string> options, out string? error)
    {
        options = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        error = null;
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal) || arg.Length == 2)
            {
                error = $"unexpected argument '{arg}'";
                return false;
            }

            string key = arg[2..];
            bool hasValue = i + 1 < args.Length && !args[i + 1].StartsWith("--", StringComparison.Ordinal);
            options[key] = hasValue ? args[++i] : "true";
        }

        return true;
    }
}

/// <summary>Self-test for <see cref="Stats"/>; keeps the percentile maths honest before measurements use it.</summary>
[DriverCase("stats-selftest")]
public sealed class StatsSelfTest : IDriverCase
{
    public int Run(CaseContext ctx)
    {
        int failures = 0;

        void Check(string what, double actual, double expected)
        {
            bool ok = actual.Equals(expected);
            Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {what}: {actual} (expected {expected})");
            if (!ok)
            {
                failures++;
            }
        }

        double[] oneToHundred = Enumerable.Range(1, 100).Select(i => (double)i).ToArray();
        Check("p50 of 1..100", Stats.Percentile(oneToHundred, 50), 50);
        Check("p95 of 1..100", Stats.Percentile(oneToHundred, 95), 95);
        Check("p99 of 1..100", Stats.Percentile(oneToHundred, 99), 99);

        double[] single = [42];
        Check("p50 of single element", Stats.Percentile(single, 50), 42);
        Check("p99 of single element", Stats.Percentile(single, 99), 42);

        StatsSummary summary = Stats.Summarize(oneToHundred);
        Check("summary count", summary.Count, 100);
        Check("summary max", summary.Max, 100);
        Check("summary mean", summary.Mean, 50.5);

        StatsSummary empty = Stats.Summarize(Array.Empty<double>());
        Check("empty count", empty.Count, 0);
        bool emptyNaN = double.IsNaN(empty.Median) && double.IsNaN(empty.P95) && double.IsNaN(empty.P99) &&
                        double.IsNaN(empty.Mean) && double.IsNaN(empty.Max);
        Console.WriteLine($"{(emptyNaN ? "PASS" : "FAIL")} empty summary values are NaN");
        if (!emptyNaN)
        {
            failures++;
        }

        Console.WriteLine(failures == 0 ? "SELFTEST OK" : $"SELFTEST FAILED ({failures})");
        return failures == 0 ? ExitCodes.Ok : ExitCodes.CriteriaFailed;
    }
}
