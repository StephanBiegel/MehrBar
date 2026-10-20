using System.Windows;
using MehrBar.Core.Logging;

namespace MehrBar.App.Hosting;

/// <summary>Logs all three unhandled-error paths; the dock never ends itself on purpose.</summary>
internal static class CrashHandlers
{
    public static void Install(Application app, IAppLog log)
    {
        app.DispatcherUnhandledException += (_, e) =>
        {
            log.Error("unhandled-dispatcher", e.Exception);
            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            log.Error($"unhandled-domain terminating={e.IsTerminating}", e.ExceptionObject as Exception);

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            log.Error("unobserved-task", e.Exception);
            e.SetObserved();
        };
    }
}
