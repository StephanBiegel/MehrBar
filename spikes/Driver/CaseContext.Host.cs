using MehrBar.Spikes.Common;

namespace MehrBar.Spikes.Driver;

// Partial of CaseContext owned by plan 01-07; the main part lives in Program.cs.
// HostProcess exposes CsWin32 types, which are internal, so the members that use it are internal too.
public sealed partial class CaseContext : IDisposable
{
    private readonly object _hostGate = new();
    private readonly List<HostProcess> _started = [];
    private RunLog? _log;
    private bool _exitHooked;
    private bool _disposed;

    /// <summary>
    /// CSV log of this case at <see cref="OutPath"/>; without <c>--out</c> it goes to
    /// <c>spikes/results/dry/&lt;case&gt;-&lt;yyyyMMdd-HHmmss&gt;.csv</c> (ignored by git). Created on first use.
    /// </summary>
    public RunLog Log
    {
        get
        {
            lock (_hostGate)
            {
                return _log ??= new RunLog(
                    OutPath ?? Path.Combine(RepoPaths.DryResults, $"{Name}-{DateTime.Now:yyyyMMdd-HHmmss}.csv"));
            }
        }
    }

    /// <summary>
    /// Starts a bar (default role) with the given WindowHost arguments and waits for READY; <paramref name="exe"/> selects
    /// another host with the same protocol (the DirectComposition host of plan 01-19).
    /// </summary>
    internal HostProcess StartHost(string args, string? exe = null) => Track(HostProcess.Start(args, exe));

    /// <summary>
    /// Starts a witness window (<c>--role witness --cover &lt;cover&gt;</c>). <paramref name="activate"/> false
    /// (<c>--activate false</c>) shows it without taking the foreground, for runs that must not disturb the desktop.
    /// </summary>
    internal HostProcess StartWitness(string cover, string? title = null, bool activate = true)
    {
        string args = $"--role witness --cover {cover}";
        if (!string.IsNullOrEmpty(title))
        {
            args += $" --title \"{title}\"";
        }

        if (!activate)
        {
            args += " --activate false";
        }

        return Track(HostProcess.Start(args));
    }

    /// <summary>
    /// Kills every process this case started and closes the log. Runs at process exit (every Driver run
    /// is one case) and may be called earlier from a case; calling it twice is harmless.
    /// </summary>
    public void Dispose()
    {
        HostProcess[] processes;
        lock (_hostGate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            processes = _started.ToArray();
            _started.Clear();
        }

        foreach (HostProcess process in processes)
        {
            process.Dispose();
        }

        lock (_hostGate)
        {
            _log?.Dispose();
            _log = null;
        }
    }

    private HostProcess Track(HostProcess process)
    {
        lock (_hostGate)
        {
            _started.Add(process);
            if (!_exitHooked)
            {
                _exitHooked = true;
                AppDomain.CurrentDomain.ProcessExit += (_, _) => Dispose();
            }
        }

        // A safety abort or Ctrl+C disposes the session, which then ends the hosts as part of the restore.
        Safety.Current?.OnRestore($"kill {process.Role} host pid {process.Pid}", process.Kill);
        return process;
    }
}
