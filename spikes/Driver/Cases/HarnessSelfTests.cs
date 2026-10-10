using MehrBar.Spikes.Common;

namespace MehrBar.Spikes.Driver.Cases;

/// <summary>
/// Proves the safety net without injecting any real input: a <see cref="RecordingSink"/> takes the
/// place of SendInput and no real-input watcher is installed (that part is exercised by hand, plan 01-17).
/// </summary>
[DriverCase("safety-selftest")]
public sealed class SafetySelfTest : IDriverCase
{
    public int Run(CaseContext ctx)
    {
        int failures = 0;

        void Check(string what, bool ok)
        {
            Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {what}");
            if (!ok)
            {
                failures++;
            }
        }

        // 1. The real sink must refuse to run without a session; this throws before any SendInput call.
        bool refused = false;
        try
        {
            new SendInputSink().Key(Vk.D, true);
        }
        catch (InvalidOperationException)
        {
            refused = true;
        }

        Check("SendInputSink refuses to run without a SafetySession", refused);

        var sink = new RecordingSink();
        var order = new List<int>();
        using (Input.UseSink(sink))
        {
            Input.GapMs = 1;
            SafetySession session = ctx.BeginSafety(TimeSpan.FromSeconds(30), watchRealInput: false);

            string? hotkey = session.HotkeyName;
            Check($"abort hotkey registered ({hotkey})", hotkey is "Ctrl+Alt+Q" or "Ctrl+Alt+Shift+Q");
            Check("hotkey is held by the session (a second registration fails)",
                hotkey != null && session.HotkeyRegistered && !SafetySession.CanRegisterHotkey(hotkey));
            Check("watcher thread is running", session.WatcherThreadAlive);
            Check("no hooks installed with watchRealInput false", !session.HooksInstalled);

            session.OnRestore("restore-1", () => order.Add(1));
            session.OnRestore("restore-2", () => order.Add(2));
            session.OnRestore("restore-3", () => order.Add(3));

            // Chord: downs in order, ups reversed, nothing left pressed.
            Input.Chord(Vk.LWin, Vk.D);
            Check("chord sends down,down,up,up in reverse order",
                sink.Events.SequenceEqual(["key:5B:down", "key:44:down", "key:44:up", "key:5B:up"]));
            Check("chord leaves no pressed key", Input.PressedKeys.Count == 0);

            // A modifier the Driver pressed itself and never released.
            const ushort fakeModifier = Vk.LShift;
            int eventsBefore = sink.Events.Count;
            Input.Key(fakeModifier, true);
            Check("PressedKeys tracks the fake modifier", Input.PressedKeys.SequenceEqual([fakeModifier]));

            session.AbortForTest("selftest");
            Check("AbortReason is the test reason", session.AbortReason == "selftest");
            Check("token is cancelled", session.Token.IsCancellationRequested);

            bool keyThrew = ThrowsCanceled(() => Input.Key(Vk.D, true));
            bool moveThrew = ThrowsCanceled(() => Input.MoveAbs(10, 10));
            bool clickThrew = ThrowsCanceled(() => Input.Click(10, 10));
            Check("Input.Key throws after abort", keyThrew);
            Check("Input.MoveAbs throws after abort", moveThrew);
            Check("Input.Click throws after abort", clickThrew);
            Check("aborted calls reached the sink never", sink.Events.Count == eventsBefore + 1);

            session.Dispose();
            Check("restore actions ran newest first (3-2-1)", order.SequenceEqual([3, 2, 1]));

            var afterAbort = sink.Events.Skip(eventsBefore + 1).ToArray();
            Check("exactly one key-up was sent, for the fake modifier",
                afterAbort.Length == 1 && afterAbort[0] == $"key:{fakeModifier:X2}:up");
            Check("no pressed key remains", Input.PressedKeys.Count == 0);
            Check("session slot is free again", Safety.Current == null);

            session.Dispose(); // idempotent
            Check("second Dispose is a no-op", order.Count == 3);

            Check("hotkey is unregistered after Dispose",
                !session.HotkeyRegistered && hotkey != null && SafetySession.CanRegisterHotkey(hotkey));
            Check("watcher thread exited after Dispose", !session.WatcherThreadAlive);

            // Optional (not part of the default run): install the real low-level hooks for a moment to prove
            // they install and are removed again. Still injects nothing, but it briefly sits in the input path.
            if (ctx.GetBool("with-hooks"))
            {
                SafetySession hooked = ctx.BeginSafety(TimeSpan.FromSeconds(30), watchRealInput: true);
                Check("low-level keyboard and mouse hooks installed", hooked.HooksInstalled);
                hooked.Dispose();
                Check("hooks removed and watcher exited after Dispose", !hooked.HooksInstalled && !hooked.WatcherThreadAlive);
            }
        }

        Console.WriteLine(failures == 0 ? "SAFETY OK" : $"SAFETY FAILED ({failures})");
        return failures == 0 ? ExitCodes.Ok : ExitCodes.CriteriaFailed;
    }

    private static bool ThrowsCanceled(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (OperationCanceledException)
        {
            return true;
        }
    }
}
