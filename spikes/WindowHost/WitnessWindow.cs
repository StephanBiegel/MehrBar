using System.Globalization;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using MehrBar.Spikes.Common;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MehrBar.Spikes.WindowHost;

/// <summary>
/// Role "witness": an ordinary, activatable window filled with the cover colour (CoverRgb, #00FFFF).
/// The Driver uses it as the deterministic stand-in for "another application" in the focus test,
/// the cover (Deckungs-) test and the menu test. It logs every activation and focus change to its CSV.
/// </summary>
internal sealed class WitnessWindow : Window
{
    private const int NeutralWidth = 300;
    private const int NeutralHeight = 120;

    private readonly HostOptions _options;
    private readonly RunLog _log;
    private readonly IntRect _screenRect;

    public WitnessWindow(HostOptions options, RunLog log)
    {
        _options = options;
        _log = log;
        _screenRect = ResolveScreenRect(options);

        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        // --activate false shows the witness without taking the foreground (dry runs on a desktop in use).
        ShowActivated = !(options.All.TryGetValue("activate", out string? activate) &&
                          string.Equals(activate, "false", StringComparison.OrdinalIgnoreCase));
        Topmost = false;
        Title = options.Title;
        Focusable = true;

        // ContentRendered (which triggers the READY line) only fires for a window that has content.
        Content = new System.Windows.Controls.Grid();
        Background = new SolidColorBrush(
            Color.FromRgb(
                (byte)((SpikeGeometry.CoverRgb >> 16) & 0xFF), (byte)((SpikeGeometry.CoverRgb >> 8) & 0xFF), (byte)(SpikeGeometry.CoverRgb & 0xFF)));

        // Provisional size (100 % scaling); the exact pixel rectangle is applied in OnSourceInitialized.
        Width = _screenRect.Width;
        Height = _screenRect.Height;

        IsKeyboardFocusWithinChanged += (_, e) => Log("focus", $"within={(bool)e.NewValue}");
        GotKeyboardFocus += (_, e) => Log("keyboard-focus", $"got={e.NewFocus}");
        LostKeyboardFocus += (_, e) => Log("keyboard-focus", $"lost={e.OldFocus}");
    }

    public IntRect ScreenRect => _screenRect;

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var source = (HwndSource)PresentationSource.FromVisual(this)!;
        var hwnd = new HWND(source.Handle);

        double scale = source.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        Width = _screenRect.Width / scale;
        Height = _screenRect.Height / scale;

        PInvoke.SetWindowPos(
            hwnd,
            HWND.Null,
            _screenRect.X,
            _screenRect.Y,
            _screenRect.Width,
            _screenRect.Height,
            SET_WINDOW_POS_FLAGS.SWP_NOZORDER | SET_WINDOW_POS_FLAGS.SWP_NOACTIVATE);

        source.AddHook(WndProc);
        Log("placed", string.Create(
            CultureInfo.InvariantCulture,
            $"cover={_options.Cover} rect={_screenRect.X},{_screenRect.Y},{_screenRect.Width},{_screenRect.Height}"));
    }

    private static IntRect ResolveScreenRect(HostOptions options)
    {
        switch (options.Cover)
        {
            case "full":
                return SpikeGeometry.WitnessFull.Offset(options.X, options.Y);
            case "partial":
                return SpikeGeometry.WitnessPartial.Offset(options.X, options.Y);
            default:
                int screenWidth = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSCREEN);
                int screenHeight = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CYSCREEN);
                return new IntRect(
                    (screenWidth - NeutralWidth) / 2,
                    (screenHeight - NeutralHeight) / 2,
                    NeutralWidth,
                    NeutralHeight);
        }
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if ((uint)msg == PInvoke.WM_ACTIVATE)
        {
            uint state = (uint)((long)wParam & 0xFFFF);
            string name = state switch
            {
                PInvoke.WA_INACTIVE => "inactive",
                PInvoke.WA_ACTIVE => "active",
                PInvoke.WA_CLICKACTIVE => "click-active",
                _ => "unknown",
            };
            Log("activate", $"state={name}");
        }

        return IntPtr.Zero;
    }

    private void Log(string evt, string detail) =>
        _log.Write("witness", _options.Cover, string.Empty, evt, detail);
}
