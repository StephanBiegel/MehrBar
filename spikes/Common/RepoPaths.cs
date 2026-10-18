using System.Diagnostics;
using System.IO; // not an implicit using in WPF projects

namespace MehrBar.Spikes.Common;

/// <summary>
/// Resolves repository locations for the throw-away spikes. Gitignored inputs and outputs
/// (reference screenshots, private results, PresentMon) live in the main checkout, so
/// <see cref="MainRoot"/> is used for them even when a GSD executor runs in a worktree.
/// </summary>
public static class RepoPaths
{
    private static readonly Lazy<string> _repoRoot = new(FindRepoRoot);
    private static readonly Lazy<string> _mainRoot = new(FindMainRoot);

    /// <summary>Root of the checkout the binaries were built from (contains MehrBar.sln).</summary>
    public static string RepoRoot => _repoRoot.Value;

    /// <summary>Root of the main checkout; equals <see cref="RepoRoot"/> outside a worktree.</summary>
    public static string MainRoot => _mainRoot.Value;

    /// <summary>Committed, small, redacted results.</summary>
    public static string Results => Path.Combine(RepoRoot, "spikes", "results");

    /// <summary>Ignored results (screenshots, paths, window titles); created on demand, survives worktree removal.</summary>
    public static string PrivateResults
    {
        get
        {
            string path = Path.Combine(MainRoot, "spikes", "results", "private");
            Directory.CreateDirectory(path);
            return path;
        }
    }

    /// <summary>Ignored output of dry runs.</summary>
    public static string DryResults => Path.Combine(RepoRoot, "spikes", "results", "dry");

    /// <summary>Release build of the bar under test.</summary>
    public static string HostExe =>
        Path.Combine(RepoRoot, "spikes", "WindowHost", "bin", "Release", "net10.0-windows", "WindowHost.exe");

    /// <summary>Release build of the DirectComposition comparison host (plan 01-19).</summary>
    public static string CompositionHostExe =>
        Path.Combine(RepoRoot, "spikes", "CompositionHost", "bin", "Release", "net10.0-windows", "CompositionHost.exe");

    /// <summary>Phase 1 planning folder holding the go/no-go notes.</summary>
    public static string PhaseDir => Path.Combine(RepoRoot, ".planning", "phases", "01-risiko-spikes");

    private static string FindRepoRoot()
    {
        // Prefer the solution file; fall back to a spikes folder with its own package props
        // (the product scaffold may not exist yet when plans run in parallel).
        string? fallback = null;
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "MehrBar.sln")))
            {
                return dir.FullName;
            }

            if (fallback == null &&
                File.Exists(Path.Combine(dir.FullName, "spikes", "Directory.Packages.props")))
            {
                fallback = dir.FullName;
            }
        }

        return fallback ?? throw new InvalidOperationException(
            "Repository root not found above " + AppContext.BaseDirectory);
    }

    private static string FindMainRoot()
    {
        try
        {
            var psi = new ProcessStartInfo("git", "rev-parse --path-format=absolute --git-common-dir")
            {
                WorkingDirectory = RepoRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            using var process = Process.Start(psi);
            if (process == null)
            {
                return RepoRoot;
            }

            string output = process.StandardOutput.ReadToEnd().Trim();
            process.StandardError.ReadToEnd();
            if (!process.WaitForExit(5000) || process.ExitCode != 0 || output.Length == 0)
            {
                return RepoRoot;
            }

            // The common git dir is <main>/.git; its parent is the main checkout.
            string? parent = Path.GetDirectoryName(Path.GetFullPath(output.TrimEnd('/', '\\')));
            return string.IsNullOrEmpty(parent) ? RepoRoot : parent;
        }
        catch (Exception)
        {
            return RepoRoot;
        }
    }
}
