using System.IO; // not an implicit using in WPF projects

namespace MehrBar.Spikes.Common;

/// <summary>
/// Entry point of the safety net around every input-sending Driver run (RESEARCH Pitfall 3, D-05).
/// Deliberately free of the CSV logger: output goes through <see cref="SafetySession.Logger"/>
/// so that every project can compile this file on its own.
/// </summary>
public static class Safety
{
    private static readonly object s_gate = new();
    private static SafetySession? s_current;

    /// <summary>The one active session of this process, or null.</summary>
    public static SafetySession? Current
    {
        get
        {
            lock (s_gate)
            {
                return s_current;
            }
        }
    }

    public static SafetySession Begin(
        string caseName, TimeSpan timeout, bool watchRealInput = true, Action<string>? logger = null)
    {
        lock (s_gate)
        {
            if (s_current != null)
            {
                throw new InvalidOperationException("a SafetySession is already active in this process");
            }

            var session = new SafetySession(caseName, timeout, watchRealInput, logger);
            s_current = session;
            return session;
        }
    }

    /// <summary>Throws <see cref="OperationCanceledException"/> when the active session was aborted.</summary>
    public static void ThrowIfAborted()
    {
        SafetySession? session = Current;
        if (session != null && session.IsAborted && !session.Restoring)
        {
            throw new OperationCanceledException($"safety abort: {session.AbortReason}");
        }
    }

    /// <summary>
    /// Sleeps <paramref name="milliseconds"/>, waking early and throwing when the session aborts.
    /// While the session restores, the wait is a plain sleep so the restore keeps its pacing.
    /// </summary>
    public static void Wait(int milliseconds)
    {
        if (milliseconds <= 0)
        {
            return;
        }

        SafetySession? session = Current;
        if (session == null || session.Restoring)
        {
            Thread.Sleep(milliseconds);
            return;
        }

        session.Token.WaitHandle.WaitOne(milliseconds);
        ThrowIfAborted();
    }

    internal static void Release(SafetySession session)
    {
        lock (s_gate)
        {
            if (ReferenceEquals(s_current, session))
            {
                s_current = null;
            }
        }
    }
}

/// <summary>
/// One guarded run. Aborts (hotkey, real input, timeout, Ctrl+C, test) cancel <see cref="Token"/>;
/// <see cref="Dispose"/> then runs the registered restore actions newest first, releases only the
/// keys this process pressed itself, and tears the watchers down. Dispose is idempotent and also
/// runs from Ctrl+C and process exit.
/// </summary>
public sealed class SafetySession : IDisposable
{
    private readonly object _disposeGate = new();
    private readonly object _restoreGate = new();
    private readonly Stack<(string Name, Action Action)> _restore = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Timer? _timeoutTimer;
    private readonly ConsoleCancelEventHandler _onCancelKey;
    private readonly EventHandler _onProcessExit;
    private string? _abortReason;
    private bool _disposed;
    private volatile bool _restoring;

    internal SafetySession(string caseName, TimeSpan timeout, bool watchRealInput, Action<string>? logger)
    {
        CaseName = caseName;
        WatchRealInput = watchRealInput;
        Token = _cts.Token;
        Logger = logger ?? Console.Error.WriteLine;

        _onCancelKey = (_, _) =>
        {
            Abort("ctrl-c");
            Dispose();
        };
        _onProcessExit = (_, _) => Dispose();
        Console.CancelKeyPress += _onCancelKey;
        AppDomain.CurrentDomain.ProcessExit += _onProcessExit;

        if (timeout > TimeSpan.Zero)
        {
            _timeoutTimer = new Timer(_ => Abort("timeout"), null, timeout, Timeout.InfiniteTimeSpan);
        }

        Log($"session begin case={caseName} timeout={(timeout > TimeSpan.Zero ? timeout.TotalSeconds + "s" : "none")} " +
            $"watchRealInput={watchRealInput}");
    }

    public string CaseName { get; }

    public bool WatchRealInput { get; }

    /// <summary>Receives every safety log line. Default: stderr.</summary>
    public Action<string> Logger { get; set; }

    public CancellationToken Token { get; }

    public bool IsAborted => _cts.IsCancellationRequested;

    /// <summary>Why the run was aborted (hotkey, real-input, timeout, ctrl-c, or a test reason); null while running.</summary>
    public string? AbortReason => Volatile.Read(ref _abortReason);

    /// <summary>True while <see cref="Dispose"/> is running restore actions; input is allowed again then.</summary>
    public bool Restoring => _restoring;

    /// <summary>Registers an undo step. Steps run newest first in <see cref="Dispose"/>.</summary>
    public void OnRestore(string name, Action action)
    {
        lock (_restoreGate)
        {
            _restore.Push((name, action));
        }
    }

    /// <summary>Triggers the same path as the hotkey or the real-input watcher, without any real input.</summary>
    public void AbortForTest(string reason) => Abort(reason);

    internal void Abort(string reason)
    {
        if (Interlocked.CompareExchange(ref _abortReason, reason, null) != null)
        {
            return; // first reason wins
        }

        Log($"ABORT reason={reason}");
        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Session already torn down; nothing left to cancel.
        }
    }

    public void Dispose()
    {
        lock (_disposeGate)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Console.CancelKeyPress -= _onCancelKey;
            AppDomain.CurrentDomain.ProcessExit -= _onProcessExit;
            _timeoutTimer?.Dispose();
            _restoring = true;

            try
            {
                RunRestoreActions();
                Input.ReleaseAllTracked(Log);
            }
            finally
            {
                _restoring = false;
                Safety.Release(this);
                _cts.Dispose();
                Log("session end");
            }
        }
    }

    private void RunRestoreActions()
    {
        while (true)
        {
            (string Name, Action Action) step;
            lock (_restoreGate)
            {
                if (_restore.Count == 0)
                {
                    return;
                }

                step = _restore.Pop();
            }

            try
            {
                Log($"restore: {step.Name}");
                step.Action();
            }
            catch (Exception ex)
            {
                Log($"restore '{step.Name}' failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private void Log(string message)
    {
        try
        {
            Logger($"safety[{CaseName}] {message}");
        }
        catch
        {
            // A broken logger must never stop the restore path.
        }
    }
}
