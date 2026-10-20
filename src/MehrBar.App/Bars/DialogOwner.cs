using System.Windows;
using System.Windows.Interop;
using Microsoft.Win32;

namespace MehrBar.App.Bars;

/// <summary>
/// Owner for dialogs: a hidden, never shown window. The bar itself must never own a dialog, because it never activates
/// and lives at the bottom of the z-order (RESEARCH Pattern 9).
/// </summary>
internal sealed class DialogOwner
{
    private readonly Window _owner;

    public DialogOwner()
    {
        _owner = new Window
        {
            WindowStyle = WindowStyle.None,
            ShowInTaskbar = false,
            ShowActivated = false,
            Width = 0,
            Height = 0,
        };
        new WindowInteropHelper(_owner).EnsureHandle();
    }

    /// <summary>The hidden owner window.</summary>
    public Window Owner => _owner;

    /// <summary>File picker with multi-select; shortcuts are returned as the .lnk path, not resolved (DND-02).</summary>
    public string[] PickFiles()
    {
        var dialog = new OpenFileDialog { DereferenceLinks = false, Multiselect = true };
        return dialog.ShowDialog(_owner) == true ? dialog.FileNames : [];
    }
}
