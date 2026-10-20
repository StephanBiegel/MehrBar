using System.Windows;

namespace MehrBar.App.Bars;

/// <summary>
/// OLE drop on the bar (DND-01). Accepts file lists with the link effect, otherwise copy, and never the move effect:
/// the source would delete the originals (RESEARCH Pitfall 7). Paths are only taken as strings; nothing is opened or
/// run on drop.
/// </summary>
internal sealed class DropTarget
{
    private readonly Action<IReadOnlyList<string>> _append;

    public DropTarget(UIElement element, Action<IReadOnlyList<string>> append)
    {
        _append = append;
        element.DragEnter += OnDragOver;
        element.DragOver += OnDragOver;
        element.Drop += OnDrop;
    }

    private static void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = ChooseEffect(e);
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        DragDropEffects effect = ChooseEffect(e);
        e.Effects = effect;
        e.Handled = true;
        if (effect == DragDropEffects.None)
        {
            return;
        }

        string[]? paths = null;
        try
        {
            paths = e.Data.GetData(DataFormats.FileDrop) as string[];
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or InvalidOperationException)
        {
            // Some sources throw while rendering the list; the drop then simply adds nothing.
        }

        if (paths is { Length: > 0 })
        {
            _append(paths);
        }
    }

    /// <summary>Link if the source allows it, else copy, else none (moves 02-06 into Core as DropEffectChooser).</summary>
    private static DragDropEffects ChooseEffect(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            return DragDropEffects.None;
        }

        if (e.AllowedEffects.HasFlag(DragDropEffects.Link))
        {
            return DragDropEffects.Link;
        }

        return e.AllowedEffects.HasFlag(DragDropEffects.Copy) ? DragDropEffects.Copy : DragDropEffects.None;
    }
}
