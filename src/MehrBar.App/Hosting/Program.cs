using System.Globalization;
using System.Reflection;
using System.Security.Principal;
using System.Windows;
using System.Windows.Threading;
using MehrBar.App.Bars;
using MehrBar.Core.Logging;

namespace MehrBar.App.Hosting;

/// <summary>
/// Entry point without App.xaml. Order: data folder, single instance (a second start ends silently before WPF loads),
/// log, crash handlers, bar, dispatcher.
/// </summary>
internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        AppPaths paths = AppPaths.FromArgs(args);
        if (!SingleInstance.TryAcquire())
        {
            return 0;
        }

        using var log = new RollingFileLog(paths.LogsDir);
        AppLog.Current = log;
        try
        {
            LogStartup(log, paths);

            int exitAfterMs = IntArg(args, "--exit-after-ms", 0);
            byte plateAlpha = (byte)Math.Clamp(IntArg(args, "--plate-alpha", 1), 1, 255);
            bool plateOutline = args.Contains("--plate-outline");

            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            CrashHandlers.Install(app, log);

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
        finally
        {
            log.Info("exit");
            AppLog.Current = NullAppLog.Instance;
        }
    }

    private static void LogStartup(IAppLog log, AppPaths paths)
    {
        string version = typeof(Program).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? "unknown";
        bool elevated;
        using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
        {
            elevated = new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }

        log.Info(string.Create(
            CultureInfo.InvariantCulture,
            $"startup version={version} os={Environment.OSVersion.Version} pid={Environment.ProcessId} elevated={(elevated ? "true" : "false")} data={paths.DataDir}"));
        if (elevated)
        {
            log.Warn("startup-elevated: drag and drop from Explorer is blocked by UIPI");
        }
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
