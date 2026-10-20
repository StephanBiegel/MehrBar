using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using MehrBar.App.Bars;
using MehrBar.App.Interop;
using MehrBar.Core.Layout;

namespace MehrBar.App.Hosting;

/// <summary>
/// Entry point without App.xaml. Places one empty bar at the top centre of the primary monitor with the default values
/// of Q2 (edge offset −5) until the configuration arrives in 02-07.
/// </summary>
internal static class Program
{
    // Provisional bar metrics (D-08, D-09): icon size 32, inner margin 6, an empty bar shows three slots.
    private const int IconSize = 32;
    private const int Padding = 6;
    private const int EmptySlots = 3;

    // Provisional zoom overhang around the bar (RESEARCH Pattern 3); 02-11 replaces it with the WindowPlan of 02-08.
    private const int OverhangAlong = 230;
    private const int OverhangDepth = 89;

    [STAThread]
    private static int Main(string[] args)
    {
        int exitAfterMs = IntArg(args, "--exit-after-ms", 0);
        byte plateAlpha = (byte)Math.Clamp(IntArg(args, "--plate-alpha", 1), 1, 255);
        bool plateOutline = args.Contains("--plate-outline");

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

        RectI monitor = BarHwnd.PrimaryMonitorRect();
        var spec = new PlacementSpec(BarEdge.Top, BarAlign.Center, 0, -5);
        int length = (2 * Padding) + (EmptySlots * IconSize);
        int depth = (2 * Padding) + IconSize;
        RectI bar = BarPlacement.Compute(monitor, spec, length, depth);
        (RectI window, RectI barInWindow) = WithOverhang(monitor, spec.Edge, bar);

        var surface = new BarSurface { PlateAlpha = plateAlpha, ShowPlateOutline = plateOutline };
        var barWindow = new BarWindow(window, barInWindow, surface);
        barWindow.Show();

        if (exitAfterMs > 0)
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(exitAfterMs) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                app.Shutdown();
            };
            timer.Start();
        }

        return app.Run();
    }

    /// <summary>
    /// Window rectangle = bar plus the overhang along the edge and away from it, clamped to the monitor along the edge;
    /// the clicks in the overhang pass through because only the plate is a hit target.
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

    private static int IntArg(string[] args, string name, int fallback)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length
            && int.TryParse(args[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : fallback;
    }
}
