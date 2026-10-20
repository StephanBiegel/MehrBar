using System.Windows;
using MehrBar.App.Interop;
using MehrBar.App.Shell;
using MehrBar.Core.Layout;

namespace MehrBar.App.Bars;

/// <summary>
/// Provisional controller of one bar: the entries are a list of paths in memory until the ConfigStore takes over in
/// 02-07. Places the bar (length grows with the entries, at least three slots, D-08), appends dropped and picked paths
/// (duplicates allowed, D-12), starts an entry on click and opens the file picker on right click.
/// </summary>
internal sealed class BarController
{
    // Provisional bar metrics (D-08, D-09): icon size 32, inner margin 6, an empty bar shows three slots.
    private const int IconSize = 32;
    private const int Padding = 6;
    private const int MinSlots = 3;

    // Provisional zoom overhang around the bar (RESEARCH Pattern 3); 02-11 replaces it with the WindowPlan of 02-08.
    private const int OverhangAlong = 230;
    private const int OverhangDepth = 89;

    private readonly List<string> _paths = [];
    private readonly PlacementSpec _spec = new(BarEdge.Top, BarAlign.Center, 0, -5);
    private readonly ShellLauncher _launcher = new();
    private readonly DialogOwner _dialogs = new();
    private readonly BarWindow _window;

    public BarController(byte plateAlpha, bool plateOutline)
    {
        (RectI window, RectI barInWindow) = ComputeBounds();
        var surface = new BarSurface
        {
            PlateAlpha = plateAlpha,
            ShowPlateOutline = plateOutline,
            IconSize = IconSize,
            Padding = Padding,
        };
        _window = new BarWindow(window, barInWindow, surface);
        _window.PathsDropped += AppendPaths;
        _window.SlotClicked += OnSlotClicked;
        _window.ContextRequested += OnContextRequested;
    }

    /// <summary>Shows the bar window.</summary>
    public void Show() => _window.Show();

    /// <summary>Appends paths in order (duplicates allowed) and places the bar again.</summary>
    public void AppendPaths(IReadOnlyList<string> paths)
    {
        if (paths.Count == 0)
        {
            return;
        }

        _paths.AddRange(paths);
        Relayout();
    }

    /// <summary>Removes the entry in <paramref name="slot"/> and places the bar again.</summary>
    public void RemoveAt(int slot)
    {
        if (slot < 0 || slot >= _paths.Count)
        {
            return;
        }

        _paths.RemoveAt(slot);
        Relayout();
    }

    private void OnSlotClicked(int slot)
    {
        if (slot < 0 || slot >= _paths.Count)
        {
            return;
        }

        string path = _paths[slot];
        _launcher.Launch(path, () => RemoveAt(slot));
        _window.Native.PlaceBottom();
    }

    private void OnContextRequested(Point screen)
    {
        // Provisional trigger; 02-13 puts the native context menu in front of it, the call stays.
        AppendPaths(_dialogs.PickFiles());
    }

    private void Relayout()
    {
        (RectI window, RectI barInWindow) = ComputeBounds();
        _window.Surface.SlotCount = _paths.Count;
        _window.ApplyBounds(window, barInWindow);
    }

    private (RectI Window, RectI BarInWindow) ComputeBounds()
    {
        RectI monitor = BarHwnd.PrimaryMonitorRect();
        int length = (2 * Padding) + (Math.Max(MinSlots, _paths.Count) * IconSize);
        int depth = (2 * Padding) + IconSize;
        RectI bar = BarPlacement.Compute(monitor, _spec, length, depth);
        return WithOverhang(monitor, _spec.Edge, bar);
    }

    /// <summary>
    /// Window rectangle = bar plus the overhang along the edge and away from it, clamped to the monitor along the edge;
    /// clicks in the overhang pass through because only the plate is a hit target.
    /// </summary>
    private static (RectI Window, RectI BarInWindow) WithOverhang(RectI monitor, BarEdge edge, RectI bar)
    {
        bool horizontal = edge is BarEdge.Top or BarEdge.Bottom;
        int left;
        int top;
        int right;
        int bottom;
        if (horizontal)
        {
            left = Math.Max(monitor.Left, bar.Left - OverhangAlong);
            right = Math.Min(monitor.Right, bar.Right + OverhangAlong);
            top = edge == BarEdge.Top ? bar.Top : bar.Top - OverhangDepth;
            bottom = edge == BarEdge.Top ? bar.Bottom + OverhangDepth : bar.Bottom;
        }
        else
        {
            top = Math.Max(monitor.Top, bar.Top - OverhangAlong);
            bottom = Math.Min(monitor.Bottom, bar.Bottom + OverhangAlong);
            left = edge == BarEdge.Left ? bar.Left : bar.Left - OverhangDepth;
            right = edge == BarEdge.Left ? bar.Right + OverhangDepth : bar.Right;
        }

        var window = new RectI(left, top, right - left, bottom - top);
        var barInWindow = new RectI(bar.Left - left, bar.Top - top, bar.Width, bar.Height);
        return (window, barInWindow);
    }
}
