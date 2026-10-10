using System.Globalization;
using System.IO; // not an implicit using in WPF projects
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using MehrBar.Spikes.Common;

namespace MehrBar.Spikes.WindowHost;

/// <summary>
/// Parsed command line. Known keys get typed properties; every <c>--key value</c> pair (known or not)
/// is also available in <see cref="All"/> with a lower-case key, which becomes HostContext.Options.
/// </summary>
internal sealed record HostOptions(
    string Role,
    string Variants,
    int Width,
    int Height,
    int X,
    int Y,
    bool Transparent,
    string Render,
    bool Sentinel,
    string Cover,
    string Title,
    string LogPath,
    int ExitAfterMs,
    bool ListVariants,
    IReadOnlyDictionary<string, string> All)
{
    public const string Usage =
        "WindowHost --role bar|witness [--variant H1[,..]] [--size WxH] [--pos x,y] [--transparent true|false] " +
        "[--render hw|sw] [--sentinel on|off] [--cover full|partial|none] [--title text] [--log file.csv] " +
        "[--exit-after-ms N] [--list-variants]";

    /// <exception cref="FormatException">An argument is malformed or unknown for its key.</exception>
    public static HostOptions Parse(string[] args)
    {
        var all = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        bool listVariants = false;
        for (int i = 0; i < args.Length; i++)
        {
            string arg = args[i];
            if (!arg.StartsWith("--", StringComparison.Ordinal) || arg.Length == 2)
            {
                throw new FormatException($"Unexpected argument '{arg}'.");
            }

            string key = arg[2..].ToLowerInvariant();
            if (key == "list-variants")
            {
                listVariants = true;
                continue;
            }

            if (i + 1 >= args.Length)
            {
                throw new FormatException($"Option '{arg}' needs a value.");
            }

            all[key] = args[++i];
        }

        string role = Get(all, "role", "bar").ToLowerInvariant();
        if (role is not ("bar" or "witness"))
        {
            throw new FormatException($"--role must be bar or witness, not '{role}'.");
        }

        (int width, int height) = SpikeGeometry.WindowSize;
        if (all.TryGetValue("size", out string? size))
        {
            string[] parts = size.Split('x', 'X');
            if (parts.Length != 2 || !TryInt(parts[0], out width) || !TryInt(parts[1], out height) || width <= 0 || height <= 0)
            {
                throw new FormatException($"--size must be WxH, not '{size}'.");
            }
        }

        (int x, int y) = SpikeGeometry.DefaultWindowPos(width);
        if (all.TryGetValue("pos", out string? pos))
        {
            string[] parts = pos.Split(',');
            if (parts.Length != 2 || !TryInt(parts[0], out x) || !TryInt(parts[1], out y))
            {
                throw new FormatException($"--pos must be x,y, not '{pos}'.");
            }
        }

        bool transparent = ParseSwitch(all, "transparent", "true", "true", "false");
        bool sentinel = ParseSwitch(all, "sentinel", "on", "on", "off");

        string render = Get(all, "render", "hw").ToLowerInvariant();
        if (render is not ("hw" or "sw"))
        {
            throw new FormatException($"--render must be hw or sw, not '{render}'.");
        }

        string cover = Get(all, "cover", "none").ToLowerInvariant();
        if (cover is not ("full" or "partial" or "none"))
        {
            throw new FormatException($"--cover must be full, partial or none, not '{cover}'.");
        }

        int exitAfterMs = 0;
        if (all.TryGetValue("exit-after-ms", out string? exit) && (!TryInt(exit, out exitAfterMs) || exitAfterMs < 0))
        {
            throw new FormatException($"--exit-after-ms must be a non-negative integer, not '{exit}'.");
        }

        string variants = Get(all, "variant", "H1");
        string defaultTitle = role == "witness" ? "MehrBar Spike Witness" : "MehrBar Spike Host";
        string title = Get(all, "title", defaultTitle);
        string log = all.TryGetValue("log", out string? logValue)
            ? logValue
            : Path.Combine(RepoPaths.DryResults, $"host-{Environment.ProcessId}.csv");

        return new HostOptions(
            role, variants, width, height, x, y, transparent, render, sentinel, cover, title, log, exitAfterMs, listVariants, all);
    }

    private static string Get(Dictionary<string, string> all, string key, string fallback) =>
        all.TryGetValue(key, out string? value) ? value : fallback;

    private static bool TryInt(string text, out int value) =>
        int.TryParse(text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out value);

    private static bool ParseSwitch(Dictionary<string, string> all, string key, string fallback, string on, string off)
    {
        string value = Get(all, key, fallback).ToLowerInvariant();
        if (value == on)
        {
            return true;
        }

        if (value == off)
        {
            return false;
        }

        throw new FormatException($"--{key} must be {on} or {off}, not '{value}'.");
    }
}

public partial class App : Application
{
    private RunLog? _log;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        HostOptions options;
        try
        {
            options = HostOptions.Parse(e.Args);
        }
        catch (FormatException ex)
        {
            Console.Error.WriteLine(ex.Message);
            Console.Error.WriteLine(HostOptions.Usage);
            Shutdown(2);
            return;
        }

        if (options.ListVariants)
        {
            foreach (string name in VariantRegistry.Names)
            {
                Console.Out.WriteLine(name);
            }

            Shutdown(0);
            return;
        }

        // Pitfall 6: the process render mode must be set before the first window exists.
        RenderOptions.ProcessRenderMode = options.Render == "sw" ? RenderMode.SoftwareOnly : RenderMode.Default;

        _log = new RunLog(options.LogPath);
        LogRenderMode(_log, options);

        Window window;
        if (options.Role == "bar")
        {
            IReadOnlyList<(string Name, IHostVariant Variant)> variants;
            try
            {
                variants = VariantRegistry.Resolve(options.Variants);
            }
            catch (UnknownVariantException ex)
            {
                Console.Error.WriteLine(ex.Message);
                Shutdown(2);
                return;
            }

            window = new HostWindow(options, _log, variants);
        }
        else
        {
            window = new WitnessWindow(options, _log);
        }

        // READY is printed once the HWND exists and the first content has been rendered, so the
        // self-check and first-frame rows are already in the CSV when a driver sees the line.
        window.ContentRendered += (_, _) => PrintReady(window, options.Role);
        window.Show();

        if (options.ExitAfterMs > 0)
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(options.ExitAfterMs) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                Shutdown(0);
            };
            timer.Start();
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        // The generated Main of a WPF app discards Run()'s result, so publish the exit code explicitly.
        Environment.ExitCode = e.ApplicationExitCode;
        _log?.Dispose();
        base.OnExit(e);
    }

    private static void PrintReady(Window window, string role)
    {
        nint handle = new WindowInteropHelper(window).Handle;
        Console.Out.WriteLine(
            string.Create(CultureInfo.InvariantCulture, $"READY hwnd=0x{handle:X} pid={Environment.ProcessId} role={role}"));
        Console.Out.Flush();
    }

    private static void LogRenderMode(RunLog log, HostOptions options)
    {
        int tier = RenderCapability.Tier >> 16;
        bool software = RenderOptions.ProcessRenderMode == RenderMode.SoftwareOnly || tier == 0;
        log.Write(
            options.Role,
            options.Role == "bar" ? options.Variants : string.Empty,
            string.Empty,
            "render-mode",
            string.Create(
                CultureInfo.InvariantCulture,
                $"effective={(software ? "software" : "hardware")} tier={tier} requested={options.Render}"));
    }
}
