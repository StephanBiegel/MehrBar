using System.ComponentModel;
using System.Diagnostics;
using MehrBar.App.Interop;

namespace MehrBar.App.Shell;

/// <summary>
/// Starts an entry (ITEM-01). The signature stays stable for the whole phase; 02-09 fills in the resolver and the
/// "target missing" dialog (D-10).
/// </summary>
internal sealed class ShellLauncher
{
    /// <summary>Starts <paramref name="path"/> through the shell; <paramref name="removeEntry"/> removes the entry.</summary>
    public void Launch(string path, Action removeEntry)
    {
        // The bar never activates, so the started program needs explicit permission to come to the front.
        BarHwnd.AllowLaunchedProcessForeground();
        try
        {
            using Process? process = Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            // Logged from 02-04 on; the dialog for a missing target follows in 02-09.
        }
    }
}
