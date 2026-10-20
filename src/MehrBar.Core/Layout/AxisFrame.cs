namespace MehrBar.Core.Layout;

/// <summary>
/// Maps window pixels to the bar axes and back: u runs along the edge (x for top/bottom, y for left/right), v away from
/// the edge (0 on the edge side). The rest of Core thinks only in (u, v).
/// </summary>
public readonly record struct AxisFrame(BarEdge Edge)
{
    public (double U, double V) ToAxis(double x, double y, double windowWidth, double windowHeight) => Edge switch
    {
        BarEdge.Top => (x, y),
        BarEdge.Bottom => (x, windowHeight - y),
        BarEdge.Left => (y, x),
        _ => (y, windowWidth - x),
    };

    public (double X, double Y) FromAxis(double u, double v, double windowWidth, double windowHeight) => Edge switch
    {
        BarEdge.Top => (u, v),
        BarEdge.Bottom => (u, windowHeight - v),
        BarEdge.Left => (v, u),
        _ => (windowWidth - v, u),
    };
}
