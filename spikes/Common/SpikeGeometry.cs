using Windows.Win32;
using Windows.Win32.UI.WindowsAndMessaging;

namespace MehrBar.Spikes.Common;

/// <summary>Integer rectangle without any WPF or Win32 dependency (shared by host and driver).</summary>
public readonly record struct IntRect(int X, int Y, int Width, int Height)
{
    public int Right => X + Width;

    public int Bottom => Y + Height;

    public bool Contains(int x, int y) => x >= X && x < Right && y >= Y && y < Bottom;

    public IntRect Offset(int dx, int dy) => new(X + dx, Y + dy, Width, Height);
}

/// <summary>
/// Fixed geometry shared by the WindowHost (bar under test) and the Driver (oracles).
/// All rectangles and probe points are window-relative pixels of the bar window unless noted.
/// </summary>
public static class SpikeGeometry
{
    public const int WindowWidth = 1900;

    public const int WindowHeight = 250;

    public const int SentinelSize = 6;

    /// <summary>Colours as 0xRRGGBB.</summary>
    public const int SentinelRgb = 0xFF00FF;

    public const int CoverRgb = 0x00FFFF;

    public static (int Width, int Height) WindowSize => (WindowWidth, WindowHeight);

    /// <summary>The alpha-1 plate that stands for the bar.</summary>
    public static IntRect Plate { get; } = new(650, 8, 600, 56);

    /// <summary>Centres of the five sentinel squares, inside the plate.</summary>
    public static IReadOnlyList<(int X, int Y)> ProbeOffsets { get; } =
        new[] { (710, 36), (830, 36), (950, 36), (1070, 36), (1190, 36) };

    /// <summary>Witness rectangle that covers all five probes.</summary>
    public static IntRect WitnessFull { get; } = new(600, 0, 700, 120);

    /// <summary>Witness rectangle that covers probes 3 and 4 only.</summary>
    public static IntRect WitnessPartial { get; } = new(900, 0, 260, 120);

    /// <summary>Top centre of the primary monitor in screen pixels (y = 0).</summary>
    public static (int X, int Y) DefaultWindowPos(int width = WindowWidth)
    {
        int screenWidth = PInvoke.GetSystemMetrics(SYSTEM_METRICS_INDEX.SM_CXSCREEN);
        return ((screenWidth - width) / 2, 0);
    }
}
