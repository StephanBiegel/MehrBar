using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using MehrBar.App.Interop;
using MehrBar.Core.Layout;

namespace MehrBar.App.Bars;

/// <summary>
/// The transparent bar window. WPF types only; everything Win32 goes through <see cref="BarHwnd"/>. Sizes are kept in
/// device pixels: the content is scaled by 1/DPI so one WPF unit equals one pixel (01-SPIKE-window-hit.md).
/// </summary>
internal sealed class BarWindow : Window
{
    private readonly Grid _root = new();
    private RectI _windowRect;
    private double _scale = 1.0;

    public BarWindow(RectI windowRect, RectI barRectInWindow, BarSurface surface)
    {
        Surface = surface;
        _windowRect = windowRect;

        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        ShowInTaskbar = false;
        ShowActivated = false;
        Topmost = false;
        ResizeMode = ResizeMode.NoResize;
        Title = "MehrBar";
        SnapsToDevicePixels = true;
        UseLayoutRounding = true;

        // Provisional size (100 % scaling); corrected to exact pixels once the DPI is known.
        Width = windowRect.Width;
        Height = windowRect.Height;
        _root.Width = windowRect.Width;
        _root.Height = windowRect.Height;
        _root.Children.Add(surface);
        Surface.BarRect = ToRect(barRectInWindow);
        Content = _root;
    }

    /// <summary>Win32 side of the window (z-order, bounds).</summary>
    internal BarHwnd Native { get; } = new();

    /// <summary>The drawing surface.</summary>
    internal BarSurface Surface { get; }

    /// <summary>Moves and resizes the window and the bar inside it, in pixels.</summary>
    public void ApplyBounds(RectI windowRect, RectI barRectInWindow)
    {
        _windowRect = windowRect;
        SetPixelSize(windowRect);
        Surface.BarRect = ToRect(barRectInWindow);
        Native.SetBounds(windowRect);
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        var source = (HwndSource)PresentationSource.FromVisual(this)!;
        _scale = source.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        _root.LayoutTransform = new ScaleTransform(1.0 / _scale, 1.0 / _scale);
        SetPixelSize(_windowRect);

        Native.Attach(source, _windowRect);
    }

    private void SetPixelSize(RectI windowRect)
    {
        _root.Width = windowRect.Width;
        _root.Height = windowRect.Height;
        Width = windowRect.Width / _scale;
        Height = windowRect.Height / _scale;
    }

    private static Rect ToRect(RectI r) => new(r.Left, r.Top, r.Width, r.Height);
}
