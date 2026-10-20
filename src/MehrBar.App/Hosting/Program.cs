using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using MehrBar.App.Bars;

namespace MehrBar.App.Hosting;

/// <summary>Entry point without App.xaml: creates the bar controller and runs the dispatcher.</summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        int exitAfterMs = IntArg(args, "--exit-after-ms", 0);
        byte plateAlpha = (byte)Math.Clamp(IntArg(args, "--plate-alpha", 1), 1, 255);
        bool plateOutline = args.Contains("--plate-outline");

        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };

        var controller = new BarController(plateAlpha, plateOutline);
        controller.Show();

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

    private static int IntArg(string[] args, string name, int fallback)
    {
        int index = Array.IndexOf(args, name);
        return index >= 0 && index + 1 < args.Length
            && int.TryParse(args[index + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)
            ? value
            : fallback;
    }
}
