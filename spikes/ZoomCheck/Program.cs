using System.Globalization;
using System.Text;
using MehrBar.Core.Layout;
using MehrBar.Tests.Fixtures;

// Report-only: predicted vs expected bar edges and separator lines for every reference row, both anchors, plus a
// sensitivity block. It reads the measured fixture and never writes into it.

const double Tolerance = 6;
var inv = CultureInfo.InvariantCulture;

var variants = new List<Variant>
{
    new("baseline", RocketDockReference.MeasuredRadius, 0, true, RocketDockReference.ZoomSize),
    new("r-0.25", RocketDockReference.MeasuredRadius - 0.25, 0, true, RocketDockReference.ZoomSize),
    new("r+0.25", RocketDockReference.MeasuredRadius + 0.25, 0, true, RocketDockReference.ZoomSize),
    new("r=2.41 (measured min)", 2.41, 0, true, RocketDockReference.ZoomSize),
    new("r=2.72 (measured max)", 2.72, 0, true, RocketDockReference.ZoomSize),
    new("f-0.02", RocketDockReference.MeasuredRadius, -0.02, true, RocketDockReference.ZoomSize),
    new("f+0.02", RocketDockReference.MeasuredRadius, 0.02, true, RocketDockReference.ZoomSize),
    new("fixed-separators", RocketDockReference.MeasuredRadius, 0, false, RocketDockReference.ZoomSize),
    new("zoomsize-4", RocketDockReference.MeasuredRadius, 0, true, RocketDockReference.ZoomSize - 4),
    new("zoomsize+4", RocketDockReference.MeasuredRadius, 0, true, RocketDockReference.ZoomSize + 4),
};

var csv = new StringBuilder("row,anchor,edge,expected,predicted,residual,cropped,r,fraction,variant\n");
var summary = new List<string>();

foreach (var variant in variants)
{
    var items = RocketDockReference.BuildRestItems(variant.SeparatorsZoom);
    var p = new ZoomParams(RocketDockReference.IconSize, variant.ZoomSize, variant.Radius);

    foreach (var anchor in new[] { ZoomAnchorMode.Cursor, ZoomAnchorMode.Center })
    {
        double worstUncropped = 0;      // largest |residual| over edges and separator lines that are real values
        double worstBoundExcess = 0;    // how far a cropped left edge lies right of its bound (+6 tolerance not applied)
        foreach (var row in RocketDockReference.Rows)
        {
            double fraction = Math.Clamp(row.PointerFraction + variant.FractionDelta, 0, 1);
            var cursorItem = items[row.PointerItemIndex];
            double pointer = cursorItem.Start + fraction * cursorItem.Length;
            ZoomResult r = ZoomEngine.Compute(items, RocketDockReference.Pitch, p, pointer, anchor);

            double before = RocketDockReference.RestStart - r.ExtraBefore;
            double after = RocketDockReference.RestEnd + r.ExtraAfter;

            void Add(string edge, double expected, double predicted, bool cropped)
            {
                double residual = predicted - expected;
                csv.Append(row.Name.Replace(',', ';')).Append(',').Append(anchor).Append(',').Append(edge).Append(',')
                    .Append(expected.ToString("F1", inv)).Append(',').Append(predicted.ToString("F1", inv)).Append(',')
                    .Append(residual.ToString("F1", inv)).Append(',').Append(cropped ? "yes" : "no").Append(',')
                    .Append(variant.Radius.ToString("F2", inv)).Append(',').Append(fraction.ToString("F3", inv)).Append(',')
                    .Append(variant.Name).Append('\n');
                if (cropped)
                {
                    worstBoundExcess = Math.Max(worstBoundExcess, predicted - expected);
                }
                else
                {
                    worstUncropped = Math.Max(worstUncropped, Math.Abs(residual));
                }
            }

            Add("before", row.ExpectedBefore, before, row.BeforeCropped);
            Add("after", row.ExpectedAfter, after, false);
            for (int s = 0; s < RocketDockReference.SeparatorIndices.Length; s++)
            {
                int i = RocketDockReference.SeparatorIndices[s];
                Add($"separator{s + 1}", row.SeparatorCentres[s], r.DisplayedStarts[i] + r.DisplayedLengths[i] / 2, false);
            }
        }

        bool pass = worstUncropped <= Tolerance && worstBoundExcess <= Tolerance;
        summary.Add(
            $"{variant.Name,-24} {anchor,-6} max uncropped residual {worstUncropped,5:F1} px, " +
            $"crop-bound excess {worstBoundExcess,5:F1} px -> {(pass ? "within +-6" : "MISSES +-6")}");
    }
}

string root = FindRepoRoot();
string path = Path.Combine(root, "spikes", "results", "q5-zoom-residuals.csv");
Directory.CreateDirectory(Path.GetDirectoryName(path)!);
File.WriteAllText(path, csv.ToString(), new UTF8Encoding(false));

Console.WriteLine($"Q5 residuals (tolerance +-{Tolerance} px) -> {path}");
foreach (string line in summary)
{
    Console.WriteLine(line);
}

static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir != null && !File.Exists(Path.Combine(dir.FullName, "MehrBar.sln")))
    {
        dir = dir.Parent;
    }

    return dir?.FullName ?? throw new InvalidOperationException("MehrBar.sln not found above " + AppContext.BaseDirectory);
}

internal sealed record Variant(string Name, double Radius, double FractionDelta, bool SeparatorsZoom, double ZoomSize);
