using System.IO; // not an implicit using in WPF projects
using Windows.Win32;
using Windows.Win32.UI.Input.KeyboardAndMouse;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MehrBar.Spikes.Common;

public enum PointerButton
{
    Left,
    Right,
}

/// <summary>
/// Where synthetic input goes. Coordinates are screen pixels of the virtual desktop; the real
/// sink normalises them to the 0..65535 range SendInput expects. Tests use <see cref="RecordingSink"/>.
/// </summary>
public interface IInputSink
{
    void Key(ushort vk, bool down);

    void MoveTo(int x, int y);

    void Button(PointerButton button, bool down);
}

/// <summary>Virtual-key codes the harness needs; everything else can be passed as a raw ushort.</summary>
public static class Vk
{
    public const ushort LButton = 0x01;
    public const ushort RButton = 0x02;
    public const ushort Tab = 0x09;
    public const ushort Return = 0x0D;
    public const ushort Escape = 0x1B;
    public const ushort D = 0x44;
    public const ushort M = 0x4D;
    public const ushort Q = 0x51;
    public const ushort LWin = 0x5B;
    public const ushort LShift = 0xA0;
    public const ushort LControl = 0xA2;
    public const ushort LMenu = 0xA4;
}

/// <summary>The real thing. Refuses to run without an active <see cref="SafetySession"/>.</summary>
public sealed unsafe class SendInputSink : IInputSink
{
    public void Key(ushort vk, bool down)
    {
        RequireSession();
        KEYBD_EVENT_FLAGS flags = down ? 0 : KEYBD_EVENT_FLAGS.KEYEVENTF_KEYUP;
        if (IsExtendedKey(vk))
        {
            flags |= KEYBD_EVENT_FLAGS.KEYEVENTF_EXTENDEDKEY;
        }

        var input = new INPUT { type = INPUT_TYPE.INPUT_KEYBOARD };
        input.Anonymous.ki = new KEYBDINPUT { wVk = (VIRTUAL_KEY)vk, dwFlags = flags };
        Send(input);
    }

    public void MoveTo(int x, int y)
    {
        RequireSession();
        int left = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_XVIRTUALSCREEN);
        int top = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_YVIRTUALSCREEN);
        int width = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXVIRTUALSCREEN);
        int height = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYVIRTUALSCREEN);

        var input = new INPUT { type = INPUT_TYPE.INPUT_MOUSE };
        input.Anonymous.mi = new MOUSEINPUT
        {
            dx = Normalise(x - left, width),
            dy = Normalise(y - top, height),
            dwFlags = MOUSE_EVENT_FLAGS.MOUSEEVENTF_MOVE |
                      MOUSE_EVENT_FLAGS.MOUSEEVENTF_ABSOLUTE |
                      MOUSE_EVENT_FLAGS.MOUSEEVENTF_VIRTUALDESK,
        };
        Send(input);
    }

    public void Button(PointerButton button, bool down)
    {
        RequireSession();
        MOUSE_EVENT_FLAGS flags = (button, down) switch
        {
            (PointerButton.Left, true) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTDOWN,
            (PointerButton.Left, false) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_LEFTUP,
            (PointerButton.Right, true) => MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTDOWN,
            _ => MOUSE_EVENT_FLAGS.MOUSEEVENTF_RIGHTUP,
        };

        var input = new INPUT { type = INPUT_TYPE.INPUT_MOUSE };
        input.Anonymous.mi = new MOUSEINPUT { dwFlags = flags };
        Send(input);
    }

    /// <summary>Screen pixel to the 0..65535 range: px * 65535 / (extent - 1), clamped.</summary>
    internal static int Normalise(int offsetPx, int extentPx)
    {
        int span = Math.Max(1, extentPx - 1);
        long value = (long)offsetPx * 65535 / span;
        return (int)Math.Clamp(value, 0, 65535);
    }

    private static bool IsExtendedKey(ushort vk) =>
        vk is 0x5B or 0x5C or 0xA3 or 0xA5 or 0x25 or 0x26 or 0x27 or 0x28; // Win keys, right Ctrl/Alt, arrows

    private static void RequireSession()
    {
        if (Safety.Current == null)
        {
            throw new InvalidOperationException(
                "real input requires an active SafetySession (CaseContext.BeginSafety)");
        }
    }

    private static void Send(INPUT input)
    {
        uint sent = PInvoke.SendInput(new ReadOnlySpan<INPUT>(in input), sizeof(INPUT));
        if (sent != 1)
        {
            // Typically UIPI: the foreground window runs elevated. Surface it, never swallow it.
            throw new IOException("SendInput injected 0 events (blocked by UIPI or desktop locked?)");
        }
    }
}

/// <summary>Collects events instead of injecting them. Used by the self-tests.</summary>
public sealed class RecordingSink : IInputSink
{
    private readonly object _gate = new();
    private readonly List<string> _events = [];

    public IReadOnlyList<string> Events
    {
        get
        {
            lock (_gate)
            {
                return _events.ToArray();
            }
        }
    }

    public void Key(ushort vk, bool down) => Add($"key:{vk:X2}:{(down ? "down" : "up")}");

    public void MoveTo(int x, int y) => Add($"move:{x},{y}");

    public void Button(PointerButton button, bool down) =>
        Add($"button:{button.ToString().ToLowerInvariant()}:{(down ? "down" : "up")}");

    private void Add(string text)
    {
        lock (_gate)
        {
            _events.Add(text);
        }
    }
}

/// <summary>
/// Synthetic input for the Driver. Every call first asks the active <see cref="SafetySession"/>
/// whether the run was aborted and throws <see cref="OperationCanceledException"/> if so. The
/// class remembers which keys this process pressed and has not released, so that the session can
/// release exactly those (never blind key-ups: a key Stephan holds himself stays held).
/// </summary>
public static class Input
{
    private static readonly object s_gate = new();
    private static readonly List<ushort> s_pressed = [];
    private static IInputSink s_sink = new SendInputSink();

    /// <summary>Pause between the events of a chord or click, in milliseconds.</summary>
    public static int GapMs { get; set; } = 20;

    /// <summary>Keys (and mouse buttons as VK 1/2) pressed through this class and not yet released.</summary>
    public static IReadOnlyList<ushort> PressedKeys
    {
        get
        {
            lock (s_gate)
            {
                return s_pressed.ToArray();
            }
        }
    }

    /// <summary>Redirect all input to <paramref name="sink"/> until the returned scope is disposed.</summary>
    public static IDisposable UseSink(IInputSink sink)
    {
        IInputSink previous;
        lock (s_gate)
        {
            previous = s_sink;
            s_sink = sink;
        }

        return new SinkScope(previous);
    }

    public static void Key(ushort vk, bool down)
    {
        Safety.ThrowIfAborted();
        if (down)
        {
            // Track before sending: a send that fails half-way must still be released later.
            lock (s_gate)
            {
                if (!s_pressed.Contains(vk))
                {
                    s_pressed.Add(vk);
                }
            }

            Sink.Key(vk, true);
        }
        else
        {
            Sink.Key(vk, false);
            Forget(vk);
        }
    }

    /// <summary>Downs in order, ups in reverse order, <see cref="GapMs"/> apart. Always releases what it pressed.</summary>
    public static void Chord(params ushort[] keys)
    {
        var pressedHere = new List<ushort>();
        try
        {
            foreach (ushort vk in keys)
            {
                Key(vk, true);
                pressedHere.Add(vk);
                Safety.Wait(GapMs);
            }
        }
        finally
        {
            for (int i = pressedHere.Count - 1; i >= 0; i--)
            {
                ReleaseUnchecked(pressedHere[i]);
                if (i > 0)
                {
                    Safety.Wait(GapMs);
                }
            }
        }
    }

    /// <summary>Absolute move to a screen pixel of the virtual desktop.</summary>
    public static void MoveAbs(int x, int y)
    {
        Safety.ThrowIfAborted();
        Sink.MoveTo(x, y);
    }

    public static void Click(int x, int y, PointerButton button = PointerButton.Left)
    {
        MoveAbs(x, y);
        Safety.Wait(GapMs);
        ushort vk = button == PointerButton.Left ? Vk.LButton : Vk.RButton;
        Safety.ThrowIfAborted();
        Track(vk);
        Sink.Button(button, true);
        try
        {
            Safety.Wait(GapMs);
        }
        finally
        {
            ReleaseUnchecked(vk);
        }
    }

    public static void RightClick(int x, int y) => Click(x, y, PointerButton.Right);

    /// <summary>
    /// Releases everything still pressed, newest first, bypassing the abort check. Called by
    /// <see cref="SafetySession.Dispose"/>; failures are logged and never thrown.
    /// </summary>
    internal static void ReleaseAllTracked(Action<string> log)
    {
        foreach (ushort vk in PressedKeys.Reverse())
        {
            try
            {
                ReleaseUnchecked(vk);
                log($"released key 0x{vk:X2}");
            }
            catch (Exception ex)
            {
                log($"release of key 0x{vk:X2} failed: {ex.Message}");
            }
        }
    }

    private static IInputSink Sink
    {
        get
        {
            lock (s_gate)
            {
                return s_sink;
            }
        }
    }

    private static void Track(ushort vk)
    {
        lock (s_gate)
        {
            if (!s_pressed.Contains(vk))
            {
                s_pressed.Add(vk);
            }
        }
    }

    private static void Forget(ushort vk)
    {
        lock (s_gate)
        {
            s_pressed.Remove(vk);
        }
    }

    private static void ReleaseUnchecked(ushort vk)
    {
        if (vk == Vk.LButton)
        {
            Sink.Button(PointerButton.Left, false);
        }
        else if (vk == Vk.RButton)
        {
            Sink.Button(PointerButton.Right, false);
        }
        else
        {
            Sink.Key(vk, false);
        }

        Forget(vk);
    }

    private sealed class SinkScope(IInputSink previous) : IDisposable
    {
        public void Dispose()
        {
            lock (s_gate)
            {
                s_sink = previous;
            }
        }
    }
}
