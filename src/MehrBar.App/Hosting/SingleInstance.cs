namespace MehrBar.App.Hosting;

/// <summary>
/// Only one MehrBar per logon session (STACK.md section 7): a named mutex in the <c>Local\</c> namespace, held in a
/// static field until the process ends. Opening the configuration of the running instance is APP-02, phase 6.
/// </summary>
internal static class SingleInstance
{
    private const string MutexName = @"Local\MehrBar.SingleInstance.B1C7A36B-A2F6-4D2F-AF9E-FA619DA8047B";

    private static Mutex? s_mutex;

    /// <summary>True when this process is the first instance.</summary>
    public static bool TryAcquire()
    {
        try
        {
            var mutex = new Mutex(initiallyOwned: true, MutexName, out bool created);
            if (!created)
            {
                mutex.Dispose();
                return false;
            }

            s_mutex = mutex;
            return true;
        }
        catch (AbandonedMutexException)
        {
            // The previous owner died without releasing it; this process owns it now.
            return true;
        }
    }
}
