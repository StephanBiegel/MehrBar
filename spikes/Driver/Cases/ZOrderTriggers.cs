using System.Diagnostics;
using System.Globalization;
using Microsoft.Win32;
using MehrBar.Spikes.Common;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Graphics.Gdi;
using Windows.Win32.System.StationsAndDesktops;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MehrBar.Spikes.Driver.Cases;

// The triggers of the D-10 matrix. Nothing in this file runs in a dry run: the case fires only the trigger "none"
// there. Every trigger that changes something registers its undo with the SafetySession BEFORE the first input,
// registry, process or display call (RESEARCH Pitfall 3), and every undo checks a state flag so that it does not
// toggle a state twice (a second Win+D would show the desktop again).

/// <summary>Trigger lookup for the <c>zorder</c> case.</summary>
internal static class ZTriggers
{
    /// <summary>All trigger names, in the order the matrix is described.</summary>
    public static IReadOnlyList<string> Names { get; } =
        ["win-d", "corner", "win-m", "peek", "peek-key", "alt-tab", "explorer", "display", "lock", "manual", "none"];

    /// <summary>
    /// Resolves a comma separated list (or <c>all</c>). <c>all</c> is win-d, corner, win-m, peek, alt-tab, plus explorer
    /// and display when their flags are given. A trigger that needs an <c>--allow-*</c> flag which is missing is refused.
    /// </summary>
    public static bool TryResolve(
        string text,
        bool allowExplorer,
        bool allowDisplay,
        bool allowLock,
        int manualSeconds,
        out IReadOnlyList<IZTrigger> triggers,
        out string? error)
    {
        triggers = [];
        error = null;

        var setting = new TaskbarCornerSetting();
        IEnumerable<string> names = text.Equals("all", StringComparison.OrdinalIgnoreCase)
            ? AllNames(allowExplorer, allowDisplay)
            : text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var result = new List<IZTrigger>();
        foreach (string name in names)
        {
            IZTrigger? trigger = Create(name.ToLowerInvariant(), setting, allowExplorer, manualSeconds);
            if (trigger == null)
            {
                error = $"unknown trigger '{name}'; known: {string.Join(", ", Names)}, all";
                return false;
            }

            bool allowed = trigger.RequiredFlag switch
            {
                "allow-explorer" => allowExplorer,
                "allow-display" => allowDisplay,
                "allow-lock" => allowLock,
                _ => true,
            };
            if (!allowed)
            {
                error = $"trigger '{trigger.Name}' changes the developer's session and is only fired with --{trigger.RequiredFlag}";
                return false;
            }

            result.Add(trigger);
        }

        if (result.Count == 0)
        {
            error = "no trigger given";
            return false;
        }

        triggers = result;
        return true;
    }

    private static IEnumerable<string> AllNames(bool allowExplorer, bool allowDisplay)
    {
        yield return "win-d";
        yield return "corner";
        yield return "win-m";
        yield return "peek";
        yield return "alt-tab";
        if (allowExplorer)
        {
            yield return "explorer";
        }

        if (allowDisplay)
        {
            yield return "display";
        }
    }

    private static IZTrigger? Create(string name, TaskbarCornerSetting setting, bool allowExplorer, int manualSeconds) =>
        name switch
        {
            "win-d" => new WinDTrigger(),
            "corner" => new CornerTrigger(setting, allowExplorer),
            "win-m" => new WinMTrigger(),
            "peek" => new PeekTrigger(setting),
            "peek-key" => new PeekKeyTrigger(),
            "alt-tab" => new AltTabTrigger(),
            "explorer" => new ExplorerTrigger(),
            "display" => new DisplayTrigger(),
            "lock" => new LockTrigger(),
            "manual" => new ManualTrigger(manualSeconds),
            "none" => new NoneTrigger(),
            _ => null,
        };
}

/// <summary>Shared defaults of the triggers.</summary>
internal abstract class ZTriggerBase : IZTrigger
{
    public abstract string Name { get; }

    public virtual string? RequiredFlag => null;

    public virtual bool HumanParticipates => false;

    public virtual bool SamplerStartsAfterFire => false;

    public virtual int SampleMs => 3000;

    public abstract void Fire(ZContext ctx);

    public virtual void Undo(ZContext ctx)
    {
    }

    protected static SafetySession Session(ZContext ctx) =>
        ctx.Safety ?? throw new InvalidOperationException("a trigger that changes the session needs the SafetySession of the run");

    protected static (int X, int Y) ScreenCentre() =>
        (PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSCREEN) / 2, PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYSCREEN) / 2);
}

/// <summary>No-op: the pipeline just samples for the window.</summary>
internal sealed class NoneTrigger : ZTriggerBase
{
    public override string Name => "none";

    public override void Fire(ZContext ctx)
    {
    }
}

/// <summary>Nothing is injected: the developer presses the real keys (hand part), the case only samples.</summary>
internal sealed class ManualTrigger : ZTriggerBase
{
    private readonly int _seconds;

    public ManualTrigger(int seconds)
    {
        _seconds = Math.Max(5, seconds);
    }

    public override string Name => "manual";

    public override bool HumanParticipates => true;

    public override int SampleMs => _seconds * 1000;

    public override void Fire(ZContext ctx) =>
        ctx.Note("manual", $"sampling for {_seconds} s; press the keys / move the mouse yourself, nothing is injected", note: false);
}

/// <summary>Win+D; the undo is a second Win+D.</summary>
internal sealed class WinDTrigger : ZTriggerBase
{
    private bool _registered;
    private bool _shown;

    public override string Name => "win-d";

    public override void Fire(ZContext ctx)
    {
        if (!_registered)
        {
            _registered = true;
            Session(ctx).OnRestore("win-d: toggle the desktop back if it is still shown", ToggleBackIfShown);
        }

        _shown = true;
        Input.Chord(Vk.LWin, Vk.D);
    }

    public override void Undo(ZContext ctx) => ToggleBackIfShown();

    private void ToggleBackIfShown()
    {
        if (!_shown)
        {
            return;
        }

        _shown = false;
        Input.Chord(Vk.LWin, Vk.D);
    }
}

/// <summary>Win+M; the undo is Win+Shift+M.</summary>
internal sealed class WinMTrigger : ZTriggerBase
{
    private bool _registered;
    private bool _minimized;

    public override string Name => "win-m";

    public override void Fire(ZContext ctx)
    {
        if (!_registered)
        {
            _registered = true;
            Session(ctx).OnRestore("win-m: Win+Shift+M if the windows are still minimized", RestoreIfMinimized);
        }

        _minimized = true;
        Input.Chord(Vk.LWin, Vk.M);
    }

    public override void Undo(ZContext ctx) => RestoreIfMinimized();

    private void RestoreIfMinimized()
    {
        if (!_minimized)
        {
            return;
        }

        _minimized = false;
        Input.Chord(Vk.LWin, Vk.LShift, Vk.M);
    }
}

/// <summary>
/// The "show desktop" corner at the far end of the taskbar needs the Explorer option TaskbarSd. The value is read,
/// set to 1 for the test and restored (or deleted when it was absent) when the session ends. One instance is shared
/// by the triggers corner and peek.
/// </summary>
internal sealed class TaskbarCornerSetting
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Advanced";
    private const string ValueName = "TaskbarSd";

    private bool _armed;
    private object? _original;
    private RegistryValueKind _originalKind = RegistryValueKind.DWord;

    /// <summary>Makes sure TaskbarSd is 1; the first call registers the restore before it touches the registry.</summary>
    public void Ensure(ZContext ctx, SafetySession session)
    {
        if (_armed)
        {
            return;
        }

        _armed = true;
        session.OnRestore("restore TaskbarSd (original value or delete) and broadcast TraySettings", Restore);

        using (RegistryKey? read = Registry.CurrentUser.OpenSubKey(KeyPath))
        {
            _original = read?.GetValue(ValueName);
            if (_original != null)
            {
                _originalKind = read!.GetValueKind(ValueName);
            }
        }

        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true))
        {
            key.SetValue(ValueName, 1, RegistryValueKind.DWord);
        }

        Broadcast();
        ctx.Note("taskbar-corner", $"TaskbarSd set to 1 for the test (original: {(_original == null ? "absent" : _original)})", note: false);
    }

    private void Restore()
    {
        using (RegistryKey key = Registry.CurrentUser.CreateSubKey(KeyPath, writable: true))
        {
            if (_original == null)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
            else
            {
                key.SetValue(ValueName, _original, _originalKind);
            }
        }

        Broadcast();
    }

    /// <summary>WM_SETTINGCHANGE with "TraySettings" tells Explorer to re-read the taskbar options.</summary>
    private static unsafe void Broadcast()
    {
        fixed (char* text = "TraySettings")
        {
            nuint result;
            PInvoke.SendMessageTimeout(
                HWND.HWND_BROADCAST,
                PInvoke.WM_SETTINGCHANGE,
                0,
                (LPARAM)(nint)text,
                SEND_MESSAGE_TIMEOUT_FLAGS.SMTO_ABORTIFHUNG,
                2000,
                &result);
        }
    }
}

/// <summary>Where the taskbar's "show desktop" corner is, taken from the real taskbar window.</summary>
internal static class TaskbarCorner
{
    /// <summary>
    /// Far end of the taskbar on its edge: right end of a horizontal taskbar, bottom end of a vertical one.
    /// The position is read from Shell_TrayWnd, never assumed.
    /// </summary>
    public static bool TryGet(out (int X, int Y) point)
    {
        point = default;
        HWND tray = PInvoke.FindWindow("Shell_TrayWnd", null);
        if (tray == HWND.Null || !PInvoke.GetWindowRect(tray, out RECT rect))
        {
            return false;
        }

        int width = rect.right - rect.left;
        int height = rect.bottom - rect.top;
        point = width >= height
            ? (rect.right - 2, rect.top + (height / 2))
            : (rect.left + (width / 2), rect.bottom - 2);
        return true;
    }
}

/// <summary>Click on the taskbar's show-desktop corner (TaskbarSd = 1); the undo clicks it again.</summary>
internal sealed class CornerTrigger : ZTriggerBase
{
    private readonly TaskbarCornerSetting _setting;
    private readonly bool _allowExplorer;
    private bool _registered;
    private bool _shown;
    private bool _inactiveLogged;

    public CornerTrigger(TaskbarCornerSetting setting, bool allowExplorer)
    {
        _setting = setting;
        _allowExplorer = allowExplorer;
    }

    public override string Name => "corner";

    public override void Fire(ZContext ctx)
    {
        SafetySession session = Session(ctx);
        if (!_registered)
        {
            _registered = true;
            session.OnRestore("corner: click the corner again if the desktop is still shown", ClickBackIfShown);
        }

        _setting.Ensure(ctx, session);
        if (!TaskbarCorner.TryGet(out (int X, int Y) corner))
        {
            ctx.Note("corner-failed", "Shell_TrayWnd not found, nothing clicked");
            return;
        }

        _shown = true;
        Click(corner);
        if (DesktopShown(ctx))
        {
            return;
        }

        if (!_inactiveLogged)
        {
            _inactiveLogged = true;
            ctx.Note("corner-inactive", "the corner click did not show the desktop");
        }

        if (!_allowExplorer)
        {
            ctx.Note("corner-retry", "skipped: restarting Explorer needs --allow-explorer");
            return;
        }

        // The option is only read at start-up on some builds: restart Explorer once and try again.
        ExplorerControl.Restart(ctx, session);
        if (TaskbarCorner.TryGet(out corner))
        {
            Click(corner);
            ctx.Note("corner-retry", DesktopShown(ctx) ? "after the Explorer restart the corner shows the desktop" : "still inactive after the Explorer restart");
        }
    }

    public override void Undo(ZContext ctx) => ClickBackIfShown();

    private static void Click((int X, int Y) corner)
    {
        Input.Click(corner.X, corner.Y);
        Safety.Wait(150);
        (int x, int y) = ScreenCentre();
        Input.MoveAbs(x, y); // leaving the corner ends any hover (Aero Peek)
    }

    private void ClickBackIfShown()
    {
        if (!_shown)
        {
            return;
        }

        _shown = false;
        if (TaskbarCorner.TryGet(out (int X, int Y) corner))
        {
            Click(corner);
        }
    }

    /// <summary>The desktop counts as shown when the witness has been minimized (it is an ordinary window).</summary>
    private static bool DesktopShown(ZContext ctx)
    {
        for (int waited = 0; waited < 1000; waited += 50)
        {
            Safety.Wait(50);
            if (PInvoke.IsIconic(ctx.Witness.Hwnd) || !PInvoke.IsWindowVisible(ctx.Witness.Hwnd))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>Aero Peek by hovering the corner for 2 s (needs TaskbarSd = 1 like the corner click).</summary>
internal sealed class PeekTrigger : ZTriggerBase
{
    private const int HoverMs = 2000;

    private readonly TaskbarCornerSetting _setting;
    private bool _registered;
    private bool _hovering;

    public PeekTrigger(TaskbarCornerSetting setting)
    {
        _setting = setting;
    }

    public override string Name => "peek";

    public override void Fire(ZContext ctx)
    {
        SafetySession session = Session(ctx);
        if (!_registered)
        {
            _registered = true;
            session.OnRestore("peek: move the pointer away from the corner", MoveAway);
        }

        _setting.Ensure(ctx, session);
        if (!TaskbarCorner.TryGet(out (int X, int Y) corner))
        {
            ctx.Note("peek-failed", "Shell_TrayWnd not found, nothing hovered");
            return;
        }

        _hovering = true;
        Input.MoveAbs(corner.X, corner.Y);
        Safety.Wait(HoverMs);
        MoveAway();
    }

    public override void Undo(ZContext ctx) => MoveAway();

    private void MoveAway()
    {
        if (!_hovering)
        {
            return;
        }

        _hovering = false;
        (int x, int y) = ScreenCentre();
        Input.MoveAbs(x, y);
    }
}

/// <summary>Aero Peek by the keyboard: Win+, (comma) held for 2 s.</summary>
internal sealed class PeekKeyTrigger : ZTriggerBase
{
    private const ushort VkOemComma = 0xBC;
    private const int HoldMs = 2000;

    private bool _registered;
    private bool _held;

    public override string Name => "peek-key";

    public override void Fire(ZContext ctx)
    {
        if (!_registered)
        {
            _registered = true;
            Session(ctx).OnRestore("peek-key: release Win and comma", Release);
        }

        _held = true;
        try
        {
            Input.Key(Vk.LWin, true);
            Safety.Wait(Input.GapMs);
            Input.Key(VkOemComma, true);
            Safety.Wait(HoldMs);
        }
        finally
        {
            Release();
        }
    }

    public override void Undo(ZContext ctx) => Release();

    private void Release()
    {
        if (!_held)
        {
            return;
        }

        _held = false;
        Input.Key(VkOemComma, false); // comma first, so that the Win key-up does not open the Start menu
        Input.Key(Vk.LWin, false);
    }
}

/// <summary>
/// Alt+Tab: Alt down, Tab, 500 ms, Tab, then Escape (cancels the switcher, so no window of the developer is
/// brought forward or restored) and Alt up. Afterwards the bar must still be a tool window and must not be the
/// foreground window; whether it shows up in the visual list is the hand part.
/// </summary>
internal sealed class AltTabTrigger : ZTriggerBase
{
    private bool _registered;
    private bool _altDown;

    public override string Name => "alt-tab";

    public override void Fire(ZContext ctx)
    {
        if (!_registered)
        {
            _registered = true;
            Session(ctx).OnRestore("alt-tab: release Alt", ReleaseAlt);
        }

        _altDown = true;
        try
        {
            Input.Key(Vk.LMenu, true);
            Safety.Wait(Input.GapMs);
            Tap(Vk.Tab);
            Safety.Wait(500);
            Tap(Vk.Tab);
            Safety.Wait(150);
            Tap(Vk.Escape);
            Safety.Wait(Input.GapMs);
        }
        finally
        {
            ReleaseAlt();
        }

        var exStyle = (WINDOW_EX_STYLE)(uint)PInvoke.GetWindowLong(ctx.Bar.Hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        bool toolWindow = exStyle.HasFlag(WINDOW_EX_STYLE.WS_EX_TOOLWINDOW);
        bool foregroundIsBar = PInvoke.GetForegroundWindow() == ctx.Bar.Hwnd;
        ctx.Note("alt-tab-check", $"toolwindow={toolWindow} foreground-is-bar={foregroundIsBar}");
        if (!toolWindow || foregroundIsBar)
        {
            ctx.Notes.Add("alt-tab: Leiste ist nicht mehr TOOLWINDOW oder im Vordergrund");
        }
    }

    public override void Undo(ZContext ctx)
    {
        if (PInvoke.GetForegroundWindow() != ctx.Witness.Hwnd)
        {
            DesktopPrep.BringToForeground(ctx.Witness.Hwnd);
        }
    }

    private static void Tap(ushort vk)
    {
        Input.Key(vk, true);
        Input.Key(vk, false);
    }

    private void ReleaseAlt()
    {
        if (!_altDown)
        {
            return;
        }

        _altDown = false;
        Input.Key(Vk.LMenu, false);
    }
}

/// <summary>Stops and restarts Explorer; shared by the explorer trigger and the corner retry.</summary>
internal static class ExplorerControl
{
    private const int RestartWaitMs = 5000;
    private const int ReappearWaitMs = 15000;
    private static bool s_restoreRegistered;

    /// <summary>Kills Explorer, waits up to 5 s for the automatic restart, starts it by hand otherwise, and waits for the shell.</summary>
    public static void Restart(ZContext ctx, SafetySession session)
    {
        if (!s_restoreRegistered)
        {
            s_restoreRegistered = true;
            session.OnRestore("explorer: make sure explorer.exe runs", EnsureRunning);
        }

        RunTool("taskkill", "/f /im explorer.exe");
        ctx.Note("explorer-killed", "taskkill /f /im explorer.exe", note: false);

        var clock = Stopwatch.StartNew();
        WaitUntil(() => !ShellPresent(), 2000); // the shell windows are gone
        bool back = WaitUntil(ShellPresent, RestartWaitMs);
        if (!back)
        {
            ctx.Note("explorer-start", "no automatic restart within 5 s, starting explorer.exe");
            StartExplorer();
            back = WaitUntil(ShellPresent, ReappearWaitMs);
        }

        ctx.Note("explorer-back", string.Create(CultureInfo.InvariantCulture, $"shell present={back} after {clock.ElapsedMilliseconds} ms"));
    }

    /// <summary>Restore action: starts Explorer when no shell is there after a short wait.</summary>
    public static void EnsureRunning()
    {
        if (WaitUntil(ShellPresent, 3000))
        {
            return;
        }

        StartExplorer();
        WaitUntil(ShellPresent, ReappearWaitMs);
    }

    /// <summary>The taskbar window and the desktop host both exist.</summary>
    public static bool ShellPresent() =>
        PInvoke.FindWindow("Shell_TrayWnd", null) != HWND.Null && DesktopHost.Find().Found;

    private static void StartExplorer() =>
        Process.Start(new ProcessStartInfo("explorer.exe") { UseShellExecute = true })?.Dispose();

    private static void RunTool(string file, string arguments)
    {
        using Process? process = Process.Start(new ProcessStartInfo(file, arguments)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        });
        process?.WaitForExit(10_000);
    }

    private static bool WaitUntil(Func<bool> condition, int timeoutMs)
    {
        var clock = Stopwatch.StartNew();
        do
        {
            if (condition())
            {
                return true;
            }

            Safety.Wait(100);
        }
        while (clock.ElapsedMilliseconds < timeoutMs);

        return condition();
    }
}

/// <summary>Explorer restart (taskkill, wait for the shell); the gap is sampled until 20 s after the kill.</summary>
internal sealed class ExplorerTrigger : ZTriggerBase
{
    public override string Name => "explorer";

    public override string? RequiredFlag => "allow-explorer";

    public override int SampleMs => 20_000;

    private bool _registered;

    public override void Fire(ZContext ctx)
    {
        SafetySession session = Session(ctx);
        if (!_registered)
        {
            _registered = true;
            session.OnRestore("explorer: make sure explorer.exe runs", ExplorerControl.EnsureRunning);
        }

        ExplorerControl.Restart(ctx, session);
    }
}

/// <summary>
/// Dynamic resolution change (not persisted) to 1600x900, else 1280x720, at the same refresh rate, for the sampling
/// window; the undo (and the registered restore) go back to the registry mode. The desktop icon layout is backed up
/// first (reg export of the Shell\Bags\1\Desktop key) because a resolution change can rearrange the icons.
/// </summary>
internal sealed unsafe class DisplayTrigger : ZTriggerBase
{
    private static readonly (int Width, int Height)[] s_preferred = [(1600, 900), (1280, 720)];

    private bool _registered;
    private bool _changed;

    public override string Name => "display";

    public override string? RequiredFlag => "allow-display";

    public override void Fire(ZContext ctx)
    {
        if (!_registered)
        {
            _registered = true;
            Session(ctx).OnRestore("display: back to the registry mode", RestoreMode);
        }

        BackupDesktopLayout(ctx);

        DEVMODEW current = NewDevMode();
        if (!PInvoke.EnumDisplaySettings((PCWSTR)null, ENUM_DISPLAY_SETTINGS_MODE.ENUM_CURRENT_SETTINGS, &current))
        {
            ctx.Note("display-failed", "EnumDisplaySettings(current) failed, resolution unchanged");
            return;
        }

        if (!TryFindMode(current, out DEVMODEW target))
        {
            ctx.Note("display-failed", $"no 1600x900 or 1280x720 mode at {current.dmDisplayFrequency} Hz, resolution unchanged");
            return;
        }

        ctx.Note(
            "display-mode",
            string.Create(
                CultureInfo.InvariantCulture,
                $"{current.dmPelsWidth}x{current.dmPelsHeight}@{current.dmDisplayFrequency} -> {target.dmPelsWidth}x{target.dmPelsHeight}@{target.dmDisplayFrequency} (dynamic, not persisted)"));

        _changed = true;
        DISP_CHANGE result = PInvoke.ChangeDisplaySettingsEx((PCWSTR)null, &target, HWND.Null, 0, null);
        if (result != DISP_CHANGE.DISP_CHANGE_SUCCESSFUL)
        {
            _changed = false;
            ctx.Note("display-failed", $"ChangeDisplaySettingsEx returned {result}");
        }
    }

    public override void Undo(ZContext ctx) => RestoreMode();

    private void RestoreMode()
    {
        if (!_changed)
        {
            return;
        }

        _changed = false;
        // No mode and no flags: back to the mode stored in the registry.
        PInvoke.ChangeDisplaySettingsEx((PCWSTR)null, null, HWND.Null, 0, null);
        Safety.Wait(1500);
    }

    private static DEVMODEW NewDevMode()
    {
        var mode = new DEVMODEW();
        mode.dmSize = (ushort)sizeof(DEVMODEW);
        return mode;
    }

    private static bool TryFindMode(DEVMODEW current, out DEVMODEW target)
    {
        foreach ((int width, int height) in s_preferred)
        {
            for (uint index = 0; ; index++)
            {
                DEVMODEW candidate = NewDevMode();
                if (!PInvoke.EnumDisplaySettings((PCWSTR)null, (ENUM_DISPLAY_SETTINGS_MODE)index, &candidate))
                {
                    break;
                }

                if (candidate.dmPelsWidth == (uint)width && candidate.dmPelsHeight == (uint)height &&
                    candidate.dmDisplayFrequency == current.dmDisplayFrequency && candidate.dmBitsPerPel == current.dmBitsPerPel)
                {
                    target = candidate;
                    return true;
                }
            }
        }

        target = default;
        return false;
    }

    /// <summary>reg export of the desktop icon layout into the private results folder (gitignored).</summary>
    private static void BackupDesktopLayout(ZContext ctx)
    {
        string file = Path.Combine(RepoPaths.PrivateResults, $"desktop-bags-{DateTime.Now:yyyyMMdd-HHmmss}.reg");
        using Process? process = Process.Start(new ProcessStartInfo(
            "reg",
            $"export \"HKCU\\Software\\Microsoft\\Windows\\Shell\\Bags\\1\\Desktop\" \"{file}\" /y")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        });
        process?.WaitForExit(15_000);
        ctx.Note("backup", $"reg export of the desktop icon layout -> {(File.Exists(file) ? "private/" + Path.GetFileName(file) : "FAILED")}", note: false);
    }
}

/// <summary>
/// LockWorkStation, then wait until the developer unlocks (OpenInputDesktop succeeds again). Sampling starts at the
/// unlock, so the snapshots at T+0, 0.5, 1 and 2 s are relative to it. Part of hand part H2; the run does not
/// watch for real input because unlocking is real input.
/// </summary>
internal sealed class LockTrigger : ZTriggerBase
{
    private const int LockWaitMs = 10_000;
    private const int UnlockWaitMs = 180_000;

    private bool _registered;

    public override string Name => "lock";

    public override string? RequiredFlag => "allow-lock";

    public override bool HumanParticipates => true;

    public override bool SamplerStartsAfterFire => true;

    public override void Fire(ZContext ctx)
    {
        if (!_registered)
        {
            _registered = true;
            Session(ctx).OnRestore("lock: nothing to restore, the developer unlocks the session", () => { });
        }

        ctx.Note("lock", "locking the workstation now; unlock it with your password", note: false);
        PInvoke.LockWorkStation();

        bool locked = WaitFor(() => !InputDesktopAvailable(), LockWaitMs);
        if (!locked)
        {
            ctx.Note("lock-failed", "the workstation did not lock within 10 s");
            return;
        }

        bool unlocked = WaitFor(InputDesktopAvailable, UnlockWaitMs);
        ctx.Note("unlock", unlocked ? "unlocked, sampling starts" : "not unlocked within 180 s");
    }

    private static bool InputDesktopAvailable()
    {
        HDESK desktop = PInvoke.OpenInputDesktop(0, false, DESKTOP_ACCESS_FLAGS.DESKTOP_SWITCHDESKTOP);
        if (desktop.IsNull)
        {
            return false;
        }

        PInvoke.CloseDesktop(desktop);
        return true;
    }

    private static bool WaitFor(Func<bool> condition, int timeoutMs)
    {
        var clock = Stopwatch.StartNew();
        do
        {
            if (condition())
            {
                return true;
            }

            Safety.Wait(250);
        }
        while (clock.ElapsedMilliseconds < timeoutMs);

        return condition();
    }
}
