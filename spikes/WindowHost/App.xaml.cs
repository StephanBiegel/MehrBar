using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MehrBar.Spikes.WindowHost;

public partial class App : Application
{
    private Window? _window;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        int exitAfterMs = ParseExitAfterMs(e.Args);

        _window = new Window
        {
            Width = 10,
            Height = 10,
            WindowStyle = WindowStyle.None,
            AllowsTransparency = true,
            Background = System.Windows.Media.Brushes.Transparent,
            ShowActivated = false,
            ShowInTaskbar = false,
        };

        // Once the HWND exists, call the generated APIs so CsWin32 generation is proven at runtime.
        _window.SourceInitialized += (_, _) => ApplyNativeStyles(_window);
        _window.Show();

        if (exitAfterMs > 0)
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(exitAfterMs) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                Shutdown(0);
            };
            timer.Start();
        }
    }

    private static int ParseExitAfterMs(string[] args)
    {
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], "--exit-after-ms", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(args[i + 1], out int ms))
            {
                return ms;
            }
        }

        return 0;
    }

    private static void ApplyNativeStyles(Window window)
    {
        var hwnd = new HWND(new WindowInteropHelper(window).Handle);

        int exStyle = PInvoke.GetWindowLong(hwnd, WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE);
        PInvoke.SetWindowLong(
            hwnd,
            WINDOW_LONG_PTR_INDEX.GWL_EXSTYLE,
            exStyle | (int)WINDOW_EX_STYLE.WS_EX_NOACTIVATE);

        PInvoke.SetWindowPos(
            hwnd,
            HWND.HWND_BOTTOM,
            0, 0, 0, 0,
            SET_WINDOW_POS_FLAGS.SWP_NOMOVE |
            SET_WINDOW_POS_FLAGS.SWP_NOSIZE |
            SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);
    }
}
