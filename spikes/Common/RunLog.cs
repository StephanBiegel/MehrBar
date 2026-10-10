using System.Diagnostics;
using System.IO; // not an implicit using in WPF projects
using System.Text;

namespace MehrBar.Spikes.Common;

/// <summary>
/// CSV logger with the columns ts_ms,case,variant,guard,event,detail. The timestamp is the
/// number of milliseconds since the process started; every line is flushed immediately so that
/// a crash or a kill never loses the rows an oracle depends on. Fields follow RFC 4180 quoting.
/// </summary>
public sealed class RunLog : IDisposable
{
    public const string Header = "ts_ms,case,variant,guard,event,detail";

    private readonly object _gate = new();
    private readonly StreamWriter _writer;
    private readonly Stopwatch _clock = Stopwatch.StartNew();
    private readonly double _startOffsetMs;

    public RunLog(string path)
    {
        Path = System.IO.Path.GetFullPath(path);
        string? dir = System.IO.Path.GetDirectoryName(Path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        // Share read access so a driver can tail the file while the host is running.
        var stream = new FileStream(Path, FileMode.Create, FileAccess.Write, FileShare.ReadWrite);
        _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true, NewLine = "\n" };
        _writer.WriteLine(Header);

        using var self = Process.GetCurrentProcess();
        _startOffsetMs = Math.Max(0, (DateTime.Now - self.StartTime).TotalMilliseconds);
    }

    public string Path { get; }

    /// <summary>Milliseconds since process start.</summary>
    public long NowMs => (long)(_startOffsetMs + _clock.Elapsed.TotalMilliseconds);

    public void Write(string @case, string variant, string guard, string evt, string detail)
    {
        string line = string.Join(
            ',',
            NowMs.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Quote(@case),
            Quote(variant),
            Quote(guard),
            Quote(evt),
            Quote(detail));

        lock (_gate)
        {
            _writer.WriteLine(line);
        }
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _writer.Dispose();
        }
    }

    private static string Quote(string field)
    {
        if (field.IndexOfAny(new[] { ',', '"', '\r', '\n' }) < 0)
        {
            return field;
        }

        return "\"" + field.Replace("\"", "\"\"") + "\"";
    }
}
