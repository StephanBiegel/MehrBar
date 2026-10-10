namespace MehrBar.Core.Layout;

/// <summary>
/// The zoom falloff: how strongly an item grows depending on its distance from the pointer.
/// </summary>
public static class ZoomKernel
{
    /// <summary>
    /// Cosine kernel <c>(cos(pi * d / r) + 1) / 2</c> for <c>d &lt; r</c>, otherwise 0.
    /// </summary>
    /// <param name="distanceInPitches">
    /// Distance between the pointer and the item's centre <b>at rest</b>, in slot pitches (never the displayed
    /// position, otherwise the shift feeds back into the distance and the bar starts to wobble).
    /// </param>
    /// <param name="radius">Zoom radius in slot pitches; 0 or less disables the zoom.</param>
    /// <returns>1 under the pointer, falling to 0 at the radius.</returns>
    public static double Factor(double distanceInPitches, double radius)
    {
        double d = Math.Abs(distanceInPitches);
        if (radius <= 0 || d >= radius)
        {
            return 0;
        }

        return (Math.Cos(Math.PI * d / radius) + 1) / 2;
    }
}
