using MehrBar.Spikes.Common;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace MehrBar.Spikes.Driver;

/// <summary>
/// Preparation of the real desktop for the input-driven cases. Both members change what the developer
/// sees, so they are only ever called from real runs inside a <see cref="SafetySession"/>, after the
/// hands-off checkpoint (RESEARCH Pitfall 3); dry runs never call them.
/// </summary>
internal static class DesktopPrep
{
    private const int SettleMs = 600;
    private const int ForegroundPollMs = 400;

    /// <summary>
    /// Minimizes every window so the top screen area is free (the bar is only visible when nothing
    /// covers it). The restore action is registered on the session first; only then is the minimize sent,
    /// so an abort or a crash right after still brings the windows back.
    /// </summary>
    public static void ClearTopArea(SafetySession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (!ReferenceEquals(Safety.Current, session))
        {
            throw new InvalidOperationException("ClearTopArea needs the active SafetySession of this process");
        }

        // Registered BEFORE anything is minimized (Pitfall 3).
        session.OnRestore("restore minimized windows (Win+Shift+M)", RestoreWindows);

        Input.Chord(Vk.LWin, Vk.M);
        Safety.Wait(SettleMs);
    }

    /// <summary>
    /// Makes <paramref name="hwnd"/> the foreground window. Allows foreground changes first; when the
    /// foreground lock still refuses, taps Alt once (the documented way to let the next
    /// SetForegroundWindow through; the tap needs a session because it is synthetic input) and retries.
    /// Returns whether the window really is the foreground window afterwards.
    /// </summary>
    public static bool BringToForeground(HWND hwnd)
    {
        PInvoke.AllowSetForegroundWindow(PInvoke.ASFW_ANY);
        if (TrySet(hwnd))
        {
            return true;
        }

        if (Safety.Current == null)
        {
            return false;
        }

        Input.Key(Vk.LMenu, true);
        Input.Key(Vk.LMenu, false);
        Safety.Wait(30);
        return TrySet(hwnd);
    }

    private static void RestoreWindows() => Input.Chord(Vk.LWin, Vk.LShift, Vk.M);

    private static bool TrySet(HWND hwnd)
    {
        PInvoke.SetForegroundWindow(hwnd);
        for (int waited = 0; waited < ForegroundPollMs; waited += 50)
        {
            if (PInvoke.GetForegroundWindow() == hwnd)
            {
                return true;
            }

            Safety.Wait(50);
        }

        return PInvoke.GetForegroundWindow() == hwnd;
    }
}
