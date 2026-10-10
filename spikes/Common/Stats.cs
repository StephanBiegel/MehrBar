namespace MehrBar.Spikes.Common;

public sealed record StatsSummary(int Count, double Median, double P95, double P99, double Mean, double Max);

public static class Stats
{
    /// <summary>Nearest-rank percentile on a sorted copy: index = ceil(p/100*n)-1, clamped. Empty input gives NaN.</summary>
    public static double Percentile(IReadOnlyList<double> values, double p)
    {
        if (values.Count == 0)
        {
            return double.NaN;
        }

        double[] sorted = values.ToArray();
        Array.Sort(sorted);
        int index = (int)Math.Ceiling(p / 100.0 * sorted.Length) - 1;
        index = Math.Clamp(index, 0, sorted.Length - 1);
        return sorted[index];
    }

    /// <summary>Summary of a sample; empty input returns Count 0 and NaN values, never throws.</summary>
    public static StatsSummary Summarize(IEnumerable<double> values)
    {
        double[] list = values.ToArray();
        if (list.Length == 0)
        {
            return new StatsSummary(0, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN);
        }

        return new StatsSummary(
            list.Length,
            Percentile(list, 50),
            Percentile(list, 95),
            Percentile(list, 99),
            list.Average(),
            list.Max());
    }
}
