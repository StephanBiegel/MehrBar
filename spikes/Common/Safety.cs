using System.IO; // not an implicit using in WPF projects
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

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

    /// <summary>
    /// Opens the session: registers the abort hotkey (Ctrl+Alt+Q, fallback Ctrl+Alt+Shift+Q), installs
    /// the real-input watcher when asked, and arms the per-case timeout. Throws when no hotkey is available.
    /// </summary>
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
public sealed unsafe class SafetySession : IDisposable
{
    // The low-level hook callbacks are static function pointers and find their session through this
    // static field (RESEARCH Pitfall 7: nothing the OS calls back into may be collectable).
    private static SafetySession? s_hookOwner;

    private const uint LLKHF_INJECTED = 0x10;
    private const uint LLMHF_INJECTED = 0x01;
    private const int HotkeyId = 0x4D42; // 'MB'
    private const int ProbeHotkeyId = 0x4D43;

    private static readonly (HOT_KEY_MODIFIERS Mods, string Name)[] s_hotkeys =
    [
        (HOT_KEY_MODIFIERS.MOD_CONTROL | HOT_KEY_MODIFIERS.MOD_ALT, "Ctrl+Alt+Q"),
        (HOT_KEY_MODIFIERS.MOD_CONTROL | HOT_KEY_MODIFIERS.MOD_ALT | HOT_KEY_MODIFIERS.MOD_SHIFT, "Ctrl+Alt+Shift+Q"),
    ];

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
    private Thread? _watcher;
    private uint _watcherThreadId;
    private volatile bool _hotkeyRegistered;
    private volatile bool _hooksInstalled;

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

        try
        {
            StartWatchers();
        }
        catch
        {
            Dispose();
            throw;
        }
    }

    public string CaseName { get; }

    public bool WatchRealInput { get; }

    /// <summary>The registered emergency abort hotkey ("Ctrl+Alt+Q" or the fallback); null if none.</summary>
    public string? HotkeyName { get; private set; }

    /// <summary>True while the abort hotkey is registered with the system.</summary>
    public bool HotkeyRegistered => _hotkeyRegistered;

    /// <summary>True while the low-level keyboard and mouse hooks are installed (only with watchRealInput).</summary>
    public bool HooksInstalled => _hooksInstalled;

    /// <summary>True while the message-loop thread of this session is running.</summary>
    public bool WatcherThreadAlive => _watcher is { IsAlive: true };

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

    /// <summary>
    /// Whether the named hotkey can be registered right now from the calling thread. The self-test uses
    /// it as an independent witness: false while a session holds the hotkey, true after Dispose.
    /// </summary>
    internal static bool CanRegisterHotkey(string name)
    {
        foreach ((HOT_KEY_MODIFIERS mods, string candidate) in s_hotkeys)
        {
            if (candidate != name)
            {
                continue;
            }

            if (!PInvoke.RegisterHotKey(HWND.Null, ProbeHotkeyId, mods | HOT_KEY_MODIFIERS.MOD_NOREPEAT, Vk.Q))
            {
                return false;
            }

            PInvoke.UnregisterHotKey(HWND.Null, ProbeHotkeyId);
            return true;
        }

        throw new ArgumentException($"unknown hotkey '{name}'", nameof(name));
    }

    internal void Abort(string reason, bool logAsync = false)
    {
        if (Interlocked.CompareExchange(ref _abortReason, reason, null) != null)
        {
            return; // first reason wins
        }

        if (logAsync)
        {
            // Called from a low-level hook: no console I/O inside the hook callback.
            ThreadPool.QueueUserWorkItem(_ => Log($"ABORT reason={reason}"));
        }
        else
        {
            Log($"ABORT reason={reason}");
        }

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
                StopWatchers();
                _restoring = false;
                Safety.Release(this);
                _cts.Dispose();
                Log("session end");
            }
        }
    }

    private void StartWatchers()
    {
        using var ready = new ManualResetEventSlim(false);
        _watcher = new Thread(() => WatcherMain(ready))
        {
            IsBackground = true,
            Name = "SafetyWatcher",
        };
        _watcher.Start();
        if (!ready.Wait(TimeSpan.FromSeconds(5)))
        {
            throw new InvalidOperationException("safety watcher thread did not start");
        }

        if (HotkeyName == null)
        {
            throw new InvalidOperationException(
                "no abort hotkey could be registered (Ctrl+Alt+Q and Ctrl+Alt+Shift+Q are both taken); " +
                "refusing to run input without an emergency exit");
        }

        Log($"abort hotkey {HotkeyName} active; real-input watcher " +
            (WatchRealInput ? (_hooksInstalled ? "active" : "FAILED to install") : "off"));
    }

    /// <summary>Hotkey, hooks and message loop all live on this one thread (hooks and WM_HOTKEY need its queue).</summary>
    private void WatcherMain(ManualResetEventSlim ready)
    {
        HHOOK keyboardHook = HHOOK.Null;
        HHOOK mouseHook = HHOOK.Null;
        try
        {
            // Touching the queue first creates it, so PostThreadMessage(WM_QUIT) cannot be lost.
            PInvoke.PeekMessage(out MSG _, HWND.Null, 0, 0, PEEK_MESSAGE_REMOVE_TYPE.PM_NOREMOVE);
            _watcherThreadId = PInvoke.GetCurrentThreadId();

            foreach ((HOT_KEY_MODIFIERS mods, string name) in s_hotkeys)
            {
                if (PInvoke.RegisterHotKey(HWND.Null, HotkeyId, mods | HOT_KEY_MODIFIERS.MOD_NOREPEAT, Vk.Q))
                {
                    _hotkeyRegistered = true;
                    HotkeyName = name;
                    break;
                }

                Log($"RegisterHotKey {name} failed (taken by another program or session)");
            }

            if (WatchRealInput)
            {
                s_hookOwner = this;
                HINSTANCE module = (HINSTANCE)PInvoke.GetModuleHandle((PCWSTR)null).Value;
                keyboardHook = PInvoke.SetWindowsHookEx(
                    WINDOWS_HOOK_ID.WH_KEYBOARD_LL, &KeyboardProc, module, 0);
                mouseHook = PInvoke.SetWindowsHookEx(
                    WINDOWS_HOOK_ID.WH_MOUSE_LL, &MouseProc, module, 0);
                _hooksInstalled = !keyboardHook.IsNull && !mouseHook.IsNull;
            }
        }
        catch (Exception ex)
        {
            Log($"watcher setup failed: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            ready.Set();
        }

        try
        {
            while (PInvoke.GetMessage(out MSG msg, HWND.Null, 0, 0).Value > 0)
            {
                if (msg.message == PInvoke.WM_HOTKEY && msg.wParam.Value == (nuint)HotkeyId)
                {
                    Abort("hotkey");
                }
            }
        }
        finally
        {
            if (!keyboardHook.IsNull)
            {
                PInvoke.UnhookWindowsHookEx(keyboardHook);
            }

            if (!mouseHook.IsNull)
            {
                PInvoke.UnhookWindowsHookEx(mouseHook);
            }

            if (ReferenceEquals(s_hookOwner, this))
            {
                s_hookOwner = null;
            }

            _hooksInstalled = false;
            if (_hotkeyRegistered)
            {
                PInvoke.UnregisterHotKey(HWND.Null, HotkeyId);
                _hotkeyRegistered = false;
            }
        }
    }

    private void StopWatchers()
    {
        Thread? thread = _watcher;
        if (thread == null)
        {
            return;
        }

        if (thread.IsAlive && _watcherThreadId != 0)
        {
            PInvoke.PostThreadMessage(_watcherThreadId, PInvoke.WM_QUIT, 0, 0);
        }

        if (!thread.Join(TimeSpan.FromSeconds(3)))
        {
            Log("watcher thread did not exit within 3 s");
        }
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT KeyboardProc(int nCode, WPARAM wParam, LPARAM lParam)
    {
        try
        {
            if (nCode >= 0 && lParam.Value != 0)
            {
                var info = (KBDLLHOOKSTRUCT*)lParam.Value;
                if (((uint)info->flags & LLKHF_INJECTED) == 0)
                {
                    s_hookOwner?.Abort("real-input", logAsync: true);
                }
            }
        }
        catch
        {
            // An exception must never leave an UnmanagedCallersOnly method.
        }

        return PInvoke.CallNextHookEx(HHOOK.Null, nCode, wParam, lParam);
    }

    [UnmanagedCallersOnly(CallConvs = [typeof(CallConvStdcall)])]
    private static LRESULT MouseProc(int nCode, WPARAM wParam, LPARAM lParam)
    {
        try
        {
            if (nCode >= 0 && lParam.Value != 0)
            {
                var info = (MSLLHOOKSTRUCT*)lParam.Value;
                if ((info->flags & LLMHF_INJECTED) == 0)
                {
                    s_hookOwner?.Abort("real-input", logAsync: true);
                }
            }
        }
        catch
        {
            // An exception must never leave an UnmanagedCallersOnly method.
        }

        return PInvoke.CallNextHookEx(HHOOK.Null, nCode, wParam, lParam);
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
