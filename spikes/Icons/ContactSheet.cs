using System.Globalization;
using System.IO; // not an implicit using in WPF projects
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace MehrBar.Spikes.Icons;

/// <summary>
/// Eye-check sheet for Stephan (H7): every returned icon on a light, a dark and a checkerboard background at
/// 32 px and 121 px (the rest and the zoomed slot size), scaled with HighQuality. The sheet contains the icons
/// of his own machine, so it is only ever written to the ignored private results folder.
/// </summary>
internal static class ContactSheet
{
    private const int Small = 32;
    private const int Large = 121;
    private const int Pad = 8;
    private const int LabelWidth = 190;

    public static void Write(IReadOnlyList<CaseOutcome> results, string path)
    {
        // One tile per case and requested size: the STA extraction when it succeeded (the thread model the
        // measurement recommends, and the only one that renders .url icons), else the first success.
        var tiles = new List<(string Label, BitmapSource Image)>();
        var order = new List<string>();
        var chosen = new Dictionary<string, CaseOutcome>();
        foreach (CaseOutcome r in results)
        {
            if (!r.Result.Ok)
            {
                continue;
            }

            string key = r.Case.Name + "@" + r.Requested;
            if (!chosen.TryGetValue(key, out CaseOutcome? existing))
            {
                order.Add(key);
                chosen[key] = r;
            }
            else if (r.Apartment == Apartment.Sta && existing.Apartment != Apartment.Sta)
            {
                chosen[key] = r;
            }
        }

        foreach (string key in order)
        {
            CaseOutcome r = chosen[key];
            string label = r.Case.Name + (r.Requested == 256 ? "" : " @" + r.Requested) + " " + r.Result.Width + "x" +
                r.Result.Height + " " + r.Apartment.ToString().ToUpperInvariant();
            tiles.Add((label, IconMetrics.ToPbgra32(r.Result.Bgra!, r.Result.Width, r.Result.Height)));
        }

        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                Render(tiles, path);
            }
            catch (Exception ex)
            {
                failure = ex;
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure != null)
        {
            throw new InvalidOperationException("contact sheet failed: " + failure.GetType().Name);
        }
    }

    private static void Render(List<(string Label, BitmapSource Image)> tiles, string path)
    {
        int cell = Small + Pad + Large + 2 * Pad;
        int rowHeight = Large + 2 * Pad;
        int width = LabelWidth + 3 * cell;
        int height = Math.Max(1, tiles.Count) * rowHeight;

        Color[] backgrounds = { Color.FromRgb(0xF0, 0xF0, 0xF0), Color.FromRgb(0x20, 0x20, 0x20), Colors.White };
        var visual = new DrawingVisual();
        RenderOptions.SetBitmapScalingMode(visual, BitmapScalingMode.HighQuality);
        using (DrawingContext dc = visual.RenderOpen())
        {
            dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
            for (int row = 0; row < tiles.Count; row++)
            {
                double top = row * rowHeight;
                var label = new FormattedText(
                    tiles[row].Label,
                    CultureInfo.InvariantCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Segoe UI"),
                    12,
                    Brushes.Black,
                    1.0);
                dc.DrawText(label, new Point(Pad, top + Pad));

                for (int bg = 0; bg < 3; bg++)
                {
                    double left = LabelWidth + bg * cell;
                    var area = new Rect(left, top, cell, rowHeight);
                    if (bg == 2)
                    {
                        DrawChecker(dc, area);
                    }
                    else
                    {
                        dc.DrawRectangle(new SolidColorBrush(backgrounds[bg]), null, area);
                    }

                    dc.DrawImage(tiles[row].Image, new Rect(left + Pad, top + Pad + (Large - Small), Small, Small));
                    dc.DrawImage(tiles[row].Image, new Rect(left + Pad + Small + Pad, top + Pad, Large, Large));
                }
            }
        }

        var target = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        target.Render(visual);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(target));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        using FileStream stream = File.Create(path);
        encoder.Save(stream);
    }

    private static void DrawChecker(DrawingContext dc, Rect area)
    {
        const int square = 8;
        Brush light = Brushes.White;
        Brush grey = new SolidColorBrush(Color.FromRgb(0xC8, 0xC8, 0xC8));
        for (int y = 0; y * square < area.Height; y++)
        {
            for (int x = 0; x * square < area.Width; x++)
            {
                double w = Math.Min(square, area.Width - x * square);
                double h = Math.Min(square, area.Height - y * square);
                dc.DrawRectangle((x + y) % 2 == 0 ? light : grey, null,
                    new Rect(area.Left + x * square, area.Top + y * square, w, h));
            }
        }
    }
}
