using System.Windows;
using System.Windows.Media;

namespace MehrBar.App.Bars;

/// <summary>
/// Draws the whole bar in one <see cref="OnRender"/> (R1, 01-SPIKE-render-cost.md), in window pixels: the alpha-1
/// plate that makes the bar a hit target (D-02), then the plain semi-transparent default background (D-07). Nothing is
/// drawn per frame at rest; the surface only redraws when its rectangle changes.
/// </summary>
internal sealed class BarSurface : FrameworkElement
{
    private const double CornerRadius = 8;

    private static readonly Brush BackgroundBrush = Frozen(new SolidColorBrush(Color.FromArgb(0xB3, 0x20, 0x20, 0x20)));
    private static readonly Pen BorderPen = Frozen(new Pen(Frozen(new SolidColorBrush(Color.FromArgb(0x33, 0xFF, 0xFF, 0xFF))), 1));
    private static readonly Pen OutlinePen = Frozen(new Pen(Frozen(new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0xFF))), 1));

    private Brush _plateBrush = Frozen(new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)));
    private byte _plateAlpha = 1;
    private Rect _barRect = Rect.Empty;

    /// <summary>Alpha of the hit plate (1 by default; 4–8 is the measured fallback if a drop target needs more).</summary>
    public byte PlateAlpha
    {
        get => _plateAlpha;
        set
        {
            if (value == _plateAlpha)
            {
                return;
            }

            _plateAlpha = value;
            _plateBrush = Frozen(new SolidColorBrush(Color.FromArgb(value, 0, 0, 0)));
            InvalidateVisual();
        }
    }

    /// <summary>Draws a 1 px frame around the plate for hand checks.</summary>
    public bool ShowPlateOutline { get; init; }

    /// <summary>Bar rectangle inside the window, in pixels.</summary>
    public Rect BarRect
    {
        get => _barRect;
        set
        {
            if (value == _barRect)
            {
                return;
            }

            _barRect = value;
            InvalidateVisual();
        }
    }

    protected override void OnRender(DrawingContext dc)
    {
        if (_barRect.IsEmpty)
        {
            return;
        }

        dc.DrawRectangle(_plateBrush, null, _barRect);

        var inner = new Rect(_barRect.X + 0.5, _barRect.Y + 0.5, _barRect.Width - 1, _barRect.Height - 1);
        dc.DrawRoundedRectangle(BackgroundBrush, BorderPen, inner, CornerRadius, CornerRadius);

        if (ShowPlateOutline)
        {
            dc.DrawRectangle(null, OutlinePen, inner);
        }
    }

    private static T Frozen<T>(T freezable)
        where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }
}
